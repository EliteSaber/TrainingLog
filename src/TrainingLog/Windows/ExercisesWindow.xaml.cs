using System.Windows;
using System.Windows.Controls;
using TrainingLog.Core.Models;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Interaction logic for ExercisesWindow.xaml
/// </summary>
public partial class ExercisesWindow : Window
{
    public ExercisesWindow(ExercisesViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is ExercisesViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }

    /// <summary>
    /// Удаление необратимо, поэтому спрашиваем подтверждение здесь, а не в модели представления:
    /// <see cref="MessageBox"/> — работа с интерфейсом, и в представлении ей не место.
    /// </summary>
    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: Exercise exercise }
            || DataContext is not ExercisesViewModel viewModel)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Удалить упражнение «{exercise.Name}» из справочника?",
            "Удаление упражнения",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer == MessageBoxResult.Yes && viewModel.DeleteCommand.CanExecute(exercise))
        {
            await viewModel.DeleteCommand.ExecuteAsync(exercise);
        }
    }
}
