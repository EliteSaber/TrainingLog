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

    /// <summary>
    /// Журнал тренировок: записи по дням.
    /// </summary>
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();

    /// <summary>
    /// Выполненные упражнения внутри записей журнала.
    /// </summary>
    public DbSet<ExerciseEntry> ExerciseEntries => Set<ExerciseEntry>();

    /// <summary>
    /// Подходы внутри выполненных упражнений.
    /// </summary>
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

            // Обратной навигации у плана нет: «список тренировок этого плана» хранилищу не
            // нужен, а лишняя коллекция в модели мешала бы только раздувать план.

            // Ссылка на план обнуляется, а не каскадит: удаление плана не должно стирать
            // историю тренировок. Название остаётся в PlanName, и строка дня показывает его.
            entity.HasOne(session => session.Plan)
                .WithMany()
                .HasForeignKey(session => session.PlanId)
                .OnDelete(DeleteBehavior.SetNull);

            // Exercises здесь — настоящая навигация «один ко многим», в отличие от
            // Exercises у TrainingPlan: та вычисляется по строкам связи, и её пришлось
            // исключить. У этой свойства нет Ignore, и это не упущение: Ignore навигации
            // попутно удаляет и настроенную по ней связь, после чего остаётся сирота — внешний
            // ключ без ограничения и индекса.
            //
            // Связь обязательная, и это не формальность: при необязательной очистка коллекции
            // не удаляет упражнения, а обнуляет внешний ключ, и в базе остаются подходы всех
            // прошлых редакций дня. Правка дня переписывает запись целиком, и ушедшее должно
            // уходить, поэтому внешний ключ не nullable, а удаление каскадное.
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
            // по навигации EF находит связь уже по соглашению (Sets — навигация с backing
            // field, конвенция её видит), и настройка по типу сущности создала бы вторую связь
            // с собственным теневым внешним ключом — колонка ExerciseEntryId1 рядом с
            // собственной ExerciseEntryId. Подходы удаляются каскадом от базы.
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
            // значений лексикографическое — 100 в строке окажется раньше 60. Сортировать
            // записи журнала по весу не придётся, а тип объявлен явно, чтобы провайдер не
            // писал об этом предупреждение в лог при каждом создании модели.
            entity.Property(set => set.Weight).HasColumnType("TEXT");
        });
    }
}
