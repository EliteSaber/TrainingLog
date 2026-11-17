using System.Threading.Tasks;
using TrainingLog.Core.Models;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>Рукописный подменный <see cref="IWindowService"/>: окон в тестах не нужно.</summary>
/// <remarks>
/// Окна правки немодальные, и о записи они сообщают обратным вызовом — он здесь запоминается,
/// чтобы тест сам позвал его и увидел, как список перестроился.
/// </remarks>
internal sealed class FakeWindowService : IWindowService
{
    /// <summary>Сколько раз открывали окно добавления дня.</summary>
    public int AddDayCalls { get; private set; }

    /// <summary>Сколько раз открывали окно правки дня.</summary>
    public int EditDayCalls { get; private set; }

    /// <summary>Дата последней правки дня.</summary>
    public DateOnly? LastEditDay { get; private set; }

    /// <summary>Сколько раз открывали окно параметров.</summary>
    public int SettingsCalls { get; private set; }

    /// <summary>Сколько раз окно параметров сообщило о записи настроек.</summary>
    public int SettingsSavedRises { get; private set; }

    /// <summary>Сколько раз открывали окно импорта.</summary>
    public int ImportCalls { get; private set; }

    /// <summary>Обратный вызов последнего окна правки упражнения: тест зовёт его сам.</summary>
    public Func<Task>? ExerciseSaved { get; private set; }

    /// <summary>Обратный вызов последнего окна плана: тест зовёт его сам.</summary>
    public Func<Task>? PlanSaved { get; private set; }

    /// <summary>Обратный вызов последнего окна импорта: тест зовёт его сам.</summary>
    public Func<Task>? ImportSaved { get; private set; }

    /// <summary>Обратный вызов закрытия окна правки упражнения.</summary>
    public Action? ExerciseClosed { get; private set; }

    /// <summary>Обратный вызов закрытия окна плана.</summary>
    public Action? PlanClosed { get; private set; }

    /// <summary>Упражнение, которое правится: подменяется открытием окна.</summary>
    public Exercise? EditedExercise { get; private set; }

    /// <summary>План, который правится: подменяется открытием окна.</summary>
    public TrainingPlan? EditedPlan { get; private set; }

    /// <summary>Сколько раз окно дня сообщило о сохранении.</summary>
    public int DaySavedRises { get; private set; }

    public event Func<Task>? DaySaved;

    public event Func<Task>? SettingsSaved;

    public void OpenExercises()
    {
    }

    public void OpenPlans()
    {
    }

    /// <summary>
    /// Окно параметров открыто.
    /// </summary>
    /// <remarks>
    /// Ответа у вызова нет: окно немодальное, и о записи настроек вызывающий узнаёт из
    /// <see cref="RaiseSettingsSaved"/>.
    /// </remarks>
    public void OpenSettings() => SettingsCalls++;

    /// <summary>Окно правки упражнения открыто: обратные вызовы ждут записи и закрытия.</summary>
    /// <param name="exercise">Упражнение, которое правится.</param>
    /// <param name="onSaved">Что делать, когда упражнение записано.</param>
    /// <param name="onClosed">Что делать, когда окно закрылось.</param>
    public void OpenEditExercise(Exercise exercise, Func<Task>? onSaved = null, Action? onClosed = null)
    {
        ExerciseSaved = onSaved;
        ExerciseClosed = onClosed;
        EditedExercise = exercise;
    }

    /// <summary>Окно добавления плана открыто: обратные вызовы ждут записи и закрытия.</summary>
    /// <param name="onSaved">Что делать, когда план записан.</param>
    /// <param name="onClosed">Что делать, когда окно закрылось.</param>
    public void OpenAddPlan(Func<Task>? onSaved = null, Action? onClosed = null)
    {
        PlanSaved = onSaved;
        PlanClosed = onClosed;
    }

    /// <summary>Окно правки плана открыто: обратные вызовы ждут записи и закрытия.</summary>
    /// <param name="plan">План, который правится.</param>
    /// <param name="onSaved">Что делать, когда план записан.</param>
    /// <param name="onClosed">Что делать, когда окно закрылось.</param>
    public void OpenEditPlan(TrainingPlan plan, Func<Task>? onSaved = null, Action? onClosed = null)
    {
        PlanSaved = onSaved;
        PlanClosed = onClosed;
        EditedPlan = plan;
    }

    /// <summary>Окно импорта открыто: обратные вызовы ждут записи и закрытия.</summary>
    /// <param name="onSaved">Что делать, когда упражнения записаны.</param>
    /// <param name="onClosed">Что делать, когда окно закрылось.</param>
    public void OpenImportExercises(Func<Task>? onSaved = null, Action? onClosed = null)
    {
        ImportCalls++;
        ImportSaved = onSaved;
    }

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

    /// <summary>Окно параметров записало настройки.</summary>
    /// <remarks>
    /// Задача подписчика возвращается вызовом события — так же, как у окна дня: главное окно
    /// перечитывает файл и обновляет список, и тест дожидается этого await'ом.
    /// </remarks>
    public Task RaiseSettingsSaved()
    {
        SettingsSavedRises++;

        return SettingsSaved?.Invoke() ?? Task.CompletedTask;
    }

    /// <summary>Окно правки упражнения записало упражнение: зовём обратный вызов.</summary>
    /// <remarks>
    /// Без обратного вызова возвращается уже исполненная задача: тест этого случая не ждёт,
    /// а null'ом await'ить нельзя.
    /// </remarks>
    public Task RaiseExerciseSaved() => ExerciseSaved?.Invoke() ?? Task.CompletedTask;

    /// <summary>Окно плана записало план: зовём обратный вызов.</summary>
    public Task RaisePlanSaved() => PlanSaved?.Invoke() ?? Task.CompletedTask;

    /// <summary>Окно импорта записало упражнения: зовём обратный вызов.</summary>
    public Task RaiseImportSaved() => ImportSaved?.Invoke() ?? Task.CompletedTask;

    /// <summary>Окно правки упражнения закрылось, в том числе без записи.</summary>
    public void RaiseEditClosed()
    {
        EditedExercise = null;
        ExerciseClosed?.Invoke();
    }

    /// <summary>Окно плана закрылось, в том числе без записи.</summary>
    public void RaisePlanClosed()
    {
        EditedPlan = null;
        PlanClosed?.Invoke();
    }
}
