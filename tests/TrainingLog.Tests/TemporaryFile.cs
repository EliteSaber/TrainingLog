namespace TrainingLog.Tests;

/// <summary>Временный файл со списком упражнений на сообщение теста.</summary>
/// <remarks>
/// Именно файл, а не строка в памяти: проверяется чтение с диска вместе с определением кодировки.
/// Содержимое пишется байтами, потому что кодировку задаёт именно набор байтов, а любой
/// <c>File.WriteAllText</c> запишет UTF-8 и проверить windows-1251 не даст.
/// </remarks>
internal sealed class TemporaryFile : IDisposable
{
    public TemporaryFile(byte[] content)
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"TrainingLogImport-{Guid.NewGuid():N}.txt");

        File.WriteAllBytes(FilePath, content);
    }

    /// <summary>Путь к файлу.</summary>
    public string FilePath { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (IOException)
        {
            // Файл уже удалён либо занят — тесту это не мешает.
        }
    }
}
