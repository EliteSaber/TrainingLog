using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>Правило подсказки переполнения: когда текст не влезает, а когда tooltip не нужен.</summary>
/// <remarks>
/// Только чистая функция решения — поля и окна в тестах не поднимаются, показатели меряются
/// укладкой и приходят в класс значениями.
/// </remarks>
public sealed class OverflowTipTests
{
    /// <summary>Влезающий текст подсказки не получает: дублировать видимое незачем.</summary>
    [Fact]
    public void Overflows_ТекстПомещается_ПодсказкиНет()
    {
        Assert.False(OverflowTip.Overflows(120, 120));
    }

    /// <summary>
    /// Дробные показатели ScrollViewer у влезающего значения расходились на доли пикселя:
    /// разница в допуске — ещё не переполнение, иначе подсказка мерцала бы над каждым полем.
    /// </summary>
    [Fact]
    public void Overflows_РазницаРовноВДопуске_ПодсказкиНет()
    {
        Assert.False(OverflowTip.Overflows(120.4, 120));
    }

    /// <summary>Текст шире допуска — переполнение, наименьший случай.</summary>
    [Fact]
    public void Overflows_РазницаЛишьЧутьБольше_Подсказка()
    {
        Assert.True(OverflowTip.Overflows(120.6, 120));
    }

    /// <summary>Длинный путь в узком поле — обычный случай подсказки.</summary>
    [Fact]
    public void Overflows_ЗаметноеПереполнение_Подсказка()
    {
        Assert.True(OverflowTip.Overflows(900, 240));
    }

    /// <summary>Вычитание, а не модуль: отрицательной разницы без переполнения не бывает.</summary>
    [Fact]
    public void Overflows_ВидимаяЧастьШире_ПодсказкиНет()
    {
        Assert.False(OverflowTip.Overflows(80, 240));
    }
}
