using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TrainingLog.Controls;

/// <summary>
/// Передача фокуса элементу по клику на подпись. Повторяемое поведение: достаточно
/// указать <c>ClickToFocus.Target="{Binding ElementName=...}"</c> на подписи.
/// </summary>
/// <remarks>
/// Само по себе слово-подпись в WPF ничего не делает: ни <c>TextBlock</c>, ни
/// <see cref="System.Windows.Controls.Label"/> по клику фокус не передают. У
/// <c>Label</c> свойство <c>Target</c> работает только по клавише доступа и на
/// подпись для экранных читалок, а обработчика мыши у него нет вовсе, поэтому
/// передачу фокуса приходится делать самому.
///
/// Клик передаёт не только фокус, но и выделение: текст цели выделяется, чтобы значение удобно
/// было ввести заново, без стирания. Поведение безусловное, и это осознанно — подписей в
/// проекте пять («Количество дней», «Поиск» в справочнике упражнений и в окне планов,
/// «Наименование» в окне правки плана, «Название упражнения» в окне правки упражнения), и все
/// пять ведут в поля, где значение вводят целиком. Флага для отключения нет: заводить его
/// незачем, пока правило всех устраивает.
/// </remarks>
public static class ClickToFocus
{
    /// <summary>
    /// Элемент, получающий фокус по клику на подпись.
    /// </summary>
    /// <remarks>
    /// <c>null</c> отключает поведение и отписывает подпись от события. Работает на любом
    /// элементе, не только на подписи: <c>TextBlock</c> чаще всего, но можно и на кнопке.
    /// </remarks>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target",
        typeof(UIElement),
        typeof(ClickToFocus),
        new PropertyMetadata(null, OnTargetChanged));

    public static UIElement? GetTarget(DependencyObject element) => (UIElement?)element.GetValue(TargetProperty);

    public static void SetTarget(DependencyObject element, UIElement? value) => element.SetValue(TargetProperty, value);

    /// <summary>
    /// Решает, передавать ли фокус цели по клику.
    /// </summary>
    /// <param name="hasTarget">Задана ли цель вообще.</param>
    /// <param name="targetFocused">Находится ли цель уже в фокусе.</param>
    /// <param name="targetEnabled">Доступна ли цель: на отключённом поле фокус не встанет.</param>
    /// <returns><c>true</c>, если фокус нужно передать.</returns>
    /// <remarks>
    /// Отдельная функция, а не условие прямо в обработчике: проверки целиком примитивные,
    /// поэтому правило проверяется тестом. Обвязка — подписка на событие — тестом не
    /// ловится в принципе, её видно только кликом в работающем окне.
    /// </remarks>
    public static bool ShouldFocus(bool hasTarget, bool targetFocused, bool targetEnabled) =>
        hasTarget && !targetFocused && targetEnabled;

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        // Через событие элемента, а не через AddHandler/RemoveHandler: их параметр имеет тип
        // Delegate, и начиная с C# 10 группа методов конвертируется в него по своему
        // естественному типу, то есть в Action<object, MouseButtonEventArgs>, а не в
        // MouseButtonEventHandler, который зарегистрирован за событием. WPF такой делегат
        // отвергает и бросает ArgumentException «Несоответствие типа обработчика».
        // У события элемента объявленный тип делегата, поэтому конверсия правильная сама.
        // Отписка перед подпиской обязательна: повторное присваивание цели иначе оставило бы
        // на подписи два обработчика, и фокус передавался бы дважды.
        element.MouseLeftButtonDown -= OnMouseLeftButtonDown;

        if (e.NewValue is UIElement)
        {
            element.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }
    }

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement element || GetTarget(element) is not UIElement target)
        {
            return;
        }

        if (!ShouldFocus(hasTarget: true, targetFocused: target.IsKeyboardFocused, targetEnabled: target.IsEnabled))
        {
            return;
        }

        if (target.Focus())
        {
            // Выделение — после фокуса, а не до: постановка фокуса сбрасывает выделение,
            // и наоборот получилось бы поле в фокусе с кареткой в конце, а не весь текст.
            // Цель, умеющая выделять, — это TextBoxBase (TextBox, PasswordBox и производные);
            // на кнопке или любом другом элементе выделять нечего, и это не ошибка.
            if (target is TextBoxBase box)
            {
                box.SelectAll();
            }

            // Клик по подписи не должен доходить дальше: иначе его увидит, например,
            // родительский ScrollViewer и начнёт перетаскивание содержимого.
            e.Handled = true;
        }
    }
}
