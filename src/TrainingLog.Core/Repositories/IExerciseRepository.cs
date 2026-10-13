using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Доступ к справочнику упражнений.
/// </summary>
public interface IExerciseRepository
{
    /// <summary>Справочник изменился: упражнение добавлено, переименовано или удалено.</summary>
    /// <remarks>
    /// Поднимается после успешной записи, поэтому подписчик сразу читает актуальные данные;
    /// отказы — пустое наименование, дубль, неизвестный идентификатор — события не поднимают.
    /// Событие на потоке вызова, то есть в приложении на потоке интерфейса.
    ///
    /// Поднимает его репозиторий, а не модель представления: пишет только он, и новая команда
    /// записи не сможет «забыть» про уведомление. Подписка при этом живёт в окне, потому что у
    /// модели представления нет явного срока жизни, а репозиторий синглтон.
    /// </remarks>
    event EventHandler? Changed;

    /// <summary>Порядок не гарантируется — его определяет вызывающий.</summary>
    Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>При успехе в переданный объект пишутся обрезанное название и присвоенный идентификатор.</summary>
    Task<AddExerciseOutcome> AddAsync(Exercise exercise, CancellationToken cancellationToken = default);

    /// <summary>Название сверяется со всеми остальными упражнениями, кроме самой записи.</summary>
    Task<UpdateExerciseOutcome> UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default);

    /// <summary>Возвращает <c>false</c>, если упражнения нет.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Массовая запись справочника из файла: одной транзакцией, а не вызовом <see cref="AddAsync"/>
    /// на каждое упражнение.
    /// </summary>
    /// <param name="names">
    /// Названия в порядке файла. Проверяются целиком тем же правилом, что и в окне импорта:
    /// пустое, слишком длинное или повторяющееся название отменяет всю запись, а не молча
    /// выбрасывается из неё.
    /// </param>
    /// <param name="replaceExisting">
    /// <c>true</c> — справочник станет ровно этим списком. Удаление необратимо: упражнение исчезает
    /// из всех планов, а в журнале остаётся копия названия без ссылки.
    /// </param>
    Task<ImportExercisesOutcome> ImportAsync(
        IReadOnlyList<string> names,
        bool replaceExisting,
        CancellationToken cancellationToken = default);

    /// <summary>Сравнение регистронезависимое.</summary>
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
}
