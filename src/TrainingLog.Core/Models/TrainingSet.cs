namespace TrainingLog.Core.Models;

/// <summary>
/// Один подход упражнения.
/// </summary>
public sealed class TrainingSet
{
    /// <summary>
    /// Количество повторений в подходе.
    /// </summary>
    public int Repetitions { get; set; }

    /// <summary>
    /// Вес подхода в килограммах. Значение <c>0</c> означает упражнение без дополнительного веса.
    /// </summary>
    public decimal Weight { get; set; }
}
