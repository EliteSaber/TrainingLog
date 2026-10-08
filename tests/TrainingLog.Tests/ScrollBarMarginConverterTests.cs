using System.Windows;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Отступ рамки строки дня, который снимается, пока вертикальная полоса на экране. Проверяется
/// чистая функция, без окна на экране: привязка внутри шаблона строки глазами и не ловится.
/// </summary>
public sealed class ScrollBarMarginConverterTests
{
    /// <summary>
    /// Полоса на месте — правого отступа нет: она стоит у края окна, и воздух между ней и
    /// таблицей читался бы как разрыв, которого слева нет.
    /// </summary>
    [Fact]
    public void Resolve_ПолосаНаМесте_ПравогоОтступаНет()
    {
        Assert.Equal(new Thickness(16, 0, 0, 0), ScrollBarMarginConverter.Resolve(Visibility.Visible, 16));
    }

    /// <summary>
    /// Полосы нет — отступы симметричные, как их и задаёт разметка.
    /// </summary>
    [Fact]
    public void Resolve_ПолосыНет_ОтступыСимметричные()
    {
        Assert.Equal(new Thickness(16, 0, 16, 0), ScrollBarMarginConverter.Resolve(Visibility.Collapsed, 16));
    }

    /// <summary>
    /// <c>Hidden</c> полосой не считается: скролл отдаёт <c>Visible</c> или <c>Collapsed</c>,
    /// а на любой другой ответ правило даёт запас с обеих сторон.
    /// </summary>
    [Fact]
    public void Resolve_ПолосаСкрыта_ОтступыСимметричные()
    {
        Assert.Equal(new Thickness(16, 0, 16, 0), ScrollBarMarginConverter.Resolve(Visibility.Hidden, 16));
    }

    /// <summary>
    /// Величина отступа приходит из разметки, а не зашита в правило: иначе смена отступа окна
    /// разъезжалась бы с числом в коде.
    /// </summary>
    [Fact]
    public void Resolve_ДругойОтступ_УчитываетПереданный()
    {
        Assert.Equal(new Thickness(8, 0, 0, 0), ScrollBarMarginConverter.Resolve(Visibility.Visible, 8));
    }
}