using System.Windows.Controls;
using System.Windows.Input;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>Снятие фокуса с поля по клавишам: перечень и правило решения.</summary>
/// <remarks>
/// Поведение повторяет устройство ClickToFocus: ядро — чистые функции, обвязку — подписку
/// на событие — тест не ловит в принципе. Сама постановка фокуса в STA-тесте не проверяется:
/// без PresentationSource клавиатурный фокус не ставится.
/// </remarks>
public sealed class BlurOnKeysTests
{
    /// <summary>Годный перечень разбирается с сохранением порядка: дубли не переупорядочивать.</summary>
    [Fact]
    public void TryReadKeys_ГодныйПеречень_Разбирается()
    {
        Assert.True(BlurOnKeys.TryReadKeys("Escape,Enter", out var keys));
        Assert.Equal(2, keys.Count);
        Assert.Equal(Key.Escape, keys[0]);
        Assert.Equal(Key.Enter, keys[1]);
    }

    /// <summary>Пробелы вокруг частей снимаются, регистр не важен: запись для глаз, не для машины.</summary>
    [Fact]
    public void TryReadKeys_ПробелыИРегистр_Нормируются()
    {
        Assert.True(BlurOnKeys.TryReadKeys(" escape , ENTER ", out var keys));
        Assert.Equal(2, keys.Count);
        Assert.Equal(Key.Escape, keys[0]);
        Assert.Equal(Key.Enter, keys[1]);
    }

    /// <summary>Пусто фокус не снимает вовсе: перечень обязателен.</summary>
    [Fact]
    public void TryReadKeys_Пусто_Отказ()
    {
        Assert.False(BlurOnKeys.TryReadKeys(null, out var keys));
        Assert.Empty(keys);

        Assert.False(BlurOnKeys.TryReadKeys("", out keys));
        Assert.Empty(keys);

        Assert.False(BlurOnKeys.TryReadKeys("   ", out keys));
        Assert.Empty(keys);
    }

    /// <summary>Мусорная часть отдаёт отказ целиком: частично молчащий перечень хуже явного отказа.</summary>
    [Fact]
    public void TryReadKeys_МусорнаяЧасть_Отказ()
    {
        Assert.False(BlurOnKeys.TryReadKeys("Escape,Мусор", out var keys));
        Assert.Empty(keys);
    }

    /// <summary>Повтор клавиши — отказ по духу HotkeyRules: дубль не несёт смысла.</summary>
    [Fact]
    public void TryReadKeys_Дубль_Отказ()
    {
        Assert.False(BlurOnKeys.TryReadKeys("Escape,Escape", out var keys));
        Assert.Empty(keys);
    }

    /// <summary>ContainsKey ищет по разобранному перечню, а не по строке записи.</summary>
    [Fact]
    public void ContainsKey_РазбираетПоКлавише()
    {
        BlurOnKeys.TryReadKeys("Escape,Enter", out var keys);

        Assert.True(BlurOnKeys.ContainsKey(keys, Key.Escape));
        Assert.True(BlurOnKeys.ContainsKey(keys, Key.Enter));
        Assert.False(BlurOnKeys.ContainsKey(keys, Key.Tab));
    }

    /// <summary>Правило решения: всё вместе — и цель, и фокус на элементе, и клавиша в перечне.</summary>
    [Fact]
    public void ShouldRelease_ТребуетВсёВместе()
    {
        // Всё на месте: фокус снимается.
        Assert.True(BlurOnKeys.ShouldRelease(hasTarget: true, elementFocused: true, keyListed: true));

        // По одному не хватает: цель и фокус на элементе, и клавиша в перечне — каждое отдельное.
        Assert.False(BlurOnKeys.ShouldRelease(hasTarget: false, elementFocused: true, keyListed: true));
        Assert.False(BlurOnKeys.ShouldRelease(hasTarget: true, elementFocused: false, keyListed: true));
        Assert.False(BlurOnKeys.ShouldRelease(hasTarget: true, elementFocused: true, keyListed: false));

        // Ничего нет.
        Assert.False(BlurOnKeys.ShouldRelease(hasTarget: false, elementFocused: false, keyListed: false));
    }

    /// <summary>Применение и снятие перечня не бросает: подписка перепривязывается заново.</summary>
    [Fact]
    public void Keys_ПрименениеИСнятие_НеБросает()
    {
        StaRunner.Run(() =>
        {
            var box = new TextBox();

            BlurOnKeys.SetKeys(box, "Escape,Enter");
            BlurOnKeys.SetKeys(box, "Escape");
            BlurOnKeys.SetKeys(box, null);
        });
    }
}
