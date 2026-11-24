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
    /// <remarks>
    /// Сравнение точное, по целой паре «таблица.колонка»: подстрочный поиск считал бы отказ по
    /// <c>Exercises.NameKey</c> совпавшим с <c>Exercises.Name</c> — ровно тот ложный диагноз,
    /// ради которого класс и заведён.
    /// </remarks>
    public static bool IsUniqueViolation(DbUpdateException exception, string table, string column)
    {
        if (exception.InnerException is not SqliteException
            {
                SqliteErrorCode: ConstraintErrorCode,
                Message: var message,
            })
        {
            return false;
        }

        // Формат SQLite: «UNIQUE constraint failed: Table.Column, Table.Other».
        const string prefix = "UNIQUE constraint failed: ";

        var start = message.IndexOf(prefix, StringComparison.Ordinal);

        if (start < 0)
        {
            return false;
        }

        var failed = message[(start + prefix.Length)..];

        // Сообщение Microsoft.Data.Sqlite оборачивает текст: «SQLite Error 19: '…'». Хвост
        // после закрывающей кавычки к списку колонок не относится.
        var end = failed.IndexOf('\'');

        if (end >= 0)
        {
            failed = failed[..end];
        }

        return failed
            .Split(',', StringSplitOptions.TrimEntries)
            .Contains($"{table}.{column}", StringComparer.Ordinal);
    }
}
