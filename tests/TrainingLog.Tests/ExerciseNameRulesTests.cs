using TrainingLog.Core;
using TrainingLog.Core.Models;

namespace TrainingLog.Tests;

/// <summary>
/// Правило проверки названия управляет активностью кнопок «Добавить» и «Принять».
/// </summary>
/// <remarks>
/// Здесь же зафиксирован регрессионный случай: пустое название обязано давать
/// недопустимое, иначе кнопка добавления активна на пустом поле.
/// </remarks>
public sealed class ExerciseNameRulesTests
{
    private static readonly Exercise Squat = new() { Id = 1, Name = "Приседание" };
    private static readonly Exercise Bench = new() { Id = 2, Name = "Жим" };

    private static readonly Exercise[] All = [Squat, Bench];

    /// <summary>Справочник без редактируемого упражнения — так его и передаёт модель правки.</summary>
    private static readonly Exercise[] Other = [Bench];

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsValid_ПустоеНазвание_НеДопустимо(string name)
    {
        Assert.False(ExerciseNameRules.IsValid(name, All));
    }

    [Fact]
    public void IsValid_НовоеНазвание_Допустимо()
    {
        Assert.True(ExerciseNameRules.IsValid("Тяга", All));
    }

    [Theory]
    [InlineData("приседание")]
    [InlineData("ПРИСЕДАНИЕ")]
    [InlineData("  Приседание  ")]
    public void IsValid_СовпадениеПоРегистру_НеДопустимо(string name)
    {
        Assert.False(ExerciseNameRules.IsValid(name, All));
    }

    [Fact]
    public void IsValid_СобственноеНазваниеПриПравке_Допустимо()
    {
        Assert.True(ExerciseNameRules.IsValid("Приседание", All, excludeId: Squat.Id));
        Assert.True(ExerciseNameRules.IsValid("ПРИСЕДАНИЕ", All, excludeId: Squat.Id));
    }

    [Fact]
    public void IsValid_ЧужоеНазваниеПриПравке_НеДопустимо()
    {
        Assert.False(ExerciseNameRules.IsValid("Жим", All, excludeId: Squat.Id));
    }

    [Fact]
    public void IsValid_ПустойСправочник_ДопускаетЛюбоеНепустоеНазвание()
    {
        Assert.True(ExerciseNameRules.IsValid("Жим", []));
        Assert.False(ExerciseNameRules.IsValid("  ", []));
    }

    [Theory]
    [InlineData("Приседание")]
    [InlineData("  Приседание  ")]
    [InlineData("\tПриседание")]
    public void IsValidEdit_БезИзменений_НеДопустимо(string name)
    {
        Assert.False(ExerciseNameRules.IsValidEdit(name, "Приседание", Other));
    }

    [Fact]
    public void IsValidEdit_СменаРегистраСчитаетсяИзменением()
    {
        Assert.True(ExerciseNameRules.IsValidEdit("приседание", "Приседание", Other));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidEdit_ПустоеНазвание_НеДопустимо(string name)
    {
        Assert.False(ExerciseNameRules.IsValidEdit(name, "Приседание", Other));
    }

    [Theory]
    [InlineData("Жим")]
    [InlineData("жим")]
    [InlineData("ЖИМ ")]
    public void IsValidEdit_НазваниеДругогоУпражнения_НеДопустимо(string name)
    {
        Assert.False(ExerciseNameRules.IsValidEdit(name, "Приседание", Other));
    }

    [Fact]
    public void IsValidEdit_НовоеУникальноеНазвание_Допустимо()
    {
        Assert.True(ExerciseNameRules.IsValidEdit("Тяга", "Приседание", Other));
    }

    /// <summary>
    /// Предел длины считается по обрезанному названию: хвостовые пробелы его не занимают.
    /// Поле ввода с <c>MaxLength</c> считает сырые символы строже, но правило — источник
    /// истины, и оно должно принимать название, которое в обрезанном виде в лимит укладывается.
    /// </summary>
    [Fact]
    public void IsValid_НазваниеДлинойВПределСПробелами_Допустимо()
    {
        var name = new string('а', ExerciseNameRules.MaxNameLength);

        Assert.True(ExerciseNameRules.IsValid($"  {name}  ", All));
    }

    [Fact]
    public void IsValid_НазваниеДлиннееПредела_НеДопустимо()
    {
        var name = new string('а', ExerciseNameRules.MaxNameLength + 1);

        Assert.False(ExerciseNameRules.IsValid(name, All));
    }

    [Fact]
    public void IsValidEdit_НазваниеДлинойВПредел_Допустимо()
    {
        var name = new string('а', ExerciseNameRules.MaxNameLength);

        Assert.True(ExerciseNameRules.IsValidEdit(name, "Приседание", Other));
    }

    [Fact]
    public void IsValidEdit_НазваниеДлиннееПредела_НеДопустимо()
    {
        var name = new string('а', ExerciseNameRules.MaxNameLength + 1);

        Assert.False(ExerciseNameRules.IsValidEdit(name, "Приседание", Other));
    }
}
