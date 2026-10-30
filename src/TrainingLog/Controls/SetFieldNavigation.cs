using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TrainingLog.Controls;

/// <summary>
/// Столбец подхода, к которому относится поле. <see cref="None"/> отключает поведение.
/// </summary>
public enum SetField
{
    /// <summary>Поле не участвует в переходах по Tab.</summary>
    None,

    /// <summary>Вес подхода.</summary>
    Weight,

    /// <summary>Повторения подхода.</summary>
    Repetitions,
}

/// <summary>
/// Переход по Tab по единой цепочке «все веса, затем все повторения»: от веса первого
/// подхода до веса последнего, затем повторения по подходам. Повторяемое поведение:
/// достаточно указать <c>SetFieldNavigation.Part="Weight"</c> на нужном <see cref="TextBox"/>.
/// </summary>
/// <remarks>
/// Поля одного подхода стоят в столбце друг под другом, и обход дерева даёт «вес, повторения,
/// следующий подход» — из повторений Tab уходил бы в вес соседа. Для набора быстрее идти по
/// столбцу, поэтому переход ведёт по единой цепочке: все веса подряд,
/// все повторения сразу за ними.
///
/// Стандартные средства WPF такого порядка не умеют: KeyboardNavigation ходит только по
/// таб-порядку элементов, а «тот же столбец соседнего подхода» деревом не выражается.
///
/// Перехват стоит на <c>PreviewKeyDown</c> с <c>e.Handled</c>: так обработка гарантированно
/// идёт раньше обхода дерева, а другим клавишам поведение ничего не меняет.
///
/// Границы цепочки поведению не принадлежат: Tab после последних повторений и Shift+Tab до
/// первого веса цели не имеют, фокус по умолчанию продолжает обычную цепочку на примечание
/// и кнопки. Внутри же столбцы зациклены — Tab с последнего веса ведёт к первым повторениям,
/// Shift+Tab с первых повторений возвращает к последнему весу.
/// </remarks>
public static class SetFieldNavigation
{
    /// <summary>
    /// Столбец, к которому относится поле. <see cref="SetField.None"/> отключает поведение
    /// и снимает уже подвешенную подписку.
    /// </summary>
    public static readonly DependencyProperty PartProperty = DependencyProperty.RegisterAttached(
        "Part",
        typeof(SetField),
        typeof(SetFieldNavigation),
        new PropertyMetadata(SetField.None, OnPartChanged));

    public static SetField GetPart(DependencyObject element) => (SetField)element.GetValue(PartProperty);

    public static void SetPart(DependencyObject element, SetField value) => element.SetValue(PartProperty, value);

    /// <summary>
    /// Позиция поля в единой цепочке «все веса, затем все повторения».
    /// </summary>
    /// <param name="part">Столбец поля.</param>
    /// <param name="setIndex">Номер подхода поля.</param>
    /// <param name="count">Сколько всего подходов.</param>
    /// <returns>Позиция: веса занимают первую половину цепочки, повторения — вторую.</returns>
    /// <remarks>
    /// Отдельная функция, а не условие прямо в обработчике: правило целиком примитивное,
    /// и проверяется тестом. Обвязка — подписка на событие и поиск в дереве — тестом
    /// не ловится в принципе.
    /// </remarks>
    public static int PositionOf(SetField part, int setIndex, int count) =>
        part == SetField.Weight ? setIndex : count + setIndex;

    /// <summary>
    /// Поле на позиции единой цепочки: обратное отображение к <see cref="PositionOf"/>.
    /// </summary>
    /// <param name="position">Позиция в цепочке.</param>
    /// <param name="count">Сколько всего подходов.</param>
    /// <returns>Столбец и номер подхода: первая половина цепочки — веса, вторая — повторения.</returns>
    /// <remarks>
    /// Отдельная функция, а не условие прямо в обработчике: правило целиком примитивное,
    /// и проверяется тестом.
    /// </remarks>
    public static (SetField Part, int SetIndex) FieldAt(int position, int count) =>
        position < count ? (SetField.Weight, position) : (SetField.Repetitions, position - count);

    /// <summary>
    /// Индекс позиции, куда переходит фокус, либо <c>null</c>, когда позиции больше нет.
    /// </summary>
    /// <param name="position">Позиция текущего поля в цепочке.</param>
    /// <param name="length">Длина цепочки: два столбца на число подходов.</param>
    /// <param name="forward"><c>true</c> — Tab, <c>false</c> — Shift+Tab.</param>
    /// <returns>Индекс цели либо <c>null</c>: тогда фокус продолжает обычную цепочку.</returns>
    /// <remarks>
    /// Отдельная функция, а не условие прямо в обработчике: правило целиком примитивное,
    /// и проверяется тестом.
    /// </remarks>
    public static int? TargetIndex(int position, int length, bool forward)
    {
        var target = forward ? position + 1 : position - 1;

        return target >= 0 && target < length ? target : null;
    }

    private static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
        {
            return;
        }

        // Отписка перед подпиской обязательна: повторное присваивание столбца иначе оставило бы
        // на поле два обработчика, и фокус передавался бы дважды.
        box.PreviewKeyDown -= OnPreviewKeyDown;

        if (e.NewValue is SetField part && part != SetField.None)
        {
            box.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    /// <summary>В поле подхода нажат Tab: фокус передаётся полю следующей позиции цепочки.</summary>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox source || e.Key != Key.Tab)
        {
            return;
        }

        var part = GetPart(source);

        if (part == SetField.None || FindColumn(source) is not { } column)
        {
            return;
        }

        var count = column.Panel.Children.Count;
        var position = PositionOf(part, column.Index, count);
        var forward = (Keyboard.Modifiers & ModifierKeys.Shift) == 0;

        // Цели нет — e.Handled не ставится: стандартный обход дерева сам уводит фокус
        // на примечание и кнопки сохранения.
        if (TargetIndex(position, count * 2, forward) is not { } targetPosition)
        {
            return;
        }

        var targetField = FieldAt(targetPosition, count);

        // Подключение ищется в нужном подходе; поле целевого столбца может лежать на разной
        // глубине: вес — прямой ребёнок корня шаблона, повторения — внутри сетки плейсхолдера.
        if (FindField(column.Panel.Children[targetField.SetIndex], targetField.Part)
            is not { IsEnabled: true } target)
        {
            return;
        }

        if (!target.Focus())
        {
            return;
        }

        // Выделение после фокуса: постановка фокуса сбрасывает выделение (тот же порядок, что
        // в ClickToFocus) — число вводится заново целиком, без стирания.
        target.SelectAll();

        e.Handled = true;
    }

    /// <summary>
    /// Панель столбцов подходов и индекс столбца, в котором лежит поле, либо <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Контейнер подхода — ближайший предок поля, чей родитель есть <c>WrapPanel</c>:
    /// между ними стоят корень шаблона и <c>ContentPresenter</c>, а у самих полей родителя-панели
    /// этого вида нет, поэтому условие находит ровно столбец подхода. Поиск визуальный:
    /// поле не знает своего VM и жителей соседних столбцов.
    /// </remarks>
    private static (WrapPanel Panel, int Index)? FindColumn(TextBox field)
    {
        var node = VisualTreeHelper.GetParent(field);

        while (node is not null)
        {
            if (VisualTreeHelper.GetParent(node) is WrapPanel panel)
            {
                return (panel, panel.Children.IndexOf((UIElement)node));
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    /// <summary>
    /// Поле нужного столбца в поддереве контейнера.
    /// </summary>
    /// <remarks>
    /// Поиск по всему поддереву, а не по прямым детям: вес — прямой ребёнок корня шаблона,
    /// а повторения лежат внутри сетки плейсхолдера, и глубина у столбцов разная.
    /// </remarks>
    private static TextBox? FindField(DependencyObject root, SetField part)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is TextBox box && GetPart(box) == part)
            {
                return box;
            }

            if (FindField(child, part) is TextBox descendant)
            {
                return descendant;
            }
        }

        return null;
    }
}
