namespace TrainingLog.Core.Repositories;

/// <summary>
/// Результат добавления плана.
/// </summary>
public enum AddTrainingPlanOutcome
{
    /// <summary>
    /// План добавлен.
    /// </summary>
    Added,

    /// <summary>
    /// Наименование пустое либо состоит только из пробелов.
    /// </summary>
    NameIsEmpty,

    /// <summary>
    /// Наименование уже занято другим планом. Сравнение регистронезависимое.
    /// </summary>
    DuplicateName
}
