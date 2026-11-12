using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>Контракты каталога назначений: то, на чём строятся сервис и вкладка.</summary>
/// <remarks>
/// Каталог статический и «данные-как-код»: ошибка в нём — пропавшая строка во вкладке или
/// молча не срабатывающая клавиши. Проверки здесь держат каталог честным при правках.
/// </remarks>
public sealed class HotkeyCatalogTests
{
    /// <summary>Id уникальны глобально: ключ в файле один, и двусмыслица в адресах недопустима.</summary>
    [Fact]
    public void Id_ГлобальноУникальны()
    {
        var ids = HotkeyCatalog.Groups.SelectMany(group => group.Actions).Select(action => action.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>Имена — то, что пользователь читает во вкладке: пустых не бывает.</summary>
    [Fact]
    public void Имена_Непусты()
    {
        Assert.All(
            HotkeyCatalog.Groups.SelectMany(group => group.Actions),
            action => Assert.False(string.IsNullOrWhiteSpace(action.Name)));
    }

    /// <summary>В группе имена не повторяются: поиск «действия по имени» был бы двусмысленным.</summary>
    [Fact]
    public void ВГруппеИменаНеПовторяются()
    {
        Assert.All(
            HotkeyCatalog.Groups,
            group => Assert.Equal(
                group.Actions.Count,
                group.Actions.Select(action => action.Name).Distinct().Count()));
    }

    /// <summary>Умолчания — «Сохранить» окна дня, стрелки журнала и клавиши окна плана.</summary>
    /// <remarks>
    /// Второе умолчание памятью держится плохо: у пользователя нет вкладки «какие клавиши из
    /// коробки», кроме этой самой строки умолчания.
    /// </remarks>
    [Fact]
    public void Умолчания_СохранитьСтрелкиЖурналаИКлавишиПлана()
    {
        var defaults = HotkeyCatalog.Groups
            .SelectMany(group => group.Actions)
            .Where(action => action.DefaultGesture is not null)
            .ToDictionary(action => action.Id, action => action.DefaultGesture!);

        Assert.Equal(8, defaults.Count);
        Assert.Equal("Ctrl+S", defaults["Day.Save"]);
        Assert.Equal("Up", defaults["Main.SelectedUp"]);
        Assert.Equal("Down", defaults["Main.SelectedDown"]);
        Assert.Equal("Up", defaults["EditPlan.SelectedUp"]);
        Assert.Equal("Down", defaults["EditPlan.SelectedDown"]);
        Assert.Equal("Space", defaults["EditPlan.ToggleCheck"]);
        Assert.Equal("Left", defaults["EditPlan.BlockLeft"]);
        Assert.Equal("Right", defaults["EditPlan.BlockRight"]);
    }

    /// <summary>В окне две одинаковые комбинации спорят: умолчания внутри группы не повторяются.</summary>
    /// <remarks>
    /// Конфликт считается по окну: <c>KeyBinding</c> живёт в окне, и одна комбинация в двух
    /// окнах выполняет два разных действия, ни за что не споря. Внутри окна — спор, и вкладка
    /// «Горячие клавиши» такой ход отвергнет.
    /// </remarks>
    [Fact]
    public void Умолчания_ВГруппеНеПовторяются()
    {
        Assert.All(
            HotkeyCatalog.Groups,
            group =>
            {
                var defaults = group.Actions
                    .Where(action => action.DefaultGesture is not null)
                    .Select(action => action.DefaultGesture!)
                    .ToList();

                Assert.Equal(defaults.Count, defaults.Distinct().Count());
            });
    }

    /// <summary>Слотам каталогом обещаны все номера подряд от 1 до 12.</summary>
    [Fact]
    public void Слоты_НомераПодряд()
    {
        var slots = HotkeyCatalog.Groups
            .SelectMany(group => group.Actions)
            .Where(action => action.Kind == HotkeyKind.Slot)
            .Select(action => action.Id)
            .ToList();

        Assert.Equal(
            Enumerable.Range(1, HotkeyCatalog.SlotCount).Select(number => $"{HotkeyCatalog.SlotIdPrefix}{number}"),
            slots);
    }

    /// <summary>Разметка и окна читают действия только через <c>ActionOf</c>.</summary>
    /// <remarks>
    /// Неизвестный id обязан быть <c>null</c>, а не исключением: файл правят руками, и хвост от
    /// удалённой в каталоге строки не должен падать.
    /// </remarks>
    [Fact]
    public void ActionOf_НеизвестныйId_Null()
    {
        Assert.NotNull(HotkeyCatalog.ActionOf("Day.Save"));
        Assert.Null(HotkeyCatalog.ActionOf("Removed.Nobody"));
    }
}
