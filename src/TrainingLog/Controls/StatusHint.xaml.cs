using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingLog.ViewModels;

namespace TrainingLog.Controls;

/// <summary>
/// Всплывающая подсказка окна: показывает текст под указанным элементом, живёт заданное время и
/// закрывается крестиком, нажатием снаружи или сама.
/// </summary>
/// <remarks>
/// Показывается ею и отказ по вводу, и примечание упражнения — различаются тон и срок жизни, их
/// задаёт модель (<see cref="HintState.Kind"/> и <see cref="HintState.Lifetime"/>), а рисует их
/// контрол. Вид, поведение при закрытии и правило позиционирования общие, иначе подсказки
/// разъедутся при первом же копировании.
/// </remarks>
/// <remarks>
/// Один контрол на все окна: вид, срок жизни и поведение при закрытии должны быть одинаковыми, и
/// расходятся они дальше только потому, что оказалось скопировано. Правило позиционирования — в
/// <see cref="PopupPlacement"/> и тоже одно; здесь оно лишь применяется.
///
/// Показывается элементом поверх содержимого окна, а не отдельным окном <c>Popup</c>: второе
/// окно WPF в этом приложении отнимает у главного окна наведение и курсор, причём нажатия
/// продолжают работать, и окно выглядит «мёртвым» до самого закрытия подсказки.
/// </remarks>
public partial class StatusHint : UserControl
{
    /// <summary>
    /// Высота рисунка стрелки, указывающей на поле. Рисунок 18×9.
    /// </summary>
    /// <remarks>
    /// Рисунок задан кривыми и растягивается <c>Stretch="Uniform"</c>, поэтому размер достигается
    /// сменой этой константы и пары <c>Width</c>/<c>Height</c> в разметке, а <c>Path</c>-данные
    /// трогать не нужно: пропорции вогнутых рёбер сохранятся сами.
    /// </remarks>
    private const double ArrowHeight = 9;

    /// <summary>Ширина рисунка стрелки.</summary>
    private const double ArrowWidth = 18;

    /// <summary>
    /// Отступ стрелки от краёв блока, за который она не заходит.
    /// </summary>
    /// <remarks>
    /// Это отступ от скруглённых углов блока (радиус 3), а не часть рисунка, поэтому при смене
    /// размера стрелки он не меняется.
    /// </remarks>
    private const double ArrowInset = 4;

    /// <summary>
    /// Насколько стрелка заходит в поле — ровно настолько, чтобы её вершина лежала на поле, а не
    /// на фоне окна.
    /// </summary>
    /// <remarks>
    /// Не «на глаз», а против конкретного дефекта. Вершина у стрелки вырожденная: две дуги
    /// сходятся в точку, и верхний ряд пикселей набирает частичное покрытие. Заливка вдобавок
    /// полупрозрачна (альфа 0.95), и сложившись, вершина выцветала в фоне — глаз читал это как
    /// просвет между полем и подсказкой, хотя геометрически остриё стояло ровно на нижней грани
    /// поля. Вынос вершины на непрозрачное поле убирает выцветание по построению.
    ///
    /// **Растёт вместе с высотой рисунка, и это обязательно.** Зона частичного покрытия у вершины
    /// занимает верхние пиксели стрелки, поэтому у крупной стрелки она шире, и захода в 2 px при
    /// высоте 6 ей уже не хватало — выцветание вернулось бы, то есть дефект, ради которого всё
    /// затевалось.
    ///
    /// Здесь 1 px при высоте 9: ровно столько нужно, чтобы вершина лежала на поле, и меньше уже некуда — если
    /// выцветание вернётся, увеличивать надо именно эту величину, а высоту рисунка трогать не
    /// нужно: поднимать стрелку целиком уводит её с блока.
    /// </remarks>
    private const double ArrowTipOverlap = 1;

    /// <summary>
    /// Зазор между полем и верхней гранью блока: раньше он держал блок на расстоянии от поля,
    /// теперь эту полосу занимает стрелка.
    /// </summary>
    /// <remarks>
    /// Формула из трёх слагаемых, а не «высота минус единица», и в этом весь смысл: вертикальный
    /// расклад занимает ровно высоту рисунка, и складывается он из трёх частей — захода в поле,
    /// зазора до блока и перекрытия блока. Любая из трёх величин обязана быть здесь видна. Здесь
    /// 9 − 1 − 1 = 7: блок стоит на 7 px ниже поля, стрелка заходит в поле на 1 px, в блок на 1.
    ///
    /// **Без члена про заход в поле расклад не сходился.** При формуле «высота минус 1» и заходе
    /// 2 px вёрстка дала просвет в 1 px у основания стрелки: 2 px, взятые на заход в поле,
    /// вычитались из перекрытия с блоком, а не добавлялись к нему. Нужная сумма была
    /// 2 + 5 + 1 = 8 px при высоте 6.
    ///
    /// Единица в конце — перекрытие с блоком. Оно не «на глаз», а обязательное: обе фигуры
    /// полупрозрачны, и при точном стыке пиксель границы набирает покрытие 0.5 + 0.5 и выходит
    /// светлее подложки — тонкая светлая линия поперёк стрелки. При перекрытии шва нет по
    /// построению, а лишняя полоска чуть темнее: при альфе 0.95 двойное наложение даёт 0.9975 против
    /// 0.95, то есть около 5 % от 5 % и глазом не берётся. Чем плотнее подложка, тем слабее сам
    /// артефакт: он и есть произведение двух недозаливок.
    ///
    /// Подкрутить <c>Gap</c>, чтобы прилепить стрелку, бесполезно: <c>Resolve</c> ставит верх
    /// блока в «низ поля + Gap», а стрелка кладётся на <c>Gap</c> выше него, то есть величина
    /// сокращается и с блоком уедет и она сама. **Поэтому «сдвинуть подсказку со стрелкой» — это
    /// две разные константы: <c>Gap</c> двигает блок, а вертикаль стрелки задаёт
    /// <see cref="ArrowTipOverlap"/>.** Правка на 1 px вниз — это <c>Gap</c> плюс единица и
    /// <c>ArrowTipOverlap</c> минус единица; сдвиг только блока оставил бы стрелку на месте, а
    /// сдвиг только стрелки разорвал бы её с блоком.
    /// </remarks>
    private const double Gap = ArrowHeight - ArrowTipOverlap - 1;

    /// <summary>
    /// Отступ подсказки от границы содержимого окна. Тот же, что <c>Margin="16"</c> у корневой
    /// сетки окна: подсказка примыкает к содержимому по той же линии, что и всё остальное в шапке.
    /// </summary>
    private const double Inset = 16;

    /// <summary>
    /// Уже этой ширины подсказка становится нечитаемой: ниже неё правило центрирования
    /// перестаёт уступать и возвращается к сдвигу по границе окна.
    /// </summary>
    private const double MinHintWidth = 120;

    /// <summary>
    /// Обратный отсчёт до закрытия подсказки.
    /// </summary>
    private readonly DispatcherTimer _timer;

    /// <summary>
    /// Где подсказка лежит в координатах окна: нужно, чтобы отличить нажатие по подсказке от
    /// нажатия снаружи.
    /// </summary>
    private Rect _placedBounds;

    /// <summary>
    /// Текст отказа.
    /// </summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusHint),
        new PropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>
    /// Вид показа: отказ по вводу или примечание упражнения.
    /// </summary>
    /// <remarks>
    /// Задаёт тон текста и крестика. Примечание — спокойное сообщение, а не отказ, поэтому оно
    /// рисуется обычным цветом текста окна; красным показывают только то, что пользователь
    /// сделал не так.
    /// </remarks>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(HintState.HintKind),
        typeof(StatusHint),
        new PropertyMetadata(HintState.HintKind.Rejection, OnKindChanged));

    /// <summary>
    /// Сколько живёт подсказка, пока её не закрыли крестиком или нажатием снаружи.
    /// </summary>
    /// <remarks>
    /// Значение по умолчанию — срок отказа по вводу; примечание приходит со своим и держится
    /// подольше. Задаёт модель: это её решение, а не свойство окна (см. <see cref="HintState"/>).
    /// Применяется к таймеру и здесь, и при каждом запуске отсчёта: показать одну и ту же
    /// подсказку дважды можно с разным сроком, и таймер обязан считать по последнему.
    /// </remarks>
    public static readonly DependencyProperty LifetimeProperty = DependencyProperty.Register(
        nameof(Lifetime),
        typeof(TimeSpan),
        typeof(StatusHint),
        new PropertyMetadata(HintState.RejectionLifetime, OnLifetimeChanged));

    /// <summary>
    /// Показана ли подсказка. Привязывается односторонне: показом ведёт модель, а окно
    /// подсказки само модель не трогает.
    /// </summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(StatusHint),
        new PropertyMetadata(false, OnIsOpenChanged));

    /// <summary>
    /// Элемент, под которым показывается подсказка.
    /// </summary>
    /// <remarks>
    /// Задаётся окном: у полей внутри <c>DataTemplate</c> нет имени, на которое можно сослаться
    /// в разметке, зато у события отказа по вводу есть <c>Source</c> — то есть сам отказавшее
    /// поле. Без якоря показывать нечего, и подсказка не показывается вовсе.
    ///
    /// **Привязкой на этом свойстве заниматься нельзя:** окно переставляет якорь с отказа на
    /// примечание и обратно, а локальное присваивание заменяет привязку навсегда — ровно то,
    /// из-за чего страдало поведение <c>ScrollBarGutter</c> (ловушка 11).
    /// </remarks>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(FrameworkElement),
        typeof(StatusHint),
        new PropertyMetadata(null, OnTargetChanged));

    /// <summary>
    /// Команда гашения показа: её зовут крестик, таймер и нажатие снаружи.
    /// </summary>
    public static readonly DependencyProperty DismissCommandProperty = DependencyProperty.Register(
        nameof(DismissCommand),
        typeof(ICommand),
        typeof(StatusHint));

    /// <summary>
    /// Команда, зовёмая после того, как подсказка исчезла: пора убрать текст из модели.
    /// </summary>
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
    /// Отдельный шаг не избыточен: повторный отказ даёт <em>то же самый</em> текст, поэтому
    /// уведомления об изменении состояния не будет, и отсчёт, начатый на первом отказе, истёк бы
    /// на середине набора.
    ///
    /// Интервал ставится здесь, а не только в обработчике изменения срока: тот успевает сработать
    /// при смене срока у уже висящей подсказки, но не при первом показе — тогда таймер создан с
    /// интервалом по умолчанию. Число одно и то же в обоих местах задаёт модель.
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
    /// Срок жизни сменился, пока подсказка на экране: отсчёт продолжает идти по старому, поэтому
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
    /// Якорь сменился, пока подсказка на экране: переставить её под новый элемент.
    /// </summary>
    /// <remarks>
    /// Без этого подсказка осталась бы под прежним якорем: показать два примечания с одинаковым
    /// текстом состояние не меняет, а переставляет подсказку только смена текста.
    /// </remarks>
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
    /// Отсчёт перезапускается здесь, а не только в окне: текст отказа у полей разный (вес
    /// пропускает разделители, повторения нет), и без этого подсказка, показанная на весе,
    /// погасла бы через пару секунд после отказа в повторениях.
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
    /// Присваивания, а не привязки: см. ловушку 23 — молчаливые поломки разметки. Здесь всё видно
    /// и перечитывается целиком.
    /// </remarks>
    private void SyncHint()
    {
        HintText.Text = Text;
        HintButton.Command = DismissCommand;

        var accent = Accent(Kind);

        // Тон задаётся и надписи, и крестику: они означают одно и то же, и разъезжаться не
        // должны. Присваиванием, а не привязкой к кисти ресурса: вид показа сменился, а ресурс
        // с тем же ключом остался бы прежним.
        HintText.Foreground = accent;
        HintCross.Fill = accent;

        var visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;

        // Collapsed, а не Hidden: скрытая подсказка не рисуется, но всё ещё ловит наведение и
        // нажатие, то есть продолжала бы мешать. Свёрнутая не ловит ничего.
        HintBlock.Visibility = visibility;

        // Стрелка гасится вместе с блоком, а не отдельно по событию показа: иначе после закрытия
        // она осталась бы висеть одна, без подсказки, которую она указывает.
        HintArrow.Visibility = visibility;
    }

    /// <summary>
    /// Тон подсказки по виду показа: отказ — предупреждающим цветом, примечание — обычным цветом
    /// текста окна.
    /// </summary>
    /// <remarks>
    /// Цвет отказа взят напрямую, а цвет текста — системной кистью: тот же, что у подписей
    /// «Вес» и «Повторения» в строке журнала, поэтому примечание читается как часть таблицы, а
    /// в тёмной теме не становится тёмным пятном на светлой подложке.
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
    /// Размер считается здесь же и вручную, и это законно: содержимое лежит в собственном дереве
    /// окна, а не в отдельном окне, чей размер задаёт чужой проход разметки. При превышении
    /// предела текст переносится по словам, а не выходит за границу.
    ///
    /// Координаты цели берутся <c>TranslatePoint</c> относительно окна, а не обходом визуального
    /// дерева: у содержимого, завёрнутого в <c>ScrollContentPresenter</c>, визуальным родителем
    /// является не то, что кажется (ловушка 11).
    ///
    /// Кроме ширины считается и предел высоты: правило сдвигает блок вверх ровно на свой выход,
    /// а блок длиннее окна сдвинуть некуда, и текст уходит за нижний край. Предел ставится до
    /// измерения, потому что иначе меряется неограниченный текст (см.
    /// <see cref="PopupPlacement.ResolveMaxHeight"/>).
    ///
    /// Якоря на экране больше нет — подсказка гаснет. Пока подсказка висит, журнал может
    /// перечитаться (правка дня, «Применить»), а строки дней пересоздаются вместе с кнопками
    /// примечаний. Отсоединённый элемент координат не отдаёт: <c>TranslatePoint</c> вернул бы
    /// точку отсчёта, и подсказка уехала бы в угол окна на весь оставшийся срок.
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

        // Предел высоты ставится до первого измерения: иначе меряется неограниченный текст, и
        // блок оказывается длиннее окна — правило положения сдвинет его вверх ровно на свой
        // выход, то есть никуда, и низ уйдёт за нижнюю границу (ловушка 35 по вертикали).
        //
        // Предел достаётся содержимому, а не блоку: поля блока и так заняты, и передавать их
        // размером блока было бы двойным счётом. Сами поля читаются из разметки, чтобы число не
        // расходилось с ней.
        HintTextScroll.MaxHeight = PopupPlacement.ResolveMaxHeight(
            bounds,
            Gap,
            HintBlock.Padding.Top + HintBlock.Padding.Bottom);

        // Ширина задаётся явно, а не пределом — и это не stylistic, а требование: у предела
        // фактический размер может разойтись с измеренным (текст или предел успевают измениться
        // после расчёта, а рисует блок следующий проход разметки), и тогда блок выходит шире, чем
        // позиция считала, — то есть заезжает за правый край окна. Наблюдалось: посчитали по
        // 326 px, нарисовали 705. Явная ширина переживает оба изменения: текст переносится по
        // словам, а нарисованный центр блока всегда равен половине заданной ширины, то есть
        // центру поля.
        //
        // Естественная ширина меряется без предела, иначе перенос не сработал бы и мерился бы уже
        // обрезанный результат.
        HintBlock.Width = double.NaN;
        HintBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = Math.Min(bounds.Width, PopupPlacement.ResolveWidth(anchor, bounds, HintBlock.DesiredSize.Width, MinHintWidth));

        HintBlock.Width = width;

        // Повторное измерение обязательно: позиция считается из размера, а без перемера
        // DesiredSize ещё держит естественную ширину.
        HintBlock.Measure(new Size(width, double.PositiveInfinity));

        var placement = PopupPlacement.Resolve(anchor, HintBlock.DesiredSize, bounds, Gap);

        // Правило возвращает угол в координатах окна, а Canvas позиционирует ребёнка от своего
        // начала — то есть от начала самого контрола в окне.
        var self = TranslatePoint(new Point(0, 0), window);

        Canvas.SetLeft(HintBlock, placement.X - self.X);
        Canvas.SetTop(HintBlock, placement.Y - self.Y);

        PlaceArrow(anchor, placement, self);

        // Прямоугольник «снаружи» продлён вверх на всю высоту стрелки, включая заход в поле:
        // клик по самой стрелке не должен гасить подсказку, иначе нажатие работало бы как
        // нажатие мимо. Верхняя граница считается от верхней грани блока вверх, потому что
        // Resolve при сдвиге вверх отодвигает блок ровно на Gap.
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
    /// По вертикали расклад такая: верх блока — в «низ поля + <see cref="Gap"/>», верх стрелки —
    /// на <see cref="Gap"/> выше него, то есть в «низ поля», а сдвигом
    /// <see cref="ArrowTipOverlap"/> поднимается ещё выше и заходит в поле. Низ рисунка при этом
    /// уходит на 1 px в блок: обе границы закрыты перекрытием, и ни на одной из них нет шва,
    /// который давали бы две полупрозрачные фигуры в стык.
    ///
    /// По горизонтали решение отдано правилу: стрелка указывает на центр поля, но внутрь блока,
    /// иначе у края окна она указывала бы мимо. И отдельно — блок у края окна может быть уже
    /// самой стрелки, и тогда она наезжает на угол, но за пределы не выходит: выход заметнее
    /// наезда.
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
    /// Пределы берутся у <b>содержимого</b> окна, а не у самого окна, и на этом уже была ошибка:
    /// <c>Window.ActualHeight</c> — размер окна целиком, с заголовком и рамками, а
    /// <see cref="PopupPlacement.Resolve"/> прижимает нижнюю грань блока к нижней границе
    /// пределов. По размеру окна это уводило подсказку на заголовок и рамки, то есть за пределы
    /// окна: на коротком отказе по вводу не видно (он висит под полем и никогда не сдвигается
    /// вверх), а на длинном примечании упражнения видно сразу — блок прижимается к низу и уезжает
    /// под нижнюю границу.
    ///
    /// Содержимое и есть та область, в которой подсказка нарисована, и его начало приходится
    /// переносить в координаты окна: рамка и заголовок сдвигают его вниз.
    ///
    /// У обоих окон приложения <c>Content</c> — корневая сетка, так что иначе и не бывает; на
    /// случай, когда содержимого нет, правило то же, что при отсутствии окна, — возвращается
    /// <c>null</c>, и <see cref="Place"/> гасит подсказку.
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
    /// Слушается <em>превью</em> с <c>handledEventsToo</c> и без <c>e.Handled</c>: нажатие должно
    /// и дальше попасть в кнопку окна, а раньше этим занимался <c>StaysOpen="False"</c> у
    /// <c>Popup</c>, который попутно отнимал у окна наведение и курсор (ловушка 34). Нажатие по
    /// самой подсказке игнорируется: им занимается крестик.
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
    /// Окно растянули или сузили, а подсказка на экране: её положение считается от границ окна
    /// и устарело, как и предел ширины.
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

        // Отписка перед подпиской: иначе повторное показывание навесило бы вторые обработчики,
        // и нажатие снаружи гасило бы подсказку дважды.
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

        // Команда не задана: гасим показ сами. Это запасной путь для контрола, подключённого без
        // модели, и привязки здесь нет, поэтому рассинхронизации быть не с чем.
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