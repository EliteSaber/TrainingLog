using System.Windows;
using System.Windows.Controls;

namespace TrainingLog.Controls;

/// <summary>
/// Зазор между содержимым и полосой прокрутки по вертикали. Повторяемое поведение:
/// достаточно указать <c>ScrollBarGutter.Left="2"</c> на любом <see cref="ScrollViewer"/>.
/// </summary>
/// <remarks>
/// Зазор появляется только когда полоса действительно есть, и исчезает вместе с ней, поэтому
/// у содержимого не остаётся пустого поля при коротком списке. Поведение владеет правой
/// компонентой <see cref="FrameworkElement.Margin"/> содержимого скролла: свой отступ справа
/// там не задавайте.
/// </remarks>
public static class ScrollBarGutter
{
    /// <summary>
    /// Ширина зазора слева от вертикальной полосы прокрутки.
    /// </summary>
    /// <remarks>
    /// <c>0</c> отключает поведение и убирает уже применённый зазор. Правая компонента
    /// <see cref="Margin"/> элемента <see cref="ScrollViewer.Content"/> принадлежит поведению,
    /// остальные не затрагиваются.
    /// </remarks>
    public static readonly DependencyProperty LeftProperty = DependencyProperty.RegisterAttached(
        "Left",
        typeof(double),
        typeof(ScrollBarGutter),
        new PropertyMetadata(0d, OnLeftChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof(GutterState),
        typeof(ScrollBarGutter),
        new PropertyMetadata(null));

    public static double GetLeft(DependencyObject element) => (double)element.GetValue(LeftProperty);

    public static void SetLeft(DependencyObject element, double value) => element.SetValue(LeftProperty, value);

    /// <summary>
    /// Вычисляет, какой зазор оставить слева от полосы прокрутки.
    /// </summary>
    /// <param name="configured">Зазор, заданный свойством <see cref="LeftProperty"/>.</param>
    /// <param name="scrollableHeight">
    /// Переполнение содержимого по вертикали — <see cref="ScrollViewer.ScrollableHeight"/>.
    /// Именно по этой величине WPF сам решает, показывать ли полосу, поэтому спрашиваем её, а не
    /// сравниваем <see cref="FrameworkElement.ActualHeight"/> с <see cref="ScrollViewer.ViewportHeight"/>:
    /// своё перевычисление переполнения с решением WPF расходится.
    /// </param>
    /// <returns>Зазор, либо <c>0</c>, если полосы прокрутки не будет.</returns>
    /// <remarks>
    /// При <see cref="ScrollViewer.VerticalScrollBarVisibility"/> равном
    /// <see cref="ScrollBarVisibility.Visible"/> полоса видна всегда, но переполнение может быть
    /// нулевым и зазор не применится. Для <see cref="ScrollBarVisibility.Auto"/> и
    /// <see cref="ScrollBarVisibility.Scroll"/> признак совпадает с видимостью полосы.
    /// </remarks>
    public static double ResolveGutter(double configured, double scrollableHeight)
    {
        if (configured <= 0 || scrollableHeight <= 0)
        {
            // Зазор не настроен либо прокручивать нечего.
            return 0;
        }

        return configured;
    }

    private static void OnLeftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer)
        {
            return;
        }

        Detach(viewer);

        if (e.NewValue is not double configured || configured <= 0)
        {
            // Иначе уже применённый зазор остался бы навсегда.
            ApplyGutter(viewer.Content as FrameworkElement, 0);
            return;
        }

        var state = new GutterState(viewer);

        viewer.SetValue(StateProperty, state);
        viewer.SizeChanged += state.OnViewerSizeChanged;

        // ScrollChanged приходит и при изменении ExtentHeight, то есть когда меняется переполнение.
        // Без него остаётся зазор на порядок событий: к моменту content.SizeChanged значение
        // ScrollableHeight у скролла может быть ещё не пересчитано.
        viewer.ScrollChanged += state.OnScrollChanged;

        // В разметке свойство может примениться раньше, чем присвоится Content: пока
        // содержимого нет, подписываться не на что, и зазор не заработал бы никогда.
        if (viewer.Content is FrameworkElement content)
        {
            state.AttachContent(content);
        }
        else
        {
            viewer.Loaded += state.OnViewerLoaded;
        }
    }

    private static void Detach(ScrollViewer viewer)
    {
        if (viewer.GetValue(StateProperty) is not GutterState state)
        {
            return;
        }

        viewer.SizeChanged -= state.OnViewerSizeChanged;
        viewer.ScrollChanged -= state.OnScrollChanged;
        viewer.Loaded -= state.OnViewerLoaded;
        state.DetachContent();

        viewer.ClearValue(StateProperty);
    }

    private static void ApplyGutter(FrameworkElement? content, double gutter)
    {
        if (content is null)
        {
            return;
        }

        var margin = content.Margin;

        // Правая граница не влияет на высоту содержимого, поэтому присваивание не вызывает
        // повторного пересчёта по цепочке SizeChanged.
        if (Math.Abs(margin.Right - gutter) < 0.01)
        {
            return;
        }

        content.Margin = new Thickness(margin.Left, margin.Top, gutter, margin.Bottom);
    }

    /// <summary>
    /// Состояние одного скролла. Держит прямые ссылки на скролл и на подписанное содержимое,
    /// поэтому обработчикам не нужно искать скролл по визуальному дереву: у содержимого
    /// <see cref="ScrollViewer"/> визуальным родителем является <c>ScrollContentPresenter</c>,
    /// а не сам скролл, и такой поиск всегда давал <c>null</c>.
    /// </summary>
    private sealed class GutterState(ScrollViewer viewer)
    {
        private FrameworkElement? _content;

        public void OnViewerLoaded(object sender, RoutedEventArgs e)
        {
            viewer.Loaded -= OnViewerLoaded;

            if (viewer.Content is FrameworkElement content)
            {
                AttachContent(content);
            }
        }

        public void OnViewerSizeChanged(object sender, SizeChangedEventArgs e) => Update();

        public void OnScrollChanged(object sender, ScrollChangedEventArgs e) => Update();

        public void OnContentSizeChanged(object sender, SizeChangedEventArgs e) => Update();

        public void AttachContent(FrameworkElement content)
        {
            DetachContent();

            _content = content;
            content.SizeChanged += OnContentSizeChanged;

            Update();
        }

        public void DetachContent()
        {
            if (_content is not null)
            {
                _content.SizeChanged -= OnContentSizeChanged;
                _content = null;
            }
        }

        private void Update()
        {
            ApplyGutter(_content, ResolveGutter(GetLeft(viewer), viewer.ScrollableHeight));
        }
    }
}