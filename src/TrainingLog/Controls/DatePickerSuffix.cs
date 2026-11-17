using System.Windows;

namespace TrainingLog.Controls;

/// <summary>
/// Подпись в правой части текстовой области поля даты: передаётся из модели в шаблон.
/// </summary>
/// <remarks>
/// Attached-свойство, а не новое свойство DatePicker: шаблон остаётся общим, подпись задаёт
/// место вызова, и шаблон не зависит от модели окна дня. Подпись только отображается —
/// <c>TextBlock</c> в шаблоне выведен из хит-теста, редактировать его нельзя.
/// </remarks>
public static class DatePickerSuffix
{
    /// <summary>Текст подписи; пустая строка — подписи нет.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(DatePickerSuffix),
        new PropertyMetadata(string.Empty));

    public static string GetText(DependencyObject element) =>
        (string)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string value) =>
        element.SetValue(TextProperty, value);
}
