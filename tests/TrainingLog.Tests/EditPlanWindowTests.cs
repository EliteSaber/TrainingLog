using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingLog.Controls;
using TrainingLog.Core.Models;
using TrainingLog.Services;
using TrainingLog.ViewModels;
using TrainingLog.Windows;

namespace TrainingLog.Tests;

/// <summary>
/// Окно правки плана: выбор строки мышью и стрелками, отметка упражнения, переход между блоками.
/// </summary>
/// <remarks>
/// Тесту нужно настоящее окно: без него <c>Focus()</c> не удаётся, и выделение стрелками не
/// воспроизводится. Окно уводится за экран и не активируется, чтобы тест не мигал и не забирал
/// фокус у пользователя.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class EditPlanWindowTests
{
    /// <summary>Стрелка вниз выделяет первую строку активного блока.</summary>
    [Fact]
    public void СтрелкаВниз_ВыделяетСтрокуАктивногоБлока()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var window = OpenWindow(database, settings);

            try
            {
                var list = (ItemsControl)window.FindName("AllList")!;

                list.Focus();
                Press(window, Key.Down);

                Assert.NotNull(RowSelection.Item(list));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Пробел отмечает упражнение выделенной стрелками строки.</summary>
    [Fact]
    public void Пробел_ОтмечаетВыделеннуюСтроку()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var window = OpenWindow(database, settings);

            try
            {
                var viewModel = (EditPlanViewModel)window.DataContext;
                var list = (ItemsControl)window.FindName("AllList")!;

                list.Focus();
                Press(window, Key.Down);
                Press(window, Key.Space);

                // Выделенной оказалась первая строка справочника — «Жим», его в плане не было.
                Assert.True(viewModel.AllExercises[0].IsChecked);
                Assert.Equal(2, viewModel.SelectedExercises.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>В составе отметок нет: «Пробел» там не делает ничего.</summary>
    [Fact]
    public void Пробел_ВСоставе_НичегоНеОтмечает()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var window = OpenWindow(database, settings);

            try
            {
                var viewModel = (EditPlanViewModel)window.DataContext;
                var list = (ItemsControl)window.FindName("SelectedList")!;

                list.Focus();
                Press(window, Key.Right);
                Press(window, Key.Space);

                Assert.Equal(PlanBlock.Order, viewModel.ActiveBlock);
                Assert.Single(viewModel.SelectedExercises);
                Assert.False(viewModel.AllExercises[0].IsChecked);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Стрелка вправо переводит стрелки в другой блок.</summary>
    [Fact]
    public void СтрелкаВправо_ПереключаетБлок()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var window = OpenWindow(database, settings);

            try
            {
                var viewModel = (EditPlanViewModel)window.DataContext;
                var list = (ItemsControl)window.FindName("AllList")!;

                list.Focus();
                Press(window, Key.Right);

                Assert.Equal(PlanBlock.Order, viewModel.ActiveBlock);
                Assert.Null(RowSelection.Item(list));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Клик по строке справочника отмечает упражнение и не выделяет строку.</summary>
    /// <remarks>
    /// Выделение в справочнике — только от стрелок: мышь отмечает упражнение.
    /// </remarks>
    [Fact]
    public void КликПоСтрокеСправочника_ОтмечаетУпражнение()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var window = OpenWindow(database, settings);

            try
            {
                var viewModel = (EditPlanViewModel)window.DataContext;
                var list = (ItemsControl)window.FindName("AllList")!;

                Click(RowOf(list, 0));

                // Клик отметил упражнение, но строку не выделил: выделение в справочнике — от стрелок.
                Assert.True(viewModel.AllExercises[0].IsChecked);
                Assert.Equal(2, viewModel.SelectedExercises.Count);
                Assert.Null(RowSelection.Item(list));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Клик по строке состава выделяет строку и переводит стрелки в этот блок.</summary>
    [Fact]
    public void КликПоСтрокеСостава_ВыделяетСтроку()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var window = OpenWindow(database, settings);

            try
            {
                var viewModel = (EditPlanViewModel)window.DataContext;
                var list = (ItemsControl)window.FindName("SelectedList")!;

                Click(RowOf(list, 0));

                Assert.NotNull(RowSelection.Item(list));
                Assert.Equal(PlanBlock.Order, viewModel.ActiveBlock);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>У активного блока подсвечены заголовок и рамка.</summary>
    /// <remarks>
    /// Условие триггера — перечисление, и опечатка в нём тихо оставила бы подсветку
    /// выключенной навсегда: глазом это видно, а тестом — нет.
    /// </remarks>
    [Fact]
    public void АктивныйБлок_ПодсвечиваетЗаголовокИРамку()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var window = OpenWindow(database, settings);

            try
            {
                var palette = LoadPalette();
                var accent = ((SolidColorBrush)palette["Accent"]).Color;
                var primary = ((SolidColorBrush)palette["TextPrimary"]).Color;
                var line = ((SolidColorBrush)palette["LineStrong"]).Color;

                var leftHeader = (TextBlock)window.FindName("AllBlockHeader")!;
                var rightHeader = (TextBlock)window.FindName("OrderBlockHeader")!;
                var leftBlock = (Border)window.FindName("AllBlock")!;
                var rightBlock = (Border)window.FindName("OrderBlock")!;

                Assert.Equal(accent, ((SolidColorBrush)leftHeader.Foreground).Color);
                Assert.Equal(accent, ((SolidColorBrush)leftBlock.BorderBrush).Color);
                Assert.Equal(primary, ((SolidColorBrush)rightHeader.Foreground).Color);
                Assert.Equal(line, ((SolidColorBrush)rightBlock.BorderBrush).Color);

                var list = (ItemsControl)window.FindName("AllList")!;

                list.Focus();
                Press(window, Key.Right);

                Assert.Equal(primary, ((SolidColorBrush)leftHeader.Foreground).Color);
                Assert.Equal(accent, ((SolidColorBrush)rightHeader.Foreground).Color);
                Assert.Equal(line, ((SolidColorBrush)leftBlock.BorderBrush).Color);
                Assert.Equal(accent, ((SolidColorBrush)rightBlock.BorderBrush).Color);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Окно с наполненным справочником и составом.</summary>
    /// <remarks>
    /// Наполнение окна асинхронное, и оно пересобирает состав в конце: ждать только первого
    /// упражнения нельзя — окно переписало бы состав после того, как тест отметил упражнение.
    /// Поэтому диспетчеру отдаётся несколько порций работы подряд.
    /// </remarks>
    private static EditPlanWindow OpenWindow(TemporaryDatabase database, TemporarySettings settings)
    {
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        database.Repository.AddAsync(squat).GetAwaiter().GetResult();
        database.Repository.AddAsync(bench).GetAwaiter().GetResult();

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        database.PlanRepository.AddAsync(plan).GetAwaiter().GetResult();

        var viewModel = new EditPlanViewModel(
            plan,
            isNew: false,
            database.PlanRepository,
            database.Repository);

        var window = new EditPlanWindow(viewModel, new HotkeyService(settings.Store));

        // Палитра в окне: без неё ссылка DynamicResource не разрешается, и вид блока
        // в тесте было бы не о чем судить (ловушка 6).
        window.Resources.MergedDictionaries.Add(LoadPalette());

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.ShowActivated = false;

        window.Show();
        window.UpdateLayout();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        Assert.Equal(2, viewModel.AllExercises.Count);
        Assert.Single(viewModel.SelectedExercises);

        return window;
    }

    /// <summary>Палитра оформления из сборки приложения.</summary>
    private static ResourceDictionary LoadPalette()
    {
        var assembly = typeof(AppTheme).Assembly.GetName().Name;
        var uri = new Uri($"/{assembly};component/Themes/Palette.Light.xaml", UriKind.Relative);

        return (ResourceDictionary)Application.LoadComponent(uri)!;
    }

    /// <summary>Нажатие клавиши в окне: тот же путь, что у настоящей клавиатуры.</summary>
    private static void Press(Window window, Key key)
    {
        window.RaiseEvent(new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(window),
            Environment.TickCount,
            key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
    }

    /// <summary>Нажатие и отпускание мышью на элементе строки.</summary>
    /// <remarks>
    /// Настоящая мышь шлёт событие предварительного нажатия раньше обычного, и окно слушает
    /// именно его: по нему блок переключается и фокус уходит в список.
    /// </remarks>
    private static void Click(FrameworkElement element)
    {
        element.RaiseEvent(new MouseButtonEventArgs(
            Mouse.PrimaryDevice,
            Environment.TickCount,
            MouseButton.Left)
        {
            RoutedEvent = Mouse.PreviewMouseDownEvent,
        });

        element.RaiseEvent(new MouseButtonEventArgs(
            Mouse.PrimaryDevice,
            Environment.TickCount,
            MouseButton.Left)
        {
            RoutedEvent = Mouse.MouseDownEvent,
        });

        element.RaiseEvent(new MouseButtonEventArgs(
            Mouse.PrimaryDevice,
            Environment.TickCount,
            MouseButton.Left)
        {
            RoutedEvent = Mouse.MouseUpEvent,
        });
    }

    /// <summary>Рамка строки списка: по ней кликает мышь, а не текст.</summary>
    private static Border RowOf(ItemsControl list, int index)
    {
        var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(index)!;

        return FindDescendant<Border>(container)!;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : class
    {
        if (root is T direct)
        {
            return direct;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var index = 0; index < count; index++)
        {
            if (FindDescendant<T>(VisualTreeHelper.GetChild(root, index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
