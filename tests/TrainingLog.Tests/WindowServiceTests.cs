using System.Windows;
using System.Windows.Threading;
using TrainingLog.Core.Models;
using TrainingLog.Services;
using TrainingLog.ViewModels;
using TrainingLog.Windows;

namespace TrainingLog.Tests;

/// <summary>
/// Окна правки — единственные, как окно дня: повторное открытие не создаёт второе окно, а
/// выводит существующее вперёд. После закрытия следующий вызов создаёт окно заново.
/// </summary>
/// <remarks>
/// Тесту нужны настоящие окна: сервис работает с <see cref="Window.IsVisible"/> и закрытием.
/// окна уводятся за экран и не активируются, чтобы тест не мигал и не забирал фокус.
/// Неиспользуемые фабрики бросают исключение: если сервис их вызовет, тест честно упадёт.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class WindowServiceTests
{
    /// <summary>Повторное открытие не создаёт окно; после закрытия фабрика вызывается заново.</summary>
    [Fact]
    public void OpenEditExercise_ПовторноеОткрытие_ОкноНеСоздаётся()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var created = 0;
            EditExerciseWindow? window = null;

            var service = CreateService(editExercise: exercise =>
            {
                created++;

                return window = NewExerciseWindow(exercise, database, settings);
            });

            var exercise = new Exercise { Name = "Жим" };

            service.OpenEditExercise(exercise);
            service.OpenEditExercise(exercise);

            Assert.Equal(1, created);

            window!.Close();
            service.OpenEditExercise(exercise);

            Assert.Equal(2, created);

            window!.Close();
        });
    }

    /// <summary>Повторное открытие заменяет обратный вызов: вызывающий мог переоткрыться.</summary>
    [Fact]
    public void OpenEditExercise_ПовторноеОткрытие_ЗаменяетОбратныйВызов()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            EditExerciseWindow? window = null;
            EditExerciseViewModel? viewModel = null;

            var service = CreateService(editExercise: exercise =>
            {
                window = NewExerciseWindow(exercise, database, settings, out viewModel);

                return window;
            });

            var exercise = new Exercise { Name = "Жим" };
            var first = 0;
            var second = 0;

            service.OpenEditExercise(exercise, () =>
            {
                first++;

                return Task.CompletedTask;
            });
            service.OpenEditExercise(exercise, () =>
            {
                second++;

                return Task.CompletedTask;
            });

            viewModel!.Name = "Жим лёжа";

            RunCommand(window!, () => viewModel.AcceptCommand.ExecuteAsync(null));

            Assert.Equal(0, first);
            Assert.Equal(1, second);

            window!.Close();
        });
    }

    /// <summary>Добавление и правка плана делят одно окно: экземпляр общий на оба вызова.</summary>
    [Fact]
    public void OpenAddPlan_ПотомOpenEditPlan_ОдноОкно()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var added = 0;
            var edited = 0;
            EditPlanWindow? window = null;

            var service = CreateService(
                addPlan: () =>
                {
                    added++;

                    return window = NewPlanWindow(new TrainingPlan(), database, settings);
                },
                editPlan: plan =>
                {
                    edited++;

                    return window = NewPlanWindow(plan, database, settings);
                });

            service.OpenAddPlan();
            service.OpenEditPlan(new TrainingPlan { Name = "Ноги" });

            Assert.Equal(1, added);
            Assert.Equal(0, edited);

            window!.Close();
        });
    }

    /// <summary>Окно правки упражнения с настоящей базой, уведённое за экран.</summary>
    private static EditExerciseWindow NewExerciseWindow(
        Exercise exercise,
        TemporaryDatabase database,
        TemporarySettings settings,
        out EditExerciseViewModel viewModel)
    {
        database.Repository.AddAsync(exercise).GetAwaiter().GetResult();

        viewModel = new EditExerciseViewModel(exercise, database.Repository);
        viewModel.InitializeCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        var window = new EditExerciseWindow(viewModel, new HotkeyService(settings.Store));
        ShowAway(window);

        return window;
    }

    /// <summary>Окно правки упражнения без out-параметра: для проверки единственности.</summary>
    private static EditExerciseWindow NewExerciseWindow(
        Exercise exercise,
        TemporaryDatabase database,
        TemporarySettings settings)
    {
        var window = NewExerciseWindow(exercise, database, settings, out _);

        return window;
    }

    /// <summary>Окно плана с настоящими базами, уведённое за экран.</summary>
    private static EditPlanWindow NewPlanWindow(
        TrainingPlan plan,
        TemporaryDatabase database,
        TemporarySettings settings)
    {
        var viewModel = new EditPlanViewModel(
            plan,
            isNew: true,
            database.PlanRepository,
            database.Repository);
        viewModel.InitializeCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        var window = new EditPlanWindow(viewModel, new HotkeyService(settings.Store));
        ShowAway(window);

        return window;
    }

    /// <summary>Сервис с фабриками только для тестируемых окон: остальные не должны вызываться.</summary>
    private static WindowService CreateService(
        Func<Exercise, EditExerciseWindow>? editExercise = null,
        Func<ImportExercisesWindow>? import = null,
        Func<EditPlanWindow>? addPlan = null,
        Func<TrainingPlan, EditPlanWindow>? editPlan = null) =>
        new(
            () => throw new NotSupportedException("Окно упражнений в этом тесте не открывается."),
            editExercise ?? (_ => throw new NotSupportedException("Окно правки упражнения не задано.")),
            import ?? (() => throw new NotSupportedException("Окно импорта не задано.")),
            () => throw new NotSupportedException("Окно планов в этом тесте не открывается."),
            addPlan ?? (() => throw new NotSupportedException("Окно добавления плана не задано.")),
            editPlan ?? (_ => throw new NotSupportedException("Окно правки плана не задано.")),
            _ => throw new NotSupportedException("Окно дня в этом тесте не открывается."),
            () => throw new NotSupportedException("Окно параметров в этом тесте не открывается."));

    private static void ShowAway(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.ShowActivated = false;

        window.Show();
    }

    /// <summary>
    /// Запускает асинхронную команду окна и доводит её до конца.
    /// </summary>
    /// <remarks>
    /// Запись в базу уходит в фон, и продолжение возвращается в UI-поток. Без контекста
    /// диспетчера оно пришло бы из пула потоков, и окно закрывалось бы из чужого потока.
    /// Прокачка идёт, пока задача не завершится: фиксированное число кадров не гарантирует,
    /// что фоновая запись успела.
    /// </remarks>
    /// <param name="window">Окно теста.</param>
    /// <param name="start">Запуск команды.</param>
    private static void RunCommand(Window window, Func<Task> start)
    {
        var previous = SynchronizationContext.Current;

        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));

        try
        {
            var task = start();

            // Без срока зависшая задача повесила бы весь прогон: лучше явный отказ теста.
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (!task.IsCompleted)
            {
                Assert.True(DateTime.UtcNow < deadline, "Команда окна не завершилась за отведённое время.");

                window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            }

            task.GetAwaiter().GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
