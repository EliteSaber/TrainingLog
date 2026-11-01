using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TrainingLog.Core.Repositories;

/// <summary>
/// Разбор отказов SQLite по конкретному ограничению, а не «любой отказ — дубль».
/// </summary>
/// <remarks>
/// Ловить <see cref="DbUpdateException"/> целиком нельзя: под него попадают и сбой диска, и
/// чужое ограничение, и тогда пользователь видит «название занято» там, где название ни при чём.
/// SQLite в сообщении об уникальности называет таблицу и колонку («UNIQUE constraint failed:
/// Exercises.NameKey»), поэтому сверка идёт по ним, а не по имени индекса: индекса в тексте нет.
/// </remarks>
internal static class SqliteConstraintErrors
{
    /// <summary>Код SQLITE_CONSTRAINT: уникальный индекс, внешний ключ, NOT NULL.</summary>
    private const int ConstraintErrorCode = 19;

    /// <summary>Совпал ли отказ с уникальностью указанной колонки указанной таблицы.</summary>
    public static bool IsUniqueViolation(DbUpdateException exception, string table, string column) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: ConstraintErrorCode,
            Message: var message,
        }
        && message.Contains($"UNIQUE constraint failed: {table}.{column}", StringComparison.Ordinal);
}
