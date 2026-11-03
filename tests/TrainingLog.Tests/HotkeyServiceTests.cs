using System.Windows.Input;
using TrainingLog.Core.Data;
using TrainingLog.Core.Settings;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>Действующее назначение: приоритет файла над умолчанием и отказ негодных строк.</summary>
public sealed class HotkeyServiceTests
{
    /// <summary>Записанного назначения нет — действует умолчание; у остальных — ничего.</summary>
    [Fact]
    public void Resolve_НазначенияНет_УмолчаниеИлиНичего()
    {
        using var settings = new TemporarySettings();
        var service = new HotkeyService(settings.Store);

        var gesture = service.Resolve("Day.Save");

        Assert.NotNull(gesture);
        Assert.Equal(Key.S, gesture.Key);
        Assert.Equal(ModifierKeys.Control, gesture.Modifiers);
        Assert.Null(service.Resolve("Exercises.Add"));
    }

    /// <summary>Годная запись файла сильнее умолчания.</summary>
    [Fact]
    public void Resolve_ЗаписьВФайле_СильнееУмолчания()
    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
        {
            Hotkeys = new Dictionary<string, string> { ["Day.Save"] = "Ctrl+Shift+S" },
        });

        var service = new HotkeyService(settings.Store);

        var gesture = service.Resolve("Day.Save");

        Assert.NotNull(gesture);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, gesture.Modifiers);
        Assert.Equal(Key.S, gesture.Key);
    }

    /// <summary>Назначение действия без умолчания тоже работает.</summary>
    [Fact]
    public void Resolve_НазначениеБезУмолчания()
    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
        {
            Hotkeys = new Dictionary<string, string> { ["Exercises.Add"] = "F2" },
        });

        Assert.Equal(Key.F2, new HotkeyService(settings.Store).Resolve("Exercises.Add")!.Key);
    }

    /// <summary>Негодная строка файла и неизвестное имя клавиши — умолчание, а не исключение.</summary>
    /// <remarks>
    /// Файл правят руками: «Ctrl+» — чайник не дописал, «SpaceInvaders» — опечатка. Обе записи
    /// обязаны пропасть в умолчание, а не уронить запуск.
    /// </remarks>
    [Theory]
    [InlineData("Ctrl+")]
    [InlineData(" SpaceInvaders ")]
    [InlineData("SpaceInvaders")]
    public void Resolve_НегоднаяСтрока_Умолчание(string text)
    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
        {
            Hotkeys = new Dictionary<string, string> { ["Day.Save"] = text },
        });

        var service = new HotkeyService(settings.Store);

        var gesture = service.Resolve("Day.Save");

        Assert.NotNull(gesture);
        Assert.Equal(Key.S, gesture.Key);
        Assert.Equal(ModifierKeys.Control, gesture.Modifiers);
    }

    /// <summary>Букве без модификатора WPF-жест не переводится: считается умолчанием.</summary>
    [Fact]
    public void Resolve_БукваБезМодификаторов_Умолчание()
    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
        {
            Hotkeys = new Dictionary<string, string> { ["Day.Save"] = "S" },
        });

        Assert.Equal(ModifierKeys.Control, new HotkeyService(settings.Store).Resolve("Day.Save")!.Modifiers);
    }

    /// <summary>Уведомление после перечитывания: живущие окна пересобирают привязки.</summary>
    [Fact]
    public void NotifyChanged_ПеречитываетФайлИСообщает()
    {
        using var settings = new TemporarySettings();
        var service = new HotkeyService(settings.Store);

        var changed = 0;
        service.Changed += () => changed++;

        settings.Store.Save(new AppSettings
        {
            Hotkeys = new Dictionary<string, string> { ["Exercises.Add"] = "F3" },
        });

        Assert.Null(service.Resolve("Exercises.Add"));

        service.NotifyChanged();

        Assert.Equal(1, changed);
        Assert.Equal(Key.F3, service.Resolve("Exercises.Add")!.Key);
    }
}
