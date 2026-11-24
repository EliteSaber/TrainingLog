using TrainingLog.Core;
using TrainingLog.Core.Models;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>
/// Окно справочника. Импорт пишет в базу мимо модели окна, поэтому модель обязана перечитывать
/// справочник сама: иначе правка выглядела бы несработавшей.
/// </summary>
public sealed class ExercisesViewModelTests
{
    [Fact]
    public async Task LoadAsync_ПоказываетСправочник()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });

        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Жим", "Тяга"], viewModel.Visible.Select(exercise => exercise.Name));
        Assert.False(viewModel.IsEmpty);
    }

    /// <summary>
    /// Импорт пишет в базу мимо модели окна, поэтому модель обязана перечитывать справочник
    /// сама: иначе правка выглядела бы несработавшей. Перечитывание вызывает окно импорта —
    /// когда оно сообщает, что упражнения записаны.
    /// </summary>
    [Fact]
    public async Task ImportCommand_ПослеИмпорта_ПеречитываетСправочник()
    {
        using var database = new TemporaryDatabase();
        var windowService = new FakeWindowService();
        var viewModel = new ExercisesViewModel(database.Repository, windowService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Так же, как пишет окно импорта: модель окна справочника об этой записи не знает.
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        viewModel.ImportCommand.Execute(null);

        Assert.Equal(1, windowService.ImportCalls);

        // Окно импорта сообщило, что упражнения записаны.
        await windowService.RaiseImportSaved();

        Assert.Equal("Жим", Assert.Single(viewModel.Visible).Name);
    }

    /// <summary>Окно импорта не сообщило о записи — справочник прежний.</summary>
    [Fact]
    public async Task ImportCommand_БезЗаписи_СписокНеПеречитывается()
    {
        using var database = new TemporaryDatabase();
        var windowService = new FakeWindowService();
        var viewModel = new ExercisesViewModel(database.Repository, windowService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        viewModel.ImportCommand.Execute(null);

        Assert.Equal(1, windowService.ImportCalls);
        Assert.Empty(viewModel.Visible);
    }

    /// <summary>
    /// Пока упражнение правится, его нельзя удалить: после удаления «Принять» не нашёл бы запись
    /// и молча ничего не сделал. Сервис знает, какое упражнение правится на самом деле.
    /// </summary>
    [Fact]
    public async Task EditCommand_ОткрытаПравка_УпражнениеПомеченоКакРедактируемое()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });

        var windowService = new FakeWindowService();
        var viewModel = new ExercisesViewModel(database.Repository, windowService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        var shown = viewModel.Visible.Single();
        viewModel.EditCommand.Execute(shown);

        Assert.Same(shown, viewModel.EditedExercise);

        // Окно закрылось без записи — кнопка «Удалить» снова активна.
        windowService.RaiseEditClosed();

        Assert.Null(viewModel.EditedExercise);
    }

    /// <summary>
    /// Правка упражнения меняет имя — и сортировка могла поменяться, поэтому видимый список
    /// перестраивается. Перестраивает его окно правки, когда сообщает о записи.
    /// </summary>
    /// <remarks>
    /// Переименование обязано менять порядок: с одним упражнением перестройка ненаблюдаема,
    /// и тест прошёл бы даже с вовсе не подключённым обратным вызовом. Объект переименовывает
    /// сам тест — как это делает окно правки, — поэтому проверяется порядок, а не имя.
    /// </remarks>
    [Fact]
    public async Task EditCommand_ПослеПравки_ПерестраиваетСписок()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });

        var windowService = new FakeWindowService();
        var viewModel = new ExercisesViewModel(database.Repository, windowService);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Жим", "Тяга"], viewModel.Visible.Select(exercise => exercise.Name));

        // Окно правки правит объект из видимого списка — так же, как это делает справочник.
        var shown = viewModel.Visible[0];
        viewModel.EditCommand.Execute(shown);

        shown.Name = "Ящик";

        await windowService.RaiseExerciseSaved();

        // «Ящик» после «Тяги»: без перестройки список остался бы в прежнем порядке.
        Assert.Equal(["Тяга", "Ящик"], viewModel.Visible.Select(exercise => exercise.Name));

        // Перестройка переставляет, а не пересоздаёт: строка переживает правку тем же объектом.
        Assert.Same(shown, viewModel.Visible[1]);
    }

    /// <summary>
    /// Сортировка списка — по кодам Unicode (Ordinal): блок кириллицы расположен в алфавитном
    /// порядке, поэтому сравнение кодов даёт русский алфавит. Проверяется на модели окна:
    /// репозиторий отдаёт справочник без порядка, сортирует именно окно.
    /// </summary>
    [Fact]
    public async Task Visible_Кириллица_ПорядокПоАлфавитуИОбратно()
    {
        using var database = new TemporaryDatabase();

        // Вставка нарочно вразнобой: порядок показа не должен зависеть от порядка чтения базы.
        foreach (var name in new[] { "Ящик", "Болт", "Присед", "Атлант" })
        {
            await database.Repository.AddAsync(new Exercise { Name = name });
        }

        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Атлант", "Болт", "Присед", "Ящик"], viewModel.Visible.Select(exercise => exercise.Name));

        viewModel.ToggleSortCommand.Execute(null);

        Assert.Equal(["Ящик", "Присед", "Болт", "Атлант"], viewModel.Visible.Select(exercise => exercise.Name));
    }

    /// <summary>
    /// Выгрузка отражает справочник, а не то, что показано на экране: поиск и обратная сортировка
    /// не должны менять файл, иначе одна и та же выгрузка давала бы разные файлы.
    /// </summary>
    [Fact]
    public async Task Export_ПишетВесьСправочникПоАлфавиту()
    {
        using var database = new TemporaryDatabase();
        await database.Repository.AddAsync(new Exercise { Name = "Тяга" });
        await database.Repository.AddAsync(new Exercise { Name = "Жим" });
        await database.Repository.AddAsync(new Exercise { Name = "Приседание" });

        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.IsDescending = true;
        viewModel.FilterText = "ж";

        var path = TemporaryPath();

        try
        {
            await viewModel.ExportCommand.ExecuteAsync(path);

            Assert.True(viewModel.ExportSucceeded);
            Assert.Equal(3, viewModel.Count);
            Assert.Equal(["Жим", "Приседание", "Тяга"], ExerciseFileReader.ReadLines(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CanExport_ПустойСправочник_КнопкаПогашена()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.CanExport);
        Assert.False(viewModel.ExportCommand.CanExecute(null));

        viewModel.NewName = "Жим";
        await viewModel.AddCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanExport);
        Assert.True(viewModel.ExportCommand.CanExecute(null));
    }

    /// <summary>Проверка одна гасит и кнопку, и тело команды: Execute её не спрашивает (ловушка 46).</summary>
    [Fact]
    public async Task Export_ПустойСправочникВОбходПредиката_НичегоНеПишет()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        var path = TemporaryPath();

        try
        {
            await viewModel.ExportCommand.ExecuteAsync(path);

            Assert.False(viewModel.ExportSucceeded);
            Assert.False(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Export_БезПути_НичегоНеПишет()
    {
        using var database = new TemporaryDatabase();
        var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
        viewModel.NewName = "Жим";

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.False(viewModel.ExportSucceeded);
    }

    private static string TemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"TrainingLogExport-{Guid.NewGuid():N}.txt");
}
