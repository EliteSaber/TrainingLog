using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>
/// Возврат активации после закрытия окна: главное окно становится активным, если закрыто
/// последнее окно приложения, и разворачивается, если было свёрнуто.
/// </summary>
/// <remarks>
/// Приложения в тестах нет, поэтому правило проверяется без <see cref="Application.Current"/>:
/// главное окно и список окон передаются явно. Окно, которое должно быть видимым, уводится за
/// экран и не активируется, чтобы тест не мигал и не забирал фокус.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class WindowReturnTests
{
    /// <summary>Кроме главного и закрываемого окон ничего нет — закрытое было последним.</summary>
    [Fact]
    public void IsLastOtherWindow_ОдноОкноКромеГлавного_Истина()
    {
        StaRunner.Run(() =>
        {
            var main = new Window();
            var closed = new Window();

            Assert.True(WindowReturn.IsLastOtherWindow(closed, main, [main, closed]));
        });
    }

    /// <summary>Осталось ещё видимое окно — закрытое не последнее, главное не трогаем.</summary>
    [Fact]
    public void IsLastOtherWindow_ЕстьЕщёВидимоеОкно_Ложь()
    {
        StaRunner.Run(() =>
        {
            var main = new Window();
            var closed = new Window();
            var other = ShowAway(new Window());

            try
            {
                Assert.False(WindowReturn.IsLastOtherWindow(closed, main, [main, closed, other]));
            }
            finally
            {
                other.Close();
            }
        });
    }

    /// <summary>Скрытое окно не считается: переключаться пользователю некуда.</summary>
    [Fact]
    public void IsLastOtherWindow_СкрытоеОкно_НеСчитается()
    {
        StaRunner.Run(() =>
        {
            var main = new Window();
            var closed = new Window();
            var hidden = new Window();

            Assert.True(WindowReturn.IsLastOtherWindow(closed, main, [main, closed, hidden]));
        });
    }

    /// <summary>
    /// Видимое окно из чужой сборки не считается: отладчик Visual Studio добавляет в
    /// <c>Application.Windows</c> своё AdornerWindow, и оно не должно означать, что осталось
    /// ещё одно окно приложения.
    /// </summary>
    [Fact]
    public void IsLastOtherWindow_ЧужоеОкноОтладчика_НеСчитается()
    {
        StaRunner.Run(() =>
        {
            var main = new AppWindow();
            var closed = new AppWindow();
            var foreign = ShowAway(new Window());

            try
            {
                Assert.True(WindowReturn.IsLastOtherWindow(closed, main, [main, closed, foreign]));
            }
            finally
            {
                foreign.Close();
            }
        });
    }

    /// <summary>Свёрнутое кнопкой окно разворачивается, а не остаётся свёрнутым.</summary>
    /// <remarks>
    /// Проверяется состояние самого окна (<see cref="WindowAttention.IsMinimized"/>), а не
    /// свойство: при сворачивании кнопкой заголовка WPF перестаёт обновлять кэш стиля, и
    /// <c>WindowState = Normal</c> окно не разворачивает, зато свойство уже показывает Normal.
    /// Именно на этом ломался возврат активации после закрытия окна.
    /// </remarks>
    [Fact]
    public void Restore_СвёрнутоеОкно_Разворачивается()
    {
        StaRunner.Run(() =>
        {
            var window = ShowAway(new Window());

            try
            {
                window.WindowState = WindowState.Minimized;
                Pump(window);
                Assert.True(WindowAttention.IsMinimized(window));

                WindowReturn.Restore(window);
                Pump(window);

                Assert.False(WindowAttention.IsMinimized(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Обычное окно разворот не трогает.</summary>
    [Fact]
    public void Restore_ОбычноеОкно_НеМеняется()
    {
        StaRunner.Run(() =>
        {
            var window = ShowAway(new Window());

            try
            {
                WindowReturn.Restore(window);

                Assert.False(WindowAttention.IsMinimized(window));
                Assert.Equal(WindowState.Normal, window.WindowState);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Развёрнутое до сворачивания окно возвращается развёрнутым, а не обычным.</summary>
    [Fact]
    public void Restore_РазвёрнутоеОкно_ОстаётсяРазвёрнутым()
    {
        StaRunner.Run(() =>
        {
            var window = ShowAway(new Window());

            try
            {
                window.WindowState = WindowState.Maximized;
                Pump(window);
                window.WindowState = WindowState.Minimized;
                Pump(window);
                Assert.True(WindowAttention.IsMinimized(window));

                WindowReturn.Restore(window);
                Pump(window);

                Assert.False(WindowAttention.IsMinimized(window));
                Assert.Equal(WindowState.Maximized, window.WindowState);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// То же на окне с хромом приложения: WindowChrome подменяет неклиентскую область, и
    /// сворачивание идёт через хук WPF, а не напрямую через DefWindowProc.
    /// </summary>
    [Fact]
    public void Activate_ОкноСХромом_ГлавноеРазворачивается()
    {
        StaRunner.Run(() =>
        {
            var main = new Window();
            WindowChrome.SetWindowChrome(
                main,
                new WindowChrome
                {
                    CaptionHeight = 36,
                    ResizeBorderThickness = new Thickness(4),
                    GlassFrameThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(6),
                    UseAeroCaptionButtons = false,
                });
            ShowAway(main);

            try
            {
                SystemCommands.MinimizeWindow(main);
                WaitMinimized(main);
                Assert.True(main.IsVisible);

                var closed = ShowAway(new Window());
                closed.Closed += (_, _) => WindowReturn.Activate(main, closed, main, [main, closed]);
                closed.Close();

                Assert.False(WindowAttention.IsMinimized(main));
            }
            finally
            {
                main.Close();
            }
        });
    }

    /// <summary>
    /// Закрылось последнее окно, кроме главного: главное становится активным и разворачивается —
    /// ровно тот путь, который идёт из обработчика <c>Closed</c> в приложении.
    /// </summary>
    [Fact]
    public void Activate_ЗакрытоПоследнееОкно_ГлавноеРазворачивается()
    {
        StaRunner.Run(() =>
        {
            var main = ShowAway(new Window());

            try
            {
                SystemCommands.MinimizeWindow(main);
                WaitMinimized(main);
                Assert.True(main.IsVisible);

                var closed = ShowAway(new Window());
                closed.Closed += (_, _) => WindowReturn.Activate(main, closed, main, [main, closed]);
                closed.Close();

                Assert.False(WindowAttention.IsMinimized(main));
            }
            finally
            {
                main.Close();
            }
        });
    }

    /// <summary>
    /// Свёрнутое кнопкой заголовка окно разворачивается: там минимизация идёт командой системы,
    /// а не присваиванием свойства, и путь у WPF другой.
    /// </summary>
    [Fact]
    public void Restore_СвёрнутоеКнопкойЗаголовка_Разворачивается()
    {
        StaRunner.Run(() =>
        {
            var window = ShowAway(new Window());

            try
            {
                SystemCommands.MinimizeWindow(window);
                WaitMinimized(window);

                WindowReturn.Restore(window);

                Assert.False(WindowAttention.IsMinimized(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Под отладчиком закрывается последнее окно приложения, а рядом висит чужое видимое окно
    /// Live Visual Tree: главное всё равно разворачивается.
    /// </summary>
    [Fact]
    public void Activate_ЧужоеОкноОтладчика_ГлавноеРазворачивается()
    {
        StaRunner.Run(() =>
        {
            var main = ShowAway(new AppWindow());

            try
            {
                SystemCommands.MinimizeWindow(main);
                WaitMinimized(main);
                Assert.True(main.IsVisible);

                var foreign = ShowAway(new Window());

                try
                {
                    var closed = ShowAway(new AppWindow());
                    closed.Closed += (_, _) =>
                        WindowReturn.Activate(main, closed, main, [main, foreign, closed]);
                    closed.Close();

                    Assert.False(WindowAttention.IsMinimized(main));
                }
                finally
                {
                    foreign.Close();
                }
            }
            finally
            {
                main.Close();
            }
        });
    }

    /// <summary>Дожидается, пока окно свернётся: команда системы приходит через очередь сообщений.</summary>
    /// <param name="window">Окно теста.</param>
    private static void WaitMinimized(Window window)
    {
        for (var attempt = 0; attempt < 50 && !WindowAttention.IsMinimized(window); attempt++)
        {
            PumpMessages();
        }

        Assert.True(WindowAttention.IsMinimized(window));
    }

    /// <summary>Обрабатывает очередь сообщений Windows.</summary>
    private static void PumpMessages()
    {
        var frame = new DispatcherFrame();

        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));

        Dispatcher.PushFrame(frame);
    }

    /// <summary>Даёт окну обработать сообщения Windows о смене состояния.</summary>
    /// <param name="window">Окно теста.</param>
    private static void Pump(Window window)
    {
        window.UpdateLayout();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        window.UpdateLayout();
    }

    /// <summary>Окно за экраном и без активации: тест не мигает и не забирает фокус.</summary>
    /// <param name="window">Окно теста.</param>
    private static Window ShowAway(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.ShowActivated = false;

        window.Show();

        return window;
    }

    /// <summary>
    /// Окно «приложения»: объявлено в сборке тестов, как окна TrainingLog — в своей. Обычный
    /// <see cref="Window"/> из PresentationFramework играет роль окна, подселённого отладчиком.
    /// </summary>
    private sealed class AppWindow : Window
    {
    }
}
