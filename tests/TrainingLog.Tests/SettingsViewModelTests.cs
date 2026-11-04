using TrainingLog.Core.Data;
using TrainingLog.Core.Settings;
using TrainingLog.ViewModels;

namespace TrainingLog.Tests;

/// <summary>Окно параметров: откуда значение в поле, что делает «Сохранить» и чем гасится отказ.</summary>
public sealed class SettingsViewModelTests
{
    /// <summary>
    /// Поле открывается на сохранённом значении: пустое поле снесло бы настройку первым же
    /// сохранением. Имя базы в файле хранится с расширением, поле показывает его без хвоста.
    /// </summary>
    [Fact]
    public void Поля_ОткрываютсяНаСохранённыхЗначениях()
    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
            {
                DaysCount = 12,
                DatabaseDirectory = @"D:\журнал\базы",
                DatabaseName = "спорт.db",
            });

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.Equal(12, viewModel.DaysCount);
        Assert.Equal(@"D:\журнал\базы", viewModel.DatabaseDirectory);
        Assert.Equal("спорт", viewModel.DatabaseName);
    }

    /// <summary>По <c>Accepted</c> окно выставляет <c>DialogResult</c>, а главное окно перечитывает файл.</summary>
    [Fact]
    public void Save_ПишетВФайл_ИОтмечаетПринятым()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store)
        {
            DaysCount = 6,
            DatabaseDirectory = @"D:\журнал\базы",
            DatabaseName = "спорт",
        };

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Accepted);

        var loaded = new AppSettingsStore(settings.FilePath).Load();

        Assert.Equal(6, loaded.DaysCount);
        Assert.Equal(@"D:\журнал\базы", loaded.DatabaseDirectory);
        Assert.Equal("спорт.db", loaded.DatabaseName);
    }

    /// <summary>
    /// Расширение дописывается всегда и в файл попадает один раз: впечатав его вручную, получили
    /// бы «база.db.db», собирая имя конкатенацией.
    /// </summary>
    [Fact]
    public void Save_ХвостРасширениеВПоле_ФайлБезУдвоения()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DatabaseName = "спорт.db" };

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("спорт.db", new AppSettingsStore(settings.FilePath).Load().DatabaseName);
    }

    /// <summary>После сохранения поле снова показывает имя без расширения: показ и хранение разведены.</summary>
    [Fact]
    public void Круг_SaveОткрыть_FieldБезРасширения()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DatabaseName = "спорт.2026" };

        viewModel.SaveCommand.Execute(null);

        var opened = new SettingsViewModel(new AppSettingsStore(settings.FilePath));

        Assert.Equal("спорт.2026", opened.DatabaseName);
    }

    /// <summary>
    /// Признак объявляется уведомлением: по одному значению <c>Accepted</c> окно не выставило бы
    /// <c>DialogResult</c>, и главное окно осталось бы со старым числом дней.
    /// </summary>
    [Fact]
    public void Save_УведомляетОПринятом()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DaysCount = 4 };

        var notified = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        viewModel.SaveCommand.Execute(null);

        Assert.Contains(nameof(SettingsViewModel.Accepted), notified);
    }

    /// <summary>Иначе журнал открывался бы на ноль дней или на тысячу.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(DaysCountRules.MaxDaysCount + 1)]
    public void SaveCommand_НедопустимоеЗначение_КнопкаГасится(int daysCount)
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DaysCount = daysCount };

        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// База собирается из двух полей, и оба должны быть заполнены: пробельный адрес соединение
    /// построило бы, а базу — нет.
    /// </summary>
    [Theory]
    [InlineData("", "база.db")]
    [InlineData("  ", "база.db")]
    [InlineData(@"D:\базы", "")]
    [InlineData(@"D:\базы", "  ")]
    public void SaveCommand_АдресБазыНеполный_КнопкаГасится(string directory, string name)
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store)
        {
            DatabaseDirectory = directory,
            DatabaseName = name,
        };

        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// Проверка полей повторяется и в теле команды: <c>Execute</c> предикат не спрашивает
    /// (ловушка 46).
    /// </summary>
    [Fact]
    public void Save_ДиректорияПустаПриВызовеПрограммно_НеЗаписывает()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DatabaseDirectory = "" };

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Accepted);
        Assert.Equal(SqliteDatabasePath.DefaultDirectory, new AppSettingsStore(settings.FilePath).Load().DatabaseDirectory);
    }

    /// <summary>
    /// Проверка значения повторяется и в теле команды: <c>Execute</c> предикат не спрашивает
    /// (ловушка 46). Проверяется содержимое файла, а не его отсутствие: к этому моменту файл уже
    /// создан при открытии окна.
    /// </summary>
    [Fact]
    public void Save_НедопустимоеЗначениеВызваноПрограммно_НеЗаписывает()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store) { DaysCount = 0 };

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Accepted);
        Assert.Equal(AppSettings.DefaultDaysCount, settings.Store.Load().DaysCount);
    }

    /// <summary>
    /// Отказ записи показывается надписью и держится до успеха: молча закрывшееся окно выглядело
    /// бы как «настройка сохранилась». Каталога нет, поэтому запись в него невозможна.
    /// </summary>
    [Fact]
    public void Save_ОтказЗаписи_ПоказываетОтказАПослеУспеха_ГаситЕго()
    {
        using var settings = new TemporarySettings();

        var file = Path.Combine(settings.FolderPath, "позже", "settings.json");
        var viewModel = new SettingsViewModel(new AppSettingsStore(file)) { DaysCount = 5 };

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.HasStatus);
        Assert.NotEqual(string.Empty, viewModel.Status);
        Assert.False(viewModel.Accepted);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasStatus);
        Assert.True(viewModel.Accepted);
        Assert.Equal(5, new AppSettingsStore(file).Load().DaysCount);
    }

    /// <summary>Отказ по вводу показывается той же подсказкой, что и в остальных окнах.</summary>
    [Fact]
    public void Hint_ОтказПоВводу_Показывается()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.Hint.ShowCommand.Execute("В поле допустимы только цифры.");

        Assert.True(viewModel.Hint.IsVisible);
        Assert.Equal("В поле допустимы только цифры.", viewModel.Hint.Text);

        viewModel.Hint.DismissCommand.Execute(null);

        Assert.False(viewModel.Hint.IsVisible);
    }

    /// <summary>Строки вкладки построены по каталогу, значения — из файла, а не из умолчаний.</summary>
    [Fact]
    public void Строки_ЗначениеИзФайлаПередУмолчанием()
    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
        {
            Hotkeys = new Dictionary<string, string> { ["Exercises.Add"] = "ctrl+f2" },
        });

        var viewModel = new SettingsViewModel(settings.Store);

        var row = HotkeyRow(viewModel, "Exercises.Add");

        // Файл хранит запись в любом виде, а строка покажет её canonical вид.
        Assert.Equal("Ctrl+F2", row.GestureText);

        // Действие без записи в файле показывает пусто: умолчания у него нет.
        Assert.Equal(string.Empty, HotkeyRow(viewModel, "Exercises.ToggleSort").GestureText);
    }

    /// <summary>Действие с умолчанием показывает его, пока пользователь не написал своё.</summary>
    [Fact]
    public void Строки_УмолчаниеИзКаталога()
    {
        using var settings = new TemporarySettings();

        Assert.Equal(
            "Ctrl+S",
            HotkeyRow(new SettingsViewModel(settings.Store), "Day.Save").GestureText);
    }

    /// <summary>Конфликт гасит «Сохранить» и показывает, чем занята комбинация.</summary>
    /// <remarks>
    /// В сообщении названо действие — прежний держатель комбинации: пользователь же назначает
    /// её сейчас, и ему нужно знать, с чем именно столкнулась его правка.
    /// </remarks>
    [Fact]
    public void Конфликт_ГаситКнопкуИПоказываетКонфликтующееДействие()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        HotkeyRow(viewModel, "Exercises.Add").CommitCapture("ctrl+f2");
        HotkeyRow(viewModel, "Exercises.ToggleSort").CommitCapture("Ctrl+F2");

        Assert.True(viewModel.HasHotkeyConflict);
        Assert.Contains("Добавить", viewModel.HotkeyConflict);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// Проверка конфликта повторяется в теле команды: <c>Execute</c> предикат не спрашивает
    /// (ловушка 46), и секция с конфликтом на диск не попадает.
    /// </summary>
    [Fact]
    public void Save_КонфликтПриПрограммномВызове_НеЗаписывает()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);
        var first = HotkeyRow(viewModel, "Exercises.Add");
        var second = HotkeyRow(viewModel, "Exercises.ToggleSort");

        foreach (var row in new[] { first, second })
        {
            row.GestureText = "F2";
        }

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Accepted);
        Assert.Null(new AppSettingsStore(settings.FilePath).Load().Hotkeys.GetValueOrDefault("Exercises.ToggleSort"));
    }

    /// <summary>«Убрать» возвращает умолчание, и умолчание в файл не пишется.</summary>
    [Fact]
    public void Убрать_ВозвращаетУмолчание_ВФайлНеПопадает()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        HotkeyRow(viewModel, "Day.Save").GestureText = "Ctrl+Q";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Ctrl+Q", new AppSettingsStore(settings.FilePath).Load().Hotkeys["Day.Save"]);

        HotkeyRow(viewModel, "Day.Save").ClearCommand.Execute(null);
        HotkeyRow(viewModel, "Exercises.Add").ClearCommand.Execute(null);

        // После «Убрать»: назначение без умолчания стало пустым, с умолчанием — умолчанием.
        Assert.Equal("Ctrl+S", HotkeyRow(viewModel, "Day.Save").GestureText);
        Assert.Equal(string.Empty, HotkeyRow(viewModel, "Exercises.Add").GestureText);

        viewModel.SaveCommand.Execute(null);

        // Умолчание и пустота в файл не пишутся: отсутствие записи — само умолчание.
        Assert.Empty(new AppSettingsStore(settings.FilePath).Load().Hotkeys);
    }

    /// <summary>Назначение без умолчания попадает в секцию файла.</summary>
    [Fact]
    public void Save_НазначениеПишетсяВСекцию()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        HotkeyRow(viewModel, "Exercises.Add").GestureText = "Ctrl+F2";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Ctrl+F2", new AppSettingsStore(settings.FilePath).Load().Hotkeys["Exercises.Add"]);
    }

    /// <summary>Одинаковая комбинация в разных окнах конфликтом не считается.</summary>
    /// <remarks>
    /// KeyBinding живёт в окне, а не в глобальном хуке: одна комбинация в двух окнах
    /// выполняет два разных действия, и общий запрет приписал бы законные совпадения.
    /// </remarks>
    [Fact]
    public void Конфликт_ТолькоВнутриОкна()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        HotkeyRow(viewModel, "Exercises.Add").CommitCapture("F2");
        HotkeyRow(viewModel, "Plans.Add").CommitCapture("F2");

        Assert.False(viewModel.HasHotkeyConflict);
        Assert.True(viewModel.SaveCommand.CanExecute(null));

        viewModel.SaveCommand.Execute(null);

        var stored = new AppSettingsStore(settings.FilePath).Load().Hotkeys;

        Assert.Equal("F2", stored.GetValueOrDefault("Exercises.Add"));
        Assert.Equal("F2", stored.GetValueOrDefault("Plans.Add"));
    }

    /// <summary>Конфликт после снятия назначения исчезает: кнопка снова продвигает сохранение.</summary>
    [Fact]
    public void Конфликт_ПоСнятииНазначенияИсчезает()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        HotkeyRow(viewModel, "Exercises.Add").CommitCapture("F2");
        HotkeyRow(viewModel, "Exercises.ToggleSort").CommitCapture("F2");

        Assert.True(viewModel.HasHotkeyConflict);

        HotkeyRow(viewModel, "Exercises.Add").ClearCommand.Execute(null);

        Assert.False(viewModel.HasHotkeyConflict);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// Сохранение во время захвата не пишет в файл промежуточный набор строки.
    /// </summary>
    /// <remarks>
    /// «Сохранить» сработает и тогда, когда поле захвата показывает приглашение или «Ctrl+…»:
    /// такой текст назначением быть не может — жест по нему никогда не построится. Признак
    /// <see cref="HotkeyRowViewModel.IsCapturing"/> отсекает незавершённые строки, а другие
    /// подтверждённые назначения в тот же момент сохраняются как ни в чём не бывало.
    /// </remarks>
    [Fact]
    public void СохранениеВоВремяЗахвата_НеПишетНезавершённыйНабор()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        // Одну строку подтверждаем, вторую оставляем в незавершённом наборе.
        HotkeyRow(viewModel, "Exercises.Add").CommitCapture("Ctrl+D");

        var capturing = HotkeyRow(viewModel, "Plans.Add");
        capturing.BeginCapture();
        capturing.CapturePreview("Ctrl+B");

        Assert.True(viewModel.SaveCommand.CanExecute(null));

        viewModel.SaveCommand.Execute(null);

        var stored = new AppSettingsStore(settings.FilePath).Load().Hotkeys;

        Assert.Equal("Ctrl+D", stored.GetValueOrDefault("Exercises.Add"));
        Assert.False(stored.ContainsKey("Plans.Add"));
    }

    /// <summary>
    /// Признак захвата ставится началом набора и гасится и подтверждением, и отменой.
    /// </summary>
    /// <remarks>
    /// На нём держится и подсветка поля, и защита сохранения: флаг, переживший набор, покрыл бы
    /// строку подсветкой навсегда или молча выпал бы из файла вместе с настоящим назначением.
    /// </remarks>
    [Fact]
    public void ФлагЗахвата_СтавитсяИСнимаетсяПодтверждениемИОтменой()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var row = HotkeyRow(viewModel, "Exercises.Add");

        row.BeginCapture();
        Assert.True(row.IsCapturing);

        row.CommitCapture("Ctrl+D");
        Assert.False(row.IsCapturing);

        row.BeginCapture();
        Assert.True(row.IsCapturing);

        row.CancelCapture();
        Assert.False(row.IsCapturing);
    }

    /// <summary>
    /// Плоский перечень вкладки повторяет группы: за заголовком окна идут строки этого окна.
    /// </summary>
    /// <remarks>
    /// Виртуализация вкладки слушает именно этот перечень: порядок обязан совпасть с группами,
    /// иначе строки поедут под чужими заголовками, а отсутствие заголовка собьёт ритм списка.
    /// </remarks>
    [Fact]
    public void ПлоскийСписок_ПовторяетГруппыПоПорядку()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var listing = viewModel.HotkeyListing;
        var position = 0;

        foreach (var group in viewModel.HotkeyGroups)
        {
            var section = Assert.IsType<HotkeySectionViewModel>(listing[position++]);
            Assert.Equal(group.WindowName, section.WindowName);

            foreach (var row in group.Rows)
            {
                Assert.Same(row, Assert.IsType<HotkeyRowViewModel>(listing[position++]));
            }
        }

        Assert.Equal(listing.Count, position);
    }

    /// <summary>Строка поиска по id: каталог у теста один на все группы.</summary>
    private static HotkeyRowViewModel HotkeyRow(SettingsViewModel viewModel, string id) =>
        viewModel.HotkeyGroups.SelectMany(group => group.Rows).Single(row => row.Id == id);
}
