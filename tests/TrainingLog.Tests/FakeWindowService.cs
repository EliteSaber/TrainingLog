using System.Threading.Tasks;
using TrainingLog.Core.Models;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>Рукописный подменный <see cref="IWindowService"/>: окон в тестах не нужно.</summary>
internal sealed class FakeWindowService : IWindowService
{
    /// <summary>Сколько раз открывали окно добавления дня.</summary>
    public int AddDayCalls { get; private set; }

    /// <summary>Сколько раз открывали окно правки дня.</summary>
    public int EditDayCalls { get; private set; }

    /// <summary>Дата последней правки дня.</summary>
    public DateOnly? LastEditDay { get; private set; }

    /// <summary>Что <c>ShowSettings</c> вернёт вызывающему.</summary>
    public bool SettingsResult { get; set; }

    /// <summary>Что сделает <c>ShowSettings</c>, если задано.</summary>
    /// <remarks>
    /// Нужно, чтобы окно параметров записало в файл то, что потом перечитает главное окно:
    /// проверяется ровно то, что перечитывание случается по файлу.
    /// </remarks>
    public Action? OnSettings { get; set; }

    /// <summary>Сколько раз открывали окно параметров.</summary>
    public int SettingsCalls { get; private set; }

    /// <summary>Что <c>ShowImportExercises</c> вернёт вызывающему.</summary>
    public bool ImportResult { get; set; }

    /// <summary>Сколько раз открывали окно импорта.</summary>
    public int ImportCalls { get; private set; }

    /// <summary>Сколько раз окно дня сообщило о сохранении.</summary>
    public int DaySavedRises { get; private set; }

    public event Func<Task>? DaySaved;

    public void OpenExercises()
    {
    }

    public void OpenPlans()
    {
    }

    public bool ShowSettings()
    {
        SettingsCalls++;

        OnSettings?.Invoke();

        return SettingsResult;
    }

    public bool ShowEditExercise(Exercise exercise) => false;

    public bool ShowImportExercises()
    {
        ImportCalls++;

        return ImportResult;
    }

    public bool ShowAddPlan() => false;

    public bool ShowEditPlan(TrainingPlan plan) => false;

    public void ShowAddDay() => AddDayCalls++;

    public void ShowEditDay(DateOnly day)
    {
        EditDayCalls++;
        LastEditDay = day;
    }

    /// <summary>Окно дня сообщило о сохранении.</summary>
    /// <remarks>
    /// Возвращает задачу подписчика, чтобы тест дождался перечитывания журнала await'ом, а не
    /// полем в надежде на порядок выполнения. Без подписчика возвращает уже исполненную задачу:
    /// тест этого случая не ждёт, а null'ом await'ить нельзя.
    /// </remarks>
    public Task RaiseDaySaved()
    {
        DaySavedRises++;

        return DaySaved?.Invoke() ?? Task.CompletedTask;
    }
}
