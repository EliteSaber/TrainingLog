using System.ComponentModel;
using System.Windows;
using TrainingLog.Controls;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>Окно параметров: сколько дней показывать в журнале.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <param name="viewModel">Параметры приложения.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Подписка в окне, а не в модели: событие приходит от поля ввода и живёт в Controls.
        // Отписка в Closed обязательна — иначе закрытое окно удерживалось бы подпиской.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Фокус и выделение — при загрузке, а не в конструкторе: у ещё не показанного окна нет
    /// цепочки активации, и <c>Focus()</c> отработает вхолостую (ловушка 9).
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        DaysCountBox.Focus();
        DaysCountBox.SelectAll();
    }

    /// <summary>
    /// <c>DialogResult</c> выставляется по уведомлению: по одному значению
    /// <see cref="SettingsViewModel.Accepted"/> вызывающий <c>ShowDialog</c> не узнал бы, что
    /// настройка записана.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Accepted) && _viewModel.Accepted)
        {
            DialogResult = true;
        }
    }

    /// <summary>
    /// Введено или вставлено недопустимое: показать объяснение и перезапустить отсчёт.
    /// </summary>
    /// <remarks>
    /// Якорь берётся из <see cref="AllowedCharsInput.RejectedEventArgs.Field"/>, а не из
    /// <c>e.Source</c>: событие всплывает от поля до окна, и на приёмнике <c>Source</c> уже не
    /// поле (ловушка 35). Отсчёт перезапускается явно — повторный отказ даёт тот же текст,
    /// состояние подсказки не меняется, и без этого она погасла бы на середине набора.
    /// </remarks>
    private void OnInputRejected(object sender, RoutedEventArgs e)
    {
        if (e is not AllowedCharsInput.RejectedEventArgs rejected)
        {
            return;
        }

        InputHint.Target = rejected.Field;

        _viewModel.Hint.ShowCommand.Execute(rejected.Message);

        InputHint.RestartCountdown();
    }

    /// <summary>Допустимый ввод гасит висящее объяснение. Отказ сюда не приводит: мигал бы.</summary>
    private void OnInputAccepted(object sender, RoutedEventArgs e) =>
        _viewModel.Hint.DismissCommand.Execute(null);

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
    }
}
