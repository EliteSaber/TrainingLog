namespace TrainingLog.Core;

/// <summary>
/// Правила проверки наименования, общие для упражнений и планов: одна сущность за наименованием
/// правилу не нужна.
/// </summary>
public static class NameRules
{
    /// <summary>
    /// Предел длины. Считается по обрезанному наименованию, поэтому хвостовые пробелы длину
    /// не занимают; поля ввода ссылаются на ту же константу через <c>x:Static</c>.
    /// </summary>
    public const int MaxNameLength = 100;

    /// <summary>
    /// Проверка дубля идёт по всему переданному списку, а не по видимым строкам: иначе при
    /// активном поиске можно было бы добавить «жим лёжа», когда он в базе уже есть, просто
    /// отфильтрованный. Свою запись при правке исключает вызывающий — для этого есть
    /// <see cref="IsValidEdit"/>.
    /// </summary>
    public static bool IsValid(string? name, IEnumerable<string> existingNames)
    {
        ArgumentNullException.ThrowIfNull(existingNames);

        var candidate = name?.Trim();

        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        if (candidate.Length > MaxNameLength)
        {
            return false;
        }

        return !IsTakenByOther(candidate, existingNames);
    }

    /// <summary>
    /// Сверх <see cref="IsValid"/> требуется, чтобы наименование отличалось от прежнего.
    /// Сравнение правок идёт с учётом регистра: «Жим» → «жим» это изменение, ради которого
    /// есть смысл открывать окно правки, а вот добавленные пробелы — нет.
    /// </summary>
    public static bool IsValidEdit(string? name, string? originalName, IEnumerable<string> otherNames)
    {
        ArgumentNullException.ThrowIfNull(otherNames);

        var candidate = name?.Trim();

        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        if (candidate.Length > MaxNameLength)
        {
            return false;
        }

        if (string.Equals(candidate, originalName?.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        return !IsTakenByOther(candidate, otherNames);
    }

    private static bool IsTakenByOther(string candidate, IEnumerable<string> existingNames) =>
        existingNames.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase));
}
