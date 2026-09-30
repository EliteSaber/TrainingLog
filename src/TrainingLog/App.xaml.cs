using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingLog.Core.Data;
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

        // Окно упражнений единственное, поэтому состояние хоста переиспользуется, а сам сервис
        // должен быть синглтоном. Окно редактирования, наоборот, создаётся заново на каждый
        // вызов, внутри него то упражнение, которое правят.
        services.AddSingleton<IWindowService>(provider => new WindowService(
            () => provider.GetRequiredService<ExercisesWindow>(),
            exercise => new EditExerciseWindow(
                new EditExerciseViewModel(exercise, provider.GetRequiredService<IExerciseRepository>()))));

        services.AddTransient<ExercisesViewModel>();
        services.AddTransient<ExercisesWindow>();

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
