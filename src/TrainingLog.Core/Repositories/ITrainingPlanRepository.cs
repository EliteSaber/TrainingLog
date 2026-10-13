using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Доступ к планам тренировок.
/// </summary>
public interface ITrainingPlanRepository
{
    /// <summary>Порядок не гарантируется — его определяет вызывающий.</summary>
    /// <remarks>
    /// Упражнения приходят вместе с планом, в отличие от <see cref="IExerciseRepository"/>:
    /// список планов раскрывает состав прямо в строке, и догружать его по раскрытию означало бы
    /// асинхронную загрузку на каждое нажатие.
    /// </remarks>
    Task<IReadOnlyList<TrainingPlan>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>При успехе в переданный объект пишутся обрезанное наименование, идентификатор и найденный состав.</summary>
    Task<AddTrainingPlanOutcome> AddAsync(TrainingPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Состав упражнений в переданном объекте не изменяется — вызывающий перечитывает планы
    /// заново. Наименование сверяется со всеми остальными планами, кроме самой записи.
    /// </summary>
    Task<UpdateTrainingPlanOutcome> UpdateAsync(TrainingPlan plan, CancellationToken cancellationToken = default);

    /// <summary>Возвращает <c>false</c>, если плана нет.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Сравнение регистронезависимое.</summary>
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
}
