namespace TrainingLog.Core.Models;

/// <summary>
/// Упражнение из справочника.
/// </summary>
public sealed class Exercise
{
    /// <summary>Идентификатор. <c>0</c> — запись ещё не сохранена.</summary>
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
