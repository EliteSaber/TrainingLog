using System.ComponentModel;
using System.Windows.Input;
using TrainingLog.Core.Settings;

namespace TrainingLog.Services;

/// <summary>Назначения горячих клавиш: файл, умолчания, пересборка по событию.</summary>
/// <remarks>
/// Приоритет выбрать так: назначенное годной строкой из файла, затем умолчание из каталога, затем
/// ничего — как у DaysCount вне диапазона значение в памяти подменяется умолчанием.
/// Событие <see cref="Changed"/> поднимается после Accepted окна параметров: живущие окна
/// (главное, справочники через хост, окно дня) пересобирают привязки, диалогам пересборка не нужна.
/// </remarks>
public sealed class HotkeyService
{
    private readonly AppSettingsStore _store;

    /// <summary>Назначения из файла: id к KeyGesture или null, если файла нет/строка негодна.</summary>
    private Dictionary<string, KeyGesture?> _assigned = [];

    public event Action? Changed;

    public HotkeyService(AppSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;

        Reload();
    }

    /// <summary>Перечитывает назначения из файла. Созывается после записи файла окном параметров.</summary>
    public void Reload() =>
        _assigned = _store.Load().Hotkeys.ToDictionary(pair => pair.Key, pair => ToGesture(pair.Value));

    /// <summary>Действующее назначение: назначенное в файле, затем умолчание, затем ничего.</summary>
    public KeyGesture? Resolve(string id)
    {
        // Назначение годно, только если переводилось в жест: негодная дала null.
        if (_assigned.TryGetValue(id, out var assigned) && assigned is not null)
        {
            return assigned;
        }

        var action = HotkeyCatalog.ActionOf(id);

        return action is null ? null : ToGesture(action.DefaultGesture);
    }

    /// <summary>Настройки записаны: перечитать и поднять <see cref="Changed"/>.</summary>
    public void NotifyChanged()
    {
        Reload();

        Changed?.Invoke();
    }

    /// <summary>Совпадает ли нажатие с жестом: клавиша и набор модификаторов рядом.</summary>
    /// <remarks>
    /// Чистая функция, а не условие в обработчике: правило проверяется тестом. Клавиша живёт
    /// в жесте единственной, а модификаторы сравниваются целиком — «Ctrl+Up» не совпадает
    /// с нажатым «Up», частичных наборов между ними не бывает.
    /// </remarks>
    public static bool Matches(KeyGesture gesture, Key key, ModifierKeys modifiers) =>
        gesture.Key == key && gesture.Modifiers == modifiers;

    /// <summary>Canonical-строку комбинации переводит в KeyGesture; негодную — null.</summary>
    private static KeyGesture? ToGesture(string? text)
    {
        // IsDefined после TryParse: парсинг принимает и числовые значения перечисления,
        // а клавиша должна быть именованной. Числа отсекает правило разбора, IsDefined —
        // страховка на случай других неопределённых значений.
        if (!HotkeyRules.TryParse(text, out var combo)
            || !Enum.TryParse(combo.Key, ignoreCase: true, out Key key)
            || !Enum.IsDefined(key))
        {
            return null;
        }

        try
        {
            return new KeyGesture(key, ToModifiers(combo.Modifiers));
        }
        // Клавиши без модификаторов KeyGesture принимает не все: буквы требуют модификатор.
        // Такое назначение нигде не заработало бы, и считаем его умолчанием, а не ошибкой.
        catch (Exception exception) when (exception is NotSupportedException or InvalidEnumArgumentException)
        {
            return null;
        }
    }

    private static ModifierKeys ToModifiers(HotkeyModifiers modifiers)
    {
        var result = ModifierKeys.None;

        if ((modifiers & HotkeyModifiers.Control) != 0)
        {
            result |= ModifierKeys.Control;
        }

        if ((modifiers & HotkeyModifiers.Shift) != 0)
        {
            result |= ModifierKeys.Shift;
        }

        if ((modifiers & HotkeyModifiers.Alt) != 0)
        {
            result |= ModifierKeys.Alt;
        }

        if ((modifiers & HotkeyModifiers.Windows) != 0)
        {
            result |= ModifierKeys.Windows;
        }

        return result;
    }
}
