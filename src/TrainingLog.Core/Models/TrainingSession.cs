namespace TrainingLog.Core.Models;

/// <summary>
/// Запись журнала тренировок за один день.
/// </summary>
public sealed class TrainingSession
{
    private readonly List<ExerciseEntry> _exercises = [];

    /// <summary>
    /// Идентификатор записи. Значение <c>0</c> означает, что запись ещё не сохранена
    /// и идентификатор будет присвоен хранилищем.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Дата тренировки без времени.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Выполненные упражнения.
    /// </summary>
    public IReadOnlyList<ExerciseEntry> Exercises => _exercises;

    /// <summary>
    /// Заметка к тренировке.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Добавляет выполненное упражнение к тренировке.
    /// </summary>
    public ExerciseEntry AddExercise(ExerciseEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _exercises.Add(entry);
        return entry;
    }

    /// <summary>
    /// Добавляет упражнение из справочника и возвращает добавленную запись.
    /// </summary>
    public ExerciseEntry AddExercise(Exercise exercise, string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return AddExercise(new ExerciseEntry { Exercise = exercise, Notes = notes });
    }
}
