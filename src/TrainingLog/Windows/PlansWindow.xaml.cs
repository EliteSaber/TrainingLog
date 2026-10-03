using System.Windows;
using System.Windows.Controls;
using TrainingLog.Core.Models;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Interaction logic for PlansWindow.xaml
/// </summary>
public partial class PlansWindow : Window
{
    public PlansWindow(PlansViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is PlansViewModel viewModel)
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
        if (sender is not Button { CommandParameter: TrainingPlan plan }
            || DataContext is not PlansViewModel viewModel)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Удалить план «{plan.Name}»?",
            "Удаление плана",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer == MessageBoxResult.Yes && viewModel.DeleteCommand.CanExecute(plan))
        {
            await viewModel.DeleteCommand.ExecuteAsync(plan);
        }
    }
}
