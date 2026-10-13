namespace TrainingLog.Core.Repositories;

/// <summary>Результат добавления упражнения в справочник.</summary>
public enum AddExerciseOutcome
{
    /// <summary>Идентификатор присвоен.</summary>
    Added,

    /// <summary>Название пустое либо из одних пробелов.</summary>
    NameIsEmpty,

    /// <summary>Сравнение с уже существующими регистронезависимое.</summary>
    DuplicateName
}
