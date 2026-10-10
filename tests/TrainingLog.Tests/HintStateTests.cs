using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Состояние подсказки об отказе по вводу. Проверяется здесь, а не у каждой модели представления:
/// состояние общее для окон, и правило одно — «показ гасится сразу, текст переживает затухание».
/// </summary>
public sealed class HintStateTests
{
    private const string Message = "В поле допустимы только цифры.";

    [Fact]
    public void ShowCommand_ПодсказкиНеБыло_ПоказываетТекст()
    {
        var hint = new HintState();

        Assert.False(hint.IsVisible);
        Assert.Equal(string.Empty, hint.Text);

        hint.ShowCommand.Execute(Message);

        Assert.True(hint.IsVisible);
        Assert.Equal(Message, hint.Text);
    }

    /// <summary>
    /// Крестик, таймер и клик снаружи ведут в одно действие — погасить показ.
    /// </summary>
    [Fact]
    public void DismissCommand_ПоказЕсть_ГаситЕго()
    {
        var hint = new HintState();
        hint.ShowCommand.Execute(Message);

        hint.DismissCommand.Execute(null);

        Assert.False(hint.IsVisible);
    }

    /// <summary>
    /// Показ гасится, а текст остаётся — и это не упущение, а то самое, из-за чего подсказка
    /// гасла в два шага. Текст должен пережить затухание окна: очисти его раньше, и содержимое
    /// пустеет раньше окна, то есть сперва исчезает надпись, потом крестик. Убирается он уже по
    /// <c>Closed</c> окна подсказки, то есть когда подсказки нет.
    /// </summary>
    [Fact]
    public void DismissCommand_ТекстНеТрогает()
    {
        var hint = new HintState();
        hint.ShowCommand.Execute(Message);

        hint.DismissCommand.Execute(null);

        Assert.Equal(Message, hint.Text);
    }

    /// <summary>Текст убирается после исчезновения подсказки, а не в момент закрытия окна.</summary>
    [Fact]
    public void ClearCommand_ТекстЕсть_УбираетЕго()
    {
        var hint = new HintState();
        hint.ShowCommand.Execute(Message);

        hint.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, hint.Text);
    }

    /// <summary>
    /// Повторный отказ с тем же текстом после закрытия: показ включится снова, хотя текст не
    /// изменился и сам по себе уведомления не поднимет.
    /// </summary>
    [Fact]
    public void ShowCommand_ПослеЗакрытия_ПоказываетСнова()
    {
        var hint = new HintState();
        hint.ShowCommand.Execute(Message);
        hint.DismissCommand.Execute(null);
        hint.ClearCommand.Execute(null);

        hint.ShowCommand.Execute(Message);

        Assert.True(hint.IsVisible);
        Assert.Equal(Message, hint.Text);
    }

    /// <summary>
    /// Показ не гаснет от смены значения поля: его этим не должно занимать, объяснение живёт
    /// столько, сколько положено.
    /// </summary>
    [Fact]
    public void DismissCommand_Дважды_ПоказНеВозвращается()
    {
        var hint = new HintState();
        hint.ShowCommand.Execute(Message);

        hint.DismissCommand.Execute(null);
        hint.DismissCommand.Execute(null);

        Assert.False(hint.IsVisible);
    }

    /// <summary>
    /// Гашение на погашенной подсказке безопасно: принятый ввод зовёт команду из окна безусловно,
    /// а подсказки на экране в этот момент может не быть. Показ от этого не должен ни
    /// возвращаться, ни задевать текст.
    /// </summary>
    [Fact]
    public void DismissCommand_БезПоказа_НичегоНеЛомает()
    {
        var hint = new HintState();

        hint.DismissCommand.Execute(null);

        Assert.False(hint.IsVisible);
        Assert.Equal(string.Empty, hint.Text);

        hint.ShowCommand.Execute(Message);

        Assert.True(hint.IsVisible);
    }

/// <summary>
    /// Примечание упражнения показывается той же подсказкой, но живёт дольше и выглядит иначе:
    /// оба различия задаёт модель, поэтому проверяются здесь, а не в окне.
    /// </summary>
    [Fact]
    public void ShowNoteCommand_Примечание_ПоказываетТекстНаСвойСрок()
    {
        var hint = new HintState();

        hint.ShowNoteCommand.Execute("Болело левое плечо");

        Assert.True(hint.IsVisible);
        Assert.Equal("Болело левое плечо", hint.Text);
        Assert.Equal(HintState.HintKind.Note, hint.Kind);
        Assert.Equal(TimeSpan.FromSeconds(30), hint.Lifetime);
    }

    /// <summary>
    /// После показа примечания обычный отказ по вводу снова живёт пять секунд и выглядит как
    /// отказ. Иначе отказ, случившийся следом, держался бы на срок примечания.
    /// </summary>
    [Fact]
    public void ShowCommand_ПослеПоказаПримечания_ВозвращаетОтказПоВводу()
    {
        var hint = new HintState();
        hint.ShowNoteCommand.Execute("Болело левое плечо");

        hint.ShowCommand.Execute(Message);

        Assert.Equal(HintState.HintKind.Rejection, hint.Kind);
        Assert.Equal(TimeSpan.FromSeconds(5), hint.Lifetime);
    }

    /// <summary>
    /// Срок и вид отказа задаются по умолчанию: окна зовут обычный показ и не должны сами помнить,
    /// что подсказка до того показывала примечание.
    /// </summary>
    [Fact]
    public void ShowCommand_ПодсказкиНеБыло_ОтказПоУмолчанию()
    {
        var hint = new HintState();

        hint.ShowCommand.Execute(Message);

        Assert.Equal(HintState.HintKind.Rejection, hint.Kind);
        Assert.Equal(TimeSpan.FromSeconds(5), hint.Lifetime);
    }

    /// <summary>
/// Принятый ввод гасит подсказку, но не вычищает её текст: текст живёт до закрытия и убирается
/// по <see cref="HintState.ClearCommand"/>, как и при любом другом закрытии.
/// </summary>
    [Fact]
    public void DismissCommand_ПослеПоказа_ГаситПоказИОставляетТекст()
    {
        var hint = new HintState();
        hint.ShowCommand.Execute(Message);

        hint.DismissCommand.Execute(null);

        Assert.False(hint.IsVisible);
        Assert.Equal(Message, hint.Text);
    }
}