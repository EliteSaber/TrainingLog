using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainingLog.Core.Models;

namespace TrainingLog.ViewModels;

/// <summary>
/// Упражнение плана в окне добавления дня: номер кнопки, название и его подходы.
/// </summary>
/// <remarks>
/// Отдельный класс, а не <see cref="Exercise"/> из справочника: «третье упражнение этого дня
/// и подходы, введённые в него» — свойство сеанса правки, а не упражнения. Родственник
/// <see cref="PlanExerciseSelection"/> из окна правки плана.
/// </remarks>
public sealed partial class PlanExerciseViewModel : ObservableObject
{
    /// <param name="exercise">Упражнение из справочника.</param>
    /// <param name="number">Номер кнопки в плане, с единицы.</param>
    public PlanExerciseViewModel(Exercise exercise, int number)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        Exercise = exercise;
        Number = number;
    }

    /// <summary>Упражнение из справочника.</summary>
    public Exercise Exercise { get; }

    /// <summary>Название упражнения: показывается и по кнопкой, и по центру окна.</summary>
    public string Name => Exercise.Name;

    /// <summary>Номер кнопки в плане, с единицы.</summary>
    public int Number { get; }

    /// <summary>Активно ли это упражнение: его подходы показаны в центре окна.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Подходы упражнения в порядке ввода — слева направо в центре окна.</summary>
    public ObservableCollection<SetInputViewModel> Sets { get; } = [];

    /// <summary>
    /// Есть ли введённые подходы. Упражнение без подходов в запись дня не попадает:
    /// план выполнен не целиком, а значит невыполненные упражнения в журнале лишние.
    /// </summary>
    public bool HasData => Sets.Any(set => set.HasData);

    /// <summary>
    /// Создаёт строку упражнения с одним пустым подходом.
    /// </summary>
    /// <remarks>
    /// Подход один и пустой, а не ноль: нулевой набор полей требовал бы первого нажатия «+»
    /// ради того, чтобы просто начать ввод. Лишний подход убирается кнопкой «−».
    /// </remarks>
    /// <param name="exercise">Упражнение из справочника.</param>
    /// <param name="number">Номер кнопки в плане.</param>
    public static PlanExerciseViewModel Create(Exercise exercise, int number) =>
        new(exercise, number)
        {
            Sets = { new SetInputViewModel() },
        };
}