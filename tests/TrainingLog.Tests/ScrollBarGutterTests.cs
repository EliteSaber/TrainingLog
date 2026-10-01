using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Решение о зазоре слева от полосы прокрутки. Проверяется чистая функция, без окна на экране:
/// вся обвязка со свойством — это подписки на события, ошибку в которых заметят только глаза.
/// </summary>
public sealed class ScrollBarGutterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveGutter_ЗазорНеНастроен_ВозвращаетНоль(double configured)
    {
        Assert.Equal(0, ScrollBarGutter.ResolveGutter(configured, scrollableHeight: 40));
    }

    [Fact]
    public void ResolveGutter_ПрокручиватьНечего_ВозвращаетНоль()
    {
        Assert.Equal(0, ScrollBarGutter.ResolveGutter(2, scrollableHeight: 0));
    }

    [Fact]
    public void ResolveGutter_ЕстьПереполнение_ВозвращаетЗаданныйЗазор()
    {
        Assert.Equal(2, ScrollBarGutter.ResolveGutter(2, scrollableHeight: 40));
    }

    [Fact]
    public void ResolveGutter_ЕстьПереполнение_УчитываетДругойЗазор()
    {
        Assert.Equal(4, ScrollBarGutter.ResolveGutter(4, scrollableHeight: 40));
    }

    [Fact]
    public void ResolveGutter_РазмерыЕщёНеИзмерены_ВозвращаетНоль()
    {
        // До раскладки скролл не знает про переполнение, и зазор применяться не должен.
        Assert.Equal(0, ScrollBarGutter.ResolveGutter(2, scrollableHeight: 0));
    }
}