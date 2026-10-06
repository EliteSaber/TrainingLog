using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Окно добавления дня. Проверяется то, из-за чего день потом не собрать: нумерация кнопок
/// упражнений и подсказки на них, правила подходов, сохранение против правки и то, что окно
/// подтягивает уже записанную дату вместо добавления второй записи на ту же дату.
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

    /// <summary>
    /// «‹» и «›» обязаны быть готовы к работе сразу после открытия окна, без нажатия на кнопку
    /// с номером упражнения.
    /// </summary>
    /// <remarks>
    /// Проверяется через <c>CanExecuteChanged</c>, а не через <c>CanExecute</c>: WPF спрашивает
    /// предикат один раз и держит состояние до события, поэтому сам предикат тут всегда прав.
    /// Именно поэтому нужен счётчик — так же, как в проверке подписки на ввод в подход.
    /// </remarks>
    [Fact]
    public async Task ОткрытиеОкна_НавигацияГотоваБезНажатияНаНомер()
    {
        using var database = new TemporaryDatabase();
        var day = new DateOnly(2026, 10, 5);

        // За день запись уже есть: план подставляется сам, и именно этот путь ломался — индекс и
        // так ноль, поэтому уведомления от сеттера не было. На пустом дне план не выбирается
        // вовсе, и навигации быть не может.
        var plan = await CreatePlanAsync(database, "Ноги", "Приседание", "Жим", "Тяга");

        var session = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
        session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
        await database.SessionRepository.AddAsync(session);

        // Подписка ставится до InitializeCommand — до того, как окно открывается, ровно как
        // в приложении: план и упражнения появляются уже во время загрузки.
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

        // И сразу работают: «›» переводит на второе упражнение без всякой предварительной
        // расшаривки состояния кнопкой с номером.
        viewModel.NextCommand.Execute(null);

        Assert.Same(viewModel.Exercises[1], viewModel.Current);
    }

    /// <summary>
    /// Навигация должна пересчитываться и при пересборке состава: счётчик проверяет, что
    /// <c>Refresh</c> действительно её трогает, а не полагается на сеттер индекса.
    /// </summary>
    [Fact]
    public async Task СменаПлана_ПересчитываетНавигацию()
    {
        using var database = new TemporaryDatabase();

        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        // Упражнения у планов разные: наименования в справочнике уникальны, второе «Приседание»
        // просто не добавилось бы.
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
    /// У дат с одним планом сеттер <c>SelectedPlan</c> не срабатывает, и без явного сброса
    /// активным оставалось упражнение, где пользователь остановился.
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
    /// Смена плана переписывает состав дня: иначе подходы старого плана уехали бы в новый.
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
    /// Новый подход наследует вес предыдущего: подходы одного упражнения отличаются
    /// повторениями гораздо чаще, чем весом.
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

        // Поля убираются до конца: упражнение остаётся в плане, но без введённых данных.
        // Набрать подход заново можно кнопкой «+».
        viewModel.RemoveSetCommand.Execute(null);

        Assert.Empty(viewModel.Current.Sets);
        Assert.False(viewModel.RemoveSetCommand.CanExecute(null));
        Assert.False(viewModel.SaveAndCloseCommand.CanExecute(null));
    }

    /// <summary>
    /// Подписка на ввод должна быть и на только что добавленном подходе: иначе «Сохранить»
    /// осталась бы активной с негодными данными, и нажатие молча ничего бы не сделало.
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

        // WPF держит активность кнопки до события CanExecuteChanged, а сам предикат не зовёт:
        // без подписки на ввод в новый подход кнопка осталась бы активной с негодными
        // данными, и нажатие молча ничего бы не сделало.
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

    /// <summary>
/// «Сохранить» пишет день и оставляет окно открытым: <c>Accepted</c> не выставляется, иначе
/// окно закрылось бы вместе с кнопкой.
/// </summary>
[Fact]
public async Task SaveCommand_СохраняетНоНеЗакрываетОкно()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    await viewModel.SaveCommand.ExecuteAsync(null);

    Assert.True(viewModel.Saved);

    // Именно это и не закрывает окно: Accepted остаётся false, и DialogResult не выставится.
    Assert.False(viewModel.Accepted);

    var stored = await database.SessionRepository.GetByDateAsync(day);

    Assert.NotNull(stored);
    Assert.Equal(60m, Assert.Single(Assert.Single(stored.Exercises).Sets).Weight);
}

/// <summary>
/// После сохранения без закрытия запись уже в базе, и следующее сохранение обязано пойти как
/// правка. Без присваивания <c>_existing</c> второе нажатие ушло бы в <c>AddAsync</c> и
/// упёрлось бы в занятую дату.
/// </summary>
[Fact]
public async Task SaveCommand_Дважды_ОднаЗаписьСоВторымПланом()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    await viewModel.SaveCommand.ExecuteAsync(null);

    // Заголовок обязан перейти в «Правка дня»: в базе это теперь правка, а не добавление.
    Assert.Equal("Правка дня", viewModel.Title);

    var sessionId = (await database.SessionRepository.GetByDateAsync(day))!.Id;

    // Правка идёт через модель, а не через подмену базы: присваивание всей собранной записи
    // занесло бы в модель упражнения с идентификаторами, и второй SaveAndClose прошёл бы
    // даже при нулевом Id. Нужна именно та схема, что в приложении: после AddAsync модель
    // берёт из записи только идентификатор записи.
    viewModel.Current!.Sets[0].WeightText = "70";

    await viewModel.SaveCommand.ExecuteAsync(null);

    // Пустой Status — тоже часть проверки: второе сохранение прошло как правка, а не как
    // отказ по занятой дате. Иначе день в базе обновился бы, но пользователь увидел бы
    // «за эту дату уже есть запись» и решил, что правка не сработала.
    Assert.Equal(string.Empty, viewModel.Status);

    var stored = await database.SessionRepository.GetByDateAsync(day);

    Assert.Equal(sessionId, stored!.Id);
    Assert.Equal(70m, Assert.Single(Assert.Single(stored.Exercises).Sets).Weight);
    Assert.Single(await database.SessionRepository.GetAsync());
}

/// <summary>
/// «Сохранить и закрыть» сохраняет и выставляет <c>Accepted</c>, по которому окно ставит
/// <c>DialogResult</c>.
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
/// То же, что и предыдущий, но через «Сохранить» без закрытия: идентификатор записи должен
/// доехать до модели. При нулевом второе сохранение ушло бы в <c>AddAsync</c> и упало бы на
/// внешнем ключе — либо, если за датой уже есть запись, вернуло «дата занята».
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
/// Отказ сохранения не должен считаться сохранением: иначе главное окно перечитает журнал
/// после окна, в котором ничего не записалось.
/// </summary>
[Fact]
public async Task SaveCommand_Отказ_НеСчитаетсяСохранением()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);
    var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

    var viewModel = await CreateReadyToSaveAsync(database, day, plan);

    // Запись занимает дату, пока окно считает, что её нет: так проверяется отказ по существу,
    // а не по неактивной кнопке.
    var taken = new TrainingSession { Date = day, PlanName = "Занято" };
    await database.SessionRepository.AddAsync(taken);

    await viewModel.SaveCommand.ExecuteAsync(null);

    Assert.False(viewModel.Saved);
    Assert.False(viewModel.Accepted);
    Assert.Equal("Занято", (await database.SessionRepository.GetByDateAsync(day))!.PlanName);
}

/// <summary>
/// Мусор в поле гасит обе кнопки одинаково: у «Сохранить» это тем важнее, что она же висит на
/// Ctrl+S, и горячая клавиша при серой кнопке звать сохранение не должна.
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

    // Смена текста обязана поднять CanExecuteChanged у обеих команд: иначе кнопка осталась бы
    // активной с негодными данными, а нажатие молча ничего бы не сделало.
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
    /// На дату запись одна, поэтому окно подтягивает её и правит на месте. Проверяется и
    /// содержимое полей, и то, что вторая запись за ту же дату не появилась.
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
        var plan = await CreatePlanAsync(database, "Ноги", "Приседание");

        var first = new DateOnly(2026, 10, 5);
        var session = new TrainingSession { Date = first, PlanId = plan.Id, PlanName = plan.Name };
        session.AddExercise(plan.Exercises[0]).AddSet(8, 60m);
        await database.SessionRepository.AddAsync(session);

        var viewModel = await CreateInitializedAsync(database);
        await LoadDayAsync(viewModel, first);

        Assert.Equal("60", Assert.Single(viewModel.Exercises[0].Sets).WeightText);

        await LoadDayAsync(viewModel, new DateOnly(2026, 10, 6));

        // Плана у новой даты нет — упражнений нет, и подходы прежней записи в полях не остались.
        Assert.Empty(viewModel.Exercises);

        viewModel.SelectedPlan = Assert.Single(viewModel.Plans);

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

    /// <summary>
    /// Привязки полей подходов и названия упражнения идут через <c>Current</c>, и WPF
    /// подписывается на уведомление об этом свойстве: путь <c>Exercises</c> в привязке не
    /// участвует, поэтому наполнение коллекции упражнений привязку не будит. Без уведомления
    /// область подходов оставалась пустой — кнопки упражнений появлялись, а значения нет, пока
    /// пользователь не переключит упражнение кнопкой.
    /// </summary>
    /// <remarks>
    /// Проверяется именно список уведомлений, а не состояние команды: WPF держит активность
    /// кнопки до <c>CanExecuteChanged</c> и сам предикат не зовёт, так что через
    /// <c>CanExecute</c> этот баг не виден вовсе.
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
    /// Тот же случай при открытии окна на дату с записью: активное упражнение и до загрузки
    /// первое, поэтому индекс не меняется и уведомление по нему не поднимается.
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

        // Повторная загрузка того же дня намеренно идёт через установку даты заново: смена
        // даты — единственный путь, которым дата меняется в реальном окне, и он обязан
        // перечитывать запись.
        await LoadDayAsync(viewModel, day);

        Assert.Contains(nameof(AddDayViewModel.Current), notifications);
        Assert.Equal("60", Assert.Single(viewModel.Current!.Sets).WeightText);
    }

    /// <summary>
    /// Снятие плана обнуляет упражнения, и без уведомления подходы прежнего плана остались бы
    /// висеть в окне, к которому их уже не к чему привязать.
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

    /// <summary>
    /// <c>DatePicker</c> присоединяется со своим значением по умолчанию и двусторонней
    /// привязкой пишет его в источник, поэтому дата меняется при открытии окна сама по себе.
    /// Запись за тот же день перечитывать незачем.
    /// </summary>
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
        // перезапуск команды виден по нему. Считать сами запросы пришлось бы отдельным
        // подменным репозиторием, а интересует как раз факт повторного чтения.
        Assert.DoesNotContain(nameof(AddDayViewModel.Title), notifications);
    }

    /// <summary>
/// Смена даты подтягивает запись именно этой даты. Это же проверяет, что после перехода
/// в окне не осталось подходов прежнего дня.
/// </summary>
/// <summary>
/// Запись за дату одна, и она принадлежит одному плану. Возврат на этот план после
/// переключения на другой должен показывать её подходы: иначе наполнение зовётся только из
/// смены даты, а дата та же.
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
/// Гейт по плану: упражнение, входящее и в сохранённый план, и в другой, не должно показывать
/// подходы записи в полях чужого плана.
/// </summary>
[Fact]
public async Task СменаПланаНаПланСОбщимУпражнением_НеПоказываетПодходыЧужогоПлана()
{
    using var database = new TemporaryDatabase();
    var day = new DateOnly(2026, 10, 5);

    // Упражнение одно на оба плана: наименования в справочнике уникальны, поэтому второе
    // «Приседание» просто не добавилось бы и проверка ничего не говорила бы.
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

    // Общее упражнение — то самое, на котором гейт и проверяется.
    Assert.Equal(
        [string.Empty],
        viewModel.Exercises.SelectMany(row => row.Sets).Select(set => set.WeightText));
}

/// <summary>
/// Возврат на сохранённый план и сохранение не должны ни стирать подходы, ни плодить вторую
/// запись за дату.
/// </summary>
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

private static AddDayViewModel CreateViewModel(TemporaryDatabase database) =>
        new(database.SessionRepository, database.PlanRepository);

    private static async Task<AddDayViewModel> CreateInitializedAsync(TemporaryDatabase database)
    {
        var viewModel = CreateViewModel(database);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        return viewModel;
    }

    private static List<string?> TrackNotifications(AddDayViewModel viewModel)
    {
        var notifications = new List<string?>();

        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        return notifications;
    }

    /// <summary>
    /// Загружает в окно день.
    /// </summary>
    /// <remarks>
    /// Дата задаётся явно и день перечитывается командой: по умолчанию в окне сегодняшняя, и
    /// тест молча зависел бы от того, что запись записана именно на сегодня. Такой тест и был —
    /// он рассыпался в полночь, когда «сегодня» перестало совпадать с датой в фикстуре.
    /// </remarks>
    private static async Task LoadDayAsync(AddDayViewModel viewModel, DateOnly day)
    {
        viewModel.Date = day.ToDateTime(TimeOnly.MinValue);
        await viewModel.LoadDateCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Готовит окно на дату с планом и одним введённым подходом: состояние, из которого
    /// сохранять имеет смысл.
    /// </summary>
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

    /// <summary>
/// Надпись должна честно отвечать сразу при открытии окна, без единого щелчка по упражнениям.
/// </summary>
/// <remarks>
/// Считаются уведомления самой модели, а не <c>CanExecute</c>: предикат проверять бессмысленно,
/// WPF зовёт его сам и держит результат до события. И проверяется именно порядок — снимок
/// эталонного состояния обязан стоять до первого уведомления, иначе надпись считается против
/// предыдущего снимка и горит на уже сохранённом дне.
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

    // Ловится не итоговое значение свойства, а то, которое ушло в привязку в момент
    // уведомления: перечитывание свойства после загрузки дало бы верный результат даже тогда,
    // когда надпись в окне осталась загоревшейся. В WPF привязка берёт значение один раз и
    // держит его до следующего уведомления — ровно эта ошибка и ловится здесь.
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

    // Ни одного «есть изменения» за всю загрузку быть не должно. Проверяется не итоговое
    // значение, а все переданные привязке: снимок эталонного состояния обязан стоять до
    // первого уведомления, иначе надпись вспыхивает по ходу загрузки против предыдущего
    // снимка — на первом открытии против null, то есть против «записи нет».
    Assert.True(raised > 0);
    Assert.DoesNotContain(true, seen);
    Assert.False(viewModel.HasUnsavedChanges);
}

/// <summary>
/// Надпись обязана реагировать на ввод немедленно: иначе она молчит ровно до того момента,
/// когда пользователь дёрнет другое упражнение и счёт перезапустится заново.
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
/// День считается по всему составу, а не по показанному упражнению: ввод в невидимое
/// упражнение тоже изменение.
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

/// <summary>
/// Смена упражнения — это переход взглядом, а не правка: сама по себе она надписи не касается.
/// </summary>
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
/// Открытый день с данными — это уже сохранённое состояние, а не правка. Надпись на такой день
/// гореть не должна: сравнивать форму с базой наивно нельзя, потому что форма показывает весь
/// план, а запись — только упражнения с подходами.
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

    // Второе упражнение плана подходов не имеет и в записи не попадает — именно этот случай и
    // выдавал бы «изменено» при сравнении формы с базой.
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
/// Надпись должна гаснуть, когда внесли значение и стерели его обратно: она отвечает на вопрос
/// «отличается ли то, что в окне, от того, что в базе», а не «трогал ли пользователь окно».
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
/// Смена даты гасит надпись: показываются данные того дня, и они считаются сохранёнными.
/// Несохранённое прошлого дня теряется — так же, как оно терялось без надписи.
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
/// Мусор в поле не должен ронять надпись: разбор вернёт «нечего сохранять», и сравнение с базой
/// честно ответит, что день отличается. Кнопки при этом гаснут, и пользователь видит причину.
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
    // Порядок подходов значим: он и есть строка в журнале. Сравнение множеств пропустило бы
    // перестановку, как это уже сделано с составом плана.
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
    // Поле разбирается, и «60» и «60,0» дают одно и то же значение: показывать «Не сохранено»
    // из-за формата записи было бы враньём.
    var saved = Session("Ноги", ("Приседание", 8, 60m));
    var current = Session("Ноги", ("Приседание", 8, 60.0m));

    Assert.Equal(saved.Exercises.Single().Sets.Single().Weight, current.Exercises.Single().Sets.Single().Weight);
    Assert.False(AddDayViewModel.Differs(saved, current));
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

            // Исход проверяется, а не игнорируется: молча недобавленное упражнение уехало бы
            // в план без идентификатора, выпало бы при сохранении, и проверка прошла бы вхолостую.
            Assert.Equal(AddExerciseOutcome.Added, await database.Repository.AddAsync(exercise));

            exercises[index] = exercise;
        }

        return exercises;
    }
}