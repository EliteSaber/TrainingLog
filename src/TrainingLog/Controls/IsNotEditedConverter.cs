using System.Globalization;
using System.Windows.Data;

namespace TrainingLog.Controls;

/// <summary>
/// Признак того, что строка списка — не тот объект, который сейчас редактируется.
/// </summary>
/// <remarks>
/// Первое значение — строка списка (упражнение или план), второе — объект правки из модели
/// представления. Сравнение по ссылке, а не по имени: редактируется конкретный экземпляр из
/// этого же списка, и удалять можно только другой.
///
/// Отдельный конвертер, а не условие в коде окна: WPF сам снимает активность кнопки по
/// <c>IsEnabled</c>, и серое состояние видно до нажатия.
/// </remarks>
public sealed class IsNotEditedConverter : IMultiValueConverter
{
    /// <summary>
    /// Возвращает <c>true</c>, если строка не редактируется.
    /// </summary>
    /// <param name="values">Строка списка и объект правки.</param>
    /// <param name="targetType">Тип свойства привязки: <see cref="bool"/>.</param>
    /// <param name="parameter">Параметр привязки: не используется.</param>
    /// <param name="culture">Культура привязки: не используется.</param>
    /// <returns><c>true</c>, если объект правки — не эта строка.</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2)
        {
            return true;
        }

        return !ReferenceEquals(values[0], values[1]);
    }

    /// <summary>Обратный переход не нужен: кнопку только включают и выключают.</summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("IsNotEditedConverter работает только в одну сторону.");
}
