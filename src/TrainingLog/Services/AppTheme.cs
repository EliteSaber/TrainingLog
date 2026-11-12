using System.IO;
using System.Windows;
using System.Windows.Markup;
using TrainingLog.Core.Settings;

namespace TrainingLog.Services;

/// <summary>
/// Применение темы оформления: палитра лежит на уровне приложения, и переключатель
/// перезаписывает её значения на выбранные.
/// </summary>
/// <remarks>
/// Механизм — тот же, что у <see cref="AppFonts"/>: значение в словаре перезаписывается, и
/// DynamicResource-ссылки обновляются сами. Словарь не подменяется целиком: он остаётся на
/// месте, и окна, открытые позже, берут из него актуальные цвета. Подмена словаря в списке
/// слияния оставила бы новые окна со старой палитрой.
///
/// Application.Current без значения — тесты и дизайнер: перекрашивать нечего, и палитра там
/// не найдётся.
/// </remarks>
public static class AppTheme
{
    /// <summary>
    /// Ключ-метка палитры: по нему словарь находится в списке слияния приложения, а по
    /// значению видно, какая тема сейчас применена.
    /// </summary>
    public const string PaletteIdKey = "PaletteId";

    /// <summary>
    /// Выбор пользователя: светлая, тёмная или системная. Хранится отдельно от действующей
    /// темы: выбор «системная» и тема Windows — разные вещи, и при смене темы Windows
    /// переприменяется именно выбор.
    /// </summary>
    public static string CurrentChoice { get; private set; } = ThemeRules.Default;

    /// <summary>
    /// Применяет тему: перекрашивает палитру приложения и запоминает выбор.
    /// </summary>
    /// <remarks>
    /// Зовётся при старте (до показа окон — иначе первое окно рисовалось бы старой темой) и
    /// окном параметров при выборе и после записи файла.
    ///
    /// Не бросает: зовётся при старте до показа окон, и негодный файл палитры превратился бы
    /// в отказ запуска.
    /// </remarks>
    /// <param name="theme">Выбор пользователя: светлая, тёмная или системная.</param>
    public static void Apply(string theme)
    {
        CurrentChoice = ThemeRules.Normalize(theme);

        var resources = Application.Current?.Resources;

        if (resources is null)
        {
            return;
        }

        var palette = FindPalette(resources);

        if (palette is null)
        {
            return;
        }

        ApplyTo(palette, CurrentChoice);
    }

    /// <summary>
    /// Перезаписывает значения палитры значениями выбранной темы.
    /// </summary>
    /// <remarks>
    /// Словарь остаётся на месте — меняются только значения. Поэтому окна, открытые позже,
    /// берут из него актуальные цвета, а уже открытые перерисовываются по ссылкам
    /// DynamicResource: значение в словаре сменилось, и ссылка перечитала его.
    /// </remarks>
    /// <param name="palette">Палитра — словарь с токенами темы.</param>
    /// <param name="theme">Выбор пользователя: светлая, тёмная или системная.</param>
    public static void ApplyTo(ResourceDictionary palette, string theme)
    {
        var effective = ThemeRules.Effective(ThemeRules.Normalize(theme), SystemTheme.IsDark());

        // Уже применена — ничего не трогаем: перезапись значений перерисовывает все окна,
        // а лишняя перерисовка при смене темы Windows (сообщение приходит и на другие
        // настройки) ни к чему не ведёт.
        if (palette[PaletteIdKey] as string == effective)
        {
            return;
        }

        var source = LoadPalette(effective);

        if (source is null)
        {
            return;
        }

        CopyInto(source, palette);
    }

    /// <summary>Находит палитру в списке слияния приложения: по метке, а не по позиции.</summary>
    private static ResourceDictionary? FindPalette(ResourceDictionary resources)
    {
        foreach (var merged in resources.MergedDictionaries)
        {
            if (merged.Contains(PaletteIdKey))
            {
                return merged;
            }
        }

        return null;
    }

    /// <summary>Загружает палитру выбранной темы из сборки приложения.</summary>
    /// <remarks>
    /// Через <see cref="Application.LoadComponent(Uri)"/>, а не строкой pack-URI: такую строку
    /// разбирает <see cref="System.Uri"/>, который схемы «pack» не знает, и без заранее
    /// зарегистрированного разборщика он падает с «Invalid port specified». LoadComponent
    /// обходится относительным адресом и работает одинаково и в приложении, и в тестовом хосте.
    /// </remarks>
    private static ResourceDictionary? LoadPalette(string effective)
    {
        var assembly = typeof(AppTheme).Assembly.GetName().Name;

        var uri = new Uri($"/{assembly};component/Themes/Palette.{effective}.xaml", UriKind.Relative);

        try
        {
            return Application.LoadComponent(uri) as ResourceDictionary;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or XamlParseException)
        {
            // Палитра не загрузилась — приложение остаётся в прежней теме, а не падает:
            // тему выбирают в параметрах, и отказ запуска из-за неё недопустим.
            return null;
        }
    }

    /// <summary>
    /// Перезаписывает значения палитры значениями другой темы.
    /// </summary>
    /// <remarks>
    /// Ключи, которых нет в новой палитре, убираются: иначе в словаре остался бы токен прежней
    /// темы и тихо красил бы часть интерфейса. Наборы ключей у палитр одинаковые, и это
    /// проверяется тестом, — но полагаться на это в коде нельзя.
    /// </remarks>
    private static void CopyInto(ResourceDictionary source, ResourceDictionary target)
    {
        foreach (var key in source.Keys)
        {
            target[key] = source[key];
        }

        foreach (var key in target.Keys.Cast<object>().ToList())
        {
            if (!source.Contains(key))
            {
                target.Remove(key);
            }
        }
    }
}
