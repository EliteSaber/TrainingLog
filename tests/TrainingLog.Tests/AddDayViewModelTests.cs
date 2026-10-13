using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Окно добавления дня. Запись за дату одна: окно подтягивает её и правит на месте, а вторую
/// запись за ту же дату не создаёт.
/// </summary>
public sealed class AddDayViewModelTests
{
    [Fact]
    public async Task InitializeAsync_План_КнопкиПоНомерамСУпражнениями()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

        var viewModel = CreateViewModel(database);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        var plan = Assert.Single(viewModel.Plans);

        viewModel.SelectedPlan = plan;

        Assert.Equal([1, 2], viewModel.Exercises.Select(row => row.Number));
        Assert.Equal(["Приседание", "Жим"], viewModel.Exercises.Select(row => row.Name));

        // По умолчанию активно первое упражнение — с пустым выбором открывать окно незачем.
        Assert.True(viewModel.Exercises[0].IsSelected);
        Assert.Same(viewModel.Exercises[0], viewModel.Current);
    }

    [Fact]
    public async Task НавигацияПоУпражнениям_КрайниеКнопкиГасят()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

        var viewModel = await CreateInitializedAsync(database);
        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

        Assert.False(viewModel.PreviousCommand.CanExecute(null));
        Assert.True(viewModel.NextCommand.CanExecute(null));

        viewModel.NextCommand.Execute(null);

        Assert.Same(viewModel.Exercises[1], viewModel.Current);
        Assert.True(viewModel.PreviousCommand.CanExecute(null));
        Assert.False(viewModel.NextCommand.CanExecute(null));
    }

    /// <remarks>
    /// Счётчик <c>CanExecuteChanged</c>, а не <c>CanExecute</c>: WPF держит состояние до события и
    /// сам предикат не зовёт, поэтому тест на предикат всегда прав (ловушка 24).
    /// </remarks>
    [Fact]
    public async Task ОткрытиеОкна_НавигацияГотоваБезНажатияНаНомер()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);

        // За день запись уже есть: на пустом дне план не подставляется и навигации нет. Именно
        // этот путь ломался — индекс и так ноль, уведомления от сеттера не было.
        var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим", "Тяга");

        var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
        session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
        await database.SessionRepository.AddAsync(session);

        // Подписка до InitializeCommand: в приложении окно открывается так же, и состав
        // упражнений появляется уже во время загрузки.
        var viewModel = CreateViewModel(database);

        var raised = 0;
        viewModel.PreviousCommand.CanExecuteChanged += (_, _) => raised++;
        viewModel.NextCommand.CanExecuteChanged += (_, _) => raised++;

        viewModel.Date = day.ToDateTime(TimeOnly.MinValue);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Exercises.Count);
        Assert.True(raised > 0);
        Assert.False(viewModel.PreviousCommand.CanExecute(null));
        Assert.True(viewModel.NextCommand.CanExecute(null));

        viewModel.NextCommand.Execute(null);

        Assert.Same(viewModel.Exercises[1], viewModel.Current);
    }

    /// <summary>
    /// Пересборка состава обязана трогать навигацию: индекс при смене плана и так ноль, поэтому
    /// пересчёт обязан жить в <c>Refresh</c>, а не на сеттере индекса (ловушка 25).
    /// </summary>
    [Fact]
    public async Task СменаПлана_ПересчитываетНавигацию()
    {
        using var database = new TemporaryDatabase();

        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        // Упражнения у планов разные: наименования в справочнике уникальны.
        await CreatePlanAsync(database, "Одно", squat);

        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(bench);
        var row = new Exercise { Name = "Тяга" };
        await database.Repository.AddAsync(row);

        await CreatePlanAsync(database, "Три", squat, bench, row);

        var viewModel = await CreateInitializedAsync(database);
        viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Одно");

        var raised = 0;
        viewModel.PreviousCommand.CanExecuteChanged += (_, _) => raised++;
        viewModel.NextCommand.CanExecuteChanged += (_, _) => raised++;

        viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Три");

        Assert.True(raised > 0);
        Assert.True(viewModel.NextCommand.CanExecute(null));
    }

    /// <summary>
    /// У дат с одним планом сеттер <c>SelectedPlan</c> не срабатывает, поэтому сброс активного
    /// упражнения обязан быть явным в месте, где читается день.
    /// </summary>
    [Fact]
    public async Task СменаДатыНаТотЖеПлан_СбрасываетАктивноеУпражнение()
    {
        using var database = new TemporaryDatabase();
        var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим", "Тяга");

        var first = new DateOnly(2026, 10, 5);
        var second = new DateOnly(2026, 10, 6);

        foreach (var day in new[] { first, second })
        {
            var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
            session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
            await database.SessionRepository.AddAsync(session);
        }

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, first);

        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);

        Assert.Equal(2, viewModel.CurrentIndex);

        await LoadDayAsync(viewModel, second);

        Assert.Same(viewModel.Exercises[0], viewModel.Current);
        Assert.Equal(0, viewModel.CurrentIndex);
        Assert.True(viewModel.Exercises[0].IsSelected);
        Assert.False(viewModel.PreviousCommand.CanExecute(null));
        Assert.True(viewModel.NextCommand.CanExecute(null));
    }

    [Fact]
    public async Task SelectExerciseCommand_КнопкаУпражнения_ДелаетЕгоАктивным()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

        var viewModel = await CreateInitializedAsync(database);
        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

        viewModel.SelectExerciseCommand.Execute(viewModel.Exercises[1]);

        Assert.Same(viewModel.Exercises[1], viewModel.Current);
        Assert.False(viewModel.Exercises[0].IsSelected);
    }

    /// <summary>
    /// Смена плана переписывает состав дня: иначе подходы старого плана уехали бы в запись нового.
    /// </summary>
    [Fact]
    public async Task СменаПлана_ПересобираетУпражненияИЧиститПодходы()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");
        await CreatePlanAsync(database, "Верх", "Жим лёжа");

        var viewModel = await CreateInitializedAsync(database);

        viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Ноги");
        viewModel.Current!.Sets[0].WeightText = "60";

        viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Верх");

        Assert.Equal("Жим лёжа", Assert.Single(viewModel.Exercises).Name);
        Assert.Equal(string.Empty, Assert.Single(viewModel.Exercises[0].Sets).WeightText);
    }

    /// <summary>
    /// В подходах одного упражнения повторения меняются чаще веса, поэтому наследуется вес.
    /// </summary>
    [Fact]
    public async Task AddSetCommand_НовыйПодход_БерётВесПредыдущего()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание");

        var viewModel = await CreateInitializedAsync(database);
        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

        viewModel.Current!.Sets[0].WeightText = "60";
        viewModel.Current.Sets[0].RepetitionText = "8";

        viewModel.AddSetCommand.Execute(null);

        Assert.Equal(2, viewModel.Current.Sets.Count);
        Assert.Equal("60", viewModel.Current.Sets[1].WeightText);
        Assert.Equal(string.Empty, viewModel.Current.Sets[1].RepetitionText);
    }

    [Fact]
    public async Task RemoveSetCommand_УбираетСамыйПравыйПодход()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание");

        var viewModel = await CreateInitializedAsync(database);
        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

        viewModel.Current!.Sets[0].WeightText = "60";
        viewModel.AddSetCommand.Execute(null);
        viewModel.Current.Sets[1].WeightText = "70";

        viewModel.RemoveSetCommand.Execute(null);

        Assert.Equal("60", Assert.Single(viewModel.Current.Sets).WeightText);

        // Убираются все поля: упражнение остаётся в плане, подход набирается заново кнопкой «+».
        viewModel.RemoveSetCommand.Execute(null);

        Assert.Empty(viewModel.Current.Sets);
        Assert.False(viewModel.RemoveSetCommand.CanExecute(null));
        Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));
    }

    /// <summary>
    /// Подписка на ввод нужна и на только что добавленном подходе: иначе «Сохранить» осталась бы
    /// активной с негодными данными, и нажатие молча ничего бы не сделало (ловушка 24).
    /// </summary>
    [Fact]
    public async Task ПравкаТекстаВДобавленномПодходе_ОбновляетАктивностьСохранения()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);
        await CreatePlanAsync(database, "Ноги", "Приседание");

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, day);

        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

        var row = viewModel.Current!;

        row.Sets[0].WeightText = "60";
        Assert.True(viewModel.SaveAndCloseCommand.CanExecute(null));

        viewModel.AddSetCommand.Execute(null);
        row.Sets[1].WeightText = "много";

        Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));

        var raised = 0;
        viewModel.SaveAndCloseCommand.CanExecuteChanged += (_, _) => raised++;
        row.Sets[1].RepetitionText = "8";

        Assert.True(raised > 0);

        row.Sets[1].WeightText = "70";
        row.Sets[1].RepetitionText = "6";

        Assert.True(viewModel.SaveAndCloseCommand.CanExecute(null));

        await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Equal([60m, 70m], (await database.SessionRepository.GetByDateAsync(day))!
            .Exercises.Single().Sets.OrderBy(set => set.Order).Select(set => set.Weight));
    }

[Fact]
public async Task SaveCommand_СохраняетНоНеЗакрываетОкно()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    await viewModel.SaveCommand.ExecuteAsync(null);

    Assert.True(viewModel.Saved);

    // DialogResult не выставится — Accepted остаётся false, и окно не закроется.
    Assert.False(viewModel.Accepted);

    var stored = await database.SessionRepository.GetByDateAsync(day);

    Assert.NotNull(stored);
    Assert.Equal(60m, Assert.Single(Assert.Single(stored.Exercises).Sets).Weight);
}

/// <summary>
/// Запись за дату одна, поэтому второе сохранение обязано пойти как правка: без присваивания
/// <c>_existing</c> оно ушло бы в <c>AddAsync</c> и упёрлось бы в занятую дату.
/// </summary>
[Fact]
public async Task SaveCommand_Дважды_ОднаЗаписьСоВторымПланом()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    await viewModel.SaveCommand.ExecuteAsync(null);

    // Заголовок — признак правки: в базе запись за дату уже есть.
    Assert.Equal("Правка дня", viewModel.Title);

    var sessionId = (await database.SessionRepository.GetByDateAsync(day))!.Id;

    // Правка идёт через модель, а не подменой базы: подмена занесла бы в модель упражнения с
    // идентификаторами, и второй SaveAndClose прошёл бы даже при нулевом Id.
    viewModel.Current!.Sets[0].WeightText = "70";

    await viewModel.SaveCommand.ExecuteAsync(null);

    // Пустой Status — часть проверки: отказ по занятой дате обновил бы день в базе, но оставил
    // бы надпись, и пользователь решил бы, что правка не сработала.
    Assert.Equal(string.Empty, viewModel.Status);

    var stored = await database.SessionRepository.GetByDateAsync(day);

    Assert.Equal(sessionId, stored!.Id);
    Assert.Equal(70m, Assert.Single(Assert.Single(stored.Exercises).Sets).Weight);
    Assert.Single(await database.SessionRepository.GetAsync());
}

/// <summary>
/// Accepted выставляется только здесь — по нему окно ставит DialogResult.
/// </summary>
[Fact]
public async Task SaveAndCloseCommand_СохраняетИПроситЗакрыться()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);
    var notifications = TrackNotifications(viewModel);

    await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

    Assert.True(viewModel.Accepted);
    Assert.True(viewModel.Saved);
    Assert.Contains(nameof(AddDayViewModel.Accepted), notifications);
}

/// <summary>
/// Через «Сохранить» без закрытия идентификатор записи тоже должен доехать до модели: при нулевом
/// второе сохранение ушло бы в <c>AddAsync</c> и упало бы на внешнем ключе или на занятой дате.
/// </summary>
[Fact]
public async Task SaveCommand_Дважды_ИдентификаторЗаписиДоехалДоМодели()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    await viewModel.SaveCommand.ExecuteAsync(null);

    viewModel.Current!.Sets[0].WeightText = "70";

    await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

    Assert.Equal(string.Empty, viewModel.Status);

    var stored = await database.SessionRepository.GetByDateAsync(day);

    Assert.Equal(70m, Assert.Single(Assert.Single(stored!.Exercises).Sets).Weight);
    Assert.Single(await database.SessionRepository.GetAsync());
}

/// <summary>
/// Иначе главное окно перечитает журнал после окна, в котором ничего не записалось.
/// </summary>
[Fact]
public async Task SaveCommand_Отказ_НеСчитаетсяСохранением()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    // Запись занимает дату, пока окно считает, что её нет: так отказ проверяется по существу,
    // а не по неактивной кнопке.
    var taken = new TrainingSession { Date = day, PlanName = "Занято" };
    await database.SessionRepository.AddAsync(taken);

    await viewModel.SaveCommand.ExecuteAsync(null);

    Assert.False(viewModel.Saved);
    Assert.False(viewModel.Accepted);
    Assert.Equal("Занято", (await database.SessionRepository.GetByDateAsync(day))!.PlanName);
}

/// <summary>
/// Мусор в поле гасит обе кнопки одинаково: «Сохранить» висит ещё и на Ctrl+S, а горячая клавиша
/// при серой кнопке звать сохранение не должна.
/// </summary>
[Theory]
[InlineData("SaveCommand")]
[InlineData("SaveAndCloseCommand")]
public async Task ОбеКнопкиГасятсяНаМусореВПоле(string command)
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan, weight: "столько");

    var raised = 0;

    var save = viewModel.SaveCommand;
    var saveAndClose = viewModel.SaveAndCloseCommand;

    var isSave = command == nameof(AddDayViewModel.SaveCommand);

    var target = isSave ? save : saveAndClose;
    var other = isSave ? saveAndClose : save;

    target.CanExecuteChanged += (_, _) => raised++;
    other.CanExecuteChanged += (_, _) => raised++;

    // Счётчик ловит уведомление, а не состояние: CanExecute у предиката всегда прав (ловушка 24).
    viewModel.Current!.Sets[0].RepetitionText = "10";

    Assert.True(raised > 0);
    Assert.False(target.CanExecute(null));
    Assert.False(other.CanExecute(null));
}

[Fact]
public async Task AddSetCommand_БезПлана_КомандаНеактивна()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание");

        var viewModel = await CreateInitializedAsync(database);

        Assert.Null(viewModel.Current);
        Assert.False(viewModel.AddSetCommand.CanExecute(null));
        Assert.False(viewModel.RemoveSetCommand.CanExecute(null));
        Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveAndCloseAsync_ПодходыУпражнений_СохраняетДень()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);
        await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, day);

        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

        var legs = viewModel.Exercises[0];
        legs.Sets[0].WeightText = "60";
        legs.Sets[0].RepetitionText = "8";
        viewModel.AddSetCommand.Execute(null);
        legs.Sets[1].WeightText = "70";
        legs.Sets[1].RepetitionText = "6";

        // Жим без единого подхода в запись не попадает: невыполненное упражнение в журнале шумит.
        Assert.True(viewModel.SaveAndCloseCommand.CanExecute(null));

        await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);

        var stored = await database.SessionRepository.GetByDateAsync(day);

        Assert.NotNull(stored);
        Assert.Equal("Ноги", stored.PlanName);

        var entry = Assert.Single(stored.Exercises);
        Assert.Equal("Приседание", entry.ExerciseName);
        Assert.Equal(1, entry.Order);
        Assert.Equal(
            [(60m, 8), (70m, 6)],
            entry.Sets.OrderBy(set => set.Order).Select(set => (set.Weight, set.Repetitions)));
    }

    [Fact]
    public async Task SaveAndCloseAsync_МусорВПоле_КнопкаГаситИДеньНеПишется()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);
        await CreatePlanAsync(database, "Ноги", "Приседание");

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, day);

        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);
        viewModel.Current!.Sets[0].WeightText = "столько";

        Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));

        await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

        Assert.False(viewModel.Accepted);
        Assert.Empty(await database.SessionRepository.GetAsync());
    }

    [Fact]
    public async Task SaveAndCloseAsync_ОтрицательныйВес_НеСохраняется()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);
        await CreatePlanAsync(database, "Ноги", "Приседание");

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, day);

        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);
        viewModel.Current!.Sets[0].RepetitionText = "-3";

        Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));
        Assert.Empty(await database.SessionRepository.GetAsync());
    }

    /// <summary>
    /// На дату запись одна, поэтому окно правит её на месте, а вторую запись за ту же дату не
    /// создаёт.
    /// </summary>
    [Fact]
    public async Task ЗаДатуЕстьЗапись_ОткрываетсяПравка()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);
        var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

        var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
        var legs = session.AddExercise(plan.Exercises[0]);
        legs.AddSet(8, 60m);
        legs.AddSet(6, 70m);
        await database.SessionRepository.AddAsync(session);

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, day);

        Assert.Equal("Правка дня", viewModel.Title);

        var row = viewModel.Exercises[0];

        Assert.Equal(
            ["60", "70"],
            row.Sets.Select(set => set.WeightText));
        Assert.Equal(
            ["8", "6"],
            row.Sets.Select(set => set.RepetitionText));

        // Правка без изменений сохраняется: день, набранный заново на ту же дату, тоже.
        row.Sets[0].RepetitionText = "10";

        await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Single(await database.SessionRepository.GetAsync());

        var stored = await database.SessionRepository.GetByDateAsync(day);
        Assert.Equal(10, stored!.Exercises.Single().Sets.First().Repetitions);
    }

    [Fact]
    public async Task СменаДаты_ЧиститПодходыПрежнейЗаписи()
    {
        using var database = new TemporaryDatabase();
        var stored = await CreatePlanAsync(database, "Прежний", "Приседание");
        var next = await CreatePlanAsync(database, "Ноги", "Жим");

        // Запись прежнего дня с другим планом: подтягивание по нему не сработает, и проверяется
        // именно очистка полей, а не отказ от показа прошлых весов.
        var first = new DateOnly(2026, 10, 5);
        var session = new TrainingSession { Date = first, PlanId = stored.Id, PlanName = stored.Name };
        session.AddExercise(stored.Exercises[0]).AddSet(8, 60m);
        await database.SessionRepository.AddAsync(session);

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, first);

        Assert.Equal("60", Assert.Single(viewModel.Exercises[0].Sets).WeightText);

        await LoadDayAsync(viewModel, new DateOnly(2026, 10, 6));

        Assert.Empty(viewModel.Exercises);

        viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Ноги");

        await SettleAsync();

        Assert.Equal(string.Empty, Assert.Single(viewModel.Exercises[0].Sets).WeightText);
    }

    [Fact]
    public async Task ПланыЗагружаютсяПоАлфавиту()
    {
        using var database = new TemporaryDatabase();

        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);
        var lunge = new Exercise { Name = "Выпад" };
        await database.Repository.AddAsync(lunge);
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(bench);

        await CreatePlanAsync(database, "Ягодицы", squat);
        await CreatePlanAsync(database, "Верх", bench);
        await CreatePlanAsync(database, "Ноги", squat, lunge);

        var viewModel = await CreateInitializedAsync(database);

        Assert.Equal(["Верх", "Ноги", "Ягодицы"], viewModel.Plans.Select(plan => plan.Name));
    }

    /// <remarks>
    /// Уведомление об <c>Current</c> обязано идти из <c>Refresh</c>: индекс при пересборке состава
    /// и так ноль, а путь <c>Exercises</c> в привязке не участвует (ловушка 25).
    /// </remarks>
    [Fact]
    public async Task СменаПлана_СообщаетОТекущемУпражнении()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

        var viewModel = await CreateInitializedAsync(database);
        var notifications = TrackNotifications(viewModel);

        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

        Assert.Contains(nameof(AddDayViewModel.Current), notifications);
    }

    /// <summary>
    /// Открытие на дату с записью: активное упражнение и до загрузки первое, поэтому индекс не
    /// меняется и уведомление по нему не поднимается.
    /// </summary>
    [Fact]
    public async Task ЗаписьЗаДату_СообщаетОТекущемУпражнении()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);
        var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

        var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
        session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
        await database.SessionRepository.AddAsync(session);

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, day);

        var notifications = TrackNotifications(viewModel);

        // Повторная загрузка намеренно идёт через установку даты заново: в реальном окне дата
        // меняется только так, и этот путь обязан перечитывать запись.
        await LoadDayAsync(viewModel, day);

        Assert.Contains(nameof(AddDayViewModel.Current), notifications);
        Assert.Equal("60", Assert.Single(viewModel.Current!.Sets).WeightText);
    }

    /// <summary>
    /// Без уведомления подходы прежнего плана остались бы висеть в окне, к которому их не к чему
    /// привязать.
    /// </summary>
    [Fact]
    public async Task СнятиеПлана_СообщаетОТекущемУпражнении()
    {
        using var database = new TemporaryDatabase();
        await CreatePlanAsync(database, "Ноги", "Приседание");

        var viewModel = await CreateInitializedAsync(database);
        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

        var notifications = TrackNotifications(viewModel);

        viewModel.SelectedPlan = null;

        Assert.Null(viewModel.Current);
        Assert.Contains(nameof(AddDayViewModel.Current), notifications);
    }

    /// <remarks>
    /// Повторное чтение за ту же дату запрещено: <c>DatePicker</c> присоединяется со своим
    /// значением по умолчанию и двусторонней привязкой пишет его в источник (ловушка 27).
    /// </remarks>
    [Fact]
    public async Task ТаЖеДатаДругимВременем_НеПеречитываетБазу()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);
        await CreatePlanAsync(database, "Ноги", "Приседание");

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, day);

        var notifications = TrackNotifications(viewModel);

        viewModel.Date = day.ToDateTime(new TimeOnly(18, 30));

        await Task.Yield();
        await Task.Yield();

        // Каждая загрузка за день заканчивается уведомлением о заголовке, поэтому лишний
        // перезапуск команды виден по нему.
        Assert.DoesNotContain(nameof(AddDayViewModel.Title), notifications);
    }

/// <summary>
/// Наполнение обязано зваться из перестройки состава, а не только из смены даты: дата тут не
/// менялась (ловушка 26).
/// </summary>
[Fact]
public async Task СменаПланаИВозврат_ПодходыЗаписиВозвращаются()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var legs = await CreatePlanAsync(database, "Ноги", "Приседание");
    var upper = await CreatePlanAsync(database, "Верх", "Жим лёжа");

    var session = new TrainingSession { Date = day, PlanId = legs.Id, PlanName = legs.Name };
    session.AddExercise(legs.Exercises[0]).AddSet(8, 60m);
    await database.SessionRepository.AddAsync(session);

    var viewModel = await CreateInitializedAsync(database);
    viewModel.Date = day.ToDateTime(TimeOnly.MinValue);
    await viewModel.LoadDateCommand.ExecuteAsync(null);

    Assert.Equal("60", Assert.Single(viewModel.Current!.Sets).WeightText);

    viewModel.SelectedPlan = upper;
    Assert.Equal(string.Empty, Assert.Single(viewModel.Current!.Sets).WeightText);

    viewModel.SelectedPlan = legs;

    Assert.Equal("Правка дня", viewModel.Title);
    Assert.Equal("60", Assert.Single(viewModel.Current!.Sets).WeightText);
    Assert.Equal("8", Assert.Single(viewModel.Current!.Sets).RepetitionText);
}

/// <summary>
/// Гейт по плану, а не по названию упражнения: входящее в оба плана упражнение не должно
/// показывать подходы записи в полях чужого плана.
/// </summary>
[Fact]
public async Task СменаПланаНаПланСОбщимУпражнением_НеПоказываетПодходыЧужогоПлана()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);

    // Упражнение одно на оба плана: наименования в справочнике уникальны, второе «Приседание»
    // не добавилось бы.
    var squat = new Exercise { Name = "Приседание" };
    await database.Repository.AddAsync(squat);

    var saved = await CreatePlanAsync(database, "Ноги", squat);
    var other = await CreatePlanAsync(database, "Ягодицы", squat);

    var session = new TrainingSession { Date = day, PlanId = saved.Id, PlanName = saved.Name };
    session.AddExercise(squat).AddSet(8, 60m);
    await database.SessionRepository.AddAsync(session);

    var viewModel = await CreateInitializedAsync(database);
    viewModel.Date = day.ToDateTime(TimeOnly.MinValue);
    await viewModel.LoadDateCommand.ExecuteAsync(null);

    viewModel.SelectedPlan = other;

    Assert.Equal(
        [string.Empty],
        viewModel.Exercises.SelectMany(row => row.Sets).Select(set => set.WeightText));
}

[Fact]
public async Task СменаПланаИВозвратСПоследующимСохранением_ТаЖеЗаписьТеЖеПодходы()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var legs = await CreatePlanAsync(database, "Ноги", "Приседание");
    var upper = await CreatePlanAsync(database, "Верх", "Жим лёжа");

    var session = new TrainingSession { Date = day, PlanId = legs.Id, PlanName = legs.Name };
    session.AddExercise(legs.Exercises[0]).AddSet(8, 60m);
    await database.SessionRepository.AddAsync(session);

    var viewModel = await CreateInitializedAsync(database);
    viewModel.Date = day.ToDateTime(TimeOnly.MinValue);
    await viewModel.LoadDateCommand.ExecuteAsync(null);

    viewModel.SelectedPlan = upper;
    viewModel.SelectedPlan = legs;

    await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

    Assert.True(viewModel.Accepted);
    Assert.Single(await database.SessionRepository.GetAsync());

    var stored = await database.SessionRepository.GetByDateAsync(day);

    Assert.Equal(legs.Id, stored!.PlanId);
    Assert.Equal("Приседание", Assert.Single(stored.Exercises).ExerciseName);
    Assert.Equal(60m, Assert.Single(Assert.Single(stored.Exercises).Sets).Weight);
}

/// <summary>
/// Смена даты должна подтягивать запись именно этой даты и очищать подходы прежнего дня.
/// </summary>
[Fact]
public async Task СменаДатыНаДатуСЗаписью_ПодтягиваетЗаписьНовогоДня()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var first = new DateOnly(2026, 10, 5);
    var second = new DateOnly(2026, 10, 6);

    foreach (var (day, weight) in new[] { (first, 60m), (second, 70m) })
    {
        var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
        session.AddExercise(plan.Exercises[0]).AddSet(8, weight);
        await database.SessionRepository.AddAsync(session);
    }

    var viewModel = await CreateInitializedAsync(database);

    viewModel.Date = first.ToDateTime(TimeOnly.MinValue);
    await viewModel.LoadDateCommand.ExecuteAsync(null);

    Assert.Equal("60", Assert.Single(viewModel.Current!.Sets).WeightText);

    viewModel.Date = second.ToDateTime(TimeOnly.MinValue);
    await viewModel.LoadDateCommand.ExecuteAsync(null);

    Assert.Equal("Правка дня", viewModel.Title);
    Assert.Equal("70", Assert.Single(viewModel.Current!.Sets).WeightText);
}

    /// <remarks>
    /// Дата приходит в конструктор и кладётся в поле, минуя сеттер: сеттер зовёт
    /// <c>LoadDateCommand</c> до загрузки планов, и первое наполнение мигнуло бы «план удалён»
    /// (ловушка 27).
    /// </remarks>
    [Fact]
    public async Task ОкноНаДатуИзЖурнала_СразуПоказываетЗаписьЗаНей()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);
        var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

        var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
        session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
        await database.SessionRepository.AddAsync(session);

        var viewModel = new AddDayViewModel(database.SessionRepository, database.PlanRepository, day);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.Equal("Правка дня", viewModel.Title);
        Assert.Equal("Приседание", Assert.Single(viewModel.Exercises).Name);
        Assert.Equal("60", Assert.Single(viewModel.Current!.Sets).WeightText);
    }

    private static AddDayViewModel CreateViewModel(TemporaryDatabase database) =>
        new(database.SessionRepository, database.PlanRepository);


    private static AddDayViewModel CreateViewModel(TemporaryDatabase database, ITrainingSessionRepository sessions) =>
        new(sessions, database.PlanRepository);

    private static async Task<AddDayViewModel> CreateInitializedAsync(TemporaryDatabase database) =>
        await CreateInitializedAsync(database, database.SessionRepository);

    private static async Task<AddDayViewModel> CreateInitializedAsync(
        TemporaryDatabase database,
        ITrainingSessionRepository sessions)
    {
        var viewModel = CreateViewModel(database, sessions);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        return viewModel;
    }

    /// <remarks>
    /// Настоящая пауза (<c>Task.Delay</c>), а не <c>Task.Yield</c>: чтение возвращается в
    /// диспетчер, и число уступок в тесте ничего не гарантирует.
    /// </remarks>
    private static Task SettleAsync() => Task.Delay(50);

    /// <remarks>
    /// Пока гейт не отпущен, состояние не наступит никогда — ждать надо условия, а не времени.
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

    private static List<string?> TrackNotifications(AddDayViewModel viewModel)
    {
        var notifications = new List<string?>();

        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        return notifications;
    }

    /// <remarks>
    /// Дата задаётся явно: по умолчанию в окне сегодняшняя, и тест молча зависел бы от того, что
    /// запись записана именно на сегодня — такой тест и рассыпался в полночь.
    /// </remarks>
    private static async Task LoadDayAsync(AddDayViewModel viewModel, DateOnly day)
    {
        viewModel.Date = day.ToDateTime(TimeOnly.MinValue);
        await viewModel.LoadDateCommand.ExecuteAsync(null);
    }

    private static async Task<AddDayViewModel> CreateReadyToSaveAsync(
        TemporaryDatabase database,
        DateOnly day,
        TrainingPlan plan,
        string weight = "60",
        string repetitions = "8")
    {
        var viewModel = await CreateInitializedAsync(database);

        await LoadDayAsync(viewModel, day);

        viewModel.SelectedPlan = plan;
        viewModel.Current!.Sets[0].WeightText = weight;
        viewModel.Current.Sets[0].RepetitionText = repetitions;

        return viewModel;
    }

private static async Task<AddDayViewModel> CreateReadyWithPreviousDayAsync(TemporaryDatabase database)
    {
        var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

        var previous = new TrainingSession
        {
            Date = new DateOnly(2026, 10, 8),
            PlanId = plan.Id,
            PlanName = plan.Name,
        };

        previous.AddExercise(plan.Exercises[0]).AddSet(10, 60m);

        await database.SessionRepository.AddAsync(previous);

        var viewModel = await CreateInitializedAsync(database);

        await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

        viewModel.SelectedPlan = plan;

        await SettleAsync();

        return viewModel;
    }

    private static async Task<TrainingPlan> CreatePlanAsync(
        TemporaryDatabase database,
        string name,
        params string[] exercises) =>
        await CreatePlanAsync(database, name, await CreateExercisesAsync(database, exercises));

    private static Task<TrainingPlan> CreatePlanAsync(
        TemporaryDatabase database,
        string name,
        params Exercise[] exercises) =>
        CreatePlanAsync(database, name, (IEnumerable<Exercise>)exercises);

/// <remarks>
/// Снимок эталонного состояния обязан встать до первого уведомления, читающего его: надпись
/// считается против базы, и на первом открытии вспыхнула бы против «записи нет» (ловушка 28).
/// </remarks>
[Fact]
public async Task НадписьНаОткрытии_НеГоритБезЩелчковПоУпражнениям()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

    var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
    session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
    await database.SessionRepository.AddAsync(session);

    var viewModel = CreateViewModel(database);

    // Ловится значение, ушедшее в привязку в момент уведомления, а не итоговое: привязка берёт
    // его один раз и держит до следующего уведомления, поэтому итог был бы верным при горении.
    var raised = 0;
    var seen = new List<bool>();

    viewModel.PropertyChanged += (_, args) =>
    {
        if (args.PropertyName == nameof(AddDayViewModel.HasUnsavedChanges))
        {
            raised++;
            seen.Add(viewModel.HasUnsavedChanges);
        }
    };

    viewModel.Date = day.ToDateTime(TimeOnly.MinValue);
    await viewModel.InitializeCommand.ExecuteAsync(null);

    Assert.True(raised > 0);
    Assert.DoesNotContain(true, seen);
    Assert.False(viewModel.HasUnsavedChanges);
}

/// <summary>
/// Иначе надпись молчит ровно до того момента, когда пользователь дёрнет другое упражнение и счёт
/// перезапустится заново.
/// </summary>
[Fact]
public async Task Надпись_Ввод_УведомляетСразу()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);
    await viewModel.SaveCommand.ExecuteAsync(null);

    Assert.False(viewModel.HasUnsavedChanges);

    var raised = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
        if (args.PropertyName == nameof(AddDayViewModel.HasUnsavedChanges))
        {
            raised++;
        }
    };

    viewModel.Current!.Sets[0].WeightText = "70";

    Assert.True(raised > 0);
    Assert.True(viewModel.HasUnsavedChanges);
}

/// <summary>
/// День считается по всему составу, а не по показанному упражнению.
/// </summary>
[Fact]
public async Task Надпись_ВводВНевидимоеУпражнение_Уведомляет()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);
    await viewModel.SaveCommand.ExecuteAsync(null);

    var raised = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
        if (args.PropertyName == nameof(AddDayViewModel.HasUnsavedChanges))
        {
            raised++;
        }
    };

    // Второе упражнение плана не показано: пользователь на нём не был.
    viewModel.Exercises[1].Sets[0].WeightText = "70";

    Assert.True(raised > 0);
    Assert.True(viewModel.HasUnsavedChanges);
}

[Fact]
public async Task Надпись_СменаУпражнения_СамаПоСебеНеМеняетСостояние()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);
    await viewModel.SaveCommand.ExecuteAsync(null);

    viewModel.NextCommand.Execute(null);
    Assert.False(viewModel.HasUnsavedChanges);

    viewModel.NextCommand.Execute(null);
    viewModel.PreviousCommand.Execute(null);
    Assert.False(viewModel.HasUnsavedChanges);
}

/// <summary>
/// Сравнивать форму с базой наивно нельзя: форма показывает весь план, а запись — только
/// упражнения с подходами.
/// </summary>
[Fact]
public async Task НадписьНеГоритНаОткрытомДне()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

    var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
    session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
    await database.SessionRepository.AddAsync(session);

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, day);

    Assert.False(viewModel.HasUnsavedChanges);

    Assert.Equal(2, viewModel.Exercises.Count);
}

[Fact]
public async Task Надпись_ПравкаВеса_ГоритИГаснетПослеСохранения()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    Assert.True(viewModel.HasUnsavedChanges);

    await viewModel.SaveCommand.ExecuteAsync(null);

    Assert.False(viewModel.HasUnsavedChanges);

    viewModel.Current!.Sets[0].WeightText = "70";

    Assert.True(viewModel.HasUnsavedChanges);
}

/// <summary>
/// Надпись отвечает на вопрос «отличается ли окно от базы», а не «трогал ли пользователь окно».
/// </summary>
[Fact]
public async Task Надпись_ВнеслиИСтёрлиОбратно_Гаснет()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);
    await viewModel.SaveCommand.ExecuteAsync(null);

    var row = viewModel.Current!;

    row.Sets[0].WeightText = "90";
    Assert.True(viewModel.HasUnsavedChanges);

    row.Sets[0].WeightText = "60";
    Assert.False(viewModel.HasUnsavedChanges);
}

[Fact]
public async Task Надпись_ПравкаПодходаИПлана_Горит()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var legs = await CreatePlanAsync(database, "Ноги", "Приседание");
    var upper = await CreatePlanAsync(database, "Верх", "Жим");

    var viewModel = await CreateReadyToSaveAsync(database, day, legs);
    await viewModel.SaveCommand.ExecuteAsync(null);

    viewModel.AddSetCommand.Execute(null);
    Assert.True(viewModel.HasUnsavedChanges);

    viewModel.RemoveSetCommand.Execute(null);
    Assert.False(viewModel.HasUnsavedChanges);

    viewModel.SelectedPlan = upper;
    Assert.True(viewModel.HasUnsavedChanges);
}

/// <summary>
/// Несохранённое прошлого дня теряется при смене даты — так же, как терялось до надписи.
/// </summary>
[Fact]
public async Task Надпись_СменаДаты_Гасится()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var first = new DateOnly(2026, 10, 5);
    var second = new DateOnly(2026, 10, 6);

    foreach (var day in new[] { first, second })
    {
        var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
        session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
        await database.SessionRepository.AddAsync(session);
    }

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, first);

    viewModel.Current!.Sets[0].WeightText = "90";
    Assert.True(viewModel.HasUnsavedChanges);

    await LoadDayAsync(viewModel, second);

    Assert.False(viewModel.HasUnsavedChanges);
}

/// <summary>
/// Разбор мусора возвращает «нечего сохранять», и сравнение с базой честно отвечает, что день
/// отличается; причину пользователь видит по погасшим кнопкам.
/// </summary>
[Fact]
public async Task Надпись_МусорВПоле_ГоритИНеПадает()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan, weight: "столько");

    Assert.True(viewModel.HasUnsavedChanges);
    Assert.False(viewModel.SaveCommand.CanExecute(null));
}

[Fact]
public void Differs_ОдинаковыеЗаписи_Ложь()
{
    var saved = Session("Ноги", ("Приседание", 8, 60m));
    var current = Session("Ноги", ("Приседание", 8, 60m));

    Assert.False(AddDayViewModel.Differs(saved, current));
}



[Fact]
public void Differs_ДругойПлан_Истина()
{
    var saved = Session("Ноги", ("Приседание", 8, 60m));
    var current = Session("Верх", ("Приседание", 8, 60m));

    Assert.True(AddDayViewModel.Differs(saved, current));
}

[Fact]
public void Differs_ПерестановкаПодходов_Истина()
{
    // Порядок подходов значим — он и есть строка в журнале: сравнение множеств пропустило бы
    // перестановку.
    var saved = Session("Ноги", ("Приседание", 8, 60m), ("Приседание", 6, 70m));
    var current = Session("Ноги", ("Приседание", 6, 70m), ("Приседание", 8, 60m));

    Assert.True(AddDayViewModel.Differs(saved, current));
}

[Fact]
public void Differs_ДобавленныйПодход_Истина()
{
    var saved = Session("Ноги", ("Приседание", 8, 60m));
    var current = Session("Ноги", ("Приседание", 8, 60m), ("Приседание", 6, 70m));

    Assert.True(AddDayViewModel.Differs(saved, current));
}

[Fact]
public void Differs_ЗаписиНет_ЛожьНаПустомДнеИИстинаСДанными()
{
    Assert.False(AddDayViewModel.Differs(null, Session("Ноги")));
    Assert.True(AddDayViewModel.Differs(null, Session("Ноги", ("Приседание", 8, 60m))));
}

[Fact]
public void Differs_ОдинаковоеЧислоРазнымФорматом_Ложь()
{
    // Поле разбирается, поэтому «60» и «60,0» — одно значение: «Не сохранено» из-за формата
    // записи было бы враньём.
    var saved = Session("Ноги", ("Приседание", 8, 60m));
    var current = Session("Ноги", ("Приседание", 8, 60.0m));

    Assert.Equal(saved.Exercises.Single().Sets.Single().Weight, current.Exercises.Single().Sets.Single().Weight);
    Assert.False(AddDayViewModel.Differs(saved, current));
}

/// <summary>
/// Вес меняется реже повторений, поэтому вес подставляется значением, а повторения — подсказкой:
/// иначе в журнал попало бы то, чего в этот раз не делали.
/// </summary>
[Fact]
public async Task ПодтягиваниеПрошлогоДня_ВесаЗначениямиПовторенияПодсказкой()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var day = new DateOnly(2026, 10, 10);
    var previousDay = new DateOnly(2026, 10, 8);

    var previous = new TrainingSession { Date = previousDay, PlanId = plan.Id, PlanName = plan.Name };
    var legs = previous.AddExercise(plan.Exercises[0]);

    legs.AddSet(10, 60m);
    legs.AddSet(8, 70m);

    await database.SessionRepository.AddAsync(previous);

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, day);

    viewModel.SelectedPlan = plan;

    await SettleAsync();

    var row = viewModel.Current!;

    Assert.Equal(2, row.Sets.Count);

    // Вес — значение: по нему сразу видно, что подходов было два, и сохранять можно не
    // набирая ничего.
    Assert.Equal(["60", "70"], row.Sets.Select(set => set.WeightText));

    Assert.Equal(["10", "8"], row.Sets.Select(set => set.RepetitionPlaceholder));
    Assert.Equal([string.Empty, string.Empty], row.Sets.Select(set => set.RepetitionText));

    Assert.True(row.Sets.All(set => set.HasRepetitionPlaceholder));

    // Дата источника показывается рядом с «Не сохранено» — по ней видно, откуда значения.
    Assert.Equal("(на основе 08 октября 2026)", viewModel.PulledFromDateText);
}

/// <summary>
/// Плейсхолдер пропадает, как только пользователь начал набирать: под введённым числом он
/// только мешал бы.
/// </summary>
[Fact]
public async Task Плейсхолдер_ГаснетПослеВвода()
{
    using var database = new TemporaryDatabase();
    var viewModel = await CreateReadyWithPreviousDayAsync(database);

    var set = viewModel.Current!.Sets[0];

    Assert.True(set.HasRepetitionPlaceholder);

    viewModel.Current!.Sets[0].RepetitionText = "12";

    Assert.False(set.HasRepetitionPlaceholder);

    viewModel.Current!.Sets[0].RepetitionText = string.Empty;

    Assert.True(set.HasRepetitionPlaceholder);
}

/// <summary>
/// Подсказка данными не считается: иначе пустой подход попал бы в запись, а надпись о
/// несохранённом загорелась бы от того, чего пользователь не вводил.
/// </summary>
[Fact]
    public void Плейсхолдер_ДаннымиНеСчитается()
{
    var set = new SetInputViewModel { RepetitionPlaceholder = "10" };

    Assert.False(set.HasData);
    Assert.True(set.HasRepetitionPlaceholder);
    Assert.True(set.TryParseRepetitions(out var repetitions));
    Assert.Equal(0, repetitions);
}

/// <summary>
/// Нулевые повторения означают, что поле тогда не заполняли: подсказка «0» читалась бы как «делай
/// ноль повторений». Нулевой вес по делу бывает, и подсказка про него есть.
/// </summary>
[Fact]
public async Task Подтягивание_НулевыеПовторения_ПодсказкиНет()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var previous = new TrainingSession
    {
        Date = new DateOnly(2026, 10, 8),
        PlanId = plan.Id,
        PlanName = plan.Name,
    };

    previous.AddExercise(plan.Exercises[0]).AddSet(0, 60m);

    await database.SessionRepository.AddAsync(previous);

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    viewModel.SelectedPlan = plan;

    await SettleAsync();

    var set = Assert.Single(viewModel.Current!.Sets);

    Assert.Equal("60", set.WeightText);
    Assert.False(set.HasRepetitionPlaceholder);
}

/// <summary>
/// Прошлый день ищется по идентификатору плана, а не по дате: запись за ту же дату с другим
/// планом подтягиванием не считается.
/// </summary>
[Theory]
[InlineData(false)]
[InlineData(true)]
public async Task Подтягивание_ПрошлогоДняНет_ФормаПуста(bool другойПланВПрошломДне)
{
    using var database = new TemporaryDatabase();
    var legs = await CreatePlanAsync(database, "Ноги", "Приседание");
    var upper = await CreatePlanAsync(database, "Верх", "Жим");

    if (другойПланВПрошломДне)
    {
        var previous = new TrainingSession
        {
            Date = new DateOnly(2026, 10, 8),
            PlanId = upper.Id,
            PlanName = upper.Name,
        };

        previous.AddExercise(upper.Exercises[0]).AddSet(10, 40m);

        await database.SessionRepository.AddAsync(previous);
    }

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    viewModel.SelectedPlan = legs;

    await SettleAsync();

    var set = Assert.Single(viewModel.Current!.Sets);

    Assert.Equal(string.Empty, set.WeightText);
    Assert.False(set.HasRepetitionPlaceholder);
    Assert.Equal(string.Empty, viewModel.PulledFromDateText);

    // Пустая форма дневника не должна и выглядеть несохранённой: нечего ведь сохранять.
    Assert.False(viewModel.HasUnsavedChanges);
}

[Fact]
public async Task Подтягивание_ПравкаДняНеЗатрагивается()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var day = new DateOnly(2026, 10, 10);

    // Прошлый день с тем же планом есть, но подтягиваться он не должен: день уже записан.
    var previous = new TrainingSession { Date = new DateOnly(2026, 10, 8), PlanId = plan.Id, PlanName = plan.Name };
    previous.AddExercise(plan.Exercises[0]).AddSet(10, 40m);

    await database.SessionRepository.AddAsync(previous);

    var stored = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
    stored.AddExercise(plan.Exercises[0]).AddSet(6, 90m);

    await database.SessionRepository.AddAsync(stored);

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, day);

    await SettleAsync();

    var set = Assert.Single(viewModel.Current!.Sets);

    Assert.Equal("90", set.WeightText);
    Assert.Equal("6", set.RepetitionText);
    Assert.False(set.HasRepetitionPlaceholder);
    Assert.Equal(string.Empty, viewModel.PulledFromDateText);
}

/// <summary>
/// Плану без этого упражнения в прошлый раз соответствует пустое поле: кнопки «+» и «−» работают
/// с ним так же, иначе подход негде было бы набрать.
/// </summary>
[Fact]
public async Task Подтягивание_УпражненияНетВПрошлыйРаз_ПустоеПоле()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

    var previous = new TrainingSession
    {
        Date = new DateOnly(2026, 10, 8),
        PlanId = plan.Id,
        PlanName = plan.Name,
    };

    previous.AddExercise(plan.Exercises[0]).AddSet(10, 60m);

    await database.SessionRepository.AddAsync(previous);

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    viewModel.SelectedPlan = plan;

    await SettleAsync();

    var bench = viewModel.Exercises.Single(row => row.Name == "Жим");
    var set = Assert.Single(bench.Sets);

    Assert.Equal(string.Empty, set.WeightText);
    Assert.False(set.HasRepetitionPlaceholder);
}

/// <summary>
/// Ответ придёт в уже заполненные поля и затрёт набранное. Проверяется на настоящей задержке:
/// на быстром хранилище состояния не поймать.
/// </summary>
[Fact]
public async Task Подтягивание_ВводЗаблокированПокаОтветВПути()
{
    using var database = new TemporaryDatabase();
    await CreatePlanAsync(database, "Ноги", "Приседание");

    var fake = new FakeSessionRepository(database.SessionRepository);
    var viewModel = await CreateInitializedAsync(database, fake);

    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    Assert.False(viewModel.IsLoading);

    viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

    await WaitUntilAsync(() => fake.Calls > 0);

    Assert.True(viewModel.IsLoading);
    Assert.False(viewModel.CanFill);

    fake.Release();

    await WaitUntilAsync(() => !viewModel.IsLoading);

    Assert.True(viewModel.CanFill);
}

/// <summary>
/// Индикатор — счётчик, а не флаг: пока первое подтягивание не пришло, может начаться второе, и
/// флаг погасил бы ввод при живом втором.
/// </summary>
[Fact]
public async Task Подтягивание_Пересечение_ИндикаторНеГаснетПослеПервого()
{
    using var database = new TemporaryDatabase();
    await CreatePlanAsync(database, "Ноги", "Приседание");
    await CreatePlanAsync(database, "Верх", "Жим");

    // Задержано только первое чтение: удержать оба одинаково нельзя, иначе порядок их
    // завершения не задан и проверять нечего.
    var fake = new FakeSessionRepository(database.SessionRepository) { DelayOnlyCall = 1 };
    var viewModel = await CreateInitializedAsync(database, fake);

    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Ноги");

    await WaitUntilAsync(() => fake.Calls >= 1);

    viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Верх");

    await WaitUntilAsync(() => fake.Calls >= 2);

    await SettleAsync();

    Assert.True(viewModel.IsLoading);
    Assert.False(viewModel.CanFill);

    fake.Release();

    await WaitUntilAsync(() => !viewModel.IsLoading);

    Assert.True(viewModel.CanFill);
}

/// <summary>
/// Ответ на уже сменённый план выбрасывается целиком: подставить веса чужого плана хуже,
/// чем не подставить ничего.
/// </summary>
[Fact]
public async Task Подтягивание_ОтветНаСменённыйПланВыброшен()
{
    using var database = new TemporaryDatabase();
    var legs = await CreatePlanAsync(database, "Ноги", "Приседание");
    var upper = await CreatePlanAsync(database, "Верх", "Жим");

    var previous = new TrainingSession { Date = new DateOnly(2026, 10, 8), PlanId = legs.Id, PlanName = legs.Name };
    previous.AddExercise(legs.Exercises[0]).AddSet(10, 60m);

    await database.SessionRepository.AddAsync(previous);

    var fake = new FakeSessionRepository(database.SessionRepository) { DelayOnlyCall = 1 };
    var viewModel = await CreateInitializedAsync(database, fake);

    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    viewModel.SelectedPlan = legs;

    await WaitUntilAsync(() => fake.Calls >= 1);

    viewModel.SelectedPlan = upper;

    fake.Release();

    await SettleAsync();

    Assert.Equal("Жим", Assert.Single(viewModel.Exercises).Name);
    Assert.Equal(string.Empty, Assert.Single(viewModel.Current!.Sets).WeightText);
    Assert.Equal(string.Empty, viewModel.PulledFromDateText);
}

/// <summary>
/// Пока идёт чтение, форма пуста и сравнивать не с чем: надпись не должна ни вспыхивать, ни
/// гореть.
/// </summary>
[Fact]
public async Task Подтягивание_НадписьНеГоритПокаИдётЧтение()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var previous = new TrainingSession { Date = new DateOnly(2026, 10, 8), PlanId = plan.Id, PlanName = plan.Name };
    previous.AddExercise(plan.Exercises[0]).AddSet(10, 60m);

    await database.SessionRepository.AddAsync(previous);

    var fake = new FakeSessionRepository(database.SessionRepository);
    var viewModel = await CreateInitializedAsync(database, fake);

    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    var seen = new List<bool>();

    viewModel.PropertyChanged += (_, args) =>
    {
        if (args.PropertyName == nameof(AddDayViewModel.HasUnsavedChanges))
        {
            seen.Add(viewModel.HasUnsavedChanges);
        }
    };

    viewModel.SelectedPlan = plan;

    await WaitUntilAsync(() => fake.Calls > 0);

    Assert.DoesNotContain(true, seen);

    fake.Release();

    await SettleAsync();

    // А после наполнения — загорается: подтянутые веса это данные, которых нет в базе.
    Assert.Contains(true, seen);
}

/// <summary>
/// Смена даты перестраивает окно мимо сеттера плана, и подтягивание при записи за новую дату
/// выходит сразу: без уведомления в перестройке дата прежнего источника осталась бы в скобках.
/// </summary>
[Fact]
public async Task Подтягивание_СменаДатыУбираетДатуИсточника()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var previous = new TrainingSession { Date = new DateOnly(2026, 10, 8), PlanId = plan.Id, PlanName = plan.Name };
    previous.AddExercise(plan.Exercises[0]).AddSet(10, 60m);

    await database.SessionRepository.AddAsync(previous);

    var viewModel = await CreateInitializedAsync(database);

    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));
    viewModel.SelectedPlan = plan;

    await SettleAsync();

    Assert.Equal("(на основе 08 октября 2026)", viewModel.PulledFromDateText);

    // Запись за новую дату есть, план тот же: сеттер SelectedPlan не срабатывает, и
    // подтягивание выходит сразу.
    var stored = new TrainingSession { Date = new DateOnly(2026, 10, 12), PlanId = plan.Id, PlanName = plan.Name };
    stored.AddExercise(plan.Exercises[0]).AddSet(6, 90m);

    await database.SessionRepository.AddAsync(stored);

    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 12));

    Assert.Equal("Правка дня", viewModel.Title);
    Assert.Equal("90", Assert.Single(viewModel.Current!.Sets).WeightText);
    Assert.Equal(string.Empty, viewModel.PulledFromDateText);
}

/// <summary>
/// Колонка Notes принадлежит упражнению, а проверка упражнений записи её не покрывает: текст
/// потерялся бы молча.
/// </summary>
[Fact]
public async Task Примечание_СохраняетсяИЧитаетсяОбратно()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    viewModel.Current!.NoteText = "колени побаливали";

    await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

    Assert.True(viewModel.Accepted);

    var stored = await database.SessionRepository.GetByDateAsync(day);

    Assert.Equal("колени побаливали", Assert.Single(stored!.Exercises).Notes);
}

/// <summary>
/// Молча выбросить набранный текст при сохранении хуже, чем показать в журнале лишнее упражнение.
/// </summary>
[Fact]
public async Task ПримечаниеБезПодходов_УпражнениеПопадаетВЗапись()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, day);

    viewModel.SelectedPlan = plan;

    Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));

    viewModel.Current!.NoteText = "не делал, спина";

    Assert.True(viewModel.SaveAndCloseCommand.CanExecute(null));

    await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

    var stored = await database.SessionRepository.GetByDateAsync(day);
    var entry = Assert.Single(stored!.Exercises);

    Assert.Equal("не делал, спина", entry.Notes);
    Assert.Empty(entry.Sets);
}

/// <summary>
/// Иначе день из одних пробелов сохранялся бы как выполненный.
/// </summary>
[Fact]
public async Task ПримечаниеИзПробелов_НеСчитаетсяЗаполненным()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, day);

    viewModel.SelectedPlan = plan;
    viewModel.Current!.NoteText = "   ";

    Assert.False(viewModel.Current.HasNote);
    Assert.False(viewModel.Current.HasData);
    Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));
}

/// <summary>
/// Как и повторения, примечание подтягивается подсказкой: значение прошлого раза записало бы в
/// журнал то, чего в этот раз не писали.
/// </summary>
[Fact]
public async Task Подтягивание_ПримечаниеПрошлогоДня_Подсказкой()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var previous = new TrainingSession
    {
        Date = new DateOnly(2026, 10, 8),
        PlanId = plan.Id,
        PlanName = plan.Name,
    };

    previous.AddExercise(plan.Exercises[0], "болело левое плечо").AddSet(10, 60m);

    await database.SessionRepository.AddAsync(previous);

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    viewModel.SelectedPlan = plan;

    await SettleAsync();

    var row = viewModel.Current!;

    Assert.Equal(string.Empty, row.NoteText);
    Assert.Equal("болело левое плечо", row.NotePlaceholder);
    Assert.True(row.HasNotePlaceholder);

    Assert.False(row.HasNote);

    await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

    var stored = await database.SessionRepository.GetByDateAsync(new DateOnly(2026, 10, 10));

    // Подсказка в запись не уходит: иначе упражнение сохранилось бы с чужим примечанием.
    Assert.Null(Assert.Single(stored!.Exercises).Notes);
}

/// <summary>
/// Подставить подтянутую заметку значением нельзя: день получился бы выполненным из ничего, а
/// надпись о несохранённом загорелась бы от того, чего пользователь не вводил.
/// </summary>
[Fact]
public async Task Подтягивание_ПримечаниеБезПодходов_ФормуНеНаполняет()
{
    using var database = new TemporaryDatabase();
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var previous = new TrainingSession
    {
        Date = new DateOnly(2026, 10, 8),
        PlanId = plan.Id,
        PlanName = plan.Name,
    };

    previous.AddExercise(plan.Exercises[0], "болело левое плечо");

    await database.SessionRepository.AddAsync(previous);

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, new DateOnly(2026, 10, 10));

    viewModel.SelectedPlan = plan;

    await SettleAsync();

    var row = viewModel.Current!;

    Assert.Equal("болело левое плечо", row.NotePlaceholder);
    Assert.False(row.HasData);
    Assert.False(viewModel.HasUnsavedChanges);
    Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));
}

/// <summary>
/// На правке примечание уже записано за этот день: пользователь правит его, а не сверяется с
/// прошлым разом.
/// </summary>
[Fact]
public async Task ПравкаДня_ПримечаниеГрузитсяЗначением()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
    session.AddExercise(plan.Exercises[0], "колени побаливали").AddSet(8, 60m);

    await database.SessionRepository.AddAsync(session);

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, day);

    var row = viewModel.Current!;

    Assert.Equal("колени побаливали", row.NoteText);
    Assert.Equal(string.Empty, row.NotePlaceholder);
    Assert.True(row.HasNote);
    Assert.False(row.HasNotePlaceholder);

    // Загруженное значение — то, что лежит в базе, поэтому надпись не горит.
    Assert.False(viewModel.HasUnsavedChanges);
}

[Fact]
public async Task Надпись_ПравкаПримечания_ГоритИГаснет()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    viewModel.Current!.NoteText = "колени побаливали";
    Assert.True(viewModel.HasUnsavedChanges);

    await viewModel.SaveCommand.ExecuteAsync(null);

    Assert.False(viewModel.HasUnsavedChanges);

    viewModel.Current!.NoteText = "колени побаливали сильнее";
    Assert.True(viewModel.HasUnsavedChanges);

    viewModel.Current!.NoteText = "колени побаливали";
    Assert.False(viewModel.HasUnsavedChanges);
}

/// <summary>
/// Примечание лежит на строке упражнения, а не в подходе: без подписки на строку кнопка сохранить
/// осталась бы активной с негодными данными (ловушка 24).
/// </summary>
[Fact]
public async Task ПравкаПримечания_ОбновляетАктивностьСохранения()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateInitializedAsync(database);
    await LoadDayAsync(viewModel, day);

    viewModel.SelectedPlan = plan;

    Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));

    var raised = 0;
    viewModel.SaveAndCloseCommand.CanExecuteChanged += (_, _) => raised++;

    viewModel.Current!.NoteText = "не делал, спина";

    Assert.True(raised > 0);
    Assert.True(viewModel.SaveAndCloseCommand.CanExecute(null));
}

[Fact]
public void ПлейсхолдерПримечания_ГаснетПослеВвода()
{
    var row = PlanExerciseViewModel.Create(new Exercise { Name = "Приседание" }, 1);

    row.NotePlaceholder = "болело левое плечо";

    Assert.True(row.HasNotePlaceholder);

    row.NoteText = "не болело";

    Assert.False(row.HasNotePlaceholder);
    Assert.True(row.HasNote);

    row.NoteText = string.Empty;

    Assert.True(row.HasNotePlaceholder);
    Assert.False(row.HasNote);
}

/// <summary>
/// Иначе надпись горела бы на поле, которое сохранилось бы ровно тем же, что лежит в базе.
/// </summary>
[Fact]
public void Differs_Примечание_ЛовитРазницуИПрощаетПробелы()
{
    var saved = SessionWithNote("Ноги", "колени побаливали");
    var current = SessionWithNote("Ноги", "колени побаливали сильнее");

    Assert.True(AddDayViewModel.Differs(saved, current));
    Assert.False(AddDayViewModel.Differs(saved, SessionWithNote("Ноги", "колени побаливали")));
    Assert.False(AddDayViewModel.Differs(saved, SessionWithNote("Ноги", " колени побаливали ")));
}

/// <summary>
/// Иначе примечание от упражнения чужого плана уехало бы в запись нового.
/// </summary>
[Fact]
public async Task СменаПлана_ЧиститПримечание()
{
    using var database = new TemporaryDatabase();
    await CreatePlanAsync(database, "Ноги", "Приседание");
    await CreatePlanAsync(database, "Верх", "Жим лёжа");

    var viewModel = await CreateInitializedAsync(database);

    viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Ноги");
    viewModel.Current!.NoteText = "колени побаливали";

    viewModel.SelectedPlan = viewModel.Plans.Single(plan => plan.Name == "Верх");

    Assert.Equal(string.Empty, Assert.Single(viewModel.Exercises).NoteText);
    Assert.False(Assert.Single(viewModel.Exercises).HasNote);
}

/// <summary>
/// Взаимная исключительность проверяется на модели: в разметке столкнуться индикаторы могут
/// только из-за ломаной привязки (ловушка 43).
/// </summary>
[Fact]
public void Индикаторы_НеЗажигаютсяОдновременно()
{
    var row = PlanExerciseViewModel.Create(new Exercise { Name = "Приседание" }, 1);

    Assert.False(row.HasNote);
    Assert.False(row.HasNotePlaceholder);

    row.NotePlaceholder = "болело левое плечо";

    Assert.False(row.HasNote);
    Assert.True(row.HasNotePlaceholder);

    row.NoteText = "не болело";

    Assert.True(row.HasNote);
    Assert.False(row.HasNotePlaceholder);

    // Пробелы примечанием не считаются, но поле ими заполнено — подсказка гаснет.
    row.NoteText = "   ";

    Assert.False(row.HasNote);
    Assert.False(row.HasNotePlaceholder);
}

/// <summary>
/// Показывать поле там, где некому его заполнить, незачем: показ привязан к HasCurrent, а не к
/// Current — иначе ломаная привязка оставила бы элемент видимым (ловушка 43).
/// </summary>
[Fact]
public async Task БезПлана_УпражненияНет_ИПримечаниеСкрыто()
{
    using var database = new TemporaryDatabase();
    await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateInitializedAsync(database);

    Assert.Null(viewModel.Current);
    Assert.False(viewModel.HasCurrent);

    viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

    Assert.NotNull(viewModel.Current);
    Assert.True(viewModel.HasCurrent);

    await SettleAsync();

    viewModel.SelectedPlan = null;

    Assert.False(viewModel.HasCurrent);
}

/// <summary>
/// Признак уведомляется в общей точке пересчёта: на сеттере индекса уведомления нет — при смене
/// плана индекс и так ноль (ловушка 25).
/// </summary>
[Fact]
public async Task СменаПлана_СообщаетОНаличииУпражнения()
{
    using var database = new TemporaryDatabase();
    await CreatePlanAsync(database, "Ноги", "Приседание", "Жим");

    var viewModel = await CreateInitializedAsync(database);
    var notifications = TrackNotifications(viewModel);

    viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

    Assert.Contains(nameof(AddDayViewModel.HasCurrent), notifications);
    Assert.True(viewModel.HasCurrent);
}

private static TrainingSession SessionWithNote(string planName, string? note)
{
    var session = new TrainingSession { PlanName = planName };
    var entry = session.AddExercise("Приседание");

    entry.Notes = note;
    entry.AddSet(8, 60m);

    return session;
}

private static TrainingSession Session(string planName, params (string Exercise, int Repetitions, decimal Weight)[] sets)
{
    var session = new TrainingSession { PlanName = planName };

    foreach (var (exercise, repetitions, weight) in sets)
    {
        session.AddExercise(exercise).AddSet(repetitions, weight);
    }

    return session;
}

private static async Task<TrainingPlan> CreatePlanAsync(
        TemporaryDatabase database,
        string name,
        IEnumerable<Exercise> exercises)
    {
        var plan = new TrainingPlan { Name = name };

        foreach (var exercise in exercises)
        {
            plan.AddExercise(exercise);
        }

        await database.PlanRepository.AddAsync(plan);

        return plan;
    }

    private static async Task<Exercise[]> CreateExercisesAsync(TemporaryDatabase database, string[] names)
    {
        var exercises = new Exercise[names.Length];

        for (var index = 0; index < names.Length; index++)
        {
            var exercise = new Exercise { Name = names[index] };

            // Молча недобавленное упражнение уехало бы в план без идентификатора и выпало бы при
            // сохранении, поэтому исход проверяется.
            Assert.Equal(AddExerciseOutcome.Added, await database.Repository.AddAsync(exercise));

            exercises[index] = exercise;
        }

        return exercises;
    }
}