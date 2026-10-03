using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Data;

namespace TrainingLog.Tests;

/// <summary>
/// Временная файловая база данных на одно сообщение теста.
/// </summary>
/// <remarks>
/// Именно файловая, а не провайдер in-memory: нужны настоящие ограничения SQLite —
/// в частности поведение уникального индекса, ради которого и написана колонка
/// <c>NameKey</c>. In-memory провайдер уникальные индексы не проверяет, и тесты
/// регистронезависимого дубликата прошли бы вхолостую.
/// </remarks>
internal sealed class TemporaryDatabase : IDisposable
{
    private readonly string _path;

    public TemporaryDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"TrainingLogTests-{Guid.NewGuid():N}.db");

        var options = new DbContextOptionsBuilder<TrainingLogDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        Factory = new TestContextFactory(options);
        Repository = new Core.Repositories.ExerciseRepository(Factory);
        PlanRepository = new Core.Repositories.TrainingPlanRepository(Factory);

        // Миграции, а не EnsureCreated: так проверяется и итоговая схема,
        // и то, что цепочка миграций применима к пустой базе.
        using var context = Factory.CreateDbContext();
        context.Database.Migrate();
    }

    public IDbContextFactory<TrainingLogDbContext> Factory { get; }

    public Core.Repositories.ExerciseRepository Repository { get; }

    public Core.Repositories.TrainingPlanRepository PlanRepository { get; }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm", _path + "-journal" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Файл уже удалён либо занят — тесту это не мешает.
            }
        }
    }

    private sealed class TestContextFactory(DbContextOptions<TrainingLogDbContext> options)
        : IDbContextFactory<TrainingLogDbContext>
    {
        public TrainingLogDbContext CreateDbContext() => new(options);
    }
}
