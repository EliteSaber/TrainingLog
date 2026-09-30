using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Доступ к справочнику упражнений.
/// </summary>
public interface IExerciseRepository
{
    /// <summary>
    /// Возвращает все упражнения справочника. Порядок не гарантируется — его определяет вызывающий.
    /// </summary>
    Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет упражнение в справочник. При успехе в переданный объект записываются
    /// обрезанное название и присвоенный идентификатор.
    /// </summary>
    Task<AddExerciseOutcome> AddAsync(Exercise exercise, CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет название существующего упражнения. Название сверяется со всеми остальными
    /// упражнениями, поэтому сохранение без изменений дубликатом не считается.
    /// </summary>
    Task<UpdateExerciseOutcome> UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет упражнение из справочника. Возвращает <c>false</c>, если упражнения нет.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет, занято ли название в справочнике. Сравнение регистронезависимое.
    /// </summary>
    /// <param name="name">Проверяемое название.</param>
    /// <param name="excludeId">
    /// Идентификатор упражнения, которое нужно исключить из проверки. Используется при правке,
    /// чтобы сохранение под собственным именем не сочлось дубликатом.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
}
