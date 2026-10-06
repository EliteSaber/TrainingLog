using System.ComponentModel;
using System.Windows;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Окно добавления дня тренировки. Одно и на добавление, и на правку: запись за дату одна,
/// и модель сама определяет, есть она в базе или нет.
/// </summary>
public partial class AddDayWindow : Window
{
    private readonly AddDayViewModel _viewModel;

    public AddDayWindow(AddDayViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Было ли окно закрыто кнопкой сохранения, а не крестиком или Esc.
    /// </summary>
    /// <remarks>
    /// Отдельное от <see cref="Saved"/> состояние, и различие важно вызывающему: «Сохранить»
    /// не закрывает окно, поэтому <see cref="DialogResult"/> после него остаётся <c>null</c>,
    /// и по одному значению диалога перечитывать журнал было бы не на чем. Сохранён ли день —
    /// спросим у модели в момент закрытия.
    /// </remarks>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Было ли что-то сохранено за время работы окна, включая сохранение без закрытия.
    /// </summary>
    public bool Saved { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // Фокус в конструкторе ставить нельзя: у ещё не показанного окна нет в цепочке
        // активации, и Focus() отработает вхолостую. Первым фокусируется поле даты, дальше
        // всё работает клавишей Tab.
        _viewModel.InitializeCommand.Execute(null);
    }

    /// <summary>
    /// Кнопки сохранения привязаны к командам, поэтому WPF сама снимает их активность, когда
    /// день сохранять нечем. А вот результат известен только здесь: по одному значению
    /// <see cref="AddDayViewModel.Accepted"/> вызывающий <c>ShowDialog</c> поймёт, что день
    /// сохранён и окно надо закрывать. Поэтому <see cref="DialogResult"/> выставляется по
    /// уведомлению.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddDayViewModel.Accepted) && _viewModel.Accepted)
        {
            Accepted = true;
            DialogResult = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        Saved = _viewModel.Saved;
    }
}