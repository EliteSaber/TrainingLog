namespace TrainingLog.Core.Settings;

/// <summary>
/// Пригодность шрифтовой настройки: масштаб кегля в процентах и нормализация имени гарнитуры.
/// Отдельным классом, а не свойствами у <see cref="AppSettings"/>, — по той же причине, что и
/// <see cref="DaysCountRules"/> отдельно от количества дней.
/// </summary>
public static class FontRules
{
    /// <summary>
    /// Гарнитура по умолчанию: пока в файле ничего не сохранено, и годный откат, когда
    /// сохранённое имя из системы пропало. Segoe UI есть на любом Windows 10+, поэтому
    /// умолчание не умеет «не найтись»; цепочка замен не нужна — у приложения есть выборщик
    /// шрифтов, а недостающие в гарнитуре символы WPF дорисует композитным заменителем.
    /// </summary>
    public const string DefaultFontFamily = "Segoe UI";

    /// <summary>Ниже восьмидесяти кегль нечитаем даже на белом фоне.</summary>
    public const int MinScalePercent = 80;

    /// <summary>Выше ста пятидесяти текст фиксированных кнопок (ширина 216) не влезает.</summary>
    public const int MaxScalePercent = 150;

    /// <summary>Значение «как было»: до первого выбора размеры точно такие, у чего в разметке.</summary>
    public const int DefaultScalePercent = 100;

    /// <summary>Проверка одна для ввода в окне параметров, старта приложения и значения из файла.</summary>
    public static bool IsScaleValid(int percent) => percent is >= MinScalePercent and <= MaxScalePercent;

    /// <summary>Имя шрифта со срезанными краями: пустое и пробельное одинаково означают «по цепочке».</summary>
    public static string NormalizeName(string? name) => name?.Trim() ?? string.Empty;

    /// <summary>Масштаб кегля: базовый размер из словаря умножается на процент целиком, а не по частям.</summary>
    public static double Scale(double size, int percent) => size * percent / 100.0;
}
