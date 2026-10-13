using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TrainingLog.Controls;
using TrainingLog.ViewModels;

namespace TrainingLog;

/// <summary>
/// Главное окно журнала тренировок: дни, количество показываемых дней и меню параметров.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;

        _viewModel = viewModel;

        // Отказ по вводу приходит от поля количества дней, и подсказкой его показывает StatusHint.
        // Окно живёт до конца работы приложения, поэтому отписка в Closed — формальность.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>В поле количества дней введено или вставлено недопустимое.</summary>
    /// <remarks>
    /// Здесь только якорь и команда: вид, текст, срок жизни, позиция и крестик живут в
    /// <see cref="StatusHint"/> и в состоянии <c>Hint</c>. Якорь задаётся здесь, а не в разметке:
    /// подсказка общая у отказа и примечания, а якорь у них разный, и локальное присваивание
    /// заменило бы привязку навсегда (ловушка 11).
    ///
    /// Отсчёт перезапускается явно: повторный отказ даёт тот же текст, состояние подсказки не
    /// меняется, и без этого она погасла бы на середине набора.
    /// </remarks>
    private void OnInputRejected(object sender, RoutedEventArgs e)
    {
        if (e is AllowedCharsInput.RejectedEventArgs rejected)
        {
            InputHint.Target = DaysCountBox;

            _viewModel.Hint.ShowCommand.Execute(rejected.Message);

            InputHint.RestartCountdown();
        }
    }

    /// <summary>
    /// В поле введён или вставлен допустимый символ: гасим висящее объяснение. Отказ сюда не
    /// приводит — иначе подсказка мигала бы на каждом негодном символе.
    /// </summary>
    private void OnInputAccepted(object sender, RoutedEventArgs e) =>
        _viewModel.Hint.DismissCommand.Execute(null);

    private void OnClosed(object? sender, EventArgs e)
    {
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
    }

    /// <summary>
    /// Нажата кнопка «Показать» у упражнения: показываем примечание.
    /// </summary>
    /// <remarks>
    /// Вид и срок задаёт модель (<see cref="HintState.ShowNoteCommand"/>), окно знает только якорь:
    /// имя у кнопок в строке данных отсутствует — они создаются по составу упражнений. Якорь
    /// задаётся до показа, потому что позиция считается в момент включения показа.
    ///
    /// Пустой якорь или ячейка без примечания — не показываем ничего: так случиться не может,
    /// но проверка стоит копейку и снимает вопрос «а что показывает <c>null</c>».
    /// </remarks>
    private void OnShowNoteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || button.DataContext is not DayExerciseCell { HasNote: true } exercise)
        {
            return;
        }

        InputHint.Target = button;

        _viewModel.Hint.ShowNoteCommand.Execute(exercise.Note);

        InputHint.RestartCountdown();
    }

    /// <summary>
    /// Дни читаются при загрузке окна, а не в конструкторе: до показа окна ждать нечего — список
    /// всё равно никто не увидит.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>
    /// Открывает меню параметров по нажатию на кнопку-шестерёнку.
    /// </summary>
    private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
    {
        if (SettingsButton.ContextMenu is not { } menu)
        {
            return;
        }

        menu.PlacementTarget = SettingsButton;
        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>Двойной клик по строке дня открывает правку этой даты.</summary>
    /// <remarks>
    /// Подписка на списке дней, а не на строке: <c>MouseDoubleClick</c> объявлен у
    /// <see cref="Control"/>, а строка дня — <c>Border</c>, то есть <c>Decorator</c> без него; в
    /// разметке такое событие на <c>Border</c> и не поставилось бы. Строка находится по элементу под
    /// указателем штатным <see cref="ItemsControl.ContainerFromElement(DependencyObject)"/>.
    ///
    /// Команда, а не прямой вызов окна: дабл-клик и пункт меню обязаны делать одно и то же,
    /// и перечитывать журнал после правки должен один и тот же код.
    /// </remarks>
    private void OnDayDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl days)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source
            || days.ContainerFromElement(source) is not FrameworkElement { DataContext: DayRowViewModel day } container)
        {
            return;
        }

        if (IsShowNoteButton(source, container))
        {
            return;
        }

        _viewModel.EditDayCommand.Execute(day);
    }

    /// <summary>Двойной клик пришёлся по кнопке «Показать», а не по самой строке.</summary>
    /// <remarks>
    /// Два быстрых клика по «Показать» — обычный способ перечитать примечание, и правку дня они
    /// открывать не должны. Проверка живёт здесь, а не гашением <c>MouseDoubleClick</c> на кнопке:
    /// событие поднимается от элемента под указателем, и на самой кнопке помечать его оказалось
    /// бесполезно (ловушка 45).
    ///
    /// Проверка сделана по данным ячейки, чтобы сработать ровно на той кнопке, которая показывает
    /// примечание.
    /// </remarks>

    /// <param name="source">Элемент под указателем.</param>
    /// <param name="container">Контейнер строки дня: обход идёт до него, не включая.</param>
    private static bool IsShowNoteButton(DependencyObject source, DependencyObject container)
    {
        for (var element = source; element is not null && !ReferenceEquals(element, container); element = ParentOf(element))
        {
            if (element is Button { DataContext: DayExerciseCell { HasNote: true } })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Родитель элемента: визуальный для <see cref="Visual"/>, логический для прочего.</summary>
    /// <remarks>
    /// Ветка для не-<see cref="Visual"/> нужна на всякий случай: <c>VisualTreeHelper</c> такой
    /// элемент не берёт и бросает исключение, а источником события может оказаться любой элемент под
    /// указателем.
    /// </remarks>
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    /// <summary>Кладёт строку дня в открываемое меню: модель главного окна и параметр команды.</summary>
    /// <remarks>
    /// Из разметки это не выразить: у <c>ContextMenu</c> отдельное логическое дерево, внутрь него не
    /// наследуется <c>DataContext</c> и не доходит поиск предка до окна — в отличие от привязки
    /// <c>PlacementTarget.DataContext</c> в меню шестерёнки (ловушки 1 и 41). Здесь нужны оба
    /// значения сразу. Событие <c>ContextMenuOpening</c> поднимается до показа меню, поэтому к
    /// моменту вычисления привязок оба значения уже на месте.
    /// </remarks>
    private void OnDayContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement element
            || element.ContextMenu is not { } menu
            || element.DataContext is not DayRowViewModel day)
        {
            return;
        }

        menu.DataContext = _viewModel;

        foreach (var item in menu.Items)
        {
            if (item is MenuItem menuItem)
            {
                menuItem.CommandParameter = day;
            }
        }
    }
}
