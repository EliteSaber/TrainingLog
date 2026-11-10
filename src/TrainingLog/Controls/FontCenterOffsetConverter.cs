using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TrainingLog.Controls;

/// <summary>
/// Сдвиг по вертикали, который ставит элемент, центрированный в боксе строки, на видимый
/// центр строки — по метрикам шрифта, а не по ручной доводке.
/// </summary>
/// <remarks>
/// Значения приходят парой: FontFamily и FontSize места применения. Элемент-цель наследует
/// шрифт окна, и подмена шрифта или кегля в «Параметрах» перечитывает привязку, поэтому
/// доводка живёт при любом шрифте. Считает <see cref="ExpanderChrome.AlignOffset"/>.
///
/// Негодные значения дают <see cref="DependencyProperty.UnsetValue"/>, а не молчаливый ноль:
/// подставленный ноль выглядел бы как «сдвига по метрикам нет», хотя по метрикам он есть.
/// </remarks>
public sealed class FontCenterOffsetConverter : IMultiValueConverter
{
    public object Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [FontFamily font, double fontSize])
        {
            return DependencyProperty.UnsetValue;
        }

        return ExpanderChrome.AlignOffset(font, fontSize);
    }

    /// <summary>Обратного превращения нет: сдвиг выводится из шрифта, а не хранится.</summary>
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(FontCenterOffsetConverter)}: сдвиг выводится из шрифта, а не наоборот.");
}
