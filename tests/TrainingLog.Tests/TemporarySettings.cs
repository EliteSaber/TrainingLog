using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>Временный каталог с файлом настроек на сообщение теста.</summary>
/// <remarks>
/// Именно файл, а не объект настроек в памяти: проверяется, что значение доезжает до диска и
/// обратно. Настоящий файл лежит рядом с программой, и писать в него из теста нельзя.
/// </remarks>
internal sealed class TemporarySettings : IDisposable
{
    public TemporarySettings()
    {
        FolderPath = Path.Combine(Path.GetTempPath(), $"TrainingLogSettings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FolderPath);

        FilePath = Path.Combine(FolderPath, AppSettingsStore.FileName);

        Store = new AppSettingsStore(FilePath);
    }

    /// <summary>Каталог, в котором лежит файл.</summary>
    public string FolderPath { get; }

    /// <summary>Путь к файлу. До первого сохранения файла нет.</summary>
    public string FilePath { get; }

    /// <summary>Хранилище этого файла.</summary>
    public AppSettingsStore Store { get; }

    /// <summary>Кладёт в файл произвольное содержимое — так проверяется поведение на негодном файле.</summary>
    /// <param name="content">Содержимое файла.</param>
    public void Write(string content) => File.WriteAllText(FilePath, content);

    /// <summary>Путь, куда записать нельзя, потому что там лежит каталог.</summary>
    /// <remarks>
    /// Запись файла поверх существующего каталога падает одинаково на любой машине и в любом
    /// процессе, тогда как запрет записи в каталог средствами Windows зависит от прав.
    /// </remarks>
    public string AsDirectory()
    {
        var path = Path.Combine(FolderPath, "каталог-вместо-файла");
        Directory.CreateDirectory(path);

        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(FolderPath, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Каталог уже удалён либо занят — тесту это не мешает.
        }
    }
}
