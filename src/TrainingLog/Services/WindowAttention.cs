using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TrainingLog.Services;

/// <summary>Оповещение пользователя об уже открытом окне: мигание и звук.</summary>
/// <remarks>
/// WPF ничего подобного не умеет: <c>Window.Activate()</c> выводит окно на передний план, но без
/// объяснения, откуда выехал фокус. Ровно это оповещение Windows сам показывает при клике на
/// главное окно с открытым модальным окном — но у окна дня владельца нет (с владельцем
/// сворачивание главного окна прятало бы окно дня), и этот случай системы нам больше не
/// достаётся. Отсюда FlashWindowEx и MessageBeep своими руками: мигает и заголовок, и кнопка на
/// панели задач (FLASHW_ALL), пока пользователь не вернётся в окно (FLASHW_TIMERNOFG).
/// </remarks>
public static class WindowAttention
{
    /// <summary>FLASHW_ALL: мигать и заголовком окна, и его кнопкой на панели задач.</summary>
    private const uint FlashAll = 3;

    /// <summary>FLASHW_TIMERNOFG: продолжать мигать, пока окно не станет передним.</summary>
    private const uint FlashTimerNoForeground = 0xC;

    /// <summary>MessageBeep: 0xFFFFFFFF — звук по умолчанию из схемы звуков системы.</summary>
    private const uint DefaultBeep = 0xFFFFFFFF;

    /// <summary>Мигает окном и кнопкой панели задач со звуком по умолчанию.</summary>
    public static void Flash(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        MessageBeep(DefaultBeep);

        // Handle у неявно созданного окна уже есть (мигают только показанные окна), но
        // EnsureHandle гарантирует его и на всякий нештатный вызов.
        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = new WindowInteropHelper(window).EnsureHandle(),
            dwFlags = FlashAll | FlashTimerNoForeground,
            uCount = uint.MaxValue,
            dwTimeout = 0,
        };

        FlashWindowEx(ref info);
    }

    /// <summary>Поднимает окно на верх z-полосы и передаёт ему передний план с фокусом.</summary>
    /// <remarks>
    /// <c>Window.Activate()</c> этого не делает: под ним <c>SetActiveWindow</c> даёт окну
    /// активацию и клавиатуру, но не меняет z-порядок. Для owned-окон разницы нет — owned-окно
    /// всегда стоит выше владельца, — а окно дня владельца не имеет, и после клика по главному
    /// окну оно оставалось за ним. Отсюда <c>SetWindowPos</c> с <c>HWND_TOP</c> и
    /// <c>SetForegroundWindow</c>; вызовы разрешены, потому что наш процесс как раз получает
    /// последний ввод (клик был в нём).
    /// </remarks>
    public static void BringToFront(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Handle у показанного окна уже есть, EnsureHandle гарантирует его и вне этого правила.
        var handle = new WindowInteropHelper(window).EnsureHandle();

        // SWP_NOMOVE и SWP_NOSIZE: меняется только порядок, а не положение и не размер.
        SetWindowPos(handle, HwndTop, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);

        SetForegroundWindow(handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public nint hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    /// <summary>HWND_TOP: поставить окно на верх своей z-полосы.</summary>
    private const nint HwndTop = 0;

    /// <summary>SWP_NOSIZE и SWP_NOMOVE: положение и размер не менять, SWP_SHOWWINDOW — показать.</summary>
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoMove = 0x2;
    private const uint SwpShowWindow = 0x40;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint type);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint handle);
}
