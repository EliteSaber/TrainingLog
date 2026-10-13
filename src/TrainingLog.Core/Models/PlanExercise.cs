namespace TrainingLog.Core.Models;

/// <summary>Строка связи плана с упражнением: какое упражнение входит в план и на каком месте.</summary>
/// <remarks>
/// Связь many-to-many хранилась теневой таблицей, пока не понадобился порядок: отсортировать
/// <c>Include</c> по колонке тени EF не умеет, у тени нет навигации, по которой можно сортировать.
/// </remarks>
public sealed class PlanExercise
{
    /// <summary>Часть составного ключа связи.</summary>
    public int TrainingPlanId { get; set; }

    /// <summary>Часть составного ключа связи.</summary>
    public int ExerciseId { get; set; }

    /// <summary>Место в порядке выполнения, с единицы — чтобы номер показывался без конвертера.</summary>
    public int Order { get; set; }

    /// <summary>
    /// Без инициализации: пустой <see cref="TrainingPlan"/> был бы несохранённым объектом,
    /// который EF попытался бы вставить вместе со связью.
    /// </summary>
    public TrainingPlan Plan { get; set; } = null!;

    /// <summary>Тоже заполняется явно при добавлении связи.</summary>
    public Exercise Exercise { get; set; } = null!;
}
