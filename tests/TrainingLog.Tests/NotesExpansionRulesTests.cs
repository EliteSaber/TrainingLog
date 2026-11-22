using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>Правило примечания: канон, негодные значения и когда поле раскрыто.</summary>
public sealed class NotesExpansionRulesTests
{
    /// <summary>Канонические значения годны: их пишет программа.</summary>
    [Theory]
    [InlineData(NotesExpansionRules.Collapsed)]
    [InlineData(NotesExpansionRules.WhenFilled)]
    [InlineData(NotesExpansionRules.WhenHint)]
    [InlineData(NotesExpansionRules.WhenFilledOrHint)]
    [InlineData(NotesExpansionRules.Expanded)]
    public void IsValid_Канон_Годен(string value)
    {
        Assert.True(NotesExpansionRules.IsValid(value));
    }

    /// <summary>Регистр не важен: файл настроек правят руками.</summary>
    [Fact]
    public void IsValid_ДругойРегистр_Годен()
    {
        Assert.True(NotesExpansionRules.IsValid("whenfilledorhint"));
    }

    /// <summary>Подпись окна и постороннее слово — не правило: молча принять их нельзя.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Свернуто")]
    [InlineData("Default")]
    public void IsValid_НеКанон_Негоден(string value)
    {
        Assert.False(NotesExpansionRules.IsValid(value));
    }

    /// <summary>Пустое значение — не правило: в файле может стоять null.</summary>
    [Fact]
    public void IsValid_Null_Негоден()
    {
        Assert.False(NotesExpansionRules.IsValid(null));
    }

    /// <summary>Нормализация приводит к канону: в файле должно лежать то же, что пишет программа.</summary>
    [Theory]
    [InlineData("whenfilled", NotesExpansionRules.WhenFilled)]
    [InlineData("WHENHINT", NotesExpansionRules.WhenHint)]
    [InlineData(" expanded ", NotesExpansionRules.Expanded)]
    public void Normalize_КанонПриводитсяККанону(string value, string expected)
    {
        Assert.Equal(expected, NotesExpansionRules.Normalize(value));
    }

    /// <summary>
    /// Негодное значение заменяется умолчанием, а не отвергается: значение из файла не должно
    /// ронять запуск приложения.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Свернуто")]
    [InlineData(null)]
    public void Normalize_Негодное_ОткатываетсяКУмолчанию(string? value)
    {
        Assert.Equal(NotesExpansionRules.Default, NotesExpansionRules.Normalize(value));
    }

    /// <summary>Умолчание — свёрнуто: без записи в файле поле не раскрывается.</summary>
    [Fact]
    public void Default_Свернуто()
    {
        Assert.Equal(NotesExpansionRules.Collapsed, NotesExpansionRules.Default);
    }

    /// <summary>Свёрнуто — никогда, что бы ни было в примечании.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void IsExpanded_Свернуто_Никогда(bool hasNote, bool hasHint)
    {
        Assert.False(NotesExpansionRules.IsExpanded(NotesExpansionRules.Collapsed, hasNote, hasHint));
    }

    /// <summary>«Если заполнено» смотрит только на текст примечания, подсказка его не заменяет.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void IsExpanded_ЕслиЗаполнено_ПоТексту(bool hasNote, bool hasHint, bool expected)
    {
        Assert.Equal(
            expected,
            NotesExpansionRules.IsExpanded(NotesExpansionRules.WhenFilled, hasNote, hasHint));
    }

    /// <summary>«Если есть подсказка» смотрит только на примечание прошлого дня.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void IsExpanded_ЕслиПодсказка_ПоПодсказке(bool hasNote, bool hasHint, bool expected)
    {
        Assert.Equal(
            expected,
            NotesExpansionRules.IsExpanded(NotesExpansionRules.WhenHint, hasNote, hasHint));
    }

    /// <summary>«Заполнено или подсказка» раскрывает при любом из двух признаков.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void IsExpanded_ЗаполненоИлиПодсказка_ПоОбоим(bool hasNote, bool hasHint, bool expected)
    {
        Assert.Equal(
            expected,
            NotesExpansionRules.IsExpanded(NotesExpansionRules.WhenFilledOrHint, hasNote, hasHint));
    }

    /// <summary>«Всегда раскрыто» не зависит от содержимого.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void IsExpanded_ВсегдаРаскрыто_НеЗависитОтСодержимого(bool hasNote, bool hasHint)
    {
        Assert.True(NotesExpansionRules.IsExpanded(NotesExpansionRules.Expanded, hasNote, hasHint));
    }

    /// <summary>Негодное значение в файле ведёт себя как умолчание, а не роняет окно.</summary>
    [Fact]
    public void IsExpanded_НегодноеЗначение_КакУмолчание()
    {
        Assert.False(NotesExpansionRules.IsExpanded("Свернуто", hasNote: true, hasHint: true));
    }
}
