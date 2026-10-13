namespace TrainingLog.Core.Repositories;

/// <summary>Результат массовой записи справочника из файла.</summary>
public enum ImportExercisesOutcome
{
    /// <summary>Все названия записаны.</summary>
    Imported,

    /// <summary>Записывать нечего: после отбрасывания негодных не осталось ни одного названия.</summary>
    NothingToImport,

    /// <summary>В списке есть пустое, слишком длинное или повторяющееся название.</summary>
    InvalidName,

    /// <summary>Название уже занято в справочнике — сработал уникальный индекс <c>NameKey</c>.</summary>
    DuplicateName
}
