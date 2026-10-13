using System.Text;
using TrainingLog.Core;
using TrainingLog.Core.Models;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Окно импорта. Проверяется и то, что попадает в поле, и то, что реально уходит в базу: между
/// ними есть разбор по тем же правилам, и правила эти менялись вместе с требованиями к полю.
/// </summary>
public sealed class ImportExercisesViewModelTests
{
    private static readonly string[] ImportableNames = ["Жим", "тяга", "Приседание"];

    [Fact]
    public void LoadFile_ВПолеТолькоГодныеУпражнения()
    {
        using var database = new TemporaryDatabase();
        using var file = File("Жим\r\n\r\n   \nтяга\r\nЖим\r\nПриседание\r\n");

        var viewModel = new ImportExercisesViewModel(database.Repository);
        viewModel.LoadFile(file.FilePath);

        Assert.Equal(ImportableNames, ExerciseImportRules.SplitLines(viewModel.Text));
        Assert.Equal(file.FilePath, viewModel.SourcePath);
        Assert.False(viewModel.HasStatus);
    }

    [Fact]
    public void LoadFile_Сводка_СчитаетПрочитанныеИОтброшенныеСтроки()
    {
        using var database = new TemporaryDatabase();
        using var file = File("Жим\r\n\r\nТяга\r\n");

        var viewModel = new ImportExercisesViewModel(database.Repository);
        viewModel.LoadFile(file.FilePath);

        Assert.Contains("Прочитано строк: 3, в поле: 2", viewModel.Summary);
        Assert.True(viewModel.HasSummary);
    }

    [Fact]
    public void LoadFile_ВсеСтрокиПриняты_СводкаБезОтброшенных()
    {
        using var database = new TemporaryDatabase();
        using var file = File("Жим\r\nТяга\r\n");

        var viewModel = new ImportExercisesViewModel(database.Repository);
        viewModel.LoadFile(file.FilePath);

        Assert.Contains("Прочитано строк: 2. Все строки приняты.", viewModel.Summary);
    }

    [Fact]
    public void LoadFile_НесуществующийФайл_Отказ()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new ImportExercisesViewModel(database.Repository);

        viewModel.LoadFile(Path.Combine(Path.GetTempPath(), $"TrainingLogImport-{Guid.NewGuid():N}.txt"));

        Assert.True(viewModel.HasStatus);
        Assert.Equal(string.Empty, viewModel.Text);
        Assert.Equal(string.Empty, viewModel.SourcePath);
    }

    [Fact]
    public void LoadFile_Utf16_ОтказСТекстомОКодировке()
    {
        using var database = new TemporaryDatabase();
        using var file = new TemporaryFile([0xFF, 0xFE, 0x41, 0x00]);
        var viewModel = new ImportExercisesViewModel(database.Repository);

        viewModel.LoadFile(file.FilePath);

        Assert.Contains("UTF-16", viewModel.Status);
        Assert.Equal(string.Empty, viewModel.Text);
    }

    [Fact]
    public void LoadFile_ПустойФайл_ПолеПустоеИКнопкаПогашена()
    {
        using var database = new TemporaryDatabase();
        using var file = new TemporaryFile([]);

        var viewModel = new ImportExercisesViewModel(database.Repository);
        viewModel.LoadFile(file.FilePath);

        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_БезОчистки_ПишетТолькоНовые()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        using var file = File("Жим\r\nТяга\r\n");

        var viewModel = await LoadAsync(database, file);
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Equal(["Жим", "Тяга"], await NamesAsync(database));
    }

    [Fact]
    public async Task Save_СОчисткой_СправочникСтановитсяСпискомИзПоля()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });
        using var file = File("Приседание\r\n");

        var viewModel = await LoadAsync(database, file);
        viewModel.ClearExisting = true;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Equal(["Приседание"], await NamesAsync(database));
    }

    /// <summary>Правка поля руками проверяется на тех же условиях, что и файл.</summary>
    [Fact]
    public async Task Save_СтрокаДописаннаяРуками_ПроходитТежеПравила()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        using var file = File("Тяга\r\n");

        var viewModel = await LoadAsync(database, file);
        viewModel.Text = $"Тяга\r\n  ЖИМ  \r\n\r\nПриседание";
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["Жим", "Приседание", "Тяга"], await NamesAsync(database));
    }

    /// <summary>Окно ставит DialogResult по уведомлению: по одному значению Accepted оно бы не узнало о записи.</summary>
    [Fact]
    public async Task Save_ПослеУспешнойЗаписи_AcceptedСталTrueИПришлоУведомление()
    {
        using var database = new TemporaryDatabase();
        using var file = File("Жим\r\n");

        var viewModel = await LoadAsync(database, file);

        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.Accepted);
        Assert.Contains(nameof(ImportExercisesViewModel.Accepted), notifications);
    }

    /// <summary>
    /// Упражнение могли добавить, пока окно импорта открыто: предварительная проверка по памяти
    /// тогда разошлась с базой, и последним рубежом остался уникальный индекс.
    /// </summary>
    [Fact]
    public async Task Save_ИмяЗанятоеПокаОкноОткрыто_ОтказИAcceptedОстаётсяFalse()
    {
        using var database = new TemporaryDatabase();
        using var file = File("Тяга\r\n");

        var viewModel = await LoadAsync(database, file);
        await database.Repository.AddAsync(new Exercise { Name = "тяга" });

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.Accepted);
        Assert.True(viewModel.HasStatus);
        Assert.Equal(["тяга"], await NamesAsync(database));
    }

    /// <summary>Проверка одна гасит и кнопку, и тело команды: Execute её не спрашивает (ловушка 46).</summary>
    [Fact]
    public async Task Save_ВОбходПредиката_НичегоНеПишет()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new ImportExercisesViewModel(database.Repository);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.Accepted);
        Assert.Empty(await database.Repository.GetAllAsync());
    }

    private static TemporaryFile File(string content) =>
        new(Encoding.UTF8.GetBytes(content));

    private static async Task<ImportExercisesViewModel> LoadAsync(TemporaryDatabase database, TemporaryFile file)
    {
        var viewModel = new ImportExercisesViewModel(database.Repository);

        await viewModel.InitializeCommand.ExecuteAsync(null);
        viewModel.LoadFile(file.FilePath);

        return viewModel;
    }

    private static async Task<string[]> NamesAsync(TemporaryDatabase database) =>
        (await database.Repository.GetAllAsync()).Select(exercise => exercise.Name).Order(StringComparer.Ordinal).ToArray();
}
