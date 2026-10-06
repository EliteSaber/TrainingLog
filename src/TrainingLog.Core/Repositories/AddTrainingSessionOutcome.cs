namespace TrainingLog.Core.Repositories;

/// <summary>
/// Итог добавления записи журнала.
/// </summary>
public enum AddTrainingSessionOutcome
{
    /// <summary>Запись добавлена.</summary>
    Added,

    /// <summary>
    /// На эту дату запись уже есть. Дата уникальна: одна дата — одна запись, и вторая
    /// добавляется только как правка первой.
    /// </summary>
    DateTaken,

    /// <summary>Не задано название плана — без него строку дня не подписать.</summary>
    PlanIsEmpty,
}