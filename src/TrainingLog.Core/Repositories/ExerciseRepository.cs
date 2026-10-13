using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;

namespace TrainingLog.Core.Repositories;

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
            // Предварительная проверка могла разойтись с базой: ту же проверку из другого окна
            // или процесса успели выполнить раньше. Уникальный индекс — последний рубеж.
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

    public async Task<ImportExercisesOutcome> ImportAsync(
        IReadOnlyList<string> names,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(names);

        // То же правило, что в окне импорта: если что-то отброшено, запись не начинается. Иначе
        // файл с одной плохой строкой записался бы частично, и пользователь об этом не узнал бы.
        var accepted = ExerciseImportRules.Read(names);

        if (accepted.Count == 0)
        {
            return ImportExercisesOutcome.NothingToImport;
        }

        if (accepted.Count != names.Count)
        {
            return ImportExercisesOutcome.InvalidName;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var transaction = await context.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            if (replaceExisting)
            {
                context.Exercises.RemoveRange(
                    await context.Exercises.ToListAsync(cancellationToken).ConfigureAwait(false));

                // Отдельным сохранением: порядок команд внутри одного SaveChanges такой, что вставка
                // идёт раньше удаления, а упражнение с тем же названием из файла упёрлось бы в
                // уникальный индекс прежнего. Транзакция держит обе части вместе — отказ на
                // вставке не оставил бы справочник пустым.
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var name in accepted)
            {
                var exercise = new Exercise { Name = name };
                context.Exercises.Add(exercise);
                context.Entry(exercise).Property(TrainingLogDbContext.NameKeyPropertyName).CurrentValue =
                    TrainingLogDbContext.NormalizeNameKey(name);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Тот же уникальный индекс, что у добавления: предварительная проверка разошлась с
            // базой — файл открыт в двух окнах или упражнение добавили, пока окно импорта ждало.
            return ImportExercisesOutcome.DuplicateName;
        }

        NotifyChanged();

        return ImportExercisesOutcome.Imported;
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

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
