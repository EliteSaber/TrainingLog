using System.Windows;
using System.Windows.Threading;
using TrainingLog.Core.Models;
using TrainingLog.Windows;

namespace TrainingLog.Services;

/// <summary>Список окон приложения.</summary>
/// <remarks>
/// Окна справочников идут через <see cref="SingleInstanceWindowHost{TWindow}"/>: повторное
/// нажатие фокусирует открытое. Окна правки и окно параметров создаются заново на
/// каждый вызов — внутри них то, что правят, и переиспользование экземпляра со старым
/// содержимым выглядело бы как зависание.
///
/// Окно дня единственный своего рода: немодальное, поэтому главное окно при открытом дне
/// остаётся рабочим, и единственное, но без хоста — фабрике окна нужен день, а хост вызывает
/// её без параметров. Повторное открытие и «Параметры» при активном дне не создают ничего,
/// а выводят существующее окно на передний план с оповещением <see cref="WindowAttention"/>.
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
    private readonly SingleInstanceWindowHost<ExercisesWindow> _exercises = new(exercisesWindowFactory);
    private readonly SingleInstanceWindowHost<PlansWindow> _plans = new(plansWindowFactory);

    /// <summary>Открытое окно дня. <c>null</c> — окна дня нет.</summary>
    /// <remarks>
    /// Одиночный экземпляр окно дня держит сам сервис: хосту фабрика с датой не подходит, а
    /// кроме вывода на передний план здесь же подписка на сохранение и закрытие.
    /// </remarks>
    private AddDayWindow? _dayWindow;

    public event Func<Task>? DaySaved;

    public void OpenExercises() => _exercises.Show(Application.Current?.MainWindow);

    public void OpenPlans() => _plans.Show(Application.Current?.MainWindow);

    public bool ShowSettings()
    {
        // При открытом окне дня параметры не открываются: экран настроек поверх правящегося дня
        // выглядел бы как его замена. Существующее окно выводится вперёд с оповещением.
        if (_dayWindow is { IsVisible: true })
        {
            ShowExistingDayWindow();

            return false;
        }

        return ShowDialog(settingsWindowFactory(), Application.Current?.MainWindow);
    }

    public bool ShowEditExercise(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var window = editExerciseWindowFactory(exercise);

        return ShowDialog(window, _exercises.Current ?? Application.Current?.MainWindow);
    }

    public bool ShowImportExercises() =>
        ShowDialog(importExercisesWindowFactory(), _exercises.Current ?? Application.Current?.MainWindow);

    public bool ShowAddPlan() => ShowDialog(addPlanWindowFactory(), PlansOwner);

    public bool ShowEditPlan(TrainingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return ShowDialog(editPlanWindowFactory(plan), PlansOwner);
    }

    /// <summary>Показывает окно добавления дня. <c>day</c> из <c>null</c> — сегодня.</summary>
    public void ShowAddDay() => ShowDay(day: null);

    /// <inheritdoc />
    public void ShowEditDay(DateOnly day) => ShowDay(day);

    /// <summary>Дата правки — открыть существующую запись, <c>null</c> — добавить.</summary>
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
            ShowExistingDayWindow();

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

    /// <summary>Уже открытое окно дня: восстановить из минимизации, мигнуть, вывести вперёд.</summary>
    /// <remarks>
    /// Порядок и оповещение = то самое поведение Windows при клике на заблокированное главное
    /// окно: восстановление окна, мигание и звук, затем вывод на передний план. Порядок задаёт
    /// <see cref="WindowAttention.BringToFront"/>, не <c>Activate()</c>: тот даёт фокус, но не
    /// поднимает окно по z-порядку — owned-окнам это безразлично, окно дня за главным окном
    /// было бы невидимо.
    /// </remarks>
    private void ShowExistingDayWindow()
    {
        var window = _dayWindow!;

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

    /// <summary>
    /// Показывает окно поверх владельца и возвращает результат.
    /// </summary>
    /// <remarks>
    /// Владелец берётся у хоста, а не ищется перебором окон по IsActive: такой поиск вернёт
    /// <c>null</c>, если в момент вызова ни одно окно ещё не считается активным. Без владельца
    /// закрывать окно некому, и активацию забирает приложение, активное до нас.
    ///
    /// Активация владельца после закрытия вызывается явно: полагаться на поведение WPF по
    /// умолчанию нельзя (ловушка 9).
    /// </remarks>
    private static bool ShowDialog(Window window, Window? owner)
    {
        window.Owner = owner;

        var accepted = window.ShowDialog() == true;

        owner?.Activate();

        return accepted;
    }
}
