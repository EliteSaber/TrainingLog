using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingLog.ViewModels;

namespace TrainingLog.Controls;

/// <summary>
/// Всплывающая подсказка окна: текст под указанным элементом, заданный срок жизни и закрытие
/// крестиком, нажатием снаружи или сама собой.
/// </summary>
/// <remarks>
/// Один контрол на все окна и на оба показа — отказ по вводу и примечание упражнения. Различаются
/// тон и срок, их задаёт модель (<see cref="HintState.Kind"/> и <see cref="HintState.Lifetime"/>);
/// вид, закрытие и правило положения (<see cref="PopupPlacement"/>) общие, иначе подсказки разъедутся
/// при первом же копировании.
///
/// Показывается элементом поверх содержимого окна, а не окном Popup: второе окно WPF отнимает у
/// главного окна наведение и курсор, и окно выглядит «мёртвым» до закрытия подсказки (ловушка 34).
/// </remarks>
public partial class StatusHint : UserControl
{
    /// <summary>
    /// Высота рисунка стрелки. Задана константой: рисунок растягивается <c>Stretch="Uniform"</c>,
    /// поэтому <c>Path</c>-данные трогать не нужно, пропорции вогнутых рёбер сохранятся сами.
    /// </summary>
    private const double ArrowHeight = 9;

    /// <summary>Ширина рисунка стрелки.</summary>
    private const double ArrowWidth = 18;

    /// <summary>
    /// Отступ стрелки от краёв блока, за который она не заходит. Это отступ от скруглённых углов
    /// (радиус 3), а не часть рисунка, поэтому при смене размера стрелки он не меняется.
    /// </summary>
    private const double ArrowInset = 4;

    /// <summary>
    /// Насколько стрелка заходит в поле: ровно настолько, чтобы вершина лежала на поле.
    /// </summary>
    /// <remarks>
    /// Вершина у стрелки вырожденная, и при полупрозрачной заливке её верхние пиксели выцветали в
    /// фоне — глаз читал это как просвет между полем и подсказкой. Заход убирает выцветание по
    /// построению. **Растёт вместе с высотой рисунка:** зона частичного покрытия у вершины
    /// занимает верхние пиксели стрелки, и у крупной 2 px уже не хватает.
    /// </remarks>
    private const double ArrowTipOverlap = 1;

    /// <summary>
    /// Зазор между полем и верхней гранью блока, из которого сложен вертикальный расклад.
    /// </summary>
    /// <remarks>
    /// Три слагаемых обязательны: расклад занимает ровно высоту рисунка, и складывается из захода в
    /// поле, зазора до блока и перекрытия блока. Единица в конце обязательна: обе фигуры
    /// полупрозрачны, и при точном стыке пиксель границы выходит светлее подложки.
    ///
    /// **«Сдвинуть подсказку со стрелкой» — это две разные константы:** <c>Gap</c> двигает блок, а
    /// вертикаль стрелки задаёт <see cref="ArrowTipOverlap"/>. Сдвиг только блока оставил бы стрелку
    /// на месте, сдвиг только стрелки разорвал бы её с блоком. Расчёт — в архиве проекта.
    /// </remarks>
    private const double Gap = ArrowHeight - ArrowTipOverlap - 1;

    /// <summary>
    /// Отступ подсказки от границы содержимого окна. Тот же, что <c>Margin="16"</c> у корневой
    /// сетки окна: подсказка примыкает к содержимому по той же линии, что и всё остальное в шапке.
    /// </summary>
    private const double Inset = 16;

    /// <summary>
    /// Уже этой ширины подсказка становится нечитаемой: ниже неё правило центрирования перестаёт
    /// уступать и возвращается к сдвигу по границе окна.
    /// </summary>
    private const double MinHintWidth = 120;

    /// <summary>Обратный отсчёт до закрытия подсказки.</summary>
    private readonly DispatcherTimer _timer;

    /// <summary>
    /// Где подсказка лежит в координатах окна: нужно, чтобы отличить нажатие по подсказке от
    /// нажатия снаружи.
    /// </summary>
    private Rect _placedBounds;

    /// <summary>Текст отказа.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusHint),
        new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>
    /// Вид показа: отказ по вводу или примечание упражнения. Задаёт тон текста и крестика —
    /// красным показывают только то, что пользователь сделал не так.
    /// </summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(HintState.HintKind),
        typeof(StatusHint),
        new PropertyMetadata(HintState.HintKind.Rejection, OnKindChanged));

    /// <summary>
    /// Сколько живёт подсказка. Задаёт модель: по умолчанию срок отказа, примечание приходит со
    /// своим. Применяется к таймеру и при каждом запуске отсчёта — показать одну и ту же подсказку
    /// дважды можно с разным сроком.
    /// </summary>
    public static readonly DependencyProperty LifetimeProperty = DependencyProperty.Register(
        nameof(Lifetime),
        typeof(TimeSpan),
        typeof(StatusHint),
        new PropertyMetadata(HintState.RejectionLifetime, OnLifetimeChanged));

    /// <summary>Показана ли подсказка. Привязывается односторонне: показом ведёт модель.</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(StatusHint),
        new PropertyMetadata(false, OnIsOpenChanged));

    /// <summary>
    /// Элемент, под которым показывается подсказка.
    /// </summary>
    /// <remarks>
    /// Задаётся окном: у полей внутри <c>DataTemplate</c> нет имени для ссылки в разметке, зато у
    /// события отказа по вводу есть само поле. Без якоря показывать нечего.
    ///
    /// **Привязкой на этом свойстве заниматься нельзя:** окно переставляет якорь с отказа на
    /// примечание и обратно, а локальное присваивание заменяет привязку навсегда (ловушка 11).
    /// </remarks>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(FrameworkElement),
        typeof(StatusHint),
        new PropertyMetadata(null, OnTargetChanged));

    /// <summary>Команда гашения показа: её зовут крестик, таймер и нажатие снаружи.</summary>
    public static readonly DependencyProperty DismissCommandProperty = DependencyProperty.Register(
        nameof(DismissCommand),
        typeof(ICommand),
        typeof(StatusHint));

    /// <summary>Команда, зовёмая после того, как подсказка исчезла: пора убрать текст из модели.</summary>
    public static readonly DependencyProperty ClosedCommandProperty = DependencyProperty.Register(
        nameof(ClosedCommand),
        typeof(ICommand),
        typeof(StatusHint));

    public StatusHint()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = Lifetime };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>Текст отказа.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Вид показа: отказ по вводу или примечание упражнения.</summary>
    public HintState.HintKind Kind
    {
        get => (HintState.HintKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Сколько живёт подсказка.</summary>
    public TimeSpan Lifetime
    {
        get => (TimeSpan)GetValue(LifetimeProperty);
        set => SetValue(LifetimeProperty, value);
    }

    /// <summary>Показана ли подсказка.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Элемент, под которым показывается подсказка.</summary>
    public FrameworkElement? Target
    {
        get => (FrameworkElement?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Команда гашения показа.</summary>
    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    /// <summary>Команда, зовёмая после исчезновения подсказки.</summary>
    public ICommand? ClosedCommand
    {
        get => (ICommand?)GetValue(ClosedCommandProperty);
        set => SetValue(ClosedCommandProperty, value);
    }

    /// <summary>
    /// Перезапускает отсчёт до закрытия. Зовётся окном на каждый отказ.
    /// </summary>
    /// <remarks>
    /// Отдельный шаг не избыточен: повторный отказ даёт <em>тот же самый</em> текст, поэтому
    /// уведомления не будет, и отсчёт, начатый на первом отказе, истёк бы на середине набора.
    ///
    /// Интервал ставится здесь, а не только в обработчике изменения срока: тот не срабатывает при
    /// первом показе — тогда таймер создан с интервалом по умолчанию.
    /// </remarks>
    public void RestartCountdown()
    {
        if (!IsOpen)
        {
            return;
        }

        _timer.Stop();
        _timer.Interval = Lifetime;
        _timer.Start();
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusHint hint)
        {
            hint.OnVisibilityChanged();
        }
    }

    /// <summary>
    /// Вид показа сменился, пока подсказка на экране: перекрасить её. Позицию и отсчёт не трогаем:
    /// ни размер, ни положение от тона не зависят.
    /// </summary>
    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusHint hint && hint.IsOpen)
        {
            hint.SyncHint();
        }
    }

    /// <summary>
    /// Срок жизни сменился, пока подсказка на экране: отсчёт идёт по старому, поэтому
    /// перезапускаем его по новому.
    /// </summary>
    private static void OnLifetimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusHint hint)
        {
            hint.RestartCountdown();
        }
    }

    /// <summary>
    /// Якорь сменился, пока подсказка на экране: переставить её под новый элемент. Без этого она
    /// осталась бы под прежним: показать два примечания с одинаковым текстом состояние не меняет,
    /// а переставляет подсказку только смена текста.
    /// </summary>
    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusHint hint && hint.IsOpen)
        {
            hint.Place();
        }
    }

    /// <summary>
    /// Текст сменился, пока подсказка на экране: показать новый, переставить — после переноса
    /// изменилась высота — и перезапустить отсчёт.
    /// </summary>
    /// <remarks>
    /// Отсчёт перезапускается здесь, а не только в окне: тексты отказов у полей разные, и без
    /// этого подсказка, показанная на весе, погасла бы через пару секунд после отказа в
    /// повторениях.
    /// </remarks>
    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusHint hint && hint.IsOpen)
        {
            hint.SyncHint();
            hint.Place();
            hint.RestartCountdown();
        }
    }

    /// <summary>
    /// Сбрасывает содержимое подсказки по текущим значениям.
    /// </summary>
    /// <remarks>
    /// Присваивания, а не привязки: см. ловушку 23 — молчаливые поломки разметки.
    /// </remarks>
    private void SyncHint()
    {
        HintText.Text = Text;
        HintButton.Command = DismissCommand;

        var accent = Accent(Kind);

        // Тон задаётся надписи и крестику присваиванием, а не привязкой к кисти ресурса: вид
        // показа сменился, а ресурс с тем же ключом остался бы прежним.
        HintText.Foreground = accent;
        HintCross.Fill = accent;

        var visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;

        // Collapsed, а не Hidden: скрытая подсказка не рисуется, но всё ещё ловит наведение и
        // нажатие. Свёрнутая не ловит ничего.
        HintBlock.Visibility = visibility;

        // Стрелка гасится вместе с блоком: иначе после закрытия она осталась бы висеть одна.
        HintArrow.Visibility = visibility;
    }

    /// <summary>
    /// Тон по виду показа: отказ — предупреждающим цветом, примечание — обычным цветом текста окна.
    /// </summary>
    /// <remarks>
    /// Цвет текста взят системной кистью: тот же, что у подписей в строке журнала, поэтому
    /// примечание читается как часть таблицы, а в тёмной теме не становится тёмным пятном.
    /// </remarks>
    private static SolidColorBrush Accent(HintState.HintKind kind) =>
        kind == HintState.HintKind.Note ? SystemColors.ControlTextBrush : Brushes.Firebrick;

    /// <summary>
    /// Показ включился — ставим подсказку, запускаем отсчёт и слежение за окном; выключился —
    /// убираем и разрешаем модели забыть текст.
    /// </summary>
    private void OnVisibilityChanged()
    {
        SyncHint();

        if (!IsOpen)
        {
            _timer.Stop();
            StopWatchingWindow();

            Invoke(ClosedCommand);

            return;
        }

        if (Target is null)
        {
            Dismiss();

            return;
        }

        WatchWindow();

        Place();
        RestartCountdown();
    }

    /// <summary>
    /// Ставит подсказку по правилу <see cref="PopupPlacement.Resolve"/>.
    /// </summary>
    /// <remarks>
    /// Пределы берутся от окна-владельца, а не от самого контрола: правило «не выходить за окно»
    /// именно про окно, и контрол о его размере не знает.
    ///
    /// Координаты цели берутся <c>TranslatePoint</c> относительно окна, а не обходом визуального
    /// дерева: у содержимого, завёрнутого в <c>ScrollContentPresenter</c>, визуальным родителем
    /// является не то, что кажется (ловушка 11).
    ///
    /// Якоря на экране больше нет — подсказка гаснет. Пока она висит, журнал может перечитаться,
    /// а строки дней пересоздаются вместе с кнопками примечаний; отсоединённый элемент координат
    /// не отдаёт, и подсказка уехала бы в угол на весь оставшийся срок.
    /// </remarks>
    private void Place()
    {
        if (Target is not { IsLoaded: true } target || Window.GetWindow(this) is not { } window || WindowBounds() is not { } bounds)
        {
            Dismiss();

            return;
        }

        var origin = target.TranslatePoint(new Point(0, 0), window);
        var anchor = new Rect(origin, new Size(target.ActualWidth, target.ActualHeight));

        // Предел высоты ставится до первого измерения: иначе меряется неограниченный текст, блок
        // оказывается длиннее окна, и правило сдвинет его вверх ровно на свой выход, то есть никуда.
        //
        // Предел достаётся содержимому, а не блоку: поля блока и так заняты, и передавать их
        // размером блока было бы двойным счётом. Сами поля читаются из разметки, чтобы число не
        // расходилось с ней.
        HintTextScroll.MaxHeight = PopupPlacement.ResolveMaxHeight(
            bounds,
            Gap,
            HintBlock.Padding.Top + HintBlock.Padding.Bottom);

        // Ширина задаётся явно, а не пределом: у предела фактический размер может разойтись с
        // измеренным, и блок выйдет шире, чем считала позиция, то есть за правый край окна. Наблюдалось:
        // посчитали по 326 px, нарисовали 705. Естественная ширина меряется без предела, иначе
        // перенос не сработал бы и мерился бы уже обрезанный результат.
        HintBlock.Width = double.NaN;
        HintBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = Math.Min(bounds.Width, PopupPlacement.ResolveWidth(anchor, bounds, HintBlock.DesiredSize.Width, MinHintWidth));

        HintBlock.Width = width;

        // Повторное измерение обязательно: позиция считается из размера, а без перемера DesiredSize
        // ещё держит естественную ширину.
        HintBlock.Measure(new Size(width, double.PositiveInfinity));

        var placement = PopupPlacement.Resolve(anchor, HintBlock.DesiredSize, bounds, Gap);

        // Правило возвращает угол в координатах окна, а Canvas позиционирует ребёнка от своего
        // начала — то есть от начала самого контрола в окне.
        var self = TranslatePoint(new Point(0, 0), window);

        Canvas.SetLeft(HintBlock, placement.X - self.X);
        Canvas.SetTop(HintBlock, placement.Y - self.Y);

        PlaceArrow(anchor, placement, self);

        // Прямоугольник «снаружи» продлён вверх на всю высоту стрелки: клик по самой стрелке не
        // должен гасить подсказку. Верхняя граница считается от верхней грани блока вверх, потому
        // что Resolve при сдвиге вверх отодвигает блок ровно на Gap.
        var arrowHeight = Gap + ArrowTipOverlap;

        _placedBounds = new Rect(
            placement.X,
            placement.Y - arrowHeight,
            HintBlock.DesiredSize.Width,
            HintBlock.DesiredSize.Height + arrowHeight);
    }

    /// <summary>
    /// Ставит стрелку у верхней грани блока, вершиной на нижнюю грань поля.
    /// </summary>
    /// <remarks>
    /// Расклад по вертикали: верх блока — в «низ поля + <see cref="Gap"/>», верх стрелки — на
    /// <see cref="Gap"/> выше него, то есть в «низ поля», а сдвигом
    /// <see cref="ArrowTipOverlap"/> поднимается ещё выше и заходит в поле. Низ рисунка уходит на
    /// 1 px в блок: обе границы закрыты перекрытием, и ни на одной нет шва двух полупрозрачных
    /// фигур.
    ///
    /// По горизонтали решение отдано правилу: стрелка указывает на центр поля, но внутрь блока, иначе
    /// у края окна указывала бы мимо. Блок у края окна может быть уже самой стрелки — она наезжает
    /// на угол, но за пределы не выходит: выход заметнее наезда.
    /// </remarks>
    private void PlaceArrow(Rect anchor, Point placement, Point self)
    {
        var block = new Rect(placement, HintBlock.DesiredSize);

        var left = PopupPlacement.ResolveArrowLeft(
            block,
            anchor.Left + (anchor.Width / 2),
            ArrowWidth,
            ArrowInset);

        Canvas.SetLeft(HintArrow, left - self.X);
        Canvas.SetTop(HintArrow, placement.Y - Gap - ArrowTipOverlap - self.Y);
    }

    /// <summary>
    /// Пределы, в которых подсказке положено помещаться: содержимое окна-владельца минус отступ.
    /// </summary>
    /// <remarks>
    /// **Пределы берутся у содержимого окна, а не у самого окна:** <c>Window.ActualWidth</c> —
    /// размер окна целиком, с заголовком и рамками, и по нему подсказка уезжала за нижнюю границу.
    /// Начало содержимого не в нуле — рамка и заголовок сдвигают его вниз, поэтому переносится
    /// <c>TranslatePoint</c>. Находка и расчёт — в архиве проекта.
    /// </remarks>
    /// <returns><c>null</c>, если окна или его содержимого ещё нет.</returns>
    private Rect? WindowBounds()
    {
        if (Window.GetWindow(this) is not { } window
            || window.Content is not FrameworkElement content)
        {
            return null;
        }

        var origin = content.TranslatePoint(new Point(0, 0), window);
        var bounds = new Rect(origin, new Size(content.ActualWidth, content.ActualHeight));

        return PopupPlacement.WorkArea(bounds, Inset);
    }

    /// <summary>
    /// Нажали снаружи подсказки — гасим её.
    /// </summary>
    /// <remarks>
    /// Слушается <em>превью</em> с <c>handledEventsToo</c> и без <c>e.Handled</c>: нажатие должно и
    /// дальше попасть в кнопку окна, а этим раньше занимался <c>StaysOpen="False"</c> у
    /// <c>Popup</c>, который попутно отнимал у окна наведение и курсор (ловушка 34).
    /// </remarks>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsOpen || Window.GetWindow(this) is not { } window)
        {
            return;
        }

        if (_placedBounds.Contains(e.GetPosition(window)))
        {
            return;
        }

        Dismiss();
    }

    /// <summary>
    /// Окно растянули или сузили, а подсказка на экране: её положение и предел ширины устарели.
    /// </summary>
    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsOpen)
        {
            Place();
        }
    }

    /// <summary>
    /// Окно закрылось, а подсказка была на экране: её больше некому закрывать, и состояние модели
    /// разошлось бы с тем, что видно.
    /// </summary>
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Dismiss();

        Invoke(ClosedCommand);
    }

    private void OnTimerTick(object? sender, EventArgs e) => Dismiss();

    private void WatchWindow()
    {
        if (Window.GetWindow(this) is not { } window)
        {
            return;
        }

        // Отписка перед подпиской: иначе повторное показывание навесило бы вторые обработчики, и
        // нажатие снаружи гасило бы подсказку дважды.
        StopWatchingWindow();

        window.SizeChanged += OnWindowSizeChanged;
        window.Closed += OnWindowClosed;
        window.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPreviewMouseLeftButtonDown), handledEventsToo: true);
    }

    private void StopWatchingWindow()
    {
        if (Window.GetWindow(this) is not { } window)
        {
            return;
        }

        window.SizeChanged -= OnWindowSizeChanged;
        window.Closed -= OnWindowClosed;
        window.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPreviewMouseLeftButtonDown));
    }

    private void Dismiss()
    {
        if (DismissCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
            return;
        }

        // Команда не задана: гасим показ сами. Запасной путь для контрола, подключённого без модели.
        IsOpen = false;
    }

    private static void Invoke(ICommand? command)
    {
        if (command is { } invocable && invocable.CanExecute(null))
        {
            invocable.Execute(null);
        }
    }
}
