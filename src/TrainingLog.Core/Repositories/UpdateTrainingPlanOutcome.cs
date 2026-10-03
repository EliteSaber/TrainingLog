namespace TrainingLog.Core.Repositories;

/// <summary>
/// Результат изменения плана.
/// </summary>
public enum UpdateTrainingPlanOutcome
{
    /// <summary>
    /// План сохранён вместе с новым составом упражнений.
    /// </summary>
    Updated,

    /// <summary>
    /// Наименование пустое либо состоит только из пробелов.
    /// </summary>
    NameIsEmpty,

    /// <summary>
    /// Наименование уже занято другим планом. Сравнение регистронезависимое.
    /// </summary>
    DuplicateName,

    /// <summary>
    /// Плана с таким идентификатором нет.
    /// </summary>
    NotFound
}
