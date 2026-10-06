namespace TrainingLog.Core.Models;

/// <summary>
/// Один подход упражнения.
/// </summary>
public sealed class TrainingSet
{
    /// <summary>
    /// Идентификатор подхода. Значение <c>0</c> означает, что подход ещё не сохранён.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор выполненного упражнения, которому принадлежит подход. Внешний ключ
    /// хранится явно, а не тенью: так же устроена строка связи плана с упражнением.
    /// </summary>
    public int ExerciseEntryId { get; set; }

    /// <summary>
    /// Место подхода в упражнении, с единицы. Задаётся вызывающим, а хранилище
    /// перенумеровывает подходы при записи.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Количество повторений в подходе.
    /// </summary>
    public int Repetitions { get; set; }

    /// <summary>
    /// Вес подхода в килограммах. Значение <c>0</c> означает упражнение без дополнительного веса.
    /// </summary>
    public decimal Weight { get; set; }
}