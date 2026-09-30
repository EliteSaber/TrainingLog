using System.Windows;
using TrainingLog.Core.Models;
using TrainingLog.Windows;

namespace TrainingLog.Services;

/// <summary>
/// Реализация <see cref="IWindowService"/>: список окон приложения.
/// </summary>
public sealed class WindowService(
    Func<ExercisesWindow> exercisesWindowFactory,
    Func<Exercise, EditExerciseWindow> editExerciseWindowFactory) : IWindowService
{
    private readonly SingleInstanceWindowHost<ExercisesWindow> _exercises = new(exercisesWindowFactory);

    public void OpenExercises() => _exercises.Show();

    /// <summary>
    /// Окно редактирования намеренно создаётся заново на каждый вызов, а не через
    /// <see cref="SingleInstanceWindowHost{TWindow}"/>: внутри него то упражнение, которое правят,
    /// и переиспользование экземпляра со старым содержимым выглядело бы как зависание.
    /// </summary>
    public bool ShowEditExercise(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var window = editExerciseWindowFactory(exercise);

        // Владелец берётся у хоста, а не ищется перебором окон по IsActive: такой поиск
        // возвращает null, если в момент вызова ни одно окно ещё не считается активным. Без
        // владельца закрывать окно некому, и активацию забирает приложение, активное до нас.
        var owner = _exercises.Current ?? Application.Current?.MainWindow;
        window.Owner = owner;

        var accepted = window.ShowDialog() == true;

        // Возвращаем активацию владельцу явно: полагаться на поведение WPF по умолчанию
        // нельзя, иначе после закрытия диалога и окна упражнений активным станет то приложение,
        // в котором пользователь был до нас.
        owner?.Activate();

        return accepted;
    }
}
