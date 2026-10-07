using System.Windows;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Правило положения подсказки в окне: центры совпадают, а выход за границу сдвигает ровно на
/// величину выхода. Проверяется чистая функция — сценарий с живым <c>Popup</c> без поднятия окна
/// не проверяется в принципе.
/// </summary>
public sealed class PopupPlacementTests
{
    private const double Gap = 4;

    /// <summary>
    /// Поле «Количество дней» в шапке главного окна: окно 900 px минус отступ 16, поле у правого
    /// края шапки. Числа взяты из разметки — правило проверяется на той геометрии, ради которой
    /// и написано.
    /// </summary>
    private static readonly Rect Field = new(687, 16, 52, 22);

    private static readonly Rect Window = new(16, 16, 868, 488);

    /// <summary>Подсказка шире поля, но заметно уже окна.</summary>
    private static readonly Size Hint = new(270, 40);

    /// <summary>Предел, ниже которого подсказка уже нечитаема: тест берёт тот же, что контрол.</summary>
    private const double MinimumWidth = 120;

    /// <summary>
    /// Ширина текста в одну строку, заведомо шире любого предела в этих тестах: проверки предела
    /// от него не зависят.
    /// </summary>
    private const double Width = 900;

    /// <summary>Размер рисунка стрелки, тест берёт те же, что контрол.</summary>
    /// <remarks>
    /// Правило прижима работает для любой ширины, так что на результат проверок это не влияет и
    /// нужно только ради единообразия с контролом.
    /// </remarks>
    private const double ArrowWidth = 18;

    /// <summary>Отступ стрелки от краёв блока, тест берёт тот же, что контрол.</summary>
    private const double ArrowInset = 4;

    [Fact]
    public void Resolve_УзкаяПодсказка_ЦентрСовпадаетСКентромПоля()
    {
        var popup = new Size(120, 40);

        var placement = PopupPlacement.Resolve(Field, popup, Window, Gap);

        Assert.Equal(Field.Left + (Field.Width - popup.Width) / 2, placement.X);
    }

    /// <summary>
    /// Обычный случай: подсказка шире поля, поэтому она свисает влево от него, но центры всё
    /// равно совпадают и в окно целиком помещается — сдвигать нечего.
    /// </summary>
    [Fact]
    public void Resolve_ШирокаяПодсказка_ЦентрВсёЕщёСовпадает()
    {
        var placement = PopupPlacement.Resolve(Field, Hint, Window, Gap);

        Assert.Equal(Field.Left + (Field.Width - Hint.Width) / 2, placement.X);
        Assert.True(placement.X + Hint.Width <= Window.Right);
    }

    /// <summary>
    /// Поле у самого правого края окна: подсказка вышла за границу и сдвигается влево ровно на
    /// величину выхода — прижимать её к краю с зазором было бы хуже.
    /// </summary>
    [Fact]
    public void Resolve_ВышелПравыйКрай_СдвигРовноНаВыход()
    {
        var target = new Rect(820, 16, 40, 22);
        var popup = new Size(200, 40);

        var centered = target.Left + (target.Width - popup.Width) / 2;
        var overflow = (centered + popup.Width) - Window.Right;

        var placement = PopupPlacement.Resolve(target, popup, Window, Gap);

        Assert.True(overflow > 0);
        Assert.Equal(centered - overflow, placement.X);
        Assert.Equal(Window.Right - popup.Width, placement.X);
    }

    /// <summary>Поле у самого левого края окна — симметрично, сдвиг вправо ровно на выход.</summary>
    [Fact]
    public void Resolve_ВышелЛевыйКрай_СдвигРовноНаВыход()
    {
        var target = new Rect(Window.Left, 16, 40, 22);
        var popup = new Size(200, 40);

        var placement = PopupPlacement.Resolve(target, popup, Window, Gap);

        Assert.Equal(Window.Left, placement.X);
    }

    /// <summary>
    /// Шириной подсказки управляет вызывающий, но если предел не сработал, правило всё равно не
    /// выпускает её за левый край окна.
    /// </summary>
    [Fact]
    public void Resolve_ПодсказкаШиреОкна_ПрижатаКЛевомуКраю()
    {
        var placement = PopupPlacement.Resolve(Field, new Size(900, 40), Window, Gap);

        Assert.Equal(Window.Left, placement.X);
    }
    [Fact]
    public void Resolve_СнизуЕстьМесто_СтоитПодПолемСЗазором()
    {
        var placement = PopupPlacement.Resolve(Field, Hint, Window, Gap);

        Assert.Equal(Field.Bottom + Gap, placement.Y);
    }

    /// <summary>Поле у нижнего края окна: подсказка сдвигается вверх ровно на величину выхода.</summary>
    [Fact]
    public void Resolve_ВышелНижнийКрай_СдвигРовноНаВыход()
    {
        var target = new Rect(687, Window.Bottom - 20, 52, 20);

        var placement = PopupPlacement.Resolve(target, Hint, Window, Gap);

        Assert.Equal(Window.Bottom - Hint.Height, placement.Y);
    }

    /// <summary>
    /// Подсказка выше окна целиком: сдвигать вверх больше некуда, и она прижимается к верхнему
    /// краю — накладываться приходится на содержимое окна, но не выходить за его пределы.
    /// </summary>
    [Fact]
    public void Resolve_ПодсказкаВышеОкна_ПрижатаКВерхнемуКраю()
    {
        var target = new Rect(687, Window.Bottom - 10, 52, 10);

        var placement = PopupPlacement.Resolve(target, new Size(270, Window.Height + 100), Window, Gap);

        Assert.Equal(Window.Top, placement.Y);
    }

    /// <summary>Зазор между полем и подсказкой задаётся вызывающим и соблюдается.</summary>
    [Theory]
    [InlineData(0d)]
    [InlineData(4d)]
    [InlineData(12d)]
    public void Resolve_ЗазорСоблюдается(double gap)
    {
        var placement = PopupPlacement.Resolve(Field, Hint, Window, gap);

        Assert.Equal(Field.Bottom + gap, placement.Y);
    }

    /// <summary>
    /// Места по обе стороны от центра поля хватает — предел ширины не ограничивает ничего.
    /// </summary>
    [Fact]
    public void ResolveWidth_МестаХватает_ПределНеОграничивает()
    {
        var width = PopupPlacement.ResolveWidth(Field, Window, Width, MinimumWidth);

        Assert.True(width > Hint.Width);
    }

    /// <summary>
    /// Поле у правого края окна: предел равен удвоенному расстоянию от центра поля до края, и
    /// блок в этих границах центрируется без сдвига.
    /// </summary>
    [Fact]
    public void ResolveWidth_ПолеУПравогоКрая_ПределОтЦентраДоКрая()
    {
        var target = new Rect(764, 16, 40, 22);
        var center = target.Left + (target.Width / 2);

        var width = PopupPlacement.ResolveWidth(target, Window, Width, MinimumWidth);

        Assert.Equal(2 * (Window.Right - center), width);
    }

    [Fact]
    public void ResolveWidth_ПолеУЛевогоКрая_ПределСимметричен()
    {
        var target = new Rect(96, 16, 40, 22);
        var center = target.Left + (target.Width / 2);

        var width = PopupPlacement.ResolveWidth(target, Window, Width, MinimumWidth);

        Assert.Equal(2 * (center - Window.Left), width);
    }

    /// <summary>
    /// Поле у самого края: места меньше читаемой ширины, и правило отдаёт пол — центрирование
    /// тогда уступает, и работает сдвиг по границе.
    /// </summary>
    [Fact]
    public void ResolveWidth_МестаПочтиНет_ВозвращаетсяПол()
    {
        var target = new Rect(Window.Right - 40, 16, 20, 22);

        Assert.Equal(MinimumWidth, PopupPlacement.ResolveWidth(target, Window, Width, MinimumWidth));
    }

    /// <summary>
    /// Смысл предела: с такой шириной правило положения не сдвигает блок, то есть подсказка
    /// стоит по центру поля. Это и есть требование, ради которого предел и введён.
    /// </summary>
    [Fact]
    public void ResolveWidth_ПредельныйРазмер_ЦентрСохраняется()
    {
        var target = new Rect(764, 16, 40, 22);
        var width = PopupPlacement.ResolveWidth(target, Window, Width, MinimumWidth);

        var placement = PopupPlacement.Resolve(target, new Size(width, 40), Window, Gap);

        Assert.Equal(target.Left + (target.Width / 2), placement.X + (width / 2));
        Assert.True(placement.X >= Window.Left);
        Assert.True(placement.X + width <= Window.Right);
    }

    /// <summary>
    /// Текст короче предела: блок получает ширину текста, а не предел — иначе короткое сообщение
    /// стояло бы с пустым полем сбоку.
    /// </summary>
    [Fact]
    public void ResolveWidth_ТекстКорочеПредела_БерётсяТекст()
    {
        Assert.Equal(150, PopupPlacement.ResolveWidth(Field, Window, natural: 150, MinimumWidth));
    }

    /// <summary>
    /// Текст длиннее предела: блок сужается до предела и переносит текст по словам. Это и есть
    /// требование «любой текст помещается в окно».
    /// </summary>
    [Fact]
    public void ResolveWidth_ТекстДлиннееПредела_БерётсяПредел()
    {
        var center = Field.Left + (Field.Width / 2);
        var cap = 2 * Math.Min(center - Window.Left, Window.Right - center);

        Assert.Equal(cap, PopupPlacement.ResolveWidth(Field, Window, Width, MinimumWidth));
    }

    /// <summary>
    /// Блок не сдвинут: центр блока и центр поля совпадают, значит стрелка по центру поля стоит
    /// ровно по центру блока — обе формулировки дают одну и ту же точку.
    /// </summary>
    [Fact]
    public void ResolveArrowLeft_БлокНеСдвинут_СтрелкаПоЦентруБлокаИПоля()
    {
        var block = new Rect(Field.Left + (Field.Width - Hint.Width) / 2, 200, Hint.Width, Hint.Height);
        var center = Field.Left + (Field.Width / 2);

        var left = PopupPlacement.ResolveArrowLeft(block, center, ArrowWidth, ArrowInset);

        Assert.Equal(block.Left + ((block.Width - ArrowWidth) / 2), left);
    }

    /// <summary>
    /// Блок сдвинут к краю окна, так что центр поля уже не совпадает с центром блока. Стрелка
    /// обязана остаться на поле — указывать мимо значило бы указать на пустое место окна.
    /// </summary>
    [Fact]
    public void ResolveArrowLeft_БлокСДвинут_СтрелкаВсёЕщёНаПоле()
    {
        var block = new Rect(Window.Right - Hint.Width, 200, Hint.Width, Hint.Height);
        var center = Field.Left + (Field.Width / 2);

        var left = PopupPlacement.ResolveArrowLeft(block, center, ArrowWidth, ArrowInset);

        Assert.Equal(center - (ArrowWidth / 2), left);
    }

    /// <summary>
    /// Центр поля почти у края блока: без прижима стрелка свесилась бы с него. Прижим задан
    /// отступом, и в него же она упирается.
    /// </summary>
    [Fact]
    public void ResolveArrowLeft_ПолеУКраяБлока_СтрелкаВнутри()
    {
        var block = new Rect(300, 200, Hint.Width, Hint.Height);
        var center = block.Left + 2;

        var left = PopupPlacement.ResolveArrowLeft(block, center, ArrowWidth, ArrowInset);

        Assert.Equal(block.Left + ArrowInset, left);
        Assert.True(left + ArrowWidth <= block.Right);
    }

    /// <summary>
    /// Стрелка по центру поля ни при каких условиях не выходит за пределы блока: иначе у края
    /// окна она уезжала бы за подсказку и читалась как отдельный предмет.
    /// </summary>
    [Fact]
    public void ResolveArrowLeft_ПолеЗаБлоком_СтрелкаНеСвешивается()
    {
        var block = new Rect(100, 200, Hint.Width, Hint.Height);

        var left = PopupPlacement.ResolveArrowLeft(block, block.Right + 200, ArrowWidth, ArrowInset);

        Assert.Equal(block.Right - ArrowInset - ArrowWidth, left);
    }
}
