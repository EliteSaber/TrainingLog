using System.Windows;
using System.Windows.Interop;

namespace TrainingLog.Services;

/// <summary>
/// Слежение за сменой темы Windows: приложение перекрашивается без перезапуска.
/// </summary>
/// <remarks>
/// Ловится <c>WM_SETTINGCHANGE</c> (0x001A) на главном окне. Само сообщение не разбирается:
/// Windows присылает его на десяток несвязанных вещей, а разбор строки в параметре (на
/// Windows 11 это «ImmersiveColorSet») версионно нестабилен. Вместо этого сообщение — повод
/// перечитать реестр и переприменить тему: если действующая тема не изменилась, <see cref="AppTheme"/>
/// не сделает ничего.
///
/// Хук живёт в <see cref="HwndSource"/> главного окна, то есть срабатывает на UI-нитке, —
/// диспетчеризовать нечего. Отписка не нужна: источник умирает вместе с окном, а окно
/// закрывается только при выходе из приложения.
/// </remarks>
public sealed class SystemThemeWatcher
{
    private const int WmSettingChange = 0x001A;

    private SystemThemeWatcher(HwndSource source)
    {
        source.AddHook(OnWindowMessage);
    }

    /// <summary>
    /// Вешает слежение на окно: окно должно быть уже показано, у него есть handle.
    /// </summary>
    /// <remarks>
    /// Экземпляр не хранится: хук держит ссылку на обработчик, а обработчик — на экземпляр, и
    /// пока жив источник, жив и наблюдатель.
    /// </remarks>
    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (HwndSource.FromHwnd(handle) is not { } source)
        {
            return;
        }

        _ = new SystemThemeWatcher(source);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmSettingChange)
        {
            // Переприменяется выбор пользователя, а не тема Windows: выбрана тёмная — смена темы
            // Windows приложения не касается, и переприменение ничего не изменит.
            AppTheme.Apply(AppTheme.CurrentChoice);
        }

        return IntPtr.Zero;
    }
}
