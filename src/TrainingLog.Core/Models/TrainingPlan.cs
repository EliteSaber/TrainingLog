using System.Collections.ObjectModel;
using System.ComponentModel;

namespace TrainingLog.Core.Models;

/// <summary>План тренировки: наименование и состав упражнений из справочника.</summary>
/// <remarks>
/// Модель уведомляется о своих изменениях через <see cref="INotifyPropertyChanged"/> и
/// <see cref="ObservableCollection{T}"/> из BCL — зависимости от WPF у ядра не появляется.
/// Нужно это потому, что строка списка планов переживает перечитывание из базы, а пережившая
/// строка берёт значения из той же модели: без уведомлений сохранённая правка молча не
/// появилась бы на экране.
/// </remarks>
public sealed class TrainingPlan : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Идентификатор. <c>0</c> — запись ещё не сохранена.</summary>
    public int Id { get; set; }

    private string _name = string.Empty;

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
    /// Состав плана и порядок выполнения. Объявленный тип остаётся <see cref="ICollection{T}"/>,
    /// наблюдаемым делается только экземпляр — этого хватает разметке, а весь код, который с
    /// планом работает, продолжает видеть обычную коллекцию.
    /// </summary>
    public ICollection<PlanExercise> PlanExercises { get; } = new ObservableCollection<PlanExercise>();

    /// <summary>Упражнения плана в порядке выполнения.</summary>
    /// <remarks>
    /// Свойство вычисляемое, а не навигация EF: по соглашению оно всё равно попало бы в модель
    /// и сломало <c>Include</c>, поэтому контекст исключает его через <c>Ignore</c>, а не
    /// атрибут <c>[NotMapped]</c> — EF Core этот атрибут не читает. Второй ключ сортировки,
    /// по наименованию, — страховка на случай совпавших или незаполненных номеров: список
    /// должен оставаться детерминированным, а не случайным.
    /// </remarks>
    public IReadOnlyList<Exercise> Exercises =>
        PlanExercises
            .OrderBy(link => link.Order)
            .ThenBy(link => link.Exercise.Name)
            .Select(link => link.Exercise)
            .Where(exercise => exercise is not null)
            .ToList();

    public override string ToString() => Name;

    /// <summary>Добавляет упражнение в конец порядка выполнения.</summary>
    /// <remarks>
    /// Место считается по последнему занятому, а не по числу строк: после удаления из середины
    /// номера получаются с пропуском, и добавление по числу строк начало бы выдавать занятый
    /// номер. Плотность номеров в хранилище восстанавливает репозиторий при записи плана.
    /// </remarks>
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
