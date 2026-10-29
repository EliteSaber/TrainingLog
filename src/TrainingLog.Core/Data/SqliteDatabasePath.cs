namespace TrainingLog.Core.Data;

/// <summary>
/// Расположение файла базы данных приложения.
/// </summary>
/// <remarks>
/// По умолчанию база лежит в <c>%LOCALAPPDATA%</c>, но приложение читает её адрес из файла
/// настроек: первый параметр перегрузки <see cref="Resolve"/> — сохранённая директория.
/// Умолчания здесь и в <see cref="Settings.AppSettings"/> обязаны оставаться одними и теми же
/// значениями, иначе «сброс» и «файл без ключей» указали бы на разные базы.
/// </remarks>
public static class SqliteDatabasePath
{
    public const string FileName = "exercises.db";

    /// <summary>
    /// <c>LocalApplicationData</c>, а не <c>ApplicationData</c>: база машинная, её не нужно
    /// синхронизировать между машинами облачным бэкапом профиля.
    /// </summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TrainingLog");

    public static string Resolve() => Resolve(DefaultDirectory, FileName);

    /// <summary>
    /// Путь к базе из выбранной директории и наименования. Каталог создаётся, если его нет:
    /// база по новому пути вырастает при первом же <c>Migrate()</c>, и заниматься этим должен
    /// один вызов.
    /// </summary>
    public static string Resolve(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, fileName);
    }
}
