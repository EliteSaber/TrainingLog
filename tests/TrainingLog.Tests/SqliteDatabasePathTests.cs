using TrainingLog.Core.Data;

namespace TrainingLog.Tests;

/// <summary>Адрес базы: умолчание и сборка пути из сохранённых настроек.</summary>
public sealed class SqliteDatabasePathTests
{
    /// <summary>Умолчание — тот же файл в <c>%LOCALAPPDATA%\TrainingLog</c>, что и всегда.</summary>
    [Fact]
    public void Resolve_БезПараметров_СобираетУмолчание()
    {
        var path = SqliteDatabasePath.Resolve();

        Assert.Equal(SqliteDatabasePath.FileName, Path.GetFileName(path));
        Assert.Equal(SqliteDatabasePath.DefaultDirectory, Path.GetDirectoryName(path));
        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TrainingLog"),
            SqliteDatabasePath.DefaultDirectory);
    }

    /// <summary>
    /// Сборка из директории и наименования отвечает полным адресом, и каталог создаёт: база по
    /// новому пути вырастает при первом же <c>Migrate()</c>, каталог для неё готовит сам Resolve.
    /// </summary>
    [Fact]
    public void Resolve_ИзДиректорииИНаименования_СоздаётКаталогИВозвращаетПуть()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"TrainingLogDb-{Guid.NewGuid():N}", "базы");

        try
        {
            Assert.False(Directory.Exists(directory));

            var path = SqliteDatabasePath.Resolve(directory, "спорт.db");

            Assert.Equal(Path.Combine(directory, "спорт.db"), path);
            Assert.True(Directory.Exists(directory));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }
}
