using System.Windows;
using TrainingLog.Core.Settings;

namespace TrainingLog.Services;

/// <summary>
/// Размеры окон по умолчанию: откуда окно берёт свой размер и куда размеры применяются
/// после записи настроек.
/// </summary>
/// <remarks>
/// Состояние живёт в статике, как у <see cref="AppFonts"/> и <see cref="AppTheme"/>: размеры
/// нужны всем окнам, а окна создаются фабриками, а не контейнером, и передавать им сервис
/// пришлось бы через каждую фабрику.
///
/// Размеры у окон применяются дважды: при открытии окна (тогда берутся из файла настроек) и
/// после записи настроек (тогда ко всем открытым окнам разом). Второе нужно потому, что
/// размеры — настройка по умолчанию, а не размер на весь сеанс: пользователь задал новые и
/// ждёт, что они подействуют.
/// </remarks>
public static class WindowSizes
{
    private static IReadOnlyDictionary<string, WindowSize> _current = new Dictionary<string, WindowSize>();

    /// <summary>
    /// Текущие размеры окон: те, что действуют сейчас.
    /// </summary>
    /// <remarks>
    /// Во время предпросмотра в параметрах здесь лежат показанные размеры, а не записанные:
    /// файл меняется только по «Сохранить», а окна подстраиваются сразу.
    /// </remarks>
    public static IReadOnlyDictionary<string, WindowSize> Current => _current;

    /// <summary>
    /// Запоминает размеры из файла настроек.
    /// </summary>
    /// <remarks>
    /// Пустой словарь — обычное дело: секции в файле может не быть, и тогда размеры окна
    /// берутся из каталога. Ошибки чтения секции нет и быть не должно: негодные размеры
    /// отбрасываются при чтении файла, а не здесь.
    /// </remarks>
    /// <param name="sizes">Размеры из файла настроек: id окна к его размеру.</param>
    public static void Initialize(IReadOnlyDictionary<string, WindowSize>? sizes) =>
        _current = sizes ?? new Dictionary<string, WindowSize>();

    /// <summary>
    /// Задаёт окну размер: из файла настроек, а если его там нет — из каталога.
    /// </summary>
    /// <remarks>
    /// Окно находит себя в каталоге по своему типу: в разметке и в коде окна нет ни одного
    /// числа размера, иначе размер жил бы в двух местах.
    ///
    /// Минимум окна выставляется всегда, даже когда размер взят из файла: WPF всё равно не
    /// даст окну стать меньше минимума, а записанная величина, которая не действует, только
    /// вводит в заблуждение.
    /// </remarks>
    /// <param name="window">Окно, которому задаётся размер.</param>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (WindowSizeCatalog.Find(window.GetType()) is not { } entry)
        {
            return;
        }

        var size = WindowSizeCatalog.Resolve(entry, _current);

        window.MinWidth = entry.MinWidth;
        window.MinHeight = entry.MinHeight;
        window.Width = size.Width;

        // Высота по содержимому не выставляется: у такого окна её и вычислить нельзя, WPF
        // посчитает её сам по содержимому.
        if (size.Height is { } height)
        {
            window.Height = height;
        }
    }

    /// <summary>
    /// Применять ли размер к этому окну.
    /// </summary>
    /// <remarks>
    /// Отдельная функция, а не условие прямо в цикле: проверки целиком примитивные, поэтому
    /// правило проверяется тестом. Обвязка — подписка на событие — тестом не ловится в принципе.
    /// </remarks>
    /// <param name="window">Окно, которому решаем размер.</param>
    /// <param name="except">Окно, которое не трогаем; <c>null</c> — трогаем все.</param>
    /// <returns><c>true</c>, если размер окну применять.</returns>
    public static bool ShouldApply(Window window, Window? except) =>
        except is null || !ReferenceEquals(window, except);

    /// <summary>
    /// Применяет размеры ко всем открытым окнам: пользователь задал новые, и они должны
    /// подействовать сразу, а не со следующего открытия.
    /// </summary>
    /// <param name="except">
    /// Окно, которое не трогаем. Нужно окну параметров: оно меняет размер под курсором, и
    /// поле, в котором идёт правка, уезжает вбок.
    /// </param>
    public static void ApplyToOpenWindows(Window? except = null)
    {
        if (Application.Current is null)
        {
            return;
        }

        foreach (Window window in Application.Current.Windows)
        {
            if (ShouldApply(window, except))
            {
                Apply(window);
            }
        }
    }
}
