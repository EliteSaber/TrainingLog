using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.ViewModels;

/// <summary>
/// Правка одного плана в модальном окне. Окно общее для добавления и правки: различаются
/// только набор планов для проверки дубля и команда, которая сохраняет.
/// </summary>
public sealed partial class EditPlanViewModel : ObservableObject
{
    private readonly TrainingPlan _plan;
    private readonly bool _isNew;
    private readonly ITrainingPlanRepository _planRepository;
    private readonly IExerciseRepository _exerciseRepository;

    /// <summary>
    /// Остальные планы. Нужны для проверки дубликата без обращения к базе на каждое нажатие
    /// клавиши; собственный план плана правки в список не попадает.
    /// </summary>
    private readonly ObservableCollection<TrainingPlan> _otherPlans = [];

    /// <summary>
    /// Упражнения справочника по алфавиту с отметками вхождения в план.
    /// </summary>
    /// <remarks>
    /// Порядок показыва по алфавиту, а не по порядку выполнения: по алфавиту упражнение
    /// находится глазами, и это единственный способ что-то найти в справочнике. Порядок
    /// выполнения живёт в <see cref="SelectedExercises"/>.
    /// </remarks>
    private readonly ObservableCollection<PlanExerciseSelection> _allExercises = [];

    /// <summary>Упражнения, входящие в план, в порядке выполнения.</summary>
    private readonly ObservableCollection<PlanExerciseSelection> _selectedExercises = [];

    /// <summary>
    /// Исходный состав плана в порядке выполнения. По нему видно, что изменилось: без него
    /// наименование пришлось бы менять, чтобы сохранить новый состав или новый порядок.
    /// </summary>
    private readonly List<int> _initialExerciseIds = [];

    /// <summary>
    /// Подавляет пересчёт главного чекбокса и активности «Принять», пока состояние задаётся
    /// сверху. Без него команда «Выбрать все» порождала бы встречный пересчёт на каждой строке.
    /// </summary>
    private bool _isApplyingChecks;

    /// <param name="plan">План, который правят. Для добавления — новая запись без идентификатора.</param>
    /// <param name="isNew"><c>true</c>, если окно открыто на добавление.</param>
    /// <param name="planRepository">Хранилище планов.</param>
    /// <param name="exerciseRepository">Справочник упражнений.</param>
    public EditPlanViewModel(
        TrainingPlan plan,
        bool isNew,
        ITrainingPlanRepository planRepository,
        IExerciseRepository exerciseRepository)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(planRepository);
        ArgumentNullException.ThrowIfNull(exerciseRepository);

        _plan = plan;
        _isNew = isNew;
        _planRepository = planRepository;
        _exerciseRepository = exerciseRepository;

        Name = plan.Name;

        // Подписка на состав, а не на отметки строк справочника: перестановка меняет
        // только порядок в этой коллекции, и «Принять» должна это видеть.
        _selectedExercises.CollectionChanged += OnSelectedCollectionChanged;
    }

    /// <summary>Наименование плана в поле ввода.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _name = string.Empty;

    /// <summary>
    /// Состояние чекбокса «Выбрать все»: <c>true</c> — отмечены все, <c>false</c> — не отмечена
    /// ни одна, <c>null</c> — отмечена часть строк.
    /// </summary>
    [ObservableProperty]
    private bool? _isAllChecked;

    /// <summary>Все упражнения справочника с отметками вхождения в план.</summary>
    public ObservableCollection<PlanExerciseSelection> AllExercises => _allExercises;

    /// <summary>Упражнения плана в порядке выполнения, с номерами.</summary>
    public ObservableCollection<PlanExerciseSelection> SelectedExercises => _selectedExercises;

    /// <summary>
    /// Признак, что изменения приняты и сохранены. Окно выставляет <c>DialogResult</c> по нему.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Подпись кнопки подтверждения: у добавления и правки она разная, а окно одно.
    /// </summary>
    public string AcceptText => _isNew ? "Добавить" : "Принять";

    /// <summary>
    /// Пересчитывает состояние чекбокса «Выбрать все» по отметкам строк.
    /// </summary>
    /// <param name="checkedCount">Сколько строк отмечено.</param>
    /// <param name="total">Сколько строк всего.</param>
    /// <returns>
    /// <c>true</c>, если отмечены все строки, <c>false</c>, если не отмечена ни одна,
    /// иначе <c>null</c> — отмечена часть. Пустой чекбокс считается снятым: отмечать нечего.
    /// </returns>
    public static bool? RecomputeMaster(int checkedCount, int total) =>
        checkedCount == 0
            ? false
            : checkedCount == total
                ? true
                : null;

    /// <summary>
    /// Сдвигает элемент списка на <paramref name="delta"/> позиций.
    /// </summary>
    /// <param name="items">Список, который меняется на месте.</param>
    /// <param name="index">Индекс сдвигаемого элемента.</param>
    /// <param name="delta">Сдвиг: меньше нуля — вверх, больше — вниз.</param>
    /// <returns>Новый индекс элемента либо <c>-1</c>, если сдвиг невозможен.</returns>
    /// <remarks>
    /// Отдельная функция без привязки к коллекции и командам: правило сдвига проверяется
    /// тестом, а с коллекциейObservableCollection и команд это уже не проверить.
    /// </remarks>
    public static int Move<T>(IList<T> items, int index, int delta)
    {
        ArgumentNullException.ThrowIfNull(items);

        var target = index + delta;

        if (index < 0 || index >= items.Count || target < 0 || target >= items.Count || delta == 0)
        {
            return -1;
        }

        // Элемент берётся до удаления: после RemoveAt по индексу лежит уже следующий.
        var item = items[index];

        items.RemoveAt(index);
        items.Insert(target, item);

        return target;
    }

    partial void OnIsAllCheckedChanged(bool? value)
    {
        if (_isApplyingChecks)
        {
            return;
        }

        var isChecked = value == true;

        _isApplyingChecks = true;

        try
        {
            foreach (var row in _allExercises)
            {
                SetChecked(row, isChecked);
            }
        }
        finally
        {
            _isApplyingChecks = false;
        }

        // Отметки строк не пересчитывают активность «Принять» сами, пока состояние задаётся
        // сверху, поэтому пересчитываем её здесь — иначе «Выбрать все» не разбудит кнопку.
        AcceptCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// «Принять» активна, только если наименование пригодно и есть что сохранять.
    /// </summary>
    /// <remarks>
    /// Наименование проверяется одинаково для добавления и правки: непустое, в лимите и не
    /// занятое другим планом. Требование «наименование изменилось» здесь не годится: правка
    /// состава упражнений с сохранением прежнего названия — обычное дело, и «Принять» была бы
    /// серой. Своё название дублем не считает и в этой проверке — сам редактируемый план в
    /// список остальных не входит.
    ///
    /// Состав и порядок на активность не влияют: пустой план допустим, его можно наполнить
    /// позже.
    /// </remarks>
    private bool CanAccept() =>
        NameRules.IsValid(Name, _otherPlans.Select(plan => plan.Name))
        && (_isNew || IsNameChanged() || IsCompositionChanged());

    /// <summary>
    /// Изменилось ли наименование. Сравнение по регистру, как и в правиле: добавленные пробелы
    /// изменением не считаются, а смена регистра — считается.
    /// </summary>
    private bool IsNameChanged() =>
        !string.Equals(Name?.Trim(), _plan.Name?.Trim(), StringComparison.Ordinal);

    /// <summary>
    /// Изменился ли состав или порядок упражнений.
    /// </summary>
    /// <remarks>
    /// Сравниваются последовательности, а не множества: перестановка упражнений тоже
    /// изменение плана, иначе кнопка осталась бы серой при переставленных упражнениях.
    /// </remarks>
    private bool IsCompositionChanged() =>
        !_selectedExercises.Select(row => row.Exercise.Id).SequenceEqual(_initialExerciseIds);

    private bool CanMoveUp(PlanExerciseSelection? row) =>
        row is not null && _selectedExercises.IndexOf(row) > 0;

    private bool CanMoveDown(PlanExerciseSelection? row)
    {
        if (row is null)
        {
            return false;
        }

        var index = _selectedExercises.IndexOf(row);

        return index >= 0 && index < _selectedExercises.Count - 1;
    }

    /// <summary>
    /// Убрать можно любую строку состава, даже единственную и даже последнюю.
    /// </summary>
    private bool CanRemove(PlanExerciseSelection? row) => row is not null && _selectedExercises.Contains(row);

    /// <summary>
    /// Загружает остальные планы для проверки дубля, справочник и текущий состав плана.
    /// Вызывается окном при загрузке.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        var plans = await _planRepository.GetAllAsync().ConfigureAwait(true);

        _otherPlans.Clear();
        foreach (var plan in plans)
        {
            if (plan.Id != _plan.Id)
            {
                _otherPlans.Add(plan);
            }
        }

        var exercises = await _exerciseRepository.GetAllAsync().ConfigureAwait(true);

        // Порядок выполнения сохраняем: план отдаёт упражнения упорядоченными, и состав
        // правки должен открыться именно в том виде, в каком будет сохранён.
        _initialExerciseIds.Clear();
        foreach (var exercise in _plan.Exercises)
        {
            _initialExerciseIds.Add(exercise.Id);
        }

        var rowsById = new Dictionary<int, PlanExerciseSelection>();

        _allExercises.Clear();

        // Порядок как в справочнике по умолчанию: то же сравнение кодов Unicode, что и в
        // ExercisesViewModel, иначе одна и та же строка в двух окнах встала бы по-разному.
        foreach (var exercise in exercises.OrderBy(exercise => exercise.Name, StringComparer.Ordinal))
        {
            var row = new PlanExerciseSelection(exercise);

            // Отметка ставится до подписки: иначе сработал бы обработчик, который добавил бы
            // строку в конец состава, и порядок плана рассыпался бы ещё до показа окна.
            if (_initialExerciseIds.Contains(exercise.Id))
            {
                row.IsChecked = true;
            }

            // Подписка на весь срок жизни модели: строки принадлежат ей же и уходят вместе
            // с ней, поэтому отписка не нужна и не пропустит событие мимо.
            row.PropertyChanged += OnRowPropertyChanged;

            _allExercises.Add(row);
            rowsById[exercise.Id] = row;
        }

        // Состав собирается по порядку плана, а не по алфавиту справочника.
        _selectedExercises.Clear();

        foreach (var id in _initialExerciseIds)
        {
            if (rowsById.TryGetValue(id, out var row))
            {
                _selectedExercises.Add(row);
            }
        }

        RecalculatePositions();
        RefreshMoveCommands();
        NotifyMasterChanged();

        AcceptCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp(PlanExerciseSelection? row) => Move(row, -1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown(PlanExerciseSelection? row) => Move(row, 1);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove(PlanExerciseSelection? row)
    {
        if (row is null || !_selectedExercises.Contains(row))
        {
            return;
        }

        _selectedExercises.Remove(row);
        SetChecked(row, false);
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private async Task AcceptAsync()
    {
        var candidate = new TrainingPlan { Id = _plan.Id, Name = Name };

        foreach (var row in _selectedExercises)
        {
            candidate.AddExercise(row.Exercise);
        }

        var saved = _isNew
            ? await _planRepository.AddAsync(candidate).ConfigureAwait(true) == AddTrainingPlanOutcome.Added
            : await _planRepository.UpdateAsync(candidate).ConfigureAwait(true) == UpdateTrainingPlanOutcome.Updated;

        if (!saved)
        {
            return;
        }

        // Состав упражнений в общий объект плана не пишется: вызывающий перечитывает планы
        // заново, иначе у окна справочника на руках осталась бы одна композиция, а в базе
        // другая. Имя пишем, как это делает окно правки упражнения.
        _plan.Name = candidate.Name;

        Accepted = true;

        // Приватный сеттер не даёт сгенерировать уведомление автоматически, а окно ждёт его,
        // чтобы выставить DialogResult: по одному только значению Accepted оно не узнает,
        // что правка сохранена.
        OnPropertyChanged(nameof(Accepted));
    }

    /// <summary>
    /// Сдвигает упражнение в порядке выполнения.
    /// </summary>
    /// <remarks>
    /// Команды перестановки должны запрещаться на краях списка, а проверка их активности живёт
    /// в <see cref="CanMoveUp"/> и <see cref="CanMoveDown"/>. Реализация команд звать их не
    /// может: у [RelayCommand] проверка и выполнение разделены, и в выполнении она не
    /// вызывается, — поэтому граница проверяется здесь, повторно.
    /// </remarks>
    private void Move(PlanExerciseSelection? row, int delta)
    {
        if (row is null)
        {
            return;
        }

        var index = _selectedExercises.IndexOf(row);

        if (Move(_selectedExercises, index, delta) < 0)
        {
            return;
        }

        RecalculatePositions();
        RefreshMoveCommands();
    }

    /// <summary>
    /// Проставляет номера строкам состава по их месту в списке.
    /// </summary>
    private void RecalculatePositions()
    {
        for (var position = 0; position < _selectedExercises.Count; position++)
        {
            _selectedExercises[position].Position = position + 1;
        }
    }

    private void RefreshMoveCommands()
    {
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Ставит отметку строке и держит состав в согласованном состоянии.
    /// </summary>
    /// <remarks>
    /// Перестановка состава при снятии и возврате отметки не удаляет строку из
    /// <see cref="_allExercises"/>: иначе строка переезжала бы в конец и справочник, а он по
    /// алфавиту — это место для поиска, а не для порядка.
    /// </remarks>
    private void SetChecked(PlanExerciseSelection row, bool isChecked)
    {
        if (row.IsChecked == isChecked)
        {
            return;
        }

        row.IsChecked = isChecked;
        UpdateSelected(row);
    }

    /// <summary>
    /// Приводит состав в согласованное состояние с отметкой строки: отмеченная уходит в
    /// конец порядка выполнения, снятая выходит из состава.
    /// </summary>
    /// <remarks>
    /// Идемпотентна, потому что вызывается и из <see cref="SetChecked"/>, и из обработчика
    /// отметки, а тот срабатывает синхронно во время присваивания свойства. Повторное
    /// добавление той же строки иначе продублировало бы упражнение в плане.
    /// </remarks>
    private void UpdateSelected(PlanExerciseSelection row)
    {
        if (row.IsChecked)
        {
            if (!_selectedExercises.Contains(row))
            {
                _selectedExercises.Add(row);
            }
        }
        else
        {
            _selectedExercises.Remove(row);
        }

        RecalculatePositions();
        RefreshMoveCommands();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingChecks || e.PropertyName != nameof(PlanExerciseSelection.IsChecked))
        {
            return;
        }

        var row = (PlanExerciseSelection)sender!;

        // Отметка извне — пользователь поставил или снял галочку: упражнение уходит в конец
        // порядка выполнения либо выходит из состава, а главный чекбокс пересчитывается.
        UpdateSelected(row);
        NotifyMasterChanged();

        // Состав входит в условие активности «Принять», поэтому галка обязана пересчитать и её:
        // иначе смена состава при неизменном наименовании осталась бы серой кнопкой.
        AcceptCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Состав изменился — перестановкой, удалением или добавлением. Номера и активность
        // команд считает вызывающий, здесь только активность «Принять».
        AcceptCommand.NotifyCanExecuteChanged();
    }

    private void NotifyMasterChanged()
    {
        var checkedCount = 0;

        foreach (var row in _allExercises)
        {
            if (row.IsChecked)
            {
                checkedCount++;
            }
        }

        _isApplyingChecks = true;

        try
        {
            IsAllChecked = RecomputeMaster(checkedCount, _allExercises.Count);
        }
        finally
        {
            _isApplyingChecks = false;
        }
    }
}
