using TrainingLog.Core.Models;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Окно правки опирается на уведомление об <see cref="EditExerciseViewModel.Accepted"/>, чтобы
/// выставить DialogResult. Без этого уведомления правка сохраняется, а вызывающий об этом
/// не узнаёт, поэтому механизм проверяется здесь.
/// </summary>
public sealed class EditExerciseViewModelTests
{
    [Fact]
    public async Task AcceptAsync_ПослеУспешнойПравки_AcceptedСталTrueИПришлоУведомление()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);

        var viewModel = new EditExerciseViewModel(exercise, database.Repository);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        var notifications = TrackNotifications(viewModel);

        viewModel.Name = "Жим лёжа";
        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Contains(nameof(EditExerciseViewModel.Accepted), notifications);
        Assert.Equal("Жим лёжа", exercise.Name);
    }

    [Fact]
    public async Task AcceptAsync_ПриДубле_AcceptedОстаётсяFalseИУведомленияНет()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });

        var viewModel = new EditExerciseViewModel(exercise, database.Repository);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        // Проверка одна и та же гасит и кнопку, и команду.
        viewModel.Name = "тяга";
        Assert.False(viewModel.AcceptCommand.CanExecute(null));

        var notifications = TrackNotifications(viewModel);

        // AsyncRelayCommand.ExecuteAsync не спрашивает CanExecute, поэтому правило всё равно
        // проверяется репозиторием: Accepted подниматься не должен.
        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.False(viewModel.Accepted);
        Assert.DoesNotContain(nameof(EditExerciseViewModel.Accepted), notifications);
        Assert.Equal("Жим", exercise.Name);
    }

    [Fact]
    public async Task AcceptAsync_СМенойРегистраНазваниеМеняется()
    {
        using var database = new TemporaryDatabase();
        var exercise = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(exercise);

        var viewModel = new EditExerciseViewModel(exercise, database.Repository);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.Name = "жим";
        Assert.True(viewModel.AcceptCommand.CanExecute(null));

        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Equal("жим", exercise.Name);
    }

    private static List<string?> TrackNotifications(EditExerciseViewModel viewModel)
    {
        var notifications = new List<string?>();

        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        return notifications;
    }
}
