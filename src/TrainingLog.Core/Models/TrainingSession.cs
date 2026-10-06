using System.Collections.ObjectModel;

namespace TrainingLog.Core.Models;

/// <summary>
/// Запись журнала тренировок за один день.
/// </summary>
/// <remarks>
/// План хранится и ссылкой, и копией названия. Ссылка нужна, чтобы по записи понимать, какой
/// план выполнялся, а копия — чтобы переименование или удаление плана не стирали историю:
/// журнал тренировок переживает справочники, к которым он относится. Поэтому ссылка
/// необязательная, и удаление плана обнуляет её, а не удаляет записи каскадом.
/// </remarks>
public sealed class TrainingSession
{
    /// <summary>
    /// Идентификатор записи. Значение <c>0</c> означает, что запись ещё не сохранена
    /// и идентификатор будет присвоен хранилищем.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Дата тренировки без времени. На дату приходится ровно одна запись: это проверяет
    /// уникальный индекс, а не код приложения.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// План тренировки. <c>null</c>, если план удалён из справочника, а название осталось
    /// в <see cref="PlanName"/>.
    /// </summary>
    /// <remarks>
    /// Навигация нужна самому EF: без неё внешний ключ не стал бы отношением, и удаление
    /// плана оставило бы в журнале висячий идентификатор. Показывать план по навигации не
    /// приходится — в строку дня выводится <see cref="PlanName"/>, — но сопоставить запись с
    /// планом окно правки обязано, поэтому идентификатор объявлен свойством, а не тенью.
    /// </remarks>
    public TrainingPlan? Plan { get; set; }

    /// <summary>
    /// Идентификатор плана, который выполнялся. <c>null</c>, если план удалён из справочника.
    /// </summary>
    public int? PlanId { get; set; }

    /// <summary>
    /// Название плана на момент выполнения. Это то, что показывается в строке дня.
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Выполненные упражнения в порядке выполнения.
    /// </summary>
    /// <remarks>
    /// Коллекция изменяемая, в отличие от вычисляемого <see cref="TrainingPlan.Exercises"/>:
    /// правка дня переписывает запись целиком, и очистить read-only коллекцию нечем. Объявленный
    /// тип остаётся <see cref="ICollection{T}"/>, наблюдаемым сделан только экземпляр, —
    /// ровно как у строк связи плана с упражнениями. Зависимости от WPF у ядра не появляется:
    /// <see cref="ObservableCollection{T}"/> из BCL.
    /// </remarks>
    public ICollection<ExerciseEntry> Exercises { get; } = new ObservableCollection<ExerciseEntry>();

    /// <summary>
    /// Заметка к тренировке.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Добавляет выполненное упражнение к тренировке.
    /// </summary>
    /// <param name="entry">Запись о выполненном упражнении.</param>
    /// <returns>Добавленная запись.</returns>
    public ExerciseEntry AddExercise(ExerciseEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Exercises.Add(entry);
        return entry;
    }

    /// <summary>
    /// Добавляет упражнение из справочника и возвращает добавленную запись.
    /// </summary>
    /// <param name="exercise">Упражнение из справочника.</param>
    /// <param name="notes">Заметка к упражнению.</param>
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

    /// <summary>
    /// Добавляет упражнение по сохранённому названию — на случай, когда упражнение удалено
    /// из справочника, а запись в журнале осталась.
    /// </summary>
    /// <param name="exerciseName">Название упражнения.</param>
    /// <param name="notes">Заметка к упражнению.</param>
    public ExerciseEntry AddExercise(string exerciseName, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exerciseName);

        return AddExercise(new ExerciseEntry { ExerciseName = exerciseName, Notes = notes });
    }

    /// <summary>
    /// Проставляет номера порядка выполнения с единицы по порядку в коллекции.
    /// </summary>
    /// <remarks>
    /// Номера задаёт вызывающий, а хранилище их перенумеровывает при записи — ровно как у
    /// плана. Показывать день без номеров нельзя: столбцы упражнений в строке идут именно
    /// в порядке выполнения, а полагаться на порядок строк из базы нельзя: то же ограничение,
    /// из-за которого у плана появилась своя таблица связи.
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