using System.Windows;
using TrainingLog.Core.Models;
using TrainingLog.Windows;

namespace TrainingLog.Services;

/// <summary>Список окон приложения.</summary>
/// <remarks>
/// Окна справочников идут через <see cref="SingleInstanceWindowHost{TWindow}"/>: повторное
/// нажатие фокусирует открытое. Окна правки, окно дня и окно параметров создаются заново на
/// каждый вызов — внутри них то, что правят, и переиспользование экземпляра со старым
/// содержимым выглядело бы как зависание.
/// </remarks>
public sealed class WindowService(
    Func<ExercisesWindow> exercisesWindowFactory,
    Func<Exercise, EditExerciseWindow> editExerciseWindowFactory,
    Func<PlansWindow> plansWindowFactory,
    Func<EditPlanWindow> addPlanWindowFactory,
    Func<TrainingPlan, EditPlanWindow> editPlanWindowFactory,
    Func<DateOnly?, AddDayWindow> dayWindowFactory,
    Func<SettingsWindow> settingsWindowFactory) : IWindowService
{
    private readonly SingleInstanceWindowHost<ExercisesWindow> _exercises = new(exercisesWindowFactory);
    private readonly SingleInstanceWindowHost<PlansWindow> _plans = new(plansWindowFactory);

    public void OpenExercises() => _exercises.Show();

    public void OpenPlans() => _plans.Show();

    public bool ShowSettings() =>
        ShowDialog(settingsWindowFactory(), Application.Current?.MainWindow);

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

    /// <summary>Показывает окно добавления дня. <c>day</c> из <c>null</c> — сегодня.</summary>
    /// <remarks>
    /// Ответ собирается из двух состояний окна, а не из <c>DialogResult</c>: после «Сохранить»
    /// без закрытия он остаётся <c>null</c>, и по одному значению главное окно не узнало бы, что
    /// день записан.
    /// </remarks>
    public bool ShowAddDay() => ShowDay(day: null);

    /// <inheritdoc />
    public bool ShowEditDay(DateOnly day) => ShowDay(day);

    /// <summary>Дата правки — открыть существующую запись, <c>null</c> — добавить.</summary>
    /// <remarks>
    /// Окно одно на оба случая: за дату одна запись, и модель сама определяет по дате, какая это
    /// запись.
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
