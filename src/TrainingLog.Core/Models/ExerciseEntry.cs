using System.Collections.ObjectModel;

namespace TrainingLog.Core.Models;

/// <summary>Выполненное упражнение в рамках одной тренировки.</summary>
/// <remarks>
/// Упражнение хранится и ссылкой, и копией названия: удаление из справочника обнуляет ссылку,
/// но не стирает журнал — иначе одна опечатка удаляла бы историю тренировок каскадом.
/// </remarks>
public sealed class ExerciseEntry
{
    /// <summary>Идентификатор. <c>0</c> — запись ещё не сохранена.</summary>
    public int Id { get; set; }

    /// <summary>
    /// <c>null</c>, если упражнение удалено. Навигация нужна самому EF ради <c>SetNull</c>:
    /// без неё внешний ключ не стал бы отношением и остался бы висячим идентификатором.
    /// </summary>
    public Exercise? Exercise { get; set; }

    /// <summary>
    /// По нему окно правки дня узнаёт, какое упражнение плана восстанавливает: сверяться
    /// приходится по идентификатору, а не по названию — название в справочнике меняется.
    /// </summary>
    public int? ExerciseId { get; set; }

    /// <summary>Название на момент выполнения. Это то, что показывается в строке дня.</summary>
    public string ExerciseName { get; set; } = string.Empty;

    /// <summary>Место в порядке выполнения, с единицы.</summary>
    public int Order { get; set; }

    /// <summary>Их количество произвольно: повторения и вес в каждом подходе свои.</summary>
    public ICollection<TrainingSet> Sets { get; } = new ObservableCollection<TrainingSet>();

    public string? Notes { get; set; }

    public TrainingSet AddSet(int repetitions, decimal weight)
    {
        var set = new TrainingSet { Repetitions = repetitions, Weight = weight };

        Sets.Add(set);

        return set;
    }

    /// <summary>Проставляет номера подходам с единицы по порядку в коллекции.</summary>
    /// <remarks>
    /// Подходы показываются в порядке ввода, и столбцы подходов в строке дня идут именно в
    /// этом порядке. Порядок строк из базы для этого не годится (ловушка 17).
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