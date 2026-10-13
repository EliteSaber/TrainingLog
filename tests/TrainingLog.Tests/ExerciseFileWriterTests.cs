using TrainingLog.Core;

namespace TrainingLog.Tests;

/// <summary>
/// Выгрузка справочника в текстовый файл. Формат обязан быть тем же, что читает импорт, иначе
/// выгруженный файл не вернуть обратно — а это и есть смысл выгрузки.
/// </summary>
public sealed class ExerciseFileWriterTests
{
    /// <summary>Кириллица не поедет ни в блокноте, ни в старых программах: BOM обязателен.</summary>
    [Fact]
    public void Write_ПишетUtf8СBom()
    {
        var path = TemporaryPath();

        try
        {
            Assert.True(ExerciseFileWriter.Write(path, ["Жим"]));

            var bytes = File.ReadAllBytes(path);

            Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
            Assert.Equal("Жим\r\n", ExerciseFileReader.Decode(bytes));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_ПоОдномуУпражнениюНаСтроку()
    {
        var path = TemporaryPath();

        try
        {
            ExerciseFileWriter.Write(path, ["Жим", "Тяга"]);

            // \r\n и перевод строки в конце: так файл выглядит в редакторе, а импорт лишнюю
            // пустую строку отбрасывает.
            Assert.Equal("Жим\r\nТяга\r\n", ReadWithoutBom(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Круг выгрузки и импорта: написанное читается обратно тем же списком.</summary>
    [Fact]
    public void Write_АПотомЧтение_СписокСовпадает()
    {
        var names = new[] { "Жим лёжа", "Приседание", "тяга" };
        var path = TemporaryPath();

        try
        {
            ExerciseFileWriter.Write(path, names);

            Assert.Equal(names, ExerciseFileReader.ReadLines(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_ПустойСписок_ПустойФайл()
    {
        var path = TemporaryPath();

        try
        {
            Assert.True(ExerciseFileWriter.Write(path, []));

            Assert.Empty(ExerciseFileReader.ReadLines(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Отказ возвращается значением: программа может стоять в папке без права записи, и падать на
    /// выбранном пользователем пути нельзя. Запись файла поверх каталога падает одинаково на любой
    /// машине и в любом процессе, тогда как запрет записи правами зависит от прав.
    /// </summary>
    [Fact]
    public void Write_ПутьКаталогом_ВозвращаетFalse()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"TrainingLogExport-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            Assert.False(ExerciseFileWriter.Write(folder, ["Жим"]));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string TemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"TrainingLogExport-{Guid.NewGuid():N}.txt");

    private static string ReadWithoutBom(string path) =>
        ExerciseFileReader.Decode(File.ReadAllBytes(path));
}
