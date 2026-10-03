using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

/// <summary>
/// Список планов тренировок: добавление, правка, удаление, сортировка и поиск.
/// </summary>
/// <remarks>
/// Состав упражнений каждого плана приезжает вместе с планом и раскрывается прямо в строке,
/// поэтому отдельного окна со списком упражнений нет.
/// </remarks>
public sealed partial class PlansViewModel : ObservableObject
{
    private readonly ITrainingPlanRepository _repository;
    private readonly IWindowService _windowService;

    /// <summary>Все планы. Источник истины для показа в списке.</summary>
    private readonly ObservableCollection<TrainingPlan> _all = [];

    public PlansViewModel(ITrainingPlanRepository repository, IWindowService windowService)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(windowService);

        _repository = repository;
        _windowService = windowService;
    }

    /// <summary>Планы, видимые в списке: отфильтрованные и отсортированные.</summary>
    public ObservableCollection<TrainingPlan> Visible { get; } = [];

    /// <summary>Строка поиска. Пустая строка означает, что показаны все планы.</summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>Направление сортировки: <c>false</c> — по возрастанию.</summary>
    [ObservableProperty]
    private bool _isDescending;

    /// <summary>Индикатор направления сортировки в заголовке колонки.</summary>
    public string SortIndicator => IsDescending ? "▼" : "▲";

    /// <summary>Признак, что планы загружены и их нет ни одного.</summary>
    public bool IsEmpty => _all.Count == 0;

    partial void OnFilterTextChanged(string value) => RebuildVisible();

    partial void OnIsDescendingChanged(bool value)
    {
        OnPropertyChanged(nameof(SortIndicator));
        RebuildVisible();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var plans = await _repository.GetAllAsync().ConfigureAwait(true);

        _all.Clear();
        foreach (var plan in plans)
        {
            _all.Add(plan);
        }

        OnPropertyChanged(nameof(IsEmpty));
        RebuildVisible();
    }

    [RelayCommand]
    private void ToggleSort() => IsDescending = !IsDescending;

    /// <summary>
    /// Добавление открывает то же окно, что и правка: состав упражнений выбирается в нём,
    /// и отдельного поля для наименования в списке нет.
    /// </summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        if (_windowService.ShowAddPlan())
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Открывает модальное окно правки. После сохранения список перечитывается целиком:
    /// изменился не только наименование, но и состав упражнений, а он у плана отдельным
    /// объектом, и подменять его в строке рискованнее, чем перечитать.
    /// </summary>
    [RelayCommand]
    private async Task EditAsync(TrainingPlan? plan)
    {
        if (plan is null)
        {
            return;
        }

        if (_windowService.ShowEditPlan(plan))
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(TrainingPlan? plan)
    {
        if (plan is null)
        {
            return;
        }

        if (!await _repository.DeleteAsync(plan.Id).ConfigureAwait(true))
        {
            return;
        }

        _all.Remove(plan);

        OnPropertyChanged(nameof(IsEmpty));
        RebuildVisible();
    }

    private void RebuildVisible()
    {
        var filter = FilterText?.Trim();

        var query = _all.AsEnumerable();

        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(plan => plan.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        // Ordinal, а не InvariantCulture: сравнение кодов Unicode совпадает с тем, что делает
        // база при ORDER BY, и даёт русский алфавит, поскольку блок кириллицы отсортирован.
        query = IsDescending
            ? query.OrderByDescending(plan => plan.Name, StringComparer.Ordinal)
            : query.OrderBy(plan => plan.Name, StringComparer.Ordinal);

        Visible.Clear();

        foreach (var plan in query)
        {
            Visible.Add(plan);
        }
    }
}
