using TrainingLog.Core.Models;

namespace TrainingLog.Services;

/// <summary>
/// Открытие и переключение между окнами приложения.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Показывает окно со справочником упражнений. Если окно уже открыто, оно выводится на передний план.
    /// </summary>
    void OpenExercises();

    /// <summary>
    /// Показывает окно со списком планов. Если окно уже открыто, оно выводится на передний план.
    /// </summary>
    void OpenPlans();

    /// <summary>
    /// Показывает модальное окно правки упражнения.
    /// </summary>
    /// <param name="exercise">Упражнение для правки.</param>
    /// <returns><c>true</c>, если изменения приняты и сохранены.</returns>
    bool ShowEditExercise(Exercise exercise);

    /// <summary>
    /// Показывает модальное окно добавления плана. Окно то же, что и при правке.
    /// </summary>
    /// <returns><c>true</c>, если план сохранён.</returns>
    bool ShowAddPlan();

    /// <summary>
    /// Показывает модальное окно правки плана.
    /// </summary>
    /// <param name="plan">План для правки.</param>
    /// <returns><c>true</c>, если изменения приняты и сохранены.</returns>
    bool ShowEditPlan(TrainingPlan plan);
}
