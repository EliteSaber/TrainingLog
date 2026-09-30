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
    /// Показывает модальное окно правки упражнения.
    /// </summary>
    /// <param name="exercise">Упражнение для правки.</param>
    /// <returns><c>true</c>, если изменения приняты и сохранены.</returns>
    bool ShowEditExercise(Exercise exercise);
}
