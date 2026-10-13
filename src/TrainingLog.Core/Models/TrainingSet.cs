namespace TrainingLog.Core.Models;

/// <summary>Один подход упражнения.</summary>
public sealed class TrainingSet
{
    /// <summary>Идентификатор. <c>0</c> — запись ещё не сохранена.</summary>
    public int Id { get; set; }

    /// <summary>Внешний ключ объявлен явно, а не тенью — так же устроена строка связи плана с упражнением.</summary>
    public int ExerciseEntryId { get; set; }

    /// <summary>Номера задаёт вызывающий, а хранилище перенумеровывает подходы при записи.</summary>
    public int Order { get; set; }

    public int Repetitions { get; set; }

    /// <summary>Вес в килограммах. <c>0</c> — упражнение без дополнительного веса.</summary>
    public decimal Weight { get; set; }
}