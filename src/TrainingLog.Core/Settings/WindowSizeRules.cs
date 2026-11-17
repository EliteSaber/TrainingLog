namespace TrainingLog.Core.Settings;

/// <summary>
/// Пригодность размера окна: границы, одинаковые для файла настроек и для ввода в окне.
/// </summary>
/// <remarks>
/// Границы здесь — общие, «разумные»: файл настроек правят руками, и значение оттуда может
/// оказаться чем угодно. Минимальный размер конкретного окна задаёт каталог окон (слой WPF):
/// у главного окна минимум больше, чем у диалога. Правило общее для обоих слоёв, чтобы
/// негодное значение отсекалось одинаково и в файле, и при вводе.
/// </remarks>
public static class WindowSizeRules
{
    /// <summary>Уже этого размера окно бесполезно: содержимое в него не поместится.</summary>
    public const int MinSize = 100;

    /// <summary>Больше этого окно не бывает: за экраном оно всё равно не поместится.</summary>
    public const int MaxSize = 4000;

    /// <summary>Проверка одна для ввода в окне и для значения из файла.</summary>
    /// <param name="width">Ширина окна.</param>
    /// <param name="height">Высота окна; <c>null</c> — высота по содержимому.</param>
    public static bool IsValid(int width, int? height) =>
        width is >= MinSize and <= MaxSize
        && (height is null || height is >= MinSize and <= MaxSize);
}
