namespace TrainingLog.Core.Repositories;

/// <summary>Результат добавления плана.</summary>
public enum AddTrainingPlanOutcome
{
    /// <summary>План добавлен.</summary>
    Added,

    /// <summary>Наименование пустое либо из одних пробелов.</summary>
    NameIsEmpty,

    /// <summary>Сравнение с уже существующими регистронезависимое.</summary>
    DuplicateName
}
