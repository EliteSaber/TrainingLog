using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>Правило темы: канон, негодные значения и разбор системной темы.</summary>
public sealed class ThemeRulesTests
{
    /// <summary>Канонические значения годны: их пишет программа.</summary>
    [Theory]
    [InlineData(ThemeRules.Light)]
    [InlineData(ThemeRules.Dark)]
    [InlineData(ThemeRules.System)]
    public void IsValid_Канон_Годен(string theme)
    {
        Assert.True(ThemeRules.IsValid(theme));
    }

    /// <summary>Регистр не важен: файл настроек правят руками.</summary>
    [Fact]
    public void IsValid_ДругойРегистр_Годен()
    {
        Assert.True(ThemeRules.IsValid("dark"));
    }

    /// <summary>Подпись окна и постороннее слово — не тема: молча принять их нельзя.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Тёмная")]
    [InlineData("Default")]
    public void IsValid_НеКанон_Негоден(string theme)
    {
        Assert.False(ThemeRules.IsValid(theme));
    }

    /// <summary>Пустое значение — не тема: в файле может стоять null.</summary>
    [Fact]
    public void IsValid_Null_Негоден()
    {
        Assert.False(ThemeRules.IsValid(null));
    }

    /// <summary>Нормализация приводит к канону: в файле должно лежать то же, что пишет программа.</summary>
    [Theory]
    [InlineData("dark", ThemeRules.Dark)]
    [InlineData("LIGHT", ThemeRules.Light)]
    [InlineData(" system ", ThemeRules.System)]
    public void Normalize_КанонПриводитсяККанону(string value, string expected)
    {
        Assert.Equal(expected, ThemeRules.Normalize(value));
    }

    /// <summary>
    /// Негодное значение заменяется умолчанием, а не отвергается: значение из файла не должно
    /// ронять запуск приложения.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Тёмная")]
    [InlineData(null)]
    public void Normalize_Негодное_ОткатываетсяКУмолчанию(string? value)
    {
        Assert.Equal(ThemeRules.Default, ThemeRules.Normalize(value));
    }

    /// <summary>Системная тема сводится к теме Windows: система тёмная — значит тёмная.</summary>
    [Theory]
    [InlineData(true, ThemeRules.Dark)]
    [InlineData(false, ThemeRules.Light)]
    public void Effective_Системная_СводитсяКТемеWindows(bool systemIsDark, string expected)
    {
        Assert.Equal(expected, ThemeRules.Effective(ThemeRules.System, systemIsDark));
    }

    /// <summary>Выбранная тема системе не подчиняется: тёмная остаётся тёмной и на светлой Windows.</summary>
    [Theory]
    [InlineData(ThemeRules.Dark, true)]
    [InlineData(ThemeRules.Dark, false)]
    [InlineData(ThemeRules.Light, true)]
    [InlineData(ThemeRules.Light, false)]
    public void Effective_Выбранная_НеЗависитОтСистемы(string theme, bool systemIsDark)
    {
        Assert.Equal(theme, ThemeRules.Effective(theme, systemIsDark));
    }
}
