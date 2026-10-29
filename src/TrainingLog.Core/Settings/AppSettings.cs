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

    /// <summary>Полный адрес файла базы: сохранённые директория и наименование вместе.</summary>
    public string DatabasePath => Path.Combine(DatabaseDirectory, DatabaseName);
}
