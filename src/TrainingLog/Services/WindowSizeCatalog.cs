using TrainingLog.Core.Settings;
using TrainingLog.Windows;

namespace TrainingLog.Services;

/// <summary>
/// Одно окно приложения в каталоге размеров: как его звать и каким оно открывается.
/// </summary>
/// <param name="Id">Ключ в файле настроек. Не типизированный: файл читают руками.</param>
/// <param name="Title">Подпись окна в списке параметров.</param>
/// <param name="WindowType">Тип окна: по нему размер достаётся и самому окну, и всем открытым.</param>
/// <param name="Width">Ширина окна по умолчанию.</param>
/// <param name="Height">
/// Высота окна по умолчанию; <c>null</c> — высота по содержимому (у окна правки упражнения).
/// </param>
/// <param name="MinWidth">Минимальная ширина окна: уже неё окно схлопывается.</param>
/// <param name="MinHeight">Минимальная высота окна.</param>
public sealed record WindowSizeEntry(
    string Id,
    string Title,
    Type WindowType,
    int Width,
    int? Height,
    int MinWidth,
    int MinHeight);

/// <summary>
/// Каталог окон приложения: их нынешние размеры и минимальные.
/// </summary>
/// <remarks>
/// Каталог — единственное место, где лежат размеры окон: в разметке их больше нет. Иначе
/// размер жил бы в двух местах (здесь и в XAML) и разошёлся бы при первой правке любого из них.
///
/// Высота <c>null</c> означает «по содержимому»: у окна правки упражнения высота следует за
/// полем с названием, и задать её нельзя.
/// </remarks>
public static class WindowSizeCatalog
{
    /// <summary>Главное окно журнала тренировок.</summary>
    public const string Main = "Main";

    /// <summary>Окно параметров.</summary>
    public const string Settings = "Settings";

    /// <summary>Справочник упражнений.</summary>
    public const string Exercises = "Exercises";

    /// <summary>Список планов.</summary>
    public const string Plans = "Plans";

    /// <summary>Окно дня тренировки.</summary>
    public const string AddDay = "AddDay";

    /// <summary>Окно правки плана.</summary>
    public const string EditPlan = "EditPlan";

    /// <summary>Окно правки упражнения: высота по содержимому.</summary>
    public const string EditExercise = "EditExercise";

    /// <summary>Окно импорта упражнений.</summary>
    public const string ImportExercises = "ImportExercises";

    /// <summary>Все окна приложения в порядке показа в параметрах.</summary>
    public static IReadOnlyList<WindowSizeEntry> Entries { get; } =
    [
        new(Main, "Журнал тренировок", typeof(MainWindow), 900, 560, 640, 360),
        new(Settings, "Параметры", typeof(SettingsWindow), 520, 560, 420, 420),
        new(Exercises, "Упражнения", typeof(ExercisesWindow), 520, 560, 420, 360),
        new(Plans, "Планы", typeof(PlansWindow), 520, 560, 420, 360),
        new(AddDay, "День тренировки", typeof(AddDayWindow), 760, 480, 620, 360),
        new(EditPlan, "План тренировки", typeof(EditPlanWindow), 620, 560, 520, 320),
        new(EditExercise, "Правка упражнения", typeof(EditExerciseWindow), 380, null, 320, 0),
        new(ImportExercises, "Импорт упражнений", typeof(ImportExercisesWindow), 560, 520, 460, 360),
    ];

    /// <summary>Окно по его ключу в файле настроек.</summary>
    /// <param name="id">Ключ окна.</param>
    /// <returns>Запись каталога; <c>null</c> — окна с таким ключом в каталоге нет.</returns>
    public static WindowSizeEntry? Find(string id) =>
        Entries.FirstOrDefault(entry => entry.Id == id);

    /// <summary>Окно по типу: так размер достаёт и самому окну, и всем открытым сразу.</summary>
    /// <param name="windowType">Тип окна.</param>
    /// <returns>Запись каталога; <c>null</c> — окна этого типа в каталоге нет.</returns>
    public static WindowSizeEntry? Find(Type windowType) =>
        Entries.FirstOrDefault(entry => entry.WindowType == windowType);

    /// <summary>
    /// Размер окна: сохранённый в файле, а если его нет или он негоден — нынешний размер
    /// окна из каталога.
    /// </summary>
    /// <remarks>
    /// Значение из файла проверяется и подгоняется под минимум окна: WPF всё равно не даст
    /// окну стать меньше минимума, и записанная величина, которая не действует, только вводит
    /// в заблуждение.
    /// </remarks>
    /// <param name="entry">Окно каталога.</param>
    /// <param name="saved">Размеры из файла настроек.</param>
    /// <returns>Размер окна, пригодный для применения.</returns>
    public static WindowSize Resolve(WindowSizeEntry entry, IReadOnlyDictionary<string, WindowSize> saved)
    {
        var stored = saved.TryGetValue(entry.Id, out var value) ? value : null;

        var width = Clamp(stored?.Width ?? entry.Width, entry.MinWidth);
        var height = entry.Height is null
            ? (int?)null
            : Clamp(stored?.Height ?? entry.Height.Value, entry.MinHeight);

        return new WindowSize { Width = width, Height = height };
    }

    /// <summary>Вписывает размер в границы окна.</summary>
    private static int Clamp(int value, int minimum) => Math.Clamp(value, minimum, WindowSizeRules.MaxSize);
}
