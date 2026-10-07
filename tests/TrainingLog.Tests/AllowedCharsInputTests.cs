using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>
/// Правила фильтра символов в числовых полях. Проверяются чистые функции: подписки на события
/// WPF тестом не ловятся в принципе, их видно только набором в работающем окне.
/// </summary>
public sealed class AllowedCharsInputTests
{
    [Theory]
    [InlineData('0')]
    [InlineData('5')]
    [InlineData('9')]
    public void IsAllowed_Цифра_Разрешена(char c)
    {
        Assert.True(AllowedCharsInput.IsAllowed(c, AllowedChars.Digits));
        Assert.True(AllowedCharsInput.IsAllowed(c, AllowedChars.DigitsWithSeparators));
    }

    /// <summary>
    /// Разделитель входит только в набор веса: повторения — целое число, и «8,5» там мусор.
    /// </summary>
    [Theory]
    [InlineData('.')]
    [InlineData(',')]
    public void IsAllowed_Разделитель_РазрешёнТолькоДляВеса(char c)
    {
        Assert.True(AllowedCharsInput.IsAllowed(c, AllowedChars.DigitsWithSeparators));
        Assert.False(AllowedCharsInput.IsAllowed(c, AllowedChars.Digits));
    }

    [Theory]
    [InlineData('a')]
    [InlineData('ф')]
    [InlineData(' ')]
    [InlineData('-')]
    [InlineData('+')]
    [InlineData('\t')]
    [InlineData('%')]
    [InlineData('к')]
    public void IsAllowed_Нецифра_Запрещена(char c)
    {
        Assert.False(AllowedCharsInput.IsAllowed(c, AllowedChars.Digits));
        Assert.False(AllowedCharsInput.IsAllowed(c, AllowedChars.DigitsWithSeparators));
    }

    /// <summary>
    /// Полноширинная цифра восточноазиатской раскладки цифрой не считается: <c>char.IsDigit</c>
    /// её принимает, а <c>int.TryParse</c> не разбирает — и поле получило бы значение, на
    /// котором сохранение погаснет.
    /// </summary>
    [Fact]
    public void IsAllowed_ПолношириннаяЦифра_Запрещена()
    {
        Assert.False(AllowedCharsInput.IsAllowed('６', AllowedChars.DigitsWithSeparators));
    }

    /// <summary>
    /// Набор не задан — поведение не подключено, и проверять нечего: поле принимает всё,
    /// иначе фильтр висел бы на каждом поле окна без явного указания.
    /// </summary>
    [Fact]
    public void IsAllowed_НаборНеЗадан_РазрешеноВсё()
    {
        Assert.True(AllowedCharsInput.IsAllowed('ф', AllowedChars.None));
    }

    [Fact]
    public void IsTextAllowed_Цифры_Разрешены()
    {
        Assert.True(AllowedCharsInput.IsTextAllowed("60", AllowedChars.Digits));
    }

    [Fact]
    public void IsTextAllowed_ВесСРазделителем_Разрешён()
    {
        Assert.True(AllowedCharsInput.IsTextAllowed("60,5", AllowedChars.DigitsWithSeparators));
    }

    /// <summary>
    /// Проверяется весь кусок, а не первый символ: метод ввода присылает в <c>PreviewTextInput</c>
    /// сразу несколько символов, и решение принимается по всему тексту.
    /// </summary>
    [Fact]
    public void IsTextAllowed_СмешанныйТекст_Запрещён()
    {
        Assert.False(AllowedCharsInput.IsTextAllowed("6a", AllowedChars.Digits));
        Assert.False(AllowedCharsInput.IsTextAllowed("60 5", AllowedChars.DigitsWithSeparators));
    }

    [Fact]
    public void IsTextAllowed_ПустойТекст_Разрешён()
    {
        Assert.True(AllowedCharsInput.IsTextAllowed(string.Empty, AllowedChars.Digits));
    }

    [Fact]
    public void TakePaste_ОдноЧисло_БерётсяКакЕсть()
    {
        Assert.Equal("60", AllowedCharsInput.TakePaste("60", AllowedChars.Digits));
    }

    /// <summary>
    /// Копирование числа между окнами обычно приносит с собой перевод строки и пробелы по
    /// краям — они не должны мешать вставке.
    /// </summary>
    [Theory]
    [InlineData("60\r\n", "60")]
    [InlineData("60\n", "60")]
    [InlineData("\r\n60", "60")]
    [InlineData("  60  ", "60")]
    public void TakePaste_ЧислоСКрайямиСтроки_Обрезаются(string pasted, string expected)
    {
        Assert.Equal(expected, AllowedCharsInput.TakePaste(pasted, AllowedChars.Digits));
    }

    [Fact]
    public void TakePaste_ВесСЗапятой_РазбираетсяКакВес()
    {
        Assert.Equal("60,5", AllowedCharsInput.TakePaste("  60,5  \r\n", AllowedChars.DigitsWithSeparators));
    }

    /// <summary>
    /// Столбец из таблицы: в поле попадает первая строка, остальные отбрасываются молча —
    /// одна ячейка принимает одно значение.
    /// </summary>
    [Fact]
    public void TakePaste_Столбец_БерётсяПерваяСтрока()
    {
        Assert.Equal("60", AllowedCharsInput.TakePaste("60\r\n70\r\n80", AllowedChars.Digits));
    }

    [Fact]
    public void TakePaste_ПустыеСтрокиВпереди_БерётсяПерваяНепустая()
    {
        Assert.Equal("60", AllowedCharsInput.TakePaste("\r\n   \n60", AllowedChars.Digits));
    }

    /// <summary>
    /// Отказ, а не вырезание символов: из «60 10» вырезание дало бы «6010», то есть вес,
    /// которого в буфере не было, и это хуже, чем ничего не вставить.
    /// </summary>
    [Fact]
    public void TakePaste_ЕдиницыИзмеренияВБуфере_Отказ()
    {
        Assert.Null(AllowedCharsInput.TakePaste("60 кг", AllowedChars.DigitsWithSeparators));
        Assert.Null(AllowedCharsInput.TakePaste("60,5\tкг\n70", AllowedChars.DigitsWithSeparators));
    }

    [Fact]
    public void TakePaste_МинусВБуфереПовторений_Отказ()
    {
        Assert.Null(AllowedCharsInput.TakePaste("8-10", AllowedChars.Digits));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public void TakePaste_ВставлятьНечего_Отказ(string? pasted)
    {
        Assert.Null(AllowedCharsInput.TakePaste(pasted, AllowedChars.DigitsWithSeparators));
    }

    [Fact]
    public void RejectionMessage_ТолькоЦифры_РазделителейНеУпоминает()
    {
        var message = AllowedCharsInput.RejectionMessage(AllowedChars.Digits);

        Assert.Contains("цифры", message, StringComparison.Ordinal);
        Assert.DoesNotContain("точка", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectionMessage_ДляВеса_УпоминаетОбаРазделителя()
    {
        var message = AllowedCharsInput.RejectionMessage(AllowedChars.DigitsWithSeparators);

        Assert.Contains("точка", message, StringComparison.Ordinal);
        Assert.Contains("запятая", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectionMessage_НаборНеЗадан_СообщенияНет()
    {
        Assert.Equal(string.Empty, AllowedCharsInput.RejectionMessage(AllowedChars.None));
    }
}
