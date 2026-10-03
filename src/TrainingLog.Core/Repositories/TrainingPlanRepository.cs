using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Реализация <see cref="ITrainingPlanRepository"/> поверх SQLite.
/// </summary>
public sealed class TrainingPlanRepository(IDbContextFactory<TrainingLogDbContext> contextFactory)
    : ITrainingPlanRepository
{
    public async Task<IReadOnlyList<TrainingPlan>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Порядок выполнения лежит в строках связи, поэтому состав упорядочивается и при
        // выборке, а не только при показе: иначе показ зависел бы от того, как база вернула
        // строки.
        return await context.TrainingPlans
            .AsNoTracking()
            .Include(plan => plan.PlanExercises.OrderBy(link => link.Order))
                .ThenInclude(link => link.Exercise)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AddTrainingPlanOutcome> AddAsync(TrainingPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var name = plan.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return AddTrainingPlanOutcome.NameIsEmpty;
        }

        if (await ExistsByNameAsync(name, excludeId: null, cancellationToken).ConfigureAwait(false))
        {
            return AddTrainingPlanOutcome.DuplicateName;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var exercises = await ResolveExistingAsync(context, plan, cancellationToken).ConfigureAwait(false);

        plan.Name = name;
        ReplaceExercises(plan, exercises);
        context.TrainingPlans.Add(plan);
        context.Entry(plan)
            .Property(TrainingLogDbContext.NameKeyPropertyName)
            .CurrentValue = TrainingLogDbContext.NormalizeNameKey(name);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Предварительная проверка могла разойтись с базой: та же проверка из другого окна
            // или процесса успела вставить строку. Уникальный индекс — последний рубеж.
            return AddTrainingPlanOutcome.DuplicateName;
        }

        return AddTrainingPlanOutcome.Added;
    }

    public async Task<UpdateTrainingPlanOutcome> UpdateAsync(
        TrainingPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var name = plan.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return UpdateTrainingPlanOutcome.NameIsEmpty;
        }

        if (await ExistsByNameAsync(name, plan.Id, cancellationToken).ConfigureAwait(false))
        {
            return UpdateTrainingPlanOutcome.DuplicateName;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var stored = await context.TrainingPlans
            .Include(p => p.PlanExercises)
            .FirstOrDefaultAsync(p => p.Id == plan.Id, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return UpdateTrainingPlanOutcome.NotFound;
        }

        var exercises = await ResolveExistingAsync(context, plan, cancellationToken).ConfigureAwait(false);

        stored.Name = name;
        context.Entry(stored)
            .Property(TrainingLogDbContext.NameKeyPropertyName)
            .CurrentValue = TrainingLogDbContext.NormalizeNameKey(name);
        ReplaceExercises(stored, exercises);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return UpdateTrainingPlanOutcome.DuplicateName;
        }

        plan.Name = name;

        return UpdateTrainingPlanOutcome.Updated;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var stored = await context.TrainingPlans
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return false;
        }

        // Строки связи удалятся каскадом от самой базы: в модели за это отвечает
        // DeleteBehavior.Cascade на обоих внешних ключах, заданный соглашением EF.
        context.TrainingPlans.Remove(stored);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var key = TrainingLogDbContext.NormalizeNameKey(name.Trim());

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.TrainingPlans
            .AsNoTracking()
            .AnyAsync(
                p => p.Id != excludeId
                     && EF.Property<string>(p, TrainingLogDbContext.NameKeyPropertyName) == key,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Находит в справочнике те упражнения, на которые ссылается план, и возвращает их
    /// в том же порядке.
    /// </summary>
    /// <remarks>
    /// Упражнение, удалённое из справочника, пока открыто окно правки, молча выпадает из
    /// состава: блокировать сохранение плана из-за чужого окна нельзя, а состав плана всё
    /// равно задаётся тем, что осталось в базе. Порядок оставшихся сохраняется — иначе
    /// перестановка, сделанная в окне, потеряла бы смысл.
    /// </remarks>
    private static async Task<List<Exercise>> ResolveExistingAsync(
        TrainingLogDbContext context,
        TrainingPlan plan,
        CancellationToken cancellationToken)
    {
        // Порядок приходит из ссылок плана, а не из коллекции: коллекция не упорядочена.
        // Группировка идёт по упражнению из ссылки, а не по свойству ExerciseId: до того как
        // сущности взял контекст, внешний ключ в связи ещё равен нулю, и группировка по нему
        // схлопнула бы все упражнения плана в одну строку. Повторы схлопываются по
        // идентификатору упражнения, иначе оно попало бы в план дважды.
        var requested = plan.PlanExercises
            .Where(link => link.Exercise is not null && link.Exercise.Id > 0)
            .GroupBy(link => link.Exercise.Id)
            .Select(group => (Id: group.Key, Order: group.Min(link => link.Order)))
            .OrderBy(item => item.Order)
            .Select(item => item.Id)
            .ToArray();

        if (requested.Length == 0)
        {
            return [];
        }

        var found = await context.Exercises
            .Where(exercise => requested.Contains(exercise.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byId = found.ToDictionary(exercise => exercise.Id);
        var resolved = new List<Exercise>(requested.Length);

        foreach (var id in requested)
        {
            if (byId.TryGetValue(id, out var exercise))
            {
                resolved.Add(exercise);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Переписывает состав плана, перенумеровывая упражнения с единицы по порядку,
    /// в котором они переданы.
    /// </summary>
    /// <remarks>
    /// Перенумерация нужна потому, что номера приходят из окна правки, где пользователь
    /// двигал упражнения кнопками: там могли остаться пропуски и совпадения. В баже дыр в
    /// нумерации быть не должно, иначе сортировка по нему однажды встанет произвольно.
    /// </remarks>
    private static void ReplaceExercises(TrainingPlan plan, IEnumerable<Exercise> exercises)
    {
        // Коллекция заменяется на месте, а не присваивается заново: у отслеживаемой навигации
        // EF не должно меняться сам экземпляр коллекции.
        plan.PlanExercises.Clear();

        var order = 1;

        foreach (var exercise in exercises)
        {
            plan.PlanExercises.Add(new PlanExercise { Exercise = exercise, Order = order++ });
        }
    }
}
