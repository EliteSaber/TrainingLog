using System.ComponentModel;
using System.Windows;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Окно добавления и правки плана. Одно на оба случая: различаются только набор планов
/// для проверки дубля и команда сохранения.
/// </summary>
public partial class EditPlanWindow : Window
{
    private readonly EditPlanViewModel _viewModel;

    public EditPlanWindow(EditPlanViewModel viewModel)
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
    /// Кнопка подтверждения привязана к команде, поэтому WPF сама снимает активность кнопки,
    /// когда правки нет. А вот результат сохранения известен только здесь: по одному значению
    /// <see cref="EditPlanViewModel.Accepted"/> вызывающий <c>ShowDialog</c> поймёт, что план
    /// сохранён. Поэтому <see cref="DialogResult"/> выставляется по уведомлению.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditPlanViewModel.Accepted) && _viewModel.Accepted)
        {
            DialogResult = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
