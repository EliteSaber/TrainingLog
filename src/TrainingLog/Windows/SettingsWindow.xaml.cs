using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TrainingLog.Controls;
using TrainingLog.Core.Settings;
using TrainingLog.Services;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>Окно параметров: число дней, адрес базы, горячие клавиши и размеры окон.</summary>
/// <remarks>
/// Окно немодальное и единственное: его можно открыть при любых других открытых окнах, а
/// повторное «Параметры» выводит уже открытое окно вперёд. Отсюда два отличия от диалога:
/// <c>DialogResult</c> на немодальном окне выставлять нельзя (присваивание кинет
/// <see cref="InvalidOperationException"/>), и «Отменить» с Esc закрывают окно руками —
/// <c>IsCancel</c> работает только в диалоге. Сохранение окно оповещает событием
/// <see cref="Saved"/>, и его ждёт <see cref="Services.WindowService"/>.
/// </remarks>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly HotkeyService _hotkeys;
    private readonly RoutedEventHandler _dragEndedHandler;

    // Само окно параметров отложено до отпускания ЛКМ при перетаскивании размера.
    private bool _applySelfWhenMouseUp;

    /// <summary>
    /// Настройки записаны: главное окно перечитает файл, а размеры применятся к открытым окнам.
    /// </summary>
    /// <remarks>
    /// Событие, а не результат диалога: окно немодальное, и спрашивать у него «приняли ли»
    /// нечем — принять можно только записав файл.
    /// </remarks>
    public event Action? Saved;

    /// <param name="viewModel">Параметры приложения.</param>
    /// <param name="hotkeys">Сервис назначений: уведомляет живущие окна после записи файла.</param>
    public SettingsWindow(SettingsViewModel viewModel, HotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(hotkeys);

        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _hotkeys = hotkeys;
        _dragEndedHandler = new RoutedEventHandler(OnDragEnded);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Размеры окон применяются к открытым окнам сразу, как шрифт и тема: предпросмотр
        // виден, и файл при этом не меняется.
        _viewModel.WindowSizesChanged += OnWindowSizesChanged;

        // Размер окна — из каталога или из файла настроек: в разметке его нет, иначе размер
        // жил бы в двух местах.
        WindowSizes.Apply(this);

        // Подписка в окне, а не в модели: событие приходит от поля ввода и живёт в Controls.
        // Отписка в Closed обязательна — иначе закрытое окно удерживалось бы подпиской.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

        // Конец перетаскивания DragValue: применить отложенный размер самого окна параметров.
        AddHandler(DragValue.DragEndedEvent, _dragEndedHandler, handledEventsToo: true);

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Фокус и выделение — при загрузке, а не в конструкторе: у ещё не показанного окна нет
    /// цепочки активации, и <c>Focus()</c> отработает вхолостую (ловушка 9).
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        DaysCountBox.Focus();
        DaysCountBox.SelectAll();

        // Список шрифтов — на выбранном пункте, а не с начала: выделение ставится до
        // применения шаблона и раскладки, и автоматическая прокрутка WPF тогда молча не
        // срабатывает; прокрутка после раскладки. Только внутри списка.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ScrollSelectedFontIntoView));
    }

    /// <summary>Прокручивает список шрифтов к выбранному пункту.</summary>
    private void ScrollSelectedFontIntoView()
    {
        if (FontList.SelectedItem is not { } selected)
        {
            return;
        }

        // Форсированная раскладка: ScrollIntoView до неё молча не срабатывает.
        FontList.UpdateLayout();
        FontList.ScrollIntoView(selected);
    }

    /// <summary>
    /// Запись настроек: применяем то, что записано, и закрываем окно.
    /// </summary>
    /// <remarks>
    /// Окно немодальное, и <c>DialogResult</c> выставлять нельзя: присваивание кинет
    /// <see cref="InvalidOperationException"/>. Закрытие делает само окно, а вызывающий узнаёт
    /// о записи по событию <see cref="Saved"/>.
    /// </remarks>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Accepted) && _viewModel.Accepted)
        {
            // Шрифт переписан в файле: подменяем токены, и все открытые окна перерисовываются
            // сами — ответственность, как и у диалогов, интерфейсная, поэтому окно.
            AppFonts.Apply(_viewModel.SelectedFont.Name, _viewModel.SelectedScale);

            // Тема — тем же порядком: применённая тема обязана совпадать с записанной.
            AppTheme.Apply(_viewModel.SelectedTheme.Value);

            // Файл переписан: сервис перечитает его и соберёт живущие окна пересборкой привязок.
            // Windows-диалоги привязки пересоздают при показе, поэтому здесь только живущие.
            _hotkeys.NotifyChanged();

            // Размеры — к уже открытым окнам: пользователь задал новые, и они обязаны
            // подействовать сразу, а не с следующего открытия.
            WindowSizes.Initialize(_viewModel.SavedWindowSizes);
            WindowSizes.ApplyToOpenWindows(this);

            Saved?.Invoke();

            Close();

            return;
        }

        // Предпросмотр: выбор в ComboBox (и клавиатурная навигация по закрытому списку — она
        // тоже пишет SelectedItem) перекрашивает окна до всякого «Сохранить». Подменять токены
        // из модели нельзя — она живёт в тестах без Application, поэтому окно.
        if (e.PropertyName is nameof(SettingsViewModel.SelectedFont)
            or nameof(SettingsViewModel.SelectedScale))
        {
            AppFonts.Apply(_viewModel.SelectedFont.Name, _viewModel.SelectedScale);
        }

        // Тема — тем же предпросмотром: палитра перезаписывается на месте, и все открытые окна
        // перерисовываются по ссылкам DynamicResource.
        if (e.PropertyName == nameof(SettingsViewModel.SelectedTheme))
        {
            AppTheme.Apply(_viewModel.SelectedTheme.Value);
        }

        // Перечень пересобран — показывать его с начала: иначе после смены окна или ввода в
        // поиск он остался бы прокрученным на прежнем месте, а прокрутка при этом не сбрасывается
        // сама: WPF только подрезает её по высоте нового содержимого.
        if (e.PropertyName is nameof(SettingsViewModel.SelectedChip)
            or nameof(SettingsViewModel.HotkeySearch))
        {
            ScrollHotkeyListToTop();
        }
    }

    /// <summary>
    /// Размер окна изменился: открытые окна подстраиваются сразу, как шрифт и тема.
    /// </summary>
    /// <remarks>
    /// Файл при этом не меняется — он пишется по «Сохранить».
    ///
    /// Прочие окна применяются сразу на каждое изменение. Само окно параметров — тоже, но
    /// только когда правка идёт без зажатой левой кнопки мыши (клавиатура — сразу); при
    /// перетаскивании мышью по своему полю сдвинуть себя сейчас нельзя: поле уедет из-под
    /// курсора и сломается отсчёт перетаскивания (<see cref="DragValue"/>). Поэтому при зажатой
    /// ЛКМ само окно откладывается и применяется один раз на отпускание — <see cref="OnDragEnded"/>.
    /// </remarks>
    private void OnWindowSizesChanged()
    {
        WindowSizes.Initialize(_viewModel.PreviewWindowSizes());

        WindowSizes.ApplyToOpenWindows(this);

        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            _applySelfWhenMouseUp = true;
        }
        else
        {
            WindowSizes.Apply(this);
        }
    }

    /// <summary>Перетаскивание размера отпущено: применить отложенный размер самого окна.</summary>
    private void OnDragEnded(object sender, RoutedEventArgs e)
    {
        if (!_applySelfWhenMouseUp)
        {
            return;
        }

        _applySelfWhenMouseUp = false;
        WindowSizes.Apply(this);
    }

    /// <summary>Прокручивает перечень клавиш к началу.</summary>
    /// <remarks>
    /// ScrollViewer живёт в шаблоне перечня: над ItemsControl'ом он виртуализацию глушит
    /// (ловушка 30), и достать его иначе нельзя — по имени в области имён шаблона.
    /// Шаблон может быть ещё не применён (вкладку не открывали) — тогда и списка нет.
    /// </remarks>
    private void ScrollHotkeyListToTop()
    {
        if (HotkeyList.Template.FindName("HotkeyScroll", HotkeyList) is ScrollViewer viewer)
        {
            viewer.ScrollToHome();
        }
    }

    /// <summary>Фокус попал в поле захвата: показать приглашение и запомнить значение для Esc.</summary>
    /// <remarks>
    /// Приглашение пишется тем же носителем, что и промежуточный набор «Ctrl+…», — GestureText'ом
    /// строки, — поэтому первая нажатая клавиша перекрывает его без специально-логики. Незавершённым
    /// текстам в сохранение и конфликты закрыто дорогой признаком
    /// <see cref="HotkeyRowViewModel.IsCapturing"/>, который ведёт та же строка.
    /// </remarks>
    private void OnCaptureGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HotkeyRowViewModel row })
        {
            row.BeginCapture();
            row.CapturePreview(CaptureHint);
        }
    }

    /// <summary>Текст приглашения незапущенного захвата: на его месте скоро будет набор.</summary>
    /// <remarks>
    /// Коротко умышленно: поле захвата шириной 120px, и полное «Нажмите комбинацию…» в нём
    /// не помещается; остальное показывают подсветка и тултип поля.
    /// </remarks>
    private const string CaptureHint = "Комбинация…";

    /// <summary>Уход фокуса без подтверждения (Tab, клик мимо): вернуть значение до захвата.</summary>
    /// <remarks>
    /// После подтверждения обмен «сохранено — ушёл» отыгрывается честно: <see cref="HotkeyRowViewModel.CommitCapture"/>
    /// ставит «до захвата» равным подтверждённому, и возврат возвращает то же самое.
    /// </remarks>
    private void OnCaptureLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HotkeyRowViewModel row })
        {
            row.CancelCapture();
        }
    }

    /// <summary>Захват комбинации клавиатурой: поле печатает только клавиатура, не пользователь.</summary>
    /// <remarks>
    /// Чистые модификаторы показываются прогрессом «Ctrl+…»; обычная клавиша завершает набор.
    /// Esc возвращает захваченное до фокуса и гасит событие — иначе диалог бы закрылся, потому
    /// что Esc всплывает до <c>IsCancel</c>. Enter только гасит событие (case один:
    /// <c>Key.Enter == Key.Return</c>) и ничего не подтверждает: комбинация подтверждается
    /// нажатой клавишей, и подтверждение само выводит фокус из поля.
    ///
    /// Буквы без модификаторов подтверждением не становятся: WPF-жест их не переварит, и
    /// назначение было бы мёртвым — лучше молчаливо дождаться модификатора, чем сохранить
    /// клавишу, которая никогда не сработает.
    /// </remarks>
    private void OnCaptureKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HotkeyRowViewModel row })
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;

        // Keyboard.Modifiers флага Windows не отдаёт (ловушка 28): клавиша нажата физически,
        // а модификаторной сводки её нет — Win-одиночка рисовалась одним многоточием, а
        // Win+буква уходила в ветку «буква без модификаторов» без комита. Опрашиваем напрямую.
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
        {
            modifiers |= ModifierKeys.Windows;
        }

        // Alt-сочетания WPF относит к системным: в e.Key приходит Key.System, а настоящая
        // клавиша лежит в e.SystemKey (ловушка 27). Без подмены ветка модификаторов
        // (там перечислены LeftAlt/RightAlt, но не System) служило бы мимо, и Alt
        // сам по себе комитился бы как «Alt+System» — жест, который никогда не сработает.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch (key)
        {
            // Esc не станет частью комбинации никогда: она возвращает захваченное до фокуса.
            case Key.Escape:
                row.CancelCapture();
                e.Handled = true;
                return;

            // Enter ничего не подтверждает: комбинация подтверждается нажатой клавишей. Его
            // задача — остаться в поле и не закрыть диалог (IsDefault внизу ждал бы нажатия).
            // Key.Enter и Key.Return — один и тот же член перечисления, второй case был бы дублем.
            case Key.Enter:
                e.Handled = true;
                return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
        {
            // Модификатор сам по себе — промежуточный набор: показываем, что наели.
            row.CapturePreview(CombineText(modifiers, "…"));

            e.Handled = true;

            return;
        }

        // Буква без модификаторов не подтверждает: комбинация не собрана, поле ждёт модификатора.
        if (modifiers == ModifierKeys.None && key is >= Key.A and <= Key.Z)
        {
            e.Handled = true;

            return;
        }

        row.CommitCapture(CombineText(modifiers, key.ToString()));

        // Завершённый набор выводит фокус из окна: иначе поле продолжило бы слушать клавиши
        // уже без индикации — подсветка с приглашением комитом сняты, и следующая клавиша
        // молча переписывала бы значение. Правка комбинации — только по новому клику в поле.
        // Полная разфокусировка вместо MoveFocus(Next): следующим элементом была бы кнопка
        // «✕» той же строки, и привычный Enter после набора нажал бы её — убрал бы только
        // что введённое назначение.
        Keyboard.ClearFocus();

        e.Handled = true;
    }

    /// <summary>Canonical набор: модификаторы в порядке Ctrl, Shift, Alt, Win, за ними клавиша.</summary>
    /// <remarks>
    /// Порядок совпадает с <see cref="HotkeyRules.ToString(HotkeyCombo)"/>, но правило Core
    /// вызываться не может: поле захвата оперирует WPF-перечислениями.
    /// </remarks>
    private static string CombineText(ModifierKeys modifiers, string key)
    {
        List<string> parts = [];

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(key);

        return string.Join("+", parts);
    }

    /// <summary>
    /// Введено или вставлено недопустимое: показать объяснение и перезапустить отсчёт.
    /// </summary>
    /// <remarks>
    /// Якорь берётся из <see cref="AllowedCharsInput.RejectedEventArgs.Field"/>, а не из
    /// <c>e.Source</c>: событие всплывает от поля до окна, и на приёмнике <c>Source</c> уже не
    /// поле (ловушка 41). Отсчёт перезапускается явно — повторный отказ даёт тот же текст,
    /// состояние подсказки не меняется, и без этого она погасла бы на середине набора.
    /// </remarks>
    private void OnInputRejected(object sender, RoutedEventArgs e)
    {
        if (e is not AllowedCharsInput.RejectedEventArgs rejected)
        {
            return;
        }

        InputHint.Target = rejected.Field;

        _viewModel.Hint.ShowCommand.Execute(rejected.Message);

        InputHint.RestartCountdown();
    }

    /// <summary>Допустимый ввод гасит висящее объяснение. Отказ сюда не приводит: мигал бы.</summary>
    private void OnInputAccepted(object sender, RoutedEventArgs e) =>
        _viewModel.Hint.DismissCommand.Execute(null);

    /// <summary>
    /// Диалоги адреса базы живут здесь, а не в модели: это работа интерфейса, как и
    /// <c>MessageBox</c>. Стартовый каталог диалогов — текущее поле, иначе каждый выбор заново
    /// начинался бы с «Документов» среди незнакомых дисков.
    /// </summary>
    private void OnPickDirectory(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = _viewModel.DatabaseDirectory,
        };

        if (dialog.ShowDialog(this) is true)
        {
            _viewModel.DatabaseDirectory = dialog.FolderName;
        }
    }

    /// <summary>
    /// Файл отвечает на оба поля: директория берётся у выбранного файла, наименование — его имя.
    /// Раздельная подстановка полей вместо целого пути: иначе имя файла осталось бы
    /// хвостом в поле адреса и разошлось бы с наименованием при следующей правке.
    /// </summary>
    /// <remarks>
    /// Наименование переводится тем же правилом, что и в модели: в поле имя живёт без хвостового
    /// <c>.db</c>, а расширение дописывается при сохранении всегда. Поэтому файл с чужим
    /// расширением (например <c>старая.sqlite</c>) возвращается в настройках как
    /// <c>старая.db</c> — единственного расширения у файла базы по договору нет и второго не будет.
    /// </remarks>
    private void OnPickDatabaseFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            InitialDirectory = _viewModel.DatabaseDirectory,
            FileName = _viewModel.DatabaseName,
            Filter = "База данных (*.db)|*.db|Все файлы (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) is true)
        {
            _viewModel.DatabaseDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            _viewModel.DatabaseName = DatabaseNameRules.ForDisplay(Path.GetFileName(dialog.FileName));
        }
    }

    /// <summary>Нажата кнопка «Отменить»: окно закрывается без записи.</summary>
    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Esc закрывает окно, как в диалоге закрывала кнопка <c>IsCancel</c>.</summary>
    /// <remarks>
    /// В немодальном окне WPF связку «Esc → Отменить» больше никто не держит: <c>IsCancel</c>
    /// работает только в диалоге. Здесь обработка на окне, а не KeyBinding'ом, — команды
    /// «закрыть у окна» в модели нет, и заводить её ради клавиши незачем.
    ///
    /// Поле захвата клавиши перехватывает Esc раньше окна (ловушка 41): захват отменяет
    /// набор, а окно при этом остаётся открытым.
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Выход без сохранения возвращает предпросмотр к срезу файла на открытие окна. По
        // Accepted откат не нужен: модель уже применила выбранное, и файл с ним согласен.
        if (!_viewModel.Accepted)
        {
            AppFonts.Apply(_viewModel.SavedFontName, _viewModel.SavedFontSizePercent);
            AppTheme.Apply(_viewModel.SavedTheme);

            // Размеры — тем же порядком: окна, подстроенные предпросмотром, возвращаются к
            // прежнему виду, а не остаются с размером, который не записан.
            WindowSizes.Initialize(_viewModel.SavedWindowSizes);
            WindowSizes.ApplyToOpenWindows();
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.WindowSizesChanged -= OnWindowSizesChanged;
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
        RemoveHandler(DragValue.DragEndedEvent, _dragEndedHandler);
    }
}
