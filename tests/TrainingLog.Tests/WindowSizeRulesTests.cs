using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>Пригодность размера окна: границы, общие для файла настроек и для ввода.</summary>
/// <remarks>
/// Проверка одна на оба слоя: значение из файла и значение, введённое в окне, должны
/// отбрасываться по одному правилу, иначе файл принимал бы то, чего окно не примет.
/// </remarks>
public sealed class WindowSizeRulesTests
{
    [Theory]
    [InlineData(100, 100)]
    [InlineData(900, 560)]
    [InlineData(4000, 4000)]
    public void IsValid_ВГраницах_Годен(int width, int height) =>
        Assert.True(WindowSizeRules.IsValid(width, height));

    [Theory]
    [InlineData(99, 560)]
    [InlineData(900, 99)]
    [InlineData(4001, 560)]
    [InlineData(900, 4001)]
    public void IsValid_ЗаГраницами_Негоден(int width, int height) =>
        Assert.False(WindowSizeRules.IsValid(width, height));

    /// <summary>
    /// Высоты нет — размер годен: у окна правки упражнения высота следует за содержимым,
    /// и отсутствие высоты там не ошибка.
    /// </summary>
    [Fact]
    public void IsValid_ВысотыНет_Годен() =>
        Assert.True(WindowSizeRules.IsValid(380, null));
}
