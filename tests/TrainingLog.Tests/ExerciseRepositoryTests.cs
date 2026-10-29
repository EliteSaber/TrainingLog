using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.Tests;

public sealed class ExerciseRepositoryTests
{
    private static readonly string[] AlphabeticalNames = ["Атлант", "Болт", "Присед", "Ящик"];
    private static readonly string[] ReverseAlphabeticalNames = ["Ящик", "Присед", "Болт", "Атлант"];

    [Fact]
    public async Task AddAsync_НовоеУпражнение_ПрисваиваетИдентификатор()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим лёжа" };

        var outcome = await database.Repository.AddAsync(exercise);

        Assert.Equal(AddExerciseOutcome.Added, outcome);
        Assert.True(exercise.Id > 0);

        var all = await database.Repository.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Жим лёжа", all[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task AddAsync_ПустоеНазвание_ВозвращаетNameIsEmpty(string name)
    {
        using var database = new TemporaryDatabase();

        var outcome = await database.Repository.AddAsync(new Exercise { Name = name });

        Assert.Equal(AddExerciseOutcome.NameIsEmpty, outcome);
        Assert.Empty(await database.Repository.GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_НазваниеОбрезается()
    {
        using var database = new TemporaryDatabase();

        var outcome = await database.Repository.AddAsync(new Exercise { Name = "  Приседание  " });

        Assert.Equal(AddExerciseOutcome.Added, outcome);
        var all = await database.Repository.GetAllAsync();
        Assert.Equal("Приседание", all[0].Name);
    }

    [Fact]
    public async Task AddAsync_ТочныйДубль_ВозвращаетDuplicateName()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });

        var outcome = await database.Repository.AddAsync(new Exercise { Name = "Тяга" });

        Assert.Equal(AddExerciseOutcome.DuplicateName, outcome);
        Assert.Single(await database.Repository.GetAllAsync());
    }

    /// <summary>SQLite складывает регистр только для ASCII, регистронезависимость даёт колонка NameKey.</summary>
    [Theory]
    [InlineData("жим", "Жим")]
    [InlineData("ЖИМ ЛЁЖА", "жим лёжа")]
    [InlineData("Приседание", "приседание")]
    public async Task AddAsync_ДубльПоРегиструКириллицы_ВозвращаетDuplicateName(string existing, string duplicate)
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = existing });

        var outcome = await database.Repository.AddAsync(new Exercise { Name = duplicate });

        Assert.Equal(AddExerciseOutcome.DuplicateName, outcome);
        Assert.Single(await database.Repository.GetAllAsync());
    }

    [Fact]
    public async Task ExistsByNameAsync_НеУчитываетРегистр()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        Assert.True(await database.Repository.ExistsByNameAsync("жим"));
        Assert.True(await database.Repository.ExistsByNameAsync("  ЖИМ  "));
        Assert.False(await database.Repository.ExistsByNameAsync("жимка"));
    }

    [Fact]
    public async Task ExistsByNameAsync_СExcludeId_НеСчитаетСвоёИмяДубликатом()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);

        Assert.True(await database.Repository.ExistsByNameAsync("Жим"));
        Assert.False(await database.Repository.ExistsByNameAsync("Жим", excludeId: exercise.Id));
    }

    [Fact]
    public async Task UpdateAsync_СохранениеБезИзменений_НеСчитаетсяДубликатом()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);

        var outcome = await database.Repository.UpdateAsync(exercise);

        Assert.Equal(UpdateExerciseOutcome.Updated, outcome);
        Assert.Single(await database.Repository.GetAllAsync());
    }

    [Fact]
    public async Task UpdateAsync_СменаРегистраСвоегоИмени_НеСчитаетсяДубликатом()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);

        var outcome = await database.Repository.UpdateAsync(new Exercise { Id = exercise.Id, Name = "жим" });

        Assert.Equal(UpdateExerciseOutcome.Updated, outcome);
        var all = await database.Repository.GetAllAsync();
        Assert.Equal("жим", all[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_НазваниеДругогоУпражнения_ВозвращаетDuplicateName()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        var outcome = await database.Repository.UpdateAsync(new Exercise { Id = squat.Id, Name = "ЖИМ" });

        Assert.Equal(UpdateExerciseOutcome.DuplicateName, outcome);

        var all = await database.Repository.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.Id == squat.Id && e.Name == "Приседание");
    }

    [Fact]
    public async Task UpdateAsync_НаНовоеУникальноеНазвание_Обновляет()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);

        var outcome = await database.Repository.UpdateAsync(new Exercise { Id = exercise.Id, Name = "Жим лёжа" });

        Assert.Equal(UpdateExerciseOutcome.Updated, outcome);
        var all = await database.Repository.GetAllAsync();
        Assert.Equal("Жим лёжа", all[0].Name);
        Assert.Equal(exercise.Id, all[0].Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_ПустоеНазвание_ВозвращаетNameIsEmpty(string name)
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);

        var outcome = await database.Repository.UpdateAsync(new Exercise { Id = exercise.Id, Name = name });

        Assert.Equal(UpdateExerciseOutcome.NameIsEmpty, outcome);
        var all = await database.Repository.GetAllAsync();
        Assert.Equal("Жим", all[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_НеизвестныйИдентификатор_ВозвращаетNotFound()
    {
        using var database = new TemporaryDatabase();

        var outcome = await database.Repository.UpdateAsync(new Exercise { Id = 4242, Name = "Жим" });

        Assert.Equal(UpdateExerciseOutcome.NotFound, outcome);
    }

    [Fact]
    public async Task DeleteAsync_УдаляетУпражнение()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });

        Assert.True(await database.Repository.DeleteAsync(exercise.Id));

        var all = await database.Repository.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Тяга", all[0].Name);
    }

    [Fact]
    public async Task DeleteAsync_НеизвестныйИдентификатор_ВозвращаетFalse()
    {
        using var database = new TemporaryDatabase();

        Assert.False(await database.Repository.DeleteAsync(4242));
    }

    [Fact]
    public async Task DeleteAsync_ОсвобождаетНазвание()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);
        await database.Repository.DeleteAsync(exercise.Id);

        var outcome = await database.Repository.AddAsync(new Exercise { Name = "жим" });

        Assert.Equal(AddExerciseOutcome.Added, outcome);
    }

    [Fact]
    public async Task GetAllAsync_ВозвращаетВсеУпражнения()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });
        await database.Repository.AddAsync(new Exercise { Name = "Приседание" });

        Assert.Equal(3, (await database.Repository.GetAllAsync()).Count);
    }

    [Fact]
    public async Task ImportAsync_ЗаписываетВесьСписок()
    {
        using var database = new TemporaryDatabase();

        var outcome = await database.Repository.ImportAsync(["Жим", "Тяга", "Приседание"], replaceExisting: false);

        Assert.Equal(ImportExercisesOutcome.Imported, outcome);

        var all = await database.Repository.GetAllAsync();
        Assert.Equal(["Жим", "Приседание", "Тяга"], all.Select(e => e.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Импорт одной транзакцией: частично записанный справочник после отказа хуже любого отказа,
    /// потому что потерялись бы упражнения вместе с планами, из которых они выпали.
    /// </summary>
    [Fact]
    public async Task ImportAsync_ДубльВСписке_ВозвращаетInvalidNameИНеПишетНичего()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var outcome = await database.Repository.ImportAsync(["Тяга", "тяга"], replaceExisting: false);

        Assert.Equal(ImportExercisesOutcome.InvalidName, outcome);
        Assert.Equal(["Жим"], (await database.Repository.GetAllAsync()).Select(e => e.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ImportAsync_ТолькоНегодныеСтроки_ВозвращаетNothingToImport(string name)
    {
        using var database = new TemporaryDatabase();

        var outcome = await database.Repository.ImportAsync([name], replaceExisting: false);

        Assert.Equal(ImportExercisesOutcome.NothingToImport, outcome);
        Assert.Empty(await database.Repository.GetAllAsync());
    }

    [Fact]
    public async Task ImportAsync_ПустойСписок_ВозвращаетNothingToImport()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var outcome = await database.Repository.ImportAsync([], replaceExisting: false);

        Assert.Equal(ImportExercisesOutcome.NothingToImport, outcome);
        Assert.Single(await database.Repository.GetAllAsync());
    }

    [Fact]
    public async Task ImportAsync_БезОчистки_ИмяЗанятое_ВозвращаетDuplicateName()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var outcome = await database.Repository.ImportAsync(["Жим", "Тяга"], replaceExisting: false);

        Assert.Equal(ImportExercisesOutcome.DuplicateName, outcome);
        Assert.Equal(["Жим"], (await database.Repository.GetAllAsync()).Select(e => e.Name));
    }

    [Fact]
    public async Task ImportAsync_СОчисткой_СправочникСтановитсяСпискомИзФайла()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });

        var outcome = await database.Repository.ImportAsync(["Приседание", "Атлант"], replaceExisting: true);

        Assert.Equal(ImportExercisesOutcome.Imported, outcome);

        var all = await database.Repository.GetAllAsync();
        Assert.Equal(["Атлант", "Приседание"], all.Select(e => e.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Имя из файла может совпасть с уже записанным — в режиме замены оно не отбрасывается.
    /// Удаление и вставка поэтому идут разными сохранениями: при одном EF отправил бы вставку
    /// раньше удаления, и новый «Жим» упёрся бы в уникальный индекс прежнего.
    /// </summary>
    [Fact]
    public async Task ImportAsync_СОчисткой_ИмяСовпадаетСУжеЗаписанным_ЗаменяетВместоОтказа()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var outcome = await database.Repository.ImportAsync(["Жим", "Тяга"], replaceExisting: true);

        Assert.Equal(ImportExercisesOutcome.Imported, outcome);

        var all = await database.Repository.GetAllAsync();
        Assert.Equal(["Жим", "Тяга"], all.Select(e => e.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Журнал переживает справочник: удаление упражнения обнуляет ссылку, а копия названия
    /// остаётся. Планы такого себе не позволяют — из них упражнение исчезает каскадом.
    /// </summary>
    [Fact]
    public async Task ImportAsync_СОчисткой_УпражнениеУходитИзПланаАВЖурналеОстаётсяНазвание()
    {
        using var database = new TemporaryDatabase();
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "Грудь" };
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        var session = new TrainingSession
        {
            Date = new DateOnly(2026, 10, 5),
            PlanId = plan.Id,
            PlanName = plan.Name,
        };
        session.AddExercise(bench).AddSet(8, 60m);
        await database.SessionRepository.AddAsync(session);

        var outcome = await database.Repository.ImportAsync(["Тяга"], replaceExisting: true);

        Assert.Equal(ImportExercisesOutcome.Imported, outcome);

        var storedPlan = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Empty(storedPlan.Exercises);

        var storedSession = Assert.Single(await database.SessionRepository.GetAsync());
        var entry = Assert.Single(storedSession.Exercises);

        Assert.Equal("Жим", entry.ExerciseName);
        Assert.Null(entry.ExerciseId);
    }

    /// <summary>
    /// Одно уведомление на всю запись: подписчик перечитывает справочник, и на каждое упражнение
    /// файла перечитывать его заново незачем.
    /// </summary>
    [Fact]
    public async Task Changed_Импорт_ПоднимаетСобытиеОдинРаз()
    {
        using var database = new TemporaryDatabase();
        var changes = TrackChanges(database.Repository);

        await database.Repository.ImportAsync(["Жим", "Тяга", "Приседание"], replaceExisting: true);

        Assert.Single(changes);
    }

    [Fact]
    public async Task Changed_НегодныйИмпорт_СобытияНеПоднимает()
    {
        using var database = new TemporaryDatabase();
        var changes = TrackChanges(database.Repository);

        await database.Repository.ImportAsync(["Жим", "Жим"], replaceExisting: false);
        await database.Repository.ImportAsync([], replaceExisting: false);

        Assert.Empty(changes);
    }

    /// <summary>Сортировка по кодам Unicode в SQLite совпадает с русским алфавитом.</summary>
    [Fact]
    public async Task Сортировка_ПоАлфавиту_РаботаетДляКириллицы()
    {
        using var database = new TemporaryDatabase();
        foreach (var name in new[] { "Ящик", "Болт", "Присед", "Атлант" })
        {
            await database.Repository.AddAsync(new Exercise { Name = name });
        }

        var all = await database.Repository.GetAllAsync();

        Assert.Equal(AlphabeticalNames, all.OrderBy(e => e.Name).Select(e => e.Name));
        Assert.Equal(ReverseAlphabeticalNames, all.OrderByDescending(e => e.Name).Select(e => e.Name));
    }

    /// <summary>
    /// Уникальный индекс NameKey — последний рубеж: он срабатывает даже если предварительная
    /// проверка в AddAsync была обойдена.
    /// </summary>
    [Fact]
    public async Task УникальныйИндексNameKey_БлокируетДубликатНаУровнеБазы()
    {
        using var database = new TemporaryDatabase();
        var first = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(first);

        await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
        {
            await using var context = database.Factory.CreateDbContext();
            var duplicate = new Exercise { Name = "жим" };
            context.Exercises.Add(duplicate);
            context.Entry(duplicate).Property(TrainingLogDbContext.NameKeyPropertyName).CurrentValue = "ЖИМ";

            await context.SaveChangesAsync();
        });
    }

    /// <summary>Без него переименование и удаление упражнения остались бы в соседнем окне незаметными.</summary>
    [Fact]
    public async Task Changed_УспешныеЗаписи_ПоднимаютСобытие()
    {
        using var database = new TemporaryDatabase();
        var changes = TrackChanges(database.Repository);

        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);
        await database.Repository.UpdateAsync(new Exercise { Id = exercise.Id, Name = "Жим лёжа" });
        await database.Repository.DeleteAsync(exercise.Id);

        Assert.Equal(3, changes.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Changed_ПустоеНазвание_СобытияНет(string name)
    {
        using var database = new TemporaryDatabase();
        var changes = TrackChanges(database.Repository);

        await database.Repository.AddAsync(new Exercise { Name = name });

        Assert.Empty(changes);
    }

    [Fact]
    public async Task Changed_ДубликатИНеизвестныйИдентификатор_СобытияНет()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);

        var changes = TrackChanges(database.Repository);

        await database.Repository.AddAsync(new Exercise { Name = "жим" });
        await database.Repository.UpdateAsync(new Exercise { Id = exercise.Id, Name = "  " });
        await database.Repository.UpdateAsync(new Exercise { Id = 4242, Name = "Тяга" });
        await database.Repository.DeleteAsync(4242);

        Assert.Empty(changes);
    }

    private static List<int> TrackChanges(IExerciseRepository repository)
    {
        var changes = new List<int>();

        repository.Changed += (_, _) => changes.Add(changes.Count);

        return changes;
    }
}
