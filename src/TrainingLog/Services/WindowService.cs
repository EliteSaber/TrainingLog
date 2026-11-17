using System.Windows;
using System.Windows.Threading;
using TrainingLog.Core.Models;
using TrainingLog.Windows;

namespace TrainingLog.Services;

/// <summary>Список окон приложения.</summary>
/// <remarks>
/// Окна справочников и окно параметров идут через <see cref="SingleInstanceWindowHost{TWindow}"/>:
/// повторное нажатие фокусирует открытое. Окна правки тоже единственные — по одному на приложение,
/// как окно дня: экземпляр держит сервис, потому что фабрикам нужны упражнение или план, а хост
/// вызывает их без параметров.
///
/// Справочники и параметры — отдельные окна: владельца у них нет, своя кнопка на панели задач
/// есть, и после сворачивания журнала до них можно переключиться. Так же устроено окно дня.
/// Окна правки упражнения, плана и импорта — тоже отдельные и немодальные: модальный диалог
/// блокирует всё приложение, включая «Параметры», и вкладку «Окна» во время правки было бы не
/// открыть. Модальных окон у приложения не осталось — системные «MessageBox» и «Выбрать
/// файл…» к ним не относятся.
///
/// Окно дня единственный своего рода: немодальное, поэтому главное окно при открытом дне
/// остаётся рабочим, и единственное, но без хоста — фабрике окна нужен день, а хост вызывает
/// её без параметров. Повторное открытие выводит существующее окно на передний план с
/// оповещением <see cref="WindowAttention"/>.
///
/// Окно параметров немодальное и открывается при любых других открытых окнах: правило
/// «при открытом окне дня параметры не открываются» отменено — параметры нужны и во время
/// правки дня.
/// </remarks>
public sealed class WindowService(
    Func<ExercisesWindow> exercisesWindowFactory,
    Func<Exercise, EditExerciseWindow> editExerciseWindowFactory,
    Func<ImportExercisesWindow> importExercisesWindowFactory,
    Func<PlansWindow> plansWindowFactory,
    Func<EditPlanWindow> addPlanWindowFactory,
    Func<TrainingPlan, EditPlanWindow> editPlanWindowFactory,
    Func<DateOnly?, AddDayWindow> dayWindowFactory,
    Func<SettingsWindow> settingsWindowFactory) : IWindowService
{
    private readonly SingleInstanceWindowHost<ExercisesWindow> _exercises = new(exercisesWindowFactory, owned: false);
    private readonly SingleInstanceWindowHost<PlansWindow> _plans = new(plansWindowFactory, owned: false);

    /// <summary>
    /// Окно параметров: единственное и немодальное.
    /// </summary>
    /// <remarks>
    /// Подписка на его «Сохранено» — при открытии: хост создаёт окно сам, и момент его
    /// появления известен только хосту.
    /// </remarks>
    private readonly SingleInstanceWindowHost<SettingsWindow> _settings = new(settingsWindowFactory, owned: false);

    /// <summary>Открытое окно дня. <c>null</c> — окна дня нет.</summary>
    /// <remarks>
    /// Одиночный экземпляр окно дня держит сам сервис: хосту фабрика с датой не подходит, а
    /// кроме вывода на передний план здесь же подписка на сохранение и закрытие.
    /// </remarks>
    private AddDayWindow? _dayWindow;

    /// <summary>Открытые окна правки. <c>null</c> — такого окна нет.</summary>
    /// <remarks>
    /// По одному на приложение, как окно дня: второе окно правки того же упражнения или плана
    /// смысла не имеет, а при повторном открытии существующее выводится вперёд.
    /// </remarks>
    private EditExerciseWindow? _exerciseWindow;
    private ImportExercisesWindow? _importWindow;
    private EditPlanWindow? _planWindow;

    /// <summary>Текущие обратные вызовы окон правки: кто просит обновить список после записи.</summary>
    /// <remarks>
    /// Хранятся полями, а не в подписке на окно: при повторном открытии окно остаётся то же, а
    /// вызывающий мог измениться — например, справочник закрыли и открыли заново, и старый
    /// вызов указывал бы на мёртвую модель. После закрытия окна ссылки очищаются: сервис —
    /// синглтон и не должен удерживать временную модель представления.
    /// </remarks>
    private Func<Task>? _exerciseSaved;
    private Func<Task>? _importSaved;
    private Func<Task>? _planSaved;

    /// <summary>Обратные вызовы на закрытие окон правки: снять блокировку удаления в списках.</summary>
    /// <remarks>
    /// Окно может закрыться и без записи, когда <c>onSaved</c> не вызывается, — поэтому для
    /// снятия блокировки нужен отдельный вызов. Как и <c>onSaved</c>, живёт вместе с окном.
    /// </remarks>
    private Action? _exerciseClosed;
    private Action? _importClosed;
    private Action? _planClosed;

    /// <summary>Упражнение, которое реально правится: окно единственное, и правит оно один объект.</summary>
    public Exercise? EditedExercise { get; private set; }

    /// <summary>План, который реально правится; для добавления — ещё не сохранённый.</summary>
    public TrainingPlan? EditedPlan { get; private set; }

    public event Func<Task>? DaySaved;

    public event Func<Task>? SettingsSaved;

    public void OpenExercises() => _exercises.Show();

    public void OpenPlans() => _plans.Show();

    /// <summary>
    /// Открывает окно параметров: немодальное и единственное.
    /// </summary>
    /// <remarks>
    /// Подписка на «Сохранено» снимается перед новой: окно могло быть уже открыто, и тогда
    /// повторное открытие не создаёт нового, а подписка была бы второй на том же окне.
    /// </remarks>
    public void OpenSettings()
    {
        _settings.Show();

        if (_settings.Current is { } window)
        {
            window.Saved -= OnSettingsSaved;
            window.Saved += OnSettingsSaved;
        }
    }

    /// <summary>
    /// Открывает окно правки упражнения: отдельное, немодальное и единственное.
    /// </summary>
    /// <remarks>
    /// Повторное открытие не создаёт второе окно и не переключает упражнение: существующее
    /// выводится вперёд, как окно дня при попытке править другой день. Новый обратный вызов
    /// подменяет прежний — список мог переоткрыться.
    /// </remarks>
    /// <param name="exercise">Упражнение, которое правится.</param>
    /// <param name="onSaved">Что делать, когда упражнение записано.</param>
    /// <param name="onClosed">Что делать, когда окно закрылось, в том числе без записи.</param>
    public void OpenEditExercise(Exercise exercise, Func<Task>? onSaved = null, Action? onClosed = null)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        if (onSaved is not null)
        {
            _exerciseSaved = onSaved;
        }

        if (onClosed is not null)
        {
            _exerciseClosed = onClosed;
        }

        if (_exerciseWindow is { IsVisible: true })
        {
            ShowExisting(_exerciseWindow);

            return;
        }

        EditedExercise = exercise;
        var window = _exerciseWindow = editExerciseWindowFactory(exercise);
        window.Saved += OnExerciseWindowSaved;
        window.Closed += OnExercisesChildClosed;
        window.Show();
        BringToFrontAfterInput(window);
    }

    /// <summary>
    /// Открывает окно импорта упражнений: отдельное, немодальное и единственное.
    /// </summary>
    /// <param name="onSaved">Что делать, когда упражнения записаны.</param>
    /// <param name="onClosed">Что делать, когда окно закрылось, в том числе без записи.</param>
    public void OpenImportExercises(Func<Task>? onSaved = null, Action? onClosed = null)
    {
        if (onSaved is not null)
        {
            _importSaved = onSaved;
        }

        if (onClosed is not null)
        {
            _importClosed = onClosed;
        }

        if (_importWindow is { IsVisible: true })
        {
            ShowExisting(_importWindow);

            return;
        }

        var window = _importWindow = importExercisesWindowFactory();
        window.Saved += OnImportWindowSaved;
        window.Closed += OnExercisesChildClosed;
        window.Show();
        BringToFrontAfterInput(window);
    }

    /// <summary>
    /// Открывает окно добавления плана: отдельное, немодальное и единственное.
    /// </summary>
    /// <remarks>
    /// Одно окно на добавление и правку плана: фабрики разные, экземпляр общий.
    /// </remarks>
    /// <param name="onSaved">Что делать, когда план записан.</param>
    /// <param name="onClosed">Что делать, когда окно закрылось, в том числе без записи.</param>
    public void OpenAddPlan(Func<Task>? onSaved = null, Action? onClosed = null)
    {
        if (onSaved is not null)
        {
            _planSaved = onSaved;
        }

        if (onClosed is not null)
        {
            _planClosed = onClosed;
        }

        if (_planWindow is { IsVisible: true })
        {
            ShowExisting(_planWindow);

            return;
        }

        // Новый план ещё не в списке: ни одна строка не блокируется. Если окно правки уже
        // открыто, его содержимое не меняется — EditedPlan остаётся тем планом, что правится.
        var window = _planWindow = addPlanWindowFactory();
        window.Saved += OnPlanWindowSaved;
        window.Closed += OnPlansChildClosed;
        window.Show();
        BringToFrontAfterInput(window);
    }

    /// <summary>
    /// Открывает окно правки плана: отдельное, немодальное и единственное.
    /// </summary>
    /// <remarks>
    /// Повторное открытие не переключает план: выводится тот, что уже открыт.
    /// </remarks>
    /// <param name="plan">План, который правится.</param>
    /// <param name="onSaved">Что делать, когда план записан.</param>
    /// <param name="onClosed">Что делать, когда окно закрылось, в том числе без записи.</param>
    public void OpenEditPlan(TrainingPlan plan, Func<Task>? onSaved = null, Action? onClosed = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (onSaved is not null)
        {
            _planSaved = onSaved;
        }

        if (onClosed is not null)
        {
            _planClosed = onClosed;
        }

        if (_planWindow is { IsVisible: true })
        {
            ShowExisting(_planWindow);

            return;
        }

        EditedPlan = plan;
        var window = _planWindow = editPlanWindowFactory(plan);
        window.Saved += OnPlanWindowSaved;
        window.Closed += OnPlansChildClosed;
        window.Show();
        BringToFrontAfterInput(window);
    }

    /// <summary>Упражнение записано: вызываем текущий обратный вызов.</summary>
    private void OnExerciseWindowSaved() => _ = _exerciseSaved?.Invoke();

    /// <summary>Упражнения импортированы: вызываем текущий обратный вызов.</summary>
    private void OnImportWindowSaved() => _ = _importSaved?.Invoke();

    /// <summary>План записан: вызываем текущий обратный вызов.</summary>
    private void OnPlanWindowSaved() => _ = _planSaved?.Invoke();

    /// <summary>
    /// Окно правки упражнения или импорта закрылось: активация возвращается справочнику.
    /// </summary>
    /// <remarks>
    /// У окон без владельца возвращать активацию некому: фокус забрало бы приложение, которое
    /// было активно до нас (ловушка 9). Отдаём её окну-родителю, а если его нет — журналу.
    /// </remarks>
    private void OnExercisesChildClosed(object? sender, EventArgs e)
    {
        if (sender is EditExerciseWindow exercise)
        {
            exercise.Closed -= OnExercisesChildClosed;
            _exerciseWindow = null;
            _exerciseSaved = null;
            EditedExercise = null;
            _exerciseClosed?.Invoke();
            _exerciseClosed = null;
        }
        else if (sender is ImportExercisesWindow import)
        {
            import.Closed -= OnExercisesChildClosed;
            _importWindow = null;
            _importSaved = null;
            _importClosed?.Invoke();
            _importClosed = null;
        }

        if (ExercisesOwner is { IsVisible: true } owner)
        {
            owner.Activate();
        }
    }

    /// <summary>Окно плана закрылось: активация возвращается списку планов.</summary>
    /// <remarks>
    /// Как и у окон справочника: у окна без владельца возвращать активацию некому (ловушка 9).
    /// </remarks>
    private void OnPlansChildClosed(object? sender, EventArgs e)
    {
        if (sender is EditPlanWindow plan)
        {
            plan.Closed -= OnPlansChildClosed;
            _planWindow = null;
            _planSaved = null;
            EditedPlan = null;
            _planClosed?.Invoke();
            _planClosed = null;
        }

        if (PlansOwner is { IsVisible: true } owner)
        {
            owner.Activate();
        }
    }

    /// <summary>Показывает окно добавления дня. <c>day</c> из <c>null</c> — сегодня.</summary>
    public void ShowAddDay() => ShowDay(day: null);

    /// <inheritdoc />
    public void ShowEditDay(DateOnly day) => ShowDay(day);

    /// <summary>
    /// Настройки записаны: сообщаем подписчикам, чтобы главное окно перечитало файл.
    /// </summary>
    /// <remarks>
    /// Задача подписчика возвращается вызовом события, поэтому в тесте перечитывание можно
    /// дождаться await'ом. В приложении подписчик один, и ждать его задачи сервису нечего: она
    /// дополнится сама.
    /// </remarks>
    private void OnSettingsSaved() => SettingsSaved?.Invoke();

    /// <summary>
    /// Дата правки — открыть существующую запись, <c>null</c> — добавить.
    /// </summary>
    /// <remarks>
    /// Окно одно на оба случая: за дату одна запись, и модель сама определяет по дате, какая это
    /// запись. И единственное на всё приложение: день пока правится, и параллельным окном его не
    /// развести, поэтому попытка открыть ещё одно не создаёт окно, а выводит существующее вперёд
    /// с оповещением.
    ///
    /// Владельца у окна нет: с владельцем сворачивание главного окна прятало бы окно дня вместе с
    /// собой, а держать оба окна рабочими — смысл этой немодальности. Расплата — активация после
    /// закрытия делается руками, см. <see cref="OnDayWindowClosed"/> (ловушка 9).
    ///
    /// Сохранение окно оповещает событием <c>SavedChanged</c>, сервис раздаёт его как
    /// <see cref="DaySaved"/>: окно-то может остаться открытым, и журнал надо перечитать и после
    /// «Сохранить» без закрытия. Подписка снимается при закрытии окна.
    /// </remarks>
    private void ShowDay(DateOnly? day)
    {
        if (_dayWindow is { IsVisible: true })
        {
            ShowExisting(_dayWindow);

            return;
        }

        // Скрытого окна дня не бывает: закрытое окно доходит до OnDayWindowClosed, который
        // снимает подписи и выбрасывает его из поля, а Hide() никто не зовёт.
        var window = _dayWindow = dayWindowFactory(day);
        window.Closed += OnDayWindowClosed;
        window.SavedChanged += OnDayWindowSaved;
        window.Show();

        // Активация отложена так же, как у уже открытого окна: дабл-клик создаёт окно посреди
        // клика, и смену переднего плана система отклоняет, пока кнопка мыши держится. Для
        // кнопки «Добавить день» и пункта меню это безвредное мгновенное повторение
        // SetForegroundWindow на уже переднем окне.
        BringToFrontAfterInput(window);
    }

    /// <summary>
    /// Уже открытое окно: восстановить из минимизации, мигнуть, вывести вперёд.
    /// </summary>
    /// <remarks>
    /// Общий помощник для окна дня и трёх окон правки: у всех у них нет владельца, и порядок
    /// один и тот же. Повторное открытие не меняет содержимое окна — какое было, такое и
    /// остаётся, как у окна дня при попытке править другой день.
    ///
    /// Порядок и оповещение = то самое поведение Windows при клике на заблокированное главное
    /// окно: восстановление окна, мигание и звук, затем вывод на передний план. Порядок задаёт
    /// <see cref="WindowAttention.BringToFront"/>, не <c>Activate()</c>: тот даёт фокус, но не
    /// поднимает окно по z-порядку — owned-окнам это безразлично, а окно без владельца за главным
    /// окном осталось бы невидимым.
    /// </remarks>
    /// <param name="window">Окно, которое уже открыто.</param>
    private static void ShowExisting(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        WindowAttention.Flash(window);

        BringToFrontAfterInput(window);
    }

    /// <summary>Выводит окно на передний план вне текущей обработки ввода.</summary>
    /// <remarks>
    /// <c>SetForegroundWindow</c> из середины двойного клика отклоняется: обработчик
    /// <c>MouseDoubleClick</c> выполняется при ещё нажатой кнопке мыши, и система не разрешает
    /// менять передний план посреди клика, а кнопка «Добавить день» и пункт меню выполняются
    /// после отпускания — поэтому их прямой вызов работал, а дабл-клик нет. Вывод уходит в
    /// очередь диспетчера: приоритет Background выполняется только после всей очереди Input,
    /// куда входит оставшийся mouse-up, — вызов происходит уже вне клика, на миллисекунды позже.
    /// </remarks>
    private static void BringToFrontAfterInput(Window window) =>
        window.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () =>
            {
                // Окно могло закрыться, пока очередь дожидалась: подниматься некому.
                if (window.IsVisible)
                {
                    WindowAttention.BringToFront(window);
                }
            });

    /// <summary>Окно дня записало день в базу: журнал в главном окне надо перечитать.</summary>
    /// <remarks>
    /// Задача подписчика возвращается вызовом события, поэтому в тесте перечитывание можно
    /// дождаться await'ом. В приложении подписчик один, и ждать его задачи сервису нечего: она
    /// дополнится сама. Отказ AsyncRelayCommand при уже идущем перечитывании здесь не страшен:
    /// событие поднимает окно дня, а не вторая команда того же окна.
    /// </remarks>
    private void OnDayWindowSaved() => DaySaved?.Invoke();

    /// <summary>Окно дня закрылось: подписи сняты, активация возвращена главному окну.</summary>
    private void OnDayWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not AddDayWindow window)
        {
            return;
        }

        window.Closed -= OnDayWindowClosed;
        window.SavedChanged -= OnDayWindowSaved;
        _dayWindow = null;

        // Владельца у окна нет, и Windows не знает, кому вернуть передний план, поэтому
        // активируем главное окно явно (ловушка 9) — так же, как после ShowDialog. На
        // IsVisible проверка: при закрытии главного окна окно дня закрывается следом за
        // приложением, и активировать закрытое главное окно нечего.
        if (Application.Current?.MainWindow is { IsVisible: true } main)
        {
            main.Activate();
        }
    }

    private Window? PlansOwner => _plans.Current ?? Application.Current?.MainWindow;

    /// <summary>Окно справочника: ему возвращается активация после закрытия окон правки.</summary>
    private Window? ExercisesOwner => _exercises.Current ?? Application.Current?.MainWindow;
}
