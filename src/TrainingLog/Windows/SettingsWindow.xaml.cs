using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TrainingLog.Controls;
using TrainingLog.Core.Settings;
using TrainingLog.Services;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>Окно параметров: число дней, адрес базы и горячие клавиши.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly HotkeyService _hotkeys;

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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Подписка в окне, а не в модели: событие приходит от поля ввода и живёт в Controls.
        // Отписка в Closed обязательна — иначе закрытое окно удерживалось бы подпиской.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

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
    }

    /// <summary>
    /// <c>DialogResult</c> выставляется по уведомлению: по одному значению
    /// <see cref="SettingsViewModel.Accepted"/> вызывающий <c>ShowDialog</c> не узнал бы, что
    /// настройка записана.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Accepted) && _viewModel.Accepted)
        {
            // Шрифт переписан в файле: подменяем токены, и все открытые окна перерисовываются
            // сами — ответственность, как и у диалогов, интерфейсная, поэтому окно.
            AppFonts.Apply(_viewModel.SelectedFont.Name, _viewModel.SelectedScale);

            // Файл переписан: сервис перечитает его и соберёт живущие окна пересборкой привязок.
            // Windows-диалоги привязки пересоздают при показе, поэтому здесь только живущие.
            _hotkeys.NotifyChanged();

            DialogResult = true;

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

    private void OnClosed(object? sender, EventArgs e)
    {
        // Выход без сохранения возвращает предпросмотр к срезу файла на открытие окна. По
        // Accepted откат не нужен: модель уже применила выбранное, и файл с ним согласен.
        if (!_viewModel.Accepted)
        {
            AppFonts.Apply(_viewModel.SavedFontName, _viewModel.SavedFontSizePercent);
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
    }
}
