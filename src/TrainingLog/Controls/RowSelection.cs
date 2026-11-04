using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TrainingLog.Controls;

/// <summary>Выделение строки списка кликом: списки в проекте — ItemsControl без выделения.</summary>
/// <remarks>
/// Клик по строке выделяет её, повторный клик снимает. Конкретно строки ходят своими рамками,
/// поэтому подсветка задаётся слабым системным цветом на контейнере — стилем контейнеров,
/// а не перестройкой шаблонов. Нажатия по кнопкам внутри строки выделение не меняют: кнопки
/// сами гасят MouseDown, и клик «Редактировать» не должен выделять строку.
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
    /// строки могут пересоздаваться, и выделение честно пропадает вместе с ними.
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

    // Контейнер выделенной строки: ищется кликом и держится на списке, отдаётся Item().
    // Отдельный контейнер: тип без nullable-суффикса, typeof с «?» для ссылочных типов запрещён.
    private static readonly DependencyProperty SelectedContainerProperty =
        DependencyProperty.RegisterAttached(
            "SelectedContainer",
            typeof(FrameworkElement),
            typeof(RowSelection));

    /// <summary>Выделенная строка данных, иначе <c>null</c>.</summary>
    /// <remarks>
    /// Контейнер должен дожить до этого момента: после перечитывания списка объект в поле
    /// мёртвый, и выделение по нему не отдаётся — комбинация молчит.
    /// </remarks>
    public static object? Item(ItemsControl list)
    {
        if (list.GetValue(SelectedContainerProperty) is not FrameworkElement container)
        {
            return null;
        }

        // Мёртвый контейнер больше не внутрь этого списка: перечитывание могло его заменить.
        var alive = container is ContentPresenter presentation
            && ItemsControl.ItemsControlFromItemContainer(presentation) == list;

        return alive && GetIsSelected(container) ? container.DataContext : null;
    }

    /// <summary>Сдвигает выделение строк на <paramref name="delta"/> позиций: стрелки выделения.</summary>
    /// <remarks>
    /// Без выделения шаг вниз выделяет первую строку, вверх — последнюю: стрелки обязаны работать
    /// и без предварительного клика мышью. За краями списка выделение остаётся на месте —
    /// зацикливание уводило бы строку из видимой области неожиданно для глаза.
    ///
    /// Мёртвый после перечитывания контейнер считается «без выделения» — выделение умирает вместе
    /// со строками по ловушке 18 — так же, как его читает <see cref="Item"/>. Строка для выделения
    /// даётся генератором, а не обходом визуального дерева: контейнеры перечисляются им же.
    /// </remarks>
    public static void Move(ItemsControl list, int delta)
    {
        ArgumentNullException.ThrowIfNull(list);

        var count = list.Items.Count;

        if (count == 0)
        {
            return;
        }

        var generator = list.ItemContainerGenerator;

        var index = list.GetValue(SelectedContainerProperty) is FrameworkElement selected
            && GetIsSelected(selected)
                ? generator.IndexFromContainer(selected)
                : -1;

        // Мёртвый контейнер за пределами списка даёт -1: выделение пропало, и начинаем
        // с края по направлению шага — как и без выделения вовсе.
        index = index < 0
            ? delta > 0 ? 0 : count - 1
            : Math.Clamp(index + delta, 0, count - 1);

        // Нереализованный контейнер не заменяется: виртуализации в списках нет, а дождаться
        // поколения на заявку клавиши нечем.
        if (generator.ContainerFromIndex(index) is not FrameworkElement container)
        {
            return;
        }

        if (list.GetValue(SelectedContainerProperty) is FrameworkElement previous
            && !ReferenceEquals(previous, container))
        {
            SetIsSelected(previous, false);
        }

        SetIsSelected(container, true);
        list.SetValue(SelectedContainerProperty, container);

        // Список внутри ScrollViewer видит запрос BringIntoView сквозь строку: контейнеры
        // росли вверх от низа, и без докрутки выделение уходило бы за нижний край окна.
        container.BringIntoView();
    }

    /// <summary>Сам контейнер выделенной строки, иначе <c>null</c>: нужен для поиска кнопок внутри.</summary>
    public static FrameworkElement? Container(ItemsControl list) =>
        list.GetValue(SelectedContainerProperty) is FrameworkElement container
        && GetIsSelected(container)
            ? container
            : null;

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

        if (list.GetValue(SelectedContainerProperty) is FrameworkElement previous
            && !ReferenceEquals(previous, container))
        {
            SetIsSelected(previous, false);
        }

        SetIsSelected(container, (bool)container.GetValue(IsSelectedProperty) is false);
        list.SetValue(SelectedContainerProperty, container);
    }
}
