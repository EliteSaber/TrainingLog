using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Реализация <see cref="ITrainingSessionRepository"/> поверх SQLite.
/// </summary>
public sealed class TrainingSessionRepository(IDbContextFactory<TrainingLogDbContext> contextFactory)
    : ITrainingSessionRepository
{
    /// <summary>Код ошибки SQLite для нарушения ограничения: уникальный индекс, внешний ключ, NOT NULL.</summary>
    private const int SqliteConstraintErrorCode = 19;

    /// <summary>Имя таблицы записей журнала — нужно, чтобы отличить отказ по дате от других.</summary>
    private const string SessionsTableName = "TrainingSessions";
    public async Task<IReadOnlyList<TrainingSession>> GetAsync(
        DateOnly? fromInclusive = null,
        DateOnly? toInclusive = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Порядок выполнения и порядок подходов задаются при записи, а при выборке только
        // читаются: иначе показ зависел бы от того, как база вернула строки.
        var query = Readable(context.TrainingSessions.AsNoTracking());

        if (fromInclusive is { } from)
        {
            query = query.Where(session => session.Date >= from);
        }

        if (toInclusive is { } to)
        {
            query = query.Where(session => session.Date <= to);
        }

        return await query
            .OrderByDescending(session => session.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TrainingSession?> GetByDateAsync(
        DateOnly targetDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await Readable(context.TrainingSessions.AsNoTracking())
            .FirstOrDefaultAsync(session => session.Date == targetDate, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TrainingSession?> GetPreviousByPlanAsync(
        int planId,
        DateOnly before,
        CancellationToken cancellationToken = default)
    {
        // План без идентификатора ещё не сохранён, а значит и в журнале он быть не может:
        // запрос по нулевому идентификатору вернул бы первую запись без плана.
        if (planId <= 0)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Запись на дату уникальна, поэтому ближайшая прошедшая находится ровно одна, и взять
        // её — дело FirstOrDefault после сортировки по убыванию. Порядок упражнений и подходов
        // приходит тот же, что и при чтении дня, иначе полосы подходов разъехались бы по
        // порядку, а он и есть содержание подхода.
        return await Readable(context.TrainingSessions.AsNoTracking())
            .Where(session => session.PlanId == planId && session.Date < before)
            .OrderByDescending(session => session.Date)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AddTrainingSessionOutcome> AddAsync(
        TrainingSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var planName = session.PlanName?.Trim() ?? string.Empty;
        if (planName.Length == 0)
        {
            return AddTrainingSessionOutcome.PlanIsEmpty;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (await ExistsByDateAsync(context, session.Date, cancellationToken).ConfigureAwait(false))
        {
            return AddTrainingSessionOutcome.DateTaken;
        }

        // Запись собирается заново, а не присоединяется переданному объекту: у упражнений
        // в переданном объекте проставлены ссылками на строки, прочитанные другим контекстом,
        // и EF счёл бы их новыми и вставил повторно. Состав собирается из того, что ещё
        // есть в справочнике: упражнение, удалённое из окна правки, молча выпадает, как и
        // у плана, а его название остаётся в записи.
        var stored = new TrainingSession
        {
            Date = session.Date,
            PlanName = planName,
            Notes = session.Notes,
        };

        var exercises = await ResolveExercisesAsync(context, session, cancellationToken).ConfigureAwait(false);
        stored.Plan = await ResolvePlanAsync(context, session, cancellationToken).ConfigureAwait(false);

        // Идентификатор ставится явно, а не по навигации при фиксации изменений: план, удалённый
        // из справочника, должен оставить запись без ссылки, а не с чужим идентификатором.
        stored.PlanId = stored.Plan?.Id;

        foreach (var entry in BuildEntries(session, exercises))
        {
            stored.AddExercise(entry);
        }

        stored.NormalizeOrder();

        context.TrainingSessions.Add(stored);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsDateTaken(exception))
        {
            // Предварительная проверка могла разойтись с базой: запись на эту дату добавили
            // из другого окна, пока было открыто. Уникальный индекс — последний рубеж.
            return AddTrainingSessionOutcome.DateTaken;
        }

        session.Id = stored.Id;

        return AddTrainingSessionOutcome.Added;
    }

    public async Task<UpdateTrainingSessionOutcome> UpdateAsync(
        TrainingSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var planName = session.PlanName?.Trim() ?? string.Empty;
        if (planName.Length == 0)
        {
            return UpdateTrainingSessionOutcome.NotFound;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var stored = await context.TrainingSessions
            .Include(item => item.Exercises)
                .ThenInclude(entry => entry.Sets)
            .FirstOrDefaultAsync(item => item.Id == session.Id, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return UpdateTrainingSessionOutcome.NotFound;
        }

        var exercises = await ResolveExercisesAsync(context, session, cancellationToken).ConfigureAwait(false);

        stored.Date = session.Date;
        stored.PlanName = planName;
        stored.Notes = session.Notes;
        stored.Plan = await ResolvePlanAsync(context, session, cancellationToken).ConfigureAwait(false);
        stored.PlanId = stored.Plan?.Id;

        // Запись переписывается целиком, как состав плана: удалённые подходы и упражнения
        // уходят каскадом от самой базы, а новые приходят с нумерацией с единицы.
        stored.Exercises.Clear();

        foreach (var entry in BuildEntries(session, exercises))
        {
            stored.AddExercise(entry);
        }

        stored.NormalizeOrder();

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UpdateTrainingSessionOutcome.Updated;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var stored = await context.TrainingSessions
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return false;
        }

        // Упражнения и подходы удалятся каскадом от базы: в модели за это отвечает
        // DeleteBehavior.Cascade на обоих внешних ключах, заданный соглашением EF.
        context.TrainingSessions.Remove(stored);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Запрос, который читает запись вместе с упражнениями и подходами по порядку выполнения.
    /// </summary>
    private static IQueryable<TrainingSession> Readable(IQueryable<TrainingSession> query) =>
        query
            .Include(session => session.Exercises.OrderBy(entry => entry.Order))
                .ThenInclude(entry => entry.Sets.OrderBy(set => set.Order));

    private static async Task<bool> ExistsByDateAsync(
        TrainingLogDbContext context,
        DateOnly date,
        CancellationToken cancellationToken) =>
        await context.TrainingSessions
            .AsNoTracking()
            .AnyAsync(session => session.Date == date, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Возвращает план из базы, если он ещё там, иначе <c>null</c>: ссылка обнуляется,
    /// а название плана из записи при этом сохраняется.
    /// </summary>
    /// <remarks>
    /// Без <c>AsNoTracking</c> — иначе план вернулся бы отсоединённым, и добавление записи
    /// пометило бы весь достижимый граф как новый: <c>Add</c> вставил бы план повторно,
    /// упал бы на уникальном индексе наименования, и отказ выглядел бы как «дата занята».
    /// </remarks>
    private static async Task<TrainingPlan?> ResolvePlanAsync(
        TrainingLogDbContext context,
        TrainingSession session,
        CancellationToken cancellationToken)
    {
        // Берётся идентификатор, а не сам объект: объект у вызывающего отсоединённый, и в Add он
        // попал бы в граф как новый — планы добавляются повторно, а уникальный индекс
        // наименования роняет вставку. Идентификатор берётся из свойства, а ссылка на
        // объект — запасной путь для вызывающего, который задал только навигацию.
        var planId = session.PlanId ?? session.Plan?.Id;

        if (planId is not > 0)
        {
            return null;
        }

        return await context.TrainingPlans
            .FirstOrDefaultAsync(stored => stored.Id == planId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Находит в справочнике те упражнения, на которые ссылается запись, и возвращает их
    /// по идентификатору.
    /// </summary>
    /// <remarks>
    /// Повтор одного упражнения в записи не схлопывается: два подхода одного упражнения — это
    /// два столбца упражнения в строке дня, а не две одинаковые строки. Проверка повторов
    /// живёт в окне добавления дня, где она и видна пользователю.
    /// </remarks>
    private static async Task<Dictionary<int, Exercise>> ResolveExercisesAsync(
        TrainingLogDbContext context,
        TrainingSession session,
        CancellationToken cancellationToken)
    {
        // Идентификатор берётся из свойства, а не из ссылки на упражнение: ссылка у вызывающего
        // ведёт на отсоединённый объект, и добавлять его в граф нельзя.
        var ids = session.Exercises
            .Select(entry => entry.ExerciseId ?? entry.Exercise?.Id ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var found = await context.Exercises
            .Where(exercise => ids.Contains(exercise.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToDictionary(exercise => exercise.Id);
    }

    /// <summary>
    /// Проверяет, что отказ вставки — именно занятая дата, а не что-то другое.
    /// </summary>
    /// <remarks>
    /// Раньше любой <see cref="DbUpdateException"/> считался занятым числом, и это обернулось
    /// ложным диагнозом: план, который вернулся отсоединённым, добавлялся повторно, падал на
    /// уникальном индексе наименования — и сообщение говорило про дату. Отказ по чужой причине
    /// (например, упражнение удалили между чтением и записью) должен дойти до вызывающего
    /// как отказ, а не раствориться в «дата занята».
    /// </remarks>
    private static bool IsDateTaken(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: SqliteConstraintErrorCode,
            Message: var message,
        }
        && message.Contains($"IX_{SessionsTableName}_{nameof(TrainingSession.Date)}", StringComparison.Ordinal);

    /// <summary>
    /// Собирает упражнения записи заново, подставляя упражнения справочника там, где ссылка
    /// ещё действительна.
    /// </summary>
    private static IEnumerable<ExerciseEntry> BuildEntries(
        TrainingSession session,
        Dictionary<int, Exercise> exercises)
    {
        foreach (var source in session.Exercises)
        {
            var entry = new ExerciseEntry { ExerciseName = source.ExerciseName?.Trim() ?? string.Empty };

            // Ссылка проставляется только тем упражнениям, которые ещё есть в справочнике.
            // Иначе в запись ушёл бы внешний ключ на несуществующую строку и вставка упала бы
            // на ограничении: упражнение, удалённое из окна правки, выпадает из записи,
            // остаётся только название.
            var exerciseId = source.ExerciseId ?? source.Exercise?.Id;

            if (exerciseId is > 0 && exercises.TryGetValue(exerciseId.Value, out var stored))
            {
                entry.Exercise = stored;
                entry.ExerciseId = stored.Id;

                if (entry.ExerciseName.Length == 0)
                {
                    entry.ExerciseName = stored.Name;
                }
            }

            entry.Notes = source.Notes;

            foreach (var sourceSet in source.Sets)
            {
                entry.AddSet(sourceSet.Repetitions, sourceSet.Weight);
            }

            entry.NormalizeOrder();

            yield return entry;
        }
    }
}