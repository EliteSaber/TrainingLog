using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.Tests;

/// <summary>
/// Журнал тренировок. Три вещи проверяются здесь не только как результат, но и как инвариант:
/// порядок выполнения и порядок подходов (на них держится вся раскладка строки дня) и то,
/// что удаление упражнения или плана не стирает запись — она хранит копии названий.
/// </summary>
public sealed class TrainingSessionRepositoryTests
{
    [Fact]
    public async Task AddAsync_ДеньСупражнениямиИПодходами_СохраняетВсёИНормализуетНомера()
    {
        using var database = new TemporaryDatabase();
        var squat = await AddExerciseAsync(database, "Приседание");
        var bench = await AddExerciseAsync(database, "Жим");
        var plan = await AddPlanAsync(database, "Ноги", squat, bench);

        // Ссылка на план задаётся идентификатором: так её передаёт окно добавления дня, а навигация
        // остаётся незаполненной, и тянуть её в граф вставки не нужно.
        var session = new TrainingSession
        {
            Date = new DateOnly(2026, 10, 5),
            PlanId = plan.Id,
            PlanName = plan.Name,
        };

        // Порядок задаётся вызывающим, а номера с единицы проставляются при сохранении.
        var legs = session.AddExercise(bench, "сначала жим");
        legs.AddSet(8, 60m);
        legs.AddSet(6, 70m);

        var upper = session.AddExercise(squat);
        upper.AddSet(10, 40m);

        var outcome = await database.SessionRepository.AddAsync(session);

        Assert.Equal(AddTrainingSessionOutcome.Added, outcome);
        Assert.True(session.Id > 0);

        var stored = Assert.Single(await database.SessionRepository.GetAsync());

        Assert.Equal("Ноги", stored.PlanName);
        Assert.Equal(plan.Id, stored.PlanId);

        Assert.Collection(
            stored.Exercises.OrderBy(entry => entry.Order),
            first =>
            {
                Assert.Equal(1, first.Order);
                Assert.Equal(bench.Id, first.ExerciseId);
                Assert.Equal("Жим", first.ExerciseName);
                Assert.Equal("сначала жим", first.Notes);
            },
            second =>
            {
                Assert.Equal(2, second.Order);
                Assert.Equal(squat.Id, second.ExerciseId);
            });

        var sets = stored.Exercises.First(entry => entry.ExerciseId == bench.Id).Sets;

        Assert.Collection(
            sets.OrderBy(set => set.Order),
            first =>
            {
                Assert.Equal(1, first.Order);
                Assert.Equal(8, first.Repetitions);
                Assert.Equal(60m, first.Weight);
            },
            second =>
            {
                Assert.Equal(2, second.Order);
                Assert.Equal(6, second.Repetitions);
                Assert.Equal(70m, second.Weight);
            });
    }

    /// <summary>
    /// Вес — <c>decimal</c>, а SQLite хранит его как текст, поэтому значение обязано
    /// вернуться тем же самым: тихо обрезанный вес в журнале недопустим.
    /// </summary>
    [Fact]
    public async Task AddAsync_ДробныйВес_ВозвращаетсяБезОкругления()
    {
        using var database = new TemporaryDatabase();
        var squat = await AddExerciseAsync(database, "Приседание");

        var session = new TrainingSession { Date = new DateOnly(2026, 10, 5), PlanName = "День" };
        session.AddExercise(squat).AddSet(5, 82.5m);

        await database.SessionRepository.AddAsync(session);

        Assert.Equal(82.5m, Assert.Single(Assert.Single((await database.SessionRepository.GetAsync()).Single().Exercises).Sets).Weight);
    }

    [Fact]
    public async Task AddAsync_НаДатуУжеЕстьЗапись_ВозвращаетDateTaken()
    {
        using var database = new TemporaryDatabase();
        var date = new DateOnly(2026, 10, 5);

        Assert.Equal(
            AddTrainingSessionOutcome.Added,
            await database.SessionRepository.AddAsync(new TrainingSession { Date = date, PlanName = "День" }));

        var second = new TrainingSession { Date = date, PlanName = "День" };

        Assert.Equal(AddTrainingSessionOutcome.DateTaken, await database.SessionRepository.AddAsync(second));
        Assert.Single(await database.SessionRepository.GetAsync());
        Assert.Equal(0, second.Id);
    }

    [Fact]
    public async Task AddAsync_БезНазванияПлана_ВозвращаетPlanIsEmpty()
    {
        using var database = new TemporaryDatabase();

        var outcome = await database.SessionRepository.AddAsync(
            new TrainingSession { Date = new DateOnly(2026, 10, 5), PlanName = "   " });

        Assert.Equal(AddTrainingSessionOutcome.PlanIsEmpty, outcome);
        Assert.Empty(await database.SessionRepository.GetAsync());
    }

    [Fact]
    public async Task GetAsync_СвежиеСверху_АДатаПоУбыванию()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 1));
        await AddSessionAsync(database, new DateOnly(2026, 10, 5));
        await AddSessionAsync(database, new DateOnly(2026, 10, 3));

        var all = await database.SessionRepository.GetAsync();

        Assert.Equal(
            [new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 1)],
            all.Select(session => session.Date));
    }

    [Fact]
    public async Task GetAsync_Период_ОграничиваетПоДатамВключительно()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 1));
        await AddSessionAsync(database, new DateOnly(2026, 10, 3));
        await AddSessionAsync(database, new DateOnly(2026, 10, 5));

        var period = await database.SessionRepository.GetAsync(
            new DateOnly(2026, 10, 3),
            new DateOnly(2026, 10, 5));

        Assert.Equal(
            [new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 3)],
            period.Select(session => session.Date));
    }

    [Fact]
    public async Task GetByDateAsync_ЗаписиНет_ВозвращаетNull()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 1));

        Assert.NotNull(await database.SessionRepository.GetByDateAsync(new DateOnly(2026, 10, 1)));
        Assert.Null(await database.SessionRepository.GetByDateAsync(new DateOnly(2026, 10, 2)));
    }

    /// <summary>
    /// Правка дня переписывает запись целиком: ушедшие упражнения и подходы должны исчезнуть
    /// из базы, а не остаться в ней с прошлой редакции.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ПравкаДня_ПереписываетСоставИПодходы()
    {
        using var database = new TemporaryDatabase();
        var squat = await AddExerciseAsync(database, "Приседание");
        var bench = await AddExerciseAsync(database, "Жим");
        var plan = await AddPlanAsync(database, "Ноги", squat, bench);

        var session = new TrainingSession { Date = new DateOnly(2026, 10, 5), Plan = plan, PlanName = plan.Name };
        var first = session.AddExercise(squat);
        first.AddSet(10, 40m);
        first.AddSet(8, 45m);
        session.AddExercise(bench).AddSet(5, 60m);

        await database.SessionRepository.AddAsync(session);

        var edit = new TrainingSession { Id = session.Id, Date = session.Date, Plan = plan, PlanName = "  Ноги  " };
        var only = edit.AddExercise(bench);
        only.AddSet(5, 65m);
        only.AddSet(5, 70m);

        Assert.Equal(UpdateTrainingSessionOutcome.Updated, await database.SessionRepository.UpdateAsync(edit));

        var stored = Assert.Single(await database.SessionRepository.GetAsync());

        Assert.Equal("Ноги", stored.PlanName);
        Assert.Equal(bench.Id, Assert.Single(stored.Exercises).ExerciseId);
        Assert.Equal([65m, 70m], stored.Exercises.Single().Sets.OrderBy(set => set.Order).Select(set => set.Weight));

        // Отдельный контекст: каскад от базы проверяется на том, что осталось в самой таблице.
        await using var context = await database.Factory.CreateDbContextAsync();
        Assert.Equal(2, await context.TrainingSets.CountAsync());
        Assert.Equal(1, await context.ExerciseEntries.CountAsync());
    }

    /// <summary>
    /// Журнал переживает справочники: удаление упражнения обнуляет ссылку, но название
    /// остаётся, и строка дня по-прежнему подписана. Проверяется именно идентификатор:
    /// навигация при чтении не грузится, обнуление её ничего бы не доказало.
    /// </summary>
    [Fact]
    public async Task УдалениеУпражнения_ЗаписьОстаётсяСКопиейНазвания()
    {
        using var database = new TemporaryDatabase();
        var squat = await AddExerciseAsync(database, "Приседание");

        var session = new TrainingSession { Date = new DateOnly(2026, 10, 5), PlanName = "Ноги" };
        session.AddExercise(squat).AddSet(10, 40m);
        await database.SessionRepository.AddAsync(session);

        await database.Repository.DeleteAsync(squat.Id);

        var stored = Assert.Single(await database.SessionRepository.GetAsync());
        var entry = Assert.Single(stored.Exercises);

        Assert.Null(entry.ExerciseId);
        Assert.Equal("Приседание", entry.ExerciseName);
    }

    /// <summary>
    /// То же для плана: переименование и удаление не переписывают историю.
    /// </summary>
    [Fact]
    public async Task УдалениеПлана_ЗаписьОстаётсяСКопиейНазвания()
    {
        using var database = new TemporaryDatabase();
        var squat = await AddExerciseAsync(database, "Приседание");
        var plan = await AddPlanAsync(database, "Ноги", squat);

        var session = new TrainingSession { Date = new DateOnly(2026, 10, 5), Plan = plan, PlanName = plan.Name };
        session.AddExercise(squat).AddSet(10, 40m);
        await database.SessionRepository.AddAsync(session);

        await database.PlanRepository.DeleteAsync(plan.Id);

        var stored = Assert.Single(await database.SessionRepository.GetAsync());

        Assert.Null(stored.PlanId);
        Assert.Equal("Ноги", stored.PlanName);
        Assert.Equal("Приседание", Assert.Single(stored.Exercises).ExerciseName);
    }

    /// <summary>
    /// Упражнение, удалённое из справочника, пока окно добавления дня открыто, из новой
    /// записи выпадает — как и у плана: запись собирается по тому, что есть в базе.
    /// </summary>
    [Fact]
    public async Task AddAsync_УпражнениеУдаленоПокаОткрыто_ВыпадаетИзЗаписи()
    {
        using var database = new TemporaryDatabase();
        var squat = await AddExerciseAsync(database, "Приседание");

        var session = new TrainingSession { Date = new DateOnly(2026, 10, 5), PlanName = "Ноги" };
        session.AddExercise(squat).AddSet(10, 40m);

        await database.Repository.DeleteAsync(squat.Id);
        await database.SessionRepository.AddAsync(session);

        var entry = Assert.Single(Assert.Single(await database.SessionRepository.GetAsync()).Exercises);

        Assert.Null(entry.ExerciseId);
        Assert.Equal("Приседание", entry.ExerciseName);
        Assert.Equal(40m, Assert.Single(entry.Sets).Weight);
    }

    [Fact]
    public async Task DeleteAsync_ЗаписиНет_ВозвращаетFalse()
    {
        using var database = new TemporaryDatabase();
        var session = await AddSessionAsync(database, new DateOnly(2026, 10, 5));

        Assert.True(await database.SessionRepository.DeleteAsync(session.Id));
        Assert.False(await database.SessionRepository.DeleteAsync(session.Id));
        Assert.Empty(await database.SessionRepository.GetAsync());
    }

    private static async Task<Exercise> AddExerciseAsync(TemporaryDatabase database, string name)
    {
        var exercise = new Exercise { Name = name };
        await database.Repository.AddAsync(exercise);

        return exercise;
    }

    private static async Task<TrainingPlan> AddPlanAsync(
        TemporaryDatabase database,
        string name,
        params Exercise[] exercises)
    {
        var plan = new TrainingPlan { Name = name };

        foreach (var exercise in exercises)
        {
            plan.AddExercise(exercise);
        }

        await database.PlanRepository.AddAsync(plan);

        return plan;
    }

    private static async Task<TrainingSession> AddSessionAsync(TemporaryDatabase database, DateOnly date)
    {
        var session = new TrainingSession { Date = date, PlanName = "День" };

        await database.SessionRepository.AddAsync(session);

        return session;
    }
}