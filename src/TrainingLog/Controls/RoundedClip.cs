using System.Windows;
using System.Windows.Media;

namespace TrainingLog.Controls;

/// <summary>
/// Скруглённая обрезка элемента: <c>helpers:RoundedClip.Radius="4"</c> обрезает
/// содержимое по прямоугольнику с этим радиусом, повторяя размеры элемента.
/// </summary>
/// <remarks>
/// Зачем: слои стеклянных кнопок (блики, грани, отблёски) лежат до прямых краёв
/// bounding box и торчат за дугу стекла на скруглённых торцах — без обрезки кнопка
/// выглядит подрезанной. WPF не умеет скруглённый эквивалент ClipToBounds,
/// RectangleGeometry в разметке не берёт размеры элемента — поэтому геометрию
/// собирает этот хелпер по факту SizeChanged.
/// </remarks>
public static class RoundedClip
{
    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.RegisterAttached(
            "Radius",
            typeof(double),
            typeof(RoundedClip),
            new PropertyMetadata(0d, OnRadiusChanged));

    public static double GetRadius(DependencyObject element) =>
        (double)element.GetValue(RadiusProperty);

    public static void SetRadius(DependencyObject element, double value) =>
        element.SetValue(RadiusProperty, value);

    private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        // Повторная установка радиуса не должна дублировать подписку.
        element.SizeChanged -= OnElementSizeChanged;
        element.SizeChanged += OnElementSizeChanged;

        Apply(element);
    }

    private static void OnElementSizeChanged(object sender, SizeChangedEventArgs e) =>
        Apply((FrameworkElement)sender);

    /// <summary>Голая функция геометрии обрезки — её и покрывает тест.</summary>
    public static Geometry BuildGeometry(Size size, double radius) =>
        new RectangleGeometry(new Rect(size), radius, radius);

    private static void Apply(FrameworkElement element)
    {
        // До первой раскладки размер нулевой: пустой RectangleGeometry спрятал бы
        // элемент до первого SizeChanged, поэтому обрезка снимается вовсе.
        element.Clip = element.ActualWidth > 0 && element.ActualHeight > 0
            ? BuildGeometry(new Size(element.ActualWidth, element.ActualHeight), GetRadius(element))
            : null;
    }
}
