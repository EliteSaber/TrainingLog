namespace TrainingLog.Core.Repositories;

/// <summary>
/// Результат добавления упражнения в справочник.
/// </summary>
public enum AddExerciseOutcome
{
    /// <summary>
    /// Упражнение добавлено, идентификатор присвоен.
    /// </summary>
    Added,

    /// <summary>
    /// Название пустое либо состоит только из пробелов.
    /// </summary>
    NameIsEmpty,

    /// <summary>
    /// Упражнение с таким названием уже есть в справочнике. Сравнение регистронезависимое.
    /// </summary>
    DuplicateName
}
