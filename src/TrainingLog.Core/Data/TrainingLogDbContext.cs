using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Models;

namespace TrainingLog.Core.Data;

/// <summary>
/// Контекст данных приложения.
/// </summary>
public sealed class TrainingLogDbContext(DbContextOptions<TrainingLogDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Имя теневого свойства, в котором хранится нормализованное имя записи.
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
    /// Имя таблицы связи плана с упражнениями.
    /// </summary>
    public const string PlanExercisesTableName = "PlanExercises";

    /// <summary>
    /// Приводит наименование к сравнимому виду для хранения в <c>NameKey</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="string.ToUpperInvariant"/>, а не <see cref="string.ToUpper()"/>: вариант без
    /// параметра зависит от локали машины, и одна база сравнивалась бы по-разному на разных
    /// компьютерах. Сравнение строк в SQLite складывает регистр только для ASCII, поэтому
    /// нормализация выполняется в .NET. Общая для упражнений и планов: правило одно.
    /// </remarks>
    public static string NormalizeNameKey(string name) => name.ToUpperInvariant();

    /// <summary>
    /// Справочник упражнений.
    /// </summary>
    public DbSet<Exercise> Exercises => Set<Exercise>();

    /// <summary>
    /// Планы тренировок.
    /// </summary>
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();

    /// <summary>
    /// Строки связи плана с упражнением: состав плана и порядок выполнения.
    /// </summary>
    public DbSet<PlanExercise> PlanExercises => Set<PlanExercise>();

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

        modelBuilder.Entity<TrainingPlan>(entity =>
        {
            entity.ToTable("TrainingPlans");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedOnAdd();
            entity.Property(p => p.Name).IsRequired();
            entity.Property<string>(NameKeyPropertyName).IsRequired();
            entity.HasIndex(NameKeyPropertyName).IsUnique();

            entity.HasMany(p => p.PlanExercises)
                .WithOne(link => link.Plan)
                .HasForeignKey(link => link.TrainingPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Exercises — вычисляемое свойство, а не навигация, но по соглашению EF оно всё
            // равно попало бы в модель навигацией и сломало Include. Исключается именно через
            // Ignore: атрибут [NotMapped] EF Core не читает, он работает с Data Annotations,
            // а не с отображением.
            entity.Ignore(p => p.Exercises);
        });

        modelBuilder.Entity<PlanExercise>(entity =>
        {
            entity.ToTable(PlanExercisesTableName);
            entity.HasKey(link => new { link.TrainingPlanId, link.ExerciseId });
            entity.Property(link => link.Order).IsRequired();

            // Обратной навигации у упражнения нет: список планов, в которые входит упражнение,
            // хранилищу не нужен, а лишняя коллекция в модели только мешала бы.

            // Второй каскад, к первому: удаление упражнения убирает его из всех планов.
            // Без него в раскрытом плане осталась бы ссылка на несуществующее упражнение.
            entity.HasOne(link => link.Exercise)
                .WithMany()
                .HasForeignKey(link => link.ExerciseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
