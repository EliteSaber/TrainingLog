using CommunityToolkit.Mvvm.ComponentModel;
using TrainingLog.Core.Models;

namespace TrainingLog.ViewModels;

/// <summary>
/// Строка списка упражнений в окне плана: само упражнение, признак вхождения в план
/// и его место в порядке выполнения.
/// </summary>
/// <remarks>
/// Отдельный класс, а не свойство у <see cref="Exercise"/>: упражнение справочника — доменная
/// модель, и «выбран в этом плане на третьем месте» ей не свойство. Один и тот же экземпляр
/// упражнения может стоять в чекбоксе неотмеченным, пока в другом окне он же отмечен.
/// </remarks>
public sealed partial class PlanExerciseSelection : ObservableObject
{
    public PlanExerciseSelection(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        Exercise = exercise;
    }

    /// <summary>Упражнение из справочника.</summary>
    public Exercise Exercise { get; }

    /// <summary>Наименование упражнения для показа в списке.</summary>
    public string Name => Exercise.Name;

    /// <summary>Входит ли упражнение в план.</summary>
    [ObservableProperty]
    private bool _isChecked;

    /// <summary>
    /// Место в порядке выполнения, с единицы. Пока строка не добавлена в план, значение
    /// нулевое — показывать его негде.
    /// </summary>
    [ObservableProperty]
    private int _position;
}
