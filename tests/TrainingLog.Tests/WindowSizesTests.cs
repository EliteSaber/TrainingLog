using System.Windows;
using TrainingLog.Core.Models;
using TrainingLog.Core.Settings;
using TrainingLog.Services;
using TrainingLog.ViewModels;
using TrainingLog.Windows;

namespace TrainingLog.Tests;

/// <summary>
/// Размеры окон в действии: что окно получает при открытии и какое окно не трогают.
/// </summary>
/// <remarks>
/// Проверяется одно окно, а не все открытые: коллекция открытых окон живёт в
/// <c>Application.Current</c>, а приложения в тестах нет — иначе тест создал бы его на всё
/// время прогона. Правило «какое окно пропустить» вынесено в отдельную функцию именно
/// поэтому.
///
/// Окна настоящие: каталог ищет окно по его типу, и чужое окно размеров не получает вовсе.
///
/// Тесты идут в общей группе WPF: размеры окон лежат в статике, и параллельный прогон
/// двух наборов перепутал бы их значения.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class WindowSizesTests
{
    /// <summary>Размер из файла действует на окно: и ширина, и высота.</summary>
    [Fact]
    public void Apply_РазмерИзФайла_СтавитсяНаОкно()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = NewSettingsWindow(settings);

            try
            {
                WindowSizes.Initialize(new Dictionary<string, WindowSize>
                {
                    [WindowSizeCatalog.Settings] = new WindowSize { Width = 1000, Height = 700 },
                });

                WindowSizes.Apply(window);

                Assert.Equal(1000, window.Width);
                Assert.Equal(700, window.Height);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Записи в файле нет — окно получает нынешний размер из каталога.</summary>
    [Fact]
    public void Apply_БезЗаписи_БерётРазмерКаталога()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = NewSettingsWindow(settings);
            var entry = WindowSizeCatalog.Find(WindowSizeCatalog.Settings)!;

            try
            {
                WindowSizes.Initialize(new Dictionary<string, WindowSize>());

                WindowSizes.Apply(window);

                Assert.Equal(entry.Width, window.Width);
                Assert.Equal((double)entry.Height!.Value, window.Height);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Минимум окна выставляется всегда: WPF всё равно не даст окну стать меньше.</summary>
    [Fact]
    public void Apply_МинимумОкна_СтавитсяВсегда()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = NewSettingsWindow(settings);
            var entry = WindowSizeCatalog.Find(WindowSizeCatalog.Settings)!;

            try
            {
                WindowSizes.Initialize(new Dictionary<string, WindowSize>
                {
                    [WindowSizeCatalog.Settings] = new WindowSize { Width = 100, Height = 100 },
                });

                WindowSizes.Apply(window);

                Assert.Equal(entry.MinWidth, window.MinWidth);
                Assert.Equal(entry.MinHeight, window.MinHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Окно без высоты (высота по содержимому) высоты не получает: её вычислить нельзя.
    /// </summary>
    [Fact]
    public void Apply_ОкноБезВысоты_ВысотуНеСтавит()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var viewModel = new EditExerciseViewModel(new Exercise { Name = "Жим" }, database.Repository);
            var window = new EditExerciseWindow(viewModel, new HotkeyService(settings.Store));

            try
            {
                WindowSizes.Initialize(new Dictionary<string, WindowSize>
                {
                    [WindowSizeCatalog.EditExercise] = new WindowSize { Width = 400, Height = 500 },
                });

                WindowSizes.Apply(window);

                Assert.Equal(400, window.Width);

                // Незаданная высота — «по содержимому», а не ноль.
                Assert.True(double.IsNaN(window.Height));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Окно, которого в каталоге нет, размеров не получает: молча, без ошибки.</summary>
    [Fact]
    public void Apply_ЧужоеОкно_ОстаётсяКакБыло()
    {
        StaRunner.Run(() =>
        {
            var window = new Window();

            try
            {
                WindowSizes.Initialize(new Dictionary<string, WindowSize>
                {
                    [WindowSizeCatalog.Settings] = new WindowSize { Width = 1000, Height = 700 },
                });

                WindowSizes.Apply(window);

                Assert.True(double.IsNaN(window.Width));
                Assert.True(double.IsNaN(window.Height));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Исключённое окно не трогают: окно параметров меняет ширину под курсором.</summary>
    [Fact]
    public void ShouldApply_ИсключённоеОкно_Пропускается()
    {
        StaRunner.Run(() =>
        {
            var window = new Window();
            var another = new Window();

            try
            {
                Assert.False(WindowSizes.ShouldApply(window, window));
                Assert.True(WindowSizes.ShouldApply(window, another));
                Assert.True(WindowSizes.ShouldApply(window, null));
            }
            finally
            {
                window.Close();
                another.Close();
            }
        });
    }

    /// <summary>Окно параметров для проверки: файла настроек хватает и модели, и горячих клавиш.</summary>
    /// <param name="settings">Файл настроек окна.</param>
    private static SettingsWindow NewSettingsWindow(TemporarySettings settings) =>
        new(new SettingsViewModel(settings.Store), new HotkeyService(settings.Store));
}
