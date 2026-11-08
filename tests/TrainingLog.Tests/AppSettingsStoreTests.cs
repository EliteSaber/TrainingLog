using TrainingLog.Core.Data;
using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>Файл настроек рядом с программой.</summary>
/// <remarks>
/// Отдельный набор проверок, потому что от него зависит запуск целиком: чтение зовётся до
/// показа окон, и исключение здесь означало бы отказ запуска из-за файла, который правили руками.
/// </remarks>
public sealed class AppSettingsStoreTests
{
    /// <summary>Рядом с программой, а не в рабочем каталоге: тот зависит от способа запуска.</summary>
    /// <remarks>
    /// Сравнение через Path.Combine, а не GetDirectoryName: каталог программы кончается
    /// разделителем, а GetDirectoryName его срезает, и равенство падало бы вовсе.
    /// </remarks>
    [Fact]
    public void Resolve_ФайлРядомСПрограммой()
    {
        var path = AppSettingsStore.Resolve();

        Assert.Equal(AppSettingsStore.FileName, Path.GetFileName(path));
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, AppSettingsStore.FileName), path);
    }

    /// <summary>Файла нет — он создаётся со значением по умолчанию, иначе настройки не появились бы.</summary>
    [Fact]
    public void Load_ФайлаНет_СоздаётЗначениеПоУмолчанию()
    {
        using var settings = new TemporarySettings();

        Assert.False(File.Exists(settings.FilePath));

        var loaded = settings.Store.Load();

        Assert.Equal(AppSettings.DefaultDaysCount, loaded.DaysCount);
        Assert.Equal(SqliteDatabasePath.DefaultDirectory, loaded.DatabaseDirectory);
        Assert.Equal(SqliteDatabasePath.FileName, loaded.DatabaseName);
        Assert.True(File.Exists(settings.FilePath));

        // Отдельным хранилищем: значение должно лежать в файле, а не только в памяти.
        var reread = new AppSettingsStore(settings.FilePath).Load();

        Assert.Equal(AppSettings.DefaultDaysCount, reread.DaysCount);
        Assert.Equal(SqliteDatabasePath.DefaultDirectory, reread.DatabaseDirectory);
        Assert.Equal(SqliteDatabasePath.FileName, reread.DatabaseName);
    }

    [Fact]
    public void Save_ПотомЧитаетсяТоЖеЗначение()
    {
        using var settings = new TemporarySettings();

        Assert.True(settings.Store.Save(new AppSettings
            {
                DaysCount = 7,
                DatabaseDirectory = @"D:\журнал\базы",
                DatabaseName = "спорт.db",
            }));

        var loaded = settings.Store.Load();

        Assert.Equal(7, loaded.DaysCount);
        Assert.Equal(@"D:\журнал\базы", loaded.DatabaseDirectory);
        Assert.Equal("спорт.db", loaded.DatabaseName);
        Assert.Equal(@"D:\журнал\базы\спорт.db", loaded.DatabasePath);
    }

    /// <summary>Секция Hotkeys: круг «сохранили — прочитали» вместе с остальными полями.</summary>
    /// <remarks>
    /// Словарь сериализуется штатным System.Text.Json, но проверяется не сериализатор: та же
    /// запись обязана вернуться из файла целиком, а не только из памяти хранилища.
    /// </remarks>
    [Fact]
    public void Save_ПотоЧитаетсяСекцияГорячихКлавиш()
    {
        using var settings = new TemporarySettings();

        Assert.True(settings.Store.Save(new AppSettings
        {
            DaysCount = 7,
            DatabaseDirectory = @"D:\журнал\базы",
            DatabaseName = "спорт.db",
            Hotkeys = new Dictionary<string, string>
            {
                ["Day.Save"] = "Ctrl+Shift+S",
                ["Exercises.Add"] = "F2",
            },
        }));

        var loaded = settings.Store.Load();

        Assert.Equal("Ctrl+Shift+S", loaded.Hotkeys["Day.Save"]);
        Assert.Equal("F2", loaded.Hotkeys["Exercises.Add"]);

        // Отдельным хранилищем: секция лежит в файле, а не только в памяти.
        var reread = new AppSettingsStore(settings.FilePath).Load();

        Assert.Equal("Ctrl+Shift+S", reread.Hotkeys["Day.Save"]);
        Assert.Equal("F2", reread.Hotkeys["Exercises.Add"]);
    }

    /// <summary>Секции нет, она пустая или негодное содержимое файла — словарь пуст, не null.</summary>
    /// <remarks>
    /// Пустой словарь, а не null: разбор назначений и умолчаний живёт в WPF-слое, и «отсутствует»
    /// и «пусто» обязаны объясняться там одним значением.
    /// </remarks>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"DaysCount": 5}""")]
    [InlineData("""{"Hotkeys": {}}""")]
    [InlineData("{ это не json")]
    public void Load_СекцииГорячихКлавишНет_ПустойСловарь(string content)
    {
        using var settings = new TemporarySettings();
        settings.Write(content);

        Assert.Empty(settings.Store.Load().Hotkeys);
    }

    /// <summary>Отступы при записи: в файл заглядывают глазами, когда правят руками.</summary>
    [Fact]
    public void Save_ПишетJsonСОтступами()
    {
        using var settings = new TemporarySettings();

        settings.Store.Save(new AppSettings { DaysCount = 9 });

        Assert.Contains("\"DaysCount\": 9", File.ReadAllText(settings.FilePath), StringComparison.Ordinal);
        Assert.Contains("\"DatabaseDirectory\":", File.ReadAllText(settings.FilePath), StringComparison.Ordinal);
        Assert.Contains("\"DatabaseName\":", File.ReadAllText(settings.FilePath), StringComparison.Ordinal);
    }

    /// <summary>
    /// Негодное содержимое не роняет запуск, и файл при этом не переписывается: его правят
    /// руками, и молчаливая замена испортила бы то, что человек ещё не дописал.
    /// </summary>
    /// <remarks>
    /// Три содержимых, а не одно: поломанный JSON, пустой файл и объект без ключа — три разные
    /// причины, и ни одна не должна была дойти до пользователя исключением.
    /// </remarks>
    [Theory]
    [InlineData("{ это не json")]
    [InlineData("")]
    [InlineData("{}")]
    public void Load_СодержимоеНеРазбирается_ОтдаётПоУмолчаниюИНеПереписываетФайл(string content)
    {
        using var settings = new TemporarySettings();
        settings.Write(content);

        Assert.Equal(AppSettings.DefaultDaysCount, settings.Store.Load().DaysCount);
        Assert.Equal(content, File.ReadAllText(settings.FilePath));
    }

    /// <summary>
    /// Правило одно и у ввода, и у того, что лежит на диске: иначе файл открыл бы журнал на
    /// ноль дней.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(DaysCountRules.MaxDaysCount + 1)]
    public void Load_ЗначениеНеПоДиапазону_ОтдаётПоУмолчанию(int daysCount)
    {
        using var settings = new TemporarySettings();
        settings.Write($$"""{"DaysCount": {{daysCount}}}""");

        Assert.Equal(AppSettings.DefaultDaysCount, settings.Store.Load().DaysCount);
    }

    /// <summary>Правило включительное, и оба края годные — иначе проверялось бы не то.</summary>
    [Theory]
    [InlineData(DaysCountRules.MinDaysCount)]
    [InlineData(DaysCountRules.MaxDaysCount)]
    public void Load_ГраницыДиапазона_Принимаются(int daysCount)
    {
        using var settings = new TemporarySettings();
        settings.Write($$"""{"DaysCount": {{daysCount}}}""");

        Assert.Equal(daysCount, settings.Store.Load().DaysCount);
    }

    /// <summary>Пробельное имя гарнитуры — тоже «по цепочке»: срез края и пустое означают одно.</summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("  Fira Code  ")]
    public void Load_ИмяШрифтаНормализуется_ПробелыСрезаются(string font)
    {
        using var settings = new TemporarySettings();
        settings.Write($$"""{"FontFamily": "{{font}}"}""");

        Assert.Equal(FontRules.NormalizeName(font), settings.Store.Load().FontFamily);
    }

    /// <summary>
    /// Негодный масштаб откатывает к умолчанию в памяти, а файл не переписывается: его правят
    /// руками, и молчаливая замена испортила бы недописанное.
    /// </summary>
    [Theory]
    [InlineData(79)]
    [InlineData(151)]
    [InlineData(0)]
    public void Load_МасштабНеПоДиапазону_ОтдаётПоУмолчанию(int percent)
    {
        using var settings = new TemporarySettings();
        settings.Write($$"""{"FontSizePercent": {{percent}}}""");

        Assert.Equal(AppSettings.DefaultFontSizePercent, settings.Store.Load().FontSizePercent);
    }

    /// <summary>Правило включительное, и оба края годные — иначе проверялось бы не то.</summary>
    [Theory]
    [InlineData(FontRules.MinScalePercent)]
    [InlineData(FontRules.MaxScalePercent)]
    public void Load_ГраницыМасштаба_Принимаются(int percent)
    {
        using var settings = new TemporarySettings();
        settings.Write($$"""{"FontSizePercent": {{percent}}}""");

        Assert.Equal(percent, settings.Store.Load().FontSizePercent);
    }

    /// <summary>Шрифтовые поля проходят тот же круг «сохранили — прочитали», что и остальные.</summary>
    [Fact]
    public void Save_ПотомЧитаютсяШрифтовыеПоля()
    {
        using var settings = new TemporarySettings();

        Assert.True(settings.Store.Save(new AppSettings
        {
            FontFamily = "Fira Code",
            FontSizePercent = 125,
        }));

        var loaded = settings.Store.Load();

        Assert.Equal("Fira Code", loaded.FontFamily);
        Assert.Equal(125, loaded.FontSizePercent);

        // Отдельным хранилищем: значения лежат в файле, а не только в памяти.
        var reread = new AppSettingsStore(settings.FilePath).Load();

        Assert.Equal("Fira Code", reread.FontFamily);
        Assert.Equal(125, reread.FontSizePercent);
    }

    /// <summary>
    /// Негодное число дней не выбрасывает адрес базы: значения отбираются по одному полю, а не
    /// «весь файл годен или весь файл уходит в умолчание».
    /// </summary>
    [Fact]
    public void Load_СчётчикНегоден_ПоляБазыСохраняются()
    {
        using var settings = new TemporarySettings();
        settings.Write("""{"DaysCount": 0, "DatabaseDirectory": "D:\\базы", "DatabaseName": "спорт.db"}""");

        var loaded = settings.Store.Load();

        Assert.Equal(AppSettings.DefaultDaysCount, loaded.DaysCount);
        Assert.Equal(@"D:\базы", loaded.DatabaseDirectory);
        Assert.Equal("спорт.db", loaded.DatabaseName);
    }

    /// <summary>
    /// Пустые поля базы означают брошенное редактирование, а не правку руками: умолчание
    /// подставляется и дописывается в файл, чтобы адрес базы в нём появился целиком.
    /// </summary>
    [Fact]
    public void Load_ПоляБазыПустые_ПодставляетУмолчанияИДописываетВФайл()
    {
        using var settings = new TemporarySettings();
        settings.Write("""{"DaysCount": 5, "DatabaseDirectory": "", "DatabaseName": " "}""");

        var loaded = settings.Store.Load();

        Assert.Equal(5, loaded.DaysCount);
        Assert.Equal(SqliteDatabasePath.DefaultDirectory, loaded.DatabaseDirectory);
        Assert.Equal(SqliteDatabasePath.FileName, loaded.DatabaseName);

        var reread = new AppSettingsStore(settings.FilePath).Load();

        Assert.Equal(SqliteDatabasePath.DefaultDirectory, reread.DatabaseDirectory);
        Assert.Equal(SqliteDatabasePath.FileName, reread.DatabaseName);
    }

    /// <summary>
    /// Совсем без ключей поля базы: ключи не видны, но значения всё равно умолчания. Файл не
    /// переписывается — признака, что поля отсутствуют, у разобранного объекта нет.
    /// </summary>
    [Fact]
    public void Load_КлючейБазыНет_БерутсяУмолчанияБезПереписиФайла()
    {
        using var settings = new TemporarySettings();
        settings.Write("""{"DaysCount": 5}""");

        var loaded = settings.Store.Load();

        Assert.Equal(SqliteDatabasePath.DefaultDirectory, loaded.DatabaseDirectory);
        Assert.Equal(SqliteDatabasePath.FileName, loaded.DatabaseName);
        Assert.Equal("""{"DaysCount": 5}""", File.ReadAllText(settings.FilePath));
    }

    /// <summary>Запись в папку не бросает наружу: окно показывает отказ, а не падает.</summary>
    [Fact]
    public void Save_ПутьЗанятКаталогом_ВозвращаетОтказ()
    {
        using var settings = new TemporarySettings();

        Assert.False(new AppSettingsStore(settings.AsDirectory()).Save(new AppSettings()));
    }

    [Fact]
    public void Load_ПутьЗанятКаталогом_ОтдаётПоУмолчанию()
    {
        using var settings = new TemporarySettings();

        Assert.Equal(
            AppSettings.DefaultDaysCount,
            new AppSettingsStore(settings.AsDirectory()).Load().DaysCount);
    }
}
