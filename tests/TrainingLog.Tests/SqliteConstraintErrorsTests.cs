using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Repositories;

namespace TrainingLog.Tests;

/// <summary>
/// Отказ сверяется по таблице и колонке, а не «любой отказ — дубль». Регрессия ложного диагноза:
/// отказ по уникальности наименования плана выдавался за занятую дату журнала, потому что ловился
/// любой <see cref="DbUpdateException"/>.
/// </summary>
/// <remarks>
/// Исключения собираются вручную: через публичный API репозиториев отказ по чужому ограничению
/// не воспроизвести — предварительные проверки и разрешение ссылок гасят его до записи.
/// </remarks>
public sealed class SqliteConstraintErrorsTests
{
    [Fact]
    public void IsUniqueViolation_СвояТаблицаИКолонка_Истина()
    {
        var exception = UniqueViolation("TrainingSessions.Date");

        Assert.True(SqliteConstraintErrors.IsUniqueViolation(exception, "TrainingSessions", "Date"));
    }

    /// <summary>Тот самый ложный диагноз: уникальность наименования плана — не занятая дата.</summary>
    [Fact]
    public void IsUniqueViolation_ЧужаяТаблица_Ложь()
    {
        var exception = UniqueViolation("TrainingPlans.NameKey");

        Assert.False(SqliteConstraintErrors.IsUniqueViolation(exception, "TrainingSessions", "Date"));
    }

    [Fact]
    public void IsUniqueViolation_ЧужаяКолонка_Ложь()
    {
        var exception = UniqueViolation("Exercises.NameKey");

        Assert.False(SqliteConstraintErrors.IsUniqueViolation(exception, "Exercises", "Name"));
    }

    /// <summary>Код ограничения SQLite общий на уникальный индекс, внешний ключ и NOT NULL.</summary>
    [Fact]
    public void IsUniqueViolation_НеУникальность_Ложь()
    {
        var exception = new DbUpdateException(
            "отказ сохранения",
            new SqliteException("FOREIGN KEY constraint failed", 19));

        Assert.False(SqliteConstraintErrors.IsUniqueViolation(exception, "TrainingSessions", "Date"));
    }

    [Fact]
    public void IsUniqueViolation_ЧужойКодОшибки_Ложь()
    {
        var exception = new DbUpdateException(
            "отказ сохранения",
            new SqliteException("UNIQUE constraint failed: TrainingSessions.Date", 5));

        Assert.False(SqliteConstraintErrors.IsUniqueViolation(exception, "TrainingSessions", "Date"));
    }

    [Fact]
    public void IsUniqueViolation_БезSqliteИсключения_Ложь()
    {
        var exception = new DbUpdateException("отказ сохранения", new InvalidOperationException("диск недоступен"));

        Assert.False(SqliteConstraintErrors.IsUniqueViolation(exception, "TrainingSessions", "Date"));
    }

    private static DbUpdateException UniqueViolation(string constraint) =>
        new("отказ сохранения", new SqliteException($"UNIQUE constraint failed: {constraint}", 19));
}
