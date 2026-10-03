namespace TrainingLog.Core.Models;
/// <summary>
/// План тренировки: наименование и список упражнений из справочника.
/// </summary>
public sealed class TrainingPlan
{
    /// <summary>
    /// Идентификатор плана. Значение <c>0</c> означает, что запись ещё не сохранена
    /// и идентификатор будет присвоен хранилищем.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Наименование плана.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Строки связи с упражнениями: состав плана и порядок выполнения.
    /// </summary>
    /// <remarks>
    /// Связь many-to-many хранится таблицей <c>PlanExercises</c>, и порядок упражнений — это
    /// её данные, поэтому у таблицы есть класс. Восьми править состав напрямую нельзя: вместо
    /// этого принято передавать план, у которого заполнено это свойство.
    /// </remarks>
    public ICollection<PlanExercise> PlanExercises { get; } = [];

    /// <summary>
    /// Упражнения плана в порядке выполнения.
    /// </summary>
    /// <remarks>
    /// Свойство вычисляемое, а не навигация EF: <c>IReadOnlyList</c> навигацией EF не
    /// является, и по соглашению оно всё равно попало бы в модель и сломало <c>Include</c>.
    /// Поэтому контекст исключает его явно — <c>Ignore</c>, а не атрибут <c>[NotMapped]</c>:
    /// EF Core этот атрибут не читает, он работает с Data Annotations, а не с отображением.
    /// Второй ключ сортировки — по наименованию — страховка на случай, если у части планов
    /// номера совпадают или не заполнены: список должен оставаться детерминированным, а не
    /// случайным.
    /// </remarks>
    public IReadOnlyList<Exercise> Exercises =>
        PlanExercises
            .OrderBy(link => link.Order)
            .ThenBy(link => link.Exercise.Name)
            .Select(link => link.Exercise)
            .Where(exercise => exercise is not null)
            .ToList();

    public override string ToString() => Name;

    /// <summary>
    /// Добавляет упражнение в конец порядка выполнения.
    /// </summary>
    /// <remarks>
    /// Место считается по последнему занятому, а не по числу строк: после удаления из середины
    /// номера получаются с пропуском, и добавление по числу строк начало бы выдавать занятый
    /// номер. Плотность номеров в хранилище всё равно восстанавливает репозиторий при записи
    /// плана — она доверяет не этой нумерации, а порядку строк.
    /// </remarks>
    /// <param name="exercise">Упражнение из справочника.</param>
    /// <returns>Добавленная строка связи.</returns>
    public PlanExercise AddExercise(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var last = PlanExercises.Count == 0
            ? 0
            : PlanExercises.Max(link => link.Order);

        var link = new PlanExercise { Exercise = exercise, Order = last + 1 };

        PlanExercises.Add(link);

        return link;
    }
}
