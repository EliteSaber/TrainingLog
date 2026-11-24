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
    private readonly object _sync = new();
    private readonly List<TaskCompletionSource> _gates = [];

    private bool _released;
    private int _calls;

    /// <summary>Отказ, которым закончится отложенное чтение. <c>null</c> — чтение пройдёт.</summary>
    public Exception? Failure { get; init; }

    /// <summary>Сколько чтений журнала началось.</summary>
    public int Calls
    {
        get
        {
            lock (_sync)
            {
                return _calls;
            }
        }
    }

    /// <summary>Отпустить все отложенные чтения, в том числе ещё не начавшиеся.</summary>
    public void Release()
    {
        lock (_sync)
        {
            _released = true;

            foreach (var gate in _gates)
            {
                gate.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Отпустить одно чтение по счёту, с единицы. Раздельные гейты нужны проверке пересечения:
    /// поздний запрос отвечает раньше раннего, и порядок завершения задаётся тестом, а не случаем.
    /// </summary>
    public void Release(int call)
    {
        lock (_sync)
        {
            if (call >= 1 && call <= _gates.Count)
            {
                _gates[call - 1].TrySetResult();
            }
        }
    }

    public async Task<IReadOnlyList<TrainingSession>> GetAsync(
        DateOnly? fromInclusive = null,
        DateOnly? toInclusive = null,
        int? count = null,
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource gate;

        lock (_sync)
        {
            _calls++;

            gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _gates.Add(gate);

            if (_released)
            {
                gate.TrySetResult();
            }
        }

        await gate.Task.ConfigureAwait(false);

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
