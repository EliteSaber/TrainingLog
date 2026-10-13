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

    [Fact]
    public void DismissCommand_ПоказЕсть_ГаситЕго()
    {
        var hint = new HintState();
        hint.ShowCommand.Execute(Message);

        hint.DismissCommand.Execute(null);

        Assert.False(hint.IsVisible);
    }

    /// <summary>
    /// Текст переживает затухание, и это не упущение: очисти его раньше, и содержимое пустеет
    /// раньше окна. Убирается он уже по <c>Closed</c> окна подсказки (ловушка 31).
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

    /// <summary>Показ включится снова, хотя текст не изменился и сам по себе уведомления не поднимет.</summary>
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

    [Fact]
    public void DismissCommand_Дважды_ПоказНеВозвращается()
    {
        var hint = new HintState();
        hint.ShowCommand.Execute(Message);

        hint.DismissCommand.Execute(null);
        hint.DismissCommand.Execute(null);

        Assert.False(hint.IsVisible);
    }

    /// <summary>Принятый ввод зовёт команду из окна безусловно, а подсказки на экране может не быть.</summary>
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

    /// <summary>Оба различия задаёт модель, поэтому проверяются здесь, а не в окне.</summary>
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

    /// <summary>Иначе отказ, случившийся следом, держался бы на срок примечания.</summary>
    [Fact]
    public void ShowCommand_ПослеПоказаПримечания_ВозвращаетОтказПоВводу()
    {
        var hint = new HintState();
        hint.ShowNoteCommand.Execute("Болело левое плечо");

        hint.ShowCommand.Execute(Message);

        Assert.Equal(HintState.HintKind.Rejection, hint.Kind);
        Assert.Equal(TimeSpan.FromSeconds(5), hint.Lifetime);
    }

    /// <summary>Окна зовут обычный показ и не должны сами помнить, что подсказка до того показывала примечание.</summary>
    [Fact]
    public void ShowCommand_ПодсказкиНеБыло_ОтказПоУмолчанию()
    {
        var hint = new HintState();

        hint.ShowCommand.Execute(Message);

        Assert.Equal(HintState.HintKind.Rejection, hint.Kind);
        Assert.Equal(TimeSpan.FromSeconds(5), hint.Lifetime);
    }

    /// <summary>Принятый ввод гасит подсказку, но не вычищает её текст.</summary>
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