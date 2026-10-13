using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

/// <summary>
/// Справочник упражнений: добавление, правка, удаление, сортировка и фильтрация.
/// </summary>
public sealed partial class ExercisesViewModel : ObservableObject
{
    private readonly IExerciseRepository _repository;
    private readonly IWindowService _windowService;

    /// <summary>Весь справочник. Источник истины для проверки дубликатов.</summary>
    private readonly ObservableCollection<Exercise> _all = [];

    public ExercisesViewModel(IExerciseRepository repository, IWindowService windowService)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(windowService);

        _repository = repository;
        _windowService = windowService;
    }

    /// <summary>Упражнения, видимые в списке: отфильтрованные и отсортированные.</summary>
    public ObservableCollection<Exercise> Visible { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _newName = string.Empty;

    /// <summary>Пустая строка означает, что показан весь справочник.</summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary><c>false</c> — по возрастанию.</summary>
    [ObservableProperty]
    private bool _isDescending;

    /// <summary>Показывается в заголовке колонки.</summary>
    public string SortIndicator => IsDescending ? "▼" : "▲";

    /// <summary>Признак, что справочник загружен.</summary>
    public bool IsEmpty => _all.Count == 0;

    /// <summary>Сколько упражнений в справочнике: нужно для текста сообщения о выгрузке.</summary>
    public int Count => _all.Count;

    /// <summary>Выгружать нечего, пока справочник пуст.</summary>
    public bool CanExport => _all.Count > 0;

    /// <summary>Записался ли файл последней выгрузки.</summary>
    /// <remarks>
    /// Уведомления не поднимается: окно читает значение сразу после выполнения команды и
    /// показывает результат сообщением, подписки тут не нужно.
    /// </remarks>
    public bool ExportSucceeded { get; private set; }

    partial void OnFilterTextChanged(string value) => RebuildVisible();

    partial void OnIsDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortIndicator));
        RefreshEmptyState();
        RebuildVisible();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var exercises = await _repository.GetAllAsync().ConfigureAwait(true);

        _all.Clear();
        foreach (var exercise in exercises)
        {
            _all.Add(exercise);
        }

        RefreshEmptyState();
        RebuildVisible();
    }

    /// <summary>
    /// Кнопка «Добавить» активна, только если поле не пустое и такого упражнения ещё нет.
    /// </summary>
    /// <remarks>
    /// Дубликат ищется по всему справочнику, а не по видимым строкам: при активном фильтре
    /// иначе можно было бы добавить «жим лёжа», когда он в базе уже есть, просто отфильтрован.
    /// </remarks>
    private bool CanAdd() => NameRules.IsValid(NewName, _all.Select(exercise => exercise.Name));

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var exercise = new Exercise { Name = NewName };

        var outcome = await _repository.AddAsync(exercise).ConfigureAwait(true);

        if (outcome != AddExerciseOutcome.Added)
        {
            return;
        }

        _all.Add(exercise);
        NewName = string.Empty;
        RefreshEmptyState();
        RebuildVisible();
    }

    [RelayCommand]
    private void ToggleSort() => IsDescending = !IsDescending;

    /// <summary>
    /// Открывает окно импорта и перечитывает справочник, если упражнения записаны.
    /// </summary>
    /// <remarks>
    /// Перечитывание обязательно: окно импорта пишет в базу мимо этой модели, и её собственные
    /// объекты после замены справочника устарели — без перечитывания список остался бы прежним,
    /// и правка выглядела бы несработавшей.
    /// </remarks>
    [RelayCommand]
    private async Task ImportAsync()
    {
        if (!_windowService.ShowImportExercises())
        {
            return;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Открывает модальное окно правки. Объект <paramref name="exercise"/> общий с моделью
    /// представления окна справочника: репозиторий записывает новое название прямо в него,
    /// поэтому после успешной правки достаточно перестроить видимый список.
    /// </summary>
    [RelayCommand]
    private void Edit(Exercise? exercise)
    {
        if (exercise is null)
        {
            return;
        }

        if (_windowService.ShowEditExercise(exercise))
        {
            RebuildVisible();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Exercise? exercise)
    {
        if (exercise is null)
        {
            return;
        }

        if (!await _repository.DeleteAsync(exercise.Id).ConfigureAwait(true))
        {
            return;
        }

        _all.Remove(exercise);
        RefreshEmptyState();
        RebuildVisible();
    }

    /// <summary>
    /// Выгружает весь справочник в файл, по одному упражнению на строку.
    /// </summary>
    /// <remarks>
    /// Список полный и по алфавиту, независимо от строки поиска и направления сортировки: файл
    /// должен отражать справочник, а не то, что сейчас показано на экране, иначе одна и та же
    /// выгрузка давала бы разные файлы. Порядок тот же, что у сортировки в списке, поэтому два
    /// выгруженных файла совпадают между собой.
    ///
    /// Путь приходит от окна — диалог сохранения открывается там, и привязать команду к кнопке
    /// нельзя: у кнопки с обработчиком `Click` команда выполнилась бы с пустым параметром.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export(string? path)
    {
        // Проверка повторяется в теле команды, а не только в предикате: предикат отвечает за
        // кнопку, а Execute его не спрашивает (ловушка 46).
        if (string.IsNullOrWhiteSpace(path) || _all.Count == 0)
        {
            ExportSucceeded = false;

            return;
        }

        ExportSucceeded = ExerciseFileWriter.Write(path, NamesForExport());
    }

    private IEnumerable<string> NamesForExport() =>
        _all.Select(exercise => exercise.Name).Order(StringComparer.Ordinal);

    /// <summary>
    /// Признаки пустоты справочника вычисляются, поэтому уведомляются оба: и надпись «Справочник
    /// пуст», и доступность кнопки выгрузки. Команде нужен отдельный вызов — она переспрашивает
    /// предикат сама и по уведомлению свойства не смотрит.
    /// </summary>
    private void RefreshEmptyState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanExport));
        ExportCommand.NotifyCanExecuteChanged();
    }

    private void RebuildVisible()
    {
        var filter = FilterText?.Trim();

        var query = _all.AsEnumerable();

        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(exercise => exercise.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        // Ordinal, а не InvariantCulture: сравнение кодов Unicode совпадает с тем, что делает
        // база при ORDER BY, и даёт русский алфавит, поскольку блок кириллицы отсортирован.
        query = IsDescending
            ? query.OrderByDescending(exercise => exercise.Name, StringComparer.Ordinal)
            : query.OrderBy(exercise => exercise.Name, StringComparer.Ordinal);

        Visible.Clear();

        foreach (var exercise in query)
        {
            Visible.Add(exercise);
        }
    }
}
