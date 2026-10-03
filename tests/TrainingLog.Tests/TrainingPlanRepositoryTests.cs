using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.Tests;

/// <summary>
/// Планы хранятся рядом со справочником упражнений, но наименование у них своё: «Полный» и
/// «День» не должны быть заняты, даже когда в базе есть упражнение с таким же названием.
/// </summary>
public sealed class TrainingPlanRepositoryTests
{
    [Fact]
    public async Task AddAsync_НовыйПлан_ПрисваиваетИдентификаторИОбрезаетНазвание()
    {
        using var database = new TemporaryDatabase();
        var plan = new TrainingPlan { Name = "  Полный  " };

        var outcome = await database.PlanRepository.AddAsync(plan);

        Assert.Equal(AddTrainingPlanOutcome.Added, outcome);
        Assert.True(plan.Id > 0);

        var all = await database.PlanRepository.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Полный", all[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddAsync_ПустоеНазвание_ВозвращаетNameIsEmpty(string name)
    {
        using var database = new TemporaryDatabase();

        var outcome = await database.PlanRepository.AddAsync(new TrainingPlan { Name = name });

        Assert.Equal(AddTrainingPlanOutcome.NameIsEmpty, outcome);
        Assert.Empty(await database.PlanRepository.GetAllAsync());
    }

    /// <summary>
    /// Наименования планов и упражнений живут в разных списках, поэтому пересечение имён
    /// между ними дублем не является.
    /// </summary>
    [Fact]
    public async Task AddAsync_НазваниеСовпадаетСупражнением_Добавляется()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var outcome = await database.PlanRepository.AddAsync(new TrainingPlan { Name = "Жим" });

        Assert.Equal(AddTrainingPlanOutcome.Added, outcome);
    }

    [Theory]
    [InlineData("жим", "Жим")]
    [InlineData("ПОЛНЫЙ", "полный")]
    public async Task AddAsync_ДубльПоРегиструКириллицы_ВозвращаетDuplicateName(string existing, string duplicate)
    {
        using var database = new TemporaryDatabase();
        await database.PlanRepository.AddAsync(new TrainingPlan { Name = existing });

        var outcome = await database.PlanRepository.AddAsync(new TrainingPlan { Name = duplicate });

        Assert.Equal(AddTrainingPlanOutcome.DuplicateName, outcome);
        Assert.Single(await database.PlanRepository.GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_ПеречисленныеУпражнения_СохраняетПорядокСЕдиницы()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        var deadlift = new Exercise { Name = "Тяга" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);
        await database.Repository.AddAsync(deadlift);

        // Порядок задаётся вызывающим, а не алфавитным: в базе он такой и должен остаться.
        var plan = new TrainingPlan { Name = "Ноги" };
        plan.AddExercise(deadlift);
        plan.AddExercise(bench);
        plan.AddExercise(squat);

        Assert.Equal(AddTrainingPlanOutcome.Added, await database.PlanRepository.AddAsync(plan));

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal([deadlift.Name, bench.Name, squat.Name], stored.Exercises.Select(exercise => exercise.Name));
        Assert.Equal([1, 2, 3], stored.PlanExercises.OrderBy(link => link.Order).Select(link => link.Order));
    }

    /// <summary>
    /// Номера в хранилище всегда плотные и с единицы, даже если план передали с пропусками:
    /// иначе сортировка по номеру однажды встанет произвольно.
    /// </summary>
    [Fact]
    public async Task AddAsync_НомераСПропусками_ПеренумеровываютсяПлотно()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "Ноги" };
        plan.PlanExercises.Add(new PlanExercise { Exercise = squat, Order = 4 });
        plan.PlanExercises.Add(new PlanExercise { Exercise = bench, Order = 10 });

        Assert.Equal(AddTrainingPlanOutcome.Added, await database.PlanRepository.AddAsync(plan));

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal([1, 2], stored.PlanExercises.OrderBy(link => link.Order).Select(link => link.Order));
        Assert.Equal([squat.Name, bench.Name], stored.Exercises.Select(exercise => exercise.Name));
    }

    /// <summary>
    /// Порядок задаётся слоем выше, поэтому упражнение, добавленное в план дважды, попадает в
    /// него один раз — на первом из мест.
    /// </summary>
    [Fact]
    public async Task AddAsync_УпражнениеДобавленоДважды_ПопадаетОдинРаз()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "Ноги" };
        plan.AddExercise(bench);
        plan.AddExercise(squat);
        plan.AddExercise(bench);

        Assert.Equal(AddTrainingPlanOutcome.Added, await database.PlanRepository.AddAsync(plan));

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal([bench.Name, squat.Name], stored.Exercises.Select(exercise => exercise.Name));
    }

    [Fact]
    public async Task AddAsync_БезУпражнений_ПланПустой()
    {
        using var database = new TemporaryDatabase();

        Assert.Equal(
            AddTrainingPlanOutcome.Added,
            await database.PlanRepository.AddAsync(new TrainingPlan { Name = "День отдыха" }));

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Empty(stored.Exercises);
    }

    [Fact]
    public async Task UpdateAsync_ПолностьюЗаменяетСостав()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        var deadlift = new Exercise { Name = "Тяга" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);
        await database.Repository.AddAsync(deadlift);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        var candidate = new TrainingPlan { Id = plan.Id, Name = "День" };
        candidate.AddExercise(bench);
        candidate.AddExercise(deadlift);

        var outcome = await database.PlanRepository.UpdateAsync(candidate);

        Assert.Equal(UpdateTrainingPlanOutcome.Updated, outcome);

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal(2, stored.Exercises.Count);
        Assert.Contains(stored.Exercises, e => e.Id == bench.Id);
        Assert.Contains(stored.Exercises, e => e.Id == deadlift.Id);
        Assert.DoesNotContain(stored.Exercises, e => e.Id == squat.Id);
    }

    /// <summary>
    /// Перестановка упражнений — это и есть порядок выполнения, поэтому он сохраняется, а не
    /// приводится к алфавитному.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ПерестановкаУпражнений_СохраняетНовыйПорядок()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        var deadlift = new Exercise { Name = "Тяга" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);
        await database.Repository.AddAsync(deadlift);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        var candidate = new TrainingPlan { Id = plan.Id, Name = "День" };
        candidate.AddExercise(deadlift);
        candidate.AddExercise(bench);
        candidate.AddExercise(squat);

        Assert.Equal(UpdateTrainingPlanOutcome.Updated, await database.PlanRepository.UpdateAsync(candidate));

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal(
            [deadlift.Name, bench.Name, squat.Name],
            stored.Exercises.Select(exercise => exercise.Name));
    }

    [Fact]
    public async Task UpdateAsync_ОчисткаСостава_ОставляетПланПустым()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var outcome = await database.PlanRepository.UpdateAsync(new TrainingPlan { Id = plan.Id, Name = "День" });

        Assert.Equal(UpdateTrainingPlanOutcome.Updated, outcome);
        Assert.Empty(Assert.Single(await database.PlanRepository.GetAllAsync()).Exercises);
    }

    /// <summary>
    /// Чекбоксы берутся из справочника при открытии окна, и упражнение могут удалить из другого
    /// окна, пока диалог открыт. Сохранять такое имя плана нельзя — у него просто нет строки.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_УпражнениеУдаленоИзСправочника_ПропускаетЕгоТихо()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        await database.Repository.DeleteAsync(bench.Id);

        var candidate = new TrainingPlan { Id = plan.Id, Name = "День" };
        candidate.AddExercise(bench);
        candidate.AddExercise(squat);

        var outcome = await database.PlanRepository.UpdateAsync(candidate);

        Assert.Equal(UpdateTrainingPlanOutcome.Updated, outcome);

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal(squat.Id, Assert.Single(stored.Exercises).Id);
    }

    [Fact]
    public async Task UpdateAsync_СохранениеБезИзменений_НеСчитаетсяДубликатом()
    {
        using var database = new TemporaryDatabase();
        var plan = new TrainingPlan { Name = "День" };
        await database.PlanRepository.AddAsync(plan);

        var outcome = await database.PlanRepository.UpdateAsync(new TrainingPlan { Id = plan.Id, Name = "День" });

        Assert.Equal(UpdateTrainingPlanOutcome.Updated, outcome);
        Assert.Single(await database.PlanRepository.GetAllAsync());
    }

    [Fact]
    public async Task UpdateAsync_НазваниеДругогоПлана_ВозвращаетDuplicateName()
    {
        using var database = new TemporaryDatabase();
        var first = new TrainingPlan { Name = "День" };
        var second = new TrainingPlan { Name = "Ноги" };
        await database.PlanRepository.AddAsync(first);
        await database.PlanRepository.AddAsync(second);

        var outcome = await database.PlanRepository.UpdateAsync(new TrainingPlan { Id = second.Id, Name = "ДЕНЬ" });

        Assert.Equal(UpdateTrainingPlanOutcome.DuplicateName, outcome);
        Assert.Contains(await database.PlanRepository.GetAllAsync(), p => p.Id == second.Id && p.Name == "Ноги");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_ПустоеНазвание_ВозвращаетNameIsEmpty(string name)
    {
        using var database = new TemporaryDatabase();
        var plan = new TrainingPlan { Name = "День" };
        await database.PlanRepository.AddAsync(plan);

        var outcome = await database.PlanRepository.UpdateAsync(new TrainingPlan { Id = plan.Id, Name = name });

        Assert.Equal(UpdateTrainingPlanOutcome.NameIsEmpty, outcome);
        Assert.Equal("День", Assert.Single(await database.PlanRepository.GetAllAsync()).Name);
    }

    [Fact]
    public async Task UpdateAsync_НеизвестныйИдентификатор_ВозвращаетNotFound()
    {
        using var database = new TemporaryDatabase();

        var outcome = await database.PlanRepository.UpdateAsync(new TrainingPlan { Id = 4242, Name = "День" });

        Assert.Equal(UpdateTrainingPlanOutcome.NotFound, outcome);
    }

    /// <summary>
    /// Ключевой тест схемы: у плана своя колонка NameKey, поэтому дубль по регистру блокируется
    /// на уровне базы, даже если предварительная проверка обойдена.
    /// </summary>
    [Fact]
    public async Task УникальныйИндексNameKey_БлокируетДубльПланаНаУровнеБазы()
    {
        using var database = new TemporaryDatabase();
        await database.PlanRepository.AddAsync(new TrainingPlan { Name = "День" });

        await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
        {
            await using var context = database.Factory.CreateDbContext();
            var duplicate = new TrainingPlan { Name = "день" };
            context.TrainingPlans.Add(duplicate);
            context.Entry(duplicate).Property(TrainingLogDbContext.NameKeyPropertyName).CurrentValue = "ДЕНЬ";

            await context.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task ExistsByNameAsync_НеУчитываетРегистр()
    {
        using var database = new TemporaryDatabase();
        await database.PlanRepository.AddAsync(new TrainingPlan { Name = "День" });

        Assert.True(await database.PlanRepository.ExistsByNameAsync("день"));
        Assert.False(await database.PlanRepository.ExistsByNameAsync("день отдыха"));
    }

    [Fact]
    public async Task DeleteAsync_УдаляетПланИЕгоСостав()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var other = new TrainingPlan { Name = "Ноги" };
        other.AddExercise(bench);
        await database.PlanRepository.AddAsync(other);

        Assert.True(await database.PlanRepository.DeleteAsync(plan.Id));

        var remaining = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal("Ноги", remaining.Name);
        Assert.Equal(bench.Id, Assert.Single(remaining.Exercises).Id);

        // Строка связи удалённого плана обязана уйти вместе с ним, а строка второго плана —
        // остаться: у таблицы PlanExercises нет своего DbSet, и каскад виден только отсюда.
        await using var context = database.Factory.CreateDbContext();
        var links = await context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM PlanExercises")
            .ToListAsync();

        Assert.Equal(1, links.Single());
    }

    [Fact]
    public async Task DeleteAsync_НеизвестныйИдентификатор_ВозвращаетFalse()
    {
        using var database = new TemporaryDatabase();

        Assert.False(await database.PlanRepository.DeleteAsync(4242));
    }

    /// <summary>
    /// Обратная сторона того же каскада: удаление упражнения убирает его из всех планов,
    /// иначе в раскрытом плане осталась бы ссылка на несуществующее упражнение.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_УпражнениеУдаляетсяИзВсехПланов()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var first = new TrainingPlan { Name = "Ноги" };
        first.AddExercise(squat);
        first.AddExercise(bench);
        await database.PlanRepository.AddAsync(first);

        var second = new TrainingPlan { Name = "День" };
        second.AddExercise(squat);
        await database.PlanRepository.AddAsync(second);

        Assert.True(await database.Repository.DeleteAsync(squat.Id));

        var plans = await database.PlanRepository.GetAllAsync();
        Assert.All(plans, plan => Assert.DoesNotContain(plan.Exercises, e => e.Id == squat.Id));
        Assert.Contains(Assert.Single(plans, p => p.Id == first.Id).Exercises, e => e.Id == bench.Id);
    }

    [Fact]
    public async Task GetAllAsync_ВозвращаетПланыСУпражнениями()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        var plan = new TrainingPlan { Name = "Ноги" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);
        await database.PlanRepository.AddAsync(new TrainingPlan { Name = "День" });

        var all = await database.PlanRepository.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("Приседание", Assert.Single(Assert.Single(all, p => p.Id == plan.Id).Exercises).Name);
    }

    /// <summary>
    /// Каскад удаляет строку связи упражнения, но номера у оставшихся не трогает: без
    /// нормализации при чтении единственное упражнение плана показывалось бы как «2».
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ПослеУдаленияУпражнения_НомераПлотныеСЕдиницы()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        await database.Repository.DeleteAsync(squat.Id);

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal([1], stored.PlanExercises.Select(link => link.Order));
        Assert.Equal([bench.Name], stored.Exercises.Select(exercise => exercise.Name));
    }

    [Fact]
    public async Task GetAllAsync_НомераСДырами_ПеренумеровываютсяБезСменыПорядка()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        // Номера с дырами: так выглядит план, у которого переставили упражнения, но состав
        // не менялся. Перенумерация обязана сохранить взаимный порядок.
        var plan = new TrainingPlan { Name = "День" };
        plan.PlanExercises.Add(new PlanExercise { Exercise = squat, Order = 5 });
        plan.PlanExercises.Add(new PlanExercise { Exercise = bench, Order = 9 });
        await database.PlanRepository.AddAsync(plan);

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());

        Assert.Equal([1, 2], stored.PlanExercises.OrderBy(link => link.Order).Select(link => link.Order));
        Assert.Equal([squat.Name, bench.Name], stored.Exercises.Select(exercise => exercise.Name));
    }

    [Fact]
    public async Task GetAllAsync_ПлотныеНомера_НеМеняются()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());

        Assert.Equal([1], stored.PlanExercises.Select(link => link.Order));
    }

    [Fact]
    public async Task GetAllAsync_Нормализация_ПланыНезависимы()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var first = new TrainingPlan { Name = "Ноги" };
        first.AddExercise(squat);
        await database.PlanRepository.AddAsync(first);

        var second = new TrainingPlan { Name = "День" };
        second.AddExercise(bench);
        second.AddExercise(squat);
        await database.PlanRepository.AddAsync(second);

        var all = await database.PlanRepository.GetAllAsync();

        Assert.Equal(
            [1],
            Assert.Single(all, plan => plan.Id == first.Id).PlanExercises.Select(link => link.Order));
        Assert.Equal(
            [1, 2],
            Assert.Single(all, plan => plan.Id == second.Id).PlanExercises.Select(link => link.Order));
    }
}
