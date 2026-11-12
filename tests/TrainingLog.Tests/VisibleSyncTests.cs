using System.Collections.ObjectModel;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Приведение видимой коллекции к целевому порядку на больших списках.
/// </summary>
/// <remarks>
/// Проверяется через публичный <see cref="PlansViewModel.SyncVisible{T}"/>: общей части с
/// справочником упражнений достаётся один и тот же порядок действий коллекции, а проверять
/// внутренности через отражение — худший вид теста.
/// </remarks>
public sealed class VisibleSyncTests
{
    /// <summary>
    /// Поиск места элемента линеен: сравнений на элемент — единицы, а не десятки.
    /// </summary>
    /// <remarks>
    /// Сравнения считает сам элемент, а не таймер: замер времени зависит от машины, а число
    /// сравнений — от алгоритма. Квадратичный поиск на тысяче элементов дал бы около миллиона
    /// сравнений, линейный — единицы тысяч.
    /// </remarks>
    [Fact]
    public void Sync_БольшойСписок_СравненийЛинейноеЧисло()
    {
        var counter = new Counter();

        var items = Enumerable
            .Range(0, 1000)
            .Select(index => new Counted($"элемент {index}", counter))
            .ToList();

        var visible = new ObservableCollection<Counted>(items);
        var target = items.AsEnumerable().Reverse().ToList();

        PlansViewModel.SyncVisible(visible, target);

        var comparisons = counter.Value;

        Assert.Equal(target, visible);
        Assert.True(
            comparisons < items.Count * 20,
            $"сравнений {comparisons} на {items.Count} элементах — поиск не линейный");
    }

    /// <summary>
    /// Одинаковые значения не ломают раскладку: список приходит к цели, а не к её обрывку.
    /// </summary>
    /// <remarks>
    /// Раньше повтор в цели искался уже поставленным элементом, и порядок в конце оказывался не
    /// тем: на ["б", "а"] цель ["а", "а", "б"] давала ["б", "а", "б"]. Для планов и упражнений это
    /// не встречается — совпадение считается по самому элементу, а два разных плана в базе вправе
    /// называться одинаково, — но повреждённый порядок лучше не оставлять вовсе.
    /// </remarks>
    [Fact]
    public void Sync_ОдинаковыеЗначения_СписокПриходитКЦели()
    {
        var visible = new ObservableCollection<string> { "б", "а" };

        PlansViewModel.SyncVisible(visible, ["а", "а", "б"]);

        Assert.Equal(["а", "а", "б"], visible);
    }

    /// <summary>Счётчик сравнений: им измеряется линейность поиска.</summary>
    private sealed class Counter
    {
        public int Value { get; set; }
    }

    /// <summary>Элемент, считающий сравнения с другими элементами.</summary>
    private sealed class Counted : IEquatable<Counted>
    {
        private readonly Counter _counter;

        public Counted(string name, Counter counter)
        {
            Name = name;
            _counter = counter;
        }

        public string Name { get; }

        public bool Equals(Counted? other)
        {
            _counter.Value++;

            return other is not null && Name == other.Name;
        }

        public override bool Equals(object? obj) => Equals(obj as Counted);

        public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    }
}
