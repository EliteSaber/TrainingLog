using System.Windows;
using System.Windows.Input;

namespace TrainingLog.Controls;

/// <summary>
/// Команда крестика очистки внутри поля даты: передаётся из модели в шаблон.
/// </summary>
/// <remarks>
/// Attached-свойство, а не новое свойство <see cref="System.Windows.Controls.DatePicker"/>:
/// шаблон остаётся общим, наличие крестика задаёт место вызова. Команда сама обнуляет дату в
/// модели, и привязка SelectedDate переносит это в поле — шаблону не нужно знать, что чистить.
/// Крестик показывается только при заданной команде и выбранной дате: в окне, где очистка не
/// нужна, поле выглядит как раньше.
/// </remarks>
public static class DatePickerClear
{
    /// <summary>Команда очистки; <c>null</c> — крестика в поле нет.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(DatePickerClear),
        new PropertyMetadata(null));

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);
}
