using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Доступ к журналу тренировок.
/// </summary>
public interface ITrainingSessionRepository
{
    /// <summary>
    /// Возвращает записи журнала за период включительно, отсортированные по дате по убыванию.
    /// </summary>
    /// <param name="fromInclusive">Начало периода или <c>null</c>, если ограничение не задано.</param>
    /// <param name="toInclusive">Конец периода или <c>null</c>, если ограничение не задано.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<IReadOnlyList<TrainingSession>> GetAsync(
        DateOnly? fromInclusive = null,
        DateOnly? toInclusive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет новую запись журнала и возвращает присвоенный идентификатор.
    /// </summary>
    Task<int> AddAsync(TrainingSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет существующую запись журнала.
    /// </summary>
    Task UpdateAsync(TrainingSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет запись журнала по идентификатору.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
