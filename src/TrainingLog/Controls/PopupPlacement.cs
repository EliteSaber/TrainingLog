using System.Windows;

namespace TrainingLog.Controls;

/// <summary>
/// Где поставить всплывающую подсказку, чтобы она осталась в окне. Правило вынесено в чистые
/// функции: координаты и размеры приходят аргументами, проверяются тестом и не требуют поднимать окно.
/// </summary>
/// <remarks>
/// За границы окна оверлей выйти не может в принципе — он нарисован внутри окна, — но правило
/// осталось: подсказка должна ещё и помещаться рядом с полем, а не наезжать на него и не упираться
/// в край.
/// </remarks>
public static class PopupPlacement
{
    /// <summary>Положение подсказки в координатах окна.</summary>
    /// <param name="target">Поле, к которому привязана подсказка.</param>
    /// <param name="popup">Размер подсказки.</param>
    /// <param name="container">Пределы окна в тех же координатах.</param>
    /// <param name="gap">Зазор между полем и подсказкой.</param>
    /// <returns>Левый верхний угол подсказки.</returns>
    /// <remarks>
    /// Подсказка стоит под полем, и центры у них совпадают: так она читается как продолжение поля.
    /// Дальше по одному правилу на ось: **что вышло за границу окна, сдвигается ровно на величину
    /// выхода в противоположную сторону.** Ровно на величину, а не «прижать к краю»: прижатие
    /// притаило бы подсказку к границе там, где хватило бы сдвига на несколько пикселей.
    ///
    /// Проверки идут подряд, а не через <c>else</c>: подсказку может выносить с обеих сторон
    /// разом, и каждая загоняется своя. По вертикали переворот над полем не делается — накрывать то
    /// самое поле хуже, чем встать наравне с ним; при <c>MinHeight="360"</c> место под полем в шапке
    /// есть всегда, так что это страховка.
    /// </remarks>
    public static Point Resolve(Rect target, Size popup, Rect container, double gap)
    {
        var left = target.Left + (target.Width - popup.Width) / 2;
        var top = target.Bottom + gap;

        if (left + popup.Width > container.Right)
        {
            left -= (left + popup.Width) - container.Right;
        }

        if (left < container.Left)
        {
            left = container.Left;
        }

        if (top + popup.Height > container.Bottom)
        {
            top -= (top + popup.Height) - container.Bottom;
        }

        if (top < container.Top)
        {
            top = container.Top;
        }

        return new Point(left, top);
    }

    /// <summary>Ширина блока: какая помещается рядом с полем по центру и не шире самого текста.</summary>
    /// <remarks>
    /// Поле в окне дня узкое, и половины ширины подсказки у поля у самого края окна попросту не
    /// помещается. Тогда сдвиг в <see cref="Resolve"/> сбил бы центрирование, а сужаться должен
    /// блок, а не двигаться: текст переносится по словам, центр остаётся на поле.
    ///
    /// Формула — ровно условие, при котором блок влезает по обе стороны от центра поля. Сверху ещё
    /// <paramref name="natural"/>: подсказка не растягивается до предела, если текст короче, иначе
    /// стояла бы с пустым полем сбоку от короткого сообщения.
    /// </remarks>
    /// <param name="target">Поле, под которым показывается подсказка.</param>
    /// <param name="container">Пределы окна в тех же координатах.</param>
    /// <param name="natural">Ширина текста без предела: во что он встаёт в одну строку.</param>
    /// <param name="minimum">Ширина, ниже которой подсказка становится нечитаемой.</param>
    /// <returns>Ширина блока.</returns>
    public static double ResolveWidth(Rect target, Rect container, double natural, double minimum)
    {
        var center = target.Left + (target.Width / 2);
        var cap = 2 * Math.Min(center - container.Left, container.Right - center);

        // Пол внизу: центрирование тогда уступает, и работает сдвиг по границе из Resolve.
        return Math.Min(natural, Math.Max(cap, minimum));
    }

    /// <summary>Рабочая область подсказки: содержимое окна минус отступ по краям.</summary>
    /// <remarks>
    /// **Пределы берутся у содержимого окна, а не у самого окна:** <c>Window.ActualHeight</c> —
    /// размер окна целиком, с заголовком и рамками, и по нему подсказка уезжала за пределы окна.
    /// Начало содержимого тоже не в нуле, поэтому прямоугольник приходит целиком, вместе с ним.
    ///
    /// Отступ уходит на все четыре стороны — он равен полям корневой сетки окна. Отступ, съедающий
    /// всё содержимое, даёт точку, а не перевёрнутый прямоугольник: правило положения всё равно
    /// прижмёт блок к краю, и отрицательный прямоугольник испортил бы только арифметику сдвига.
    /// </remarks>
    /// <param name="content">Содержимое окна в координатах окна.</param>
    /// <param name="inset">Отступ от краёв содержимого.</param>
    /// <returns>Прямоугольник, в котором подсказке положено помещаться.</returns>
    public static Rect WorkArea(Rect content, double inset)
    {
        var left = content.Left + inset;
        var top = content.Top + inset;
        var width = content.Width - (2 * inset);
        var height = content.Height - (2 * inset);

        return new Rect(left, top, Math.Max(0, width), Math.Max(0, height));
    }

    /// <summary>Предел высоты блока: сколько влезает в окно вместе с зазором до поля.</summary>
    /// <remarks>
    /// Предела ширины <see cref="ResolveWidth"/> недостаточно: он заставляет текст переноситься, а
    /// перенос плодит строки, и блок растёт по высоте сколько угодно. <see cref="Resolve"/> выход
    /// вверх компенсирует лишь один раз, то есть ровно настолько, насколько блок велик, — а блок
    /// длиннее окна прижать некуда.
    ///
    /// Предел отнимает у высоты окна две величины: зазор, который блок держит под полем и который
    /// при сдвиге вверх уходит вместе с блоком, и собственные поля блока, которые занимают высоту
    /// независимо от текста. Отсюда и порядок применения: предел ставится содержимому, а не блоку.
    /// </remarks>
    /// <param name="container">Пределы окна в тех же координатах.</param>
    /// <param name="gap">Зазор между полем и подсказкой.</param>
    /// <param name="chrome">Поля блока подсказки по вертикали.</param>
    /// <returns>Предел высоты содержимого блока.</returns>
    public static double ResolveMaxHeight(Rect container, double gap, double chrome) =>
        Math.Max(0, container.Height - gap - chrome);

    /// <summary>Левый край стрелки: по центру поля, но внутри самого блока.</summary>
    /// <remarks>
    /// По центру поля, а не по центру блока: когда блок сдвинут правилом выхода за границу окна,
    /// центры разъезжаются, и стрелка по центру блока указала бы мимо поля.
    ///
    /// Прижим внутрь блока нужен по той же причине: у края окна центр поля почти совпадает с краем
    /// блока, и стрелка без прижима свесилась бы с него. Пустой диапазон отдаёт левый край с
    /// отступом: стрелка наезжает на угол, но за пределы блока не выходит — выход заметнее наезда.
    /// </remarks>
    /// <param name="block">Прямоугольник блока подсказки в координатах окна.</param>
    /// <param name="fieldCenterX">Центр поля, к которому показывается подсказка.</param>
    /// <param name="arrowWidth">Ширина рисунка стрелки.</param>
    /// <param name="inset">Отступ от краёв блока, за который стрелка не заходит.</param>
    /// <returns>Левый край стрелки в координатах окна.</returns>
    public static double ResolveArrowLeft(Rect block, double fieldCenterX, double arrowWidth, double inset)
    {
        var desired = fieldCenterX - (arrowWidth / 2);
        var min = block.Left + inset;
        var max = block.Right - inset - arrowWidth;

        return max < min ? min : Math.Min(Math.Max(desired, min), max);
    }
}
