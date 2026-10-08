using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrainingLog.Controls;

/// <summary>
/// Отступ рамки, который снимается, пока вертикальная полоса прокрутки на экране.
/// </summary>
/// <remarks>
/// Повторяемое поведение: значение приходит свойством
/// <see cref="ScrollViewer.ComputedVerticalScrollBarVisibility"/> скролла, а величина отступа —
/// параметром конвертера, то есть её задаёт разметка. Окна и его отступы конвертер не знает.
///
/// Правило одно: полоса стоит у самого края окна и отнимает место у содержимого, поэтому
/// правый отступ рамки на её время исчезает. Иначе у таблицы справа остаётся воздух, которого
/// слева нет, и полоса читается как оторванная от содержимого.
/// </remarks>
public sealed class ScrollBarMarginConverter : IValueConverter
{
    /// <summary>
    /// Отступ рамки при заданном состоянии полосы.
    /// </summary>
    /// <param name="scrollBar">Состояние вертикальной полосы.</param>
    /// <param name="inset">Отступ, который держится слева и снизу.</param>
    /// <returns>Отступ с пустым правым запасом, если полоса на экране.</returns>
    /// <remarks>
    /// Полосой считается только <see cref="Visibility.Visible"/>: <c>Hidden</c> занимает место
    /// так же, как <c>Collapsed</c>, но на практике скролл отдаёт и то и другое, а
    /// <see cref="ScrollViewer.ComputedVerticalScrollBarVisibility"/> по контракту бывает
    /// <c>Visible</c> или <c>Collapsed</c>.
    ///
    /// Отступ приходит аргументом, а не зашит: правило должно работать с той величиной, которую
    /// задала разметка, иначе смена отступа окна разъезжалась бы с двумя числами в коде.
    /// </remarks>
    public static Thickness Resolve(Visibility scrollBar, double inset) =>
        scrollBar == Visibility.Visible
            ? new Thickness(inset, 0, 0, 0)
            : new Thickness(inset, 0, inset, 0);

    /// <summary>
    /// Превращает состояние полосы в отступ рамки.
    /// </summary>
    /// <param name="value">Состояние полосы: <see cref="Visibility"/>.</param>
    /// <param name="targetType">Тип цели — <see cref="Thickness"/>.</param>
    /// <param name="parameter">Величина отступа, числом или строкой.</param>
    /// <param name="culture">Культура разбора параметра.</param>
    /// <returns>Отступ по правилу <see cref="Resolve"/>.</returns>
    /// <remarks>
    /// Негодное значение или негодный параметр дают <see cref="DependencyProperty.UnsetValue"/>,
    /// а не отступ по умолчанию: молча подставить 16 значило бы выдать замысел за найденное
    /// значение, и в строке дня отступ вёл бы себя не так, как написано в разметке.
    /// </remarks>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Visibility scrollBar || !TryReadInset(parameter, out var inset))
        {
            return DependencyProperty.UnsetValue;
        }

        return Resolve(scrollBar, inset);
    }

    /// <summary>
    /// Обратного превращения нет: отступ задаёт разметка, а полоса только снимает его.
    /// </summary>
    /// <param name="value">Не используется.</param>
    /// <param name="targetType">Не используется.</param>
    /// <param name="parameter">Не используется.</param>
    /// <param name="culture">Не используется.</param>
    /// <returns>Никогда: вызов означал бы запись отступа обратно в полосу.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(ScrollBarMarginConverter)}: отступ задаёт разметка, а не полоса.");

    /// <summary>
    /// Читает величину отступа из параметра конвертера.
    /// </summary>
    /// <remarks>
    /// Параметр в разметке приходит строкой (<c>ConverterParameter=16</c>), поэтому число
    /// разбирается текстом, а инвариантно: величина отступа не должна зависеть от языка машины.
    /// </remarks>
    private static bool TryReadInset(object? parameter, out double inset)
    {
        switch (parameter)
        {
            case double value:
                inset = value;
                return true;

            case string text:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out inset);

            default:
                inset = 0;
                return false;
        }
    }
}