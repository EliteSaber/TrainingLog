using System.Collections.Specialized;
using TrainingLog.Core.Models;

namespace TrainingLog.Tests;

/// <summary>
/// План подписывается на свои изменения, потому что строка списка планов переживает
/// перечитывание из базы и берёт значения из той же модели. Без уведомлений сохранённая
/// правка осталась бы в базе и не появилась бы на экране. Здесь нет ни окна, ни STA: оба
/// контракта — из BCL.
/// </summary>
public sealed class TrainingPlanTests
{
    [Fact]
    public void Name_НовоеЗначение_ПриходитУведомление()
    {
        var plan = new TrainingPlan();
        var notifications = TrackNameChanges(plan);

        plan.Name = "Ноги";

        Assert.Equal([nameof(TrainingPlan.Name)], notifications);
        Assert.Equal("Ноги", plan.Name);
    }

    /// <summary>
    /// Повторное присваивание того же значения уведомления не шлёт: при обновлении плана из
    /// базы имя приходит равным, и лишнее событие дёргало бы разметку без причины.
    /// </summary>
    [Fact]
    public void Name_БезИзменения_УведомленияНет()
    {
        var plan = new TrainingPlan { Name = "Ноги" };
        var notifications = TrackNameChanges(plan);

        plan.Name = "Ноги";

        Assert.Empty(notifications);
    }

    [Fact]
    public void AddExercise_Добавление_ПриходитУведомлениеКоллекции()
    {
        var plan = new TrainingPlan();
        var actions = TrackCollectionChanges(plan);

        plan.AddExercise(new Exercise { Name = "Приседание" });

        Assert.Equal([NotifyCollectionChangedAction.Add], actions);
        Assert.Single(plan.PlanExercises);
    }

    [Fact]
    public void PlanExercises_Очистка_ПриходитСброс()
    {
        var plan = new TrainingPlan();
        plan.AddExercise(new Exercise { Name = "Приседание" });

        var actions = TrackCollectionChanges(plan);

        plan.PlanExercises.Clear();

        // Именно сброс, а не удаление по одной: список упражнений обновляется целиком.
        Assert.Equal([NotifyCollectionChangedAction.Reset], actions);
        Assert.Empty(plan.PlanExercises);
    }

    [Fact]
    public void AddExercise_НомераИдутСЕдиницыПодряд()
    {
        var plan = new TrainingPlan();

        plan.AddExercise(new Exercise { Name = "Приседание" });
        plan.AddExercise(new Exercise { Name = "Жим" });

        Assert.Equal([1, 2], plan.PlanExercises.Select(link => link.Order));
        Assert.Equal(
            ["Приседание", "Жим"],
            plan.Exercises.Select(exercise => exercise.Name));
    }

    private static List<string?> TrackNameChanges(TrainingPlan plan)
    {
        var notifications = new List<string?>();

        plan.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        return notifications;
    }

    private static List<NotifyCollectionChangedAction> TrackCollectionChanges(TrainingPlan plan)
    {
        var actions = new List<NotifyCollectionChangedAction>();

        ((INotifyCollectionChanged)plan.PlanExercises).CollectionChanged += (_, args) => actions.Add(args.Action);

        return actions;
    }
}
