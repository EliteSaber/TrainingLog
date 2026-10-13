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
}
