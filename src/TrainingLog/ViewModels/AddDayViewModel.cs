using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.ViewModels;

/// <summary>Окно добавления дня тренировки: дата, план и подходы по упражнениям плана.</summary>
/// <remarks>
/// Окно одно и на добавление, и на правку: различаются только запись, которая за этой датой уже
/// есть, и команда сохранения. Отдельного окна правки не заводим — дата и так единственный способ
/// найти запись, и он в этом окне.
///
/// Состав дня задаётся планом: кнопки упражнений перечисляют упражнения плана по порядку
/// выполнения, и в записи не может оказаться упражнения, которого в плане нет.
///
/// Сохранения два, и это не дублирование: «Сохранить» пишет день и оставляет окно открытым,
/// «Сохранить и закрыть» — то же плюс закрытие. Одна кнопка на оба случая означала бы, что
/// сохранить без закрытия нельзя, а при наборе дня это обычное дело.
/// </remarks>
public sealed partial class AddDayViewModel : ObservableObject
{
    private readonly ITrainingSessionRepository _sessionRepository;
    private readonly ITrainingPlanRepository _planRepository;

    /// <summary>Запись за текущую дату, если она уже есть. Её наличие переводит окно в правку.</summary>
    private TrainingSession? _existing;

    /// <summary>
    /// День, для которого запись уже подтянута.
    /// </summary>
    /// <remarks>
    /// <c>DatePicker</c> присоединяется со своим значением по умолчанию и двусторонней привязкой
    /// пишет его в источник, поэтому при открытии окна дата меняется один раз сама по себе. Без
    /// этой отметки запись читалась бы дважды, и первый проход успевал бы обнулить упражнения и
    /// написать в Status «план удалён», пока планы ещё не загружены (ловушка 27).
    /// </remarks>
    private DateOnly? _loadedDay;

    /// <summary>
    /// Снимок того, что лежит в базе за текущий день.
    /// </summary>
    /// <remarks>
    /// Именно снимок того, что сохраняется, а не то, что показывает форма: форма показывает все
    /// упражнения плана, а запись — только те, где есть подходы, и сравнение «строка формы против
    /// упражнений плана» сочло бы изменением каждый непустой план, даже не тронутый. <c>null</c>
    /// означает, что записи за день нет: тогда любое набранное — изменение.
    /// </remarks>
    private TrainingSession? _savedSnapshot;

    /// <summary>
    /// Подавляет пересчёт «есть несохранённое», пока окно наполняется сверху.
    /// </summary>
    /// <remarks>
    /// Наполнение идёт построчно: упражнение очищается, потом заново заполняется, и между этими
    /// шагами в форме на миг не остаётся ничего. Сравнение с записью на этот миг честно отвечает
    /// «отличается», и надпись вспыхивает по ходу загрузки — на пустом дне так и остаётся
    /// загоревшейся. Тот же приём, что <c>_isApplyingChecks</c> в <c>EditPlanViewModel</c>.
    /// </remarks>
    private bool _isApplyingSets;

    /// <summary>
    /// Сколько подтягиваний прошлого дня идёт прямо сейчас.
    /// </summary>
    /// <remarks>
    /// Счётчик, а не флаг: планы меняют быстро, второе подтягивание успевает начаться, пока первое
    /// ещё в пути, и с флагом более ранний <c>finally</c> погасил бы индикатор при живом втором —
    /// ввод разрешился бы с недозаполненной формой. Обратный порядок оставил бы счётчик
    /// отрицательным только при ошибке в самом счётчике, а не при гонке, то есть окно не может
    /// остаться заблокированным навсегда.
    /// </remarks>
    private int _loadingCount;

    /// <summary>
    /// Дата записи, из которой подтянуты значения. Обнуляется при каждой перестройке окна, а
    /// ставится после наполнения: пока форма пересобирается, дата источника неверна в любом случае.
    /// </summary>
    private DateOnly? _pulledFromDate;

    /// <param name="sessionRepository">Журнал тренировок.</param>
    /// <param name="planRepository">Планы тренировок: состав дня берётся из плана.</param>
    /// <param name="day">Дата, на которую открывается окно. <c>null</c> — добавляется сегодня.</param>
    public AddDayViewModel(
        ITrainingSessionRepository sessionRepository,
        ITrainingPlanRepository planRepository,
        DateOnly? day = null)
    {
        ArgumentNullException.ThrowIfNull(sessionRepository);
        ArgumentNullException.ThrowIfNull(planRepository);

        _sessionRepository = sessionRepository;
        _planRepository = planRepository;

        // Дата кладётся в поле напрямую, а не через свойство Date: сеттер зовёт LoadDateCommand, а
        // планы в конструкторе ещё не загружены, и собственное значение DatePicker при показе окна
        // запустило бы лишнее чтение вхолостую. Первое наполнение делает InitializeCommand — сначала
        // планы, потом запись, — поэтому чтений остаётся одно.
        _date = day?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
    }

    /// <summary>Планы для выбора, по алфавиту.</summary>
    public ObservableCollection<TrainingPlan> Plans { get; } = [];

    /// <summary>Упражнения выбранного плана в порядке выполнения: кнопки окна.</summary>
    public ObservableCollection<PlanExerciseViewModel> Exercises { get; } = [];

    /// <summary>
    /// Дата в окне. Тип <see cref="DateTime"/> — из-за <c>DatePicker</c>, который по
    /// <see cref="DateOnly"/> не привязывается; доменная дата получается через <see cref="Day"/>.
    /// </summary>
    /// <remarks>
    /// Начальное значение задаёт конструктор, минуя сеттер (см. замечание там), поэтому и значение
    /// по умолчанию живёт в конструкторе, а не в инициализаторе поля.
    /// </remarks>
    [ObservableProperty]
    private DateTime? _date;

    /// <summary>Выбранный план.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAndCloseCommand))]
    private TrainingPlan? _selectedPlan;

    /// <summary>
    /// Номер активного упражнения, с нуля. Навигация уведомляется ещё и в <see cref="Refresh"/>:
    /// одного атрибута мало, потому что при пересборке состава индекс не меняется, а состав
    /// меняется, и границы «‹» и «›» уезжают.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private int _currentIndex;

    /// <summary>
    /// Сообщение об отказе сохранения. Именно отказы сохранения, а не ввода: они живут до успешного
    /// сохранения, потому что день не записан и объяснение обязано дождаться, пока им займутся.
    /// Отказ по вводу показывается подсказкой <see cref="Hint"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    /// <summary>
    /// Подсказка об отказе по вводу в полях подходов. Общее с главным окном состояние и общий
    /// контрол показа. Разделено с <see cref="Status"/> не по удобству, а по сроку жизни: отказ о
    /// записи не должен исчезать через несколько секунд.
    /// </summary>
    public HintState Hint { get; } = new();

    /// <summary>Изменения приняты и окно должно закрыться. Окно выставляет <c>DialogResult</c> по нему.</summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Было ли что-то сохранено за время работы окна.
    /// </summary>
    /// <remarks>
    /// Отдельное от <see cref="Accepted"/> состояние: «Сохранить» не закрывает окно, поэтому закрыть
    /// его потом можно крестиком или Esc, и <c>DialogResult</c> останется <c>null</c>. По одному
    /// <see cref="Accepted"/> главное окно тогда не узнает, что день сохранён, и не перечитает журнал.
    /// </remarks>
    public bool Saved { get; private set; }

    /// <summary>Есть ли непустое сообщение об отказе.</summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    /// <summary>Идёт ли чтение из базы, и ввод в окно поэтому заблокирован.</summary>
    /// <remarks>
    /// Пока подтягивается прошлый день, набирать нельзя: ответ придёт в уже заполненные поля и
    /// затрёт набранное. Блокируется только область ввода, а кнопки сохранения — теми же
    /// <see cref="CanAccept"/>, что и всегда: на пустом дне сохранять нечего, и отдельное
    /// состояние разошлось бы с правилом пригодности.
    /// </remarks>
    public bool IsLoading => _loadingCount > 0;

    /// <summary>Разрешён ли ввод: обратное <see cref="IsLoading"/>.</summary>
    /// <remarks>
    /// Отдельное свойство, а не инверсия в разметке: конвертер «не наоборот» означал бы ещё одно
    /// вычисляемое выражение, которое придётся дублировать при каждой правке.
    /// </remarks>
    public bool CanFill => !IsLoading;

    /// <summary>
    /// Дата, из которой подтянуты значения, в скобках: «(на основе 08 октября 2026)».
    /// </summary>
    /// <remarks>
    /// Формат задаёт <see cref="Dates"/>, а не эта строка: он общий со строкой журнала. Рядом с
    /// надписью «Не сохранено» она отвечает на вопрос, откуда взялись значения, — иначе
    /// подставленные веса выглядели бы как введённые вручную.
    /// </remarks>
    public string PulledFromDateText =>
        _pulledFromDate is { } date ? $"(на основе {Dates.Format(date)})" : string.Empty;

    /// <summary>
    /// Есть ли в окне то, чего нет в базе.
    /// </summary>
    /// <remarks>
    /// Прямое сравнение с <see cref="_savedSnapshot"/>, а не признак «пользователь что-то трогал»:
    /// надпись должна гаснуть, когда внесли значение и стёрли его обратно. В отличие от
    /// <c>CanAccept</c>, изменение здесь и есть условие — та про пригодность данных.
    ///
    /// Негодный текст тоже считается изменением, иначе надпись молчала бы ровно тогда, когда
    /// кнопки погасли: <see cref="BuildSession"/> на мусоре возвращает <c>null</c>.
    /// </remarks>
    public bool HasUnsavedChanges
    {
        get
        {
            if (SelectedPlan is not { } plan)
            {
                return false;
            }

            var current = BuildSession(plan);

            return current is null
                ? Exercises.Any(row => row.HasData)
                : Differs(_savedSnapshot, current);
        }
    }

    /// <summary>Дата в доменном виде. Пустое поле даёт сегодняшний день: записать день некуда.</summary>
    public DateOnly Day => DateOnly.FromDateTime(Date ?? DateTime.Today);

    /// <summary>Активное упражнение: его подходы показаны в центре окна.</summary>
    public PlanExerciseViewModel? Current =>
        CurrentIndex >= 0 && CurrentIndex < Exercises.Count ? Exercises[CurrentIndex] : null;

    /// <summary>Есть ли активное упражнение.</summary>
    /// <remarks>
    /// Отдельное свойство, а не вычисление в разметке: привязка индикации идёт через
    /// <see cref="Current"/>, который до выбора плана равен <c>null</c>, и тогда путь не разрешается,
    /// а привязка отдаёт значение по умолчанию целевого свойства — у <c>Visibility</c> это
    /// <c>Visible</c> (ловушка 43). Здесь источник непустой, и конвертер снова законен.
    ///
    /// Уведомляется в <see cref="Refresh"/> рядом с <see cref="Current"/>: это единственная общая
    /// точка смены текущего упражнения, а уведомления на сеттере индекса не было бы — при смене
    /// плана индекс и так ноль.
    /// </remarks>
    public bool HasCurrent => Current is not null;

    /// <summary>Заголовок окна: на добавление или на правку — по тому, есть ли запись за дату.</summary>
    public string Title => _existing is null ? "Добавить день" : "Правка дня";

    /// <summary>
    /// Дата изменилась — читаем запись за неё. Тот же день перечитывать незачем: иначе собственное
    /// значение <c>DatePicker</c> при открытии окна запускало бы второе чтение вхолостую.
    /// </summary>
    partial void OnDateChanged(DateTime? value)
    {
        var day = DateOnly.FromDateTime(value ?? DateTime.Today);

        if (_loadedDay != day)
        {
            LoadDateCommand.Execute(null);
        }
    }

    /// <summary>
    /// Смена плана пересобирает упражнения, наполняет их подходами и подтягивает прошлый день.
    /// </summary>
    /// <remarks>
    /// Подтягивание идёт отдельной командой, а не внутри перестройки: перестройка обязана быть
    /// синхронной (её же зовёт <see cref="LoadDateAsync"/> посреди чтения), а чтение из базы
    /// асинхронно.
    /// </remarks>
    partial void OnSelectedPlanChanged(TrainingPlan? value)
    {
        RebuildWindow();

        LoadPreviousCommand.Execute(null);
    }

    /// <summary>
    /// Подставляет ли подходы записи в строки упражнений.
    /// </summary>
    /// <remarks>
    /// Запись за дату принадлежит одному плану. Пока выбран другой, показывать их нельзя: одно
    /// упражнение может входить в оба плана, и тогда в полях чужого плана появились бы подходы из
    /// записи. Идентификатор плана сравнивается, а не название: планы переименовывают.
    /// </remarks>
    private bool ShowsStoredSets => _existing is { PlanId: > 0 } stored && stored.PlanId == SelectedPlan?.Id;

    partial void OnCurrentIndexChanged(int value) => Refresh();

    /// <summary>Загружает планы. Вызывается окном при загрузке.</summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        var plans = await _planRepository.GetAllAsync().ConfigureAwait(true);

        Plans.Clear();

        // Порядок тот же, что в окне планов: сравнение кодов Unicode совпадает с тем, что делает
        // база, и даёт русский алфавит.
        foreach (var plan in plans.OrderBy(plan => plan.Name, StringComparer.Ordinal))
        {
            Plans.Add(plan);
        }

        await LoadDateAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Перечитывает запись за текущую дату и наполняет окно: план, упражнения, подходы.
    /// </summary>
    /// <remarks>
    /// Запись за дату одна, поэтому окно само знает, добавление это или правка, и вызывающему не
    /// нужно передавать режим: иначе дата, изменённая после открытия, осталась бы в режиме
    /// добавления и упёрлась бы в занятую дату. Сначала план, потом сброс активного упражнения,
    /// потом подходы: смена плана перестраивает кнопки, и подходы наполняются на готовых строках.
    /// </remarks>
    [RelayCommand]
    private async Task LoadDateAsync()
    {
        var requestedDay = Day;

        var existing = await _sessionRepository.GetByDateAsync(requestedDay).ConfigureAwait(true);

        // Пока читалось, дату могли поменять: ответы возвращаются не по порядку, и без этой
        // проверки в окне остались бы подходы чужого дня.
        if (requestedDay != Day)
        {
            return;
        }

        _loadedDay = requestedDay;
        _existing = existing;

        // Снимок берётся с прочитанной записи, а не собирается заново из полей: так эталоном
        // становится то, что действительно лежит в базе, и смена даты гасит надпись сразу.
        //
        // Присваивается до перестройки окна. Само по себе это страховка, а не лечение: пока
        // состояние задаётся сверху, пересчёт подавлен. Но эталон обязан стоять раньше любого
        // уведомления, которое его читает, иначе новая правка снова получит старое значение.
        _savedSnapshot = existing;

        var plan = SelectPlan(existing);

        Status = existing is null || plan is not null
            ? string.Empty
            : "План этой записи удалён из справочника: выберите план, чтобы записать день заново.";

        SelectedPlan = plan;

        // Перестройка идёт и под смену плана, и под смену даты. Сеттер SelectedPlan сработает не
        // всегда: у двух дат план один, значение не меняется, OnSelectedPlanChanged не зовётся — и
        // ни сброс активного упражнения, ни наполнение подходов не случатся. Поэтому они зовутся
        // явно, а повторный вызов через сеттер безвреден: обе операции идемпотентны.
        RebuildWindow();

        OnPropertyChanged(nameof(Title));
    }

    /// <summary>Находит план записи среди доступных — по идентификатору, а не по названию.</summary>
    private TrainingPlan? SelectPlan(TrainingSession? session) =>
        session is null ? null : Plans.SingleOrDefault(plan => plan.Id == session.PlanId);

    /// <summary>Подтягивает подходы и веса прошлого раза с тем же планом.</summary>
    /// <remarks>
    /// Только при добавлении: за день, который уже записан, показываются подходы самой записи, и
    /// подтягивание затерло бы их.
    ///
    /// Дата и план запоминаются **до** чтения, а проверяются после: планы переключают быстро, и без
    /// проверки в окно попали бы веса того плана, который выбрали минуту назад. Тот же приём, что в
    /// <see cref="LoadDateAsync"/> (ловушка 27).
    ///
    /// У команды нет <c>CanExecute</c>, и это осознанно: <c>ICommand.Execute</c> его проверяет, и
    /// второй план во время загрузки просто не запустил бы чтение — форма осталась бы пустой.
    /// Блокируется ввод, а не логика.
    /// </remarks>
    [RelayCommand]
    private async Task LoadPreviousAsync()
    {
        if (_existing is not null || SelectedPlan is not { } plan)
        {
            return;
        }

        var requestedDay = Day;
        var requestedPlanId = plan.Id;

        _loadingCount++;
        UpdateLoading();

        try
        {
            var previous = await _sessionRepository
                .GetPreviousByPlanAsync(requestedPlanId, requestedDay)
                .ConfigureAwait(true);

            // Ответ на сменённый план или сменённую дату выбрасывается целиком: подставить веса
            // чужого плана хуже, чем не подставить ничего.
            if (previous is null || Day != requestedDay || SelectedPlan?.Id != requestedPlanId)
            {
                return;
            }

            ApplyPreviousSets(previous);
        }
        finally
        {
            _loadingCount--;
            UpdateLoading();

            // Пересчёт и на отказе: пустой ответ и выброшенный ответ — тоже состояние формы.
            Refresh();
        }
    }

    /// <summary>Подставляет подходы прошлого дня в поля упражнений плана.</summary>
    /// <remarks>
    /// Веса становятся значениями, повторения и примечание — подсказками: подходы отличаются
    /// повторениями гораздо чаще, чем весом, а молчаливая подстановка прошлого числа записала бы в
    /// журнал то, чего в этот раз не делали.
    ///
    /// Наполнение идёт под <c>_isApplyingSets</c>: иначе надпись «Не сохранено» мигала бы на
    /// каждом подходе. Дата источника ставится после наполнения, чтобы уведомление ушло с готовым
    /// значением. Сопоставление упражнений — по идентификатору, как и в
    /// <see cref="ApplyExistingSets"/>: упражнение могли переименовать.
    /// </remarks>
    private void ApplyPreviousSets(TrainingSession previous)
    {
        _isApplyingSets = true;

        try
        {
            foreach (var row in Exercises)
            {
                var entry = previous.Exercises.SingleOrDefault(item => item.ExerciseId == row.Exercise.Id);

                row.Sets.Clear();

                // Примечание подтягивается подсказкой и всегда с нуля: в форме его ещё нет, и
                // прежнее значение у упражнения, которое в прошлый раз не делали, показало бы
                // подсказку от чужого упражнения.
                row.NoteText = string.Empty;
                row.NotePlaceholder = entry?.Notes ?? string.Empty;

                if (entry is not null)
                {
                    foreach (var set in entry.Sets)
                    {
                        row.Sets.Add(ToSetInput(set, repetitionsAsPlaceholder: true));
                    }
                }

                // Упражнение, которого в прошлый раз не делали, получает одно пустое поле — кнопки
                // «+» и «−» с ним работают так же.
                if (row.Sets.Count == 0)
                {
                    row.Sets.Add(new SetInputViewModel());
                }
            }
        }
        finally
        {
            _isApplyingSets = false;
            _pulledFromDate = previous.Date;
        }
    }

    /// <summary>Сообщает о смене состояния чтения.</summary>
    /// <remarks>
    /// Оба свойства уведомляются отсюда и только отсюда: они вычисляются от одного и того же
    /// счётчика, и уведомление по одному разошлось бы с другим. Дата источника уведомляется здесь
    /// же: подтягивание выходит раньше этой точки и на правке, где его вовсе не надо.
    /// </remarks>
    private void UpdateLoading()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(CanFill));
        OnPropertyChanged(nameof(PulledFromDateText));
    }

    /// <summary>Заполняет подходы упражнений содержимым записи за дату либо очищает их.</summary>
    /// <remarks>
    /// Очистка обязательна и при переходе на дату без записи: смена даты не должна оставлять то, что
    /// набрано для другой.
    ///
    /// Идемпотентна: зовётся и из <see cref="OnSelectedPlanChanged"/>, и из
    /// <see cref="LoadDateAsync"/>, а пересекаются при загрузке дня. Дубликат безвреден: поля
    /// очищаются и заполняются заново из той же записи.
    ///
    /// Упражнение, удалённое из плана или из справочника, в окне не показать: кнопки перечисляют
    /// упражнения плана. На сохранении оно выпадет из записи.
    /// </remarks>
    private void ApplyExistingSets()
    {
        var stored = ShowsStoredSets ? _existing : null;

        foreach (var row in Exercises)
        {
            var entry = stored?.Exercises.SingleOrDefault(item => item.ExerciseId == row.Exercise.Id);

            row.Sets.Clear();

            // Примечание здесь — значение, а не подсказка: показывается то, что записано за этот
            // день, и пользователь правит его, а не сверяется с прошлым разом.
            row.NoteText = entry?.Notes ?? string.Empty;
            row.NotePlaceholder = string.Empty;

            if (entry is not null)
            {
                foreach (var set in entry.Sets)
                {
                    row.Sets.Add(ToSetInput(set, repetitionsAsPlaceholder: false));
                }
            }

            // Пустое упражнение всё равно получает одно пустое поле, иначе подход негде набрать.
            if (row.Sets.Count == 0)
            {
                row.Sets.Add(new SetInputViewModel());
            }
        }

        Refresh();
    }

    /// <summary>Подход записи журнала в виде полей окна.</summary>
    /// <remarks>
    /// Общее место и для правки дня, и для подтягивания прошлого: формат веса один, и разъехался
    /// бы он только в одном из двух путей — а видно это как «60» в одном поле и «60,0» в другом.
    /// При <paramref name="repetitionsAsPlaceholder"/> повторения попадают в подсказку, а не в
    /// поле: значение прошлого раза показывается, но не сохраняется.
    /// </remarks>
    /// <param name="set">Подход записи журнала.</param>
    /// <param name="repetitionsAsPlaceholder">Показать ли повторения плейсхолдером.</param>
    private static SetInputViewModel ToSetInput(TrainingSet set, bool repetitionsAsPlaceholder)
    {
        // Ноль повторений означает, что поле тогда просто не заполняли: подсказка «0» читалась бы
        // как «делай ноль повторений». Вес нулевым быть может и по делу, поэтому подсказка про него
        // есть.
        var repetitions = set.Repetitions > 0
            ? set.Repetitions.ToString(CultureInfo.CurrentCulture)
            : string.Empty;

        return new SetInputViewModel
        {
            WeightText = set.Weight.ToString("0.##", CultureInfo.CurrentCulture),

            // Форматируется по текущей культуре: в русской раскладке десятичный разделитель — запятая,
            // и это ровно тот вид, в котором поле разбирается обратно.
            RepetitionText = repetitionsAsPlaceholder ? string.Empty : repetitions,
            RepetitionPlaceholder = repetitionsAsPlaceholder ? repetitions : string.Empty,
        };
    }

    /// <summary>
    /// Собирает упражнения выбранного плана заново: смена плана переписывает состав дня, иначе
    /// подходы старого плана остались бы в новом.
    /// </summary>
    private void BuildExercises()
    {
        UnsubscribeFromRows();

        Exercises.Clear();

        if (SelectedPlan is null)
        {
            SelectFirstExercise();
            return;
        }

        var number = 1;

        foreach (var exercise in SelectedPlan.Exercises)
        {
            var row = PlanExerciseViewModel.Create(exercise, number++);

            Exercises.Add(row);
        }

        SubscribeToRows();

        // Активным по умолчанию становится первое упражнение: с пустым выбором открывать окно
        // незачем, а выбирать первое вручную — лишний шаг.
        SelectFirstExercise();
    }

    /// <summary>Перестраивает окно под текущий план и подходы, не показывая промежуточных состояний.</summary>
    /// <remarks>
    /// Подавление охватывает обе операции целиком, а не каждую по отдельности: между
    /// <see cref="BuildExercises"/> и <see cref="ApplyExistingSets"/> форма ещё пуста, и признак
    /// «есть несохранённое» на этот миг честно отвечает «отличается» — надпись вспыхивает.
    ///
    /// Дата источника обнуляется здесь же: перестройка сама стирает значения, и оставить дату, из
    /// которой они пришли, значило бы показать в скобках прошлый день у пустых полей.
    /// </remarks>
    private void RebuildWindow()
    {
        _pulledFromDate = null;

        // Уведомление здесь не нужно: смена плана зовёт LoadPreviousCommand, а тот даже на правке
        // доводит дело до UpdateLoading.
        _isApplyingSets = true;

        try
        {
            BuildExercises();
            ApplyExistingSets();
        }
        finally
        {
            _isApplyingSets = false;
        }

        // Единственное уведомление за перестройку — уже с готовым значением.
        Refresh();
    }

    /// <summary>Подписывает состав на изменения: появление и исчезновение подходов, ввод в них и правку примечания.</summary>
    /// <remarks>
    /// Подписка на все упражнения, а не на видимое, и это не разница в оптимизации. Снимок и признак
    /// «есть несохранённое» считаются по всему дню, и подписка только на видимое означала бы, что
    /// ввод в другое упражнение не обновляет ничего.
    ///
    /// Отписка снимает и подписки на подходы: у строк свой состав, и на новый набор они навешиваются
    /// заново. Подписка на саму строку нужна ради примечания: оно лежит не в подходе, и без неё
    /// правка молча не обновила бы ни надпись, ни активность кнопок сохранения.
    /// </remarks>
    private void SubscribeToRows()
    {
        foreach (var row in Exercises)
        {
            row.PropertyChanged += OnRowPropertyChanged;
            row.Sets.CollectionChanged += OnSetsCollectionChanged;

            foreach (var set in row.Sets)
            {
                set.PropertyChanged += OnSetPropertyChanged;
            }
        }
    }

    /// <summary>Снимает подписки со всех упражнений и их подходов.</summary>
    private void UnsubscribeFromRows()
    {
        foreach (var row in Exercises)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
            row.Sets.CollectionChanged -= OnSetsCollectionChanged;

            foreach (var set in row.Sets)
            {
                set.PropertyChanged -= OnSetPropertyChanged;
            }
        }
    }

    private void OnSetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not ObservableCollection<SetInputViewModel> sets)
        {
            return;
        }

        // Новый подход подписан сразу: пока он не подписан, ввод в него не обновляет ни кнопки
        // сохранения, ни надпись. Отписка перед навешиванием — чтобы повторный вызов не накопился.
        foreach (var set in sets)
        {
            set.PropertyChanged -= OnSetPropertyChanged;
            set.PropertyChanged += OnSetPropertyChanged;
        }

        Refresh();
    }

    /// <summary>Любое изменение подхода пересчитывает окно целиком.</summary>
    /// <remarks>
    /// Безусловно, а не по списку свойств: подход хранит значения строками, меняются
    /// <c>WeightText</c> и <c>RepetitionText</c>, а <c>IsValid</c> и <c>HasData</c> — их
    /// производные. Перечислить проверяемые свойства значило бы забыть новое поле и получить
    /// молчащую надпись на «+» или «−».
    /// </remarks>
    private void OnSetPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    /// <summary>Правка примечания пересчитывает окно целиком.</summary>
    /// <remarks>
    /// По списку свойств, а не безусловно — единственное место в окне, где так: у строки упражнения
    /// есть <c>IsSelected</c>, и его пишет сам <see cref="Refresh"/>, поэтому безусловная реакция
    /// гоняла бы пересчёт по кругу Refresh → IsSelected → Refresh. У подходов такой проблемы нет:
    /// их свойства никто извне не пишет. Список полей примечания исчерпывающий — третьего
    /// текстового поля у упражнения нет.
    /// </remarks>
    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlanExerciseViewModel.NoteText)
            or nameof(PlanExerciseViewModel.NotePlaceholder))
        {
            Refresh();
        }
    }

    /// <summary>Приводит окно в согласованное состояние: активное упражнение, отметки, активность команд.</summary>
    /// <remarks>
    /// Здесь же уведомляется <see cref="Current"/>, и это обязательно: привязки <c>Current.Sets</c>
    /// и <c>Current.Name</c> подписываются на это свойство, а глубже подписаться они не могут,
    /// пока оно <c>null</c>. Уведомления на <c>CurrentIndex</c> не годилось: при смене плана и при
    /// открытии окна индекс и так ноль, уведомление не поднимается — и область подходов остаётся
    /// пустой, пока пользователь не переключит упражнение.
    ///
    /// Через <see cref="Refresh"/> проходят все изменения состава, поэтому уведомление о текущем
    /// упражнении нельзя забыть в будущей правке. Рядом уведомляется и <see cref="HasCurrent"/>:
    /// поднять его больше негде. Подписки на состав идут в <see cref="SubscribeToRows"/>, а не
    /// здесь: Refresh зовётся часто, и навешивание подписок в нём создавало бы их заново.
    /// </remarks>
    private void Refresh()
    {
        for (var index = 0; index < Exercises.Count; index++)
        {
            Exercises[index].IsSelected = index == CurrentIndex;
        }

        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(HasCurrent));

        AddSetCommand.NotifyCanExecuteChanged();
        RemoveSetCommand.NotifyCanExecuteChanged();

        // Обе кнопки сохранения проверяют одно и то же условие, поэтому обновляются вместе:
        // «Сохранить» на Ctrl+S не обновлять нельзя — иначе после негодного ввода горячая клавиша
        // звала бы сохранение при серой кнопке.
        SaveCommand.NotifyCanExecuteChanged();
        SaveAndCloseCommand.NotifyCanExecuteChanged();

        // Навигация обновляется здесь, а не только на CurrentIndex: через сеттер индекса она
        // срабатывает лишь при реальной смене значения, а при открытии окна индекс и так ноль —
        // WPF остаётся с тем CanExecute, что спросил до загрузки упражнений.
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();

        // Признак несохранённого считается здесь же: он зависит от состава и от ввода, а через
        // [ObservableProperty] не поднялся бы. На время наполнения уведомление подавлено.
        if (!_isApplyingSets)
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    /// <summary>
    /// Смещается ли активное упражнение на <paramref name="delta"/> позиций.
    /// </summary>
    /// <remarks>
    /// Проверка и выполнение разделены, как у любой команды [RelayCommand], поэтому граница
    /// повторяется в самом переходе — иначе команда, вызванная программно, уводила бы индекс за
    /// пределы списка (ловушка 46). Обе половины живут рядом, чтобы не разъехались.
    /// </remarks>
    private bool CanMoveTo(int delta) =>
        CurrentIndex + delta >= 0 && CurrentIndex + delta < Exercises.Count;

    private void MoveTo(int delta)
    {
        if (CanMoveTo(delta))
        {
            CurrentIndex += delta;
        }
    }

    /// <summary>Сбрасывает активное упражнение на первое.</summary>
    /// <remarks>
    /// Отдельный метод, а не присваивание в <see cref="LoadDateAsync"/>, потому что свойство
    /// <c>CurrentIndex</c> само уведомление не поднимет: там, где упражнений больше одного, индекс
    /// уже 0. <see cref="Refresh"/> звать нужно явно — иначе отметки активности остались бы от
    /// прежнего упражнения.
    /// </remarks>
    private void SelectFirstExercise()
    {
        CurrentIndex = 0;

        Refresh();
    }

    [RelayCommand]
    private void SelectExercise(PlanExerciseViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        CurrentIndex = Exercises.IndexOf(row);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous() => MoveTo(-1);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next() => MoveTo(1);

    private bool CanGoPrevious() => CanMoveTo(-1);

    private bool CanGoNext() => CanMoveTo(1);

    private bool CanAddSet() => Current is not null;

    private bool CanRemoveSet() => Current is { Sets.Count: > 0 };

    /// <summary>
    /// Добавляет подход активному упражнению. Вес нового подхода — вес предыдущего: подходы одного
    /// упражнения отличаются повторениями гораздо чаще, чем весом.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSet))]
    private void AddSet()
    {
        if (Current is not { } row)
        {
            return;
        }

        row.Sets.Add(new SetInputViewModel
        {
            WeightText = row.Sets.Count > 0 ? row.Sets[^1].WeightText : string.Empty,
        });

        Refresh();
    }

    /// <summary>Убирает самый правый подход активного упражнения.</summary>
    [RelayCommand(CanExecute = nameof(CanRemoveSet))]
    private void RemoveSet()
    {
        if (Current is not { Sets.Count: > 0 } row)
        {
            return;
        }

        row.Sets.RemoveAt(row.Sets.Count - 1);

        Refresh();
    }

    /// <summary>Обе кнопки сохранения активны, когда день есть чем наполнить и в полях нет мусора.</summary>
    /// <remarks>
    /// Требование «изменилось что-то» здесь не годится: день, набранный заново на ту же дату,
    /// должен сохраняться. Вместо этого проверяется пригодность: пустой день сохранять нечего, а
    /// негодный текст разбирать нечем.
    ///
    /// Упражнение с одним примечанием и без подходов день наполняет — по тому же решению, что и
    /// <see cref="BuildSession"/>.
    /// </remarks>
    private bool CanAccept() =>
        SelectedPlan is not null
        && Exercises.Any(row => row.HasData)
        && Exercises.All(row => row.Sets.All(set => set.IsValid));

    /// <summary>Сохраняет день и закрывает окно.</summary>
    [RelayCommand(CanExecute = nameof(CanAccept))]
    private Task SaveAndCloseAsync() => SaveInternalAsync(close: true);

    /// <summary>Сохраняет день и оставляет окно открытым.</summary>
    /// <remarks>
    /// У этой кнопки <see cref="Accepted"/> не выставляется: окно после неё остаётся открытым, и
    /// закрыть его можно крестиком или Esc. Повторное сохранение идёт уже как правка.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanAccept))]
    private Task SaveAsync() => SaveInternalAsync(close: false);

    /// <summary>Сохраняет день, при <paramref name="close"/> — закрывает окно.</summary>
    /// <remarks>
    /// После сохранения без закрытия присваивается <c>_existing</c>, и это не косметика: запись уже в
    /// базе, поэтому второе нажатие «Сохранить» без этого пошло бы в <c>AddAsync</c> и упёрлось бы в
    /// занятую дату — на собственной же правке. Состояние «день уже записан» должно следовать за
    /// первым же сохранением, а не за закрытием окна.
    /// </remarks>
    /// <param name="close">Закрывать ли окно после сохранения.</param>
    private async Task SaveInternalAsync(bool close)
    {
        if (SelectedPlan is not { } plan)
        {
            return;
        }

        var session = BuildSession(plan);

        if (session is null)
        {
            return;
        }

        var saved = _existing is null
            ? await AddAsync(session).ConfigureAwait(true)
            : await UpdateAsync(session).ConfigureAwait(true);

        if (!saved)
        {
            return;
        }

        // Сброс сообщения об отказе: после успешного сохранения над полями не должно висеть «за эту
        // дату уже есть запись» от прошлой попытки.
        Status = string.Empty;

        // Присваивается вся собранная запись, а не её копия: у неё уже есть идентификатор, который
        // репозиторий записал при AddAsync, и следующее сохранение должно уйти в UpdateAsync по нему.
        _existing = session;

        // Снимок обновляется здесь же: надпись должна погаснуть сразу после сохранения, а снимок —
        // ровно то, что теперь лежит в базе.
        _savedSnapshot = session;

        Saved = true;

        if (close)
        {
            Accepted = true;
        }

        // Приватные сеттеры не дают сгенерировать уведомления автоматически, а окно ждёт их. Title —
        // по той же причине: после сохранения без закрытия заголовок меняется на «Правка дня», и в
        // базе это теперь правка.
        OnPropertyChanged(nameof(Accepted));
        OnPropertyChanged(nameof(Saved));
        OnPropertyChanged(nameof(Title));

        Refresh();
    }

    /// <summary>Отличается ли набранное от того, что лежит в базе.</summary>
    /// <remarks>
    /// Отдельная чистая функция, а не только вычисляемое свойство: правило многослойное (план,
    /// состав, подходы по порядку) и его надо проверить тестом, не поднимая окно.
    ///
    /// Сравнивается по порядку, а не как множество: перестановка подходов меняет день, и сравнение
    /// множеств её пропустило бы. Обе стороны приходят из <c>BuildSession</c>, то есть уже
    /// разобранные, и потому одинаково видят и «60» → «60,0», и перестановку.
    /// </remarks>
    /// <param name="saved">Снимок того, что лежит в базе, или <c>null</c>, если записи нет.</param>
    /// <param name="current">То, что сохранилось бы при нажатии кнопки.</param>
    public static bool Differs(TrainingSession? saved, TrainingSession? current)
    {
        // Записи нет — сохранять есть что тогда и только тогда, когда набрано хоть что-то.
        if (saved is null)
        {
            return current is { Exercises.Count: > 0 };
        }

        if (current is null)
        {
            return true;
        }

        if (saved.PlanId != current.PlanId || saved.PlanName != current.PlanName)
        {
            return true;
        }

        return !SetsMatch(saved, current);
    }

    /// <summary>Совпадают ли упражнения и подходы записей.</summary>
    private static bool SetsMatch(TrainingSession saved, TrainingSession current)
    {
        var savedEntries = saved.Exercises.ToList();
        var currentEntries = current.Exercises.ToList();

        if (savedEntries.Count != currentEntries.Count)
        {
            return false;
        }

        for (var index = 0; index < savedEntries.Count; index++)
        {
            if (!EntriesMatch(savedEntries[index], currentEntries[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EntriesMatch(ExerciseEntry saved, ExerciseEntry current)
    {
        if (saved.ExerciseId != current.ExerciseId || saved.ExerciseName != current.ExerciseName)
        {
            return false;
        }

        // Примечание сравнивается нормализованным — тем же, что уходит в базу. Без этого пробелы по
        // краям, оставшиеся от прежней редакции, горели бы надписью на ненабранном поле.
        if (NormalizeNote(saved.Notes) != NormalizeNote(current.Notes))
        {
            return false;
        }

        return saved.Sets.Count == current.Sets.Count
            && saved.Sets.Zip(current.Sets, SetsEqual).All(equal => equal);
    }

    /// <summary>Приводит примечание к сравнимому виду: края срезаны, пустое — <c>null</c>.</summary>
    /// <remarks>
    /// Одна функция на запись и на сравнение: обе стороны должны решать «пусто или нет» одинаково,
    /// иначе надпись горела бы на поле, которое сохранилось бы ровно тем же, что лежит в базе.
    /// </remarks>
    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool SetsEqual(TrainingSet saved, TrainingSet current) =>
        saved.Order == current.Order
        && saved.Repetitions == current.Repetitions
        && saved.Weight == current.Weight;

    /// <summary>Собирает запись дня из введённых подходов.</summary>
    /// <remarks>
    /// Упражнение без введённых подходов и без примечания в запись не попадает: план выполнен не
    /// целиком, а невыполненные упражнения в журнале только шумят. Примечание — исключение, и оно
    /// осознанное: упражнение, к которому записали только примечание, выбросить из записи хуже,
    /// чем показать лишнее.
    ///
    /// Примечание нормализуется: пустое или из одних пробелов становится <c>null</c>, иначе надпись
    /// о несохранённом горела бы на пустом поле.
    /// </remarks>
    /// <returns><c>null</c>, если сохранять нечего.</returns>
    private TrainingSession? BuildSession(TrainingPlan plan)
    {
        var session = new TrainingSession
        {
            Id = _existing?.Id ?? 0,
            Date = Day,
            PlanId = plan.Id,
            PlanName = plan.Name,
        };

        foreach (var row in Exercises)
        {
            var sets = row.Sets.Where(set => set.HasData).ToList();

            if (sets.Count == 0 && !row.HasNote)
            {
                continue;
            }

            var entry = new ExerciseEntry
            {
                ExerciseId = row.Exercise.Id,
                ExerciseName = row.Name,
                Notes = NormalizeNote(row.NoteText),
            };

            foreach (var set in sets)
            {
                if (!set.TryParseWeight(out var weight) || !set.TryParseRepetitions(out var repetitions))
                {
                    // Разбор проверяется CanAccept, сюда с негодным текстом не дойти.
                    return null;
                }

                entry.AddSet(repetitions, weight);
            }

            entry.NormalizeOrder();
            session.AddExercise(entry);
        }

        session.NormalizeOrder();

        return session;
    }

    private async Task<bool> AddAsync(TrainingSession session)
    {
        var outcome = await _sessionRepository.AddAsync(session).ConfigureAwait(true);

        switch (outcome)
        {
            case AddTrainingSessionOutcome.Added:
                return true;

            case AddTrainingSessionOutcome.DateTaken:
                Status = "За эту дату уже есть запись. Выберите другую дату.";
                return false;

            default:
                Status = "Не выбрано название плана.";
                return false;
        }
    }

    private async Task<bool> UpdateAsync(TrainingSession session)
    {
        var outcome = await _sessionRepository.UpdateAsync(session).ConfigureAwait(true);

        if (outcome == UpdateTrainingSessionOutcome.Updated)
        {
            return true;
        }

        Status = "Запись за эту дату удалили, пока окно было открыто. Сохраните её заново.";
        return false;
    }
}
