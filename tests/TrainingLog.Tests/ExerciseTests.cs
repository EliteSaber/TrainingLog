using System.ComponentModel;
using TrainingLog.Core.Models;

namespace TrainingLog.Tests;

/// <summary>
/// Упражнение подписывается на свои изменения, потому что строка списка переживает сохранение
/// правки и берёт значения из той же модели. Контракт из BCL, STA не нужен.
/// </summary>
public sealed class ExerciseTests
{
    [Fact]
    public void Name_НовоеЗначение_ПриходитУведомление()
    {
        var exercise = new Exercise();
        var notifications = new List<string?>();

        exercise.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        exercise.Name = "Жим";

        Assert.Equal([nameof(Exercise.Name)], notifications);
        Assert.Equal("Жим", exercise.Name);
    }

    /// <summary>При чтении упражнения из базы имя приходит равным, и лишнее событие дёргало бы разметку без причины.</summary>
    [Fact]
    public void Name_БезИзменения_УведомленияНет()
    {
        var exercise = new Exercise { Name = "Жим" };
        var notifications = new List<string?>();

        exercise.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        exercise.Name = "Жим";

        Assert.Empty(notifications);
    }
}
