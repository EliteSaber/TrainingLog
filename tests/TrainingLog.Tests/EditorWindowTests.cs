using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using TrainingLog.Controls;
using TrainingLog.Core.Models;
using TrainingLog.Services;
using TrainingLog.ViewModels;
using TrainingLog.Windows;

namespace TrainingLog.Tests;

/// <summary>
/// Окна правки упражнения, плана и импорта — отдельные и немодальные: не блокируют приложение,
/// и вкладку «Окна» в «Параметрах» можно открыть рядом с правкой.
/// </summary>
/// <remarks>
/// Окна уводятся за экран и не активируются, чтобы тест не мигал и не забирал фокус у
/// пользователя.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class EditorWindowTests
{
    /// <summary>Окно правки упражнения отдельное: без владельца и со своей кнопкой на панели задач.</summary>
    [Fact]
    public void ОкноПравкиУпражнения_Отдельное()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();
            var window = ExerciseWindow(database, settings, out _);

            try
            {
                AssertSeparate(window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Окно правки упражнения не тянется по высоте: зон захвата за верх и низ нет.</summary>
    [Fact]
    public void ОкноПравкиУпражнения_БезЗахватаПоВысоте()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();
            var window = ExerciseWindow(database, settings, out _);

            try
            {
                ShowAway(window);
                var chrome = WindowChrome.GetWindowChrome(window);
                Assert.NotNull(chrome);
                Assert.Equal(0, chrome.ResizeBorderThickness.Top);
                Assert.Equal(0, chrome.ResizeBorderThickness.Bottom);
                Assert.Equal(4, chrome.ResizeBorderThickness.Left);
                Assert.Equal(4, chrome.ResizeBorderThickness.Right);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Окно плана отдельное: без владельца и со своей кнопкой на панели задач.</summary>
    [Fact]
    public void ОкноПлана_Отдельное()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();
            var window = PlanWindow(database, settings, out _);

            try
            {
                AssertSeparate(window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Окно импорта отдельное: без владельца и со своей кнопкой на панели задач.</summary>
    [Fact]
    public void ОкноИмпорта_Отдельное()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();
            var window = ImportWindow(database, settings);

            try
            {
                AssertSeparate(window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>«Принять» записывает упражнение, сообщает о записи и закрывает окно.</summary>
    [Fact]
    public void ОкноПравкиУпражнения_Принять_СообщаетИЗакрывает()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();
            var window = ExerciseWindow(database, settings, out var viewModel);

            try
            {
                var saved = 0;
                window.Saved += () => saved++;

                ShowAway(window);
                viewModel.Name = "Жим лёжа";

                RunCommand(window, () => viewModel.AcceptCommand.ExecuteAsync(null));

                Assert.Equal(1, saved);
                Assert.False(window.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Esc закрывает окно правки без записи — <c>IsCancel</c> в немодальном окне не работает.</summary>
    [Fact]
    public void ОкноПравкиУпражнения_Esc_ЗакрываетБезЗаписи()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();
            var window = ExerciseWindow(database, settings, out var viewModel);

            try
            {
                var saved = 0;
                window.Saved += () => saved++;

                ShowAway(window);
                window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(window), Environment.TickCount, Key.Escape)
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                    Source = window,
                });

                Assert.False(window.IsVisible);
                Assert.Equal(0, saved);
                Assert.False(viewModel.Accepted);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>«Отменить» закрывает окно правки без записи.</summary>
    [Fact]
    public void ОкноПравкиУпражнения_Отмена_ЗакрываетБезЗаписи()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();
            var window = ExerciseWindow(database, settings, out var viewModel);

            try
            {
                var saved = 0;
                window.Saved += () => saved++;

                ShowAway(window);
                var button = FindButton(window, "EditExercise.Cancel")!;
                Assert.NotNull(button);

                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.False(window.IsVisible);
                Assert.Equal(0, saved);
                Assert.False(viewModel.Accepted);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Признаки отдельного окна — то, что задано в разметке трёх окон.</summary>
    /// <param name="window">Окно для проверки.</param>
    private static void AssertSeparate(Window window)
    {
        Assert.True(window.ShowInTaskbar);
        Assert.Null(window.Owner);
        Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
    }

    /// <summary>Окно правки упражнения с настоящей базой и файлом настроек.</summary>
    private static EditExerciseWindow ExerciseWindow(
        TemporaryDatabase database,
        TemporarySettings settings,
        out EditExerciseViewModel viewModel)
    {
        var exercise = new Exercise { Name = "Жим" };
        database.Repository.AddAsync(exercise).GetAwaiter().GetResult();

        viewModel = new EditExerciseViewModel(exercise, database.Repository);
        viewModel.InitializeCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        var window = new EditExerciseWindow(viewModel, new HotkeyService(settings.Store));

        return window;
    }

    /// <summary>Окно плана с настоящими базами и файлом настроек.</summary>
    private static EditPlanWindow PlanWindow(
        TemporaryDatabase database,
        TemporarySettings settings,
        out EditPlanViewModel viewModel)
    {
        viewModel = new EditPlanViewModel(
            new TrainingPlan(),
            isNew: true,
            database.PlanRepository,
            database.Repository);
        viewModel.InitializeCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        var window = new EditPlanWindow(viewModel, new HotkeyService(settings.Store));

        return window;
    }

    /// <summary>Окно импорта с настоящей базой и файлом настроек.</summary>
    private static ImportExercisesWindow ImportWindow(TemporaryDatabase database, TemporarySettings settings)
    {
        var viewModel = new ImportExercisesViewModel(database.Repository);
        var window = new ImportExercisesWindow(viewModel, new HotkeyService(settings.Store));

        return window;
    }

    /// <summary>Показывает окно за экраном: startup location задана разметкой и проверяется тестом.</summary>
    /// <param name="window">Окно теста.</param>
    private static void ShowAway(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.ShowActivated = false;

        window.Show();
    }

    /// <summary>Ищет кнопку по адресу назначения в визуальном дереве окна.</summary>
    /// <param name="root">Корень поиска.</param>
    /// <param name="hotkeyId">Адрес из каталога.</param>
    private static Button? FindButton(DependencyObject root, string hotkeyId)
    {
        if (root is FrameworkElement element && element is Button button && Hotkey.GetId(element) == hotkeyId)
        {
            return button;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindButton(VisualTreeHelper.GetChild(root, index), hotkeyId) is { } found)
            {
                return found;
            }
        }

        return null;
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

            while (!task.IsCompleted)
            {
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
