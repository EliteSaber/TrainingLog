namespace TrainingLog.Core.Repositories;

/// <summary>Итог добавления записи журнала.</summary>
public enum AddTrainingSessionOutcome
{
    /// <summary>Запись добавлена.</summary>
    Added,

    /// <summary>Дата уникальна: одна дата — одна запись, вторая добавляется только как правка первой.</summary>
    DateTaken,

    /// <summary>Без названия плана строку дня не подписать.</summary>
    PlanIsEmpty,
}