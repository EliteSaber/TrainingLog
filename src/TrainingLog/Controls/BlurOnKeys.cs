using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace TrainingLog.Controls;

/// <summary>
/// Снятие фокуса с поля по клавишам. Повторяемое поведение: достаточно указать
/// <c>BlurOnKeys.Keys="Escape,Enter"</c> и <c>BlurOnKeys.Target="{Binding ElementName=...}"</c>.
/// </summary>
/// <remarks>
/// Поле перехватывает свои служебные клавиши: вертикальные стрелки каретки TextBoxBase
/// обрабатывает сам и помечает Handled, поэтому привязки окна на эти клавиши не срабатывают, пока
/// клавиатурный фокус в поле. Клик по нефокусируемому элементу (строка журнала) фокус за полем
/// не закрепляет — Keyboard.FocusedElement остаётся прежним, и «снятый» кликом фокус живёт дальше.
/// Единственный честный способ вернуть ввод окну — уводить фокус клавишами.
///
/// Поведение безусловное: цель задана, клавиша в перечне, фокус на элементе — фокус уходит.
/// Проверок «может ли цель получить фокус» нет: нефокусируемая цель — ошибка разметки.
/// </remarks>
public static class BlurOnKeys
{
    /// <summary>Перечень клавиш, по которым фокус уходит: имена WPF <see cref="Key"/>, через запятую.</summary>
    /// <remarks>
    /// Задача каждой записи — быть понятной в разметке: «Escape,Enter». Отписка здесь
    /// одна и навсегда: пустой перечень тоже подписка, только молчит.
    /// </remarks>
    public static readonly DependencyProperty KeysProperty = DependencyProperty.RegisterAttached(
        "Keys",
        typeof(string),
        typeof(BlurOnKeys),
        new PropertyMetadata(null, OnKeysChanged));

    /// <summary>Элемент, получающий фокус по указанной клавише.</summary>
    /// <remarks>
    /// null отключает поведение: обработчик проверяет цель живьём при каждом нажатии, поэтому
    /// задавать его можно и после клавиш — в разметке поле объявлено раньше списка.
    /// </remarks>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target",
        typeof(UIElement),
        typeof(BlurOnKeys));

    public static string GetKeys(DependencyObject element) => (string)element.GetValue(KeysProperty);

    public static void SetKeys(DependencyObject element, string? value) =>
        element.SetValue(KeysProperty, value);

    public static UIElement? GetTarget(DependencyObject element) => (UIElement?)element.GetValue(TargetProperty);

    public static void SetTarget(DependencyObject element, UIElement? value) =>
        element.SetValue(TargetProperty, value);

    /// <summary>Решает, уходит ли фокус по нажатию.</summary>
    /// <param name="hasTarget">Задана ли цель вообще.</param>
    /// <param name="elementFocused">Стоит ли фокус на самом элементе.</param>
    /// <param name="keyListed">Слушается ли такая клавиша.</param>
    /// <returns><c>true</c>, если фокус нужно снять.</returns>
    /// <remarks>
    /// Отдельная функция, а не условие прямо в обработчике: проверки целиком примитивные,
    /// поэтому правило проверяется тестом. Обвязка — подписка на событие — тестом не ловится
    /// в принципе.
    /// </remarks>
    public static bool ShouldRelease(bool hasTarget, bool elementFocused, bool keyListed) =>
        hasTarget && elementFocused && keyListed;

    /// <summary>Разбирает перечень клавиш или отвечает отказом.</summary>
    /// <remarks>
    /// Отказы: пусто, мусорная часть («Escape,Мусор»), повтор клавиши («Escape,Escape») —
    /// дубль не несёт смысла, как повтор модификатора в <c>HotkeyRules</c>. Окружающие пробелы
    /// частей снимаются, регистр не важен.
    /// </remarks>
    public static bool TryReadKeys(string? text, out IReadOnlyList<Key> keys)
    {
        keys = [];

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        List<Key> parsed = [];

        foreach (var part in text.Split(','))
        {
            var trimmed = part.Trim();

            if (trimmed.Length == 0 || !Enum.TryParse(trimmed, ignoreCase: true, out Key key))
            {
                return false;
            }

            if (parsed.Contains(key))
            {
                return false;
            }

            parsed.Add(key);
        }

        keys = parsed;

        return true;
    }

    /// <summary>Слушается ли такая клавиша в разобранном перечне.</summary>
    public static bool ContainsKey(IReadOnlyList<Key> keys, Key key) => keys.Contains(key);

    private static void OnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        // Прямая подписка через событие, а не AddHandler/RemoveHandler: их параметр имеет тип
        // Delegate, и группа методов конвертируется в natural-тип вместо KeyEventHandler
        // (ловушка 14). Отписка перед подпиской обязательна: повторное присваивание перечня
        // иначе оставило бы два обработчика, и фокус уходил бы дважды.
        element.PreviewKeyDown -= OnPreviewKeyDown;

        if (TryReadKeys(GetKeys(element), out _))
        {
            element.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not UIElement element
            || GetTarget(element) is not UIElement target
            || !TryReadKeys(GetKeys(element), out var keys)
            || !ContainsKey(keys, e.Key))
        {
            return;
        }

        if (!ShouldRelease(hasTarget: true, elementFocused: element.IsKeyboardFocused, keyListed: true))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            // Enter не гасится: IsDefault кнопки «Применить» обрабатывает нажатие сам. Снятие
            // фокуса откладывается до конца обработки ввода — очередь на приоритете Background
            // разбирается только после пустой очереди Input, и клавиша к тому моменту дойдёт
            // до всего, что её слушает.
            element.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => Release(element, target));

            return;
        }

        // Остальные клавиши, прежде всего Esc, снимают фокус сразу, а события гасятся:
        // ничего кроме поведения за ними не стоит.
        target.Focus();

        e.Handled = true;
    }

    private static void Release(UIElement element, UIElement target)
    {
        // Отложенное снятие: фокус мог уйти с поля сам, и уводить его в цель нечему —
        // поля в фокусе уже нет.
        if (element.IsKeyboardFocused)
        {
            target.Focus();
        }
    }
}
