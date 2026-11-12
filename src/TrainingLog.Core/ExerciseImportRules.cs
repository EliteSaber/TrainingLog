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
    /// Есть ли в поле хоть одна строка, которая попадёт в справочник при записи: тот же ответ,
    /// что «<see cref="PrepareForSave"/> не пуст», но без построения списка.
    /// </summary>
    /// <remarks>
    /// Кнопка сохранения проверяется на каждый символ в поле, и при тысячах строк каждая
    /// проверка платила бы полной пересборкой списка. Здесь первое же годное имя завершает
    /// работу, а имя ищется в множестве справочника по <see cref="ReadOnlySpan{Char}"/> —
    /// без единой аллокации. Повторы внутри поля на ответ не влияют: отбор повторов не
    /// выбрасывает первое вхождение, поэтому «есть принятое» и «есть годная строка» — одно и то же.
    /// </remarks>
    public static bool HasSavable(string? text, IReadOnlySet<string> existingNames, bool replaceExisting)
    {
        ArgumentNullException.ThrowIfNull(existingNames);

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Множество обычно HashSet: тогда имя ищется span'ом без аллокаций. Прочие реализации
        // IReadOnlySet обслуживаются обычным Contains — предикат обязан работать с любым.
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>>? lookup = existingNames is HashSet<string> set
            ? set.GetAlternateLookup<ReadOnlySpan<char>>()
            : null;

        var remaining = text.AsSpan();

        while (!remaining.IsEmpty)
        {
            var newline = remaining.IndexOf('\n');
            ReadOnlySpan<char> line;

            if (newline < 0)
            {
                line = remaining;
                remaining = [];
            }
            else
            {
                line = remaining[..newline];
                remaining = remaining[(newline + 1)..];
            }

            line = line.Trim();

            if (line.IsEmpty || line.Length > NameRules.MaxNameLength)
            {
                continue;
            }

            if (replaceExisting)
            {
                return true;
            }

            var exists = lookup is { } names
                ? names.Contains(line)
                : existingNames.Contains(line.ToString());

            if (!exists)
            {
                return true;
            }
        }

        return false;
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
