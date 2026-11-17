using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>Перетаскивание числа: сдвиг мыши даёт новое значение.</summary>
/// <remarks>
/// Проверяется чистая часть поведения — расчёт значения. Само перетаскивание мышью в тесте
/// не воспроизводится: оно живёт в WPF и проверяется окном, а правило расчёта обязано быть
/// одно и то же при любом шаге.
/// </remarks>
public sealed class DragValueTests
{
    [Fact]
    public void ComputeValue_Вправо_Больше() =>
        Assert.Equal(910, DragValue.ComputeValue(900, 10, 1));

    [Fact]
    public void ComputeValue_Влево_Меньше() =>
        Assert.Equal(890, DragValue.ComputeValue(900, -10, 1));

    [Fact]
    public void ComputeValue_Шаг_УвеличиваетПрибавку() =>
        Assert.Equal(920, DragValue.ComputeValue(900, 10, 2));

    /// <summary>Дробная часть пикселя не накапливается: сдвиг на полпикселя ничего не меняет.</summary>
    [Fact]
    public void ComputeValue_ПолПикселя_НеМеняет() =>
        Assert.Equal(900, DragValue.ComputeValue(900, 0.4, 1));

    /// <summary>
    /// Порог отсекает дрожание руки: обычный клик по полю не должен двигать число.
    /// </summary>
    [Theory]
    [InlineData(0.5, false)]
    [InlineData(2.9, false)]
    [InlineData(3, true)]
    [InlineData(-3, true)]
    public void ExceedsThreshold_Порог(double offset, bool expected) =>
        Assert.Equal(expected, DragValue.ExceedsThreshold(offset, DragValue.DragThreshold));
}
