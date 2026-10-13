using System.Text;
using TrainingLog.Core;

namespace TrainingLog.Tests;

/// <summary>
/// Чтение файла со списком упражнений. Кодировка задаётся байтами: русский список, сохранённый
/// в windows-1251, и UTF-8 разбираются по-разному, а молча прочитанный мусор попал бы в справочник.
/// </summary>
public sealed class ExerciseFileReaderTests
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Кодировки вне ASCII доступны только после регистрации провайдера: её же регистрирует и
    /// сам читатель, но тест собирает байты в windows-1251 сам.
    /// </summary>
    static ExerciseFileReaderTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// BOM пропускается, а не декодируется: иначе первое упражнение получило бы невидимый
    /// U+FEFF и не совпало бы с тем же упражнением из файла без BOM.
    /// </summary>
    [Fact]
    public void Decode_Utf8СBom_НеОставляетНевидимыхСимволов()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(StrictUtf8.GetBytes("Жим лёжа"))
            .ToArray();

        var text = ExerciseFileReader.Decode(bytes);

        Assert.Equal("Жим лёжа", text);
    }

    [Fact]
    public void Decode_Utf8БезBom_Читается()
    {
        var text = ExerciseFileReader.Decode(StrictUtf8.GetBytes("Жим лёжа\nПриседание"));

        Assert.Equal("Жим лёжа\nПриседание", text);
    }

    [Fact]
    public void Decode_Windows1251_Читается()
    {
        var source = "Жим лёжа\nПриседание";
        var bytes = Encoding.GetEncoding(1251).GetBytes(source);

        // Байты обязаны быть невалидным UTF-8, иначе тест проверял бы не разбор, а совпадение.
        Assert.Throws<DecoderFallbackException>(() => StrictUtf8.GetString(bytes));

        Assert.Equal(source, ExerciseFileReader.Decode(bytes));
    }

    /// <summary>
    /// UTF-16 разбирать нечем, а как windows-1251 он дал бы мусор: лучше отказ с текстом.
    /// </summary>
    [Theory]
    [InlineData(0xFF, 0xFE)]
    [InlineData(0xFE, 0xFF)]
    public void Decode_Utf16_Отказ(byte first, byte second)
    {
        var bytes = new byte[] { first, second, 0x41, 0x00 };

        var exception = Assert.Throws<NotSupportedException>(() => ExerciseFileReader.Decode(bytes));

        Assert.Contains("UTF-16", exception.Message);
    }

    [Fact]
    public void Decode_ПустойФайл_ПустаяСтрока()
    {
        Assert.Equal(string.Empty, ExerciseFileReader.Decode([]));
    }

    [Fact]
    public void ReadLines_ДелитПоПереводамСтрок()
    {
        using var file = new TemporaryFile(StrictUtf8.GetBytes("Жим\r\nТяга\nПриседание"));

        var lines = ExerciseFileReader.ReadLines(file.FilePath);

        Assert.Equal(["Жим", "Тяга", "Приседание"], lines);
    }

    [Fact]
    public void ReadLines_КириллицаВWindows1251_Читается()
    {
        var bytes = Encoding.GetEncoding(1251).GetBytes("Жим лёжа\r\nПриседание");
        using var file = new TemporaryFile(bytes);

        var lines = ExerciseFileReader.ReadLines(file.FilePath);

        Assert.Equal(["Жим лёжа", "Приседание"], lines);
    }

    /// <summary>
    /// Перевод строки в конце файла не даёт лишней пустой строки, как и в
    /// <c>File.ReadAllLines</c>: иначе счётчик прочитанного в окне импорта показывал бы на строку
    /// больше, чем строк в файле.
    /// </summary>
    [Fact]
    public void ReadLines_ПереводСтрокиВКонцеФайла_НеДобавляетПустуюСтроку()
    {
        using var file = new TemporaryFile(StrictUtf8.GetBytes("Жим\r\nТяга\r\n"));

        Assert.Equal(["Жим", "Тяга"], ExerciseFileReader.ReadLines(file.FilePath));
    }

    [Fact]
    public void ReadLines_ПустойФайл_ПустойСписок()
    {
        using var file = new TemporaryFile([]);

        Assert.Empty(ExerciseFileReader.ReadLines(file.FilePath));
    }

    [Fact]
    public void ReadLines_НесуществующийФайл_ОтказСреды()
    {
        Assert.ThrowsAny<IOException>(() =>
            ExerciseFileReader.ReadLines(Path.Combine(Path.GetTempPath(), $"TrainingLogImport-{Guid.NewGuid():N}.txt")));
    }
}
