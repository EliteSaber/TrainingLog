using System.Windows;
using System.Windows.Controls;

namespace TrainingLog.Controls;

/// <summary>
/// Признак занятости стеклянной кнопки: <c>helpers:BusyButton.IsBusy</c>.
/// </summary>
/// <remarks>
/// Пока значение истинно, общий шаблон кнопки (Themes/GlassButton.xaml) прячет содержимое и
/// показывает кольцо загрузки. Свойство живёт на кнопке, а не в модели представления: кнопка
/// сама не знает, чем занята, а шаблон один на все стеклянные кнопки, и копировать разметку
/// кольца в каждую нельзя. Шаблон читает значение привязкой к TemplatedParent.
/// </remarks>
public static class BusyButton
{
    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.RegisterAttached(
            "IsBusy",
            typeof(bool),
            typeof(BusyButton),
            new FrameworkPropertyMetadata(false));

    public static bool GetIsBusy(Button button) => (bool)button.GetValue(IsBusyProperty);

    public static void SetIsBusy(Button button, bool value) => button.SetValue(IsBusyProperty, value);
}
