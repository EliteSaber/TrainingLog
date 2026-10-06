using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.ViewModels;

/// <summary>
/// Окно добавления дня тренировки: дата, план и подходы по упражнениям плана.
/// </summary>
/// <remarks>
/// Окно одно и на добавление, и на правку: различаются только запись, которая за этой датой
/// уже есть, и команда сохранения. Отдельного окна правки не заводим, потому что дата —
/// единственный способ найти запись, а он и так в этом окне.
///
/// Состав дня задаётся планом: кнопки упражнений перечисляют упражнения плана по порядку
/// выполнения, и в записях журнала не может оказаться упражнения, которого в плане нет.
///
/// Сохранения два, и это не дублирование: «Сохранить» пишет день и оставляет окно открытым,
/// «Сохранить и закрыть» — то же плюс закрытие. Одна кнопка на оба случая означала бы, что
/// сохранить без закрытия нельзя, а при наборе дня это обычное дело.
/// </remarks>
public sealed partial class AddDayViewModel : ObservableObject
{
    private readonly ITrainingSessionRepository _sessionRepository;
    private readonly ITrainingPlanRepository _planRepository;

    /// <summary>
    /// Запись за текущую дату, если она уже есть. Её наличие переводит окно в режим правки.
    /// </summary>
    private TrainingSession? _existing;

    /// <summary>
    /// День, для которого запись уже подтянута.
    /// </summary>
    /// <remarks>
    /// <c>DatePicker</c> присоединяется со своим значением по умолчанию и двусторонней
    /// привязкой пишет его в источник, поэтому при открытии окна дата меняется один раз
    /// сама по себе. Без этой отметки запись читалась бы дважды, и первый проход успевал бы
    /// обнулить упражнения и написать в Status «план удалён», пока планы ещё не загружены.
    /// </remarks>
    private DateOnly? _loadedDay;

    /// <summary>
    /// Снимок того, что лежит в базе за текущий день: <see cref="HasUnsavedChanges"/> сравнивает
    /// с ним набранное.
    /// </summary>
    /// <remarks>
    /// Именно снимок того, что сохраняется, а не то, что показывает форма. Форма показывает
    /// все упражнения плана, а запись — только те, где есть подходы, и сравнение «строка формы
    /// против упражнений плана» сочло бы изменением каждый непустой план, даже не тронутый.
    /// <c>null</c> означает, что записи за день нет: тогда любое набранное — изменение.
    /// </remarks>
    private TrainingSession? _savedSnapshot;

    /// <summary>
    /// Подавляет пересчёт «есть несохранённое», пока окно наполняется сверху.
    /// </summary>
    /// <remarks>
    /// Наполнение идёт построчно: упражнение очищается, потом заново заполняется, и между
    /// этими шагами в форме на миг не остаётся ничего. Сравнение с записью на этот миг
    /// честно отвечает «отличается», и надпись вспыхивает по ходу загрузки — а на пустом дне
    /// так и остаётся загоревшейся.
    ///
    /// Тот же приём, что <c>_isApplyingChecks</c> в <c>EditPlanViewModel</c>: пока состояние
    /// задаётся сверху, пересчёт не нужен, а в конце перезаливки Refresh зовётся один раз и
    /// приходит уже с готовым значением.
    /// </remarks>
    private bool _isApplyingSets;

    /// <param name="sessionRepository">Журнал тренировок.</param>
    /// <param name="planRepository">Планы тренировок: состав дня берётся из плана.</param>
    public AddDayViewModel(ITrainingSessionRepository sessionRepository, ITrainingPlanRepository planRepository)
    {
        ArgumentNullException.ThrowIfNull(sessionRepository);
        ArgumentNullException.ThrowIfNull(planRepository);

        _sessionRepository = sessionRepository;
        _planRepository = planRepository;
    }

    /// <summary>Планы для выбора, по алфавиту.</summary>
    public ObservableCollection<TrainingPlan> Plans { get; } = [];

    /// <summary>Упражнения выбранного плана в порядке выполнения: кнопки окна.</summary>
    public ObservableCollection<PlanExerciseViewModel> Exercises { get; } = [];

    /// <summary>
    /// Дата в окне. Тип <see cref="DateTime"/> — из-за <c>DatePicker</c>, который по
    /// <see cref="DateOnly"/> не привязывается; доменная дата получается через
    /// <see cref="Day"/>.
    /// </summary>
    [ObservableProperty]
    private DateTime? _date = DateTime.Today;

    /// <summary>Выбранный план.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAndCloseCommand))]
    private TrainingPlan? _selectedPlan;

    /// <summary>
    /// Номер активного упражнения, с нуля.
    /// </summary>
    /// <remarks>
    /// Навигация уведомляется ещё и в <see cref="Refresh"/>: одного этого атрибута мало,
    /// потому что при пересборке состава индекс не меняется, а состав меняется, и границы
    /// «‹» и «›» уезжают.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private int _currentIndex;

    /// <summary>
    /// Сообщение об отказе сохранения. Пусто — отказа не было.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    /// <summary>
    /// Признак, что изменения приняты и окно должно закрыться. Окно выставляет
    /// <c>DialogResult</c> по нему.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Было ли что-то сохранено за время работы окна.
    /// </summary>
    /// <remarks>
    /// Отдельное от <see cref="Accepted"/> состояние, и это не дублирование: «Сохранить» не
    /// закрывает окно, поэтому закрыть его потом можно крестиком или Esc, и
    /// <c>DialogResult</c> останется <c>null</c>. По одному <see cref="Accepted"/> главное
    /// окно тогда не узнает, что день сохранён, и не перечитает журнал.
    /// </remarks>
    public bool Saved { get; private set; }

    /// <summary>Есть ли непустое сообщение об отказе.</summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    /// <summary>
    /// Есть ли в окне то, чего нет в базе.
    /// </summary>
    /// <remarks>
    /// Прямое сравнение с <see cref="_savedSnapshot"/>, а не признак «пользователь что-то
    /// трогал»: надпись должна гаснуть, когда внесли значение и стерели его обратно. В отличие
    /// от <c>CanAccept</c>, изменения здесь и есть условие — та про пригодность данных.
    ///
    /// Негодный текст тоже считается изменением, иначе надпись молчала бы ровно тогда, когда
    /// кнопки сохранения погасли: <see cref="BuildSession"/> на мусоре возвращает <c>null</c>,
    /// и сравнивать нечего. Пользователь должен видеть, что в окне есть то, что не уйдёт.
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
                ? Exercises.Any(row => row.Sets.Any(set => set.HasData))
                : Differs(_savedSnapshot, current);
        }
    }

    /// <summary>
    /// Дата в доменном виде. Пустое поле даёт сегодняшний день: сохранить день без даты
    /// нельзя, а оставлять дату пустой значило бы записать его не туда.
    /// </summary>
    public DateOnly Day => DateOnly.FromDateTime(Date ?? DateTime.Today);

    /// <summary>Активное упражнение: его подходы показаны в центре окна.</summary>
    public PlanExerciseViewModel? Current =>
        CurrentIndex >= 0 && CurrentIndex < Exercises.Count ? Exercises[CurrentIndex] : null;

    /// <summary>
    /// Заголовок окна: на добавление или на правку — по тому, есть ли уже запись за дату.
    /// </summary>
    public string Title => _existing is null ? "Добавить день" : "Правка дня";

    /// <summary>
    /// Дата изменилась — читаем запись за неё.
    /// </summary>
    /// <remarks>
    /// Тот же день перечитывать незачем: иначе собственное значение <c>DatePicker</c> при
    /// открытии окна запускало бы второе чтение вхолостую.
    /// </remarks>
    partial void OnDateChanged(DateTime? value)
    {
        var day = DateOnly.FromDateTime(value ?? DateTime.Today);

        if (_loadedDay != day)
        {
            LoadDateCommand.Execute(null);
        }
    }

    /// <summary>
    /// Смена плана пересобирает упражнения и наполняет их подходами заново.
    /// </summary>
    partial void OnSelectedPlanChanged(TrainingPlan? value) => RebuildWindow();

    /// <summary>
    /// Подставляет ли подходы записи в строки упражнений.
    /// </summary>
    /// <remarks>
    /// Запись за дату принадлежит одному плану, и подходы относятся к нему. Пока выбран
    /// другой, показывать их нельзя: упражнение может входить в оба плана, и тогда в полях
    /// чужого плана появились бы подходы из записи — выглядело бы как «переключил план, а
    /// данные остались».
    ///
    /// Идентификатор плана сравнивается, а не название: планы переименовывают, и совпадение
    /// названий ничего бы не значило.
    /// </remarks>
    private bool ShowsStoredSets => _existing is { PlanId: > 0 } stored && stored.PlanId == SelectedPlan?.Id;

    partial void OnCurrentIndexChanged(int value) => Refresh();

    /// <summary>
    /// Загружает планы. Вызывается окном при загрузке.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        var plans = await _planRepository.GetAllAsync().ConfigureAwait(true);

        Plans.Clear();

        // Порядок тот же, что в окне планов: сравнение кодов Unicode совпадает с тем, что
        // делает база, и даёт русский алфавит.
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
    /// Запись за дату одна, поэтому окно само знает, добавление это или правка, и вызывающему
    /// не нужно передавать режим: иначе дата, изменённая в окне после открытия, осталась бы
    /// в режиме добавления и упёрлась бы в занятую дату.
    ///
    /// Сначала план, потом сброс активного упражнения, потом подходы: смена плана перестраивает
    /// кнопки упражнений, и подходы наполняются уже на готовых строках.
    /// </remarks>
    [RelayCommand]
    private async Task LoadDateAsync()
    {
        var requestedDay = Day;

        var existing = await _sessionRepository.GetByDateAsync(requestedDay).ConfigureAwait(true);

        // Пока читалось, дату могли поменять: запросы возвращаются не по порядку, и без
        // этой проверки в окне остались бы подходы чужого дня.
        if (requestedDay != Day)
        {
            return;
        }

        _loadedDay = requestedDay;
        _existing = existing;

        // Снимок берётся с прочитанной записи, а не собирается заново из полей: так эталоном
        // становится то, что действительно лежит в базе. Смена даты заменяет снимок всегда,
        // поэтому надпись гаснет, а несохранённое прошлого дня теряется — так же, как оно
        // теряется без всякой надписи.
        //
        // Присваивается до перестройки окна. Само по себе это страховка, а не лечение: пока состояние
        // задаётся сверху, пересчёт подавлен, и промежуточные значения в привязку не уходят.
        // Но эталон обязан стоять раньше любого уведомления, которое его читает, иначе новая
        // правка в этом месте снова получит старое значение.
        _savedSnapshot = existing;

        var plan = SelectPlan(existing);

        Status = existing is null || plan is not null
            ? string.Empty
            : "План этой записи удалён из справочника: выберите план, чтобы записать день заново.";

        SelectedPlan = plan;

        // Перестройка окна идёт под смену плана и под смену даты. Сеттер SelectedPlan сработает
        // не всегда: у двух дат план один и тот же, значение не меняется, OnSelectedPlanChanged
        // не вызывается — и сброс активного упражнения, и наполнение подходов тогда не
        // случатся. Поэтому здесь они зовутся явно, а повторный вызов через сеттер безвреден:
        // обе операции идемпотентны.
        RebuildWindow();

        OnPropertyChanged(nameof(Title));
    }

    /// <summary>
    /// Находит план записи среди доступных.
    /// </summary>
    /// <remarks>
    /// По идентификатору, а не по названию: план могли переименовать, а подставить другой
    /// план по совпавшему названию значило бы записать день не в тот план.
    /// </remarks>
    private TrainingPlan? SelectPlan(TrainingSession? session) =>
        session is null ? null : Plans.SingleOrDefault(plan => plan.Id == session.PlanId);

    /// <summary>
    /// Заполняет подходы упражнений содержимым записи за дату либо очищает их.
    /// </summary>
    /// <remarks>
    /// Очистка обязательна и при переходе на дату без записи: смена даты не должна оставлять
    /// в полях то, что было набрано для другой даты, — иначе в запись попало бы то, чего
    /// пользователь туда не вносил. По той же причине очистка нужна при смене плана.
    ///
    /// Идемпотентна: зовётся и из <see cref="OnSelectedPlanChanged"/>, и из
    /// <see cref="LoadDateAsync"/>, а пересекаются они при загрузке дня — там сеттер плана
    /// успевает позвать её раньше, чем сработает следующая строка. Дубликат безвреден: поля
    /// очищаются и заполняются заново из той же записи.
    ///
    /// Упражнение, удалённое из плана или из справочника, в окне не показать: кнопки
    /// перечисляют упражнения плана. На сохранении оно выпадет из записи — так же, как
    /// упражнение, удалённое из справочника, у состава плана.
    /// </remarks>
    private void ApplyExistingSets()
    {
        var stored = ShowsStoredSets ? _existing : null;

        foreach (var row in Exercises)
        {
            var entry = stored?.Exercises.SingleOrDefault(item => item.ExerciseId == row.Exercise.Id);

            row.Sets.Clear();

            if (entry is not null)
            {
                foreach (var set in entry.Sets)
                {
                    // Форматируется по текущей культуре: в русской раскладке десятичный
                    // разделитель — запятая, и это ровно тот вид, в котором поле разбирается
                    // обратно.
                    row.Sets.Add(new SetInputViewModel
                    {
                        WeightText = set.Weight.ToString("0.##", CultureInfo.CurrentCulture),
                        RepetitionText = set.Repetitions.ToString(CultureInfo.CurrentCulture),
                    });
                }
            }

            // Пустое упражнение всё равно получает одно пустое поле: кнопки «+» и «−» работают
            // с ним так же, как с непустым, иначе подход негде было бы набрать.
            if (row.Sets.Count == 0)
            {
                row.Sets.Add(new SetInputViewModel());
            }
        }

        Refresh();
    }

    /// <summary>
    /// Собирает упражнения выбранного плана заново: смена плана переписывает состав дня,
    /// иначе подходы старого плана остались бы в новом.
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

        // Активным по умолчанию становится первое упражнение плана: с пустым выбором
        // открывать окно незачем, а выбирать первое вручную — лишний шаг.
        SelectFirstExercise();
    }

    /// <summary>
    /// Перестраивает окно под текущий план и подходы, не показывая промежуточных состояний.
    /// </summary>
    /// <remarks>
    /// Подавление должно охватывать обе операции целиком, а не каждую по отдельности: между
    /// <see cref="BuildExercises"/> и <see cref="ApplyExistingSets"/> форма ещё пуста, и признак
    /// «есть несохранённое» на этот миг честно отвечает «отличается» — надпись вспыхивает.
    /// Тот же приём, что <c>_isApplyingChecks</c> в <c>EditPlanViewModel</c>.
    /// </remarks>
    private void RebuildWindow()
    {
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

    /// <summary>
    /// Подписывает состав на изменения: появление и исчезновение подходов, а также ввод в них.
    /// </summary>
    /// <remarks>
    /// Подписка на все упражнения, а не на видимое, и это не разница в оптимизации. Снимок и
    /// признак «есть несохранённое» считаются по всему дню, а не по показанному упражнению:
    /// подписка только на видимое означала, что ввод в другое упражнение не обновлял ничего —
    /// надпись молчала до переключения упражнения, где счёт перезапускался заново.
    ///
    /// Второе следствие правильнее прежнего: кнопки «+» и «−» перестали быть серыми на
    /// невидимых упражнениях. Упражнение не выбрано только потому, что на него не
    /// переключились, и набирать подходы в нём можно.
    ///
    /// Отписка снимает и подписки на подходы: у строк свой состав, и на новый набор они
    /// навешиваются заново.
    /// </remarks>
    private void SubscribeToRows()
    {
        foreach (var row in Exercises)
        {
            row.Sets.CollectionChanged += OnSetsCollectionChanged;

            foreach (var set in row.Sets)
            {
                set.PropertyChanged += OnSetPropertyChanged;
            }
        }
    }

    /// <summary>
    /// Снимает подписки со всех упражнений и их подходов.
    /// </summary>
    private void UnsubscribeFromRows()
    {
        foreach (var row in Exercises)
        {
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
        // сохранения, ни надпись. Снятие подписки перед навешиванием — чтобы повторный вызов
        // не накопился.
        foreach (var set in sets)
        {
            set.PropertyChanged -= OnSetPropertyChanged;
            set.PropertyChanged += OnSetPropertyChanged;
        }

        Refresh();
    }

    /// <summary>
    /// Любое изменение подхода пересчитывает окно целиком.
    /// </summary>
    /// <remarks>
    /// Безусловно, а не по списку свойств: подход хранит значения строками, и меняются
    /// <c>WeightText</c> и <c>RepetitionText</c>, а <c>IsValid</c> и <c>HasData</c> — их
    /// производные, поднятые ради одной проверки. Перечислить проверяемые здесь свойства —
    /// значило бы забыть новое поле и получить молчащую надпись на «+» или «−». Пересчёт идёт
    /// через <see cref="Refresh"/>, где живут и обе кнопки сохранения, и надпись.
    /// </remarks>
    private void OnSetPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    /// <summary>
    /// Приводит окно в согласованное состояние: текущее упражнение, отметки активности,
    /// подписки на ввод и активность команд.
    /// </summary>
    /// <remarks>
    /// Здесь же уведомляется <see cref="Current"/>, и это обязательно: привязки
    /// <c>Current.Sets</c> и <c>Current.Name</c> подписываются на уведомление об этом свойстве,
    /// а подписаться глубже они не могут, пока оно <c>null</c>, — путь <c>Exercises</c> в
    /// привязке не участвует, и его <c>CollectionChanged</c> привязку не будит. Уведомление
    /// на <c>CurrentIndex</c> не годилось: при смене плана или при открытии окна индекс и
    /// так равен нулю, присваивание ничего не меняет, уведомление не поднимается — и область
    /// подходов остаётся пустой, пока пользователь не переключит упражнение кнопкой.
    ///
    /// Через <see cref="Refresh"/> проходят все изменения состава и набора подходов, поэтому
    /// уведомление о текущем упражнении нельзя забыть в будущей правке.
    ///
    /// Подписки на состав идут в <see cref="SubscribeToRows"/>, а не здесь: Refresh зовётся
    /// часто, и навешивать подписки в нём значило бы каждый раз пересоздавать их заново.
    /// </remarks>
    private void Refresh()
    {
        for (var index = 0; index < Exercises.Count; index++)
        {
            Exercises[index].IsSelected = index == CurrentIndex;
        }

        OnPropertyChanged(nameof(Current));

        AddSetCommand.NotifyCanExecuteChanged();
        RemoveSetCommand.NotifyCanExecuteChanged();

        // Обе кнопки сохранения проверяют одно и то же условие, поэтому обновляются вместе:
        // «Сохранить» на Ctrl+S не обновлять нельзя — иначе после негодного ввода горячая
        // клавиша звала бы сохранение при серой кнопке.
        SaveCommand.NotifyCanExecuteChanged();
        SaveAndCloseCommand.NotifyCanExecuteChanged();

        // Навигация по упражнениям здесь обязательна, а не только на CurrentIndex. Через сеттер
        // индекса она обновляется лишь при реальной смене значения, а при открытии окна индекс
        // и так ноль: BuildExercises присваивает CurrentIndex = 0, [ObservableProperty] молчит,
        // и WPF остаётся с тем CanExecute, что спросил до загрузки упражнений. «‹» и «›»
        // оставались серыми до нажатия на кнопку с номером.
PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();

        // Признак несохранённого пересчитывается здесь же: он зависит от состава и от ввода, а
        // оба меняются через Refresh. Через [ObservableProperty] он бы не поднялся — сравнение
        // не привязано ни к одному свойству.
        //
        // На время наполнения сверху уведомление подавлено: промежуточные состояния не
        // соответствуют ни одному тому, что увидит пользователь.
        if (!_isApplyingSets)
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    /// <summary>
    /// Смещается ли активное упражнение на <paramref name="delta"/> позиций.
    /// </summary>
    /// <remarks>
    /// Проверка и выполнение разделены, как у любой команды [RelayCommand], поэтому границу
    /// приходится повторять в самом переходе — иначе команда, вызванная программно, уводила бы
    /// индекс за пределы списка. Обе половины живут рядом, чтобы не разъехались.
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

    /// <summary>
    /// Сбрасывает активное упражнение на первое.
    /// </summary>
    /// <remarks>
    /// Отдельный метод, а не присваивание в <c>LoadDateAsync</c>, потому что свойство
    /// <c>CurrentIndex</c> само уведомление не поднимет: в день, где упражнений больше одного,
    /// индекс уже 0, и присваивание ничего не меняет. <see cref="Refresh"/> звать нужно явно —
    /// иначе отметки активности и подписки на подходы остались бы от прежнего упражнения.
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
    /// Добавляет подход активному упражнению. Вес нового подхода — вес предыдущего:
    /// подходы одного упражнения отличаются повторениями гораздо чаще, чем весом.
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

    /// <summary>
    /// Убирает самый правый подход активного упражнения.
    /// </summary>
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

    /// <summary>
    /// Обе кнопки сохранения активны, когда день есть чем наполнить и в полях нет мусора.
    /// </summary>
    /// <remarks>
    /// Требование «изменилось что-то» здесь не годится: день, набранный заново на ту же дату,
    /// должен сохраняться, а проверка сравнения с прошлой редакцией потребовала бы хранить
    /// снимок исходных значений. Вместо этого проверяется пригодность: пустой день сохранять
    /// нечего, а негодный текст в поле разбирать нечем.
    /// </remarks>
    private bool CanAccept() =>
        SelectedPlan is not null
        && Exercises.Any(row => row.Sets.Any(set => set.HasData))
        && Exercises.All(row => row.Sets.All(set => set.IsValid));

    /// <summary>
    /// Сохраняет день и закрывает окно.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAccept))]
    private Task SaveAndCloseAsync() => SaveInternalAsync(close: true);

    /// <summary>
    /// Сохраняет день и оставляет окно открытым.
    /// </summary>
    /// <remarks>
    /// У этой кнопки <see cref="Accepted"/> не выставляется: окно после неё остаётся открытым,
    /// и закрыть его можно крестиком или Esc. Повторное сохранение идёт уже как правка — см.
    /// <see cref="SaveInternalAsync"/>.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanAccept))]
    private Task SaveAsync() => SaveInternalAsync(close: false);

    /// <summary>
    /// Сохраняет день, при <paramref name="close"/> — закрывает окно.
    /// </summary>
    /// <remarks>
    /// После сохранения без закрытия присваивается <c>_existing</c>, и это не косметика: запись
    /// уже в базе, поэтому второе нажатие «Сохранить» без этого пошло бы по ветке
    /// <c>AddAsync</c> и упёрлось бы в занятую дату — день второй раз не сохранился бы, а в
    /// Status появилось бы «за эту дату уже есть запись». Состояние «день уже записан» должно
    /// следовать за первым же сохранением, а не за закрытием окна. Идентификатор берётся из
    /// <paramref name="session"/>, которому репозиторий его уже присвоил: у «Сохранить и
    /// закрыть» это безразлично, а вот на «Сохранить» модель живёт дальше.
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

        // Сброс сообщения об отказе: после успешного сохранения над полями не должно висеть
        // «за эту дату уже есть запись» от прошлой попытки.
        Status = string.Empty;

        // Присваивается вся собранная запись, а не её копия: у неё уже есть идентификатор, который
        // репозиторий записал при AddAsync, и следующее сохранение должно уйти в UpdateAsync по
        // нему. На «Сохранить и закрыть» это безразлично — окно сейчас закроется.
        _existing = session;

        // Снимок обновляется здесь же, а не по закрытию окна: надпись «Не сохранено» должна
        // погаснуть сразу после сохранения, а снимок — ровно то, что теперь лежит в базе.
        // Уведомление идёт через Refresh, который вызывается ниже через OnPropertyChanged(Title).
        _savedSnapshot = session;

        Saved = true;

        if (close)
        {
            Accepted = true;
        }

        // Приватные сеттеры не дают сгенерировать уведомления автоматически, а окно ждёт их:
        // по одному значению Accepted вызывающий не узнает, что день сохранён и пора закрывать
        // окно. Title — по той же причине: после сохранения без закрытия заголовок меняется с
        // «Добавить день» на «Правка дня», и в базе это теперь правка.
        OnPropertyChanged(nameof(Accepted));
        OnPropertyChanged(nameof(Saved));
        OnPropertyChanged(nameof(Title));

        Refresh();
    }

    /// <summary>
    /// Отличается ли набранное от того, что лежит в базе.
    /// </summary>
    /// <remarks>
    /// Отдельная чистая функция, а не только вычисляемое свойство: правило сравнения многослойное
    /// (план, состав, подходы по порядку) и его надо проверить тестом, не поднимая окно.
    ///
    /// Сравнивается по порядку, а не как множество: перестановка подходов меняет день, и
    /// сравнение множеств её пропустило бы — ровно как в <c>EditPlanViewModel</c>.
    ///
    /// Обе стороны приходят из <c>BuildSession</c>, то есть уже разобранные числа, и потому
    /// сравнение одинаково видит и «60» → «60,0», и перестановку. Разбор на каждый введённый
    /// символ — плата за простоту: подходов в дне десятки, а не тысячи.
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

    /// <summary>
    /// Совпадают ли упражнения и подходы записей.
    /// </summary>
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

        return saved.Sets.Count == current.Sets.Count
            && saved.Sets.Zip(current.Sets, SetsEqual).All(equal => equal);
    }

    private static bool SetsEqual(TrainingSet saved, TrainingSet current) =>
        saved.Order == current.Order
        && saved.Repetitions == current.Repetitions
        && saved.Weight == current.Weight;

    /// <summary>
    /// Собирает запись дня из введённых подходов.
    /// </summary>
    /// <remarks>
    /// Упражнение без введённых подходов в запись не попадает: план выполнен не целиком,
    /// а невыполненные упражнения в журнале только шумят. Идентификатор упражнения ставится
    /// рядом с копией названия, чтобы запись знала и справочник, и своё название.
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

            if (sets.Count == 0)
            {
                continue;
            }

            var entry = new ExerciseEntry
            {
                ExerciseId = row.Exercise.Id,
                ExerciseName = row.Name,
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