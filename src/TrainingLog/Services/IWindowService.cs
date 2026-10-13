using TrainingLog.Core.Models;

namespace TrainingLog.Services;

/// <summary>Открытие и переключение между окнами приложения.</summary>
public interface IWindowService
{
    /// <summary>Справочник упражнений. Уже открытое окно выводится на передний план.</summary>
    void OpenExercises();

    /// <summary>Список планов. Уже открытое окно выводится на передний план.</summary>
    void OpenPlans();

    /// <returns><c>true</c>, если параметры приняты и записаны.</returns>
    bool ShowSettings();

    /// <returns><c>true</c>, если изменения приняты и сохранены.</returns>
    bool ShowEditExercise(Exercise exercise);

    /// <summary>Добавление плана. Окно то же, что и при правке.</summary>
    /// <returns><c>true</c>, если план сохранён.</returns>
    bool ShowAddPlan();

    /// <returns><c>true</c>, если изменения приняты и сохранены.</returns>
    bool ShowEditPlan(TrainingPlan plan);

    /// <summary>Добавление дня. На дату, по которой запись уже есть, откроется правка той же записи.</summary>
    /// <returns>
    /// <c>true</c>, если день сохранён, — независимо от того, закрыт ли диалог кнопкой сохранения
    /// или крестиком. Одного <c>DialogResult</c> для этого недостаточно: после «Сохранить» без
    /// закрытия он остаётся <c>null</c>.
    /// </returns>
    bool ShowAddDay();

    /// <param name="day">Дата дня, который открывается на правку. Ответ тот же, что у добавления.</param>
    bool ShowEditDay(DateOnly day);
}
