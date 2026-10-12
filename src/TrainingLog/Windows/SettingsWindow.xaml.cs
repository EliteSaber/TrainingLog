using System.ComponentModel;
using System.Windows;
using TrainingLog.Controls;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Окно параметров: сколько дней показывать в журнале.
/// </summary>
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

        // Подписка на отказ в окне, а не в модели: событие приходит от поля ввода, живёт в
        // поведении Controls, и модель о нём знать не должна. Отписка в Closed обязательна —
        // иначе закрытое окно вместе с визуальным деревом удерживалось бы подпиской.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Фокус и выделение — при загрузке, а не в конструкторе: у ещё не показанного окна нет
    /// цепочки активации, и Focus() в конструкторе отработает вхолостую (ловушка 9). Текст
    /// выделяется целиком, чтобы значение удобно было набрать заново.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        DaysCountBox.Focus();
        DaysCountBox.SelectAll();
    }

    /// <summary>
    /// Кнопка «Сохранить» привязана к команде, поэтому WPF сама снимает её активность, когда
    /// значение негодное. А вот результат известен только здесь: по одному значению
    /// <see cref="SettingsViewModel.Accepted"/> вызывающий <c>ShowDialog</c> поймёт, что
    /// настройка записана и главному окну пора её перечитать. Поэтому
    /// <see cref="DialogResult"/> выставляется по уведомлению.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Accepted) && _viewModel.Accepted)
        {
            DialogResult = true;
        }
    }

    /// <summary>
    /// В поле количества дней введено или вставлено недопустимое.
    /// </summary>
    /// <remarks>
    /// Якорь подсказки задаётся здесь, а не в разметке: подсказка общая у отказа по вводу и у
    /// примечания, а якорь у них разный. Берётся из
    /// <see cref="AllowedCharsInput.RejectedEventArgs.Field"/>, а не из <c>e.Source</c>:
    /// событие всплывает от поля до окна, и на приёмнике всплытия <c>Source</c> уже не поле
    /// (ловушка 35).
    ///
    /// Отсчёт перезапускается явно: повторный отказ даёт тот же текст, состояние подсказки не
    /// меняется, и без этого она погасла бы на середине набора.
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

    /// <summary>
    /// В поле введён или вставлен допустимый символ.
    /// </summary>
    /// <remarks>
    /// Гасит висящее объяснение: пользователь сделал то, чего оно требовало, и держать её дальше
    /// незачем — она закрывала бы само поле. Отказ сюда не приводит: иначе подсказка мигала бы на
    /// каждом негодном символе.
    /// </remarks>
    private void OnInputAccepted(object sender, RoutedEventArgs e) =>
        _viewModel.Hint.DismissCommand.Execute(null);

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
    }
}
