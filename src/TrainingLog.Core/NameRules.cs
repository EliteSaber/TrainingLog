namespace TrainingLog.Core;

/// <summary>
/// Правила проверки наименования. Общие для упражнений и планов, чтобы справочник и список
/// планов не разошлись во мнении, что считать допустимым наименованием.
/// </summary>
public static class NameRules
{
    /// <summary>
    /// Предел длины наименования.
    /// </summary>
    /// <remarks>
    /// Считается по обрезанному наименованию, поэтому хвостовые пробелы длину не занимают.
    /// Поля ввода ссылаются на эту же константу через <c>x:Static</c>, поэтому правило
    /// нельзя обойти вводом, а лимит нельзя разъехать между формами.
    /// </remarks>
    public const int MaxNameLength = 100;

    /// <summary>
    /// Проверяет, можно ли добавить запись с наименованием <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Наименование допустимо, если оно непустое, не длиннее <see cref="MaxNameLength"/> и не
    /// совпадает без учёта регистра ни с одним уже существующим. Проверка дубля идёт по всему
    /// списку, а не по видимым строкам: иначе при активном поиске можно было бы добавить
    /// «жим лёжа», когда он в базе уже есть, просто отфильтрованный.
    /// </remarks>
    /// <param name="name">Проверяемое наименование из поля ввода.</param>
    /// <param name="existingNames">
    /// Наименования всех прочих записей того же списка — упражнений или планов.
    /// Уже существующую запись при правке передавать сюда не нужно: для этого есть
    /// <see cref="IsValidEdit"/>.
    /// </param>
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
    /// Проверяет, можно ли сохранить правку записи с наименованием <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Сверх условий <see cref="IsValid"/> требуется, чтобы наименование отличалось от прежнего.
    /// Сравнение правок идёт с учётом регистра: «Жим» → «жим» это изменение, ради которого
    /// есть смысл открывать окно правки, а вот добавленные пробелы — нет. Проверка дубля
    /// остаётся регистронезависимой, а исключать саму запись должен вызывающий: достаточно
    /// передать <paramref name="otherNames"/> без неё.
    /// </remarks>
    /// <param name="name">Проверяемое наименование из поля ввода.</param>
    /// <param name="originalName">Наименование записи до правки.</param>
    /// <param name="otherNames">Наименования остальных записей того же списка.</param>
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
