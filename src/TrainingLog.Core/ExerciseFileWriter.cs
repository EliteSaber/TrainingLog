using System.Text;

namespace TrainingLog.Core;

/// <summary>Выгрузка справочника упражнений в текстовый файл, по одному упражнению на строку.</summary>
public static class ExerciseFileWriter
{
    /// <summary>Имя файла по умолчанию в диалоге сохранения.</summary>
    public const string DefaultFileName = "упражнения.txt";

    /// <summary>
    /// UTF-8 именно с BOM: блокнот и часть старых программ на Windows без метки читают кириллицу
    /// как мусор, а <see cref="ExerciseFileReader"/> BOM пропускает и читает такой файл наравне с
    /// прочими.
    /// </summary>
    private static readonly Encoding Utf8WithBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// Пишет упражнения в файл и отвечает, записался ли он.
    /// </summary>
    /// <remarks>
    /// Отказ возвращается значением, а не исключением — тем же приёмом, что и у файла настроек:
    /// программа может стоять в папке без права записи, и падать на выбранном пользователем пути
    /// нельзя.
    /// </remarks>
    public static bool Write(string path, IEnumerable<string> names)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(names);

        var lines = names.ToList();

        // \r\n и перевод строки в конце: так файл выглядит в блокноте и в других редакторах, а
        // импорт отбрасывает последнюю пустую строку и читает файл обратно без потерь.
        var text = lines.Count == 0 ? string.Empty : $"{string.Join("\r\n", lines)}\r\n";

        try
        {
            File.WriteAllText(path, text, Utf8WithBom);
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return false;
        }

        return true;
    }

    // Список явный, а не «всё подряд»: под «всё» попал бы OutOfMemoryException.
    private static bool IsFileProblem(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Security.SecurityException;
}
