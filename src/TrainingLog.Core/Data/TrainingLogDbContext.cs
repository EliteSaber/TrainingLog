using Microsoft.EntityFrameworkCore;
using TrainingLog.Core.Models;

namespace TrainingLog.Core.Data;

public sealed class TrainingLogDbContext(DbContextOptions<TrainingLogDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Имя теневого свойства с нормализованным именем. Регистронезависимая уникальность держится
    /// на нём, а не на <c>Name</c>: сравнение строк в SQLite (<c>=</c>, <c>COLLATE NOCASE</c>,
    /// функция <c>lower()</c>) складывает регистр только для ASCII, поэтому «жим» и «Жим»
    /// для базы различны.
    /// </summary>
    public const string NameKeyPropertyName = "NameKey";

    public const string PlanExercisesTableName = "PlanExercises";

    /// <summary>
    /// <see cref="string.ToUpperInvariant"/>, а не <see cref="string.ToUpper()"/>: вариант без
    /// параметра зависит от локали машины, и одна база сравнялась бы по-разному на разных
    /// компьютерах.
    /// </summary>
    public static string NormalizeNameKey(string name) => name.ToUpperInvariant();

    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();

    public DbSet<PlanExercise> PlanExercises => Set<PlanExercise>();

    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();

    public DbSet<ExerciseEntry> ExerciseEntries => Set<ExerciseEntry>();

    public DbSet<TrainingSet> TrainingSets => Set<TrainingSet>();

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
            // Ignore: атрибут [NotMapped] EF Core не читает, он работает с Data Annotations.
            entity.Ignore(p => p.Exercises);
        });

        modelBuilder.Entity<PlanExercise>(entity =>
        {
            entity.ToTable(PlanExercisesTableName);
            entity.HasKey(link => new { link.TrainingPlanId, link.ExerciseId });
            entity.Property(link => link.Order).IsRequired();

            // Обратной навигации у упражнения нет: список планов, в которые входит упражнение,
            // хранилищу не нужен.

            // Второй каскад, к первому: удаление упражнения убирает его из всех планов.
            entity.HasOne(link => link.Exercise)
                .WithMany()
                .HasForeignKey(link => link.ExerciseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingSession>(entity =>
        {
            entity.ToTable("TrainingSessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.Id).ValueGeneratedOnAdd();
            entity.Property(session => session.Date).IsRequired();
            entity.Property(session => session.PlanName).IsRequired();

            // На дату одна запись. Это проверяет база, а не код приложения: повторное
            // сохранение из другого окна поймает уникальный индекс, а не сравнение в модели.
            entity.HasIndex(session => session.Date).IsUnique();

            // Обратной навигации у плана нет: «список тренировок этого плана» хранилищу не нужен.

            // Ссылка на план обнуляется, а не каскадит: удаление плана не должно стирать
            // историю тренировок. Название остаётся в PlanName, и строка дня показывает его.
            entity.HasOne(session => session.Plan)
                .WithMany()
                .HasForeignKey(session => session.PlanId)
                .OnDelete(DeleteBehavior.SetNull);

            // Exercises здесь — настоящая навигация «один ко многим», в отличие от Exercises
            // у TrainingPlan: у той свойства нет Ignore, и это не упущение — Ignore навигации
            // попутно удаляет и настроенную по ней связь, после чего остаётся сирота: внешний
            // ключ без ограничения и индекса.
            //
            // Связь обязательная, и это не формальность: при необязательной очистка коллекции
            // не удаляет упражнения, а обнуляет внешний ключ, и в базе остаются подходы всех
            // прошлых редакций дня.
            entity.HasMany(session => session.Exercises)
                .WithOne()
                .HasForeignKey("SessionId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExerciseEntry>(entity =>
        {
            entity.ToTable("ExerciseEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).ValueGeneratedOnAdd();
            entity.Property(entry => entry.ExerciseName).IsRequired();
            entity.Property(entry => entry.Order).IsRequired();

            // Та же история, что у плана в записи журнала: упражнение удаляется из
            // справочника, запись в журнале остаётся с копией названия.
            entity.HasOne(entry => entry.Exercise)
                .WithMany()
                .HasForeignKey(entry => entry.ExerciseId)
                .OnDelete(DeleteBehavior.SetNull);

            // Связь настраивается по навигации, а не через HasMany<TrainingSet>() без навигации:
            // по навигации EF находит связь уже по соглашению, а настройка по типу сущности
            // создала бы вторую связь с собственным теневым внешним ключом — колонка
            // ExerciseEntryId1 рядом с собственной ExerciseEntryId. Подходы удаляются каскадом
            // от базы.
            entity.HasMany(entry => entry.Sets)
                .WithOne()
                .HasForeignKey(set => set.ExerciseEntryId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingSet>(entity =>
        {
            entity.ToTable("TrainingSets");
            entity.HasKey(set => set.Id);
            entity.Property(set => set.Id).ValueGeneratedOnAdd();
            entity.Property(set => set.Order).IsRequired();
            entity.Property(set => set.Repetitions).IsRequired();

            // SQLite не умеет decimal: провайдер хранит его как TEXT, и сравнение таких
            // значений лексикографическое — 100 в строке окажется раньше 60. Тип объявлен
            // явно, чтобы провайдер не писал об этом предупреждение в лог.
            entity.Property(set => set.Weight).HasColumnType("TEXT");
        });
    }
}
