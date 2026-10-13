using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

public partial class ImportExercisesWindow : Window
{
    private readonly ImportExercisesViewModel _viewModel;

    public ImportExercisesWindow(ImportExercisesViewModel viewModel)
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

        _viewModel.InitializeCommand.Execute(null);
    }

    /// <summary>
    /// Диалог выбора файла живёт здесь, а не в модели представления: работа с интерфейсом в
    /// представлении, а модель получает уже готовый путь.
    /// </summary>
    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Файл со списком упражнений",
            Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.LoadFile(dialog.FileName);
        }
    }

    /// <summary>
    /// Курсор показывает, примет ли окно файл: иначе перетаскивание выглядит как отказ после
    /// самого броска, а не по курсору под указателем.
    /// </summary>
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];

        if (paths is not { Length: > 0 } || !File.Exists(paths[0]))
        {
            _viewModel.ShowRefusal("В окно можно перетащить только файл со списком упражнений.");
            return;
        }

        _viewModel.LoadFile(paths[0]);
    }

    /// <summary>
    /// Очистка справочника необратима, поэтому подтверждение здесь, а не в модели представления:
    /// <see cref="MessageBox"/> — работа с интерфейсом, и в представлении ей не место.
    /// </summary>
    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ClearExisting && !ConfirmClear())
        {
            return;
        }

        if (_viewModel.SaveCommand.CanExecute(null))
        {
            await _viewModel.SaveCommand.ExecuteAsync(null);
        }
    }

    private bool ConfirmClear()
    {
        var answer = MessageBox.Show(
            this,
            "Все упражнения справочника будут удалены, а вместо них запишутся показанные в списке. "
                + "Из планов удалённые упражнения тоже исчезнут, а в журнале тренировок останутся только названия. Продолжить?",
            "Очистка справочника",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return answer == MessageBoxResult.Yes;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImportExercisesViewModel.Accepted) && _viewModel.Accepted)
        {
            DialogResult = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Папка под указателем файлом не является, и бросать её в модель бессмысленно: отказ должен
    /// быть виден сразу, а не после попытки прочитать.
    /// </summary>
    private static bool IsFile(IDataObject data)
    {
        var paths = data.GetDataPresent(DataFormats.FileDrop) ? data.GetData(DataFormats.FileDrop) as string[] : null;

        return paths is { Length: > 0 } && File.Exists(paths[0]);
    }
}
