using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    /// Всё остальное — текст подсказки, срок жизни, позиция и крестик — живёт в
    /// <see cref="StatusHint"/> и в состоянии <c>Hint</c>. Здесь только команда: окно знает,
    /// что отказ случился, а не как он выглядит.
    ///
    /// Отказ по вводу: показать объяснение и перезапустить отсчёт. Отсчёт явно, потому что
    /// повторный отказ даёт тот же текст и состояние подсказки не меняется, так что само по себе
    /// она погасла бы на середине набора.
    /// </remarks>
    private void OnInputRejected(object sender, RoutedEventArgs e)
    {
        if (e is AllowedCharsInput.RejectedEventArgs rejected)
        {
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

        if (e.OriginalSource is DependencyObject source
            && days.ContainerFromElement(source) is FrameworkElement { DataContext: DayRowViewModel day })
        {
            _viewModel.EditDayCommand.Execute(day);
        }
    }

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
