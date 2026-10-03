using System.Collections.ObjectModel;
using System.Collections.Specialized;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Приведение видимой коллекции к целевому порядку. Проверяется чистая функция без окна:
/// <see cref="ObservableCollection{T}"/> — не элемент WPF, STA не нужен.
/// </summary>
/// <remarks>
/// Проверяется не только результат, но и набор действий коллекции: именно он отличает
/// «строку переставили» от «список пересоздали». Второе убивает состояние контейнера,
/// а у строки плана это <c>Expander.IsExpanded</c>.
/// </remarks>
public sealed class PlansViewModelTests
{
    [Fact]
    public void SyncVisible_ПорядокНеИзменился_КоллекцияНеТрогается()
    {
        var visible = new ObservableCollection<string> { "а", "б", "в" };
        var actions = Record(visible);

        PlansViewModel.SyncVisible(visible, ["а", "б", "в"]);

        Assert.Equal(["а", "б", "в"], visible);
        Assert.Empty(actions);
    }

    [Fact]
    public void SyncVisible_Перестановка_ТолькоПеремещение()
    {
        var visible = new ObservableCollection<string> { "а", "б", "в" };
        var actions = Record(visible);

        PlansViewModel.SyncVisible(visible, ["в", "а", "б"]);

        Assert.Equal(["в", "а", "б"], visible);
        Assert.Equal([NotifyCollectionChangedAction.Move], actions);
    }

    [Fact]
    public void SyncVisible_РазворотСписка_ТолькоПеремещение()
    {
        var visible = new ObservableCollection<string> { "а", "б", "в" };
        var actions = Record(visible);

        PlansViewModel.SyncVisible(visible, ["в", "б", "а"]);

        Assert.Equal(["в", "б", "а"], visible);
        Assert.Equal(
            [
                NotifyCollectionChangedAction.Move,
                NotifyCollectionChangedAction.Move,
            ],
            actions);
    }

    [Fact]
    public void SyncVisible_ЛишнийЭлемент_УдаляетсяИОстальныеВстаютНаМеста()
    {
        var visible = new ObservableCollection<string> { "а", "б", "в" };
        var actions = Record(visible);

        PlansViewModel.SyncVisible(visible, ["в", "а"]);

        Assert.Equal(["в", "а"], visible);
        Assert.Equal(
            [
                NotifyCollectionChangedAction.Remove,
                NotifyCollectionChangedAction.Move,
            ],
            actions);
    }

    [Fact]
    public void SyncVisible_НовыйЭлемент_ДобавляетсяИОстальныеВстаютНаМеста()
    {
        var visible = new ObservableCollection<string> { "а", "в" };
        var actions = Record(visible);

        PlansViewModel.SyncVisible(visible, ["в", "а", "б"]);

        Assert.Equal(["в", "а", "б"], visible);
        Assert.Equal(
            [
                NotifyCollectionChangedAction.Move,
                NotifyCollectionChangedAction.Add,
            ],
            actions);
    }

    [Fact]
    public void SyncVisible_ПустойЦелевойСписок_КоллекцияОчищается()
    {
        var visible = new ObservableCollection<string> { "а", "б" };
        var actions = Record(visible);

        PlansViewModel.SyncVisible(visible, []);

        Assert.Empty(visible);
        Assert.Equal(
            [
                NotifyCollectionChangedAction.Remove,
                NotifyCollectionChangedAction.Remove,
            ],
            actions);
    }

    [Fact]
    public void SyncVisible_ПустаяКоллекция_ВсёДобавляется()
    {
        var visible = new ObservableCollection<string>();

        PlansViewModel.SyncVisible(visible, ["а", "б"]);

        Assert.Equal(["а", "б"], visible);
    }

    /// <summary>
    /// Сравнение идёт по самому элементу, а не по наименованию: два разных плана в базе
    /// вправе называться одинаково после правки, и переставлять их местами нельзя.
    /// </summary>
    [Fact]
    public void SyncVisible_ОдинаковыеНаименованияРазныхПланов_НеСхлопываются()
    {
        var first = new Core.Models.TrainingPlan { Id = 1, Name = "День" };
        var second = new Core.Models.TrainingPlan { Id = 2, Name = "День" };

        var visible = new ObservableCollection<Core.Models.TrainingPlan> { first, second };

        PlansViewModel.SyncVisible(visible, [second, first]);

        Assert.Equal([second, first], visible);
    }

    private static List<NotifyCollectionChangedAction> Record(ObservableCollection<string> collection)
    {
        var actions = new List<NotifyCollectionChangedAction>();

        collection.CollectionChanged += (_, args) => actions.Add(args.Action);

        return actions;
    }
}
