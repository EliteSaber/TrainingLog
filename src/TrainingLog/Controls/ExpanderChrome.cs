using System.Windows.Media;

namespace TrainingLog.Controls;

/// <summary>Доводка шеврона Expander'а: сдвиг от центра бокса строки к видимому центру строки.</summary>
/// <remarks>
/// VerticalAlignment="Center" центрирует шеврон в боксе строки, но центр бокса не совпадает
/// с видимым центром строки: бокс вмещает и место под выносные знаки, которого у большинства
/// надписей нет. Раньше расхождение компенсировалось ручным сдвигом с места вызова —
/// константы не переживали смену шрифта или кегля в «Параметрах».
///
/// Формула центрует на высоте прописных знаков по метрикам шрифта (Baseline, CapsHeight,
/// LineSpacing — в долях кегля), поэтому сдвиг растёт вместе с кеглем и сам следует
/// за сменой шрифта. Высоту прописных <see cref="FontFamily"/> не отдаёт — берётся у
/// <see cref="Typeface"/> того же семейства. Знак зависит от семейства: WPF берёт ascent
/// из метрик hhea (у Segoe UI это 1.079 em — выше прописных), поэтому у Segoe UI видимый
/// центр строки стоит ниже центра бокса и сдвиг выходит вниз (≈ +0.7 px при кегле 11),
/// а у Arial почти совпадает (−0.13 px). Ручная константа не пережила бы ни смену шрифта,
/// ни смену кегля.
/// </remarks>
public static class ExpanderChrome
{
    /// <summary>Вертикальный сдвиг шеврона в пикселях: отрицательный — вверх от центра бокса.</summary>
    public static double AlignOffset(FontFamily font, double fontSize) =>
        fontSize * OffsetRatio(font);

    /// <summary>
    /// Доля кегля, на которую шеврон уходит от центра бокса: она зависит только от семейства.
    /// </summary>
    /// <remarks>
    /// Доводку считает конвертер, а он срабатывает на каждом <see cref="Expander"/> при каждой
    /// раскладке. Метрики семейства от того не меняются, а <see cref="Typeface"/> на каждый вызов
    /// создавался заново — имя семейства разбиралось и шрифт искался заново. Запомненная доля
    /// переживает и смену кегля (домножается на него), и повторные раскладки.
    /// </remarks>
    private static double OffsetRatio(FontFamily font)
    {
        var source = font.Source;

        if (Ratios.TryGetValue(source, out var ratio))
        {
            return ratio;
        }

        ratio = font.Baseline - new Typeface(source).CapsHeight / 2 - font.LineSpacing / 2;

        Ratios[source] = ratio;

        return ratio;
    }

    /// <summary>Запомненная доля сдвига по семействам. Живёт на нити интерфейса.</summary>
    private static readonly Dictionary<string, double> Ratios = new(StringComparer.Ordinal);
}
