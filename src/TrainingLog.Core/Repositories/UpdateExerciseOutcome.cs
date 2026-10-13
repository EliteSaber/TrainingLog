namespace TrainingLog.Core.Repositories;

/// <summary>Результат изменения упражнения в справочнике.</summary>
public enum UpdateExerciseOutcome
{
    /// <summary>Название изменено.</summary>
    Updated,

    /// <summary>Название пустое либо из одних пробелов.</summary>
    NameIsEmpty,

    /// <summary>Сравнение с уже существующими регистронезависимое.</summary>
    DuplicateName,

    /// <summary>Упражнения с таким идентификатором в справочнике нет.</summary>
    NotFound
}
