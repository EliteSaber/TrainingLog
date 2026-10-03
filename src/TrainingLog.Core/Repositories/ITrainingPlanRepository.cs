using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Доступ к планам тренировок.
/// </summary>
public interface ITrainingPlanRepository
{
    /// <summary>
    /// Возвращает все планы вместе с их упражнениями. Порядок не гарантируется — его
    /// определяет вызывающий.
    /// </summary>
    /// <remarks>
    /// Упражнения приходят вместе с планом, в отличие от <see cref="IExerciseRepository"/>:
    /// список планов раскрывает состав прямо в строке, и догружать его по раскрытию
    /// означало бы асинхронную загрузку на каждое нажатие. Для небольшого справочника
    /// это дешевле, чем лишний запрос на каждое раскрытие.
    /// </remarks>
    Task<IReadOnlyList<TrainingPlan>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет план. При успехе в переданный объект записываются обрезанное наименование,
    /// присвоенный идентификатор и те упражнения, которые удалось найти в справочнике.
    /// </summary>
    Task<AddTrainingPlanOutcome> AddAsync(TrainingPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет наименование и полностью заменяет состав упражнений плана.
    /// Наименование сверяется со всеми остальными планами, поэтому сохранение без изменений
    /// дубликатом не считается. Состав упражнений в переданном объекте не изменяется:
    /// вызывающий перечитывает планы заново.
    /// </summary>
    Task<UpdateTrainingPlanOutcome> UpdateAsync(TrainingPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет план вместе с его составом упражнений. Возвращает <c>false</c>, если плана нет.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет, занято ли наименование среди планов. Сравнение регистронезависимое.
    /// </summary>
    /// <param name="name">Проверяемое наименование.</param>
    /// <param name="excludeId">
    /// Идентификатор плана, который нужно исключить из проверки. Используется при правке,
    /// чтобы сохранение под собственным именем не сочлось дубликатом.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
}
