using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Окно плана опирается на уведомление об <see cref="EditPlanViewModel.Accepted"/>, чтобы
/// выставить DialogResult. Без этого уведомления план сохранится, а вызывающий об этом
/// не узнаёт, поэтому механизм проверяется здесь. Заодно проверяется состав: он задаётся
/// отметками строк, а не всем справочником.
/// </summary>
public sealed class EditPlanViewModelTests
{
    [Fact]
    public async Task AcceptAsync_НовыйПлан_AcceptedСталTrueИПришлоУведомление()
    {
        using var database = new TemporaryDatabase();
        var viewModel = CreateViewModel(database, new TrainingPlan(), isNew: true);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        var notifications = TrackNotifications(viewModel);

        viewModel.Name = "Ноги";
        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Contains(nameof(EditPlanViewModel.Accepted), notifications);
        Assert.Equal("Ноги", Assert.Single(await database.PlanRepository.GetAllAsync()).Name);
    }

    [Fact]
    public async Task AcceptAsync_ПравкаПлана_СохраняетНаименованиеИСостав()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        // Отметка упражнения сохраняется, а у коллеги по плану снимается.
        Assert.True(viewModel.AllExercises.Single(row => row.Exercise.Id == squat.Id).IsChecked);
        Assert.False(viewModel.AllExercises.Single(row => row.Exercise.Id == bench.Id).IsChecked);

        viewModel.Name = "Ноги";
        viewModel.AllExercises.Single(row => row.Exercise.Id == bench.Id).IsChecked = true;
        viewModel.AllExercises.Single(row => row.Exercise.Id == squat.Id).IsChecked = false;

        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Equal("Ноги", plan.Name);

        var stored = Assert.Single(await database.PlanRepository.GetAllAsync());
        Assert.Equal("Ноги", stored.Name);
        Assert.Equal(bench.Id, Assert.Single(stored.Exercises).Id);
    }

    /// <summary>
    /// Правка состава с сохранением наименования — обычное дело, и требование «наименование
    /// обязано измениться» делало бы «Принять» серой.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_ТолькоСоставИзменён_КнопкаАктивнаИПланСохраняется()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.AllExercises.Single(row => row.Exercise.Id == bench.Id).IsChecked = true;

        Assert.True(viewModel.AcceptCommand.CanExecute(null));

        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Equal("День", Assert.Single(await database.PlanRepository.GetAllAsync()).Name);
    }

    [Fact]
    public async Task AcceptAsync_СоставУрезанБезСменыНаименования_ПустойPlanНеОстаётся()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        var plan = new TrainingPlan { Name = "Ноги" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.AllExercises.Single().IsChecked = false;

        Assert.True(viewModel.AcceptCommand.CanExecute(null));

        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Empty(Assert.Single(await database.PlanRepository.GetAllAsync()).Exercises);
    }

    [Fact]
    public async Task AcceptAsync_НичегоНеИзменено_КнопкаНеактивна()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        var plan = new TrainingPlan { Name = "Ноги" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.False(viewModel.AcceptCommand.CanExecute(null));

        // Перерисовка видимого списка не должна считаться изменением.
        await viewModel.InitializeCommand.ExecuteAsync(null);
        Assert.False(viewModel.AcceptCommand.CanExecute(null));
    }

    /// <summary>
    /// Дубль наименования гасит кнопку даже тогда, когда состав меняют: «что-то изменилось»
    /// не отменяет проверку имени.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_ДубльНаименованияГаситКнопкуДажеПриСменеСостава()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var other = new TrainingPlan { Name = "Ноги" };
        other.AddExercise(bench);
        await database.PlanRepository.AddAsync(other);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.AllExercises.Single(row => row.Exercise.Id == bench.Id).IsChecked = true;
        Assert.True(viewModel.AcceptCommand.CanExecute(null));

        // Название другого плана занято, поэтому сохранение невозможно ни при каком составе.
        viewModel.Name = "НОГИ";
        Assert.False(viewModel.AcceptCommand.CanExecute(null));
    }

    /// <summary>
    /// Пробелы вокруг прежнего названия изменением не считаются — как и в окне правки
    /// упражнения, где на этом же стоит сравнение по регистру.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_ТолькоПробелыВНазвании_КнопкаНеАктивна()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        await database.Repository.AddAsync(squat);

        var plan = new TrainingPlan { Name = "Ноги" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.Name = "  Ноги  ";

        Assert.False(viewModel.AcceptCommand.CanExecute(null));
    }

    [Fact]
    public async Task AcceptAsync_ПриДубле_AcceptedОстаётсяFalseИУведомленияНет()
    {
        using var database = new TemporaryDatabase();
        var plan = new TrainingPlan { Name = "День" };
        await database.PlanRepository.AddAsync(plan);
        var other = new TrainingPlan { Name = "Ноги" };
        await database.PlanRepository.AddAsync(other);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        // Проверка одна и та же гасит и кнопку, и команду.
        viewModel.Name = "НОГИ";
        Assert.False(viewModel.AcceptCommand.CanExecute(null));

        var notifications = TrackNotifications(viewModel);

        // AsyncRelayCommand.ExecuteAsync не спрашивает CanExecute, поэтому правило всё равно
        // проверяется репозиторием: Accepted подниматься не должен.
        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.False(viewModel.Accepted);
        Assert.DoesNotContain(nameof(EditPlanViewModel.Accepted), notifications);
        Assert.Equal("День", plan.Name);
    }

    /// <summary>
    /// Пустой план допустим: наполнить его можно позже, и требовать отметку значило бы
    /// запретить создать заготовку.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_БезУпражнений_КнопкаАктивна()
    {
        using var database = new TemporaryDatabase();
        var viewModel = CreateViewModel(database, new TrainingPlan(), isNew: true);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.Name = "День отдыха";

        Assert.True(viewModel.AcceptCommand.CanExecute(null));
    }

    [Fact]
    public async Task InitializeAsync_ПустойСправочник_ГлавныйЧекбоксСнят()
    {
        using var database = new TemporaryDatabase();
        var viewModel = CreateViewModel(database, new TrainingPlan(), isNew: true);

        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.AllExercises);
        Assert.False(viewModel.IsAllChecked);
    }

    [Fact]
    public async Task InitializeAsync_ВсеУпражненияВПлане_ГлавныйЧекбоксОтмечен()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "Ноги" };
        plan.AddExercise(squat);
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsAllChecked);
    }

    [Fact]
    public async Task InitializeAsync_ЧастьУпражненийВПлане_ГлавныйЧекбоксНеопределён()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "Ноги" };
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.Null(viewModel.IsAllChecked);
    }

    [Fact]
    public async Task IsAllChecked_ОтметкаГлавногоЧекбокса_ОтмечаетВсеСтроки()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Приседание" });
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var viewModel = CreateViewModel(database, new TrainingPlan(), isNew: true);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.IsAllChecked = true;

        Assert.All(viewModel.AllExercises, row => Assert.True(row.IsChecked));
        Assert.True(viewModel.IsAllChecked);
    }

    /// <summary>
    /// «Выбрать все» меняет состав, а значит должен разбудить «Принять» и в режиме правки,
    /// где наименование осталось прежним.
    /// </summary>
    [Fact]
    public async Task IsAllChecked_ПравкаПлана_БудитКнопкуПодтверждения()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Приседание" });
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var plan = new TrainingPlan { Name = "День" };
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.False(viewModel.AcceptCommand.CanExecute(null));

        viewModel.IsAllChecked = true;

        Assert.True(viewModel.AcceptCommand.CanExecute(null));
    }

    [Fact]
    public async Task IsAllChecked_СнятиеГлавногоЧекбокса_СнимаетВсеСтроки()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Приседание" });
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var viewModel = CreateViewModel(database, new TrainingPlan(), isNew: true);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.IsAllChecked = true;
        viewModel.IsAllChecked = false;

        Assert.All(viewModel.AllExercises, row => Assert.False(row.IsChecked));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(1, 3, null)]
    public void RecomputeMaster_СостоянияГлавногоЧекбокса(int checkedCount, int total, bool? expected)
    {
        Assert.Equal(expected, EditPlanViewModel.RecomputeMaster(checkedCount, total));
    }

    [Fact]
    public void Move_СдвигВверх_МеняетМестами()
    {
        var items = new List<string> { "а", "б", "в" };

        Assert.Equal(0, EditPlanViewModel.Move(items, index: 1, delta: -1));
        Assert.Equal(["б", "а", "в"], items);
    }

    [Fact]
    public void Move_СдвигВниз_МеняетМестами()
    {
        var items = new List<string> { "а", "б", "в" };

        Assert.Equal(2, EditPlanViewModel.Move(items, index: 1, delta: 1));
        Assert.Equal(["а", "в", "б"], items);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(0)]
    public void Move_ЗаГраницейСписка_НичегоНеМеняет(int index)
    {
        var items = new List<string> { "а", "б", "в" };

        Assert.Equal(-1, EditPlanViewModel.Move(items, index, delta: -1));
        Assert.Equal(["а", "б", "в"], items);
    }

    /// <summary>
    /// План открывается в том порядке, в котором будет сохранён: база отдаёт упражнения
    /// упорядоченными, и модель обязана этот порядок удержать, а не пересобрать по алфавиту.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ПланОткрываетсяВСвоёмПорядке()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        var deadlift = new Exercise { Name = "Тяга" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);
        await database.Repository.AddAsync(deadlift);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(deadlift);
        plan.AddExercise(squat);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(
            [deadlift.Name, squat.Name],
            viewModel.SelectedExercises.Select(row => row.Name));
        Assert.Equal([1, 2], viewModel.SelectedExercises.Select(row => row.Position));
    }

    [Fact]
    public async Task MoveDownCommand_УпражнениеВниз_ПорядокИНомераМеняются()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        var first = viewModel.SelectedExercises[0];
        viewModel.MoveDownCommand.Execute(first);

        Assert.Equal([bench.Name, squat.Name], viewModel.SelectedExercises.Select(row => row.Name));

        // Номера пересчитываются по месту в списке: сдвинутая строка получила второе место,
        // а бывшая вторая стала первой.
        Assert.Equal(2, first.Position);
        Assert.Equal(1, viewModel.SelectedExercises[0].Position);
    }

    /// <summary>
    /// Перестановка без смены наименования — тоже изменение плана, и «Принять» должна
    /// разбудиться: иначе порядок нельзя было бы сохранить в тишине.
    /// </summary>
    [Fact]
    public async Task MoveUpCommand_ТолькоПерестановка_БудитКнопкуПодтверждения()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.False(viewModel.AcceptCommand.CanExecute(null));

        viewModel.MoveUpCommand.Execute(viewModel.SelectedExercises[1]);

        Assert.True(viewModel.AcceptCommand.CanExecute(null));

        await viewModel.AcceptCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Equal(
            [bench.Name, squat.Name],
            Assert.Single(await database.PlanRepository.GetAllAsync()).Exercises.Select(exercise => exercise.Name));
    }

    [Fact]
    public async Task MoveUpCommand_ПервоеУпражнение_КомандаНеактивна()
    {
        using var database = new TemporaryDatabase();
        var squat = new Exercise { Name = "Приседание" };
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(squat);
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(squat);
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.False(viewModel.MoveUpCommand.CanExecute(viewModel.SelectedExercises[0]));
        Assert.True(viewModel.MoveDownCommand.CanExecute(viewModel.SelectedExercises[0]));
        Assert.False(viewModel.MoveDownCommand.CanExecute(viewModel.SelectedExercises[1]));
    }

    /// <summary>
    /// Отметка справа не удаляет строку из справочника: он по алфавиту и нужен для поиска.
    /// </summary>
    [Fact]
    public async Task RemoveCommand_УбираетИзСоставаНоНеИзСправочника()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Приседание" });
        var bench = new Exercise { Name = "Жим" };
        await database.Repository.AddAsync(bench);

        var plan = new TrainingPlan { Name = "День" };
        plan.AddExercise(bench);
        await database.PlanRepository.AddAsync(plan);

        var viewModel = CreateViewModel(database, plan, isNew: false);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.RemoveCommand.Execute(viewModel.SelectedExercises[0]);

        Assert.Empty(viewModel.SelectedExercises);
        Assert.Equal(2, viewModel.AllExercises.Count);
        Assert.False(viewModel.AllExercises.Single(row => row.Exercise.Id == bench.Id).IsChecked);
        Assert.True(viewModel.AcceptCommand.CanExecute(null));
    }

    private static EditPlanViewModel CreateViewModel(TemporaryDatabase database, TrainingPlan plan, bool isNew) =>
        new(plan, isNew, database.PlanRepository, database.Repository);

    private static List<string?> TrackNotifications(EditPlanViewModel viewModel)
    {
        var notifications = new List<string?>();

        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        return notifications;
    }
}
