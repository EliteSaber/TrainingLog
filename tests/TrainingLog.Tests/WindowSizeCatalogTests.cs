using TrainingLog.Core.Settings;
using TrainingLog.Services;
using TrainingLog.Windows;

namespace TrainingLog.Tests;

/// <summary>
/// Каталог окон: размеры по умолчанию, минимумы и разбор записи из файла настроек.
/// </summary>
/// <remarks>
/// Каталог — единственное место, где лежат размеры окон: в разметке их нет. Поэтому тест
/// проверяет и сами числа, и то, что каждое окно приложения в каталоге есть: забытое окно
/// открылось бы без размера.
/// </remarks>
public sealed class WindowSizeCatalogTests
{
    /// <summary>Все окна приложения есть в каталоге: забытое окно открылось бы без размера.</summary>
    [Fact]
    public void Find_ПоТипу_НаходитКаждоеОкно()
    {
        Type[] windows =
        [
            typeof(TrainingLog.MainWindow),
            typeof(SettingsWindow),
            typeof(ExercisesWindow),
            typeof(PlansWindow),
            typeof(AddDayWindow),
            typeof(EditPlanWindow),
            typeof(EditExerciseWindow),
            typeof(ImportExercisesWindow),
        ];

        Assert.All(windows, window => Assert.NotNull(WindowSizeCatalog.Find(window)));
    }

    /// <summary>У каждого окна есть ключ и название: ключ идёт в файл, название — в список.</summary>
    [Fact]
    public void Entries_УКаждогоОкнаКлючИНазвание()
    {
        Assert.All(
            WindowSizeCatalog.Entries,
            entry =>
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Id));
                Assert.False(string.IsNullOrWhiteSpace(entry.Title));
                Assert.True(entry.Width > 0);
                Assert.True(entry.MinWidth > 0);
            });

        // Ключи не повторяются: иначе один размер записался бы поверх другого.
        Assert.Equal(
            WindowSizeCatalog.Entries.Count,
            WindowSizeCatalog.Entries.Select(entry => entry.Id).Distinct().Count());
    }

    /// <summary>Записи в файле нет — размер окна нынешний, из каталога.</summary>
    [Fact]
    public void Resolve_БезЗаписи_РазмерИзКаталога()
    {
        var entry = WindowSizeCatalog.Find(WindowSizeCatalog.Main)!;

        var size = WindowSizeCatalog.Resolve(entry, new Dictionary<string, WindowSize>());

        Assert.Equal(entry.Width, size.Width);
        Assert.Equal(entry.Height, size.Height);
    }

    /// <summary>Запись в файле главнее каталога: пользователь задал размер — он и действует.</summary>
    [Fact]
    public void Resolve_СЗаписью_РазмерИзФайла()
    {
        var entry = WindowSizeCatalog.Find(WindowSizeCatalog.Main)!;

        var saved = new Dictionary<string, WindowSize>
        {
            [WindowSizeCatalog.Main] = new WindowSize { Width = 1000, Height = 700 },
        };

        var size = WindowSizeCatalog.Resolve(entry, saved);

        Assert.Equal(1000, size.Width);
        Assert.Equal(700, size.Height);
    }

    /// <summary>
    /// Запись меньше минимума окна подгоняется под минимум: WPF всё равно не даст окну стать
    /// меньше, и записанная величина, которая не действует, только вводит в заблуждение.
    /// </summary>
    [Fact]
    public void Resolve_МеньшеМинимума_ПодгоняетсяПодМинимум()
    {
        var entry = WindowSizeCatalog.Find(WindowSizeCatalog.Main)!;

        var saved = new Dictionary<string, WindowSize>
        {
            [WindowSizeCatalog.Main] = new WindowSize { Width = 100, Height = 100 },
        };

        var size = WindowSizeCatalog.Resolve(entry, saved);

        Assert.Equal(entry.MinWidth, size.Width);
        Assert.Equal(entry.MinHeight, size.Height);
    }

    /// <summary>
    /// Высота по содержимому остаётся такой и из файла: у окна правки упражнения высоту задать
    /// нельзя, и запись в файле её не отменяет.
    /// </summary>
    [Fact]
    public void Resolve_ВысотаПоСодержимому_ОстаётсяNull()
    {
        var entry = WindowSizeCatalog.Find(WindowSizeCatalog.EditExercise)!;

        var saved = new Dictionary<string, WindowSize>
        {
            [WindowSizeCatalog.EditExercise] = new WindowSize { Width = 400, Height = 500 },
        };

        var size = WindowSizeCatalog.Resolve(entry, saved);

        Assert.Equal(400, size.Width);
        Assert.Null(size.Height);
    }
}
