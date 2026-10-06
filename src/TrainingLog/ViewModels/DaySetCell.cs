using System.Globalization;

namespace TrainingLog.ViewModels;

/// <summary>
/// Один подход в строке дня: вес и повторения.
/// </summary>
/// <remarks>
/// Подход показывается сразу в двух строках — вес и повторения, — и лежит в обеих
/// привязках одними и теми же данными. Расхождение между ними поэтому невозможно
/// по построению.
/// </remarks>
public sealed class DaySetCell
{
    public DaySetCell(string weightText, string repetitionText, double cellWidth)
    {
        WeightText = weightText;
        RepetitionText = repetitionText;
        CellWidth = cellWidth;
    }

    /// <summary>Вес подхода в килограммах.</summary>
    public string WeightText { get; }

    /// <summary>Количество повторений.</summary>
    public string RepetitionText { get; }

    /// <summary>Ширина столбца подхода — одинаковая у всех подходов и всех упражнений.</summary>
    public double CellWidth { get; }
}