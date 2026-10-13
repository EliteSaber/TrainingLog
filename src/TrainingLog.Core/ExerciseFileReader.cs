using System.Text;

namespace TrainingLog.Core;

/// <summary>
/// Чтение файла со списком упражнений. Файл в разметке один упражнение на строку.
/// </summary>
public static class ExerciseFileReader
{
    /// <summary>Строгий разбор UTF-8: негодная последовательность байтов обязана падать, а не заменяться.</summary>
    /// <remarks>
    /// Замена вместо отказа сделала бы разбор UTF-8 безошибочным: русский текст в windows-1251
    /// распознался бы как UTF-8 с ромбиками, и определить кодировку было бы нечем.
    /// </remarks>
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Кодировки вне ASCII доступны только после регистрации провайдера: в .NET их нет по умолчанию.
    /// Повторная регистрация безопасна, поэтому она здесь, а не в точке входа приложения.
    /// </summary>
    static ExerciseFileReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static IReadOnlyList<string> ReadLines(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var lines = ExerciseImportRules.SplitLines(Decode(File.ReadAllBytes(path))).ToList();

        // Перевод строки в конце файла не даёт лишней пустой строки — так же, как это делает
        // File.ReadAllLines: иначе счётчик прочитанного показывал бы на строку больше, чем
        // строк в файле, и выглядел бы как потерянная.
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>
    /// UTF-8 с BOM и без, а если байты не разбираются как UTF-8 — windows-1251.
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (HasPrefix(bytes, [0xEF, 0xBB, 0xBF]))
        {
            // BOM пропускается, а не декодируется: иначе в начало первого упражнения попал бы
            // невидимый U+FEFF и оно не совпало бы с тем же упражнением из другого файла.
            return StrictUtf8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (HasPrefix(bytes, [0xFF, 0xFE]) || HasPrefix(bytes, [0xFE, 0xFF]))
        {
            throw new NotSupportedException(
                "Файл записан в UTF-16. Сохраните его в UTF-8 или windows-1251.");
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Строгий разбор UTF-8 отверг файл: значит, это однобайтовая кодировка, а русский текст
            // в них — windows-1251. Обратного перебора нет: 1251 сопоставляет все 256 байтов и не
            // падает ни на чём, поэтому ошибиться можно было бы только в другую сторону.
            return Encoding.GetEncoding(1251).GetString(bytes);
        }
    }

    private static bool HasPrefix(byte[] bytes, ReadOnlySpan<byte> prefix) =>
        bytes.AsSpan().StartsWith(prefix);
}
