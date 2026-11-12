using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>Подписка выделения строк: тип делегата обязан совпадать с типом события.</summary>
[Collection(WpfTestGroup.Name)]
public sealed class RowSelectionTests
{
    /// <summary>Применение и снятие Enabled не бросает: делегат подписки типизирован событием.</summary>
    /// <remarks>
    /// Регрессия ловушки №24: метод-группа в параметре AddHandler'а типа Delegate компилятор
    /// оборачивал в natural-тип вместо MouseButtonEventHandler, и WPF рантаймом бросал
    /// «Несоответствие типа обработчика» при каждом применении свойства.
    /// </remarks>
    [Fact]
    public void Enabled_ПрименениеИСнятие_НеБросает()
    {
        StaRunner.Run(() =>
        {
            var list = new ListBox();

            RowSelection.SetEnabled(list, true);
            RowSelection.SetEnabled(list, false);
        });
    }

    /// <summary>Строит список с включённым выделением и сгенерированными контейнерами.</summary>
    /// <remarks>
    /// Дерево без PresentationSource никакой разметки не измеряет сам: тема на отрыве от окна
    /// не находит шаблон по умолчанию, панель не создаётся, и генератор остаётся NotStarted.
    /// Поэтому шаблон с <c>ItemsPresenter</c> задаётся явно, а дерево меряется руками — контейнеры
    /// генерирует та же связка Panel–ItemsPresenter, что и в настоящем окне.
    /// </remarks>
    private static ItemsControl ListWith(params string[] items)
    {
        var list = new ItemsControl
        {
            Template = (ControlTemplate)XamlReader.Parse(
                "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"" +
                "                xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
                "  <Border><ItemsPresenter /></Border>" +
                "</ControlTemplate>"),
        };

        RowSelection.SetEnabled(list, true);

        foreach (var item in items)
        {
            list.Items.Add(item);
        }

        list.ApplyTemplate();
        list.Measure(new Size(400, double.PositiveInfinity));
        list.Arrange(new Rect(0, 0, 400, 300));

        return list;
    }

    /// <summary>Стрелка вниз без выделения выделяет первую строку списка.</summary>
    [Fact]
    public void Move_БезВыделенияВниз_ВыделяетПервую()
    {
        StaRunner.Run(() =>
        {
            var list = ListWith("а", "б", "в");

            RowSelection.Move(list, 1);

            Assert.Equal("а", RowSelection.Item(list));
        });
    }

    /// <summary>Стрелка вверх без выделения выделяет последнюю строку списка.</summary>
    [Fact]
    public void Move_БезВыделенияВверх_ВыделяетПоследнюю()
    {
        StaRunner.Run(() =>
        {
            var list = ListWith("а", "б", "в");

            RowSelection.Move(list, -1);

            Assert.Equal("в", RowSelection.Item(list));
        });
    }

    /// <summary>Повторные стрелки двигают выделение построчно.</summary>
    [Fact]
    public void Move_НесколькоШагов_СдвигаетсяПострочно()
    {
        StaRunner.Run(() =>
        {
            var list = ListWith("а", "б", "в");

            RowSelection.Move(list, 1);
            RowSelection.Move(list, 1);

            Assert.Equal("б", RowSelection.Item(list));

            RowSelection.Move(list, -1);

            Assert.Equal("а", RowSelection.Item(list));
        });
    }

    /// <summary>На краях списка ветка не зацикливается: шаг за край оставляет строку на месте.</summary>
    [Fact]
    public void Move_ЗаКраем_ОстаётсяНаМесте()
    {
        StaRunner.Run(() =>
        {
            var list = ListWith("а", "б", "в");

            // Без выделения шаг вниз берёт первую строку, и только ей и остаётся: выше края
            // списка строки нет, какой бы ещё шаг «выше» ни сделали.
            RowSelection.Move(list, 1);
            RowSelection.Move(list, -1);
            RowSelection.Move(list, -1);

            Assert.Equal("а", RowSelection.Item(list));

            RowSelection.Move(list, 1);
            RowSelection.Move(list, 1);
            RowSelection.Move(list, 1);
            RowSelection.Move(list, 1);

            Assert.Equal("в", RowSelection.Item(list));
        });
    }

    /// <summary>Пустой список стрелки молча пропускают: выделять нечего.</summary>
    [Fact]
    public void Move_ПустойСписок_Молчит()
    {
        StaRunner.Run(() =>
        {
            var list = ListWith();

            RowSelection.Move(list, 1);

            Assert.Null(RowSelection.Item(list));
        });
    }

    /// <summary>Стрелка и клик управляют одним выделением: клик после стрелки переносит строку.</summary>
    [Fact]
    public void Move_ИКлик_ПереключаютсяСогласованно()
    {
        StaRunner.Run(() =>
        {
            var list = ListWith("а", "б", "в");

            RowSelection.Move(list, 1);

            var second = (System.Windows.UIElement)list.ItemContainerGenerator.ContainerFromIndex(1)!;

            // Нажатие без устройства ввода в тесте имитируется самим событием: оно идёт по
            // той же маршрутизации списка, что и настоящий клик, и пишется в то же состояние.
            second.RaiseEvent(new MouseButtonEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Left)
            {
                RoutedEvent = Mouse.MouseDownEvent,
            });

            Assert.Equal("б", RowSelection.Item(list));

            RowSelection.Move(list, -1);

            Assert.Equal("а", RowSelection.Item(list));
        });
    }

    /// <summary>
    /// У строки с признаком на элементе (<see cref="ISelectableRow"/>) флаг едет с элементом:
    /// прежняя строка гаснет, выделенная горит. Такие строки идут в списках с виртуализацией.
    /// </summary>
    [Fact]
    public void Move_СтрокаСПризнакомНаЭлементе_ФлагПереносится()
    {
        StaRunner.Run(() =>
        {
            var list = ListWithItems(new SelectableRow(), new SelectableRow(), new SelectableRow());

            RowSelection.Move(list, 1);
            RowSelection.Move(list, 1);

            var rows = list.Items.OfType<SelectableRow>().ToList();

            Assert.True(rows[1].IsSelected);
            Assert.False(rows[0].IsSelected);
            Assert.Same(rows[1], RowSelection.Item(list));
        });
    }

    /// <summary>Клик по выделенной строке с признаком на элементе снимает выделение.</summary>
    [Fact]
    public void Click_ПовторныйПоВыделеннойСтроке_СнимаетВыделение()
    {
        StaRunner.Run(() =>
        {
            var list = ListWithItems(new SelectableRow(), new SelectableRow());

            RowSelection.Move(list, 1);

            var first = (System.Windows.UIElement)list.ItemContainerGenerator.ContainerFromIndex(0)!;

            first.RaiseEvent(new MouseButtonEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Left)
            {
                RoutedEvent = Mouse.MouseDownEvent,
            });

            var rows = list.Items.OfType<SelectableRow>().ToList();

            Assert.False(rows[0].IsSelected);
            Assert.Null(RowSelection.Item(list));
        });
    }

    /// <summary>Перечитывание списка заменяет элементы, и выделение молчит вместе со старыми строками.</summary>
    /// <remarks>
    /// Журнал пересобирается новыми строками дня на каждое перечитывание: элемент в поле умирает,
    /// и комбинация над выделенной строкой обязана молчать, а не открыть чужой день.
    /// </remarks>
    [Fact]
    public void Item_ПослеПеречитывания_Молчит()
    {
        StaRunner.Run(() =>
        {
            var list = ListWithItems(new SelectableRow(), new SelectableRow());

            RowSelection.Move(list, 1);

            Assert.NotNull(RowSelection.Item(list));

            list.Items.Clear();
            list.Items.Add(new SelectableRow());
            list.Items.Add(new SelectableRow());

            Assert.Null(RowSelection.Item(list));
        });
    }

    private static ItemsControl ListWithItems(params object[] items)
    {
        var list = new ItemsControl
        {
            Template = (ControlTemplate)XamlReader.Parse(
                "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"" +
                "                xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
                "  <Border><ItemsPresenter /></Border>" +
                "</ControlTemplate>"),
        };

        RowSelection.SetEnabled(list, true);

        foreach (var item in items)
        {
            list.Items.Add(item);
        }

        list.ApplyTemplate();
        list.Measure(new Size(400, double.PositiveInfinity));
        list.Arrange(new Rect(0, 0, 400, 300));

        return list;
    }

    /// <summary>Строка с признаком на элементе: то же, что <c>DayRowViewModel</c>, но без журнала.</summary>
    private sealed class SelectableRow : ISelectableRow
    {
        public bool IsSelected { get; set; }
    }
}
