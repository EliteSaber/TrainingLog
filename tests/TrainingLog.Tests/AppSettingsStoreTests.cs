using TrainingLog.Core.Settings;

namespace TrainingLog.Tests;

/// <summary>
/// Файл настроек рядом с программой: где он лежит, что происходит, когда его нет, и что с
/// негодным содержимым.
/// </summary>
/// <remarks>
/// Отдельный набор проверок, потому что от него зависит запуск приложения целиком: чтение
/// зовётся до показа окон, и исключение здесь означало бы отказ запуска из-за файла, который
/// пользователь когда-то правил руками.
/// </remarks>
public sealed class AppSettingsStoreTests
{
    /// <summary>
    /// Файл ищется рядом с программой, а не в рабочем каталоге: рабочий каталог у процесса
    /// свой и зависит от способа запуска, и настройки завелись бы не там.
    /// </summary>
    /// <remarks>
    /// Сравнение идёт через Path.Combine, а не через GetDirectoryName: каталог программы
    /// кончается разделителем, а GetDirectoryName его срезает, и равенство падало бы вовсе.
    /// </remarks>
    [Fact]
    public void Resolve_ФайлРядомСПрограммой()
    {
        var path = AppSettingsStore.Resolve();

        Assert.Equal(AppSettingsStore.FileName, Path.GetFileName(path));
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, AppSettingsStore.FileName), path);
    }

    /// <summary>
    /// Файла нет — он создаётся, и в нём сразу значение по умолчанию. Иначе первый запуск
    /// упирался бы в «нечего читать» и настройка не появлялась бы вовсе.
    /// </summary>
    [Fact]
    public void Load_ФайлаНет_СоздаётЗначениеПоУмолчанию()
    {
        using var settings = new TemporarySettings();

        Assert.False(File.Exists(settings.FilePath));

        var loaded = settings.Store.Load();

        Assert.Equal(AppSettings.DefaultDaysCount, loaded.DaysCount);
        Assert.True(File.Exists(settings.FilePath));

        // Отдельным хранилищем: значение должно лежать в файле, а не только в памяти.
        Assert.Equal(AppSettings.DefaultDaysCount, new AppSettingsStore(settings.FilePath).Load().DaysCount);
    }

    [Fact]
    public void Save_ПотомЧитаетсяТоЖеЗначение()
    {
        using var settings = new TemporarySettings();

        Assert.True(settings.Store.Save(new AppSettings { DaysCount = 7 }));

        Assert.Equal(7, settings.Store.Load().DaysCount);
    }

    /// <summary>
    /// Файл записано с отступами: в него заглядывают глазами, когда правят руками.
    /// </summary>
    [Fact]
    public void Save_ПишетJsonСОтступами()
    {
        using var settings = new TemporarySettings();

        settings.Store.Save(new AppSettings { DaysCount = 9 });

        Assert.Contains("\"DaysCount\": 9", File.ReadAllText(settings.FilePath), StringComparison.Ordinal);
    }

    /// <summary>
    /// Негодный файл не должен ронять запуск: из него берётся значение по умолчанию. Сюда
    /// попадает и пустой файл — он тоже не разбирается как JSON.
    /// </summary>
    /// <remarks>
    /// Проверяются все три негодных содержимых, а не одно: поломанный JSON, пустой файл и
    /// объект без нужного ключа — это три разные причины, и ни одна из них не должна была
    /// добраться до пользователя исключением. Файл при этом не переписывается: его правят
    /// руками, и молчаливая замена испортила бы то, что человек ещё не дописал.
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
    /// Значение вне диапазона — тоже значение по умолчанию: правило одно и у ввода, и у того,
    /// что лежит на диске, иначе файл мог бы открыть журнал на ноль дней.
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

    /// <summary>
    /// Границы диапазона годные: правило проверяет включительно, и оба края должны работать.
    /// </summary>
    [Theory]
    [InlineData(DaysCountRules.MinDaysCount)]
    [InlineData(DaysCountRules.MaxDaysCount)]
    public void Load_ГраницыДиапазона_Принимаются(int daysCount)
    {
        using var settings = new TemporarySettings();
        settings.Write($$"""{"DaysCount": {{daysCount}}}""");

        Assert.Equal(daysCount, settings.Store.Load().DaysCount);
    }

    /// <summary>
    /// Запись в папку, а не в файл, не должна бросать наружу: окно параметров показывает
    /// отказ красной строкой, и падать на введённом числе нельзя.
    /// </summary>
    [Fact]
    public void Save_ПутьЗанятКаталогом_ВозвращаетОтказ()
    {
        using var settings = new TemporarySettings();

        Assert.False(new AppSettingsStore(settings.AsDirectory()).Save(new AppSettings()));
    }

    /// <summary>
    /// То же на чтении: файл не читается — берётся значение по умолчанию, а не исключение.
    /// </summary>
    [Fact]
    public void Load_ПутьЗанятКаталогом_ОтдаётПоУмолчанию()
    {
        using var settings = new TemporarySettings();

        Assert.Equal(
            AppSettings.DefaultDaysCount,
            new AppSettingsStore(settings.AsDirectory()).Load().DaysCount);
    }
}
