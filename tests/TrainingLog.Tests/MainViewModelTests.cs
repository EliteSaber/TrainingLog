using TrainingLog.Core.Models;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Главное окно. Проверяются три вещи: отбор последних дней, применение количества дней
/// кнопкой и строка дня — раскладка столбцов, от которой зависит, встанет ли вес над своим
/// повторением.
/// </summary>
public sealed class MainViewModelTests
{
    [Fact]
    public void ПоУмолчанию_ПоказыватьТриДня()
    {
        using var database = new TemporaryDatabase();

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository);

        Assert.Equal(3, viewModel.DaysCount);
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

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository);
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

        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository);
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
    [InlineData(MainViewModel.MaxDaysCount + 1)]
    public void ApplyCommand_НедопустимоеКоличество_КнопкаГасится(int count)
    {
        using var database = new TemporaryDatabase();
        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository)
        {
            DaysCount = count,
        };

        Assert.False(viewModel.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadAsync_ЗаписейНет_СписокПуст()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new MainViewModel(new FakeWindowService(), database.SessionRepository);

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
        var viewModel = new MainViewModel(windowService, database.SessionRepository);
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

    var viewModel = new MainViewModel(windowService, database.SessionRepository);
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

        var viewModel = new MainViewModel(new FakeWindowService { AddDayResult = false }, database.SessionRepository);
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