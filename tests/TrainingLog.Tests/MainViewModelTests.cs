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
    /// Параметры попадают в главное окно сразу: окно перечитывает файл, а не ждёт перезапуска, и
    /// показанные дни перечитываются вместе с полем — иначе «Сохранить» применял бы настройку,
    /// но её результата видно не было бы.
    /// </summary>
    /// <remarks>
    /// Пять дней в журнале, показывается три, сохраняется два: проверяется количество строк, а
    /// без перечитывания остались бы прежние три.
    /// </remarks>
    [Fact]
    public async Task OpenSettingsCommand_ПараметрыСохранены_ПолеИСписокОбновились()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var windowService = new FakeWindowService
        {
            SettingsResult = true,
            OnSettings = () => _settings.Store.Save(new AppSettings { DaysCount = 2 }),
        };

        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Days.Count);

        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(1, windowService.SettingsCalls);
        Assert.Equal(2, viewModel.DaysCount);
        Assert.Equal(2, viewModel.Days.Count);
        Assert.Equal(["День 5", "День 4"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>Отмена не трогает ни поля, ни списка: окно не записало файл.</summary>
    [Fact]
    public async Task OpenSettingsCommand_Отменено_ПолеИСписокНеМеняются()
    {
        using var database = new TemporaryDatabase();
        for (var day = 1; day <= 5; day++)
        {
            await AddSessionAsync(database, new DateOnly(2026, 10, day), $"День {day}");
        }

        var windowService = new FakeWindowService { SettingsResult = false };
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

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

        var windowService = new FakeWindowService
        {
            SettingsResult = true,
            OnSettings = () => _settings.Store.Save(new AppSettings { DatabaseDirectory = @"D:\базы" }),
        };

        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);

        var messages = new List<string>();
        viewModel.DatabaseAddressChanged += message => messages.Add(message);

        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

        var message = Assert.Single(messages);

        Assert.Contains("следующем запуске", message);
    }

    /// <summary>Адрес не менялся — события нет: сообщение на каждый запуск параметров мешало бы.</summary>
    [Fact]
    public async Task OpenSettingsCommand_АдресБазыНеИзменился_СобытияНет()
    {
        using var database = new TemporaryDatabase();

        var windowService = new FakeWindowService
        {
            SettingsResult = true,
            OnSettings = () => _settings.Store.Save(new AppSettings { DaysCount = 7 }),
        };

        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);

        var raised = false;
        viewModel.DatabaseAddressChanged += _ => raised = true;

        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

        Assert.False(raised);
    }

    /// <summary>Отказ окна параметров — и когда файл менялся, событие не поднимается: правку не приняли.</summary>
    [Fact]
    public async Task OpenSettingsCommand_Отменено_СобытияНеБывает()
    {
        using var database = new TemporaryDatabase();

        var windowService = new FakeWindowService
        {
            SettingsResult = false,
            OnSettings = () => _settings.Store.Save(new AppSettings { DatabaseDirectory = @"D:\базы" }),
        };

        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);

        var raised = false;
        viewModel.DatabaseAddressChanged += _ => raised = true;

        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

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
    /// Сохранённый день появляется в списке без перезапуска окна: главное окно перечитывает
    /// журнал после диалога.
    /// </summary>
    [Fact]
    public async Task AddDayCommand_ДеньСохранён_СписокПеречитан()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var windowService = new FakeWindowService { AddDayResult = true };
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await database.SessionRepository.AddAsync(
            Session(new DateOnly(2026, 10, 6), "День 6"));

        await viewModel.AddDayCommand.ExecuteAsync(null);

        Assert.Equal(1, windowService.AddDayCalls);
        Assert.Equal(["День 6", "День 5"], viewModel.Days.Select(day => day.PlanName));
    }

    /// <summary>
/// «Сохранить» в окне дня не закрывает его: <c>DialogResult</c> остаётся <c>null</c>, и окно
/// закрывают крестиком или Esc. Ответ <c>ShowAddDay</c> собирается из обоих состояний окна,
/// иначе сохранённый день не появился бы в журнале до перезапуска.
/// </summary>
[Fact]
public async Task AddDayCommand_СохраненоБезЗакрытия_СписокПеречитан()
{
    using var database = new TemporaryDatabase();
    await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

    // Окно сохранило день и закрылось крестиком: DialogResult не выставлен, но день записан.
    var windowService = new FakeWindowService
    {
        OnAddDay = () => database.SessionRepository.AddAsync(Session(new DateOnly(2026, 10, 6), "День 6")),
        AddDayResult = true,
    };

    var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
    await viewModel.LoadCommand.ExecuteAsync(null);

    await viewModel.AddDayCommand.ExecuteAsync(null);

    Assert.Equal(1, windowService.AddDayCalls);
    Assert.Equal(["День 6", "День 5"], viewModel.Days.Select(day => day.PlanName));
}

[Fact]
public async Task AddDayCommand_Отменено_СписокНеПеречитывается()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var viewModel = new MainViewModel(new FakeWindowService { AddDayResult = false }, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.AddDayCommand.ExecuteAsync(null);

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
        var row = DayRowViewModel.Create(Session(new DateOnly(2026, 10, 5)));

        var abbreviated = System.Globalization.CultureInfo.CurrentCulture
            .DateTimeFormat.GetAbbreviatedDayName(new DateOnly(2026, 10, 5).DayOfWeek);

        Assert.Equal($"05 октября 2026 ({abbreviated})", row.DateText);
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

        await viewModel.EditDayCommand.ExecuteAsync(row);

        Assert.Equal(1, windowService.EditDayCalls);
        Assert.Equal(new DateOnly(2026, 10, 5), windowService.LastEditDay);
    }

    /// <summary>
    /// Правка дня, как и добавление, перечитывает журнал: пока окно открыто, день могли
    /// поправить, и без перечитки строка осталась бы старой.
    /// </summary>
    [Fact]
    public async Task EditDayCommand_ДеньПоправлен_СписокПеречитан()
    {
        using var database = new TemporaryDatabase();
        await AddSessionAsync(database, new DateOnly(2026, 10, 5), "День 5");

        var windowService = new FakeWindowService { EditDayResult = true };
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await database.SessionRepository.AddAsync(Session(new DateOnly(2026, 10, 6), "День 6"));

        var row = Assert.Single(viewModel.Days);

        await viewModel.EditDayCommand.ExecuteAsync(row);

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

        var windowService = new FakeWindowService { EditDayResult = true };
        var viewModel = new MainViewModel(windowService, database.SessionRepository, _settings.Store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.EditDayCommand.ExecuteAsync(null);

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
    [InlineData("05 мая 2026", 2026, 5, 5)]
    [InlineData("30 июня 2026", 2026, 6, 30)]
    [InlineData("31 июля 2026", 2026, 7, 31)]
    [InlineData("15 сентября 2026", 2026, 9, 15)]
    [InlineData("28 февраля 2027", 2027, 2, 28)]
    [InlineData("31 декабря 2026", 2026, 12, 31)]
    public void Dates_Формат_МесяцСловом(string expected, int year, int month, int day) =>
        Assert.Equal(expected, Dates.Format(new DateOnly(year, month, day)));

    private static TrainingSession Session(DateOnly date, string planName = "День") =>
        new() { Date = date, PlanName = planName };

    private static async Task AddSessionAsync(TemporaryDatabase database, DateOnly date, string planName)
    {
        var session = Session(date, planName);

        await database.SessionRepository.AddAsync(session);
    }
}