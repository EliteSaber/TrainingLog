using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.Tests;

/// <summary>
/// Журнал с задержкой на основном чтении: настоящая база отвечает за миллисекунды, а проверяется
/// поведение окна, пока ответ в пути.
/// </summary>
/// <remarks>
/// Задерживается ровно <see cref="GetAsync"/>: остальные методы идут в настоящий репозиторий как
/// есть. Вращение кольца и активность «Применить» зависят именно от этого чтения.
/// </remarks>
internal sealed class GatedSessionRepository(ITrainingSessionRepository inner) : ITrainingSessionRepository
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Отказ, которым закончится отложенное чтение. <c>null</c> — чтение пройдёт.</summary>
    public Exception? Failure { get; init; }

    /// <summary>Отпустить отложенное чтение.</summary>
    public void Release() => _gate.TrySetResult();

    public async Task<IReadOnlyList<TrainingSession>> GetAsync(
        DateOnly? fromInclusive = null,
        DateOnly? toInclusive = null,
        int? count = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.Task.ConfigureAwait(false);

        if (Failure is { } failure)
        {
            throw failure;
        }

        return await inner.GetAsync(fromInclusive, toInclusive, count, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TrainingSession>> GetLatestAsync(
        int count,
        CancellationToken cancellationToken = default) =>
        inner.GetLatestAsync(count, cancellationToken);

    public Task<TrainingSession?> GetByDateAsync(DateOnly targetDate, CancellationToken cancellationToken = default) =>
        inner.GetByDateAsync(targetDate, cancellationToken);

    public Task<TrainingSession?> GetPreviousByPlanAsync(
        int planId,
        DateOnly before,
        CancellationToken cancellationToken = default) =>
        inner.GetPreviousByPlanAsync(planId, before, cancellationToken);

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
