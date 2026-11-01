using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.Core.Settings;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

/// <summary>Главное окно: сколько дней показывать, сами дни и добавление нового дня.</summary>
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

        // День записан открытым окном дня — журнал перечитывается, независимо от того, закрыто
        // после этого окно или осталось открытым: оно немодальное.
        _windowService.DaySaved += OnDaySaved;

        // Из файла, а не из константы: настройка живёт рядом с программой.
        DaysCount = settingsStore.Load().DaysCount;
    }

    /// <summary>День записан открытым окном: журнал перечитывается.</summary>
    /// <remarks>
    /// Возвращается Task, а не async void: сервис раздаёт задания подписчика из события, и тест
    /// таким образом дожидается перечитывания await'ом.
    /// </remarks>
    private Task OnDaySaved() => RefreshAsync();

    [ObservableProperty]
    private string _title = "Журнал тренировок";

    [RelayCommand]
    private void OpenExercises() => _windowService.OpenExercises();

    [RelayCommand]
    private void OpenPlans() => _windowService.OpenPlans();

    /// <summary>
    /// Параметры из окна настроек: поле и показанные дни обновляются вместе.
    /// </summary>
    /// <remarks>
    /// Файл перечитывается, а не значение берётся у окна: источник истины один — на диске.
    ///
    /// Список перечитывается сам, в отличие от числа, введённого в это же поле здесь: там
    /// «Применить» нужен, чтобы поле не перечитывало журнал на каждый символ, а сохранённое
    /// число уже применено, и оставить список прежним значило бы показать результат настройки и
    /// скрыть его.
    ///
    /// Место базы докладывается событием только тогда, когда оно изменилось: соединение
    /// перестраивается при старте, и прочитанное окном сегодня вложить в него нельзя. Сообщение
    /// на каждое открытие окна параметров мешало бы своим постоянством.
    /// </remarks>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var addressBefore = _settingsStore.Load().DatabasePath;

        if (!_windowService.ShowSettings())
        {
            return;
        }

        var settings = _settingsStore.Load();

        DaysCount = settings.DaysCount;

        if (settings.DatabasePath != addressBefore)
        {
            DatabaseAddressChanged?.Invoke(
                "Место базы данных изменено. Подключение к новой базе произойдёт при следующем запуске.");
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Сколько дней показать. Применяется кнопкой «Применить», иначе список перечитывался бы на
    /// каждый введённый символ.
    /// </summary>
    /// <remarks>
    /// В файл настроек «Применить» не пишет: настройкой владеет окно параметров, а второе место,
    /// откуда её можно изменить, рано или поздно разошлось бы с первым.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _daysCount;

    /// <summary>Подсказка об отказе по вводу.</summary>
    /// <remarks>
    /// Отказов о сохранении в главном окне нет, поэтому состояние целиком уходит в подсказку.
    /// </remarks>
    public HintState Hint { get; } = new();

    /// <summary>Место базы изменилось: окно показывает примечание рядом с кнопкой параметров.</summary>
    /// <remarks>
    /// Показ зовёт окно, а не модель: подсказке нужен якорь, и его знает только окно — так же, как
    /// примечание упражнения показывает обработчик кнопки «Показать». Модель решает, случилось ли
    /// изменение и каким текстом об этом сказать.
    /// </remarks>
    public event Action<string>? DatabaseAddressChanged;

    /// <summary>Дни, показанные в окне: самый свежий сверху.</summary>
    public ObservableCollection<DayRowViewModel> Days { get; } = [];

    public bool IsEmpty => Days.Count == 0;

    /// <summary>Показывает последние записи, не более <paramref name="count"/>.</summary>
    /// <remarks>
    /// Считает и сортирует сама, хотя хранилище уже отдаёт записи по дате: правило «показать N
    /// последних» проверяется тестом, а не тем, что сегодня так вернули из базы.
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

    /// <summary>Открывает окно добавления дня: немодальное, при открытом — выводит существующее вперёд.</summary>
    /// <remarks>
    /// Перечитывание журнала после сохранения живёт в <see cref="OnDaySaved"/>, а не здесь: окно
    /// может сохранить день и остаться открытым, поэтому ответа «диалог закрылся» больше нет.
    /// </remarks>
    [RelayCommand]
    private void AddDay() => _windowService.ShowAddDay();

    /// <summary>Правка дня из строки журнала: дабл-клик по строке или пункт меню.</summary>
    /// <remarks>
    /// Строка передаётся целиком, а не датой: и меню, и дабл-клик обязаны попасть в одну команду,
    /// иначе правка дня была бы в двух местах сразу.
    ///
    /// Без строки команда не открывает ничего: правки несуществующего дня не бывает, а падать на
    /// <c>null</c> пользователю не за что.
    ///
    /// При открытом окне дня попытка править другой день не меняет открытую дату: существующее
    /// окно выводится вперёд с оповещением, а решение, какой день править, остаётся за датой в нём.
    /// </remarks>
    /// <param name="day">Строка журнала, день которой открывается на правку.</param>
    [RelayCommand]
    private void EditDay(DayRowViewModel? day)
    {
        if (day is null)
        {
            return;
        }

        _windowService.ShowEditDay(day.Date);
    }

    /// <summary>
    /// Перечитывает журнал и пересобирает строки дней.
    /// </summary>
    /// <remarks>
    /// Читаются только показанные дни, а не вся история: отбор «последние N» идёт в базе, иначе
    /// каждое применение тянуло бы упражнения и подходы за все годы. <see cref="TakeLatest"/>
    /// остаётся на месте как правило показа, не зависящее от того, что сегодня вернула база.
    ///
    /// Коллекция пересобирается целиком, а не приводится к порядку через <c>Move</c>: там у строк
    /// есть состояние контейнера — раскрытый план, — а здесь строка дня состоит из текста и
    /// чисел и меняется целиком при смене количества дней.
    /// </remarks>
    private async Task RefreshAsync()
    {
        var sessions = await _sessionRepository.GetLatestAsync(DaysCount).ConfigureAwait(true);

        Days.Clear();

        foreach (var session in TakeLatest(sessions, DaysCount))
        {
            Days.Add(DayRowViewModel.Create(session));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
