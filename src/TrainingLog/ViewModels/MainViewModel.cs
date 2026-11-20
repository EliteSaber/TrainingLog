using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.Core.Settings;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

/// <summary>Главное окно: какие дни показывать, сами дни и добавление нового дня.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITrainingSessionRepository _sessionRepository;
    private readonly IWindowService _windowService;
    private readonly AppSettingsStore _settingsStore;

    /// <summary>
    /// Место базы на момент последней проверки.
    /// </summary>
    /// <remarks>
    /// Хранится, а не читается перед открытием окна параметров: окно немодальное и может
    /// простоять открытым сколько угодно, а сообщение о смене базы нужно только при самой
    /// смене, а не при каждом открытии.
    /// </remarks>
    private string _databasePath = string.Empty;

    /// <summary>Сколько чтений журнала сейчас в пути. Счётчик, а не флаг: источников несколько.</summary>
    /// <remarks>
    /// Перечитывание запускают «Применить», старт окна, сохранение настроек и запись дня из
    /// открытого окна. Флаг сбрасывался бы первым же ответом, пока второй ещё в пути.
    /// </remarks>
    private int _loadingCount;

    /// <summary>Номер последнего запрошенного чтения журнала.</summary>
    /// <remarks>
    /// Пока чтение шло на UI-потоке, два перечитывания не могли пересечься. Теперь запросы идут
    /// в фоне, и ответ более раннего мог бы прийти позже — тогда журнал показал бы данные до
    /// сохранения дня. Поэтому устаревший ответ отбрасывается: список обновляет только последний
    /// запрос, а более ранние не трогают ни строки, ни сводку.
    /// </remarks>
    private int _refreshVersion;

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

        // Настройки записаны открытым окном параметров — поле, список дней и место базы
        // обновляются вместе. Окно немодальное, поэтому ответа «диалог закрылся» больше нет.
        _windowService.SettingsSaved += OnSettingsSaved;

        // Из файла, а не из константы: настройка живёт рядом с программой.
        var settings = settingsStore.Load();

        DaysCount = settings.DaysCount;
        _appliedDaysCount = settings.DaysCount;
        IsFiltersExpanded = !settings.CollapseFiltersOnStartup;
        _databasePath = settings.DatabasePath;
    }

    /// <summary>День записан открытым окном: журнал перечитывается.</summary>
    /// <remarks>
    /// Возвращается Task, а не async void: сервис раздаёт задания подписчика из события, и тест
    /// таким образом дожидается перечитывания await'ом.
    /// </remarks>
    private Task OnDaySaved() => RefreshAsync();

    /// <summary>
    /// Настройки записаны: поле и показанные дни обновляются вместе.
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
    private async Task OnSettingsSaved()
    {
        var settings = _settingsStore.Load();

        DaysCount = settings.DaysCount;

        if (settings.DatabasePath != _databasePath)
        {
            _databasePath = settings.DatabasePath;

            DatabaseAddressChanged?.Invoke(
                "Место базы данных изменено. Подключение к новой базе произойдёт при следующем запуске.");
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    [ObservableProperty]
    private string _title = "Журнал тренировок";

    [RelayCommand]
    private void OpenExercises() => _windowService.OpenExercises();

    [RelayCommand]
    private void OpenPlans() => _windowService.OpenPlans();

    /// <summary>
    /// Открывает окно параметров: немодальное и единственное.
    /// </summary>
    /// <remarks>
    /// Команда ничего не ждёт: окно открывается и живёт рядом с журналом, а о записи настроек
    /// главное окно узнаёт по событию <see cref="IWindowService.SettingsSaved"/>.
    /// </remarks>
    [RelayCommand]
    private void OpenSettings() => _windowService.OpenSettings();

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

    /// <summary>Нижняя граница показа; <c>null</c> — без ограничения снизу.</summary>
    /// <remarks>
    /// <see cref="DateTime"/> ради DatePicker: в запрос уходит DateOnly. Фильтры — состояние
    /// показа, а не настройка, и в файл параметров не пишутся.
    /// </remarks>
    [ObservableProperty]
    private DateTime? _dateFrom;

    /// <summary>Верхняя граница показа; <c>null</c> — без ограничения сверху.</summary>
    [ObservableProperty]
    private DateTime? _dateTo;

    /// <summary>
    /// Показывать все записи в выбранном периоде, не ограничивая их числом.
    /// </summary>
    /// <remarks>
    /// Галочка снимает только ограничение по количеству, но не сам период: «все» рядом с
    /// выбранными датами значит «все в этом периоде», иначе выбор дат терял бы смысл.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCountEnabled))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _showAll;

    /// <summary>Поле количества дней активно, пока не показаны все записи.</summary>
    public bool IsCountEnabled => !ShowAll;

    /// <summary>Блок фильтров развёрнут; свёрнутый показывает только сводку и шеврон.</summary>
    /// <remarks>
    /// Начальное значение задаёт настройка <see cref="AppSettings.CollapseFiltersOnStartup"/>:
    /// по умолчанию журнал открывается свёрнутым. Дальше состояние живёт только в окне.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FiltersToggleText))]
    private bool _isFiltersExpanded;

    /// <summary>Подсказка кнопки-шеврона: действие, которое она выполнит.</summary>
    public string FiltersToggleText => IsFiltersExpanded ? "Свернуть даты" : "Развернуть даты";

    /// <summary>
    /// Применённый фильтр: то, что реально ушло в журнал и пересобрало список.
    /// </summary>
    /// <remarks>
    /// Копия нужна, потому что поля правятся отдельно от «Применить». Сводка читает только эти
    /// значения: иначе после ввода новой даты она показывала бы её, а журнал оставался прежним.
    /// Обновляется в конце успешного <see cref="RefreshAsync"/>, одновременно со списком.
    /// </remarks>
    private DateOnly? _appliedFrom;

    private DateOnly? _appliedTo;

    private bool _appliedShowAll;

    private int _appliedDaysCount;

    /// <summary>
    /// Строка свёрнутого фильтра: применённые даты, число дней в скобках или знак бесконечности.
    /// </summary>
    /// <remarks>
    /// Обе даты пишутся без подписей «С» и «До» — диапазон и так читается как «от и до»;
    /// подпись нужна только у одной границы, иначе непонятно, что именно задано.
    ///
    /// Без дат число выходит из скобок: «Последние 4» — это фраза, а не дата с пометкой.
    /// При «Показать все» без дат видно «Все записи»: «Последние ∞» читалось бы странно.
    /// Негодное число сюда не доходит: оно не применяется, а сводка показывает только применённое.
    /// </remarks>
    public string FilterSummary
    {
        get
        {
            var count = _appliedShowAll
                ? "[∞]"
                : $"[{_appliedDaysCount.ToString(CultureInfo.InvariantCulture)}]";

            return (_appliedFrom, _appliedTo) switch
            {
                ({ } from, { } to) => $"{ShortDate(from)} — {ShortDate(to)} {count}",
                ({ } from, null) => $"С {ShortDate(from)} {count}",
                (null, { } to) => $"До {ShortDate(to)} {count}",
                _ when _appliedShowAll => "Все записи",
                _ => $"Последние {_appliedDaysCount.ToString(CultureInfo.InvariantCulture)}"
            };
        }
    }

    /// <summary>
    /// Дата сводки тем же коротким видом, что и поле: <c>29.10.2026</c>. Формат задан явно,
    /// чтобы сводка не зависела от культуры машины, — как и остальные даты приложения.
    /// </summary>
    private static string ShortDate(DateOnly date) =>
        date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Текст на месте пустого журнала: с активным фильтром пустота означает «ничего не нашлось»,
    /// а не «записей нет вовсе».
    /// </summary>
    [ObservableProperty]
    private string _emptyText = "Записей пока нет";

    /// <summary>Снимает нижнюю границу без перечитывания: фильтр применяется кнопкой.</summary>
    [RelayCommand]
    private void ClearDateFrom() => DateFrom = null;

    /// <summary>Снимает верхнюю границу без перечитывания: фильтр применяется кнопкой.</summary>
    [RelayCommand]
    private void ClearDateTo() => DateTo = null;

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

    /// <summary>
    /// Идёт чтение журнала. Пока истинно, «Применить» показывает кольцо вместо надписи.
    /// </summary>
    /// <remarks>
    /// Считается по <see cref="_loadingCount"/>: читать журнал могут одновременно разные
    /// источники, и индикатор должен держаться до последнего ответа.
    /// </remarks>
    public bool IsBusy => _loadingCount > 0;

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

    private bool CanApply() => !IsBusy && (ShowAll || DaysCountRules.IsValid(DaysCount));

    /// <summary>
    /// Применяет фильтр и сворачивает блок: сводка сразу показывает принятое.
    /// </summary>
    /// <remarks>
    /// Сворачивание идёт после перечитывания, а не по нажатию: до ответа базы сводка показывала
    /// бы новый фильтр рядом со старым списком. При отказе чтения исключение уходит наверх, и
    /// блок остаётся развёрнутым — фильтр не применился.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        await RefreshAsync().ConfigureAwait(true);

        IsFiltersExpanded = false;
    }

    /// <summary>
    /// Сворачивает или разворачивает блок фильтров.
    /// </summary>
    /// <remarks>
    /// Ничего не применяет и не перечитывает: свёрнутая строка показывает последний применённый
    /// фильтр, а не введённый. Иначе сворачивание ждало бы чтения из базы, а сводка временно
    /// показывала бы ещё не применённые значения.
    /// </remarks>
    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

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
    /// Читаются только показанные дни, а не вся история: отбор по датам и «последние N» идут в
    /// базе, иначе каждое применение тянуло бы упражнения и подходы за все годы. Лимит считается
    /// уже внутри периода: при открытой нижней границе «последние N» — это самые свежие дни
    /// периода, а не самые старые. <see cref="TakeLatest"/> остаётся на месте как правило показа,
    /// не зависящее от того, что сегодня вернула база; при «Показать все» его нет вовсе.
    ///
    /// Обратный период меняется местами здесь, а не молча уходит в запрос: с перепутанными
    /// границами база вернула бы пустоту, а поля после применения показывают исправленный
    /// диапазон — то, что человек и имел в виду.
    ///
    /// Коллекция пересобирается целиком, а не приводится к порядку через <c>Move</c>: там у строк
    /// есть состояние контейнера — раскрытый план, — а здесь строка дня состоит из текста и
    /// чисел и меняется целиком при смене количества дней.
    /// </remarks>
    private async Task RefreshAsync()
    {
        var (from, to) = NormalizeRange();
        int? count = ShowAll ? null : DaysCount;

        var version = Interlocked.Increment(ref _refreshVersion);

        BeginLoading();

        try
        {
            // SQLite под EF Core отвечает синхронно: вызов на UI-потоке замораживал бы окно
            // вместе с кольцом загрузки. Фабрика контекста потокобезопасна, репозиторий без
            // состояния, поэтому чтение уходит в фон, а продолжение возвращается в окно.
            var sessions = await Task.Run(() => _sessionRepository.GetAsync(from, to, count))
                .ConfigureAwait(true);

            // Пока читали, журнал мог быть запрошен снова: этот ответ устарел, и обновлять по
            // нему список нельзя — более поздний запрос уже вернул свежие данные.
            if (version != Volatile.Read(ref _refreshVersion))
            {
                return;
            }

            var shown = ShowAll ? sessions : TakeLatest(sessions, DaysCount);

            Days.Clear();

            foreach (var session in shown)
            {
                Days.Add(DayRowViewModel.Create(session));
            }

            EmptyText = from is not null || to is not null
                ? "За выбранный период записей нет"
                : "Записей пока нет";

            // Снимок применённого фильтра — вместе со списком: сводка свёрнутой шапки не должна
            // показывать значения, которые ещё не дошли до журнала.
            _appliedFrom = from;
            _appliedTo = to;
            _appliedShowAll = ShowAll;
            _appliedDaysCount = DaysCount;
            OnPropertyChanged(nameof(FilterSummary));

            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            EndLoading();
        }
    }

    /// <summary>Отмечает начало чтения: первый источник поднимает <see cref="IsBusy"/>.</summary>
    private void BeginLoading()
    {
        _loadingCount++;

        if (_loadingCount == 1)
        {
            OnPropertyChanged(nameof(IsBusy));
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Отмечает конец чтения: индикатор снимается только с последним ответом.</summary>
    private void EndLoading()
    {
        _loadingCount--;

        if (_loadingCount == 0)
        {
            OnPropertyChanged(nameof(IsBusy));
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Границы показа в том виде, в каком они уйдут в запрос; обратный период меняется местами.</summary>
    private (DateOnly? From, DateOnly? To) NormalizeRange()
    {
        if (DateFrom is { } from && DateTo is { } to && from > to)
        {
            (DateFrom, DateTo) = (to, from);
        }

        return (ToDateOnly(DateFrom), ToDateOnly(DateTo));
    }

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value is { } date ? DateOnly.FromDateTime(date) : null;
}
