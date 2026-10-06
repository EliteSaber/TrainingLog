using System.Collections.ObjectModel;

namespace TrainingLog.Core.Models;

/// <summary>
/// Выполненное упражнение в рамках одной тренировки.
/// </summary>
/// <remarks>
/// Упражнение хранится и ссылкой, и копией названия — как план в <see cref="TrainingSession"/>.
/// Удаление упражнения из справочника обнуляет ссылку, но не стирает журнал: иначе одна
/// опечатка в справочнике удаляла бы историю тренировок каскадом.
/// </remarks>
public sealed class ExerciseEntry
{
    /// <summary>
    /// Идентификатор записи. Значение <c>0</c> означает, что запись ещё не сохранена
    /// и идентификатор будет присвоен хранилищем.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Упражнение из справочника. <c>null</c>, если упражнение удалено, а запись осталась.
    /// </summary>
    /// <remarks>
    /// Навигация нужна самому EF ради <c>SetNull</c>: без неё внешний ключ не стал бы
    /// отношением, и удаление упражнения оставило бы висячий идентификатор.
    /// </remarks>
    public Exercise? Exercise { get; set; }

    /// <summary>
    /// Идентификатор упражнения из справочника. <c>null</c>, если упражнение удалено.
    /// </summary>
    /// <remarks>
    /// По нему окно правки дня узнаёт, какое упражнение плана восстанавливает, — сверять
    /// приходится по идентификатору, а не по названию: название в справочнике меняется.
    /// </remarks>
    public int? ExerciseId { get; set; }

    /// <summary>
    /// Название упражнения на момент выполнения. Это то, что показывается в строке дня.
    /// </summary>
    public string ExerciseName { get; set; } = string.Empty;

    /// <summary>
    /// Место упражнения в порядке выполнения, с единицы.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Подходы упражнения. Их количество произвольно: повторения и вес в каждом подходе свои.
    /// </summary>
    /// <remarks>
    /// Коллекция изменяемая по той же причине, что и <see cref="TrainingSession.Exercises"/>:
    /// правка дня переписывает подходы целиком.
    /// </remarks>
    public ICollection<TrainingSet> Sets { get; } = new ObservableCollection<TrainingSet>();

    /// <summary>
    /// Заметка к упражнению.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Добавляет подход с указанными повторениями и весом.
    /// </summary>
    /// <param name="repetitions">Количество повторений.</param>
    /// <param name="weight">Вес подхода в килограммах.</param>
    public TrainingSet AddSet(int repetitions, decimal weight)
    {
        var set = new TrainingSet { Repetitions = repetitions, Weight = weight };

        Sets.Add(set);

        return set;
    }

    /// <summary>
    /// Проставляет номера подходам с единицы по порядку в коллекции.
    /// </summary>
    /// <remarks>
    /// Подходы показываются в порядке ввода, а в строке дня столбцы подходов идут слева
    /// направо именно в этом порядке. Порядок строк из базы для этого не годится.
    /// </remarks>
    public void NormalizeOrder()
    {
        var order = 1;

        foreach (var set in Sets)
        {
            set.Order = order++;
        }
    }
}