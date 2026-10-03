using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Реализация <see cref="IExerciseRepository"/> поверх SQLite.
/// </summary>
public sealed class ExerciseRepository(IDbContextFactory<TrainingLogDbContext> contextFactory) : IExerciseRepository
{
    /// <inheritdoc />
    public event EventHandler? Changed;

    public async Task<IReadOnlyList<Exercise>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.Exercises
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AddExerciseOutcome> AddAsync(Exercise exercise, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var name = exercise.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return AddExerciseOutcome.NameIsEmpty;
        }

        if (await ExistsByNameAsync(name, excludeId: null, cancellationToken).ConfigureAwait(false))
        {
            return AddExerciseOutcome.DuplicateName;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        exercise.Name = name;
        context.Exercises.Add(exercise);
        context.Entry(exercise).Property(TrainingLogDbContext.NameKeyPropertyName).CurrentValue = TrainingLogDbContext.NormalizeNameKey(name);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Предварительная проверка могла разойтись с базой: та же проверка из другого окна
            // или процесса успела вставить строку. Уникальный индекс — последний рубеж.
            return AddExerciseOutcome.DuplicateName;
        }

        NotifyChanged();

        return AddExerciseOutcome.Added;
    }

    public async Task<UpdateExerciseOutcome> UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var name = exercise.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return UpdateExerciseOutcome.NameIsEmpty;
        }

        if (await ExistsByNameAsync(name, exercise.Id, cancellationToken).ConfigureAwait(false))
        {
            return UpdateExerciseOutcome.DuplicateName;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var stored = await context.Exercises
            .FirstOrDefaultAsync(e => e.Id == exercise.Id, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return UpdateExerciseOutcome.NotFound;
        }

        stored.Name = name;
        context.Entry(stored).Property(TrainingLogDbContext.NameKeyPropertyName).CurrentValue = TrainingLogDbContext.NormalizeNameKey(name);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return UpdateExerciseOutcome.DuplicateName;
        }

        exercise.Name = name;

        NotifyChanged();

        return UpdateExerciseOutcome.Updated;
    }
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var stored = await context.Exercises
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return false;
        }

        context.Exercises.Remove(stored);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        NotifyChanged();

        return true;
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var key = TrainingLogDbContext.NormalizeNameKey(name.Trim());

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.Exercises
            .AsNoTracking()
            .AnyAsync(
                e => e.Id != excludeId
                     && EF.Property<string>(e, TrainingLogDbContext.NameKeyPropertyName) == key,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Сообщает подписчикам, что справочник изменился. Вызывается только после успешной записи.
    /// </summary>
    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
