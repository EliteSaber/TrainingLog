using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TrainingLog.Controls;

/// <summary>Выделение строки списка кликом: списки в проекте — ItemsControl без выделения.</summary>
/// <remarks>
/// Клик по строке выделяет её, повторный клик снимает. Подсветка задаётся слабым системным
/// цветом на рамке строки триггером разметки. Нажатия по кнопкам внутри строки выделение не
/// меняют: кнопки сами гасят MouseDown, и клик «Редактировать» не должен выделять строку.
///
/// Признак выделения хранится в двух видах. Панель с переработкой контейнеров отдаёт один
/// контейнер разным строкам, и оставленный на контейнере признак подсветил бы чужую строку, —
/// поэтому у элементов, реализующих <see cref="ISelectableRow"/>, признак живёт на элементе
/// и привязка подсветки читает его из данных. Спискам без переработки признак на контейнере
/// никуда не девался. Само выделение в обоих видах — элемент данных, запомненный на списке:
/// перечитывание списка заменяет элементы, и мёртвое выделение молчит (ловушка 18).
/// </remarks>
public static class RowSelection
{
    /// <summary>Включает выделение строк у списка.</summary>
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(RowSelection),
            new PropertyMetadata(false, OnEnabledChanged));

    /// <summary>Признак выделенной строки у её контейнера: читается триггером подсветки.</summary>
    /// <remarks>
    /// Живёт на контейнере потому, что он переживает перечитывание строк целиком — по ловушке 18
    /// строки могут пересоздаваться, и выделение честно пропадает вместе с ними. Спискам с
    /// переработкой контейнеров этот признак не годится — см. <see cref="ISelectableRow"/>.
    /// </remarks>
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.RegisterAttached("IsSelected", typeof(bool), typeof(RowSelection));
    public static void SetEnabled(DependencyObject element, bool value) =>
        element.SetValue(EnabledProperty, value);

    public static bool GetEnabled(DependencyObject element) =>
        (bool)element.GetValue(EnabledProperty);

    public static void SetIsSelected(DependencyObject element, bool value) =>
        element.SetValue(IsSelectedProperty, value);

    public static bool GetIsSelected(DependencyObject element) =>
        (bool)element.GetValue(IsSelectedProperty);

    // Выделенный элемент данных списка: ищется кликом, двигается стрелками, отдаётся Item().
    private static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItem",
            typeof(object),
            typeof(RowSelection));

    /// <summary>Выделенная строка данных, иначе <c>null</c>.</summary>
    /// <remarks>
    /// Элемент обязан дожить до этого момента: после перечитывания списка объект в поле
    /// мёртвый, и выделение по нему не отдаётся — комбинация молчит. Мёртвым элемент считается,
    /// когда его больше нет в списке.
    /// </remarks>
    public static object? Item(ItemsControl list)
    {
        var item = list.GetValue(SelectedItemProperty);

        return item is not null && list.Items.IndexOf(item) >= 0 ? item : null;
    }

    /// <summary>Снимает выделение: ни строки, ни подсветки.</summary>
    /// <remarks>
    /// Нужен там, где строки пересоздаются целиком: пересборка состава плана меняет набор строк,
    /// и прежнее выделение осталось бы в данных без подсветки — горячие клавиши работали бы по
    /// строке, которой на экране нет. Перестановка строк сюда не относится: там контейнер едет
    /// вместе со строкой, и выделение сохраняется само.
    /// </remarks>
    public static void Clear(ItemsControl list)
    {
        ArgumentNullException.ThrowIfNull(list);

        var item = list.GetValue(SelectedItemProperty);

        ClearSelection(list, item);

        list.SetValue(SelectedItemProperty, null);
    }

    /// <summary>Сдвигает выделение строк на <paramref name="delta"/> позиций: стрелки выделения.</summary>
    /// <remarks>
    /// Без выделения шаг вниз выделяет первую строку, вверх — последнюю: стрелки обязаны работать
    /// и без предварительного клика мышью. За краями списка выделение остаётся на месте —
    /// зацикливание уводило бы строку из видимой области неожиданно для глаза.
    ///
    /// Мёртвое после перечитывания выделение считается «без выделения» — выделение умирает
    /// вместе со строками по ловушке 18 — так же, как его читает <see cref="Item"/>. Строка за
    /// видимой областью не помеха: у списка с виртуализацией панель сама генерирует её и
    /// прокручивает к ней (<see cref="VirtualizingPanel.BringIndexIntoViewPublic(int)"/>).
    /// </remarks>
    public static void Move(ItemsControl list, int delta)
    {
        ArgumentNullException.ThrowIfNull(list);

        var count = list.Items.Count;

        if (count == 0)
        {
            return;
        }

        var selected = list.GetValue(SelectedItemProperty);
        var currentIndex = selected is not null ? list.Items.IndexOf(selected) : -1;

        // Мёртвый или снятый элемент даёт -1: выделение пропало, и начинаем с края по
        // направлению шага — как и без выделения вовсе.
        var index = currentIndex < 0
            ? delta > 0 ? 0 : count - 1
            : Math.Clamp(currentIndex + delta, 0, count - 1);

        var item = list.Items[index];
        var container = list.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;

        ClearSelection(list, selected);

        if (item is ISelectableRow row)
        {
            row.IsSelected = true;
        }
        else if (container is not null)
        {
            SetIsSelected(container, true);
        }

        list.SetValue(SelectedItemProperty, item);

        // Список внутри ScrollViewer видит запрос BringIntoView сквозь строку: контейнеры
        // росли вверх от низа, и без докрутки выделение уходило бы за нижний край окна.
        if (container is not null)
        {
            container.BringIntoView();
        }
        else if (FindPanel(list) is { } panel)
        {
            panel.BringIndexIntoViewPublic(index);
        }
    }

    /// <summary>Контейнер строки элемента, иначе <c>null</c>.</summary>
    /// <remarks>
    /// Список с виртуализацией может не держать контейнер для строки за видимой областью:
    /// панель сначала генерирует и прокручивает её, и контейнер забирается после раскладки.
    /// </remarks>
    public static FrameworkElement? Container(ItemsControl list, object item)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(item);

        if (list.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
        {
            return container;
        }

        var index = list.Items.IndexOf(item);

        if (index < 0 || FindPanel(list) is not { } panel)
        {
            return null;
        }

        panel.BringIndexIntoViewPublic(index);
        list.UpdateLayout();

        return list.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
    }

    /// <summary>Панель с виртуализацией внутри списка, иначе <c>null</c>.</summary>
    /// <remarks>
    /// Обход визуального дерева, а не готового свойства: у <see cref="ItemsControl"/> хост
    /// панелей internal, а сама панель может лежать глубже — ScrollViewer в шаблоне списка
    /// стоит между презентатором и списком. Глубина дерева списка мала, обход дёшев.
    /// </remarks>
    private static VirtualizingStackPanel? FindPanel(DependencyObject root)
    {
        if (root is VirtualizingStackPanel panel)
        {
            return panel;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var index = 0; index < count; index++)
        {
            if (FindPanel(VisualTreeHelper.GetChild(root, index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // Признак снимается с прежней строки: у элементов он едет с ними сам, у контейнеров
    // прежний контейнер ищется по элементу.
    private static void ClearSelection(ItemsControl list, object? item)
    {
        if (item is null)
        {
            return;
        }

        if (item is ISelectableRow row)
        {
            row.IsSelected = false;

            return;
        }

        if (list.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
        {
            SetIsSelected(container, false);
        }
    }

    private static void OnEnabledChanged(
        DependencyObject element,
        DependencyPropertyChangedEventArgs e)
    {
        var list = (ItemsControl)element;

        if ((bool)e.NewValue)
        {
            list.AddHandler(Mouse.MouseDownEvent, MouseDownHandler);
        }
        else
        {
            list.RemoveHandler(Mouse.MouseDownEvent, MouseDownHandler);
        }
    }

    // Делегат события типизирован явно: метод-группа в параметре AddHandler'а типа Delegate
    // оборачивается компилятором в natural-тип (Action<object, MouseButtonEventArgs>), а не в
    // MouseButtonEventHandler события, — расхождение типов сборка молчит, а WPF рантаймом
    // бросает «Несоответствие типа обработчика» при применении Enabled (ловушка 24).
    // Один экземпляр и для Add, и для Remove: пару RemoveHandler ищет сравнением делегатов.
    private static readonly MouseButtonEventHandler MouseDownHandler = new(OnMouseDown);

    // Событие-панель (Mouse.MouseDownEvent), а не сгенерированное MouseButtonEventArgs:
    // подписка AddHandler'ом, и обработчик вешается до событий строк.
    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl list
            || e.OriginalSource is not DependencyObject source
            || list.ContainerFromElement(source) is not FrameworkElement container)
        {
            return;
        }

        var item = container.DataContext;
        var previous = list.GetValue(SelectedItemProperty);

        // Повторный клик по выделенной строке снимает выделение.
        if (ReferenceEquals(previous, item))
        {
            ClearSelection(list, item);
            list.SetValue(SelectedItemProperty, null);

            return;
        }

        ClearSelection(list, previous);

        if (item is ISelectableRow row)
        {
            row.IsSelected = true;
        }
        else
        {
            SetIsSelected(container, true);
        }

        list.SetValue(SelectedItemProperty, item);
    }
}
