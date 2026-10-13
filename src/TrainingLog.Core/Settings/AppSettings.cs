namespace TrainingLog.Core.Settings;

/// <summary>Настройки приложения: то, что записано в файле рядом с программой.</summary>
/// <remarks>
/// Пригодность значения проверяет <see cref="DaysCountRules"/>, а не сам класс: правило одно на
/// оба окна, и своя проверка здесь завела бы его в двух местах.
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
}
