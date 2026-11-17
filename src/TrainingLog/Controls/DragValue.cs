using System.Windows;
using System.Windows.Input;

namespace TrainingLog.Controls;

/// <summary>
/// Число, меняемое перетаскиванием: зажал левую кнопку и повёл мышь вправо — стало больше,
/// влево — меньше. Повторяемое поведение: достаточно указать на поле
/// <c>DragValue.Value</c> и <c>DragValue.Command</c>.
/// </summary>
/// <remarks>
/// Поведение не пишет в привязанное свойство (ловушка 11): значение меняет команда, а та —
/// модель представления. Привязка остаётся целой, и поле обновляется обычным путём.
///
/// Текущее значение берётся из <see cref="ValueProperty"/>, а не разбирается из текста поля:
/// текст может быть пуст или негоден, и разбор молча сломал бы перетаскивание ровно тогда,
/// когда им пользуются, — то есть сразу после правки числа вручную.
///
/// Команда получает готовое число, а не сдвиг: перетаскивание считает значение от того, на
/// котором начали, иначе второе движение мыши сложилось бы с первым.
/// </remarks>
public static class DragValue
{
    /// <summary>
    /// Сколько пикселей нужно пройти мышью, чтобы движение признали перетаскиванием.
    /// </summary>
    /// <remarks>
    /// Порог нужен, чтобы обычный клик по полю не сдвигал число: рука дрожит всегда, и без
    /// порога значение менялось бы от одного попадания по полю.
    /// </remarks>
    public const double DragThreshold = 3;

    /// <summary>Команда, которой передаётся новое значение.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(DragValue),
        new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    /// <summary>Текущее значение поля: с него начинается отсчёт перетаскивания.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached(
        "Value",
        typeof(int),
        typeof(DragValue),
        new PropertyMetadata(0));

    public static int GetValue(DependencyObject element) => (int)element.GetValue(ValueProperty);

    public static void SetValue(DependencyObject element, int value) => element.SetValue(ValueProperty, value);

    /// <summary>Сколько единиц приходится на пиксель движения мыши.</summary>
    public static readonly DependencyProperty StepProperty = DependencyProperty.RegisterAttached(
        "Step",
        typeof(int),
        typeof(DragValue),
        new PropertyMetadata(1));

    public static int GetStep(DependencyObject element) => (int)element.GetValue(StepProperty);

    public static void SetStep(DependencyObject element, int value) => element.SetValue(StepProperty, value);

    /// <summary>Состояние перетаскивания: своё у каждого элемента.</summary>
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof(DragState),
        typeof(DragValue),
        new PropertyMetadata(null));

    private sealed class DragState
    {
        /// <summary>Куда нажали: от этой точки считается движение.</summary>
        public double StartX { get; set; }

        /// <summary>Значение на момент нажатия: от него считается новое.</summary>
        public int StartValue { get; set; }

        /// <summary>Идёт перетаскивание: движение мыши уже означает изменение значения.</summary>
        public bool Dragging { get; set; }

        /// <summary>Курсор, который был у элемента до перетаскивания: его возвращаем.</summary>
        public Cursor? PreviousCursor { get; set; }
    }

    /// <summary>
    /// Новое значение по движению мыши: от значения на нажатии плюс сдвиг.
    /// </summary>
    /// <param name="startValue">Значение на момент нажатия.</param>
    /// <param name="pixelOffset">На сколько пикселей ушла мышь от точки нажатия.</param>
    /// <param name="step">Сколько единиц приходится на пиксель.</param>
    /// <returns>Новое значение.</returns>
    public static int ComputeValue(int startValue, double pixelOffset, int step) =>
        startValue + (int)Math.Round(pixelOffset) * step;

    /// <summary>
    /// Движение уже достаточно велико, чтобы считать его перетаскиванием.
    /// </summary>
    /// <param name="pixelOffset">На сколько пикселей ушла мышь от точки нажатия.</param>
    /// <param name="threshold">Порог перетаскивания.</param>
    /// <returns><c>true</c>, если движение признано перетаскиванием.</returns>
    public static bool ExceedsThreshold(double pixelOffset, double threshold) =>
        Math.Abs(pixelOffset) >= threshold;

    /// <summary>
    /// Команда появилась или исчезла: заодно с ней элемент учится слушать мышь.
    /// </summary>
    /// <remarks>
    /// Слушатели привязаны к команде, а не к значению: значение у поля может быть нулём
    /// (смена выглядела бы как отсутствие свойства), а команда у перетаскиваемого поля есть
    /// всегда.
    ///
    /// Подписка с <c>handledEventsToo</c>: TextBox помечает нажатие обработанным, и обычный
    /// слушатель на том же элементе после этого не получил бы события вовсе — перетаскивание
    /// молча не работало бы. Обработчик приходится заводить именованным: в <c>AddHandler</c>
    /// параметр имеет тип <c>Delegate</c>, и группа методов в него не конвертируется
    /// (ловушка 14).
    /// </remarks>
    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, MouseDownHandler);
        element.RemoveHandler(UIElement.PreviewMouseMoveEvent, MouseMoveHandler);
        element.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, MouseUpHandler);

        if (e.NewValue is ICommand)
        {
            element.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, MouseDownHandler, handledEventsToo: true);
            element.AddHandler(UIElement.PreviewMouseMoveEvent, MouseMoveHandler, handledEventsToo: true);
            element.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, MouseUpHandler, handledEventsToo: true);
        }
    }

    /// <summary>Обработчики нажатия, движения и отпускания: именованные, а не группа методов.</summary>
    private static readonly MouseButtonEventHandler MouseDownHandler = OnMouseLeftButtonDown;

    private static readonly MouseEventHandler MouseMoveHandler = OnMouseMove;

    private static readonly MouseButtonEventHandler MouseUpHandler = OnMouseLeftButtonUp;

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var state = State(element);

        state.StartX = e.GetPosition(element).X;
        state.StartValue = GetValue(element);
        state.Dragging = false;
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var state = State(element);
        var offset = e.GetPosition(element).X - state.StartX;

        if (!state.Dragging)
        {
            if (!ExceedsThreshold(offset, DragThreshold))
            {
                return;
            }

            state.Dragging = true;
            state.PreviousCursor = element.Cursor;
            element.Cursor = Cursors.SizeWE;

            // Захват мыши: перетаскивание продолжается и за пределами поля, иначе движение
            // обрывалось бы на краю, и число менялось бы на половину пути.
            element.CaptureMouse();
        }

        if (GetCommand(element) is { } command)
        {
            var value = ComputeValue(state.StartValue, offset, GetStep(element));

            if (command.CanExecute(value))
            {
                command.Execute(value);
            }
        }

        // Дальше — не текст: пока мышь ведёт значение, поле не должно ездить за кареткой.
        e.Handled = true;
    }

    private static void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var state = State(element);

        if (!state.Dragging)
        {
            return;
        }

        state.Dragging = false;
        element.Cursor = state.PreviousCursor;
        state.PreviousCursor = null;

        if (element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    /// <summary>Состояние элемента: создаётся при первом обращении.</summary>
    private static DragState State(FrameworkElement element)
    {
        if (element.GetValue(StateProperty) is DragState state)
        {
            return state;
        }

        state = new DragState();
        element.SetValue(StateProperty, state);

        return state;
    }
}
