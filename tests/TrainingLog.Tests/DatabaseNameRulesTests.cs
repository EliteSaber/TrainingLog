using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>Правило наименования файла базы: показ без расширения, имя файла — с ним всегда.</summary>
public sealed class DatabaseNameRulesTests
{
    /// <summary>Умолчание из файла настроек: поле показывает имя без хвоста.</summary>
    [Fact]
    public void ForDisplay_УмолчаниеРасширениеСрезается()
    {
        var displayed = DatabaseNameRules.ForDisplay("exercises.db");

        Assert.Equal("exercises", displayed);
    }

    /// <summary>Хвост без расширения — это уже имя поля, обратно срезается ровно он.</summary>
    [Fact]
    public void ForDisplay_БезРасширения_ИмяБезИзменений()
    {
        Assert.Equal("спорт", DatabaseNameRules.ForDisplay("спорт"));
    }

    /// <summary>Регистр хвоста различен у файлов, оставшихся со старых машин.</summary>
    [Theory]
    [InlineData("exercises.DB")]
    [InlineData("exercises.Db")]
    public void ForDisplay_РегистрХвостаНеВажен(string fileName)
    {
        Assert.Equal("exercises", DatabaseNameRules.ForDisplay(fileName));
    }

    /// <summary>Точки внутри имени остаются: срезается только хвост, иначе собрать имя обратно не выйдет.</summary>
    [Fact]
    public void ForDisplay_ТочкиВнутриСохраняются()
    {
        var displayed = DatabaseNameRules.ForDisplay("журнал.2026.db");

        Assert.Equal("журнал.2026", displayed);
    }

    /// <summary>Расширение дописывается всегда: в файле настроек только настоящее имя файла.</summary>
    [Theory]
    [InlineData("спорт", "спорт.db")]
    [InlineData("exercises", "exercises.db")]
    public void ToFileName_РасширениеДописывается(string input, string expected)
    {
        Assert.Equal(expected, DatabaseNameRules.ToFileName(input));
    }

    /// <summary>Впечатанный хвост снимается: иначе сохранение двойное расширение дало бы.</summary>
    [Theory]
    [InlineData("спорт.db", "спорт.db")]
    [InlineData("спорт.DB", "спорт.db")]
    public void ToFileName_ХвостВВводе_НеУдваивается(string input, string expected)
    {
        Assert.Equal(expected, DatabaseNameRules.ToFileName(input));
    }

    /// <summary>Точка внутри имени не мешает: теряется только хвост, расширение дописывается одно.</summary>
    [Fact]
    public void ToFileName_ТочкиВнутри_ХвостОдно()
    {
        Assert.Equal("журнал.2026.db", DatabaseNameRules.ToFileName("журнал.2026"));
    }

    /// <summary>Круг «сохранили — показали» возвращает ровно то, что было в поле.</summary>
    [Fact]
    public void Круг_ПолеФайлПоле_БезИзменений()
    {
        const string field = "журнал.2026";

        var fileName = DatabaseNameRules.ToFileName(field);

        Assert.Equal(field, DatabaseNameRules.ForDisplay(fileName));
    }
}
