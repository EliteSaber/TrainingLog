namespace TrainingLog.Core.Repositories;

/// <summary>
/// Результат изменения упражнения в справочнике.
/// </summary>
public enum UpdateExerciseOutcome
{
    /// <summary>
    /// Название изменено.
    /// </summary>
    Updated,

    /// <summary>
    /// Название пустое либо состоит только из пробелов.
    /// </summary>
    NameIsEmpty,

    /// <summary>
    /// Название уже занято другим упражнением. Сравнение регистронезависимое.
    /// </summary>
    DuplicateName,

    /// <summary>
    /// Упражнения с таким идентификатором в справочнике нет.
    /// </summary>
    NotFound
}
