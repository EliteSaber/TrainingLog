using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using TrainingLog.Controls;

namespace TrainingLog.Services;

/// <summary>Собирает и пересобирает горячие клавиши одного окна.</summary>
/// <remarks>
/// Окно регистрирует исполнителей строк и слотов, а биндер по <see cref="HotkeyService"/>
/// выкладывает <see cref="KeyBinding"/> в <see cref="Window.InputBindings"/>: назначенное в
/// файле годной строкой, затем умолчание из каталога. Window-назначения без явного исполнителя
/// находят кнопку по <see cref="Hotkey.IdProperty"/> в дереве окна и жмут её целиком:
/// автоматизационный Invoke выполняет и Click, и команду с её CanExecute, и встроенные
/// семантики IsDefault/IsCancel, тогда как <c>RaiseEvent(ButtonBase.ClickEvent)</c> команду
/// не выполняет (ловушка 7).
///
/// Отписка от <see cref="HotkeyService.Changed"/> живёт в <see cref="Release"/>, вызываемом
/// закрытием окна; явного <see cref="IDisposable"/> у окна нет — сознательно, чтобы не заводить
/// dispose-обязанность у каждого окна из-за простой отписки от события.
/// </remarks>
public sealed class HotkeyBinder
{
    private readonly Window _window;
    private readonly HotkeyService _hotkeys;

    /// <summary>Явные исполнители Window-назначений: для живущего вне дерева окна.</summary>
    private readonly Dictionary<string, Action> _invokes = new();

    /// <summary>Строковые назначения: список выделения и действие над его строкой.</summary>
    private readonly List<(string Id, ItemsControl List, Action<object> Invoke)> _rows = [];

    private readonly List<KeyBinding> _bindings = [];

    public HotkeyBinder(Window window, HotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(hotkeys);

        _window = window;
        _hotkeys = hotkeys;

        _window.Closed += OnWindowClosed;
        _hotkeys.Changed += Rebuild;
    }

    /// <summary>Явный исполнитель Window-назначения — для элементов вне дерева окна (пункты меню).</summary>
    public HotkeyBinder Invoke(string id, Action invoke)
    {
        _invokes[id] = invoke;

        return this;
    }

    /// <summary>Строковое назначение: действие вызывается с выделенной строкой, без выделения — молчит.</summary>
    public HotkeyBinder Row(string id, ItemsControl list, Action<object> invoke)
    {
        ArgumentNullException.ThrowIfNull(list);

        _rows.Add((id, list, invoke));

        return this;
    }

    /// <summary>Слоты окна дня: действия по номеру упражнения от 1 до <see cref="HotkeyCatalog.SlotCount"/>.</summary>
    public HotkeyBinder Slots(string idPrefix, Action<int> invoke)
    {
        foreach (var number in Enumerable.Range(1, HotkeyCatalog.SlotCount))
        {
            var local = number;
            _invokes[$"{idPrefix}{local}"] = () => invoke(local);
        }

        return this;
    }

    /// <summary>Выкладывает привязки по текущим назначениям. Исполнитель без жеста привязки не получает.</summary>
    public void Rebuild()
    {
        foreach (var binding in _bindings)
        {
            _window.InputBindings.Remove(binding);
        }

        _bindings.Clear();

        // Кнопки собираются одним обходом на всю пересборку: проход FindById под каждый id
        // платил бы полным деревом столько же раз (ловушка 26) — на сохранении это тормозило.
        var buttons = CollectIdButtons();

        foreach (var (id, action) in Candidates(buttons))
        {
            if (action is not { } invoke)
            {
                continue;
            }

            if (_hotkeys.Resolve(id) is not { } gesture)
            {
                continue;
            }

            var binding = new KeyBinding(new Executor(invoke), gesture);
            _window.InputBindings.Add(binding);
            _bindings.Add(binding);
        }
    }

    /// <summary>Все назначаемые id окна с исполнителями: зарегистрированные и найденные по дереву.</summary>
    /// <remarks>
    /// Кнопки приходят готовым словарём от <see cref="CollectIdButtons"/>: один обход дерева
    /// на всю пересборку, а не проход под каждый id (ловушка 26).
    /// </remarks>
    private IEnumerable<(string Id, Action? Invoke)> Candidates(
        Dictionary<string, FrameworkElement> buttons)
    {
        foreach (var (id, invoke) in _invokes)
        {
            yield return (id, invoke);
        }

        foreach (var (id, list, invoke) in _rows)
        {
            Action invokeRow = () =>
            {
                if (RowSelection.Item(list) is { } row)
                {
                    invoke(row);
                }
            };

            yield return (id, invokeRow);
        }

        foreach (var group in HotkeyCatalog.Groups)
        {
            foreach (var candidate in group.Actions)
            {
                if (_invokes.ContainsKey(candidate.Id)
                    || _rows.Exists(row => row.Id == candidate.Id))
                {
                    continue;
                }

                buttons.TryGetValue(candidate.Id, out var button);

                // Кнопки чужого окна каталогу нечего исполнять: у этого окна такого адреса нет.
                if (button is not null)
                {
                    yield return (candidate.Id, () => PressButton(button));
                }
            }
        }
    }

    /// <summary>Собирает адреса <see cref="Hotkey.IdProperty"/> одного окна за один обход дерева.</summary>
    /// <remarks>
    /// Один обход вместо прохода под каждый id: id-кандидатов у каталога десятки, а дерево окна
    /// с журналом — сотни узлов, и вложенные проходы тормозили сохранение с нагрузкой ЦП
    /// (ловушка 26). Спуск ведёт и по визуальным, и по логическим детям — до раскладки
    /// шаблонное содержание есть только в логическом, — но повторные визиты узла гасит
    /// множество <c>visited</c>, а не-Visual узлы отсечены (ловушка 25).
    /// </remarks>
    private Dictionary<string, FrameworkElement> CollectIdButtons()
    {
        var buttons = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        var visited = new HashSet<DependencyObject>();

        Visit(_window);

        return buttons;

        void Visit(DependencyObject node)
        {
            if (!visited.Add(node))
            {
                return;
            }

            if (node is FrameworkElement element
                && Hotkey.GetId(element) is { Length: > 0 } id)
            {
                // Первым записанный адрес остаётся: дубликат id — авторская оплошность,
                // и прежний FindById тоже возвращал первое совпадение обхода.
                buttons.TryAdd(id, element);
            }

            if (node is Visual or Visual3D)
            {
                var children = VisualTreeHelper.GetChildrenCount(node);

                for (var i = 0; i < children; i++)
                {
                    Visit(VisualTreeHelper.GetChild(node, i));
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                // Не-Visual не обходим: кнопки там нет, а VisualTreeHelper бросил бы (ловушка 25).
                if (child is (Visual or Visual3D))
                {
                    Visit(child);
                }
            }
        }
    }

    /// <summary>Нажимает кнопку целиком: клавиша равнозначна клику мышью.</summary>
    private static void PressButton(FrameworkElement element)
    {
        // Команда с ложным CanExecute глушит и кнопку, а Invoke на невключённом элементе
        // бросает ElementNotEnabledException: клавиша равнозначна клику мышью, а клик по
        // выключенной кнопке — тишина.
        if (!element.IsEnabled)
        {
            return;
        }

        // Через peer любого ButtonBase, а не ButtonAutomationPeer конкретно Button: так же
        // нажимаются RepeatButton и ToggleButton, а Invoke выполняет команду с её CanExecute
        // и ReadLocalValue семантики IsDefault/IsCancel — RaiseEvent(Click) команды не зовёт (ловушка 7).
        if (UIElementAutomationPeer.CreatePeerForElement(element)
                ?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoker)
        {
            invoker.Invoke();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e) => Release();

    /// <summary>Отписывается от событий окна и сервиса: утечки подписками недопустимы.</summary>
    private void Release()
    {
        _window.Closed -= OnWindowClosed;
        _hotkeys.Changed -= Rebuild;
    }

    /// <summary>Обёртка исполнителя под <see cref="KeyBinding"/>: гасить нечем, гасит сама команда.</summary>
    /// <remarks>CanExecute всегда true: неизбежные отказы гасятся в исполнителях — строка без
    /// выделения молчит, выключенная кнопка молчит в <see cref="PressButton"/>.</remarks>
    private sealed class Executor(Action invoke) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => invoke();
    }
}
