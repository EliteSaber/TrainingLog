using System.Collections.ObjectModel;
using System.ComponentModel;

namespace TrainingLog.Core.Models;

/// <summary>
/// План тренировки: наименование и список упражнений из справочника.
/// </summary>
/// <remarks>
/// Модель подписывается на свои изменения: <see cref="INotifyPropertyChanged"/> и
/// <see cref="ObservableCollection{T}"/> — из BCL, зависимости от WPF у ядра не появляется.
/// Нужно это потому, что строка списка планов переживает перечитывание из базы (иначе
/// схлопывается раскрытый план), а пережившая строка берёт значения из той же модели. Без
/// уведомлений сохранённая правка сохранялась бы в базе и молча не появлялась на экране.
/// </remarks>
public sealed class TrainingPlan : INotifyPropertyChanged
{
    /// <summary>
    /// Изменилось наименование или что-то ещё, требующее обновить привязки.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Идентификатор плана. Значение <c>0</c> означает, что запись ещё не сохранена
    /// и идентификатор будет присвоен хранилищем.
    /// </summary>
    public int Id { get; set; }

    private string _name = string.Empty;

    /// <summary>
    /// Наименование плана.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>
    /// Строки связи с упражнениями: состав плана и порядок выполнения.
    /// </summary>
    /// <remarks>
    /// Связь many-to-many хранится таблицей <c>PlanExercises</c>, и порядок упражнений — это
    /// её данные, поэтому у таблицы есть класс. Восьми править состав напрямую нельзя: вместо
    /// этого принято передавать план, у которого заполнено это свойство.
    ///
    /// Объявленный тип остаётся <see cref="ICollection{T}"/>, наблюдаемым делается только
    /// экземпляр: этого хватает разметке, а весь код, который с планом работает, продолжает
    /// видеть обычную коллекцию.
    /// </remarks>
    public ICollection<PlanExercise> PlanExercises { get; } = new ObservableCollection<PlanExercise>();

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
