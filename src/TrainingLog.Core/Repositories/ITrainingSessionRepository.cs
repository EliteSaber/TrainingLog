using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Доступ к журналу тренировок.
/// </summary>
public interface ITrainingSessionRepository
{
    /// <summary>Записи за период включительно, по дате по убыванию.</summary>
    /// <remarks>
    /// Упражнения и подходы приходят вместе с записью и упорядочены по номеру выполнения: строка
    /// дня показывает их слева направо именно в этом порядке.
    /// </remarks>
    Task<IReadOnlyList<TrainingSession>> GetAsync(
        DateOnly? fromInclusive = null,
        DateOnly? toInclusive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Запись за дату вместе с упражнениями и подходами либо <c>null</c>. Нужна окну добавления
    /// дня: на ту же дату запись уже есть — значит открывается правка, а не добавление.
    /// </summary>
    Task<TrainingSession?> GetByDateAsync(
        DateOnly targetDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ближайшая запись строго раньше <paramref name="before"/> по указанному плану вместе с
    /// упражнениями и подходами либо <c>null</c>. Записи без плана или с другим планом
    /// пропускаются: подтягивать нечего. План сопоставляется по идентификатору, а не по
    /// названию — планы переименовывают, и совпавшее название ничего бы не значило.
    /// </summary>
    Task<TrainingSession?> GetPreviousByPlanAsync(
        int planId,
        DateOnly before,
        CancellationToken cancellationToken = default);

    /// <summary>В переданный объект записывается присвоенный идентификатор.</summary>
    /// <remarks>
    /// Дата уникальна и проверяется базой: предварительная проверка может разойтись с
    /// уникальным индексом, если запись на ту же дату добавили из другого окна.
    /// </remarks>
    Task<AddTrainingSessionOutcome> AddAsync(
        TrainingSession session,
        CancellationToken cancellationToken = default);

    /// <summary>Переписывает дату, план, упражнения и подходы, не меняя идентификатор записи.</summary>
    /// <remarks>
    /// Упражнение или план, удалённые из справочников, пока окно было открыто, молча выпадают
    /// из записи, а названия остаются — историю не переписываем.
    /// </remarks>
    Task<UpdateTrainingSessionOutcome> UpdateAsync(
        TrainingSession session,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает <c>false</c>, если записи нет.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}