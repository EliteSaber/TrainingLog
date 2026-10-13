using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TrainingLog.Core;
using TrainingLog.Core.Models;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

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

    /// <summary>
    /// Выгрузка справочника: путь выбирает диалог сохранения здесь, а записывает модель
    /// представления. Результат показывается сообщением — оба, и успех, и отказ: отказ записи иначе
    /// остался бы незамеченным, а файл человек ищет по своему выбору пути.
    /// </summary>
    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ExercisesViewModel viewModel)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Файл со списком упражнений",
            Filter = "Текстовые файлы (*.txt)|*.txt",
            FileName = ExerciseFileWriter.DefaultFileName,
            DefaultExt = "txt",
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        viewModel.ExportCommand.Execute(dialog.FileName);

        MessageBox.Show(
            this,
            viewModel.ExportSucceeded
                ? $"Выгружено упражнений: {viewModel.Count}.\n\nФайл: {dialog.FileName}"
                : $"Не удалось записать файл «{dialog.FileName}». Нет прав на запись или папка недоступна.",
            viewModel.ExportSucceeded ? "Выгрузка упражнений" : "Ошибка выгрузки",
            MessageBoxButton.OK,
            viewModel.ExportSucceeded ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
