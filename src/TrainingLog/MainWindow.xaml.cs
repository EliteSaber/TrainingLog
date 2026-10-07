using System.Windows;
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
}