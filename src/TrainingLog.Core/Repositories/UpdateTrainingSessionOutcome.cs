namespace TrainingLog.Core.Repositories;

/// <summary>
/// Итог правки записи журнала.
/// </summary>
public enum UpdateTrainingSessionOutcome
{
    /// <summary>Запись сохранена.</summary>
    Updated,

    /// <summary>
    /// Записи с таким идентификатором в базе нет: её удалили, пока окно правки было открыто.
    /// </summary>
    NotFound,
}