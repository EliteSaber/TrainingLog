using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Решение о передаче фокуса по клику на подпись. Проверяется чистая функция: элементы WPF
/// в тестах не создаются, для них нужна STA-нитка, а в проекте такой обвязки нет.
/// </summary>
public sealed class ClickToFocusTests
{
    [Fact]
    public void ShouldFocus_ЦелиНет_НеПередавать()
    {
        Assert.False(ClickToFocus.ShouldFocus(hasTarget: false, targetFocused: false, targetEnabled: true));
    }

    [Fact]
    public void ShouldFocus_ЦельУжеВФокусе_НеПередавать()
    {
        // Повторная передача сбросила бы каретку: цель и так активна.
        Assert.False(ClickToFocus.ShouldFocus(hasTarget: true, targetFocused: true, targetEnabled: true));
    }

    [Fact]
    public void ShouldFocus_ЦельОтключена_НеПередавать()
    {
        Assert.False(ClickToFocus.ShouldFocus(hasTarget: true, targetFocused: false, targetEnabled: false));
    }

    [Fact]
    public void ShouldFocus_ЦельСвободна_Передавать()
    {
        Assert.True(ClickToFocus.ShouldFocus(hasTarget: true, targetFocused: false, targetEnabled: true));
    }
}
