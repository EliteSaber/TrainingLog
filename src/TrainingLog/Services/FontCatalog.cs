using System.Windows.Media;

namespace TrainingLog.Services;

/// <summary>
/// Перечень установленных шрифтов для окна параметров: все семейства системы списком и
/// поиск по нему.
/// </summary>
/// <remarks>
/// Пригодность кириллицы список не проверяет — владелец отказался от проверки: если выбранное
/// семейство нужных букв не знает, WPF отрисует недостающие композитным заменителем, и текст
/// останется читаемым в двух гарнитурах.
/// </remarks>
public sealed class FontCatalog
{
    /// <summary>Пункт выбора: каноническое имя семейства.</summary>
    /// <remarks>
    /// Имя каноническое, не локализованное: записанное локальное имя не нашлось бы на машине
    /// с другой локалью, и совет был бы настоящей потерей настройки.
    /// </remarks>
    public sealed record FontFamilyOption(string Name);

    /// <summary>Установленные семейства по алфавиту, без кириллического фильтра.</summary>
    /// <remarks>
    /// Сортировка OrdinalIgnoreCase: имена большей частью латиница, а сравнение кодов Unicode
    /// для неё же — то же, что и порядок строк у строковых списков проекта.
    /// </remarks>
    public static IReadOnlyList<FontFamilyOption> Families() =>
        Fonts.SystemFontFamilies
            .Select(family => new FontFamilyOption(family.Source!))
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Поиск «содержит» без учёта регистра; пустой и пробельный запрос возвращает весь список.</summary>
    public static IReadOnlyList<FontFamilyOption> Filter(IEnumerable<FontFamilyOption> options, string? search)
    {
        var query = search?.Trim() ?? string.Empty;

        return query.Length == 0
            ? options.ToList()
            : [.. options.Where(option => option.Name.Contains(query, StringComparison.OrdinalIgnoreCase))];
    }
}
