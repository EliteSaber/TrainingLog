using TrainingLog.Core.Models;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>
/// Рукописный подменный <see cref="IWindowService"/>: mocking-библиотеки в проекте нет,
/// а проверять окна в тестах не нужно — достаточно знать, открывалось ли окно и с каким
/// результатом.
/// </summary>
internal sealed class FakeWindowService : IWindowService
{
    /// <summary>Что <c>ShowAddDay</c> вернёт вызывающему.</summary>
    public bool AddDayResult { get; set; }

    /// <summary>
    /// Что сделает <c>ShowAddDay</c>, если задано: обычное действие вызывающего из теста.
    /// </summary>
    /// <remarks>
    /// Нужно для проверки «сохранил без закрытия, потом закрыл крестиком»: возвращаемое
    /// значение должно повторять настоящее поведение окна, а <see cref="AddDayResult"/> не
    /// может выразить «сохранено, но <c>DialogResult</c> не выставлен».
    /// </remarks>
    public Func<Task>? OnAddDay { get; set; }

    /// <summary>Сколько раз открывали окно добавления дня.</summary>
    public int AddDayCalls { get; private set; }

    public void OpenExercises()
    {
    }

    public void OpenPlans()
    {
    }

    public bool ShowEditExercise(Exercise exercise) => false;

    public bool ShowAddPlan() => false;

    public bool ShowEditPlan(TrainingPlan plan) => false;

    public bool ShowAddDay()
    {
        AddDayCalls++;

        // Синхронная блокировка допустима только в тесте: вызывающий в приложении — команда
        // MainViewModel, и она тоже синхронная по контракту IWindowService.
        OnAddDay?.Invoke().GetAwaiter().GetResult();

        return AddDayResult;
    }
}