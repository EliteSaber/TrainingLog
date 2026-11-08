using System.Windows;
using System.Windows.Media;
using TrainingLog.Core.Settings;

namespace TrainingLog.Services;

/// <summary>
/// Применение шрифтовой настройки к приложению: подмена токенов в ресурсах приложения.
/// </summary>
/// <remarks>
/// Зовётся дважды: при старте (до показа окон — иначе главное окно рисовалось бы умолчанием)
/// и окном параметров при выборе и после записи файла. Подмена значения в
/// <see cref="Application.Current"/> перерисовывает все открытые окна по ссылкам
/// DynamicResource — тот же приём, которым позже сменит словарь будущий переключатель темы.
///
/// Application.Current без значения — тесты и дизайнер: токены остаются умолчаниями
/// Fonts.xaml, подменять нечего.
/// </remarks>
public static class AppFonts
{
    public const string FamilyKey = "AppFontFamily";
    public const string BodyKey = "BodySize";
    public const string TitleKey = "TitleSize";
    public const string LargeKey = "LargeSize";
    public const string SubtitleKey = "SubtitleSize";

    /// <summary>Подставляет гарнитуру и масштабированные кегли в ресурсы приложения.</summary>
    public static void Apply(string fontFamily, int fontScalePercent)
    {
        var resources = Application.Current?.Resources;

        if (resources is null)
        {
            return;
        }

        resources[FamilyKey] = ResolveFamily(fontFamily);

        var scale = FontRules.IsScaleValid(fontScalePercent)
            ? fontScalePercent
            : FontRules.DefaultScalePercent;

        // База кеглей читается из исходного словаря Fonts.xaml, а не свойством-поиском по
        // ресурсам: после первой же подмены в корне лежат масштабированные значения, и второй
        // масштаб на них умножил бы процент повторно.
        if (FindFontDictionary(resources) is not { } fonts)
        {
            return;
        }

        ApplySize(resources, fonts, BodyKey, scale);
        ApplySize(resources, fonts, TitleKey, scale);
        ApplySize(resources, fonts, LargeKey, scale);
        ApplySize(resources, fonts, SubtitleKey, scale);
    }

    /// <summary>Имя из настроек: установленное семейство, а без него — гарнитура по умолчанию.</summary>
    /// <remarks>
    /// Сравнение без учёта регистра: файл правят руками, и «fira code» с маленькой буквы не
    /// обязан потерями настройки.
    /// </remarks>
    private static FontFamily ResolveFamily(string fontFamily)
    {
        var name = FontRules.NormalizeName(fontFamily);

        return name.Length > 0
           && Fonts.SystemFontFamilies.FirstOrDefault(family =>
                   string.Equals(family.Source, name, StringComparison.OrdinalIgnoreCase)) is { } installed
            ? installed
            : new FontFamily(FontRules.DefaultFontFamily);
    }

    private static void ApplySize(
        ResourceDictionary resources,
        ResourceDictionary fonts,
        string key,
        int scale)
    {
        if (fonts[key] is double baseSize)
        {
            resources[key] = FontRules.Scale(baseSize, scale);
        }
    }

    /// <summary>Ищет словарь Fonts.xaml среди вложенных: его подменять нельзя, это источник базы.</summary>
    private static ResourceDictionary? FindFontDictionary(ResourceDictionary current)
    {
        foreach (var merged in current.MergedDictionaries)
        {
            if (merged.Contains(FamilyKey))
            {
                return merged;
            }

            if (FindFontDictionary(merged) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }
}
