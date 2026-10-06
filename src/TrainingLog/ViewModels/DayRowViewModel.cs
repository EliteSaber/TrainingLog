using System.Collections.ObjectModel;
using System.Globalization;
using TrainingLog.Core.Models;

namespace TrainingLog.ViewModels;

/// <summary>
/// Строка журнала: дата, план и упражнения с подходами.
/// </summary>
/// <remarks>
/// Четыре строки и три столбца: дата занимает все строки, слева от упражнений стоят подписи
/// «Вес» и «Повторения», а дальше идут столбцы подходов. Столбцы упражнений, весов и
/// повторений — это три горизонтальных списка, а не <c>Grid</c> с генерируемыми колонками:
/// ширина ячейки фиксирована, поэтому столбцы в строках совпадают по позиции, а вес стоит
/// ровно над своим повторением. Столбцы у <c>Grid</c> по числу упражнений множились бы на
/// число подходов, иначе динамическая ширина разъезжалась бы между строками дня.
/// </remarks>
public sealed class DayRowViewModel
{
    /// <summary>
    /// Ширина столбца подхода. Задана один раз и одинакова для всех подходов всех дней —
    /// иначе вес и повторения одного подхода разъедутся по ширине.
    /// </summary>
    public const double CellWidth = 76;

    private DayRowViewModel(DateOnly date, string planName)
    {
        Date = date;
        PlanName = planName;
    }

    /// <summary>Дата тренировки.</summary>
    public DateOnly Date { get; }

    /// <summary>
    /// Дата сокращённым днём недели: «05.10 (вс)». Сокращение берётся у текущей культуры,
    /// поэтому в русской раскладке это «вс», а в английской «Sun».
    /// </summary>
    /// <remarks>
    /// Через <see cref="DateTimeFormatInfo.GetAbbreviatedDayName(DayOfWeek)"/>, а не через
    /// <c>DayOfWeek.ToString("ddd", ...)</c>: у перечисления параметр культуры не учитывается,
    /// и такое написание устарело — название дня всегда выходило бы английским.
    /// </remarks>
    public string DateText =>
        $"{Date.ToString("dd.MM", CultureInfo.CurrentCulture)} "
        + $"({CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(Date.DayOfWeek)})";

    /// <summary>Название плана: копия названия на момент выполнения.</summary>
    public string PlanName { get; }

    /// <summary>Упражнения дня в порядке выполнения.</summary>
    public ObservableCollection<DayExerciseCell> Exercises { get; } = [];

    /// <summary>
    /// Подходы дня по порядку упражнений. Список один на обе строки — веса и повторения —
    /// поэтому разъехаться они не могут.
    /// </summary>
    public ObservableCollection<DaySetCell> Sets { get; } = [];

    /// <summary>Создаёт строку дня из записи журнала.</summary>
    /// <param name="session">Запись журнала.</param>
    public static DayRowViewModel Create(TrainingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var row = new DayRowViewModel(session.Date, session.PlanName);

        foreach (var entry in session.Exercises.OrderBy(entry => entry.Order))
        {
            var sets = entry.Sets.OrderBy(set => set.Order).ToList();

            row.Exercises.Add(new DayExerciseCell(entry.ExerciseName, sets.Count, CellWidth));

            foreach (var set in sets)
            {
                row.Sets.Add(new DaySetCell(
                    set.Weight.ToString("0.##", CultureInfo.CurrentCulture),
                    set.Repetitions.ToString(CultureInfo.CurrentCulture),
                    CellWidth));
            }
        }

        return row;
    }
}