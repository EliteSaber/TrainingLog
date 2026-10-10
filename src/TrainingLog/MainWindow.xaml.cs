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

        // Отказ по вводу приходит от поля количества дней, и подсказкой его показывает
        // StatusHint. Окно живёт до конца работы приложения, поэтому отписка в Closed —
        // формальность, а не необходимость: подписка всё равно снимается вместе с самим окном.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// В поле количества дней введено или вставлено недопустимое.
    /// </summary>
    /// <remarks>
    /// Всё остальное — вид, текст, срок жизни, позиция и крестик — живёт в
    /// <see cref="StatusHint"/> и в состоянии <c>Hint</c>. Здесь только якорь и команда: окно
    /// знает, что отказ случился и от какого поля, а не как он выглядит.
    ///
    /// Якорь задаётся здесь, а не в разметке: подсказка общая у отказа и примечания, и якорь у
    /// них разный. Задаётся до показа — подсказка позиционируется при появлении, и без цели она
    /// бы не знала, под кем встать. Без этого примечание, показанное кнопкой «Показать»,
    /// оставило бы отказ висеть под той же кнопкой.
    ///
    /// Отказ по вводу: показать объяснение и перезапустить отсчёт. Отсчёт явно, потому что
    /// повторный отказ даёт тот же текст и состояние подсказки не меняется, так что само по себе
    /// она погасла бы на середине набора.
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
    /// В поле введён или вставлен допустимый символ.
    /// </summary>
    /// <remarks>
    /// Гасит висящее объяснение: пользователь сделал то, чего оно требовало, и держать её дальше
    /// незачем — она только закрывала бы поле. Команда та же, что у крестика, таймера и нажатия
    /// снаружи, поэтому текст подсказки убирается тем же путём.
    ///
    /// Отказ сюда не приводит: иначе подсказка мигала бы на каждом негодном символе.
    /// </remarks>
    private void OnInputAccepted(object sender, RoutedEventArgs e) =>
        _viewModel.Hint.DismissCommand.Execute(null);

    private void OnClosed(object? sender, EventArgs e)
    {
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
    }

    /// <summary>
    /// Нажата кнопка «Показать» у упражнения: показываем примечание той же подсказкой, что и
    /// отказ по вводу, только обычным тоном и на тридцать секунд.
    /// </summary>
    /// <remarks>
    /// Вид и срок задаёт модель (<see cref="HintState.ShowNoteCommand"/>), окно знает только
    /// якорь: подсказка должна встать под нажатую кнопку, а имя у кнопок в строке данных
    /// отсутствует — они создаются по составу упражнений.
    ///
    /// Якорь задаётся до показа: позиция считается в момент включения показа. Отсчёт
    /// перезапускается явно — нажатие подряд по двум кнопкам с одинаковым текстом примечания не
    /// меняет состояние, и подсказка погасла бы на середине срока.
    ///
    /// Пустой якорь или ячейка без примечания — не показываем ничего: так случиться не может
    /// (кнопка свёрнута, когда примечания нет), но проверка стоит копейку и снимает вопрос «а
    /// что показывает <c>null</c>».
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
    /// Дни читаются при загрузке окна, а не в конструкторе: чтение из базы асинхронно, и
    /// до показа окна ждать нечего — список всё равно никто не увидит.
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

    /// <summary>
    /// Двойной клик по строке дня открывает правку этой даты.
    /// </summary>
    /// <remarks>
    /// Подписка на списке дней, а не на строке: <c>MouseDoubleClick</c> объявлен у
    /// <see cref="Control"/>, а строка дня — <c>Border</c>, то есть <c>Decorator</c> без
    /// <c>Control</c>; в разметке такое событие на <c>Border</c> и не поставилось бы. Список
    /// дней — <c>ItemsControl</c>, то есть как раз <see cref="Control"/>.
    ///
    /// Строка находится по элементу под мышью штатным
    /// <see cref="ItemsControl.ContainerFromElement(DependencyObject)"/>: под указателем может
    /// оказаться <c>TextBlock</c> с названием упражнения или числом веса, и контейнер строки
    /// поднимается от него внутри списка.
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

    /// <summary>
    /// Двойной клик пришёлся по кнопке «Показать» у упражнения, а не по самой строке.
    /// </summary>
    /// <remarks>
    /// Два быстрых клика по «Показать» — обычный способ перечитать примечание, и правку дня они
    /// открывать не должны. Проверка живёт здесь, а не гашением <c>MouseDoubleClick</c> на кнопке:
    /// событие поднимается от элемента под указателем, и на самой кнопке помечать его оказалось
    /// бесполезно — до списка дней оно доходило как ни в чём не бывало (ловушка 45).
    ///
    /// Обход идёт от элемента под указателем до контейнера строки: под указателем может лежать
    /// <c>TextBlock</c> с надписью кнопки, а кнопка в строке дня одна, так что проверки
    /// «есть ли кнопка на пути» достаточно — но и она сделана по данным ячейки, чтобы сработать
    /// ровно на той кнопке, которая показывает примечание.
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

    /// <summary>
    /// Родитель элемента: визуальный для <see cref="Visual"/>, логический для прочего.
    /// </summary>
    /// <remarks>
    /// Ветка для не-<see cref="Visual"/> нужна на всякий случай: <c>VisualTreeHelper</c> такой
    /// элемент не берёт и бросает исключение, а источником события может оказаться любой элемент
    /// под указателем. Обход вверх на глубине дерева строки дня стоит копейку, поэтому исключение
    /// ловить нечем.
    /// </remarks>
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    /// <summary>
    /// Кладёт строку дня в открываемое меню: модель главного окна и параметр команды.
    /// </summary>
    /// <remarks>
    /// Из разметки это не выразить. У <c>ContextMenu</c> отдельное логическое дерево: внутрь
    /// него не наследуется <c>DataContext</c> и не доходит поиск предка до окна — в отличие от
    /// привязки <c>PlacementTarget.DataContext</c>, которая доступна и потому работает в меню
    /// шестерёнки (ловушки 1 и 41). Здесь нужны оба значения сразу: команда — у модели
    /// главного окна, параметр — сама строка дня.
    ///
    /// Событие <c>ContextMenuOpening</c> поднимается до показа меню, поэтому к моменту
    /// вычисления привязок оба значения уже на месте.
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
