using System.ComponentModel;
using System.Windows;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Interaction logic for EditExerciseWindow.xaml
/// </summary>
public partial class EditExerciseWindow : Window
{
    private readonly EditExerciseViewModel _viewModel;

    public EditExerciseWindow(EditExerciseViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // Фокус ставим здесь, а не в конструкторе: у ещё не показанного окна нет в цепочке
        // активации, и Focus() в конструкторе отрабатывает вхолостую.
        NameBox.Focus();
        NameBox.SelectAll();

        _viewModel.InitializeCommand.Execute(null);
    }

    /// <summary>
    /// Кнопка «Принять» привязана к команде, поэтому WPF сама снимает активность кнопки,
    /// когда правки нет. А вот результат сохранения известен только здесь: по одному значению
    /// <see cref="EditExerciseViewModel.Accepted"/> вызывающий <c>ShowDialog</c> поймёт, что
    /// упражнение обновлено. Поэтому <see cref="DialogResult"/> выставляется по уведомлению.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditExerciseViewModel.Accepted) && _viewModel.Accepted)
        {
            DialogResult = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
