using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Порядок полей подходов по Tab и Shift+Tab. Проверяется чистая функция: элементы WPF
/// в тестах не создаются, для них нужна STA-нитка, а в проекте такой обвязки нет.
/// </summary>
public sealed class SetFieldNavigationTests
{
    [Fact]
    public void PositionOf_ВесИПовторения_ПозицииБезПерекрытия()
    {
        // Три подхода: веса занимают 0..2, повторения 3..5.
        Assert.Equal(0, SetFieldNavigation.PositionOf(SetField.Weight, setIndex: 0, count: 3));
        Assert.Equal(1, SetFieldNavigation.PositionOf(SetField.Weight, setIndex: 1, count: 3));
        Assert.Equal(2, SetFieldNavigation.PositionOf(SetField.Weight, setIndex: 2, count: 3));
        Assert.Equal(3, SetFieldNavigation.PositionOf(SetField.Repetitions, setIndex: 0, count: 3));
        Assert.Equal(4, SetFieldNavigation.PositionOf(SetField.Repetitions, setIndex: 1, count: 3));
        Assert.Equal(5, SetFieldNavigation.PositionOf(SetField.Repetitions, setIndex: 2, count: 3));
    }

    [Fact]
    public void FieldAt_ПерваяПоловина_Веса_Вторая_Повторения()
    {
        Assert.Equal((SetField.Weight, 0), SetFieldNavigation.FieldAt(position: 0, count: 3));
        Assert.Equal((SetField.Weight, 2), SetFieldNavigation.FieldAt(position: 2, count: 3));
        Assert.Equal((SetField.Repetitions, 0), SetFieldNavigation.FieldAt(position: 3, count: 3));
        Assert.Equal((SetField.Repetitions, 2), SetFieldNavigation.FieldAt(position: 5, count: 3));
    }

    [Fact]
    public void TargetIndex_ВперёдИзСередины_СледующаяПозиция()
    {
        Assert.Equal(2, SetFieldNavigation.TargetIndex(position: 1, length: 6, forward: true));
    }

    [Fact]
    public void TargetIndex_НазадИзСередины_ПредыдущаяПозиция()
    {
        Assert.Equal(0, SetFieldNavigation.TargetIndex(position: 1, length: 6, forward: false));
    }

    [Fact]
    public void TargetIndex_ВперёдСКонца_НиКуда()
    {
        Assert.Null(SetFieldNavigation.TargetIndex(position: 5, length: 6, forward: true));
    }

    [Fact]
    public void TargetIndex_НазадСНачала_НиКуда()
    {
        Assert.Null(SetFieldNavigation.TargetIndex(position: 0, length: 6, forward: false));
    }

    [Fact]
    public void ПолныйПроход_Tab_ВсеВеса_ЗатемВсеПовторения()
    {
        // Веса подряд, потом повторения в том же порядке подходов.
        Assert.Equal(PolnyiPutiVpered, Проход(0, length: 6, forward: true));
    }

    [Fact]
    public void ПолныйПроход_ShiftTab_ПовторенияЗатемВеса()
    {
        Assert.Equal(PolnyiPutiNazad, Проход(5, length: 6, forward: false));
    }

    [Fact]
    public void Кольцо_ПоследнийВес_КПервымПовторениям_ИОбратно()
    {
        // Tab с веса последнего подхода попадает в повторения первого, Shift+Tab
        // с повторений первого возвращается к последнему весу: замыкание прощупывается
        // на границе половин цепочки.
        var lastWeight = SetFieldNavigation.PositionOf(SetField.Weight, setIndex: 2, count: 3);

        Assert.Equal((SetField.Repetitions, 0),
            SetFieldNavigation.FieldAt(SetFieldNavigation.TargetIndex(lastWeight, length: 6, forward: true)!.Value, 3));

        var firstReps = SetFieldNavigation.PositionOf(SetField.Repetitions, setIndex: 0, count: 3);

        Assert.Equal((SetField.Weight, 2),
            SetFieldNavigation.FieldAt(SetFieldNavigation.TargetIndex(firstReps, length: 6, forward: false)!.Value, 3));
    }

    [Fact]
    public void ЕдинственныйПодход_ВесТогоЖеПодхода_ЗатемВыход()
    {
        // Один подход: цепочка «вес → повторения», дальше цели нет.
        var pos = SetFieldNavigation.PositionOf(SetField.Weight, setIndex: 0, count: 1);
        var target = SetFieldNavigation.TargetIndex(pos, length: 2, forward: true)!.Value;
        var (part, setIndex) = SetFieldNavigation.FieldAt(target, 1);

        Assert.Equal((SetField.Repetitions, 0), (part, setIndex));
        Assert.Null(SetFieldNavigation.TargetIndex(target, length: 2, forward: true));
    }

    /// <summary>Порядок позиций одного прохода Tab по всей цепочке.</summary>
    private static readonly int[] PolnyiPutiVpered = { 1, 2, 3, 4, 5 };

    /// <summary>Порядок позиций одного прохода Shift+Tab по всей цепочке.</summary>
    private static readonly int[] PolnyiPutiNazad = { 4, 3, 2, 1, 0 };

    /// <summary>Индекс позиций по Tab от заданной точки, пока цепочка не кончится.</summary>
    private static int[] Проход(int start, int length, bool forward)
    {
        var path = new System.Collections.Generic.List<int>();
        var position = start;

        while (SetFieldNavigation.TargetIndex(position, length, forward) is { } next)
        {
            path.Add(next);
            position = next;
        }

        return path.ToArray();
    }
}
