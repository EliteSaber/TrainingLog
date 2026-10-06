using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingLog.Core.Data;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
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
            // модель узнаёт по дате, поэтому фабрика одна.
            () => new AddDayWindow(new AddDayViewModel(
                provider.GetRequiredService<ITrainingSessionRepository>(),
                provider.GetRequiredService<ITrainingPlanRepository>()))));

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
