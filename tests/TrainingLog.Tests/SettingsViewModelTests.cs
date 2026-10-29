using TrainingLog.Core.Data;
using TrainingLog.Core.Settings;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>Окно параметров: откуда значение в поле, что делает «Сохранить» и чем гасится отказ.</summary>
public sealed class SettingsViewModelTests
{
    /// <summary>
    /// Поле открывается на сохранённом значении: пустое поле снесло бы настройку первым же
    /// сохранением. Имя базы в файле хранится с расширением, поле показывает его без хвоста.
    /// </summary>
    [Fact]
    public void Поля_ОткрываютсяНаСохранённыхЗначениях()
    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
            {
                DaysCount = 12,
                DatabaseDirectory = @"D:\журнал\базы",
                DatabaseName = "спорт.db",
            });

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.Equal(12, viewModel.DaysCount);
        Assert.Equal(@"D:\журнал\базы", viewModel.DatabaseDirectory);
        Assert.Equal("спорт", viewModel.DatabaseName);
    }

    /// <summary>По <c>Accepted</c> окно выставляет <c>DialogResult</c>, а главное окно перечитывает файл.</summary>
    [Fact]
    public void Save_ПишетВФайл_ИОтмечаетПринятым()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store)
        {
            DaysCount = 6,
            DatabaseDirectory = @"D:\журнал\базы",
            DatabaseName = "спорт",
        };

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Accepted);

        var loaded = new AppSettingsStore(settings.FilePath).Load();

        Assert.Equal(6, loaded.DaysCount);
        Assert.Equal(@"D:\журнал\базы", loaded.DatabaseDirectory);
        Assert.Equal("спорт.db", loaded.DatabaseName);
    }

    /// <summary>
    /// Расширение дописывается всегда и в файл попадает один раз: впечатав его вручную, получили
    /// бы «база.db.db», собирая имя конкатенацией.
    /// </summary>
    [Fact]
    public void Save_ХвостРасширениеВПоле_ФайлБезУдвоения()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DatabaseName = "спорт.db" };

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("спорт.db", new AppSettingsStore(settings.FilePath).Load().DatabaseName);
    }

    /// <summary>После сохранения поле снова показывает имя без расширения: показ и хранение разведены.</summary>
    [Fact]
    public void Круг_SaveОткрыть_FieldБезРасширения()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DatabaseName = "спорт.2026" };

        viewModel.SaveCommand.Execute(null);

        var opened = new SettingsViewModel(new AppSettingsStore(settings.FilePath));

        Assert.Equal("спорт.2026", opened.DatabaseName);
    }

    /// <summary>
    /// Признак объявляется уведомлением: по одному значению <c>Accepted</c> окно не выставило бы
    /// <c>DialogResult</c>, и главное окно осталось бы со старым числом дней.
    /// </summary>
    [Fact]
    public void Save_УведомляетОПринятом()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DaysCount = 4 };

        var notified = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        viewModel.SaveCommand.Execute(null);

        Assert.Contains(nameof(SettingsViewModel.Accepted), notified);
    }

    /// <summary>Иначе журнал открывался бы на ноль дней или на тысячу.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(DaysCountRules.MaxDaysCount + 1)]
    public void SaveCommand_НедопустимоеЗначение_КнопкаГасится(int daysCount)
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DaysCount = daysCount };

        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// База собирается из двух полей, и оба должны быть заполнены: пробельный адрес соединение
    /// построило бы, а базу — нет.
    /// </summary>
    [Theory]
    [InlineData("", "база.db")]
    [InlineData("  ", "база.db")]
    [InlineData(@"D:\базы", "")]
    [InlineData(@"D:\базы", "  ")]
    public void SaveCommand_АдресБазыНеполный_КнопкаГасится(string directory, string name)
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store)
        {
            DatabaseDirectory = directory,
            DatabaseName = name,
        };

        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// Проверка полей повторяется и в теле команды: <c>Execute</c> предикат не спрашивает
    /// (ловушка 46).
    /// </summary>
    [Fact]
    public void Save_ДиректорияПустаПриВызовеПрограммно_НеЗаписывает()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DatabaseDirectory = "" };

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Accepted);
        Assert.Equal(SqliteDatabasePath.DefaultDirectory, new AppSettingsStore(settings.FilePath).Load().DatabaseDirectory);
    }

    /// <summary>
    /// Проверка значения повторяется и в теле команды: <c>Execute</c> предикат не спрашивает
    /// (ловушка 46). Проверяется содержимое файла, а не его отсутствие: к этому моменту файл уже
    /// создан при открытии окна.
    /// </summary>
    [Fact]
    public void Save_НедопустимоеЗначениеВызваноПрограммно_НеЗаписывает()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DaysCount = 0 };

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Accepted);
        Assert.Equal(AppSettings.DefaultDaysCount, settings.Store.Load().DaysCount);
    }

    /// <summary>
    /// Отказ записи показывается надписью и держится до успеха: молча закрывшееся окно выглядело
    /// бы как «настройка сохранилась». Каталога нет, поэтому запись в него невозможна.
    /// </summary>
    [Fact]
    public void Save_ОтказЗаписи_ПоказываетОтказАПослеУспеха_ГаситЕго()
    {
        using var settings = new TemporarySettings();

        var file = Path.Combine(settings.FolderPath, "позже", "settings.json");
        var viewModel = new SettingsViewModel(new AppSettingsStore(file)) { DaysCount = 5 };

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.HasStatus);
        Assert.NotEqual(string.Empty, viewModel.Status);
        Assert.False(viewModel.Accepted);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasStatus);
        Assert.True(viewModel.Accepted);
        Assert.Equal(5, new AppSettingsStore(file).Load().DaysCount);
    }

    /// <summary>Отказ по вводу показывается той же подсказкой, что и в остальных окнах.</summary>
    [Fact]
    public void Hint_ОтказПоВводу_Показывается()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.Hint.ShowCommand.Execute("В поле допустимы только цифры.");

        Assert.True(viewModel.Hint.IsVisible);
        Assert.Equal("В поле допустимы только цифры.", viewModel.Hint.Text);

        viewModel.Hint.DismissCommand.Execute(null);

        Assert.False(viewModel.Hint.IsVisible);
    }
}
