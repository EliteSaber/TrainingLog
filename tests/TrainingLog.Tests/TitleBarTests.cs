using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ShapePath = System.Windows.Shapes.Path;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Кнопка закрепления общей полосы заголовка: переключает Topmost и показывает состояние.
/// </summary>
/// <remarks>
/// Состояние читается с окна, а не из модели, поэтому достаточно настоящего окна с полосой —
/// проверяется и переключатель, и то, что глифы с подсказкой за ним следуют. Порядок окон по
/// z-координате — Win32, а не наша логика, здесь он не проверяется.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class TitleBarTests
{
    [Fact]
    public void КнопкаЗакрепления_ПереключаетTopmostИСостояние()
    {
        StaRunner.Run(() =>
        {
            var window = new Window();
            var bar = new TitleBar();
            window.Content = bar;

            try
            {
                var button = (Button)bar.FindName("PinButton")!;
                var outline = (ShapePath)bar.FindName("PinOutline")!;
                var angled = (ShapePath)bar.FindName("PinAngled")!;

                Assert.False(window.Topmost);
                Pump(window);
                Assert.Equal("Закрепить", button.ToolTip);
                Assert.Equal(Visibility.Visible, outline.Visibility);
                Assert.Equal(Visibility.Collapsed, angled.Visibility);

                Click(button);
                Pump(window);
                Assert.True(window.Topmost);
                Assert.Equal("Открепить", button.ToolTip);
                Assert.Equal(Visibility.Collapsed, outline.Visibility);
                Assert.Equal(Visibility.Visible, angled.Visibility);

                Click(button);
                Pump(window);
                Assert.False(window.Topmost);
                Assert.Equal("Закрепить", button.ToolTip);
                Assert.Equal(Visibility.Visible, outline.Visibility);
                Assert.Equal(Visibility.Collapsed, angled.Visibility);
            }
            finally
            {
                window.Topmost = false;
            }
        });
    }

    private static void Click(Button button) =>
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Pump(Window window)
    {
        window.UpdateLayout();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        window.UpdateLayout();
    }
}
