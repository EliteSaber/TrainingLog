using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TrainingLog.Core;
using TrainingLog.Core.Models;
using TrainingLog.Services;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

public partial class ExercisesWindow : Window
{
    private readonly HotkeyBinder _binder;

    public ExercisesWindow(ExercisesViewModel viewModel, HotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(hotkeys);

        InitializeComponent();
        DataContext = viewModel;

        // Строки справочника работают через выделение: без него клавиши правки и удаления молчат.
        _binder = new HotkeyBinder(this, hotkeys)
            .Row("Exercises.Edit", ExerciseList, row =>
            {
                if (row is Exercise exercise)
                {
                    Run(viewModel.EditCommand, exercise);
                }
            })
            .Row("Exercises.Delete", ExerciseList, row =>
            {
                if (row is Exercise exercise)
                {
                    ConfirmDelete(exercise);
                }
            });

        _binder.Rebuild();

        Loaded += OnLoaded;
    }

    /// <summary>Выполняет команду, если её CanExecute разрешает: RelayCommand.Execute проверку
    /// CanExecute не делает, и командное поведение кнопки обязано сохраниться.</summary>
    private static void Run(ICommand command, object? parameter)
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    /// <summary>Удаление упражнения с подтверждением: как клику по кнопке из строки.</summary>
    private async void ConfirmDelete(Exercise exercise)
    {
        if (DataContext is not ExercisesViewModel viewModel)
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
        if (sender is Button { CommandParameter: Exercise exercise })
        {
            ConfirmDelete(exercise);
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
