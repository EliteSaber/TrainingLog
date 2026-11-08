using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>
/// Правило шрифтовой настройки проверяет чистые значения: наличие гарнитуры в системе —
/// вопрос WPF-слоя, и правилу знать о нём не нужно.
/// </summary>
public sealed class FontRulesTests
{
    [Theory]
    [InlineData(79)]
    [InlineData(151)]
    [InlineData(0)]
    [InlineData(-5)]
    public void IsScaleValid_ВнеДиапазона_НеДопустимо(int percent)
    {
        Assert.False(FontRules.IsScaleValid(percent));
    }

    /// <summary>Правило включительное, и оба края годные — иначе проверялось бы не то.</summary>
    [Theory]
    [InlineData(FontRules.MinScalePercent)]
    [InlineData(FontRules.MaxScalePercent)]
    [InlineData(FontRules.DefaultScalePercent)]
    public void IsScaleValid_ГраницыДиапазона_Принимаются(int percent)
    {
        Assert.True(FontRules.IsScaleValid(percent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void NormalizeName_ПробельноеИмя_ПустаяСтрока(string name)
    {
        Assert.Equal(string.Empty, FontRules.NormalizeName(name));
    }

    [Fact]
    public void NormalizeName_СЗнаком_ОтрезаетПробельныеКрая()
    {
        Assert.Equal("Fira Code", FontRules.NormalizeName("  Fira Code  "));
    }

    [Fact]
    public void NormalizeName_ЗначенияНет_ПустаяСтрока()
    {
        Assert.Equal(string.Empty, FontRules.NormalizeName(null));
    }

    /// <summary>Стопроцентный масштаб ничего не меняет: проверяется не умножение, а деление.</summary>
    [Fact]
    public void Scale_СтоПроцентов_РазмерБезИзменений()
    {
        Assert.Equal(11, FontRules.Scale(11, 100));
    }

    [Theory]
    [InlineData(125, 13.75)]
    [InlineData(80, 8.8)]
    [InlineData(150, 16.5)]
    public void Scale_КегльМасштабируетсяПроцентом(int percent, double expected)
    {
        Assert.Equal(expected, FontRules.Scale(11, percent));
    }
}
