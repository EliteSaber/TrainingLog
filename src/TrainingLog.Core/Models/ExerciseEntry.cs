namespace TrainingLog.Core.Models;

/// <summary>
/// Выполненное упражнение в рамках одной тренировки.
/// </summary>
public sealed class ExerciseEntry
{
    private readonly List<TrainingSet> _sets = [];

    /// <summary>
    /// Упражнение из справочника.
    /// </summary>
    public Exercise Exercise { get; set; } = new();

    /// <summary>
    /// Подходы упражнения. Их количество произвольно: повторения и вес в каждом подходе свои.
    /// </summary>
    public IReadOnlyList<TrainingSet> Sets => _sets;

    /// <summary>
    /// Заметка к упражнению.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Добавляет подход с указанными повторениями и весом.
    /// </summary>
    public TrainingSet AddSet(int repetitions, decimal weight)
    {
        var set = new TrainingSet { Repetitions = repetitions, Weight = weight };
        _sets.Add(set);
        return set;
    }
}
