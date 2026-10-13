using TrainingLog.Core;

namespace TrainingLog.Tests;

/// <summary>
/// Правила импорта: что попадает в поле, а что отбрасывается. Проверяются до всякой базы, потому
/// что окно импорта применяет их и к файлу, и к тому, что пользователь дописал руками.
/// </summary>
public sealed class ExerciseImportRulesTests
{
    [Fact]
    public void Read_ОбрезаетКрая()
    {
        var accepted = ExerciseImportRules.Read(["  Жим лёжа\t"]);

        Assert.Equal(["Жим лёжа"], accepted);
    }

    /// <summary>Пустая строка в файле — обычное дело, и названием она не является.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Read_ПустыеСтроки_Пропускает(string line)
    {
        var accepted = ExerciseImportRules.Read([line, "Жим"]);

        Assert.Equal(["Жим"], accepted);
    }

    [Fact]
    public void Read_СлишкомДлинноеНазвание_Пропускает()
    {
        var tooLong = new string('а', NameRules.MaxNameLength + 1);
        var longest = new string('б', NameRules.MaxNameLength);

        var accepted = ExerciseImportRules.Read([tooLong, longest]);

        Assert.Equal([longest], accepted);
    }

    /// <summary>Повтор в поле быть не должен: требование к полю окна импорта.</summary>
    [Theory]
    [InlineData("Жим лёжа", "Жим лёжа")]
    [InlineData("жим лёжа", "Жим лёжа")]
    [InlineData("ЖИМ ЛЁЖА", "жим лёжа")]
    public void Read_ПовторВФайле_ОставляетПервое(string duplicate, string original)
    {
        var accepted = ExerciseImportRules.Read([original, duplicate, "Тяга"]);

        Assert.Equal([original, "Тяга"], accepted);
    }

    [Fact]
    public void Read_СохраняетПорядокФайла()
    {
        var accepted = ExerciseImportRules.Read(["Тяга", "Жим", "Приседание"]);

        Assert.Equal(["Тяга", "Жим", "Приседание"], accepted);
    }

    /// <summary>
    /// Дубль от текущего справочника виден в поле: человек сверяет список глазами, и молча
    /// выброшенная строка выглядела бы как потерянная. Пропускается он уже при записи.
    /// </summary>
    [Fact]
    public void Read_ДубльОтСправочника_Оставляет()
    {
        var accepted = ExerciseImportRules.Read(["Жим", "Тяга"]);

        Assert.Equal(["Жим", "Тяга"], accepted);
    }

    [Fact]
    public void PrepareForSave_БезОчистки_ОтбрасываетУжеЗаписанное()
    {
        var names = ExerciseImportRules.PrepareForSave(
            ["Жим", "  тяга  ", "Приседание"],
            ["Жим", "Тяга"],
            replaceExisting: false);

        Assert.Equal(["Приседание"], names);
    }

    [Fact]
    public void PrepareForSave_СОчисткой_ОставляетУжеЗаписанное()
    {
        var names = ExerciseImportRules.PrepareForSave(
            ["Жим", "Тяга"],
            ["Жим", "Приседание"],
            replaceExisting: true);

        Assert.Equal(["Жим", "Тяга"], names);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n  \n")]
    public void PrepareForSave_ЗаписыватьНечего_Пусто(string? text)
    {
        var names = ExerciseImportRules.PrepareForSave(
            ExerciseImportRules.SplitLines(text),
            [],
            replaceExisting: false);

        Assert.Empty(names);
    }

    [Fact]
    public void SplitLines_ОбаПереводаСтроки_СчитаютсяКонцомСтроки()
    {
        Assert.Equal(["Жим", "Тяга"], ExerciseImportRules.SplitLines("Жим\r\nТяга"));
        Assert.Equal(["Жим", "Тяга"], ExerciseImportRules.SplitLines("Жим\nТяга"));
        Assert.Equal(["Жим", "", "Тяга"], ExerciseImportRules.SplitLines("Жим\r\n\nТяга"));
        Assert.Equal(["Жим", ""], ExerciseImportRules.SplitLines("Жим\r\n"));
    }
}
