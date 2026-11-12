using System.IO;
using System.Security;
using Microsoft.Win32;

namespace TrainingLog.Services;

/// <summary>
/// Тема оформления Windows: читается из реестра.
/// </summary>
/// <remarks>
/// Своего API для тёмной темы у WPF нет, а признак «приложения тёмные» лежит в реестре:
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme</c>
/// (1 — светлая, 0 — тёмная). Ключа нет — считаем тему светлой: так Windows стоит по умолчанию.
///
/// Чтение не бросает: значение нужно при каждом применении темы, и отказ чтения не должен
/// ронять приложение.
/// </remarks>
public static class SystemTheme
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const string LightValueName = "AppsUseLightTheme";

    /// <summary>Тёмная ли тема оформления Windows.</summary>
    public static bool IsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);

            // Значение — DWORD, и в объект приходит как int. Тип не тот — тема не тёмная:
            // разбирать мусор в реестре нечем.
            return key?.GetValue(LightValueName) is int light && light == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }
}
