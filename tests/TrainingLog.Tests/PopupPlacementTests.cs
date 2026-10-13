using System.Windows;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Правило положения подсказки: центры совпадают, а выход за границу сдвигает ровно на
/// величину выхода. Проверяется чистая функция — сценарий с живым <c>Popup</c> без поднятия окна
/// не проверяется в принципе.
/// </summary>
public sealed class PopupPlacementTests
{
    private const double Gap = 4;

    /// <summary>Поле «Количество дней» в шапке главного окна. Числа взяты из разметки.</summary>
    private static readonly Rect Field = new(687, 16, 52, 22);

    /// <summary>
    /// Содержимое главного окна в координатах окна. Начало не в нуле — рамка и заголовок сдвигают
    /// содержимое, и именно поэтому пределы нельзя брать у <c>Window.ActualHeight</c>: это размер
    /// окна целиком, и подсказка по нему уезжала за нижнюю границу.
    /// </summary>
    private static readonly Rect Content = new(0, 0, 900, 520);

    /// <summary>Рабочая область: содержимое окна минус отступ 16 по краям.</summary>
    private static readonly Rect Window = PopupPlacement.WorkArea(Content, inset: 16);

    /// <summary>Подсказка шире поля, но заметно уже окна.</summary>
    private static readonly Size Hint = new(270, 40);

    /// <summary>Предел, ниже которого подсказка уже нечитаема: тест берёт тот же, что контрол.</summary>
    private const double MinimumWidth = 120;

    /// <summary>Ширина текста, заведомо шире любого предела в этих тестах.</summary>
    private const double Width = 900;

    /// <summary>Размер рисунка стрелки, тест берёт те же, что контрол.</summary>
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

    /// <summary>Подсказка свисает влево от поля, но центры совпадают и в окно помещается — сдвигать нечего.</summary>
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

    [Fact]
    public void Resolve_ВышелЛевыйКрай_СдвигРовноНаВыход()
    {
        var target = new Rect(Window.Left, 16, 40, 22);
        var popup = new Size(200, 40);

        var placement = PopupPlacement.Resolve(target, popup, Window, Gap);

        Assert.Equal(Window.Left, placement.X);
    }

    /// <summary>Шириной подсказки управляет вызывающий, но правило всё равно не выпускает её за левый край.</summary>
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

    [Fact]
    public void Resolve_ВышелНижнийКрай_СдвигРовноНаВыход()
    {
        var target = new Rect(687, Window.Bottom - 20, 52, 20);

        var placement = PopupPlacement.Resolve(target, Hint, Window, Gap);

        Assert.Equal(Window.Bottom - Hint.Height, placement.Y);
    }

    /// <summary>Сдвигать вверх больше некуда: накладываться приходится на содержимое окна, но не выходить за его пределы.</summary>
    [Fact]
    public void Resolve_ПодсказкаВышеОкна_ПрижатаКВерхнемуКраю()
    {
        var target = new Rect(687, Window.Bottom - 10, 52, 10);

        var placement = PopupPlacement.Resolve(target, new Size(270, Window.Height + 100), Window, Gap);

        Assert.Equal(Window.Top, placement.Y);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(4d)]
    [InlineData(12d)]
    public void Resolve_ЗазорСоблюдается(double gap)
    {
        var placement = PopupPlacement.Resolve(Field, Hint, Window, gap);

        Assert.Equal(Field.Bottom + gap, placement.Y);
    }

    [Fact]
    public void ResolveWidth_МестаХватает_ПределНеОграничивает()
    {
        var width = PopupPlacement.ResolveWidth(Field, Window, Width, MinimumWidth);

        Assert.True(width > Hint.Width);
    }

    /// <summary>Предел равен удвоенному расстоянию от центра поля до края.</summary>
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

    /// <summary>Центрирование уступает, и работает сдвиг по границе.</summary>
    [Fact]
    public void ResolveWidth_МестаПочтиНет_ВозвращаетсяПол()
    {
        var target = new Rect(Window.Right - 40, 16, 20, 22);

        Assert.Equal(MinimumWidth, PopupPlacement.ResolveWidth(target, Window, Width, MinimumWidth));
    }

    /// <summary>Смысл предела: с такой шириной правило положения не сдвигает блок.</summary>
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

    /// <summary>Предел высоты — окно минус зазор до поля и минус поля блока.</summary>
    [Fact]
    public void ResolveMaxHeight_ОтнимаетЗазорИПоляБлока()
    {
        var chrome = 16;

        var height = PopupPlacement.ResolveMaxHeight(Window, Gap, chrome);

        Assert.Equal(Window.Height - Gap - chrome, height);
    }

    /// <summary>Без предела блок длиннее окна и правило прижимает его к верхнему краю — с уходом низа за окно.</summary>
    [Fact]
    public void ResolveMaxHeight_ПредельныйРазмер_БлокВлезаетВОкно()
    {
        var chrome = 16;
        var height = PopupPlacement.ResolveMaxHeight(Window, Gap, chrome);

        var placement = PopupPlacement.Resolve(Field, new Size(Hint.Width, height), Window, Gap);

        Assert.True(placement.Y >= Window.Top);
        Assert.True(placement.Y + height <= Window.Bottom);
    }

    /// <summary>Ноль, а не отрицательный предел, который в разметку не поставить.</summary>
    [Fact]
    public void ResolveMaxHeight_ОкноМеньшеПодсказки_Ноль()
    {
        Assert.Equal(0, PopupPlacement.ResolveMaxHeight(Window, Gap, chrome: 600));
    }

    /// <summary>Сдвиг содержимого рамкой и заголовком обязан сохраниться.</summary>
    [Fact]
    public void WorkArea_ОтступСоВсехСторон_НачалоСодержимогоСохраняется()
    {
        Assert.Equal(new Rect(16, 16, 868, 488), PopupPlacement.WorkArea(Content, inset: 16));
    }

    /// <summary>Нижняя граница — это граница содержимого окна, а не окна: иначе правило рисует подсказку за пределами окна.</summary>
    [Fact]
    public void WorkArea_ПределСодержимого_НеЗаходитНаЗаголовок()
    {
        var workArea = PopupPlacement.WorkArea(Content, inset: 16);

        Assert.Equal(Content.Height - 32, workArea.Height);
        Assert.True(workArea.Bottom <= Content.Bottom);
    }

    /// <summary>Точка, а не перевёрнутый прямоугольник: отрицательные размеры испортили бы сдвиг.</summary>
    [Fact]
    public void WorkArea_ОтступБольшеСодержимого_Точка()
    {
        var workArea = PopupPlacement.WorkArea(new Rect(0, 0, 20, 20), inset: 40);

        Assert.Equal(new Rect(40, 40, 0, 0), workArea);
    }

    /// <summary>Иначе короткое сообщение стояло бы с пустым полем сбоку.</summary>
    [Fact]
    public void ResolveWidth_ТекстКорочеПредела_БерётсяТекст()
    {
        Assert.Equal(150, PopupPlacement.ResolveWidth(Field, Window, natural: 150, MinimumWidth));
    }

    /// <summary>Блок сужается до предела и переносит текст по словам — требование «любой текст помещается в окно».</summary>
    [Fact]
    public void ResolveWidth_ТекстДлиннееПредела_БерётсяПредел()
    {
        var center = Field.Left + (Field.Width / 2);
        var cap = 2 * Math.Min(center - Window.Left, Window.Right - center);

        Assert.Equal(cap, PopupPlacement.ResolveWidth(Field, Window, Width, MinimumWidth));
    }

    /// <summary>Центры совпадают, значит обе формулировки дают одну и ту же точку.</summary>
    [Fact]
    public void ResolveArrowLeft_БлокНеСдвинут_СтрелкаПоЦентруБлокаИПоля()
    {
        var block = new Rect(Field.Left + (Field.Width - Hint.Width) / 2, 200, Hint.Width, Hint.Height);
        var center = Field.Left + (Field.Width / 2);

        var left = PopupPlacement.ResolveArrowLeft(block, center, ArrowWidth, ArrowInset);

        Assert.Equal(block.Left + ((block.Width - ArrowWidth) / 2), left);
    }

    /// <summary>Стрелка обязана остаться на поле: указывать мимо значило бы указать на пустое место окна.</summary>
    [Fact]
    public void ResolveArrowLeft_БлокСДвинут_СтрелкаВсёЕщёНаПоле()
    {
        var block = new Rect(Window.Right - Hint.Width, 200, Hint.Width, Hint.Height);
        var center = Field.Left + (Field.Width / 2);

        var left = PopupPlacement.ResolveArrowLeft(block, center, ArrowWidth, ArrowInset);

        Assert.Equal(center - (ArrowWidth / 2), left);
    }

    /// <summary>Прижим задан отступом, и в него же она упирается.</summary>
    [Fact]
    public void ResolveArrowLeft_ПолеУКраяБлока_СтрелкаВнутри()
    {
        var block = new Rect(300, 200, Hint.Width, Hint.Height);
        var center = block.Left + 2;

        var left = PopupPlacement.ResolveArrowLeft(block, center, ArrowWidth, ArrowInset);

        Assert.Equal(block.Left + ArrowInset, left);
        Assert.True(left + ArrowWidth <= block.Right);
    }

    /// <summary>Иначе у края окна стрелка уехала бы за подсказку и читалась как отдельный предмет.</summary>
    [Fact]
    public void ResolveArrowLeft_ПолеЗаБлоком_СтрелкаНеСвешивается()
    {
        var block = new Rect(100, 200, Hint.Width, Hint.Height);

        var left = PopupPlacement.ResolveArrowLeft(block, block.Right + 200, ArrowWidth, ArrowInset);

        Assert.Equal(block.Right - ArrowInset - ArrowWidth, left);
    }
}
