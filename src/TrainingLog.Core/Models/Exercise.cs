using System.ComponentModel;

namespace TrainingLog.Core.Models;

/// <summary>
/// Упражнение из справочника.
/// </summary>
/// <remarks>
/// Модель уведомляется о своих изменениях через <see cref="INotifyPropertyChanged"/> из BCL —
/// зависимости от WPF у ядра не появляется. Нужно это потому, что строка списка упражнений
/// переживает сохранение правки, а пережившая строка берёт значения из той же модели: без
/// уведомлений переименованное упражнение молча не появилось бы на экране (та же причина, что
/// у <see cref="TrainingPlan"/>).
/// </remarks>
public sealed class Exercise : INotifyPropertyChanged
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

    public override string ToString() => Name;
}
