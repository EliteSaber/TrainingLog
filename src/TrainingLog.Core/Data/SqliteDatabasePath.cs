namespace TrainingLog.Core.Data;

/// <summary>
/// Расположение файла базы данных приложения.
/// </summary>
public static class SqliteDatabasePath
{
    /// <summary>
    /// Имя файла базы данных.
    /// </summary>
    public const string FileName = "exercises.db";

    /// <summary>
    /// Полный путь к файлу базы данных: <c>%LOCALAPPDATA%\TrainingLog\exercises.db</c>.
    /// </summary>
    /// <remarks>
    /// Используется <see cref="Environment.SpecialFolder.LocalApplicationData"/>, а не
    /// <c>ApplicationData</c>: база машинная, её не нужно синхронизировать между машинами
    /// облачным бэкапом профиля. Каталог создаётся, если его ещё нет.
    /// </remarks>
    public static string Resolve()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrainingLog");

        Directory.CreateDirectory(directory);

        return Path.Combine(directory, FileName);
    }
}
