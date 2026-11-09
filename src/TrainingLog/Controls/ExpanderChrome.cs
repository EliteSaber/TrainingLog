using System.Windows;

namespace TrainingLog.Controls;

/// <summary>Доводка шеврона Expander'а: вертикальный сдвиг от геометрического центра строки.</summary>
/// <remarks>
/// Центр бокса строки шрифта не совпадает с её видимым центром, и величина расхождения зависит
/// от строения заголовка: у высокой многослойной шапки «Примечание» в «Правке дня» шеврон
/// читался ниже центра, у однострочных заголовков списка планов без сдвига точнее. Единая
/// поправка в шаблоне невозможна — значение задаётся с места вызова.
/// </remarks>
public static class ExpanderChrome
{
    /// <summary>Вертикальный сдвиг шеврона в пикселях: положительный — вниз.</summary>
    public static readonly DependencyProperty ChevronOffsetProperty =
        DependencyProperty.RegisterAttached(
            "ChevronOffset",
            typeof(double),
            typeof(ExpanderChrome),
            new PropertyMetadata(0.0));

    public static void SetChevronOffset(DependencyObject element, double value) =>
        element.SetValue(ChevronOffsetProperty, value);

    public static double GetChevronOffset(DependencyObject element) =>
        (double)element.GetValue(ChevronOffsetProperty);
}
