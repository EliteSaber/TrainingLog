using System.Windows;
using System.Windows.Media;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Доводка шеврона Expander'а: сдвиг выводится из метрик шрифта и растёт вместе с кеглем,
/// поэтому ручная константа подмены не требует. Проверяется чистая функция без окна.
/// </summary>
public sealed class ExpanderChromeTests
{
    /// <summary>Шрифт, стоящий в «Параметрах» по умолчанию.</summary>
    private static readonly FontFamily SegoeUi = new("Segoe UI");

    [Fact]
    public void AlignOffset_НулевойКегль_СдвигаНет()
    {
        Assert.Equal(0, ExpanderChrome.AlignOffset(SegoeUi, 0));
    }

    /// <summary>
    /// Формула линейна по кеглю: метрики FontFamily — в долях кегля, поэтому при удвоенном
    /// кегле расход центра бокса и видимого центра удваивается ровно.
    /// </summary>
    [Fact]
    public void AlignOffset_КегльУдвоен_СдвигУдваивается()
    {
        for (var size = 8; size <= 24; size += 4)
        {
            Assert.Equal(
                ExpanderChrome.AlignOffset(SegoeUi, size) * 1.5,
                ExpanderChrome.AlignOffset(SegoeUi, size * 1.5),
                precision: 10);
        }
    }

    /// <summary>
    /// Одна и та же семантика для разных кеглей: относительная поправка (сдвиг на кегль)
    /// от кегля не зависит и определяется только шрифтом.
    /// </summary>
    [Fact]
    public void AlignOffset_КеглиРазные_ОтносительнаяПоправкаОдинакова()
    {
        var at11 = ExpanderChrome.AlignOffset(SegoeUi, 11) / 11;
        var at16 = ExpanderChrome.AlignOffset(SegoeUi, 16) / 16;
        var at24 = ExpanderChrome.AlignOffset(SegoeUi, 24) / 24;

        Assert.Equal(at11, at16, precision: 10);
        Assert.Equal(at11, at24, precision: 10);
    }

    /// <summary>
    /// Поправка размером в доли кегля и направлена то вверх, то вниз в зависимости от
    /// семейства: у Segoe UI ascent по hhea заметно выше прописных, и видимый центр строки
    /// стоит ниже центра бокса (+0.064 em), у Arial — почти на уровне (−0.011 em). Выход
    /// за десятую долю кегля в любую сторону означал бы формулу не по метрикам.
    /// </summary>
    [Fact]
    public void AlignOffset_РядовыеШрифты_ПоправкаВДоляхКегля()
    {
        foreach (var font in new[] { new FontFamily("Segoe UI"), new FontFamily("Arial"), new FontFamily("Consolas") })
        {
            Assert.InRange(ExpanderChrome.AlignOffset(font, 11) / 11, -0.1, 0.1);
        }
    }

    /// <summary>
    /// Конкретные метрики Segoe UI (Baseline 1.079, LineSpacing 1.330, CapsHeight 0.700):
    /// сдвиг +0.70 px при кегле 11 — вниз, из-за высокого hhea-ascent семейства.
    /// У Segoe UI метрики стабильны между версиями Windows 10 и 11.
    /// </summary>
    [Fact]
    public void AlignOffset_SegoeUI_Кегль11_СдвигВнизНаДолЮКегля()
    {
        Assert.Equal(0.70361328125, ExpanderChrome.AlignOffset(SegoeUi, 11), precision: 8);
    }

    /// <summary>
    /// Разные шрифты дают разные поправки при одном кегле: значения должны идти из метрик
    /// семейства, а не из свёрнутого умолчания любого шрифта.
    /// </summary>
    [Fact]
    public void AlignOffset_ШрифтыРазные_ПоправкиРазные()
    {
        Assert.NotEqual(
            ExpanderChrome.AlignOffset(SegoeUi, 11),
            ExpanderChrome.AlignOffset(new FontFamily("Consolas"), 11),
            precision: 1);
    }

    /// <summary>Года пара FontFamily + кегль — считается сдвиг.</summary>
    [Fact]
    public void Convert_ГоднаяПара_СчитаетСдвиг()
    {
        var converter = new FontCenterOffsetConverter();
        var font = new FontFamily("Segoe UI");

        Assert.Equal(ExpanderChrome.AlignOffset(font, 11),
                     converter.Convert(new object?[] { font, 11d }, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Негодная пара — UnsetValue, не ноль: подмена нуля выдавала бы сбою видом расчёта.</summary>
    [Fact]
    public void Convert_НегодныеЗначения_Unset()
    {
        var converter = new FontCenterOffsetConverter();
        var unset = DependencyProperty.UnsetValue;

        Assert.Equal(unset, converter.Convert(null, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(unset, converter.Convert(new object?[] { null, 11d }, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(unset, converter.Convert(new object?[] { SegoeUi, "11" }, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture));
    }
}
