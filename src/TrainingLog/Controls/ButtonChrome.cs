using System.Windows;
using System.Windows.Controls;

namespace TrainingLog.Controls;

/// <summary>
/// Радиус углов стеклянной кнопки: <c>helpers:ButtonChrome.CornerRadius</c>.
/// Умолчание 4 — вариант по мокапу docs/addDayButtonStates.svg.
/// </summary>
/// <remarks>
/// Зачем свойство на кнопке, а не константа шаблона: шаблон один, а радиус нужен
/// двум вариантам — скругленному (умолчание, радиус 4) и мягким углам (*Soft-стили,
/// радиус 2; значение можно менять, не трогая шаблон). TemplateBinding
/// читает только встроенные свойства Button, поэтому шаблон берёт значение
/// RelativeSource-привязкой к TemplatedParent.
/// </remarks>
public static class ButtonChrome
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(ButtonChrome),
            new FrameworkPropertyMetadata(new CornerRadius(4)));

    public static CornerRadius GetCornerRadius(Button button) =>
        (CornerRadius)button.GetValue(CornerRadiusProperty);

    public static void SetCornerRadius(Button button, CornerRadius value) =>
        button.SetValue(CornerRadiusProperty, value);
}
