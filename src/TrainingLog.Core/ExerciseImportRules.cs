namespace TrainingLog.Core;

/// <summary>
/// Правила импорта упражнений из файла: что попадает в список, а что отбрасывается.
/// </summary>
public static class ExerciseImportRules
{
    /// <summary>
    /// Принимает строки файла: обрезает края, отбрасывает пустые, слишком длинные и повторяющиеся.
    /// </summary>
    /// <remarks>
    /// Дубль от текущего справочника здесь не отбрасывается: в окне импорта такие строки видны, а
    /// пропускаются уже при записи — см. <see cref="PrepareForSave"/>.
    ///
    /// Повторы отбираются множеством, а не вызовом <see cref="NameRules.IsValid"/> на каждую строку:
    /// правило перебирает переданный список целиком, и файл в сотни строк разбирался бы за квадрат.
    /// </remarks>
    public static IReadOnlyList<string> Read(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accepted = new List<string>();

        foreach (var line in lines)
        {
            var candidate = line.Trim();

            if (string.IsNullOrEmpty(candidate) || candidate.Length > NameRules.MaxNameLength)
            {
                continue;
            }

            if (seen.Add(candidate))
            {
                accepted.Add(candidate);
            }
        }

        return accepted;
    }

    /// <summary>
    /// Перепроверка перед записью: то же, что <see cref="Read"/>, плюс отбрасывание упражнений,
    /// которые уже есть в справочнике, если справочник не заменяется целиком.
    /// </summary>
    public static IReadOnlyList<string> PrepareForSave(
        IEnumerable<string> text,
        IEnumerable<string> existingNames,
        bool replaceExisting)
    {
        ArgumentNullException.ThrowIfNull(existingNames);

        var accepted = Read(text);

        if (replaceExisting)
        {
            return accepted;
        }

        var existing = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        return accepted.Where(name => !existing.Contains(name)).ToList();
    }

    /// <summary>
    /// Делит текст на строки. И <c>\r\n</c>, и <c>\n</c> считаются концом строки: текст приходит
    /// и из файла, где переводы строк пишет редактор, и из поля окна, где их пишет пользователь.
    /// </summary>
    public static IEnumerable<string> SplitLines(string? text) =>
        string.IsNullOrEmpty(text)
            ? []
            : text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
