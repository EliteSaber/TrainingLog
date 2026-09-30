using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Реализация <see cref="IExerciseRepository"/> поверх SQLite.
/// </summary>
public sealed class ExerciseRepository(IDbContextFactory<TrainingLogDbContext> contextFactory) : IExerciseRepository
{
    /// <summary>
    /// Приводит название к сравнимому виду для хранения в <c>NameKey</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="string.ToUpperInvariant"/>, а не <see cref="string.ToUpper()"/>: вариант без
    /// параметра зависит от локали машины, и одна база сравнивалась бы по-разному на разных
    /// компьютерах. Сравнение строк в SQLite складывает регистр только для ASCII, поэтому
    /// нормализация выполняется в .NET.
    /// </remarks>
    internal static string NormalizeName(string name) => name.ToUpperInvariant();

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
        context.Entry(exercise).Property(TrainingLogDbContext.NameKeyPropertyName).CurrentValue = NormalizeName(name);

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
        context.Entry(stored).Property(TrainingLogDbContext.NameKeyPropertyName).CurrentValue = NormalizeName(name);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return UpdateExerciseOutcome.DuplicateName;
        }

        exercise.Name = name;
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

        return true;
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var key = NormalizeName(name.Trim());

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.Exercises
            .AsNoTracking()
            .AnyAsync(
                e => e.Id != excludeId
                     && EF.Property<string>(e, TrainingLogDbContext.NameKeyPropertyName) == key,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
