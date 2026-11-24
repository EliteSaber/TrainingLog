using TrainingLog.Core.Models;
using TrainingLog.Core.Settings;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Главное окно: сколько дней пришло из файла настроек, отбор последних дней, применение
/// количества кнопкой, раскладка столбцов строки дня и примечание упражнения в ней.
/// </summary>
public sealed class MainViewModelTests : IDisposable
{
    /// <summary>
    /// Файл настроек на проверку: экземпляр теста в xUnit тоже один на проверку, а не на класс.
    /// </summary>
    private readonly TemporarySettings _settings = new();

    public void Dispose() => _settings.Dispose();

    [Fact]
    public void ПоУмолчанию_ПоказыватьТриДня()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store);

        Assert.Equal(AppSettings.DefaultDaysCount, viewModel.DaysCount);
    }

    /// <summary>По умолчанию фильтр при запуске свёрнут: в шапке только шеврон и сводка.</summary>
    [Fact]
    public void ПоУмолчанию_ФильтрСвёрнут()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store);

        Assert.False(viewModel.IsFiltersExpanded);
        Assert.Equal("Развернуть даты", viewModel.FiltersToggleText);
    }

    /// <summary>
    /// Снятая настройка запуска оставляет фильтр развёрнутым, и загрузка журнала его не
    /// сворачивает: разворачивает только «Применить».
    /// </summary>
    [Fact]
    public async Task Настройка_РазвёрнутыйФильтрПриЗапуске()
    {
        using var database = new TemporaryDatabase();
        _settings.Store.Save(new AppSettings { CollapseFiltersOnStartup = false });

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store);

        Assert.True(viewModel.IsFiltersExpanded);
        Assert.Equal("Свернуть даты", viewModel.FiltersToggleText);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsFiltersExpanded);
    }

    /// <summary>
    /// Из файла, а не из константы: записано иное число, чем по умолчанию, — иначе проверка
    /// прошла бы и по старому коду с константой.
    /// </summary>
    [Fact]
    public void КоличествоДней_БерётсяИзФайлаНастроек()
    {
        using var database = new TemporaryDatabase();
        _settings.Store.Save(new AppSettings { DaysCount = 9 });

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store);

        Assert.Equal(9, viewModel.DaysCount);
    }

    /// <summary>
    /// Настройки записаны окном параметров: поле и список обновились.
    /// </summary>
    /// <remarks>
    /// Окно параметров немодальное, и ответа у команды «Параметры» больше нет: главное окно
    /// узнаёт о записи по событию. Проверяется ровно то, что перечитывание случается по файлу.
    /// </remarks>
    [Fact]
    public async Task OpenSettingsCommand_ПараметрыСохранены_ПолеИСписокОбновились()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var windowService = new FakeWindowService();

        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Days.Count);

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, windowService.SettingsCalls);

        _settings.Store.Save(new AppSettings { DaysCount = 2 });

        await windowService.RaiseSettingsSaved();

        Assert.Equal(2, viewModel.DaysCount);
        Assert.Equal(2, viewModel.Days.Count);
        Assert.Equal(["День 5", "День 4"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>Пока окно параметров не записало файл, поле и список не меняются.</summary>
    [Fact]
    public async Task OpenSettingsCommand_Отменено_ПолеИСписокНеМеняются()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, windowService.SettingsCalls);
        Assert.Equal(AppSettings.DefaultDaysCount, viewModel.DaysCount);
        Assert.Equal(3, viewModel.Days.Count);
    }

    /// <summary>
    /// Место базы изменилось, пока окно параметров было открыто: модель докладывает событием, а
    /// показ остаётся окну — у подсказки нет якоря, пока окно его не выставит.
    /// </summary>
    [Fact]
    public async Task OpenSettingsCommand_АдресБазыИзменился_СобытиеСТекстом()
    {
        using var database = new TemporaryDatabase();

        var windowService = new FakeWindowService();

        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);

        var messages = new List<string>();
        viewModel.DatabaseAddressChanged += message => messages.Add(message);

        _settings.Store.Save(new AppSettings { DatabaseDirectory = @"D:\базы" });

        await windowService.RaiseSettingsSaved();

        var message = Assert.Single(messages);

        Assert.Contains("следующем запуске", message);
    }

    /// <summary>Адрес не менялся — события нет: сообщение на каждый запуск параметров мешало бы.</summary>
    [Fact]
    public async Task OpenSettingsCommand_АдресБазыНеИзменился_СобытияНет()
    {
        using var database = new TemporaryDatabase();

        var windowService = new FakeWindowService();

        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);

        var raised = false;
        viewModel.DatabaseAddressChanged += _ => raised = true;

        _settings.Store.Save(new AppSettings { DaysCount = 7 });

        await windowService.RaiseSettingsSaved();

        Assert.False(raised);
    }

    /// <summary>
    /// Файл сменился, но окно параметров только открыто: события нет — адрес сверяется по
    /// оповещению о записи, а не при каждом открытии.
    /// </summary>
    [Fact]
    public async Task OpenSettingsCommand_СменаФайлаБезЗаписи_СобытияНеБывает()
    {
        using var database = new TemporaryDatabase();

        var windowService = new FakeWindowService();

        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);

        var raised = false;
        viewModel.DatabaseAddressChanged += _ => raised = true;

        // Файл сменился, пока окно параметров было открыто, но запись не подтверждена:
        // открытие окна само по себе адрес не сверяет — сверяет только оповещение о записи.
        _settings.Store.Save(new AppSettings { DatabaseDirectory = @"D:\базы" });

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.False(raised);
    }

    [Fact]
    public void TakeLatest_БерётПоследниеСверху()
    {
        TrainingSession[] sessions =
        [
            Session(new DateOnly(2026, 10, 1)),
            Session(new DateOnly(2026, 10, 5)),
            Session(new DateOnly(2026, 10, 3)),
        ];

        Assert.Equal(
            [new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 3)],
            MainViewModel.TakeLatest(sessions, 2).Select(session => session.Date));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TakeLatest_НольДней_Пусто(int count)
    {
        TrainingSession[] sessions = [Session(new DateOnly(2026, 10, 1))];

        Assert.Empty(MainViewModel.TakeLatest(sessions, count));
    }

    [Fact]
    public async Task LoadAsync_ПятьДнейПоказываетТриСвежих()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsEmpty);
        Assert.Equal(["День 5", "День 4", "День 3"], viewModel.Days.Select(day => day.PlanName));
    }

    [Fact]
    public async Task ApplyAsync_НовоеКоличество_ПеречитываетДни()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Days.Count);

        viewModel.DaysCount = 5;

        // Пока «Применить» не нажата, показанные дни прежние: поле не перечитывает журнал само.
        Assert.Equal(3, viewModel.Days.Count);

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(5, viewModel.Days.Count);
        Assert.Equal("День 1", viewModel.Days[^1].PlanName);
    }

    /// <summary>
    /// Задана только нижняя граница — видны все дни с неё: база получает открытую верхнюю
    /// границу, а «Показать все» снимает ограничение числа.
    /// </summary>
    [Fact]
    public async Task ApplyAsync_ТолькоНижняяГраница_ВсеЗаписиСДаты()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            ShowAll = true,
            DateFrom = new DateTime(2026, 10, 3),
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(["День 5", "День 4", "День 3"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>Задана только верхняя граница — видны все дни до неё включительно.</summary>
    [Fact]
    public async Task ApplyAsync_ТолькоВерхняяГраница_ВсеЗаписиДоДаты()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            ShowAll = true,
            DateTo = new DateTime(2026, 10, 3),
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(["День 3", "День 2", "День 1"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>
    /// Период ограничивает выборку, а число берёт последние дни уже внутри него: иначе при
    /// заданной нижней границе показывались бы самые старые дни периода.
    /// </summary>
    [Fact]
    public async Task ApplyAsync_ПериодИКоличество_ПоследниеВнутриПериода()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DaysCount = 2,
            DateFrom = new DateTime(2026, 10, 2),
            DateTo = new DateTime(2026, 10, 4),
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(["День 4", "День 3"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>«Показать все» снимает ограничение числа, но не сам период.</summary>
    [Fact]
    public async Task ApplyAsync_ПоказатьВсе_СнимаетОграничениеЧисла()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DaysCount = 2,
            ShowAll = true,
            DateFrom = new DateTime(2026, 10, 3),
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(
            ["День 5", "День 4", "День 3"],
            viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>
    /// При «Показать все» негодное число не гасит «Применить»: поле в этот момент недоступно и
    /// на показ не влияет.
    /// </summary>
    [Fact]
    public async Task ApplyCommand_ПоказатьВсе_НедопустимоеКоличествоНеГасит()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DaysCount = 0,
            ShowAll = true,
        };

        Assert.True(viewModel.ApplyCommand.CanExecute(null));

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(["День 5"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>Поле числа недоступно ровно тогда, когда показаны все записи.</summary>
    [Fact]
    public void IsCountEnabled_СледуетЗаГалочкой()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store);

        Assert.True(viewModel.IsCountEnabled);

        viewModel.ShowAll = true;

        Assert.False(viewModel.IsCountEnabled);

        viewModel.ShowAll = false;

        Assert.True(viewModel.IsCountEnabled);
    }

    /// <summary>
    /// Крестик очищает дату, но журнал не перечитывает: фильтр применяется кнопкой, как и
    /// остальные поля шапки.
    /// </summary>
    [Fact]
    public async Task ClearDateFromCommand_ОбнуляетДатуБезПеречитывания()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            ShowAll = true,
            DateFrom = new DateTime(2026, 10, 3),
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Days.Count);

        viewModel.ClearDateFromCommand.Execute(null);

        Assert.Null(viewModel.DateFrom);
        Assert.Equal(3, viewModel.Days.Count);

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(5, viewModel.Days.Count);
    }

    /// <summary>Верхняя граница очищается так же: дата снимается, список ждёт «Применить».</summary>
    [Fact]
    public async Task ClearDateToCommand_ОбнуляетДатуБезПеречитывания()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            ShowAll = true,
            DateTo = new DateTime(2026, 10, 3),
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Days.Count);

        viewModel.ClearDateToCommand.Execute(null);

        Assert.Null(viewModel.DateTo);

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(5, viewModel.Days.Count);
    }

    /// <summary>
    /// Сводка обеих дат — диапазон без подписей «С» и «До», число в скобках и короткий формат,
    /// как в самих полях.
    /// </summary>
    [Fact]
    public async Task FilterSummary_ОбеДаты_ДиапазонБезПодписей()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DateFrom = new DateTime(2026, 10, 2),
            DateTo = new DateTime(2026, 10, 4),
            DaysCount = 2,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("02.10.2026 — 04.10.2026 [2]", viewModel.FilterSummary);
    }

    /// <summary>Одна нижняя граница подписана «С»: иначе непонятно, что задано.</summary>
    [Fact]
    public async Task FilterSummary_ТолькоНачало_СДата()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DateFrom = new DateTime(2026, 10, 3),
            DaysCount = 4,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("С 03.10.2026 [4]", viewModel.FilterSummary);
    }

    /// <summary>Одна верхняя граница подписана «До».</summary>
    [Fact]
    public async Task FilterSummary_ТолькоКонец_ДоДата()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DateTo = new DateTime(2026, 10, 5),
            DaysCount = 4,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("До 05.10.2026 [4]", viewModel.FilterSummary);
    }

    /// <summary>Без дат число выходит из скобок: это фраза «Последние N», а не дата с пометкой.</summary>
    [Fact]
    public async Task FilterSummary_БезДат_Последние()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DaysCount = 4,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("Последние 4", viewModel.FilterSummary);
    }

    /// <summary>«Показать все» заменяет число знаком бесконечности.</summary>
    [Fact]
    public async Task FilterSummary_ПоказатьВсе_Бесконечность()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DateFrom = new DateTime(2026, 10, 2),
            DateTo = new DateTime(2026, 10, 4),
            ShowAll = true,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("02.10.2026 — 04.10.2026 [∞]", viewModel.FilterSummary);
    }

    /// <summary>Без дат «Показать все» — это все записи, а не «Последние ∞».</summary>
    [Fact]
    public async Task FilterSummary_БезДатПоказатьВсе_ВсеЗаписи()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            ShowAll = true,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("Все записи", viewModel.FilterSummary);
    }

    /// <summary>
    /// Сводка показывает применённый фильтр: введённая, но не принятая дата её не меняет,
    /// иначе свёрнутая шапка говорила бы о журнале неправду.
    /// </summary>
    [Fact]
    public async Task FilterSummary_НеПрименённаяДата_НеМеняетСводку()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DateFrom = new DateTime(2026, 10, 2),
            DateTo = new DateTime(2026, 10, 4),
            DaysCount = 2,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        viewModel.DateFrom = new DateTime(2026, 10, 3);

        Assert.Equal("02.10.2026 — 04.10.2026 [2]", viewModel.FilterSummary);
    }

    /// <summary>
    /// Сворачивание ничего не применяет и не перечитывает: сводка и список остаются прежними,
    /// а введённая дата ждёт «Применить» после разворота.
    /// </summary>
    [Fact]
    public async Task ToggleFiltersCommand_СворачиваетБезПрименения()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            IsFiltersExpanded = true,
            DaysCount = 2,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        // «Применить» свернула блок — разворачиваем, чтобы проверить именно ручное сворачивание.
        viewModel.ToggleFiltersCommand.Execute(null);

        viewModel.DateFrom = new DateTime(2026, 10, 5);

        viewModel.ToggleFiltersCommand.Execute(null);

        Assert.False(viewModel.IsFiltersExpanded);
        Assert.Equal(["День 5", "День 4"], viewModel.Days.Select(day => day.PlanName));
        Assert.Equal("Последние 2", viewModel.FilterSummary);

        viewModel.ToggleFiltersCommand.Execute(null);

        Assert.True(viewModel.IsFiltersExpanded);
        Assert.Equal(new DateTime(2026, 10, 5), viewModel.DateFrom);

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsFiltersExpanded);
        Assert.Equal(["День 5"], viewModel.Days.Select(day => day.PlanName));
        Assert.Equal("С 05.10.2026 [2]", viewModel.FilterSummary);
    }

    /// <summary>
    /// «Применить» сворачивает блок после успешного перечитывания: сводка и журнал сразу
    /// показывают один и тот же принятый фильтр.
    /// </summary>
    [Fact]
    public async Task ApplyCommand_ПослеПрименения_СворачиваетФильтр()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            IsFiltersExpanded = true,
            DaysCount = 1,
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsFiltersExpanded);
        Assert.Equal("Последние 1", viewModel.FilterSummary);
    }

    /// <summary>
    /// Пока журнал читается, «Применить» показывает кольцо и недоступна: повторное нажатие не
    /// запустит второе чтение, а кольцо держится ровно до ответа.
    /// </summary>
    [Fact]
    public async Task ApplyCommand_ПокаИдётЧтение_КнопкаНедоступна()
    {
        using var database = new TemporaryDatabase();
        var repository = new GatedSessionRepository(database.SessionRepository);

        var viewModel = new MainViewModel(new FakeWindowService(), repository, _settings.Store)
        {
            IsFiltersExpanded = true,
            DaysCount = 1,
        };

        var applying = viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.ApplyCommand.CanExecute(null));

        repository.Release();

        await applying;

        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.IsFiltersExpanded);
    }

    /// <summary>
    /// Отказ чтения снимает индикатор и оставляет блок развёрнутым: фильтр не применился, и
    /// сводке нечего показывать.
    /// </summary>
    [Fact]
    public async Task ApplyCommand_ОтказЧтения_СнимаетЗагрузкуИНеСворачивает()
    {
        using var database = new TemporaryDatabase();
        var repository = new GatedSessionRepository(database.SessionRepository)
        {
            Failure = new InvalidOperationException("база недоступна"),
        };

        var viewModel = new MainViewModel(new FakeWindowService(), repository, _settings.Store)
        {
            IsFiltersExpanded = true,
            DaysCount = 1,
        };

        var applying = viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsBusy);

        repository.Release();

        await Assert.ThrowsAsync<InvalidOperationException>(() => applying);

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.IsFiltersExpanded);
    }

    /// <summary>
    /// Индикатор — счётчик, а не флаг: перечитывание запускают несколько источников, и первое
    /// завершившееся чтение не должно гасить кольцо, пока второе ещё в пути.
    /// </summary>
    [Fact]
    public async Task Перечитывание_Пересечение_ИндикаторДержитсяДоПоследнегоОтвета()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var repository = new GatedSessionRepository(database.SessionRepository);
        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, repository, _settings.Store);

        var first = viewModel.LoadCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => repository.Calls >= 1);

        // Второе перечитывание — от открытого окна дня, пока первое чтение ещё в пути.
        var second = windowService.RaiseDaySaved();
        await WaitUntilAsync(() => repository.Calls >= 2);

        // Отпускаем и дожидаемся именно второго чтения: первый ответ всё ещё в пути.
        repository.Release(2);
        await second;

        // Второй ответ пришёл, первый ещё нет: индикатор держится, «Применить» погашена.
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.ApplyCommand.CanExecute(null));
        Assert.Equal(["День 5"], viewModel.Days.Select(day => day.PlanName));

        repository.Release(1);
        await first;

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.ApplyCommand.CanExecute(null));
    }

    /// <summary>
    /// Ответ более раннего запроса, пришедший позже, выбрасывается: иначе журнал показал бы
    /// данные до сохранения дня, а сводка — устаревший фильтр.
    /// </summary>
    [Fact]
    public async Task Перечитывание_УстаревшийОтвет_НеТрогаетСписок()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var repository = new GatedSessionRepository(database.SessionRepository);
        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, repository, _settings.Store);

        var first = viewModel.LoadCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => repository.Calls >= 1);

        // Пока первое чтение в пути, день записан и запрошено второе: его ответ свежее.
        await database.SessionRepository.AddAsync(Session(new DateOnly(2026, 10, 6), "День 6"));

        var second = windowService.RaiseDaySaved();
        await WaitUntilAsync(() => repository.Calls >= 2);

        // Свежий ответ применён: второй запрос прочитал обе записи.
        repository.Release(2);
        await second;

        Assert.Equal(["День 6", "День 5"], viewModel.Days.Select(day => day.PlanName));

        // Устаревший ответ приходит после и не должен откатить список к одному дню.
        repository.Release(1);
        await first;

        Assert.Equal(["День 6", "День 5"], viewModel.Days.Select(day => day.PlanName));
        Assert.False(viewModel.IsBusy);
    }

    /// <remarks>
    /// Ждать надо условия, а не времени: фиксированная пауза — гонка на загруженной машине.
    /// </remarks>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Условие не наступило");

            await Task.Delay(1);
        }
    }

    /// <summary>
    /// Перепутанные границы меняются местами при применении: запрос с «С» позже «До» вернул бы
    /// пустоту, а поля показывают исправленный диапазон.
    /// </summary>
    [Fact]
    public async Task ApplyAsync_ОбратныйПериод_МеняетДатыМестами()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            ShowAll = true,
            DateFrom = new DateTime(2026, 10, 5),
            DateTo = new DateTime(2026, 10, 2),
        };

        await viewModel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(new DateTime(2026, 10, 2), viewModel.DateFrom);
        Assert.Equal(new DateTime(2026, 10, 5), viewModel.DateTo);
        Assert.Equal(
            ["День 5", "День 4", "День 3", "День 2"],
            viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>
    /// Пустой журнал при активном фильтре говорит о периоде, а не об отсутствии записей: иначе
    /// человек искал бы причину в базе, а не в датах.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ФильтрНичегоНеНашел_ТекстПоПериоду()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            ShowAll = true,
            DateFrom = new DateTime(2026, 10, 10),
        };

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsEmpty);
        Assert.Equal("За выбранный период записей нет", viewModel.EmptyText);

        viewModel.ClearDateFromCommand.Execute(null);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsEmpty);
        Assert.Equal("Записей пока нет", viewModel.EmptyText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(DaysCountRules.MaxDaysCount + 1)]
    public void ApplyCommand_НедопустимоеКоличество_КнопкаГасится(int count)
    {
        using var database = new TemporaryDatabase();
        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store)
        {
            DaysCount = count,
        };

        Assert.False(viewModel.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadAsync_ЗаписейНет_СписокПуст()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository, _settings.Store);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsEmpty);
    }

    /// <summary>
    /// Сохранённый день появляется в списке без перезапуска окна: окно дня оповещает о записи
    /// событием, и главное окно перечитывает журнал.
    /// </summary>
    [Fact]
    public async Task AddDayCommand_ДеньСохранён_СписокПеречитан()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Пользователь сохранил день (запись появилась в базе) — окно дня оповестило об этом.
        await database.SessionRepository.AddAsync(
            Session(new DateOnly(2026, 10, 6), "День 6"));

        viewModel.AddDayCommand.Execute(null);
        await windowService.RaiseDaySaved();

        Assert.Equal(1, windowService.AddDayCalls);
        Assert.Equal(["День 6", "День 5"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>
    /// День, сохранённый открытым окном, перечитывается и тогда, когда окно ещё живо: окно
    /// дня немодальное, и «Сохранить» без закрытия не отменяет перечитывание.
    /// </summary>
    [Fact]
    public async Task AddDayCommand_СохраненоБезЗакрытия_СписокПеречитан()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Окно сохранило день и осталось открытым — оповещение всё равно приходит.
        await database.SessionRepository.AddAsync(
            Session(new DateOnly(2026, 10, 6), "День 6"));

        viewModel.AddDayCommand.Execute(null);
        await windowService.RaiseDaySaved();
        viewModel.AddDayCommand.Execute(null);
        await windowService.RaiseDaySaved();

        Assert.Equal(2, windowService.AddDayCalls);
        Assert.Equal(["День 6", "День 5"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>
    /// Журнал перечитывается только по оповещению о записи: открытие окна само по себе ничего
    /// не меняет.
    /// </summary>
    [Fact]
    public async Task AddDayCommand_Отменено_СписокНеПеречитывается()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.AddDayCommand.Execute(null);

        Assert.Equal(1, windowService.AddDayCalls);
        Assert.Equal(["День 5"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>
    /// Ширина столбца упражнения равна сумме его подходов, а ширина подхода одна и та же
    /// везде: на этом стоит «вес строго над повторением» и совпадение столбцов между
    /// упражнениями.
    /// </summary>
    [Fact]
    public void DayRowViewModel_СтолбцыУпражнений_ШиринаОтПодходов()
    {
        var session = Session(new DateOnly(2026, 10, 5));

        var legs = session.AddExercise("Приседание");
        legs.AddSet(8, 60m);
        legs.AddSet(6, 70m);

        session.AddExercise("Жим").AddSet(5, 60m);

        var row = DayRowViewModel.Create(session);

        Assert.Equal(
            [DayRowViewModel.CellWidth * 2, DayRowViewModel.CellWidth],
            row.Exercises.Select(exercise => exercise.Width));

        Assert.Equal(
            ["60", "70", "60"],
            row.Sets.Select(set => set.WeightText));

        Assert.Equal(
            ["8", "6", "5"],
            row.Sets.Select(set => set.RepetitionText));

        Assert.All(row.Sets, set => Assert.Equal(DayRowViewModel.CellWidth, set.CellWidth));
    }

    [Fact]
    public void DayRowViewModel_УпражнениеБезПодходов_ЗанимаетОдинСтолбец()
    {
        var session = Session(new DateOnly(2026, 10, 5));
        session.AddExercise("Жим");

        var row = DayRowViewModel.Create(session);

        Assert.Equal(DayRowViewModel.CellWidth, Assert.Single(row.Exercises).Width);
        Assert.Empty(row.Sets);
    }

    /// <summary>
    /// Дата строки журнала — словом месяц и с годом, плюс сокращение дня недели по текущей
    /// культуре. Здесь важно, что строка собирает формат даты и подпись дня недели, — сам
    /// формат проверяется тестом рядом, на всех двенадцати месяцах.
    /// </summary>
    [Fact]
    public void DayRowViewModel_Дата_СоСокращённымДнёмНедели()
    {
        using var culture = CultureScope.Use("ru-RU");

        var row = DayRowViewModel.Create(Session(new DateOnly(2026, 10, 5)));

        Assert.Equal("05 октября 2026 (пн)", row.DateText);
    }

    /// <summary>
    /// Примечание доходит до ячейки упражнения, а у упражнения без примечания показывать
    /// нечего: вместо кнопки «Показать» в строке журнала стоит прочерк.
    /// </summary>
    [Fact]
    public void DayRowViewModel_ПримечаниеУпражнения_ПопадаетВЯчейку()
    {
        var session = Session(new DateOnly(2026, 10, 5));

        session.AddExercise("Жим").AddSet(5, 60m);
        session.AddExercise("Приседание", "Болело левое плечо").AddSet(8, 70m);

        var row = DayRowViewModel.Create(session);

        var withoutNote = row.Exercises[0];
        Assert.False(withoutNote.HasNote);
        Assert.Null(withoutNote.Note);

        var withNote = row.Exercises[1];
        Assert.True(withNote.HasNote);
        Assert.Equal("Болело левое плечо", withNote.Note);
    }

    /// <summary>
    /// Примечание из одних пробелов примечанием не считается. В базу оно таким и не пишется
    /// (<c>NormalizeNote</c>), но показывать «Показать» не о чем и при таком значении нельзя.
    /// </summary>
    [Fact]
    public void DayRowViewModel_ПримечаниеИзПробелов_СчитаетсяОтсутствующим()
    {
        var session = Session(new DateOnly(2026, 10, 5));
        session.AddExercise("Жим", "   ").AddSet(5, 60m);

        var row = DayRowViewModel.Create(session);

        Assert.False(Assert.Single(row.Exercises).HasNote);
    }

    /// <summary>
    /// Правка дня из строки журнала открывает окно на дату этой строки: по дате модель сама
    /// находит запись и открывается на правку.
    /// </summary>
    [Fact]
    public async Task EditDayCommand_СтрокаДня_ОткрываетПравкуЭтойДаты()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        var row = Assert.Single(viewModel.Days);

        viewModel.EditDayCommand.Execute(row);

        Assert.Equal(1, windowService.EditDayCalls);
        Assert.Equal(new DateOnly(2026, 10, 5), windowService.LastEditDay);
    }

    /// <summary>
    /// Правка дня, как и добавление, перечитывает журнал по оповещению о записи: пока окно
    /// открыто, день могли поправить, и без перечитки строка осталась бы старой.
    /// </summary>
    [Fact]
    public async Task EditDayCommand_ДеньПоправлен_СписокПеречитан()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await database.SessionRepository.AddAsync(Session(new DateOnly(2026, 10, 6), "День 6"));

        var row = Assert.Single(viewModel.Days);

        viewModel.EditDayCommand.Execute(row);
        await windowService.RaiseDaySaved();

        Assert.Equal(["День 6", "День 5"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>
    /// Команда без строки не открывает ничего: правки несуществующего дня не бывает, а падать
    /// на <c>null</c> пользователю не за что.
    /// </summary>
    [Fact]
    public async Task EditDayCommand_БезСтроки_НичегоНеОткрывает()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var windowService = new FakeWindowService();
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.EditDayCommand.Execute(null);

        Assert.Equal(0, windowService.EditDayCalls);
    }

    /// <summary>
    /// Формат даты приложения: «08 октября 2026».
    /// </summary>
    /// <remarks>
    /// Месяцев двенадцать, и формы родительного падежа у них разные: «мая», «июня», «июля»
    /// по аналогии с остальными не выводятся. Поэтому проверяются все, а не пара: ошибка
    /// была бы в одном имени из двенадцати и заметила бы себя в журнале, а не в тесте.
    /// Заодно проверяется ведущий ноль у дня и отсутствие точки в разделителях — ровно то,
    /// ради чего формат собирается вручную вместо culture-шаблона.
    /// </remarks>
    [Theory]
    [InlineData("08 октября 2026", 2026, 10, 8)]
    [InlineData("01 января 2026", 2026, 1, 1)]
    [InlineData("08 марта 2026", 2026, 3, 8)]
    [InlineData("01 апреля 2026", 2026, 4, 1)]
    [InlineData("05 мая 2026", 2026, 5, 5)]
    [InlineData("30 июня 2026", 2026, 6, 30)]
    [InlineData("31 июля 2026", 2026, 7, 31)]
    [InlineData("31 августа 2026", 2026, 8, 31)]
    [InlineData("15 сентября 2026", 2026, 9, 15)]
    [InlineData("09 ноября 2026", 2026, 11, 9)]
    [InlineData("28 февраля 2027", 2027, 2, 28)]
    [InlineData("31 декабря 2026", 2026, 12, 31)]
    public void Dates_Формат_МесяцСловом(string expected, int year, int month, int day) =>
        Assert.Equal(expected, Dates.Format(new DateOnly(year, month, day)));

    /// <summary>
    /// Сокращение дня недели берётся у текущей культуры — то же, что показывает строка журнала.
    /// Ожидаемые значения — литералы двух культур: вычисление ожидания тем же API, что и в
    /// продакшене, не поймало бы ни неверный день, ни уход от культуры машины.
    /// </summary>
    [Fact]
    public void Dates_СокращённыйДеньНедели_ПоКультуре()
    {
        var monday = new DateOnly(2026, 11, 9);

        using (CultureScope.Use("ru-RU"))
        {
            Assert.Equal("пн", Dates.Weekday(monday));
        }

        using (CultureScope.Use("en-US"))
        {
            Assert.Equal("Mon", Dates.Weekday(monday));
        }
    }

    private static TrainingSession Session(DateOnly date, string planName = "День") =>
        new() { Date = date, PlanName = planName };

    private static async Task AddSessionAsync(TemporaryDatabase database, DateOnly date, string planName)
    {
        var session = Session(date, planName);

        await database.SessionRepository.AddAsync(session);
    }
}