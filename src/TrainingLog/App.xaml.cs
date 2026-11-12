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

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Настройки до сборки контейнера: адрес базы живёт в них, и соединение уже строится по нему.
        var settingsStore = new AppSettingsStore();
        var settings = settingsStore.Load();

        _services = BuildServices(settingsStore, settings);
        ApplyMigrations();

        // Токены шрифта до показа окон: иначе первое окно рисовалось бы умолчанием из Fonts.xaml,
        // и выбор в параметрах «опаздывал» на первоотрисовку.
        AppFonts.Apply(settings.FontFamily, settings.FontSizePercent);

        // Тема — тоже до показа окон: палитра перезаписывается на месте, и первое окно должно
        // рисоваться уже в выбранной теме, а не в светлой.
        AppTheme.Apply(settings.Theme);

        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        // Слежение за темой Windows: окно показано, у него есть handle.
        SystemThemeWatcher.Attach(mainWindow);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServices(AppSettingsStore settingsStore, AppSettings settings)
    {
        var services = new ServiceCollection();

        // Path.Combine в AppSettings.DatabasePath мог бы собрать адрес и сам, но каталог может не
        // существовать: Resolve его создаёт, иначе Migrate() упёрся бы в несуществующий каталог.
        services.AddDbContextFactory<TrainingLogDbContext>(options =>
            options.UseSqlite($"Data Source={SqliteDatabasePath.Resolve(settings.DatabaseDirectory, settings.DatabaseName)}"));

        services.AddSingleton<IExerciseRepository, ExerciseRepository>();
        services.AddSingleton<ITrainingPlanRepository, TrainingPlanRepository>();
        services.AddSingleton<ITrainingSessionRepository, TrainingSessionRepository>();

        // Тот же экземпляр, чьи настройки прочтены выше: окно параметров правит один файл, а не
        // второй, найденный по тому же адресу.
        services.AddSingleton(settingsStore);

        // То же правило единственности, что у настроек: назначения правятся одним файлом, и
        // живущие окна пересобирают привязки по событию единственного сервиса.
        services.AddSingleton<HotkeyService>();

        // Каталог шрифтов без состояния: нужен только окну параметров, список строится в модели.
        services.AddSingleton<FontCatalog>();

        // Синглтон, потому что окна справочников единственные и состояние их хоста переиспользуется.
        services.AddSingleton<IWindowService>(provider =>
        {
            // Окна создаются лениво, и сервис горячих клавиш читается из контейнера в момент
            // создания окна: раньше него он всё равно не используется.
            var hotkeys = provider.GetRequiredService<HotkeyService>();

            return new WindowService(
            () => provider.GetRequiredService<ExercisesWindow>(),
            exercise => new EditExerciseWindow(
                new EditExerciseViewModel(exercise, provider.GetRequiredService<IExerciseRepository>()),
                hotkeys),
            () => new ImportExercisesWindow(new ImportExercisesViewModel(
                provider.GetRequiredService<IExerciseRepository>()),
                hotkeys),
            () => provider.GetRequiredService<PlansWindow>(),
            // Добавление и правка плана открывают одно окно, поэтому фабрики две: отличаются они
            // планом и тем, новый он или уже сохранённый.
            () => new EditPlanWindow(new EditPlanViewModel(
                new TrainingPlan(),
                isNew: true,
                provider.GetRequiredService<ITrainingPlanRepository>(),
                provider.GetRequiredService<IExerciseRepository>()),
                hotkeys),
            plan => new EditPlanWindow(new EditPlanViewModel(
                plan,
                isNew: false,
                provider.GetRequiredService<ITrainingPlanRepository>(),
                provider.GetRequiredService<IExerciseRepository>()),
                hotkeys),
            // Окно дня одно на добавление и на правку: какая это запись, модель узнаёт по дате,
            // поэтому фабрика одна, а null означает сегодняшний день.
            day => new AddDayWindow(new AddDayViewModel(
                provider.GetRequiredService<ITrainingSessionRepository>(),
                provider.GetRequiredService<ITrainingPlanRepository>(),
                day),
                hotkeys),
            () => new SettingsWindow(new SettingsViewModel(
                provider.GetRequiredService<AppSettingsStore>(),
                provider.GetRequiredService<FontCatalog>()),
                provider.GetRequiredService<HotkeyService>()));
        });

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
