using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.Tests;

/// <summary>
/// Журнал тренировок с управляемой задержкой на чтении прошлого дня.
/// </summary>
/// <remarks>
/// Настоящий репозиторий отвечает за миллисекунды, а проверяемое здесь — что происходит,
/// пока ответ в пути: ввод заблокирован, а пришедший не по тому плану ответ выброшен и не
/// подставился в форму. На настоящем хранилище это не проверить — там гонки нет, а
/// искусственно задержать запрос можно только рукописной обёрткой.
///
/// Задерживается ровно <see cref="GetPreviousByPlanAsync"/>: остальное идёт в настоящий
/// репозиторий как есть, и подменять больше нечего.
/// </remarks>
internal sealed class FakeSessionRepository(ITrainingSessionRepository inner) : ITrainingSessionRepository
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _calls;

    /// <summary>
    /// Задержать только это чтение по счёту, с единицы. <c>0</c> — задерживать все.
    /// </summary>
    /// <remarks>
    /// Именно выбор одного вызова из нескольких и делает проверку возможной: чтобы доказать,
    /// что индикатор держится на счётчике, нужен случай, когда один ответ уже пришёл, а
    /// второй ещё нет. Задержать оба одинаково нельзя — тогда порядок их завершения не
    /// задан, и проверить нечего.
    /// </remarks>
    public int DelayOnlyCall { get; init; }

    /// <summary>Сколько чтений началось, задержанных и обычных.</summary>
    public int Calls => _calls;

    /// <summary>Отпустить отложенные чтения.</summary>
    public void Release() => _gate.TrySetResult();

    public async Task<TrainingSession?> GetPreviousByPlanAsync(
        int planId,
        DateOnly before,
        CancellationToken cancellationToken = default)
    {
        var call = Interlocked.Increment(ref _calls);

        if (DelayOnlyCall == 0 || DelayOnlyCall == call)
        {
            await _gate.Task.ConfigureAwait(false);
        }

        return await inner.GetPreviousByPlanAsync(planId, before, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TrainingSession>> GetAsync(
        DateOnly? fromInclusive = null,
        DateOnly? toInclusive = null,
        CancellationToken cancellationToken = default) =>
        inner.GetAsync(fromInclusive, toInclusive, cancellationToken);

    public Task<TrainingSession?> GetByDateAsync(DateOnly targetDate, CancellationToken cancellationToken = default) =>
        inner.GetByDateAsync(targetDate, cancellationToken);

    public Task<AddTrainingSessionOutcome> AddAsync(
        TrainingSession session,
        CancellationToken cancellationToken = default) =>
        inner.AddAsync(session, cancellationToken);

    public Task<UpdateTrainingSessionOutcome> UpdateAsync(
        TrainingSession session,
        CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(session, cancellationToken);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        inner.DeleteAsync(id, cancellationToken);
}