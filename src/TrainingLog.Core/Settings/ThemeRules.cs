namespace TrainingLog.Core.Settings;

/// <summary>
/// Пригодность настройки темы: канонические значения и разбор системной темы.
/// </summary>
/// <remarks>
/// Отдельным классом, а не свойствами у <see cref="AppSettings"/>, — по той же причине, что и
/// <see cref="DaysCountRules"/> отдельно от количества дней.
///
/// Канон — три слова: <c>Light</c>, <c>Dark</c>, <c>System</c>. Слова английские, а не русские
/// подписи окна: файл настроек правят руками, и значение в нём должно совпадать с тем, что
/// пишет программа. Подписи живут в WPF-слое.
/// </remarks>
public static class ThemeRules
{
    /// <summary>Светлая палитра.</summary>
    public const string Light = "Light";

    /// <summary>Тёмная палитра.</summary>
    public const string Dark = "Dark";

    /// <summary>Тема Windows: приложение берёт её у системы.</summary>
    public const string System = "System";

    /// <summary>
    /// Умолчание: тема как в Windows. Пока пользователь ничего не выбрал, приложение выглядит
    /// так же, как система, — иначе на тёмной Windows светлое приложение выбивалось бы из ряда.
    /// </summary>
    public const string Default = System;

    /// <summary>Все допустимые значения: их же показывает окно параметров.</summary>
    public static IReadOnlyList<string> Options { get; } = [Light, Dark, System];

    /// <summary>
    /// Проверка одна и та же для выбора в окне параметров и для значения из файла: пригодность
    /// не должна зависеть от того, откуда пришло значение.
    /// </summary>
    /// <remarks>
    /// Регистр не важен: файл правят руками, и «dark» не обязано терять настройку. Проверка
    /// сравнением со списком, а не разбором: лишние слова в файле не должны превращаться в тему.
    /// </remarks>
    public static bool IsValid(string? theme) => Find(theme) is not null;

    /// <summary>
    /// Канонический вид значения: «dark» → <c>Dark</c>, негодное значение → умолчание.
    /// </summary>
    /// <remarks>
    /// Негодное значение заменяется умолчанием, а не отвергается: значение из файла не должно
    /// ронять запуск приложения (тот же договор, что у количества дней и масштаба шрифта).
    /// </remarks>
    public static string Normalize(string? theme) => Find(theme) ?? Default;

    /// <summary>
    /// Действующая тема: «системная» сводится к светлой или тёмной.
    /// </summary>
    /// <remarks>
    /// Отдельная чистая функция, а не чтение реестра внутри: читать реестр умеет WPF-слой, а
    /// решение «что показывать» проверяется тестом без Windows.
    /// </remarks>
    /// <param name="theme">Выбор пользователя: светлая, тёмная или системная.</param>
    /// <param name="systemIsDark">Тёмная ли тема оформления Windows.</param>
    public static string Effective(string? theme, bool systemIsDark)
    {
        var normalized = Normalize(theme);

        return normalized == System
            ? (systemIsDark ? Dark : Light)
            : normalized;
    }

    /// <summary>Каноническое значение по имени без учёта регистра либо null, если имени нет.</summary>
    private static string? Find(string? theme)
    {
        var trimmed = theme?.Trim();

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
