using System.Globalization;

namespace TrainingLog.ViewModels;

/// <summary>
/// Один столбец строки дня: упражнение и его подходы.
/// </summary>
/// <remarks>
/// Упражнению принадлежит столько столбцов, сколько у него подходов, поэтому
/// <see cref="Width"/> считается от числа подходов. Это и есть приём, из-за которого вес
/// стоит ровно над своим повторением, а столбцы разных упражнений совпадают: ширина
/// ячейки задана один раз в <see cref="DayRowViewModel.CellWidth"/>, и одинакова во всех
/// строках дня.
///
/// Примечание принадлежит упражнению целиком, а не подходу, поэтому в строке дня оно
/// занимает свою строку под повторениями и растягивается на все столбцы упражнения.
/// </remarks>
public sealed class DayExerciseCell
{
    public DayExerciseCell(string name, int setCount, double cellWidth, string? note)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        Note = note;

        // Минимум одна колонка: упражнение без подходов тоже занимает место под своим названием.
        Sets = Math.Max(1, setCount);
        Width = Sets * cellWidth;
    }

    /// <summary>
    /// Примечание к упражнению, как оно записано за этот день. Пусто или <c>null</c> —
    /// примечания нет.
    /// </summary>
    /// <remarks>
    /// Копия из <see cref="ExerciseEntry.Notes"/>: примечание принадлежит записи журнала, и
    /// правится только вместе с ней — в окне дня. Здесь оно только показывается.
    /// </remarks>
    public string? Note { get; }

    /// <summary>
    /// Есть ли примечание, то есть показывать ли его кнопкой. Пробелы примечанием не
    /// считаются: показать «Показать» не о чем.
    /// </summary>
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// <summary>Название упражнения на момент выполнения.</summary>
    public string Name { get; }

    /// <summary>Сколько подходов у упражнения — столько столбцов ему принадлежит.</summary>
    public int Sets { get; }

    /// <summary>Ширина столбца вместе с названием: столько, сколько занимают подходы.</summary>
    public double Width { get; }
}