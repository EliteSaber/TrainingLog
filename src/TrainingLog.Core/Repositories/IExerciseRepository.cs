using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Доступ к справочнику упражнений.
/// </summary>
public interface IExerciseRepository
{
    /// <summary>
    /// Возвращает справочник упражнений по алфавиту.
    /// </summary>
    Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет упражнение в справочник и возвращает присвоенный идентификатор.
    /// </summary>
    Task<int> AddAsync(Exercise exercise, CancellationToken cancellationToken = default);
}
