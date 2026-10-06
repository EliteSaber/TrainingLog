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
/// </remarks>
public sealed class DayExerciseCell
{
    public DayExerciseCell(string name, int setCount, double cellWidth)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;

        // Минимум одна колонка: упражнение без подходов тоже занимает место под своим названием.
        Sets = Math.Max(1, setCount);
        Width = Sets * cellWidth;
    }

    /// <summary>Название упражнения на момент выполнения.</summary>
    public string Name { get; }

    /// <summary>Сколько подходов у упражнения — столько столбцов ему принадлежит.</summary>
    public int Sets { get; }

    /// <summary>Ширина столбца вместе с названием: столько, сколько занимают подходы.</summary>
    public double Width { get; }
}