using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

/// <summary>
/// Главное окно: сколько дней показывать, сами дни и добавление нового дня.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>Сколько дней показывать по умолчанию.</summary>
    public const int DefaultDaysCount = 3;

    /// <summary>
    /// Больше дней показывать незачем: журнал показывают по одной записи в строке, и список
    /// длиннее года на экране всё равно не помещается.
    /// </summary>
    public const int MaxDaysCount = 366;

    private readonly ITrainingSessionRepository _sessionRepository;
    private readonly IWindowService _windowService;

    /// <param name="windowService">Список окон приложения.</param>
    /// <param name="sessionRepository">Журнал тренировок.</param>
    public MainViewModel(IWindowService windowService, ITrainingSessionRepository sessionRepository)
    {
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(sessionRepository);

        _windowService = windowService;
        _sessionRepository = sessionRepository;
    }

    [ObservableProperty]
    private string _title = "Журнал тренировок";

    [RelayCommand]
    private void OpenExercises() => _windowService.OpenExercises();

    [RelayCommand]
    private void OpenPlans() => _windowService.OpenPlans();

    /// <summary>
    /// Сколько дней показать. Значение применяется кнопкой «Применить»: пока она не
    /// нажата, показанные дни не меняются, иначе список перечитывался бы на каждый
    /// введённый символ.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _daysCount = DefaultDaysCount;

    /// <summary>
    /// Подсказка об отказе по вводу в поле количества дней.
    /// </summary>
    /// <remarks>
    /// Отказов о сохранении в главном окне нет, так что состояние целиком уходит в подсказку.
    /// <see cref="HintState"/> общий с окном дня: правило, что показ и текст — разные состояния,
    /// не должно повторяться в каждой модели представления.
    /// </remarks>
    public HintState Hint { get; } = new();

    /// <summary>Дни, показанные в окне: самый свежий сверху.</summary>
    public ObservableCollection<DayRowViewModel> Days { get; } = [];

    /// <summary>Признак, что показывать нечего.</summary>
    public bool IsEmpty => Days.Count == 0;

    /// <summary>
    /// Показывает последние записи, не более <see cref="DaysCount"/>.
    /// </summary>
    /// <remarks>
    /// Считает нужное количество и сортирует сама, хотя хранилище уже отдаёт записи по дате
    /// по убыванию: правило «показать N последних» проверяется тестом, а не тем, что
    /// сегодня так вернули из базы.
    /// </remarks>
    /// <param name="sessions">Записи журнала.</param>
    /// <param name="count">Сколько последних записей показать.</param>
    public static IReadOnlyList<TrainingSession> TakeLatest(IReadOnlyList<TrainingSession> sessions, int count) =>
        sessions
            .OrderByDescending(session => session.Date)
            .Take(Math.Max(0, count))
            .ToList();

    [RelayCommand]
    private Task LoadAsync() => RefreshAsync();

    private bool CanApply() => DaysCount > 0 && DaysCount <= MaxDaysCount;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyAsync() => RefreshAsync();

    /// <summary>
    /// День добавлен или поправлен — список перечитывается: запись могла добавиться на
    /// любую из показанных дат.
    /// </summary>
    [RelayCommand]
    private async Task AddDayAsync()
    {
        if (_windowService.ShowAddDay())
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Перечитывает журнал и пересобирает строки дней.
    /// </summary>
    /// <remarks>
    /// Коллекция пересобирается целиком, а не приводится к порядку через <c>Move</c>, как
    /// в списке планов: там у строк есть состояние контейнера — раскрытый план, — и его
    /// надо выживать. Здесь строка дня состоит из текста и чисел, состояния у неё нет, а
    /// строки меняются целиком при смене количества дней.
    /// </remarks>
    private async Task RefreshAsync()
    {
        var sessions = await _sessionRepository.GetAsync().ConfigureAwait(true);

        Days.Clear();

        foreach (var session in TakeLatest(sessions, DaysCount))
        {
            Days.Add(DayRowViewModel.Create(session));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}