using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Models;

namespace TrainingLog.Core.Data;

/// <summary>
/// Контекст данных приложения.
/// </summary>
public sealed class TrainingLogDbContext(DbContextOptions<TrainingLogDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Имя теневого свойства, в котором хранится нормализованное имя упражнения.
    /// </summary>
    /// <remarks>
    /// Регистронезависимая уникальность держится на этом поле, а не на <c>Name</c>.
    /// Сравнение строк в SQLite (<c>=</c>, <c>COLLATE NOCASE</c>, функция <c>lower()</c>)
    /// складывает регистр только для ASCII, поэтому «жим» и «Жим» для базы различны.
    /// Нормализация выполняется в .NET через <see cref="string.ToUpperInvariant"/>,
    /// которая не зависит от локали машины.
    /// </remarks>
    public const string NameKeyPropertyName = "NameKey";

    /// <summary>
    /// Справочник упражнений.
    /// </summary>
    public DbSet<Exercise> Exercises => Set<Exercise>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.ToTable("Exercises");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name).IsRequired();
            entity.Property<string>(NameKeyPropertyName).IsRequired();
            entity.HasIndex(NameKeyPropertyName).IsUnique();
        });
    }
}
