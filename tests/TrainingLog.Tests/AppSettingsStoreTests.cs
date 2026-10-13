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

    /// <summary>Отступы при записи: в файл заглядывают глазами, когда правят руками.</summary>
    [Fact]
    public void Save_ПишетJsonСОтступами()
    {
        using var settings = new TemporarySettings();

        settings.Store.Save(new AppSettings { DaysCount = 9 });

        Assert.Contains("\"DaysCount\": 9", File.ReadAllText(settings.FilePath), StringComparison.Ordinal);
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
