using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TrainingLog.Controls;

/// <summary>
/// Фильтр символов в поле ввода. Повторяемое поведение: достаточно указать
/// <c>AllowedCharsInput.Allowed="Digits"</c> на нужном <see cref="TextBox"/>.
/// </summary>
/// <remarks>
/// Перехватываются три входа, потому что каждому соответствует своя дырка. Набор с клавиатуры
/// ловится в <see cref="TextBox.PreviewTextInput"/>, но вставка из буфера мимо него проходит
/// всегда, а пробел при активном методе ввода до <c>PreviewTextInput</c> может не дойти. Одного
/// перехвата клавиш поэтому недостаточно — см. ловушку 34.
///
/// <c>PreviewKeyDown</c> глотает только пробел и намеренно не трогает Enter, Backspace и
/// стрелки: Enter в окне дня подтверждает сохранение, а без Backspace и стрелок в поле
/// нельзя было бы ни править текст, ни двигать каретку.
///
/// Поведение ловит только пользовательский ввод. Программное присваивание <c>Text</c> и
/// разбор загруженного из базы проходят мимо него — ровно как <c>MaxLength</c> у правила
/// наименования, который проверяет лишь то, что набирает пользователь.
/// </remarks>
public static class AllowedCharsInput
{
    /// <summary>
    /// Набор символов, который принимает поле.
    /// </summary>
    /// <remarks>
    /// <see cref="AllowedChars.None"/> отключает поведение и снимает уже подвешенные
    /// подписки: фильтр, оставшийся после переключения набора, отбрасывал бы символы
    /// по старому правилу.
    /// </remarks>
    public static readonly DependencyProperty AllowedProperty = DependencyProperty.RegisterAttached(
        "Allowed",
        typeof(AllowedChars),
        typeof(AllowedCharsInput),
        new PropertyMetadata(AllowedChars.None, OnAllowedChanged));

    /// <summary>
    /// Ввод отвергнут: в поле попытались ввести или вставить недопустимое. Событие всплывает
    /// от поля до окна, поэтому окно читает его одним обработчиком, а поля внутри
    /// <c>DataTemplate</c> не нужно перечислять.
    /// </summary>
    public static readonly RoutedEvent RejectedEvent = EventManager.RegisterRoutedEvent(
        "Rejected",
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(RejectedEventArgs))
        .AddOwner(typeof(AllowedCharsInput));

    /// <summary>
    /// Ввод принят: в поле попал допустимый символ. Событие-спутник <see cref="RejectedEvent"/>,
    /// всплывает так же и нужно для одной вещи — погасить висящее объяснение отказа.
    /// </summary>
    /// <remarks>
    /// Отдельное событие, а не проверка показа внутри поведения: поведение ничего не знает о
    /// подсказке, знает о ней окно. И не «в текст изменилось», потому что <c>TextChanged</c>
    /// срабатывает ещё и на программном заполнении полей (смена упражнения, инициализация окна),
    /// и подсказка гасла бы без всякой причины.
    ///
    /// Поднимается только на вводе, который действительно что-то добавляет: цифра или разделитель
    /// с клавиатуры, принятая вставка. Backspace и стрелки сюда не ведут — отказанный символ в
    /// поле не попадает, стирать нечего, а <c>PreviewKeyDown</c> их и не трогает.
    /// </remarks>
    public static readonly RoutedEvent AcceptedEvent = EventManager.RegisterRoutedEvent(
        "Accepted",
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(AcceptedEventArgs))
        .AddOwner(typeof(AllowedCharsInput));

    public static AllowedChars GetAllowed(DependencyObject element) => (AllowedChars)element.GetValue(AllowedProperty);

    public static void SetAllowed(DependencyObject element, AllowedChars value) => element.SetValue(AllowedProperty, value);

    /// <summary>
    /// Подписывает окно на отказ в любом из его полей.
    /// </summary>
    /// <remarks>
    /// Подписка идёт на сам элемент, а не на каждое поле: событие всплывает, поэтому
    /// достаточно одного обработчика на окно — особенно когда поля лежат внутри
    /// <c>DataTemplate</c> и появляются вместе с составом упражнений.
    /// </remarks>
    public static void AddRejectedHandler(UIElement element, RoutedEventHandler handler) =>
        element.AddHandler(RejectedEvent, handler);

    /// <summary>Снимает подписку, подвешенную <see cref="AddRejectedHandler"/>.</summary>
    public static void RemoveRejectedHandler(UIElement element, RoutedEventHandler handler) =>
        element.RemoveHandler(RejectedEvent, handler);

    /// <summary>Подписывает окно на принятый ввод в любом из его полей.</summary>
    /// <remarks>
    /// Смысл подписки на окно тот же, что и у отказа: событие всплывает, а полей может быть
    /// сколько угодно и появляются они вместе с составом упражнения.
    /// </remarks>
    public static void AddAcceptedHandler(UIElement element, RoutedEventHandler handler) =>
        element.AddHandler(AcceptedEvent, handler);

    /// <summary>Снимает подписку, подвешенную <see cref="AddAcceptedHandler"/>.</summary>
    public static void RemoveAcceptedHandler(UIElement element, RoutedEventHandler handler) =>
        element.RemoveHandler(AcceptedEvent, handler);

    /// <summary>
    /// Разрешён ли символ в поле.
    /// </summary>
    /// <param name="c">Проверяемый символ.</param>
    /// <param name="allowed">Набор символов поля.</param>
    /// <returns><c>true</c>, если символ можно ввести.</returns>
    /// <remarks>
    /// Цифры сравнением с <c>'0'</c> и <c>'9'</c>, а не <see cref="char.IsDigit(char)"/>:
    /// последняя считает цифрами и полноширинные символы восточноазиатских раскладок, а
    /// <c>int.TryParse</c> их не разбирает — символ прошёл бы в поле и погасил сохранение.
    ///
    /// При <see cref="AllowedChars.None"/> разрешено всё: набор не задан, поведение не
    /// подключено и проверять нечего.
    /// </remarks>
    public static bool IsAllowed(char c, AllowedChars allowed) => allowed switch
    {
        AllowedChars.Digits => IsDigit(c),
        AllowedChars.DigitsWithSeparators => IsDigit(c) || c is '.' or ',',
        _ => true,
    };

    /// <summary>
    /// Разрешён ли весь набранный текст.
    /// </summary>
    /// <param name="text">Текст из <see cref="TextCompositionEventArgs.Text"/>.</param>
    /// <param name="allowed">Набор символов поля.</param>
    /// <returns><c>true</c>, если можно ввести текст целиком.</returns>
    /// <remarks>
    /// Проверяется строка, а не символ: метод ввода присылает в <c>PreviewTextInput</c>
    /// сразу несколько символов, и решение принимается по всему куску.
    /// </remarks>
    public static bool IsTextAllowed(string text, AllowedChars allowed) => text.All(c => IsAllowed(c, allowed));

    /// <summary>
    /// Что вставить из буфера: первая непустая строка без краёв либо отказ.
    /// </summary>
    /// <param name="pasted">Содержимое буфера.</param>
    /// <param name="allowed">Набор символов поля.</param>
    /// <returns>
    /// Текст для вставки, либо <c>null</c>, если вставлять нечего либо в первой строке есть
    /// недопустимый символ.
    /// </returns>
    /// <remarks>
    /// Из буфера прилетает что угодно: скопированная ячейка из таблицы — это «60,5\tкг»,
    /// а столбец целиком — «60\n70\n80». Берётся первая непустая строка и с неё снимаются
    /// края, потому что копирование числа из одного окна в другое обычно приносит с собой
    /// перевод строки. Остальные строки отбрасываются молча: одна ячейка поля принимает одно
    /// значение, и разбирать таблицу ради одного числа не нужно.
    ///
    /// Отказ, а не вырезание символов: из «60 10» вырезание дало бы «6010», то есть вес,
    /// которого в буфере не было, и это хуже, чем ничего не вставить.
    /// </remarks>
    public static string? TakePaste(string? pasted, AllowedChars allowed)
    {
        if (string.IsNullOrEmpty(pasted))
        {
            return null;
        }

        // Строки режутся по \n, а \r убирается обрезкой краёв: так разбираются и переводы
        // строк Windows, и одиночный возврат каретки.
        foreach (var line in pasted.Split('\n'))
        {
            var candidate = line.Trim();

            if (candidate.Length == 0)
            {
                continue;
            }

            return IsTextAllowed(candidate, allowed) ? candidate : null;
        }

        return null;
    }

    /// <summary>
    /// Объяснение отказа для показа пользователю.
    /// </summary>
    /// <param name="allowed">Набор символов поля.</param>
    /// <returns>Текст сообщения, либо пустая строка при <see cref="AllowedChars.None"/>.</returns>
    /// <remarks>
    /// Формулировка живёт здесь, а не в окне: правило одно на все поля, и разные окна
    /// показывают одно и то же сообщение.
    /// </remarks>
    public static string RejectionMessage(AllowedChars allowed) => allowed switch
    {
        AllowedChars.Digits => "В поле допустимы только цифры.",
        AllowedChars.DigitsWithSeparators => "В поле допустимы только цифры, точка и запятая.",
        _ => string.Empty,
    };

    /// <summary>
    /// Цифра ли это в смысле набора поля: только <c>'0'</c>–<c>'9'</c>.
    /// </summary>
    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static void OnAllowedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
        {
            return;
        }

        // Отписка перед подпиской обязательна: повторное присваивание набора иначе оставило
        // бы на поле два обработчика, и отказ приходил бы дважды.
        //
        // Через события элемента, а не через AddHandler/RemoveHandler: их параметр имеет тип
        // Delegate, и группа методов конвертируется в него по своему естественному типу, то
        // есть в Action<object, TextCompositionEventArgs>, а не в обработчик, зарегистрированный
        // за событием. WPF такой делегат отвергает — см. ловушку 14.
        box.PreviewTextInput -= OnPreviewTextInput;
        box.PreviewKeyDown -= OnPreviewKeyDown;
        DataObject.RemovePastingHandler(box, OnPasting);

        if (e.NewValue is not AllowedChars allowed || allowed == AllowedChars.None)
        {
            return;
        }

        box.PreviewTextInput += OnPreviewTextInput;
        box.PreviewKeyDown += OnPreviewKeyDown;

        // Вставка подписывается на элемент статическими методами DataObject: своего свойства
        // с этим событием у TextBox нет, а получить его иначе нельзя.
        DataObject.AddPastingHandler(box, OnPasting);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        var allowed = GetAllowed(box);

        if (IsTextAllowed(e.Text, allowed))
        {
            Accept(box);

            return;
        }

        e.Handled = true;

        Reject(box, allowed);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || e.Key != Key.Space)
        {
            return;
        }

        e.Handled = true;

        Reject(box, GetAllowed(box));
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        var allowed = GetAllowed(box);

        var pasted = e.DataObject.GetDataPresent(DataFormats.UnicodeText)
            ? e.DataObject.GetData(DataFormats.UnicodeText) as string
            : null;

        var accepted = TakePaste(pasted, allowed);

        if (accepted is null)
        {
            e.CancelCommand();

            Reject(box, allowed);

            return;
        }

        // Объект вставки переписывается принятой строкой, а не отменяется и не вставляется
        // вторым шагом: иначе буфер с лишней строкой вставил бы её целиком, то есть ровно то,
        // что фильтр и отбрасывает. Само присваивание обязательно — без него вставка ушла бы
        // в поле с исходным содержимым буфера.
        if (!string.Equals(accepted, pasted, StringComparison.Ordinal))
        {
            e.DataObject = new DataObject(DataFormats.UnicodeText, accepted);
        }

        Accept(box);
    }

    private static void Reject(TextBox box, AllowedChars allowed)
    {
        if (allowed == AllowedChars.None)
        {
            return;
        }

        box.RaiseEvent(new RejectedEventArgs(RejectedEvent, box, RejectionMessage(allowed)));
    }

    /// <summary>
    /// Сообщает окну, что ввод принят, — чтобы оно погасило висящее объяснение отказа.
    /// </summary>
    /// <remarks>
    /// Отказ здесь не гасится: иначе подсказка мигала бы на каждом негодном символе, то есть
    /// пропадала бы сразу и толку от неё не было бы.
    /// </remarks>
    private static void Accept(TextBox box) =>
        box.RaiseEvent(new AcceptedEventArgs(AcceptedEvent, box));

    /// <summary>
    /// Аргументы события <see cref="RejectedEvent"/>.
    /// </summary>
    public sealed class RejectedEventArgs : RoutedEventArgs
    {
        /// <param name="routedEvent">Событие, которое поднимается.</param>
        /// <param name="source">Поле, ввод в которое отвергнут.</param>
        /// <param name="message">Объяснение отказа для показа пользователю.</param>
        public RejectedEventArgs(RoutedEvent routedEvent, object source, string message)
            : base(routedEvent, source)
        {
            Message = message;
            Field = (TextBox)source;
        }

        /// <summary>Объяснение отказа для показа пользователю.</summary>
        public string Message { get; }

        /// <summary>Поле, ввод в которое отвергнут.</summary>
        /// <remarks>
        /// Поле передаётся явно, а окно берёт его отсюда, а не из <see cref="RoutedEventArgs.Source"/>:
        /// событие всплывает от поля до окна через контейнер подходов, и на приёмнике <c>Source</c>
        /// указывает уже контейнер, а не поле. На это повесили подсказку — и она вставала по центру
        /// всех подходов сразу, независимо от того, куда вводили.
        /// </remarks>
        public TextBox Field { get; }
    }

    /// <summary>
    /// Аргументы события <see cref="AcceptedEvent"/>.
    /// </summary>
    public sealed class AcceptedEventArgs : RoutedEventArgs
    {
        /// <param name="routedEvent">Событие, которое поднимается.</param>
        /// <param name="source">Поле, ввод в которое принят.</param>
        public AcceptedEventArgs(RoutedEvent routedEvent, object source)
            : base(routedEvent, source)
        {
            Field = (TextBox)source;
        }

        /// <summary>Поле, ввод в которое принят.</summary>
        /// <remarks>
        /// Поле передаётся явно по той же причине, что и в <see cref="RejectedEventArgs"/>:
        /// на приёмнике всплытия <c>Source</c> уже не поле (ловушка 35).
        /// </remarks>
        public TextBox Field { get; }
    }
}
