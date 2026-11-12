using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>Разбор записи комбинации и её canonical вид.</summary>
/// <remarks>
/// Файл комбинаций правят руками, поэтому отказоустойчивость правила проверяется в первую
/// очередь: негодная запись обязана быть «умолчанием», а не «исключением на старте».
/// </remarks>
public sealed class HotkeyRulesTests
{
    [Theory]
    [InlineData("Ctrl+S", HotkeyModifiers.Control, "S")]
    [InlineData("Shift+F1", HotkeyModifiers.Shift, "F1")]
    [InlineData("Alt+F4", HotkeyModifiers.Alt, "F4")]
    [InlineData("Win+D1", HotkeyModifiers.Windows, "D1")]
    [InlineData("F1", HotkeyModifiers.None, "F1")]
    public void TryParse_БерётМодификаторыИКлавишу(string text, HotkeyModifiers modifiers, string key)
    {
        Assert.True(HotkeyRules.TryParse(text, out var combo));
        Assert.Equal(modifiers, combo.Modifiers);
        Assert.Equal(key, combo.Key);
    }

    /// <summary>Синонимы имён модификаторов — это те же модификаторы, а не новые клавиши.</summary>
    [Theory]
    [InlineData("Control+S", HotkeyModifiers.Control)]
    [InlineData("CTRL+S", HotkeyModifiers.Control)]
    [InlineData("Windows+D1", HotkeyModifiers.Windows)]
    [InlineData("ctrl+shift+s", HotkeyModifiers.Control | HotkeyModifiers.Shift)]
    public void TryParse_СинонимыИРегистр(string text, HotkeyModifiers modifiers)
    {
        Assert.True(HotkeyRules.TryParse(text, out var combo));
        Assert.Equal(modifiers, combo.Modifiers);
    }

    /// <summary>Canonical вид, а не введённый: порядок модификаторов фиксированный.</summary>
    [Theory]
    [InlineData("ctrl+s", "Ctrl+S")]
    [InlineData("SHIFT+S", "Shift+S")]
    [InlineData("alt+ctrl+a", "Ctrl+Alt+A")]
    [InlineData("f10", "F10")]
    [InlineData("oem5", "Oem5")]
    public void Normalize_CanonicalВидИгнорируетРегистр(string text, string canonical)
    {
        Assert.Equal(canonical, HotkeyRules.Normalize(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+")]
    [InlineData("+S")]
    [InlineData("Ctrl++")]
    // Пробельные символы внутри частей запрещены: « Control+S » — не комбинация, а склейка со счётчиком.
    [InlineData(" Ctrl+S ")]
    [InlineData("Ctrl+ S")]
    [InlineData("Ctrl+Ctrl+S")]
    [InlineData("Control+Ctrl+S")]
    [InlineData("S+F1")]
    [InlineData("Ctrl+Shift+S+F1")]
    // Часть из одних цифр — значение перечисления Key, а не имя клавиши («1» — это Key.Cancel):
    // «999» в WPF-слое и вовсе неопределённое значение. Цифровые клавиши пишутся D0–D9.
    [InlineData("Ctrl+1")]
    [InlineData("Ctrl+999")]
    [InlineData("1")]
    [InlineData("1+Ctrl")]
    public void TryParse_НегодныеЗаписи_Отказ(string? text)
    {
        Assert.False(HotkeyRules.TryParse(text, out _));
        Assert.Equal(string.Empty, HotkeyRules.Normalize(text));
        Assert.False(HotkeyRules.IsValid(text));
    }

    /// <summary>Canonical строка собирается в порядке Ctrl, Shift, Alt, Win.</summary>
    [Fact]
    public void ToString_ПорядокМодификаторов()
    {
        var combo = new HotkeyCombo(
            HotkeyModifiers.Windows | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Control,
            "S");

        Assert.Equal("Ctrl+Shift+Alt+Win+S", combo.ToString());
    }
}
