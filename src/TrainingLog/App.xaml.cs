using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.Core.Settings;
using TrainingLog.Services;
using TrainingLog.ViewModels;
using TrainingLog.Windows;

namespace TrainingLog;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = BuildServices();
        ApplyMigrations();

        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddDbContextFactory<TrainingLogDbContext>(options =>
            options.UseSqlite($"Data Source={SqliteDatabasePath.Resolve()}"));

        services.AddSingleton<IExerciseRepository, ExerciseRepository>();
        services.AddSingleton<ITrainingPlanRepository, TrainingPlanRepository>();
        services.AddSingleton<ITrainingSessionRepository, TrainingSessionRepository>();

        // Настройки — один экземпляр на всё приложение: файл один, и держать две копии
        // состояния значило бы завести второе место, откуда его можно изменить. Экземпляр
        // создаётся здесь, а не через AddSingleton<T>, потому что конструктор без параметров
        // у хранилища один — тот, что ищет файл рядом с программой.
        services.AddSingleton(new AppSettingsStore());

        // Окно упражнений и окно планов единственные, поэтому состояние хоста переиспользуется,
        // а сам сервис должен быть синглтоном. Окна редактирования, наоборот, создаются заново
        // на каждый вызов: внутри них то, что правят.
        services.AddSingleton<IWindowService>(provider => new WindowService(
            () => provider.GetRequiredService<ExercisesWindow>(),
            exercise => new EditExerciseWindow(
                new EditExerciseViewModel(exercise, provider.GetRequiredService<IExerciseRepository>())),
            () => provider.GetRequiredService<PlansWindow>(),
            // Добавление и правка плана открывают одно окно, поэтому фабрики две: отличаются
            // они планом и тем, новый он или уже сохранённый.
            () => new EditPlanWindow(new EditPlanViewModel(
                new TrainingPlan(),
                isNew: true,
                provider.GetRequiredService<ITrainingPlanRepository>(),
                provider.GetRequiredService<IExerciseRepository>())),
            plan => new EditPlanWindow(new EditPlanViewModel(
                plan,
                isNew: false,
                provider.GetRequiredService<ITrainingPlanRepository>(),
                provider.GetRequiredService<IExerciseRepository>())),
            // День тренировки — то же окно и на добавление, и на правку: какая это запись,
            // модель узнаёт по дате, поэтому фабрика одна — она принимает дату, а null
            // означает добавление сегодняшнего дня.
            day => new AddDayWindow(new AddDayViewModel(
                provider.GetRequiredService<ITrainingSessionRepository>(),
                provider.GetRequiredService<ITrainingPlanRepository>(),
                day)),
            // Параметры — то же окно на каждый вызов, как и окна правки: внутри него значение,
            // которое правят, и переиспользование экземпляра с прошлым содержимым показало бы
            // пользователю чужую настройку. Значение лежит в файле рядом с программой.
            () => new SettingsWindow(new SettingsViewModel(
                provider.GetRequiredService<AppSettingsStore>()))));

        services.AddTransient<ExercisesViewModel>();
        services.AddTransient<ExercisesWindow>();

        services.AddTransient<PlansViewModel>();
        services.AddTransient<PlansWindow>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void ApplyMigrations()
    {
        var factory = _services!.GetRequiredService<IDbContextFactory<TrainingLogDbContext>>();

        using var context = factory.CreateDbContext();
        context.Database.Migrate();
    }
}
