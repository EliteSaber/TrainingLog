namespace TrainingLog.Core.Repositories;

/// <summary>Результат изменения плана.</summary>
public enum UpdateTrainingPlanOutcome
{
    /// <summary>План сохранён вместе с новым составом упражнений.</summary>
    Updated,

    /// <summary>Наименование пустое либо из одних пробелов.</summary>
    NameIsEmpty,

    /// <summary>Сравнение с уже существующими регистронезависимое.</summary>
    DuplicateName,

    /// <summary>Плана с таким идентификатором нет.</summary>
    NotFound
}
