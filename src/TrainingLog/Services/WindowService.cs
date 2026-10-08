using System.Windows;
using TrainingLog.Core.Models;
using TrainingLog.Windows;

namespace TrainingLog.Services;

/// <summary>
/// Реализация <see cref="IWindowService"/>: список окон приложения.
/// </summary>
public sealed class WindowService(
    Func<ExercisesWindow> exercisesWindowFactory,
    Func<Exercise, EditExerciseWindow> editExerciseWindowFactory,
    Func<PlansWindow> plansWindowFactory,
    Func<EditPlanWindow> addPlanWindowFactory,
    Func<TrainingPlan, EditPlanWindow> editPlanWindowFactory,
    Func<DateOnly?, AddDayWindow> dayWindowFactory) : IWindowService
{
    private readonly SingleInstanceWindowHost<ExercisesWindow> _exercises = new(exercisesWindowFactory);
    private readonly SingleInstanceWindowHost<PlansWindow> _plans = new(plansWindowFactory);

    public void OpenExercises() => _exercises.Show();

    public void OpenPlans() => _plans.Show();

    /// <summary>
    /// Окно редактирования намеренно создаётся заново на каждый вызов, а не через
    /// <see cref="SingleInstanceWindowHost{TWindow}"/>: внутри него то упражнение, которое правят,
    /// и переиспользование экземпляра со старым содержимым выглядело бы как зависание.
    /// </summary>
    public bool ShowEditExercise(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var window = editExerciseWindowFactory(exercise);

        return ShowDialog(window, _exercises.Current ?? Application.Current?.MainWindow);
    }

    public bool ShowAddPlan() => ShowDialog(addPlanWindowFactory(), PlansOwner);

    public bool ShowEditPlan(TrainingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return ShowDialog(editPlanWindowFactory(plan), PlansOwner);
    }

    /// <summary>
    /// Окно дня создаётся заново на каждый вызов, как и окна правки: внутри него набираются
    /// подходы, и переиспользование экземпляра с прошлым содержимым выглядело бы как зависание.
    /// Владелец — главное окно, из которого окно и вызывается.
    /// </summary>
    /// <summary>
    /// Показывает окно добавления дня тренировки. Возвращает <c>true</c>, если день был
    /// сохранён, — независимо от того, закрыто ли окно кнопкой сохранения или крестиком.
    /// </summary>
    /// <remarks>
    /// Не <c>ShowDialog</c> как у остальных: у этого окна есть «Сохранить» без закрытия,
    /// поэтому <c>DialogResult</c> после него остаётся <c>null</c>, и по одному значению
    /// диалога главное окно не узнало бы, что день записан. Ответ собирается из обоих
    /// состояний окна: закрыт кнопкой — <c>Accepted</c>, сохранён без закрытия — <c>Saved</c>.
    /// </remarks>
    public bool ShowAddDay() => ShowDay(day: null);

    /// <inheritdoc />
    public bool ShowEditDay(DateOnly day) => ShowDay(day);

    /// <summary>
    /// Показывает окно дня: <paramref name="day"/> — это дата правки, <c>null</c> — добавление
    /// дня, то есть сегодня.
    /// </summary>
    /// <remarks>
    /// Окно одно на оба случая, поэтому и фабрика одна, принимающая дату: отличаются вызовы
    /// датой, а не окном. За дату одна запись, и какая это запись — добавление или правка —
    /// модель узнаёт сама при первом наполнении.
    ///
    /// Окно создаётся заново на каждый вызов, как и окна правки: внутри него набираются
    /// подходы, и переиспользование экземпляра с прошлым содержимым выглядело бы как зависание.
    /// Владелец — главное окно, из которого окно и вызывается.
    /// </remarks>
    private bool ShowDay(DateOnly? day)
    {
        var window = dayWindowFactory(day);

        var owner = Application.Current?.MainWindow;
        window.Owner = owner;

        var accepted = window.ShowDialog() == true;

        owner?.Activate();

        return accepted || window.Saved;
    }

    private Window? PlansOwner => _plans.Current ?? Application.Current?.MainWindow;

    /// <summary>
    /// Показывает окно поверх владельца и возвращает результат.
    /// </summary>
    /// <remarks>
    /// Владелец берётся у хоста, а не ищется перебором окон по IsActive: такой поиск
    /// возвращает null, если в момент вызова ни одно окно ещё не считается активным. Без
    /// владельца закрывать окно некому, и активацию забирает приложение, активное до нас.
    /// После закрытия активация возвращается владельцу явно: полагаться на поведение WPF по
    /// умолчанию нельзя, иначе активным станет то приложение, в котором пользователь был до нас.
    /// </remarks>
    private static bool ShowDialog(Window window, Window? owner)
    {
        window.Owner = owner;

        var accepted = window.ShowDialog() == true;

        owner?.Activate();

        return accepted;
    }
}
