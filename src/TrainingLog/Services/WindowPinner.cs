using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TrainingLog.Services;

/// <summary>
/// Порядок закреплённых окон: активное из них — на самом верху.
/// </summary>
/// <remarks>
/// Само закрепление — <see cref="Window.Topmost"/>, его ставит кнопка полосы. Здесь только порядок
/// среди закреплённых: Windows держит их в отдельной z-полосе, и ни <c>Window.Activate()</c>, ни
/// <c>HWND_TOP</c> из <see cref="WindowAttention.BringToFront"/> не гарантируют, что активное окно
/// встанет выше других закреплённых — особенно при программном выводе вперёд. Поэтому на
/// <see cref="Window.Activated"/> поднимаем его явным <c>HWND_TOPMOST</c>; <c>SWP_NOACTIVATE</c>
/// сохраняет фокус там, где он уже есть.
///
/// Маркер в <see cref="ConditionalWeakTable{TKey,TValue}"/> слабый, а подписка висит на самом окне
/// и замыкается на него же: снаружи ссылки нет, и закрытое окно убирается сборщиком мусора.
/// </remarks>
public static class WindowPinner
{
    /// <summary>Слабый маркер «окно уже подключено»: полоса может загрузиться повторно.</summary>
    private static readonly ConditionalWeakTable<Window, object> Attached = new();

    /// <summary>HWND_TOPMOST: верх z-полосы закреплённых окон.</summary>
    private const nint HwndTopmost = -1;

    /// <summary>SWP_NOSIZE и SWP_NOMOVE: порядок меняется, положение и размер — нет.</summary>
    private const uint SwpNoSize = 0x1;

    private const uint SwpNoMove = 0x2;

    /// <summary>SWP_NOACTIVATE: подъём по z-порядку не должен забирать фокус.</summary>
    private const uint SwpNoActivate = 0x10;

    /// <summary>Подключает окно к слежению за активацией, чтобы закреплённое вставало выше прочих.</summary>
    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Attached.TryGetValue(window, out _))
        {
            return;
        }

        Attached.Add(window, new object());
        window.Activated += OnActivated;
    }

    private static void OnActivated(object? sender, EventArgs e)
    {
        if (sender is not Window { Topmost: true } window)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).EnsureHandle();

        SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint handle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
