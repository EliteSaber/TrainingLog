using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TrainingLog.Controls;

/// <summary>
/// Подсказка полю ввода с длинным значением: пока текст не помещается в поле, наведение
/// показывает его целиком.
/// </summary>
/// <remarks>
/// Директория базы длиннее окна параметров, и поле показывает только часть: текст листается
/// курсором, но целиком не читается. Подсказка появляется только при переполнении — значение,
/// которому места хватает, tooltip-ом не дублируется.
///
/// Признак переполнения — собственные показатели внутреннего ScrollViewer поля: ExtentWidth
/// (настоящая ширина текста) больше ViewportWidth (видимой части). Мерить текст отдельно через
/// FormattedText значило бы собрать в себе шрифт, поля и рамки, а эти показатели их уже учитывают.
///
/// Пересчёт отложен через Dispatcher: показатели меряются укладкой, и в момент TextChanged или
/// SizeChanged они ещё не посчитаны (NaN) — мгновенный разбор обвёлся бы промахом, отложенный
/// считает уже по готовым значениям.
///
/// Свойство attached, а не поведение: состояния оно не держит, подписка сама ставится и
/// снимается при включении и выключении, и обработчиками окно не занимается.
/// </remarks>
public static class OverflowTip
{
    /// <summary>Свойство attached: включить подсказку переполнения для поля.</summary>
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(OverflowTip),
        new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject element) =>
        (bool)element.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject element, bool value) =>
        element.SetValue(EnabledProperty, value);

    /// <summary>Допуск сравнения: разница меньше половины пикселя переполнением не считается.</summary>
    /// <remarks>
    /// Показатели ScrollViewer измеряются с дробной точностью, и у влезающего значения они
    /// расходились на доли пикселя — подсказка мелькала бы над каждым полем.
    /// </remarks>
    private const double Slack = 0.5;

    /// <summary>Решение: влезает ли текст в поле. Публичная чистая функция — проверяется тестом.</summary>
    /// <param name="extentWidth">Настоящая ширина текста (ExtentWidth поля).</param>
    /// <param name="viewportWidth">Ширина видимой части (ViewportWidth поля).</param>
    public static bool Overflows(double extentWidth, double viewportWidth) =>
        extentWidth - viewportWidth > Slack;

    /// <summary>Предел ширины подсказки: длинный путь одной строкой уходил бы за экран.</summary>
    /// <remarks>Число произвольное — порядка трети окна: переносом гарантируется читаемость.</remarks>
    private const double MaxToolTipWidth = 320;

    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox box)
        {
            throw new InvalidOperationException("Подсказка переполнения задаётся полю ввода TextBox.");
        }

        // Отписка перед подпиской: повторное присваивание навесило бы вторые обработчики, и
        // подсказка переставлялась бы дважды.
        box.TextChanged -= OnTextChanged;
        box.SizeChanged -= OnSizeChanged;

        if (!(bool)e.NewValue)
        {
            box.ToolTip = null;

            return;
        }

        box.TextChanged += OnTextChanged;
        box.SizeChanged += OnSizeChanged;

        Update(box);
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e) =>
        Update((TextBox)sender);

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        Update((TextBox)sender);

    /// <summary>Пересчитывает подсказку после завершения текущего layout-прохода.</summary>
    private static void Update(TextBox box) =>
        box.Dispatcher.BeginInvoke(RefreshToolTip, DispatcherPriority.Loaded, box);

    private static void RefreshToolTip(object? state)
    {
        if (state is not TextBox box)
        {
            return;
        }

        if (double.IsNaN(box.ExtentWidth) || double.IsNaN(box.ViewportWidth))
        {
            return;
        }

        box.ToolTip = Overflows(box.ExtentWidth, box.ViewportWidth) ? FullToolTip(box.Text) : null;
    }

    /// <summary>Подсказка несёт значение целиком и переносится строками, а не уходит за экран.</summary>
    private static ToolTip FullToolTip(string text) => new()
    {
        Content = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = MaxToolTipWidth,
        },
    };
}
