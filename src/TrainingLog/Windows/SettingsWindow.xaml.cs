using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TrainingLog.Controls;
using TrainingLog.Core.Settings;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>Окно параметров: число дней и адрес файла базы данных.</summary>
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

    /// <summary>
    /// Диалоги адреса базы живут здесь, а не в модели: это работа интерфейса, как и
    /// <c>MessageBox</c>. Стартовый каталог диалогов — текущее поле, иначе каждый выбор заново
    /// начинался бы с «Документов» среди незнакомых дисков.
    /// </summary>
    private void OnPickDirectory(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = _viewModel.DatabaseDirectory,
        };

        if (dialog.ShowDialog(this) is true)
        {
            _viewModel.DatabaseDirectory = dialog.FolderName;
        }
    }

    /// <summary>
    /// Файл отвечает на оба поля: директория берётся у выбранного файла, наименование — его имя.
    /// Раздельная подстановка полей вместо целого пути: окончание диска и каталога в поле адреса
    /// разъехалось бы с наименованием при следующей правке одного из них.
    /// </summary>
    /// <remarks>
    /// Наименование переводится тем же правилом, что и в модели: в поле имя живёт без хвостового
    /// <c>.db</c>, а расширение дописывается при сохранении всегда. Поэтому файл с чужим
    /// расширением (например <c>старая.sqlite</c>) возвращается в настройках как
    /// <c>старая.db</c> — единственного расширения у файла базы по договору нет и второго не будет.
    /// </remarks>
    private void OnPickDatabaseFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            InitialDirectory = _viewModel.DatabaseDirectory,
            FileName = _viewModel.DatabaseName,
            Filter = "База данных (*.db)|*.db|Все файлы (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) is true)
        {
            _viewModel.DatabaseDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            _viewModel.DatabaseName = DatabaseNameRules.ForDisplay(Path.GetFileName(dialog.FileName));
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
    }
}
