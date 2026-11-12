using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Скролл списка строк: нажатие по строке доходит до списка и выделяет строку.
/// </summary>
/// <remarks>
/// Регрессия шага 3 (виртуализация списка дней): скролл переехал в шаблон списка, и
/// <see cref="ScrollViewer.OnMouseLeftButtonDown"/> помечает нажатие обработанным, когда
/// забирает фокус себе. Скролл в шаблоне стоит в маршруте <em>ниже</em> списка, и его «гашение»
/// съедало клик раньше списка: строка не выделялась ни кликом, ни повторным кликом.
/// <see cref="RowScrollViewer"/> возвращает нажатие в маршрут, оставляя фокус скроллу.
///
/// Тесту нужно настоящее окно: без окна <c>Focus()</c> не удаётся, и скролл не гасит нажатие —
/// дефект просто не воспроизводится. Окно уводится за экран и не активируется, чтобы тест не
/// мигал и не забирал фокус у пользователя.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class RowScrollViewerTests
{
    /// <summary>Клик по строке выделяет её: нажатие доходит до списка.</summary>
    [Fact]
    public void Нажатие_ПоСтроке_ВыделяетСтроку()
    {
        StaRunner.Run(() =>
        {
            var (window, list) = WindowWithList();

            try
            {
                var row = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0)!;
                var border = FindDescendant<Border>(row)!;

                border.RaiseEvent(new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseDownEvent,
                });

                Assert.NotNull(RowSelection.Item(list));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Клик по кнопке внутри строки выделение не меняет: кнопка гасит нажатие сама.</summary>
    [Fact]
    public void Нажатие_ПоКнопкеВнутриСтроки_ВыделениеНеМеняет()
    {
        StaRunner.Run(() =>
        {
            var (window, list) = WindowWithList();

            try
            {
                var row = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0)!;
                var button = FindDescendant<Button>(row)!;

                button.RaiseEvent(new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseDownEvent,
                });

                Assert.Null(RowSelection.Item(list));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Окно со списком, у которого скролл живёт в шаблоне списка.</summary>
    private static (Window Window, ItemsControl List) WindowWithList()
    {
        var list = (ItemsControl)XamlReader.Parse(
            "<ItemsControl xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "  <ItemsControl.ItemsPanel>" +
            "    <ItemsPanelTemplate>" +
            "      <VirtualizingStackPanel />" +
            "    </ItemsPanelTemplate>" +
            "  </ItemsControl.ItemsPanel>" +
            "  <ItemsControl.ItemTemplate>" +
            "    <DataTemplate>" +
            "      <Border Background=\"Transparent\">" +
            "        <StackPanel>" +
            "          <TextBlock Text=\"row\" />" +
            "          <Button Content=\"action\" />" +
            "        </StackPanel>" +
            "      </Border>" +
            "    </DataTemplate>" +
            "  </ItemsControl.ItemTemplate>" +
            "</ItemsControl>");

        list.ItemsSource = new[] { new ProbeRow() };

        var template = new ControlTemplate(typeof(ItemsControl));
        var viewer = new FrameworkElementFactory(typeof(RowScrollViewer));
        viewer.SetValue(ScrollViewer.CanContentScrollProperty, true);
        viewer.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        viewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        viewer.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        template.VisualTree = viewer;
        list.Template = template;

        RowSelection.SetEnabled(list, true);

        var window = new Window
        {
            Width = 1,
            Height = 1,
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            Content = list,
        };

        window.Show();
        window.UpdateLayout();
        window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));

        return (window, list);
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

    /// <summary>Строка с признаком выделения на элементе данных — как строка журнала.</summary>
    private sealed class ProbeRow : ISelectableRow
    {
        public bool IsSelected { get; set; }
    }
}
