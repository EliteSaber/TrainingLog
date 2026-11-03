using System.Windows;

namespace TrainingLog.Controls;

/// <summary>Присоединяемые свойства адреса назначаемой кнопки.</summary>
public static class Hotkey
{
    /// <summary>Id действия из каталога у назначаемой кнопки. Само по себе ничего не делает.</summary>
    /// <remarks>
    /// Адрес, а не поведение: биндер ищет кнопки дерева поиска по Id, а исполнителей строк и слотов
    /// окна регистрируют сами. Id должен совпадать с каталогом — его проверяет тест каталога.
    /// </remarks>
    public static readonly DependencyProperty IdProperty =
        DependencyProperty.RegisterAttached("Id", typeof(string), typeof(Hotkey));

    public static void SetId(DependencyObject element, string? value) =>
        element.SetValue(IdProperty, value);

    public static string? GetId(DependencyObject element) =>
        (string?)element.GetValue(IdProperty);

    /// <summary>Номер слота у кнопок-номеров окна дня: пометка адреса, а не поведение.</summary>
    /// <remarks>
    /// Исполнитель слота выбирает упражнение в модели представления, поэтому номер здесь
    /// дублирует содержимое кнопки и служит адресом для разметки.
    /// </remarks>
    public static readonly DependencyProperty SlotProperty =
        DependencyProperty.RegisterAttached("Slot", typeof(int), typeof(Hotkey));

    public static void SetSlot(DependencyObject element, int value) =>
        element.SetValue(SlotProperty, value);

    public static int GetSlot(DependencyObject element) =>
        (int)element.GetValue(SlotProperty);
}
