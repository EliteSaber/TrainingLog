namespace TrainingLog.Core.Settings;

/// <summary>
/// Поведение примечания в окне дня: канонические значения, разбор и правило раскрытия.
/// </summary>
/// <remarks>
/// Отдельным классом, а не свойствами у <see cref="AppSettings"/>, — по той же причине, что и
/// <see cref="ThemeRules"/>: правило одно для файла и для окна параметров.
///
/// Канон — английские слова: файл настроек правят руками, и значение в нём должно совпадать с
/// тем, что пишет программа. Русские подписи живут в WPF-слое.
/// </remarks>
public static class NotesExpansionRules
{
    /// <summary>Примечание всегда свёрнуто.</summary>
    public const string Collapsed = "Collapsed";

    /// <summary>Раскрыто, если в примечании есть текст.</summary>
    public const string WhenFilled = "WhenFilled";

    /// <summary>Раскрыто, если есть подсказка — примечание прошлого дня.</summary>
    public const string WhenHint = "WhenHint";

    /// <summary>Раскрыто, если заполнено или есть подсказка.</summary>
    public const string WhenFilledOrHint = "WhenFilledOrHint";

    /// <summary>Раскрыто всегда.</summary>
    public const string Expanded = "Expanded";

    /// <summary>
    /// Умолчание: свёрнуто. Примечаний у упражнения обычно нет, и постоянно раскрытое поле
    /// съедало бы высоту области ввода.
    /// </summary>
    public const string Default = Collapsed;

    /// <summary>Все допустимые значения: их же показывает окно параметров.</summary>
    public static IReadOnlyList<string> Options { get; } =
        [Collapsed, WhenFilled, WhenHint, WhenFilledOrHint, Expanded];

    /// <summary>Проверка одна и та же для выбора в окне параметров и для значения из файла.</summary>
    public static bool IsValid(string? value) => Find(value) is not null;

    /// <summary>
    /// Канонический вид значения: «whenfilled» → <c>WhenFilled</c>, негодное значение → умолчание.
    /// </summary>
    /// <remarks>
    /// Негодное значение заменяется умолчанием, а не отвергается: значение из файла не должно
    /// ронять запуск приложения (тот же договор, что у темы и количества дней).
    /// </remarks>
    public static string Normalize(string? value) => Find(value) ?? Default;

    /// <summary>Раскрывать ли примечание по выбранному правилу.</summary>
    /// <remarks>
    /// Чистая функция: «заполнено» и «есть подсказка» приходят признаками строки упражнения,
    /// и правило проверяется тестом без окна.
    /// </remarks>
    /// <param name="value">Значение из файла настроек или окна параметров.</param>
    /// <param name="hasNote">В примечании есть непробельный текст.</param>
    /// <param name="hasHint">Есть примечание прошлого дня, показанное подсказкой.</param>
    public static bool IsExpanded(string? value, bool hasNote, bool hasHint) =>
        Normalize(value) switch
        {
            WhenFilled => hasNote,
            WhenHint => hasHint,
            WhenFilledOrHint => hasNote || hasHint,
            Expanded => true,
            _ => false,
        };

    /// <summary>Каноническое значение по имени без учёта регистра либо null, если имени нет.</summary>
    private static string? Find(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        foreach (var option in Options)
        {
            if (string.Equals(option, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
    }
}
