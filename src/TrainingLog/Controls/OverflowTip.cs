using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TrainingLog.Controls;

/// <summary>
/// Подсказка для текста, который не помещается целиком: пока значение обрезано, наведение
/// показывает его полностью.
/// </summary>
/// <remarks>
/// Два применения: поле ввода с длинным путём базы и заголовок окна, урезанный многоточием.
/// И там, и там подсказка появляется только при переполнении — значение, которому места
/// хватает, tooltip-ом не дублируется.
///
/// У поля признак переполнения — собственные показатели внутреннего ScrollViewer: ExtentWidth
/// (настоящая ширина текста) больше ViewportWidth (видимой части). Мерить текст отдельно через
/// FormattedText значило бы собрать в себе шрифт, поля и рамки, а эти показатели их уже учитывают.
/// У заголовка нет скролла, поэтому полный текст меряется пробным TextBlock с тем же шрифтом,
/// а сравнивается с ActualWidth видимого.
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
    /// <summary>Свойство attached: включить подсказку переполнения для поля или заголовка.</summary>
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

    /// <summary>Решение: влезает ли текст в доступную ширину. Публичная чистая функция — проверяется тестом.</summary>
    /// <param name="extentWidth">Настоящая ширина текста.</param>
    /// <param name="viewportWidth">Ширина видимой части.</param>
    public static bool Overflows(double extentWidth, double viewportWidth) =>
        extentWidth - viewportWidth > Slack;

    /// <summary>Предел ширины подсказки: длинный путь одной строкой уходил бы за экран.</summary>
    /// <remarks>Число произвольное — порядка трети окна: переносом гарантируется читаемость.</remarks>
    private const double MaxToolTipWidth = 320;

    /// <summary>Изменение текста заголовка приходит от привязки, а не событием ввода.</summary>
    private static readonly DependencyPropertyDescriptor TitleTextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock))!;

    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not (TextBox or TextBlock))
        {
            throw new InvalidOperationException(
                "Подсказка переполнения задаётся полю ввода TextBox или текстовому блоку TextBlock.");
        }

        var target = (FrameworkElement)element;

        // Отписка перед подпиской: повторное присваивание навесило бы вторые обработчики, и
        // подсказка переставлялась бы дважды.
        target.SizeChanged -= OnSizeChanged;
        TitleTextDescriptor.RemoveValueChanged(target, OnTitleTextChanged);
        if (target is TextBox oldBox)
        {
            oldBox.TextChanged -= OnTextChanged;
        }

        if (!(bool)e.NewValue)
        {
            target.ToolTip = null;

            return;
        }

        target.SizeChanged += OnSizeChanged;
        if (target is TextBox box)
        {
            box.TextChanged += OnTextChanged;
        }
        else
        {
            TitleTextDescriptor.AddValueChanged(target, OnTitleTextChanged);
        }

        Update(target);
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e) =>
        Update((FrameworkElement)sender);

    private static void OnTitleTextChanged(object? sender, EventArgs e) =>
        Update((FrameworkElement)sender!);

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        Update((FrameworkElement)sender);

    /// <summary>Пересчитывает подсказку после завершения текущего layout-прохода.</summary>
    private static void Update(FrameworkElement element) =>
        element.Dispatcher.BeginInvoke(RefreshToolTip, DispatcherPriority.Loaded, element);

    private static void RefreshToolTip(object? state)
    {
        // Отложенный пересчёт может доехать уже после выключения подсказки.
        if (state is not FrameworkElement element || !GetEnabled(element))
        {
            return;
        }

        switch (element)
        {
            case TextBox box:
                if (double.IsNaN(box.ExtentWidth) || double.IsNaN(box.ViewportWidth))
                {
                    return;
                }

                box.ToolTip = Overflows(box.ExtentWidth, box.ViewportWidth)
                    ? FullToolTip(box.Text)
                    : null;
                break;

            case TextBlock block:
                if (string.IsNullOrEmpty(block.Text) || double.IsNaN(block.ActualWidth) || block.ActualWidth <= 0)
                {
                    block.ToolTip = null;
                    return;
                }

                block.ToolTip = Overflows(MeasuredTextWidth(block), block.ActualWidth)
                    ? FullToolTip(block.Text)
                    : null;
                break;
        }
    }

    /// <summary>Ширина полного текста: пробный блок меряется тем же шрифтом и без ограничения ширины.</summary>
    /// <remarks>
    /// У обрезанного TextBlock DesiredSize равен доступной ширине, а не ширине текста, поэтому
    /// взять её из самого блока нельзя. Пробный блок повторяет свойства шрифта исходного —
    /// унаследованные кегль и семейство приходят готовыми значениями.
    /// </remarks>
    private static double MeasuredTextWidth(TextBlock block)
    {
        var probe = new TextBlock
        {
            Text = block.Text,
            FontFamily = block.FontFamily,
            FontSize = block.FontSize,
            FontStyle = block.FontStyle,
            FontWeight = block.FontWeight,
            FontStretch = block.FontStretch,
            TextWrapping = TextWrapping.NoWrap,
        };

        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return probe.DesiredSize.Width;
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
