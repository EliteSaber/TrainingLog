using TrainingLog.Core.Models;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>Рукописный подменный <see cref="IWindowService"/>: окон в тестах не нужно.</summary>
internal sealed class FakeWindowService : IWindowService
{
    /// <summary>Что <c>ShowAddDay</c> вернёт вызывающему.</summary>
    public bool AddDayResult { get; set; }

    /// <summary>
    /// Что сделает <c>ShowAddDay</c>, если задано: обычное действие вызывающего из теста.
    /// </summary>
    /// <remarks>
    /// Нужно для «сохранил без закрытия, потом закрыл крестиком»: возвращаемое значение должно
    /// повторять настоящее поведение окна, а <see cref="AddDayResult"/> не может выразить
    /// «сохранено, но <c>DialogResult</c> не выставлен».
    /// </remarks>
    public Func<Task>? OnAddDay { get; set; }

    /// <summary>Сколько раз открывали окно добавления дня.</summary>
    public int AddDayCalls { get; private set; }

    /// <summary>Что <c>ShowEditDay</c> вернёт вызывающему.</summary>
    public bool EditDayResult { get; set; }

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

    public bool ShowAddPlan() => false;

    public bool ShowEditPlan(TrainingPlan plan) => false;

    public bool ShowAddDay()
    {
        AddDayCalls++;

        // Синхронная блокировка допустима только в тесте: вызывающий в приложении — команда
        // MainViewModel, и она тоже синхронна по контракту IWindowService.
        OnAddDay?.Invoke().GetAwaiter().GetResult();

        return AddDayResult;
    }

    public bool ShowEditDay(DateOnly day)
    {
        EditDayCalls++;
        LastEditDay = day;

        return EditDayResult;
    }
}
