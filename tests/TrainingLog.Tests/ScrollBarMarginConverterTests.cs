using System.Windows;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Отступ рамки строки дня, который снимается, пока вертикальная полоса на экране. Проверяется
/// чистая функция, без окна на экране: привязка внутри шаблона строки глазами и не ловится.
/// </summary>
public sealed class ScrollBarMarginConverterTests
{
    /// <summary>Воздух между полосой и таблицей читался бы как разрыв, которого слева нет.</summary>
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

    /// <summary>Скролл отдаёт <c>Visible</c> или <c>Collapsed</c>, на любой другой ответ — запас с обеих сторон.</summary>
    [Fact]
    public void Resolve_ПолосаСкрыта_ОтступыСимметричные()
    {
        Assert.Equal(new Thickness(16, 0, 16, 0), ScrollBarMarginConverter.Resolve(Visibility.Hidden, 16));
    }

    /// <summary>Иначе смена отступа окна разъезжалась бы с числом в коде.</summary>
    [Fact]
    public void Resolve_ДругойОтступ_УчитываетПереданный()
    {
        Assert.Equal(new Thickness(8, 0, 0, 0), ScrollBarMarginConverter.Resolve(Visibility.Visible, 8));
    }
}