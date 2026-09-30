using TrainingLog.Core.Models;

namespace TrainingLog.Core;

/// <summary>
/// Правила проверки названия упражнения. Общие для добавления и правки, чтобы две формы
/// не разошлись во мнении, что считать допустимым названием.
/// </summary>
public static class ExerciseNameRules
{
    /// <summary>
    /// Проверяет, можно ли добавить упражнение с названием <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Название допустимо, если оно непустое и не совпадает без учёта регистра ни с одним
    /// другим упражнением справочника. Действует при добавлении; для правки есть
    /// <see cref="IsValidEdit"/>, где сверх этого требуется, чтобы название изменилось.
    /// </remarks>
    /// <param name="name">Проверяемое название.</param>
    /// <param name="all">Упражнения, среди которых ищется совпадение.</param>
    /// <param name="excludeId">Упражнение, которое нужно исключить из поиска совпадения.</param>
    public static bool IsValid(string? name, IEnumerable<Exercise> all, int? excludeId = null)
    {
        ArgumentNullException.ThrowIfNull(all);

        var candidate = name?.Trim();

        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        return !IsTakenByOther(candidate, all, excludeId);
    }

    /// <summary>
    /// Проверяет, можно ли сохранить правку упражнения с названием <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Сверх условий <see cref="IsValid"/> требуется, чтобы название отличалось от прежнего.
    /// Сравнение правок идёт с учётом регистра: «Жим» → «жим» это изменение, ради которого
    /// есть смысл открывать окно правки, а вот добавленные пробелы — нет. Проверка дубля
    /// остаётся регистронезависимой, а исключать само упражнение должен вызывающий: достаточно
    /// передать <paramref name="all"/> без него.
    /// </remarks>
    /// <param name="name">Проверяемое название из поля ввода.</param>
    /// <param name="originalName">Название упражнения до правки.</param>
    /// <param name="all">Остальные упражнения справочника, без редактируемого.</param>
    public static bool IsValidEdit(string? name, string? originalName, IEnumerable<Exercise> all)
    {
        ArgumentNullException.ThrowIfNull(all);

        var candidate = name?.Trim();

        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        if (string.Equals(candidate, originalName?.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        return !IsTakenByOther(candidate, all, excludeId: null);
    }

    private static bool IsTakenByOther(string candidate, IEnumerable<Exercise> all, int? excludeId) =>
        all.Any(exercise => exercise.Id != excludeId
                            && string.Equals(exercise.Name, candidate, StringComparison.OrdinalIgnoreCase));
}
