namespace TrainingLog.Services;

/// <summary>Тип цели строки каталога.</summary>
public enum HotkeyKind
{
    /// <summary>Кнопка самого окна.</summary>
    Window,

    /// <summary>Кнопка в строке списка: цель — выделенная строка.</summary>
    Row,

    /// <summary>Кнопка-номер упражнения в окне дня.</summary>
    Slot,
}

/// <summary>Одно назначаемое действие: id, имя по-русски, умолчание.</summary>
/// <remarks>
/// Умолчание есть не у всех: «Сохранить» окна дня с Ctrl+S, стрелки выделения журнала с Up/Down,
/// стрелки и пробел окна плана — у остальных его нет.
/// </remarks>
public sealed record HotkeyAction(string Id, string Name, string? DefaultGesture, HotkeyKind Kind)
{
    /// <summary>Пометка строковых и слотовых назначений: видна рядом с именем действия.</summary>
    public string Marker => Kind switch
    {
        HotkeyKind.Row => "строка",
        HotkeyKind.Slot => "упражнение",
        _ => string.Empty,
    };
}

/// <summary>Группа каталога: назначения одного окна в порядке вкладки.</summary>
public sealed record HotkeyGroup(string WindowName, IReadOnlyList<HotkeyAction> Actions);

/// <summary>Каталог назначений: все кнопки всех окон, порядок групп = порядок вкладки.</summary>
/// <remarks>
/// Контракты, проверяемые тестом: id уникальны глобально, в группе имена не повторяются,
/// умолчания — «Сохранить» окна дня, стрелки выделения журнала и клавиши окна плана.
/// Слоты окна дня перечислены явно, диапазон 1–12.
/// </remarks>
public static class HotkeyCatalog
{
    public const int SlotCount = 12;

    /// <summary>Префикс id слотов: «Day.Slot1» … «Day.Slot<see cref="SlotCount"/>».</summary>
    public const string SlotIdPrefix = "Day.Slot";

    public static IReadOnlyList<HotkeyGroup> Groups { get; } =
    [
        new HotkeyGroup("Главное окно «Журнал тренировок»",
        [
            new HotkeyAction("Main.SettingsMenu", "Меню параметров", null, HotkeyKind.Window),
            new HotkeyAction("Main.OpenExercises", "Упражнения", null, HotkeyKind.Window),
            new HotkeyAction("Main.OpenPlans", "Планы", null, HotkeyKind.Window),
            new HotkeyAction("Main.OpenSettings", "Параметры…", null, HotkeyKind.Window),
            new HotkeyAction("Main.Apply", "Применить", null, HotkeyKind.Window),
            new HotkeyAction("Main.AddDay", "Добавить день", null, HotkeyKind.Window),
            new HotkeyAction("Main.SelectedUp", "Строка выше (↑)", "Up", HotkeyKind.Window),
            new HotkeyAction("Main.SelectedDown", "Строка ниже (↓)", "Down", HotkeyKind.Window),
            new HotkeyAction("Main.EditDay", "Редактировать день", null, HotkeyKind.Row),
            new HotkeyAction("Main.ShowNote", "Показать примечание", null, HotkeyKind.Row),
        ]),

        new HotkeyGroup("Окно дня",
        [
            new HotkeyAction("Day.Previous", "Предыдущее упражнение (‹)", null, HotkeyKind.Window),
            new HotkeyAction("Day.Next", "Следующее упражнение (›)", null, HotkeyKind.Window),
            new HotkeyAction("Day.RemoveSet", "Убрать подход (−)", null, HotkeyKind.Window),
            new HotkeyAction("Day.AddSet", "Добавить подход (+)", null, HotkeyKind.Window),
            new HotkeyAction("Day.Save", "Сохранить", "Ctrl+S", HotkeyKind.Window),
            new HotkeyAction("Day.SaveAndClose", "Сохранить и закрыть", null, HotkeyKind.Window),
            new HotkeyAction("Day.Cancel", "Отменить", null, HotkeyKind.Window),
            ..Enumerable.Range(1, SlotCount).Select(number => new HotkeyAction(
                $"{SlotIdPrefix}{number}",
                $"Упражнение {number}",
                null,
                HotkeyKind.Slot)),
        ]),

        new HotkeyGroup("Упражнения",
        [
            new HotkeyAction("Exercises.Import", "Импорт", null, HotkeyKind.Window),
            new HotkeyAction("Exercises.Export", "Экспорт", null, HotkeyKind.Window),
            new HotkeyAction("Exercises.Add", "Добавить", null, HotkeyKind.Window),
            new HotkeyAction("Exercises.ToggleSort", "Сортировка «Название»", null, HotkeyKind.Window),
            new HotkeyAction("Exercises.Edit", "Редактировать", null, HotkeyKind.Row),
            new HotkeyAction("Exercises.Delete", "Удалить", null, HotkeyKind.Row),
        ]),

        new HotkeyGroup("Правка упражнения",
        [
            new HotkeyAction("EditExercise.Accept", "Принять", null, HotkeyKind.Window),
            new HotkeyAction("EditExercise.Cancel", "Отменить", null, HotkeyKind.Window),
        ]),

        new HotkeyGroup("Импорт упражнений",
        [
            new HotkeyAction("Import.PickFile", "Выбрать файл…", null, HotkeyKind.Window),
            new HotkeyAction("Import.Save", "Сохранить", null, HotkeyKind.Window),
            new HotkeyAction("Import.Cancel", "Отменить", null, HotkeyKind.Window),
        ]),

        new HotkeyGroup("Планы",
        [
            new HotkeyAction("Plans.Add", "Добавить", null, HotkeyKind.Window),
            new HotkeyAction("Plans.ToggleSort", "Сортировка «Название»", null, HotkeyKind.Window),
            new HotkeyAction("Plans.Edit", "Редактировать", null, HotkeyKind.Row),
            new HotkeyAction("Plans.Delete", "Удалить", null, HotkeyKind.Row),
        ]),

        new HotkeyGroup("Правка плана",
        [
            new HotkeyAction("EditPlan.ResetBase", "Сбросить «На основе»", null, HotkeyKind.Window),
            new HotkeyAction("EditPlan.Accept", "Принять", null, HotkeyKind.Window),
            new HotkeyAction("EditPlan.Cancel", "Отменить", null, HotkeyKind.Window),
            new HotkeyAction("EditPlan.MoveUp", "Выше (↑)", null, HotkeyKind.Row),
            new HotkeyAction("EditPlan.MoveDown", "Ниже (↓)", null, HotkeyKind.Row),
            new HotkeyAction("EditPlan.Remove", "Убрать (✕)", null, HotkeyKind.Row),
            // Одна пара стрелок на оба блока: две пары в одном окне конфликтовали бы между собой
            // («Up» занят дважды), а вкладка считает конфликт по окну.
            new HotkeyAction("EditPlan.SelectedUp", "Строка выше (↑)", "Up", HotkeyKind.Window),
            new HotkeyAction("EditPlan.SelectedDown", "Строка ниже (↓)", "Down", HotkeyKind.Window),
            new HotkeyAction("EditPlan.ToggleCheck", "Отметка упражнения", "Space", HotkeyKind.Row),
            new HotkeyAction("EditPlan.BlockLeft", "Блок слева: все упражнения (←)", "Left", HotkeyKind.Window),
            new HotkeyAction("EditPlan.BlockRight", "Блок справа: порядок в плане (→)", "Right", HotkeyKind.Window),
        ]),

        new HotkeyGroup("Параметры",
        [
            new HotkeyAction("Settings.PickDirectory", "Обзор…", null, HotkeyKind.Window),
            new HotkeyAction("Settings.PickFile", "Выбрать файл…", null, HotkeyKind.Window),
            new HotkeyAction("Settings.Save", "Сохранить", null, HotkeyKind.Window),
            new HotkeyAction("Settings.Cancel", "Отменить", null, HotkeyKind.Window),
        ]),
    ];

    private static Dictionary<string, HotkeyAction> IndexOf { get; } =
        Groups.SelectMany(group => group.Actions).ToDictionary(action => action.Id, action => action);

    /// <summary>Назначение по id; неизвестный id — <c>null</c>.</summary>
    public static HotkeyAction? ActionOf(string id) =>
        IndexOf.TryGetValue(id, out var action) ? action : null;
}
