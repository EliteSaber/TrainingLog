using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core;
using TrainingLog.Core.Repositories;

namespace TrainingLog.ViewModels;

/// <summary>
/// Импорт упражнений из текстового файла в справочник, по одному упражнению на строку.
/// </summary>
public sealed partial class ImportExercisesViewModel : ObservableObject
{
    private readonly IExerciseRepository _repository;

    /// <summary>
    /// Названия текущего справочника. Нужны для отсечения того, что в него уже есть: проверка
    /// идёт по памяти, а не запросом к базе на каждый символ в поле.
    /// </summary>
    private IReadOnlyList<string> _existingNames = [];

    public ImportExercisesViewModel(IExerciseRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;

        Summary = "Файл можно перетащить в окно или выбрать кнопкой. Упражнения читаются по одному в строке.";
    }

    /// <summary>
    /// Список упражнений по одному в строке. Правка поля проверяется повторно при сохранении.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _text = string.Empty;

    /// <summary>Пусто, пока файл не выбран. Показывается в поле пути.</summary>
    [ObservableProperty]
    private string _sourcePath = string.Empty;

    /// <summary>
    /// Справочник станет ровно тем, что в поле. Подтверждение очистки спрашивает окно: оно и
    /// работает с интерфейсом.
    /// </summary>
    [ObservableProperty]
    private bool _clearExisting;

    /// <summary>
    /// Объяснение отказа, а при пустом значении — ничего.
    /// </summary>
    /// <remarks>
    /// Живёт до успеха, а не пять секунд в подсказке: файл не прочитан или упражнения не записаны,
    /// и объяснение обязано дождаться, пока им займутся.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    /// <summary>Подсказка о загруженном файле: сколько строк прочитано и сколько попало в поле.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    private string _summary = string.Empty;

    public bool HasSummary => !string.IsNullOrEmpty(Summary);

    /// <summary>Упражнения записаны. Окно выставляет <c>DialogResult</c> по нему.</summary>
    public bool Accepted { get; private set; }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        var all = await _repository.GetAllAsync().ConfigureAwait(true);

        _existingNames = all.Select(exercise => exercise.Name).ToList();
    }

    /// <summary>
    /// Загружает файл в поле. Отказ — это <see cref="Status"/>, а не исключение наружу: файл
    /// выбирает и перетаскивает человек, и оба пути обязаны вести себя одинаково.
    /// </summary>
    public void LoadFile(string path)
    {
        IReadOnlyList<string> lines;

        try
        {
            lines = ExerciseFileReader.ReadLines(path);
        }
        catch (NotSupportedException exception)
        {
            // Текст написан здесь, в проекте: сообщение среды о неизвестной кодировке человек не поймёт.
            ShowRefusal(exception.Message);
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowRefusal($"Не удалось прочитать файл «{Path.GetFileName(path)}»: нет прав на чтение, файл занят другой программой или это не файл.");
            return;
        }

        var accepted = ExerciseImportRules.Read(lines);

        Text = string.Join(Environment.NewLine, accepted);
        SourcePath = path;
        Status = string.Empty;

        Summary = accepted.Count == lines.Count
            ? $"Прочитано строк: {lines.Count}. Все строки приняты."
            : $"Прочитано строк: {lines.Count}, в поле: {accepted.Count}. Отброшены пустые, длиннее {NameRules.MaxNameLength} и повторяющиеся.";
    }

    /// <summary>Отказ, о котором окно знает само: перетащили не то.</summary>
    public void ShowRefusal(string message) => Status = message;

    /// <summary>Есть чем наполнить справочник: пустое поле и одни отбракованные строки его не гасят.</summary>
    public bool CanSave => PrepareNames().Count > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        // Проверка повторяется в теле команды, а не только в предикате: предикат отвечает за кнопку,
        // а Execute его не спрашивает (ловушка 46).
        var names = PrepareNames();

        if (names.Count == 0)
        {
            return;
        }

        var outcome = await _repository.ImportAsync(names, ClearExisting).ConfigureAwait(true);

        if (outcome != ImportExercisesOutcome.Imported)
        {
            Status = "Не удалось записать упражнения: одно из названий уже занято в справочнике. Обновите окно упражнений и повторите.";
            return;
        }

        Status = string.Empty;
        Accepted = true;

        // Приватный сеттер не даёт сгенерировать уведомление автоматически, а окно ждёт его,
        // чтобы выставить DialogResult.
        OnPropertyChanged(nameof(Accepted));
    }

    /// <summary>
    /// Разбирает поле тем же правилом, что и файл: пользователь мог править список руками, и
    /// написанное им принимается на тех же условиях.
    /// </summary>
    private IReadOnlyList<string> PrepareNames() =>
        ExerciseImportRules.PrepareForSave(
            ExerciseImportRules.SplitLines(Text),
            _existingNames,
            ClearExisting);
}
