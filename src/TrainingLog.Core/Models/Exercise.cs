namespace TrainingLog.Core.Models;

/// <summary>
/// Упражнение из справочника.
/// </summary>
public sealed class Exercise
{
    /// <summary>
    /// Идентификатор упражнения. Значение <c>0</c> означает, что запись ещё не сохранена
    /// и идентификатор будет присвоен хранилищем.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Наименование упражнения.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
