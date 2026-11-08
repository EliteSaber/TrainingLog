using TrainingLog.Core.Data;

namespace TrainingLog.Core.Settings;

/// <summary>Настройки приложения: то, что записано в файле рядом с программой.</summary>
/// <remarks>
/// Пригодность значения проверяет <see cref="DaysCountRules"/>, а не сам класс: правило одно на
/// оба окна, и своя проверка здесь завела бы его в двух местах.
///
/// Умолчания полей базы повторяют те же значения, что у <see cref="SqliteDatabasePath"/>:
/// «не записано в файле» обязано означать «база там же, где всегда», а не вторую базу.
/// </remarks>
public sealed class AppSettings
{
    /// <summary>Сколько дней показывать, пока пользователь ничего не выбрал.</summary>
    /// <remarks>
    /// Это же значение попадает в только что созданный файл — держать второе умолчание для
    /// случая «файла нет» незачем.
    /// </remarks>
    public const int DefaultDaysCount = 3;

    /// <summary>Сколько дней показывать в журнале. Из файла может быть любым целым.</summary>
    public int DaysCount { get; set; } = DefaultDaysCount;

    /// <summary>Где лежит база. Пустым не остаётся: умолчание подставляет хранилище файла.</summary>
    public string DatabaseDirectory { get; set; } = SqliteDatabasePath.DefaultDirectory;

    /// <summary>Как называется файл базы. Пустым не остаётся — тот же договор.</summary>
    public string DatabaseName { get; set; } = SqliteDatabasePath.FileName;

    /// <summary>Гарнитура приложения или пустая строка: «по цепочке» умолчаний.</summary>
    /// <remarks>
    /// Имя каноническое (не локализованное): записанное локальное не нашлось бы на машине с
    /// другой локалью. Наличие в системе проверяет WPF-слой, правило — только нормализацию.
    /// </remarks>
    public const string DefaultFontFamily = "";

    /// <summary>Гарнитура приложения. Пустое значение означает «по цепочке» умолчаний.</summary>
    public string FontFamily { get; set; } = DefaultFontFamily;

    /// <summary>Масштаб кегля при отсутствии настройки: правило одно с проверкой ввода.</summary>
    /// <remarks>
    /// Своё имя держится ради симметрии с другими умолчаниями класса: и хранилище, и окно
    /// ним пользуются, а не константой правила.
    /// </remarks>
    public const int DefaultFontSizePercent = FontRules.DefaultScalePercent;

    /// <summary>Масштаб кегля в процентах. Пригодность проверяет <see cref="FontRules"/>.</summary>
    public int FontSizePercent { get; set; } = DefaultFontSizePercent;

    /// <summary>Полный адрес файла базы: сохранённые директория и наименование вместе.</summary>
    public string DatabasePath => Path.Combine(DatabaseDirectory, DatabaseName);

    /// <summary>Горячие клавиши: id действия к canonical-строке комбинации.</summary>
    /// <remarks>
    /// Пригодность строки проверяет <see cref="HotkeyRules"/>, выбор между назначенным и
    /// умолчанием — WPF-слой. Словарь годится, а не массив записей: секцию «Hotkeys»
    /// System.Text.Json читает без конвертеров, и порядок строк в файле значения не имеет.
    /// </remarks>
    public Dictionary<string, string> Hotkeys { get; set; } = new();
}
