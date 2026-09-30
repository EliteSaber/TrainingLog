using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrainingLog.Core.Data;

/// <summary>
/// Фабрика контекста для времени разработки. Нужна инструменту <c>dotnet ef</c>: он умеет
/// находить контекст только так — через интерфейс или через хост приложения. Поднимать
/// WPF-приложение инструменту нельзя, поэтому провайдер и путь к базе задаются явно.
/// </summary>
/// <remarks>
/// Путь берётся из <see cref="SqliteDatabasePath"/> — тот же, что и у приложения, иначе
/// миграции применялись бы к одному файлу, а приложение работало с другим.
/// </remarks>
public sealed class TrainingLogDbContextFactory : IDesignTimeDbContextFactory<TrainingLogDbContext>
{
    public TrainingLogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrainingLogDbContext>()
            .UseSqlite($"Data Source={SqliteDatabasePath.Resolve()}")
            .Options;

        return new TrainingLogDbContext(options);
    }
}
