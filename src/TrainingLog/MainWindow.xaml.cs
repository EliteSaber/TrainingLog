using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingLog.Controls;
using TrainingLog.Services;
using TrainingLog.ViewModels;

namespace TrainingLog;

/// <summary>
/// Главное окно журнала тренировок: дни, количество показываемых дней и меню параметров.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly HotkeyBinder _binder;
    private readonly HotkeyService _hotkeys;

    /// <summary>Шаги выделения списка: id назначения к сдвигу строк.</summary>
    /// <remarks>
    /// Статичное поле, а не локальный массив на нажатие: обработчик срабатывает по каждой
    /// вертикальной стрелке, кем бы ни держался клавиатурный фокус в окне.
    /// </remarks>
    private static readonly (string Id, int Delta)[] StepKeys =
    [
        ("Main.SelectedUp", -1),
        ("Main.SelectedDown", 1),
    ];

    /// <summary>Прокрутка журнала: скролл живёт в шаблоне списка, окно берёт его по имени.</summary>
    private ScrollViewer? _journalScroll;

    /// <summary>Дата строки, которая сейчас сверху журнала: по ней журнал возвращается на место.</summary>
    private DateOnly? _journalTopDate;

    /// <summary>Смещение, при котором запомнено верхнее место: по нему видно, что журнал прокручен.</summary>
    private double _journalTopOffset;

    /// <summary>Дата, к которой журнал возвращается: она же замораживает запомненное место.</summary>
    private DateOnly? _restoreDate;

    public MainWindow(MainViewModel viewModel, HotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(hotkeys);

        InitializeComponent();

        DataContext = viewModel;

        _viewModel = viewModel;

        // Жест назначенных клавиш нужен обработчику списка напрямую: ScrollViewer над ним
        // перехватывает всплывающие стрелки раньше привязок окна.
        _hotkeys = hotkeys;

        // Пункты меню шестерёнки живут в отдельном логическом дереве, и биндер дерева окна их
        // не найдёт — исполнители заданы явно. «Редактировать» и «Показать» работают с
        // выделенной строкой, поэтому регистрируются строками, а не кнопками.
        _binder = new HotkeyBinder(this, hotkeys)
            .Invoke("Main.OpenExercises", () => Run(_viewModel.OpenExercisesCommand))
            .Invoke("Main.OpenPlans", () => Run(_viewModel.OpenPlansCommand))
            .Invoke("Main.OpenSettings", () => Run(_viewModel.OpenSettingsCommand))
            .Invoke("Main.Apply", () => Run(_viewModel.ApplyCommand))
            .Invoke("Main.AddDay", () => Run(_viewModel.AddDayCommand))
            .Row("Main.EditDay", DaysList, row =>
            {
                if (row is DayRowViewModel day)
                {
                    Run(_viewModel.EditDayCommand, day);
                }
            })
            .Row("Main.ShowNote", DaysList, ShowNote);

        _binder.Rebuild();

        // Отказ по вводу приходит от поля количества дней, и подсказкой его показывает StatusHint.
        // Окно живёт до конца работы приложения, поэтому отписка в Closed — формальность.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

        // Изменение места базы: модель докладывает событием, якорь и показ — здесь.
        _viewModel.DatabaseAddressChanged += OnDatabaseAddressChanged;

        // Перечитывание журнала сдвигает содержимое вниз: окно запоминает верхнее место и
        // возвращает журнал на ту же строку (OnDaysChanged).
        _viewModel.Days.CollectionChanged += OnDaysChanged;

        // Стрелки выделения слушаются на окне: туннель у корня маршрута не зависит от того,
        // где живёт клавиатурный фокус (см. OnWindowPreviewKeyDown).
        PreviewKeyDown += OnWindowPreviewKeyDown;

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>Выполняет команду, если её CanExecute разрешает: горячая клавиша обязана
    /// вести себя как кнопка, а RelayCommand.Execute CanExecute не проверяет (ловушка 46).</summary>
    private static void Run(ICommand command, object? parameter = null)
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    /// <summary>Двигает выделение строк стрелками на назначенные жесты.</summary>
    /// <remarks>
    /// Обработчик висит на окне (туннель у корня маршрута), а не на списке дней: так клавиша
    /// доходит сюда при любом положении клавиатурного фокуса. Перехват на самом списке завязывал
    /// выделение на редкое состояние «фокус внутри поддерева списка»: любой клик по строке или
    /// по кнопке, меню правой кнопки и возврат из окна дня уводят клавиатуру из него, и тогда
    /// всплывающую стрелку первым съедает ScrollViewer над списком — прокрутив содержимое
    /// и погасив нажатие. Handled на корне гасит всё дерево сразу: и скроллер, и привязки окна
    /// стрелку больше не видят.
    ///
    /// Поле ввода — исключение: там стрелки двигают каретку, и выделение строк их не отнимает.
    /// Контекстные меню, попапы и окно дня живут в своих HWND-деревьях и маршрутизируют клавиши
    /// отдельно, поэтому их клавиатуру корневой туннель чужого окна не задевает.
    ///
    /// Совпадение берётся с назначенными жестами через <see cref="HotkeyService"/>, а не
    /// с захардкоженным Up/Down: клавишу назначают в окне параметров, и файл настроек обязан
    /// звучать здесь же. Несовпавшая клавиша не гасится: это не назначение.
    /// </remarks>
    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase)
        {
            return;
        }

        // Один статичный перечень шагов: оба назначения опрашиваются у каждого нажатия,
        // и первым совпавшим список заканчивает обработку.
        foreach (var (id, delta) in StepKeys)
        {
            if (_hotkeys.Resolve(id) is { } gesture
                && HotkeyService.Matches(gesture, e.Key, Keyboard.Modifiers))
            {
                RowSelection.Move(DaysList, delta);

                e.Handled = true;

                return;
            }
        }
    }

    /// <summary>Горячая клавиша «Показать примечание»: работает с выделенной строкой журнала.</summary>
    /// <remarks>
    /// Логика та же, что у клика по кнопке, и якорь тоже нужен: имя у кнопок в строке данных
    /// отсутствует — кнопка находится поиском внутри выделенного дня. Примечание у найденной
    /// кнопки есть по построению поиска, и показывается оно так же, как по клику. Контейнер
    /// выделенного дня берётся через <see cref="RowSelection.Container(ItemsControl,object)"/>;
    /// журнал виртуализован, и за видимой областью контейнера может не быть — помощник доводит
    /// строку в видимую область сам.
    /// </remarks>
    private void ShowNote(object row)
    {
        if (RowSelection.Container(DaysList, row) is not FrameworkElement container)
        {
            return;
        }

        if (FindShowNoteButton(container) is not { } button)
        {
            return;
        }

        var cell = (DayExerciseCell)button.DataContext;

        InputHint.Target = button;

        _viewModel.Hint.ShowNoteCommand.Execute(cell.Note);

        InputHint.RestartCountdown();
    }

    /// <summary>Ищет кнопку «Показать» внутри строки дня: примечание есть — она видима.</summary>
    private static Button? FindShowNoteButton(DependencyObject root)
    {
        if (root is Button { DataContext: DayExerciseCell { HasNote: true } })
        {
            return (Button)root;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            if (FindShowNoteButton(VisualTreeHelper.GetChild(root, i)) is { } child)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>В поле количества дней введено или вставлено недопустимое.</summary>
    /// <remarks>
    /// Здесь только якорь и команда: вид, текст, срок жизни, позиция и крестик живут в
    /// <see cref="StatusHint"/> и в состоянии <c>Hint</c>. Якорь задаётся здесь, а не в разметке:
    /// подсказка общая у отказа и примечания, а якорь у них разный, и локальное присваивание
    /// заменило бы привязку навсегда (ловушка 11).
    ///
    /// Отсчёт перезапускается явно: повторный отказ даёт тот же текст, состояние подсказки не
    /// меняется, и без этого она погасла бы на середине набора.
    /// </remarks>
    private void OnInputRejected(object sender, RoutedEventArgs e)
    {
        if (e is AllowedCharsInput.RejectedEventArgs rejected)
        {
            InputHint.Target = DaysCountBox;

            _viewModel.Hint.ShowCommand.Execute(rejected.Message);

            InputHint.RestartCountdown();
        }
    }

    /// <summary>
    /// В поле введён или вставлен допустимый символ: гасим висящее объяснение. Отказ сюда не
    /// приводит — иначе подсказка мигала бы на каждом негодном символе.
    /// </summary>
    private void OnInputAccepted(object sender, RoutedEventArgs e) =>
        _viewModel.Hint.DismissCommand.Execute(null);

    /// <summary>
    /// Модель сообщила об изменении места базы: предложение перезапустить приложение сейчас.
    /// </summary>
    /// <remarks>
    /// <c>MessageBox</c> здесь, как и во всех окнах: это работа интерфейса, а модели ей не место.
    /// Вопрос модальный — переключение соединения «на лету» нужно отменять целиком, поэтому
    /// решение остаётся за пользователем; после «Нет» приложение тихо продолжает работать со
    /// старой базой до следующего изменения адреса.
    ///
    /// Перезапуск — новый процесс и закрытие текущего: соединение с базой строится один раз при
    /// старте, и «на лету» его не поменять. Путь берётся у запущенного процесса, а не рабочим
    /// каталогом: тот зависит от способа запуска, а свой exe приложение знает всегда.
    /// </remarks>
    private void OnDatabaseAddressChanged(string message)
    {
        var restart = MessageBox.Show(
            this,
            "Место базы данных изменено. Подключение к новой базе произойдёт после перезапуска. Перезапустить сейчас?",
            "Параметры",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (restart != MessageBoxResult.Yes)
        {
            return;
        }

        var path = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            ShowRestartFailure("не определён путь к запущенному файлу приложения");

            return;
        }

        try
        {
            if (Process.Start(path) is null)
            {
                ShowRestartFailure("процесс не запустился");
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException)
        {
            ShowRestartFailure(exception.Message);
        }
    }

    /// <summary>Перезапуск не удался до закрытия: приложение продолжает работать со старой базой.</summary>
    private void ShowRestartFailure(string reason) =>
        MessageBox.Show(
            this,
            $"Не удалось закрыть приложение и запустить заново ({reason}). Работа продолжается со старой базой, к новой подключение произойдёт при следующем запуске.",
            "Параметры",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

    private void OnClosed(object? sender, EventArgs e)
    {
        PreviewKeyDown -= OnWindowPreviewKeyDown;
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
        _viewModel.DatabaseAddressChanged -= OnDatabaseAddressChanged;
        _viewModel.Days.CollectionChanged -= OnDaysChanged;

        if (_journalScroll is { } scroll)
        {
            scroll.ScrollChanged -= OnJournalScrolled;
        }
    }

    /// <summary>Журнал прокрутили: запоминаем строку, которая теперь сверху.</summary>
    /// <remarks>
    /// Запоминается верхняя видимая строка, а не смещение: перечитывание пересобирает список
    /// целиком, и прежние строки уходят — остаётся только дата, по которой строка ищется заново.
    /// </remarks>
    private void OnJournalScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (_journalScroll is not { } scroll)
        {
            return;
        }

        if (TopVisibleRow(scroll) is not { } container
            || container.DataContext is not DayRowViewModel day)
        {
            return;
        }

        _journalTopDate = day.Date;
        _journalTopOffset = scroll.VerticalOffset;
    }

    /// <summary>Список дней перечитали: журнал возвращается на ту же строку, что была сверху.</summary>
    /// <remarks>
    /// Перечитывание пересобирает список целиком, и новый день сверху сдвигает содержимое вниз:
    /// смещение прокрутки остаётся прежним, а на экране оказывается другая строка. Возврат нужен
    /// пользователю, который читал журнал, — он смотрел на конкретные дни.
    ///
    /// Возврат отложен до раскладки: сразу после пересборки контейнеров ещё нет, и место строки
    /// незнаемо. Дата верхней строки запоминается при прокрутке, а перечитывание её замораживает:
    /// иначе следующая же прокрутка перебила бы возврат.
    ///
    /// У верха журнала возврата нет: там пользователь и так видит начало, и новый день должен
    /// появиться перед глазами, а не под ними.
    /// </remarks>
    private void OnDaysChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Reset or NotifyCollectionChangedAction.Add))
        {
            return;
        }

        // Возврат уже запланирован: перечитывание идёт сбросом и добавлением строк по одной.
        if (_restoreDate is not null)
        {
            return;
        }

        if (_journalTopDate is not { } date || _journalTopOffset <= 0.5)
        {
            return;
        }

        _restoreDate = date;

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RestoreJournalTop));
    }

    /// <summary>Возвращает журнал на строку, которая была сверху до перечитывания.</summary>
    private void RestoreJournalTop()
    {
        var date = _restoreDate;

        _restoreDate = null;

        if (date is not { } wanted || _journalScroll is not { } scroll)
        {
            return;
        }

        // Строка ищется по дате: перечитывание создаёт новые строки, и прежний объект в списке
        // больше не живёт — переживает пересборку только дата.
        var index = IndexOfDay(wanted);

        if (index < 0 || DaysList.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
        {
            return;
        }

        // Место строки в содержимом: от края окна к содержимому прибавляется прокрутка.
        var place = container.TransformToAncestor(scroll).Transform(new Point(0, 0)).Y;

        scroll.ScrollToVerticalOffset(place + scroll.VerticalOffset);
    }

    /// <summary>Номер строки журнала с такой датой; минус — такой даты в журнале нет.</summary>
    private int IndexOfDay(DateOnly date)
    {
        var index = 0;

        foreach (var day in _viewModel.Days)
        {
            if (day.Date == date)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    /// <summary>Контейнер строки, которая сейчас сверху журнала.</summary>
    /// <remarks>
    /// Берутся построенные контейнеры, а не все строки списка: построенных единицы, а строк
    /// сотни, и перебор всех индексов на каждое движение прокрутки стоил бы дорого.
    /// </remarks>
    private static FrameworkElement? TopVisibleRow(ScrollViewer scroll)
    {
        if (scroll.Content is not FrameworkElement presenter)
        {
            return null;
        }

        // В скролле лежит ItemsPresenter, а в нём — панель, а в панели — построенные строки.
        if (VisualTreeHelper.GetChildrenCount(presenter) == 0)
        {
            return null;
        }

        var panel = VisualTreeHelper.GetChild(presenter, 0);
        var count = VisualTreeHelper.GetChildrenCount(panel);

        for (var index = 0; index < count; index++)
        {
            if (VisualTreeHelper.GetChild(panel, index) is not FrameworkElement container)
            {
                continue;
            }

            var place = container.TransformToAncestor(scroll).Transform(new Point(0, 0)).Y;

            // Строка видна, если её низ опускается ниже верхней границы окна. Место отсчитывается
            // от края окна: прокрутка из него уже вычта.
            if (place + container.ActualHeight > 0)
            {
                return container;
            }
        }

        return null;
    }

    /// <summary>
    /// Нажата кнопка «Показать» у упражнения: показываем примечание.
    /// </summary>
    /// <remarks>
    /// Вид и срок задаёт модель (<see cref="HintState.ShowNoteCommand"/>), окно знает только якорь:
    /// имя у кнопок в строке данных отсутствует — они создаются по составу упражнений. Якорь
    /// задаётся до показа, потому что позиция считается в момент включения показа.
    ///
    /// Пустой якорь или ячейка без примечания — не показываем ничего: так случиться не может,
    /// но проверка стоит копейку и снимает вопрос «а что показывает <c>null</c>».
    /// </remarks>
    private void OnShowNoteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || button.DataContext is not DayExerciseCell { HasNote: true } exercise)
        {
            return;
        }

        InputHint.Target = button;

        _viewModel.Hint.ShowNoteCommand.Execute(exercise.Note);

        InputHint.RestartCountdown();
    }

    /// <summary>
    /// Дни читаются при загрузке окна, а не в конструкторе: до показа окна ждать нечего — список
    /// всё равно никто не увидит.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // Скролл журнала живёт в шаблоне списка, и взять его иначе нечем: имя шаблона.
        _journalScroll = DaysList.Template.FindName("DaysScroll", DaysList) as ScrollViewer;

        if (_journalScroll is { } scroll)
        {
            scroll.ScrollChanged += OnJournalScrolled;
        }

        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>
    /// Открывает меню параметров по нажатию на кнопку-шестерёнку.
    /// </summary>
    private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
    {
        if (SettingsButton.ContextMenu is not { } menu)
        {
            return;
        }

        menu.PlacementTarget = SettingsButton;
        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>Двойной клик по строке дня открывает правку этой даты.</summary>
    /// <remarks>
    /// Подписка на списке дней, а не на строке: <c>MouseDoubleClick</c> объявлен у
    /// <see cref="Control"/>, а строка дня — <c>Border</c>, то есть <c>Decorator</c> без него; в
    /// разметке такое событие на <c>Border</c> и не поставилось бы. Строка находится по элементу под
    /// указателем штатным <see cref="ItemsControl.ContainerFromElement(DependencyObject)"/>.
    ///
    /// Команда, а не прямой вызов окна: дабл-клик и пункт меню обязаны делать одно и то же,
    /// и перечитывать журнал после правки должен один и тот же код.
    /// </remarks>
    private void OnDayDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ItemsControl days)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source
            || days.ContainerFromElement(source) is not FrameworkElement { DataContext: DayRowViewModel day } container)
        {
            return;
        }

        if (IsShowNoteButton(source, container))
        {
            return;
        }

        _viewModel.EditDayCommand.Execute(day);
    }

    /// <summary>Двойной клик пришёлся по кнопке «Показать», а не по самой строке.</summary>
    /// <remarks>
    /// Два быстрых клика по «Показать» — обычный способ перечитать примечание, и правку дня они
    /// открывать не должны. Проверка живёт здесь, а не гашением <c>MouseDoubleClick</c> на кнопке:
    /// событие поднимается от элемента под указателем, и на самой кнопке помечать его оказалось
    /// бесполезно (ловушка 45).
    ///
    /// Проверка сделана по данным ячейки, чтобы сработать ровно на той кнопке, которая показывает
    /// примечание.
    /// </remarks>

    /// <param name="source">Элемент под указателем.</param>
    /// <param name="container">Контейнер строки дня: обход идёт до него, не включая.</param>
    private static bool IsShowNoteButton(DependencyObject source, DependencyObject container)
    {
        for (var element = source; element is not null && !ReferenceEquals(element, container); element = ParentOf(element))
        {
            if (element is Button { DataContext: DayExerciseCell { HasNote: true } })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Родитель элемента: визуальный для <see cref="Visual"/>, логический для прочего.</summary>
    /// <remarks>
    /// Ветка для не-<see cref="Visual"/> нужна на всякий случай: <c>VisualTreeHelper</c> такой
    /// элемент не берёт и бросает исключение, а источником события может оказаться любой элемент под
    /// указателем.
    /// </remarks>
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    /// <summary>Кладёт строку дня в открываемое меню: модель главного окна и параметр команды.</summary>
    /// <remarks>
    /// Из разметки это не выразить: у <c>ContextMenu</c> отдельное логическое дерево, внутрь него не
    /// наследуется <c>DataContext</c> и не доходит поиск предка до окна — в отличие от привязки
    /// <c>PlacementTarget.DataContext</c> в меню шестерёнки (ловушки 1 и 52). Здесь нужны оба
    /// значения сразу. Событие <c>ContextMenuOpening</c> поднимается до показа меню, поэтому к
    /// моменту вычисления привязок оба значения уже на месте.
    /// </remarks>
    private void OnDayContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement element
            || element.ContextMenu is not { } menu
            || element.DataContext is not DayRowViewModel day)
        {
            return;
        }

        menu.DataContext = _viewModel;

        foreach (var item in menu.Items)
        {
            if (item is MenuItem menuItem)
            {
                menuItem.CommandParameter = day;
            }
        }
    }
}
