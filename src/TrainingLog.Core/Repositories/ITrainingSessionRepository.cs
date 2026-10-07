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
    /// <remarks>
    /// Упражнения и подходы приходят вместе с записью и упорядочены по номеру выполнения:
    /// строка дня показывает их слева направо именно в этом порядке, и догружать их отдельно
    /// означало бы асинхронную загрузку на каждую строку.
    /// </remarks>
    /// <param name="fromInclusive">Начало периода или <c>null</c>, если ограничение не задано.</param>
    /// <param name="toInclusive">Конец периода или <c>null</c>, если ограничение не задано.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<IReadOnlyList<TrainingSession>> GetAsync(
        DateOnly? fromInclusive = null,
        DateOnly? toInclusive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает запись за дату вместе с упражнениями и подходами либо <c>null</c>,
    /// если записи нет.
    /// </summary>
    /// <remarks>
    /// Нужна окну добавления дня: на ту же дату запись уже есть — значит открывается правка,
    /// а не добавление.
    /// </remarks>
    /// <param name="targetDate">Дата записи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<TrainingSession?> GetByDateAsync(
        DateOnly targetDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ближайшую запись строго раньше <paramref name="before"/> по указанному плану
    /// вместе с упражнениями и подходами либо <c>null</c>, если такой записи нет.
    /// </summary>
    /// <remarks>
    /// Нужна окну добавления дня: при выборе плана подходы и веса прошлого раза с тем же
    /// планом подставляются в поля, а повторения показываются плейсхолдером. Записи без
    /// плана или с другим планом пропускаются: подтягивать нечего.
    ///
    /// План сопоставляется по идентификатору, а не по названию — так же, как запись с датой
    /// сопоставляется с планом в окне правки: планы переименовывают, и совпавшее название
    /// ничего бы не значило.
    /// </remarks>
    /// <param name="planId">Идентификатор плана.</param>
    /// <param name="before">Дата, строго раньше которой ищется запись.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<TrainingSession?> GetPreviousByPlanAsync(
        int planId,
        DateOnly before,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет запись журнала. В переданный объект записывается присвоенный идентификатор.
    /// </summary>
    /// <remarks>
    /// Дата уникальна и проверяется базой: предварительная проверка может разойтись с
    /// уникальным индексом, если запись на ту же дату добавили из другого окна.
    /// </remarks>
    Task<AddTrainingSessionOutcome> AddAsync(
        TrainingSession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Переписывает запись журнала: дату, план, упражнения и подходы.
    /// </summary>
    /// <remarks>
    /// Идентификатор записи не меняется, поэтому новый объект создавать не нужно: то же,
    /// что делает правка плана. Упражнение или план, удалённые из справочников, пока окно
    /// было открыто, молча выпадают из записи, а названия остаются — историю не переписываем.
    /// </remarks>
    Task<UpdateTrainingSessionOutcome> UpdateAsync(
        TrainingSession session,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет запись журнала вместе с упражнениями и подходами. Возвращает <c>false</c>,
    /// если записи нет.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}