using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.Core.Settings;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

/// <summary>
/// Главное окно: сколько дней показывать, сами дни и добавление нового дня.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITrainingSessionRepository _sessionRepository;
    private readonly IWindowService _windowService;
    private readonly AppSettingsStore _settingsStore;

    /// <param name="windowService">Список окон приложения.</param>
    /// <param name="sessionRepository">Журнал тренировок.</param>
    /// <param name="settingsStore">Файл настроек: сколько дней показывать.</param>
    public MainViewModel(
        IWindowService windowService,
        ITrainingSessionRepository sessionRepository,
        AppSettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(sessionRepository);
        ArgumentNullException.ThrowIfNull(settingsStore);

        _windowService = windowService;
        _sessionRepository = sessionRepository;
        _settingsStore = settingsStore;

        // Количество дней приходит из файла настроек, а не из константы: это настройка, и её
        // место — рядом с программой, а не в коде. Файл к этому моменту уже создан хранилищем
        // либо прочитан, так что поля не пустует даже первого запуска.
        DaysCount = settingsStore.Load().DaysCount;
    }

    [ObservableProperty]
    private string _title = "Журнал тренировок";

    [RelayCommand]
    private void OpenExercises() => _windowService.OpenExercises();

    [RelayCommand]
    private void OpenPlans() => _windowService.OpenPlans();

    /// <summary>
    /// Параметры из окна настроек: поле обновляется сразу, и показанные дни перечитываются
    /// вместе с ним.
    /// </summary>
    /// <remarks>
    /// Файл перечитывается, а не значение передаётся из окна: единственный источник истины —
    /// настройка на диске, и главное окно не должно узнавать о ней иным способом, даже если
    /// сегодня это одно и то же число.
    ///
    /// Список дней перечитывается сам, в отличие от значения, введённого в это же поле на
    /// главном окне: там кнопка «Применить» существует ради того, чтобы поле не перечитывало
    /// журнал на каждый введённый символ, а здесь число уже применено — оно записано в файл и
    /// пользователь его отпустил. Оставив список прежним, окно параметров показало бы сохранённое
    /// значение и не показало бы его результата, и «сохранить» выглядело бы не сработавшим.
    ///
    /// Кнопка «Применить» при этом остаётся: она по-прежнему применяет число, введённое в
    /// поле на главном окне, и по-прежнему в файл не пишет.
    /// </remarks>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (!_windowService.ShowSettings())
        {
            return;
        }

        DaysCount = _settingsStore.Load().DaysCount;

        await RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Сколько дней показать. Значение применяется кнопкой «Применить»: пока она не
    /// нажата, показанные дни не меняются, иначе список перечитывался бы на каждый
    /// введённый символ.
    /// </summary>
    /// <remarks>
    /// В файл настроек «Применить» не пишет: настройкой владеет окно параметров, и второе
    /// место, откуда её можно изменить, рано или поздно разошлось бы с первым.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _daysCount;

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

    private bool CanApply() => DaysCountRules.IsValid(DaysCount);

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
    /// Правка дня из строки журнала: дабл-клик по строке или пункт «Редактировать» меню.
    /// </summary>
    /// <remarks>
    /// Строка передаётся целиком, а не датой: её кладёт в меню и дабл-клик, и оба обязаны
    /// попасть в одну команду, — иначе правка дня была бы в двух местах сразу.
    ///
    /// Пустая строка возможна, если команда вызвана без параметра, и тогда она ничего не
    /// открывает: правки несуществующего дня не бывает, а падать на <c>null</c> пользователю
    /// не за что.
    /// </remarks>
    /// <param name="day">Строка журнала, день которой открывается на правку.</param>
    [RelayCommand]
    private async Task EditDayAsync(DayRowViewModel? day)
    {
        if (day is null)
        {
            return;
        }

        if (_windowService.ShowEditDay(day.Date))
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