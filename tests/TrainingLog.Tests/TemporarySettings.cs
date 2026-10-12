using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>
/// Временный каталог с файлом настроек на одно сообщение теста.
/// </summary>
/// <remarks>
/// Именно файл, а не объект настроек в памяти: проверяется, что значение доезжает до диска и
/// обратно, а настоящий файл лежит рядом с собранной программой и писать в него из теста
/// нельзя — портились бы настройки запущенного экземпляра.
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

    /// <summary>Путь к файлу настроек. До первого сохранения файла нет.</summary>
    public string FilePath { get; }

    /// <summary>Хранилище этого файла.</summary>
    public AppSettingsStore Store { get; }

    /// <summary>
    /// Кладёт в файл произвольное содержимое: так проверяется поведение на негодном файле.
    /// </summary>
    /// <param name="content">Содержимое файла.</param>
    public void Write(string content) => File.WriteAllText(FilePath, content);

    /// <summary>
    /// Путь внутри каталога, куда записать нельзя, потому что там лежит подкаталог.
    /// </summary>
    /// <remarks>
    /// Нужен для проверки отказа записи: запись файла поверх существующего каталога падает
    /// одинаково на любой машине и в любом процессе, тогда как запрет записи в каталог
    /// средствами Windows зависит от прав и от того, повышены ли они.
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
