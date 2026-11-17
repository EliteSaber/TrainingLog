using System.Globalization;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Кнопка «Удалить» активна, только когда строка не редактируется.
/// </summary>
public sealed class IsNotEditedConverterTests
{
    private static readonly IsNotEditedConverter Converter = new();

    [Fact]
    public void Convert_ТотЖеОбъект_КнопкаНеактивна()
    {
        var exercise = new Core.Models.Exercise { Name = "Жим" };

        var result = Converter.Convert([exercise, exercise], typeof(bool), null!, CultureInfo.InvariantCulture);

        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_ДругиеОбъекты_КнопкаАктивна()
    {
        var exercise = new Core.Models.Exercise { Name = "Жим" };
        var other = new Core.Models.Exercise { Name = "Тяга" };

        var result = Converter.Convert([exercise, other], typeof(bool), null!, CultureInfo.InvariantCulture);

        Assert.True((bool)result);
    }

    /// <summary>Окна правки нет — редактируемого объекта нет, и все кнопки активны.</summary>
    [Fact]
    public void Convert_ПравкиНет_КнопкаАктивна()
    {
        var exercise = new Core.Models.Exercise { Name = "Жим" };

        var result = Converter.Convert(new object[] { exercise, null! }, typeof(bool), null!, CultureInfo.InvariantCulture);

        Assert.True((bool)result);
    }

    /// <summary>Защита от неполной привязки: лучше активная кнопка, чем молчание.</summary>
    [Fact]
    public void Convert_НеполныеЗначения_КнопкаАктивна()
    {
        var exercise = new Core.Models.Exercise { Name = "Жим" };

        var result = Converter.Convert([exercise], typeof(bool), null!, CultureInfo.InvariantCulture);

        Assert.True((bool)result);
    }
}
