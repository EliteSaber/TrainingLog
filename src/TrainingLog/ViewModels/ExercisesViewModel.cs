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

    /// <summary>Название нового упражнения.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _newName = string.Empty;

    /// <summary>Строка поиска. Пустая строка означает, что показан весь справочник.</summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>Направление сортировки: <c>false</c> — по возрастанию.</summary>
    [ObservableProperty]
    private bool _isDescending;

    /// <summary>Индикатор направления сортировки в заголовке колонки.</summary>
    public string SortIndicator => IsDescending ? "▼" : "▲";

    /// <summary>Признак, что справочник загружен и в нём нет ни одного упражнения.</summary>
    public bool IsEmpty => _all.Count == 0;

    partial void OnFilterTextChanged(string value) => RebuildVisible();

    partial void OnIsDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortIndicator));
        OnPropertyChanged(nameof(IsEmpty));
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

        OnPropertyChanged(nameof(IsEmpty));
        RebuildVisible();
    }

    /// <summary>
    /// Кнопка «Добавить» активна, только если поле не пустое и такого упражнения ещё нет.
    /// </summary>
    /// <remarks>
    /// Дубликат ищется по всему справочнику, а не по видимым строкам: при активном фильтре
    /// иначе можно было бы добавить «жим лёжа», когда он в базе уже есть, просто отфильтрован.
    /// </remarks>
    private bool CanAdd() => ExerciseNameRules.IsValid(NewName, _all);

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
        OnPropertyChanged(nameof(IsEmpty));
        RebuildVisible();
    }

    [RelayCommand]
    private void ToggleSort() => IsDescending = !IsDescending;

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
        OnPropertyChanged(nameof(IsEmpty));
        RebuildVisible();
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
