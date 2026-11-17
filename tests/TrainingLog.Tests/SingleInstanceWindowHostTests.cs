using System.Windows;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>
/// Хост единственного окна: владелец у окна-владельца есть, у отдельного окна — нет.
/// </summary>
/// <remarks>
/// Тесту нужны настоящие окна: владелец назначается живому окну, и пустое обойтись не может.
/// Окна уводятся за экран и не активируются, чтобы тест не мигал и не забирал фокус.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class SingleInstanceWindowHostTests
{
    /// <summary>У окна-владельца владелец есть: сворачивание журнала прячет и его.</summary>
    [Fact]
    public void Show_ОкноВладельца_ВладелецНазначен()
    {
        StaRunner.Run(() =>
        {
            var owner = ShowAway(new Window());
            var host = new SingleInstanceWindowHost<Window>(() => ShowAway(new Window()));

            try
            {
                host.Show(owner);

                Assert.Same(owner, host.Current?.Owner);
            }
            finally
            {
                host.Current?.Close();
                owner.Close();
            }
        });
    }

    /// <summary>
    /// У отдельного окна владельца нет даже если его передали: иначе сворачивание журнала
    /// спрятало бы окно вместе с журналом.
    /// </summary>
    [Fact]
    public void Show_ОтдельноеОкно_ВладельцаНет()
    {
        StaRunner.Run(() =>
        {
            var owner = ShowAway(new Window());
            var host = new SingleInstanceWindowHost<Window>(() => ShowAway(new Window()), owned: false);

            try
            {
                host.Show(owner);

                Assert.Null(host.Current?.Owner);
            }
            finally
            {
                host.Current?.Close();
                owner.Close();
            }
        });
    }

    /// <summary>Повторный вызов не создаёт второго окна: единственный экземпляр.</summary>
    [Fact]
    public void Show_Повторно_ОкноНеСоздаётся()
    {
        StaRunner.Run(() =>
        {
            var created = 0;
            var host = new SingleInstanceWindowHost<Window>(() =>
            {
                created++;

                return ShowAway(new Window());
            });

            try
            {
                host.Show();
                var first = host.Current;

                host.Show();

                Assert.Equal(1, created);
                Assert.Same(first, host.Current);
            }
            finally
            {
                host.Current?.Close();
            }
        });
    }

    /// <summary>После закрытия окна следующий вызов создаёт новое: в закрытом живут старые данные.</summary>
    [Fact]
    public void Show_ПослеЗакрытия_СоздаётсяНовое()
    {
        StaRunner.Run(() =>
        {
            var host = new SingleInstanceWindowHost<Window>(() => ShowAway(new Window()));

            try
            {
                host.Show();
                var first = host.Current;

                first!.Close();

                host.Show();

                Assert.NotSame(first, host.Current);
            }
            finally
            {
                host.Current?.Close();
            }
        });
    }

    /// <summary>Окно уводится за экран и не активируется: тест не должен мигать и забирать фокус.</summary>
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
}
