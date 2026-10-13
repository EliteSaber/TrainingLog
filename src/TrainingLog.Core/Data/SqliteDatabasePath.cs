namespace TrainingLog.Core.Data;

/// <summary>
/// Расположение файла базы данных приложения.
/// </summary>
public static class SqliteDatabasePath
{
    public const string FileName = "exercises.db";

    /// <summary>
    /// <c>LocalApplicationData</c>, а не <c>ApplicationData</c>: база машинная, её не нужно
    /// синхронизировать между машинами облачным бэкапом профиля.
    /// </summary>
    public static string Resolve()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrainingLog");

        Directory.CreateDirectory(directory);

        return Path.Combine(directory, FileName);
    }
}
