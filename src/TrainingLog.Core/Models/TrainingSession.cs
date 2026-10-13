using System.Collections.ObjectModel;

namespace TrainingLog.Core.Models;

/// <summary>Запись журнала тренировок за один день.</summary>
/// <remarks>
/// План хранится и ссылкой, и копией названия: ссылка нужна, чтобы знать, какой план
/// выполнялся, а копия — чтобы переименование или удаление плана не стирали историю. Поэтому
/// ссылка необязательная, и удаление плана обнуляет её, а не удаляет записи каскадом.
/// </remarks>
public sealed class TrainingSession
{
    /// <summary>Идентификатор. <c>0</c> — запись ещё не сохранена.</summary>
    public int Id { get; set; }

    /// <summary>Дата тренировки без времени. На дату приходится ровно одна запись.</summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// <c>null</c>, если план удалён из справочника, а название осталось в <see cref="PlanName"/>.
    /// Навигация нужна самому EF — без неё внешний ключ не стал бы отношением и удаление плана
    /// оставило бы висячий идентификатор. Показывать план по навигации не приходится, но
    /// сопоставить запись с планом окно правки обязано, поэтому идентификатор объявлен
    /// свойством, а не тенью.
    /// </summary>
    public TrainingPlan? Plan { get; set; }

    public int? PlanId { get; set; }

    /// <summary>Название плана на момент выполнения. Это то, что показывается в строке дня.</summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Коллекция изменяемая, в отличие от вычисляемого <see cref="TrainingPlan.Exercises"/>:
    /// правка дня переписывает запись целиком, и очистить read-only коллекцию нечем.
    /// </summary>
    public ICollection<ExerciseEntry> Exercises { get; } = new ObservableCollection<ExerciseEntry>();

    public string? Notes { get; set; }

    public ExerciseEntry AddExercise(ExerciseEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Exercises.Add(entry);
        return entry;
    }

    public ExerciseEntry AddExercise(Exercise exercise, string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return AddExercise(new ExerciseEntry
        {
            Exercise = exercise,

            // У несохранённого упражнения идентификатора ещё нет, и нулевой внешний ключ
            // в базе нарушил бы ограничение: без ссылки правильно null.
            ExerciseId = exercise.Id > 0 ? exercise.Id : null,
            ExerciseName = exercise.Name,
            Notes = notes,
        });
    }

    /// <summary>На случай, когда упражнение удалено из справочника, а запись осталась.</summary>
    public ExerciseEntry AddExercise(string exerciseName, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exerciseName);

        return AddExercise(new ExerciseEntry { ExerciseName = exerciseName, Notes = notes });
    }

    /// <summary>Проставляет номера порядка выполнения с единицы по порядку в коллекции.</summary>
    /// <remarks>
    /// Номера задаёт вызывающий, а хранилище их перенумеровывает при записи — ровно как у
    /// плана. Показывать день без номеров нельзя: столбцы упражнений в строке идут именно
    /// в порядке выполнения, а полагаться на порядок строк из базы нельзя.
    /// </remarks>
    public void NormalizeOrder()
    {
        var order = 1;

        foreach (var entry in Exercises)
        {
            entry.Order = order++;
        }
    }
}