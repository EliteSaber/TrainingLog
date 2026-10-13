using TrainingLog.Core;
using TrainingLog.Core.Models;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Окно справочника. Импорт пишет в базу мимо модели окна, поэтому модель обязана перечитывать
/// справочник сама: иначе правка выглядела бы несработавшей.
/// </summary>
public sealed class ExercisesViewModelTests
{
    [Fact]
    public async Task LoadAsync_ПоказываетСправочник()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });

        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Жим", "Тяга"], viewModel.Visible.Select(exercise => exercise.Name));
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task ImportCommand_ПослеИмпорта_ПеречитываетСправочник()
    {
        using var database = new TemporaryDatabase();
        var windowService = new FakeWindowService { ImportResult = true };
        var viewModel = new ExercisesViewModel(database.Repository, windowService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Так же, как пишет окно импорта: модель окна справочника об этой записи не знает.
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        await viewModel.ImportCommand.ExecuteAsync(null);

        Assert.Equal(1, windowService.ImportCalls);
        Assert.Equal("Жим", Assert.Single(viewModel.Visible).Name);
    }

    [Fact]
    public async Task ImportCommand_ПриОтказе_СписокНеПеречитывается()
    {
        using var database = new TemporaryDatabase();
        var windowService = new FakeWindowService { ImportResult = false };
        var viewModel = new ExercisesViewModel(database.Repository, windowService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        await viewModel.ImportCommand.ExecuteAsync(null);

        Assert.Equal(1, windowService.ImportCalls);
        Assert.Empty(viewModel.Visible);
    }

    /// <summary>
    /// Выгрузка отражает справочник, а не то, что показано на экране: поиск и обратная сортировка
    /// не должны менять файл, иначе одна и та же выгрузка давала бы разные файлы.
    /// </summary>
    [Fact]
    public async Task Export_ПишетВесьСправочникПоАлфавиту()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        await database.Repository.AddAsync(new Exercise { Name = "Приседание" });

        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.IsDescending = true;
        viewModel.FilterText = "ж";

        var path = TemporaryPath();

        try
        {
            viewModel.ExportCommand.Execute(path);

            Assert.True(viewModel.ExportSucceeded);
            Assert.Equal(3, viewModel.Count);
            Assert.Equal(["Жим", "Приседание", "Тяга"], ExerciseFileReader.ReadLines(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CanExport_ПустойСправочник_КнопкаПогашена()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.CanExport);
        Assert.False(viewModel.ExportCommand.CanExecute(null));

        viewModel.NewName = "Жим";
        await viewModel.AddCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanExport);
        Assert.True(viewModel.ExportCommand.CanExecute(null));
    }

    /// <summary>Проверка одна гасит и кнопку, и тело команды: Execute её не спрашивает (ловушка 46).</summary>
    [Fact]
    public async Task Export_ПустойСправочникВОбходПредиката_НичегоНеПишет()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        var path = TemporaryPath();

        try
        {
            viewModel.ExportCommand.Execute(path);

            Assert.False(viewModel.ExportSucceeded);
            Assert.False(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_БезПути_НичегоНеПишет()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        viewModel.NewName = "Жим";

        viewModel.ExportCommand.Execute(null);

        Assert.False(viewModel.ExportSucceeded);
    }

    private static string TemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"TrainingLogExport-{Guid.NewGuid():N}.txt");
}
