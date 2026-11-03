namespace TrainingLog.Core.Settings;

/// <summary>Модификаторы комбинации: свой набор, WPF-типов у ядра нет.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1 << 0,
    Shift = 1 << 1,
    Alt = 1 << 2,
    Windows = 1 << 3,
}

/// <summary>Разобранная комбинация: набор модификаторов и одна клавиша.</summary>
public readonly record struct HotkeyCombo(HotkeyModifiers Modifiers, string Key)
{
    /// <summary>Canonical вид, тот же, что хранится в файле: «Ctrl+Shift+S».</summary>
    public override string ToString() => HotkeyRules.ToString(this);
}

/// <summary>Правило горячих клавиш: разбор записи, canonical вид, пригодность.</summary>
/// <remarks>
/// Клавиша — имя из <c>Keyboard.Key</c> («S», «F1», «Oem5…»): полным списком клавиш правило
/// не владеет, поэтому проверяет только форму записи, а перевод имени в KeyGesture делает
/// WPF-слой, и неудавшийся перевод даёт умолчание вместо назначения.
///
/// В файле комбинации хранятся в canonical виде, и им же сравниваются конфликты: «F1» и «f1» —
/// одна комбинация, а не две.
/// </remarks>
public static class HotkeyRules
{
    /// <summary>Разбирает запись или отвечает отказом.</summary>
    /// <remarks>
    /// Отказы: пусто, нулевые части («Ctrl+», «+S»), пробельные символы (в том числе
    /// « Control+S »), повтор модификатора («Ctrl+Ctrl»), две клавиши («S+F1») и ни одной
    /// клавиши («Ctrl») — Enter и Esc как комбинации сюда не попадают по построению поля
    /// захвата, а не по запрету здесь. Регистр частей не имеет значения.
    /// </remarks>
    public static bool TryParse(string? text, out HotkeyCombo combo)
    {
        combo = default;

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var parts = text.Split('+');

        if (parts.Any(p => p.Length == 0 || p.Any(char.IsWhiteSpace)))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        List<string>? keys = null;

        foreach (var part in parts)
        {
            var modifier = RecognizeModifier(part);

            if (modifier is not null)
            {
                // Повтор того же модификатора уже задан, и что-то с ним делать нечем.
                if ((modifiers & modifier.Value) != 0)
                {
                    return false;
                }

                modifiers |= modifier.Value;

                continue;
            }

            (keys ??= []).Add(part);
        }

        if (keys is not { Count: 1 })
        {
            return false;
        }

        combo = new HotkeyCombo(modifiers, CanonicalKey(keys[0]));

        return true;
    }

    /// <summary>Canonical вид записи «Ctrl+Shift+S»; негодная запись отдаёт пустую строку.</summary>
    public static string Normalize(string? text) =>
        TryParse(text, out var combo) ? ToString(combo) : string.Empty;

    /// <summary>Пригодна ли запись к сохранению в файл: всё, что разбирается.</summary>
    public static bool IsValid(string? text) => TryParse(text, out _);

    /// <summary>Canonical строка: модификаторы в порядке Ctrl, Shift, Alt, Win, за ними клавиша.</summary>
    public static string ToString(HotkeyCombo combo)
    {
        List<string> parts = [];

        if ((combo.Modifiers & HotkeyModifiers.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((combo.Modifiers & HotkeyModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((combo.Modifiers & HotkeyModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((combo.Modifiers & HotkeyModifiers.Windows) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(combo.Key);

        return string.Join("+", parts);
    }

    // Синонимы уже встречаются в чужих записях: Control рядом с Ctrl, Windows рядом с Win.
    // Показывать их канонически всё равно нужно первым именем.
    private static HotkeyModifiers? RecognizeModifier(string part) => part.ToLowerInvariant() switch
    {
        "ctrl" or "control" => HotkeyModifiers.Control,
        "shift" => HotkeyModifiers.Shift,
        "alt" => HotkeyModifiers.Alt,
        "win" or "windows" => HotkeyModifiers.Windows,
        _ => null,
    };

    // Имя клавиши сравнивается без регистра и Enum-парсингом в WPF-слое, поэтому канонический
    // регистр нужен только для показа и хранения: первая буква большая, у одиночной — вся.
    private static string CanonicalKey(string key) =>
        char.ToUpperInvariant(key[0]) + key[1..];
}
