using TrainingLog.Core;

namespace TrainingLog.Tests;

/// <summary>
/// Правило проверки наименования управляет активностью кнопок «Добавить», «Принять» и «Добавить»
/// в окне планов. Правило одно для упражнений и планов, поэтому проверяется на именах, а не на
/// сущностях: какая сущность стоит за наименованием, правилу знать не нужно.
/// </summary>
/// <remarks>
/// Здесь же зафиксирован регрессионный случай: пустое наименование обязано давать
/// недопустимое, иначе кнопка добавления активна на пустом поле.
/// </remarks>
public sealed class NameRulesTests
{
    private static readonly string[] SquatNames = ["Приседание", "Жим"];
    private static readonly string[] OtherNames = ["Жим"];

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsValid_ПустоеНазвание_НеДопустимо(string name)
    {
        Assert.False(NameRules.IsValid(name, SquatNames));
    }

    [Fact]
    public void IsValid_НовоеНазвание_Допустимо()
    {
        Assert.True(NameRules.IsValid("Тяга", SquatNames));
    }

    [Theory]
    [InlineData("приседание")]
    [InlineData("ПРИСЕДАНИЕ")]
    [InlineData("  Приседание  ")]
    public void IsValid_СовпадениеПоРегистру_НеДопустимо(string name)
    {
        Assert.False(NameRules.IsValid(name, SquatNames));
    }

    /// <summary>
    /// Своё же наименование правило дублем не считает только если вызывающий убрал его из списка:
    /// проверка на правку идёт через <see cref="NameRules.IsValidEdit"/>.
    /// </summary>
    [Fact]
    public void IsValid_СвоёНазваниеВСписке_СчитаетсяДубликатом()
    {
        Assert.False(NameRules.IsValid("Приседание", SquatNames));
    }

    [Fact]
    public void IsValid_ПустойСписок_ДопускаетЛюбоеНепустоеНазвание()
    {
        Assert.True(NameRules.IsValid("Жим", []));
        Assert.False(NameRules.IsValid("  ", []));
    }

    [Theory]
    [InlineData("Приседание")]
    [InlineData("  Приседание  ")]
    [InlineData("\tПриседание")]
    public void IsValidEdit_БезИзменений_НеДопустимо(string name)
    {
        Assert.False(NameRules.IsValidEdit(name, "Приседание", OtherNames));
    }

    [Fact]
    public void IsValidEdit_СменаРегистраСчитаетсяИзменением()
    {
        Assert.True(NameRules.IsValidEdit("приседание", "Приседание", OtherNames));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidEdit_ПустоеНазвание_НеДопустимо(string name)
    {
        Assert.False(NameRules.IsValidEdit(name, "Приседание", OtherNames));
    }

    [Theory]
    [InlineData("Жим")]
    [InlineData("жим")]
    [InlineData("ЖИМ ")]
    public void IsValidEdit_НазваниеДругойЗаписи_НеДопустимо(string name)
    {
        Assert.False(NameRules.IsValidEdit(name, "Приседание", OtherNames));
    }

    [Fact]
    public void IsValidEdit_НовоеУникальноеНазвание_Допустимо()
    {
        Assert.True(NameRules.IsValidEdit("Тяга", "Приседание", OtherNames));
    }

    /// <summary>
    /// Предел длины считается по обрезанному наименованию: хвостовые пробелы его не занимают.
    /// Поле ввода с <c>MaxLength</c> считает сырые символы строже, но правило — источник
    /// истины, и оно должно принимать наименование, которое в обрезанном виде в лимит укладывается.
    /// </summary>
    [Fact]
    public void IsValid_НазваниеДлинойВПределСПробелами_Допустимо()
    {
        var name = new string('а', NameRules.MaxNameLength);

        Assert.True(NameRules.IsValid($"  {name}  ", SquatNames));
    }

    [Fact]
    public void IsValid_НазваниеДлиннееПредела_НеДопустимо()
    {
        var name = new string('а', NameRules.MaxNameLength + 1);

        Assert.False(NameRules.IsValid(name, SquatNames));
    }

    [Fact]
    public void IsValidEdit_НазваниеДлинойВПредел_Допустимо()
    {
        var name = new string('а', NameRules.MaxNameLength);

        Assert.True(NameRules.IsValidEdit(name, "Приседание", OtherNames));
    }

    [Fact]
    public void IsValidEdit_НазваниеДлиннееПредела_НеДопустимо()
    {
        var name = new string('а', NameRules.MaxNameLength + 1);

        Assert.False(NameRules.IsValidEdit(name, "Приседание", OtherNames));
    }
}
