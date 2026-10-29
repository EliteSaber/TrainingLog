namespace TrainingLog.Core.Settings;

/// <summary>
/// Пригодность количества показываемых дней. Отдельным классом, а не свойствами у
/// <see cref="AppSettings"/>, по той же причине, что и <c>NameRules</c> отдельно от наименований.
/// </summary>
public static class DaysCountRules
{
    /// <summary>Меньше одного дня журнал не показывает: пустой список ничего не говорит.</summary>
    public const int MinDaysCount = 1;

    /// <summary>Список длиннее года на экране всё равно не помещается.</summary>
    public const int MaxDaysCount = 366;

    /// <summary>
    /// Проверка одна и та же для ввода в обоих окнах и для значения из файла: пригодность не
    /// должна зависеть от того, откуда пришло число.
    /// </summary>
    public static bool IsValid(int daysCount) => daysCount is >= MinDaysCount and <= MaxDaysCount;
}
