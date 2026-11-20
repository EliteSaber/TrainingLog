using TrainingLog.Core.Data;
using TrainingLog.Core.Settings;
using TrainingLog.Services;
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

    /// <summary>Настройка запуска читается из файла: снятая галочка — развёрнутый фильтр.</summary>
    [Fact]
    public void ФильтрПриЗапуске_БерётсяИзФайла()
    {
        using var settings = new TemporarySettings();
        settings.Write("""{"CollapseFiltersOnStartup": false}""");

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.False(viewModel.CollapseFiltersOnStartup);
    }

    /// <summary>Снятая галочка пишется тем же «Сохранить» и читается обратно из файла.</summary>
    [Fact]
    public void Save_ФильтрПриЗапуске_ПишетсяВФайл()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store)
        {
            CollapseFiltersOnStartup = false,
        };

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Accepted);
        Assert.False(new AppSettingsStore(settings.FilePath).Load().CollapseFiltersOnStartup);
    }

    /// <summary>
    /// Пустая запись в файле — гарнитура по умолчанию «Segoe UI», и ComboBox, и откат показывают
    /// её, а не пустоту: применённый шрифт приложения и поле выбора обязаны совпадать.
    /// </summary>
    [Fact]
    public void Шрифт_БезЗаписи_Умолчание()
    {
        using var settings = new TemporarySettings();

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.Equal(FontRules.DefaultFontFamily, viewModel.SelectedFont.Name);
        Assert.Equal(FontRules.DefaultFontFamily, viewModel.SavedFontName);
        Assert.Equal(AppSettings.DefaultFontSizePercent, viewModel.SelectedScale);
        Assert.Equal(AppSettings.DefaultFontSizePercent, viewModel.SavedFontSizePercent);
    }

    /// <summary>Сохранённое имя читается и без каталога: перечень ему не нужен, нужен Apply.</summary>
    [Fact]
    public void Шрифт_ЗаписьБезКаталога_ЧитаетсяКакЕсть()
    {
        using var settings = new TemporarySettings();
        settings.Write("""{"FontFamily": "Fira Code", "FontSizePercent": 125}""");

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.Equal("Fira Code", viewModel.SelectedFont.Name);
        Assert.Equal("Fira Code", viewModel.SavedFontName);
        Assert.Equal(125, viewModel.SelectedScale);
        Assert.Equal(125, viewModel.SavedFontSizePercent);
    }

    /// <summary>Выбранный пункт пишется тем же «Сохранить», без передачи через Apply.</summary>
    [Fact]
    public void Save_ВыбранныйШрифт_ЗаписываетсяВФайл()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store)
        {
            DaysCount = 6,
            DatabaseDirectory = @"D:\журнал\базы",
            DatabaseName = "спорт",
            SelectedFont = new FontCatalog.FontFamilyOption("Verdana"),
            SelectedScale = 110,
        };

        viewModel.SaveCommand.Execute(null);

        var loaded = new AppSettingsStore(settings.FilePath).Load();

        Assert.Equal("Verdana", loaded.FontFamily);
        Assert.Equal(110, loaded.FontSizePercent);
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

    /// <summary>
    /// Поиск по названию действия находит его строку — и только её, а не всё окно.
    /// </summary>
    /// <remarks>
    /// Имя действия ищется по подстроке без учёта регистра: пользователь пишет «сохранить»
    /// строчными, а в перечне «Сохранить».
    /// </remarks>
    [Fact]
    public void Поиск_ПоНазваниюДействия_ОставляетЕгоСтроки()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.HotkeySearch = "сохранить";

        var rows = ListingRows(viewModel);

        Assert.Contains(rows, row => row.Id == "Day.Save");
        Assert.Contains(rows, row => row.Id == "Day.SaveAndClose");
        Assert.Contains(rows, row => row.Id == "Import.Save");
        Assert.Contains(rows, row => row.Id == "Settings.Save");
        Assert.DoesNotContain(rows, row => row.Id == "Exercises.Delete");
    }

    /// <summary>Поиск по названию окна оставляет все строки этого окна.</summary>
    [Fact]
    public void Поиск_ПоНазваниюОкна_ОставляетСтрокиЭтогоОкна()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.HotkeySearch = "окно дня";

        var rows = ListingRows(viewModel);
        var dayRows = viewModel.HotkeyGroups.Single(group => group.WindowName == "Окно дня").Rows;

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Contains(row, dayRows));
    }

    /// <summary>
    /// Заголовок окна без совпадений пропадает: пустой раздел выглядит обломком структуры.
    /// </summary>
    [Fact]
    public void Поиск_ЗаголовокГруппыБезСовпадений_Пропадает()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.HotkeySearch = "Экспорт";

        Assert.Equal(["Упражнения"], ListingSections(viewModel));
        Assert.Equal("Exercises.Export", Assert.Single(ListingRows(viewModel)).Id);
    }

    /// <summary>Пустой перечень — это «ничего не найдено», а не пустое окно.</summary>
    [Fact]
    public void Поиск_БезСовпадений_ПереченьПуст()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.HotkeySearch = "нетакого";

        Assert.Empty(viewModel.HotkeyListing);
        Assert.True(viewModel.IsHotkeyListingEmpty);
    }

    /// <summary>
    /// Кнопка-переход оставляет только строки своего окна: до нужной клавиши не надо скроллить.
    /// </summary>
    [Fact]
    public void КнопкаОкна_ОставляетТолькоЕгоСтроки()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var chip = viewModel.HotkeyChips.Single(candidate => candidate.Label == "Планы");

        viewModel.SelectChipCommand.Execute(chip);

        var rows = ListingRows(viewModel);
        var plansRows = viewModel.HotkeyGroups.Single(group => group.WindowName == "Планы").Rows;

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Contains(row, plansRows));
        Assert.True(chip.IsSelected);
    }

    /// <summary>«Все окна» возвращает полный перечень: переход туда-обратно ничего не теряет.</summary>
    [Fact]
    public void КнопкаВсеОкна_ВозвращаетПолныйПеречень()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var full = viewModel.HotkeyListing.Count;

        viewModel.SelectChipCommand.Execute(viewModel.HotkeyChips.Single(candidate => candidate.Label == "Планы"));
        viewModel.SelectChipCommand.Execute(viewModel.HotkeyChips[0]);

        Assert.Equal(full, viewModel.HotkeyListing.Count);
        Assert.True(viewModel.HotkeyChips[0].IsSelected);
    }

    /// <summary>
    /// «Только изменённые» оставляет строки, чья комбинация отличается от умолчания.
    /// </summary>
    /// <remarks>
    /// Правка идёт через подтверждение захвата: только оно пересобирает перечень — промежуточный
    /// набор не пересобирает, иначе список прыгал бы на каждом модификаторе.
    /// </remarks>
    [Fact]
    public void ТолькоИзменённые_ОставляетОтличившиесяОтУмолчания()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.HotkeyChangedOnly = true;

        Assert.Empty(ListingRows(viewModel));

        HotkeyRow(viewModel, "Exercises.Add").CommitCapture("Ctrl+F2");

        Assert.Equal("Exercises.Add", Assert.Single(ListingRows(viewModel)).Id);

        // «Убрать» возвращает умолчание — и строка уходит из перечня снова.
        HotkeyRow(viewModel, "Exercises.Add").ClearCommand.Execute(null);

        Assert.Empty(ListingRows(viewModel));
    }

    /// <summary>
    /// Фильтр не влияет на запись: назначение, сделанное в суженном перечне, попадает в файл.
    /// </summary>
    /// <remarks>
    /// В файл пишутся все назначения, а не только видимые: фильтр — это вид, и он не должен
    /// молча выбрасывать то, что пользователь настроил в другом окне.
    /// </remarks>
    [Fact]
    public void Фильтр_НеВлияетНаЗапись()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        HotkeyRow(viewModel, "Exercises.Add").CommitCapture("Ctrl+F2");

        viewModel.HotkeySearch = "Планы";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Ctrl+F2", new AppSettingsStore(settings.FilePath).Load().Hotkeys["Exercises.Add"]);
    }

    /// <summary>
    /// Конфликт называет окно: при суженном перечне источник может быть не на экране.
    /// </summary>
    [Fact]
    public void Конфликт_НазываетОкно()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        HotkeyRow(viewModel, "Exercises.Add").CommitCapture("Ctrl+F2");
        HotkeyRow(viewModel, "Exercises.ToggleSort").CommitCapture("Ctrl+F2");

        Assert.Contains("Упражнения", viewModel.HotkeyConflict);
    }

    /// <summary>Крестик очищает поиск, и перечень снова полный.</summary>
    [Fact]
    public void ОчисткаПоиска_ВозвращаетПолныйПеречень()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var full = viewModel.HotkeyListing.Count;

        viewModel.HotkeySearch = "Экспорт";

        Assert.Single(ListingRows(viewModel));

        viewModel.ClearHotkeySearchCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.HotkeySearch);
        Assert.Equal(full, viewModel.HotkeyListing.Count);
    }

    /// <summary>
    /// На пустом поле крестик гаснет: очищать нечего.
    /// </summary>
    /// <remarks>
    /// Серое состояние кнопки даёт привязка команды, и она спрашивает предикат — предикат
    /// должен отражать поле, а не висеть всегда включённым.
    /// </remarks>
    [Fact]
    public void ОчисткаПоиска_НаПустомПоле_Гасится()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        Assert.False(viewModel.HasHotkeySearch);
        Assert.False(viewModel.ClearHotkeySearchCommand.CanExecute(null));

        viewModel.HotkeySearch = "Экспорт";

        Assert.True(viewModel.HasHotkeySearch);
        Assert.True(viewModel.ClearHotkeySearchCommand.CanExecute(null));
    }

    /// <summary>
    /// Подсказка в поле поиска шрифта видна, пока поле пустое, и прячется после ввода.
    /// </summary>
    /// <remarks>
    /// Без подсказки поле рядом с подписью «Шрифт» выглядит пустым белым прямоугольником,
    /// и что в нём искать — приходится догадываться.
    /// </remarks>
    [Fact]
    public void ПодсказкаПоискаШрифта_ВиднаНаПустомПоле()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        Assert.True(viewModel.HasFontSearchPlaceholder);

        viewModel.FontSearch = "Segoe";

        Assert.False(viewModel.HasFontSearchPlaceholder);
    }

    /// <summary>Крестик очистки поиска шрифта: появляется на непустом поле и чистит его.</summary>
    [Fact]
    public void ОчисткаПоискаШрифта_ЧиститПоле()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        Assert.False(viewModel.HasFontSearch);
        Assert.False(viewModel.ClearFontSearchCommand.CanExecute(null));

        viewModel.FontSearch = "Segoe";

        Assert.True(viewModel.HasFontSearch);
        Assert.True(viewModel.ClearFontSearchCommand.CanExecute(null));

        viewModel.ClearFontSearchCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.FontSearch);
    }

    /// <summary>Тема открывается на сохранённой: пустое поле снесло бы настройку первым же сохранением.</summary>
    [Fact]
    public void Тема_ОткрываетсяНаСохранённой()    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
        {
            Theme = ThemeRules.Dark,
        });

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.Equal(ThemeRules.Dark, viewModel.SelectedTheme.Value);
        Assert.Equal(ThemeRules.Dark, viewModel.SavedTheme);
    }

    /// <summary>Темы в файле нет — умолчание, и откат возвращает то же: окно открылось не зная выбора.</summary>
    [Fact]
    public void Тема_БезЗаписи_Умолчание()
    {
        using var settings = new TemporarySettings();

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.Equal(ThemeRules.Default, viewModel.SelectedTheme.Value);
        Assert.Equal(ThemeRules.Default, viewModel.SavedTheme);
    }

    /// <summary>Выбранная тема пишется тем же «Сохранить», что и остальные настройки.</summary>
    [Fact]
    public void Save_ВыбраннаяТема_ПишетсяВФайл()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store)
        {
            SelectedTheme = new ThemeOption(ThemeRules.Dark, "Тёмная"),
        };

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(ThemeRules.Dark, new AppSettingsStore(settings.FilePath).Load().Theme);
    }

    /// <summary>Смена темы поднимает уведомление: окно по нему перекрашивает приложение.</summary>
    [Fact]
    public void Тема_Смена_Уведомляет()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var notified = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        viewModel.SelectedTheme = new ThemeOption(ThemeRules.Dark, "Тёмная");

        Assert.Contains(nameof(SettingsViewModel.SelectedTheme), notified);
    }

    /// <summary>Строки размеров: по одной на каждое окно приложения.</summary>
    [Fact]
    public void Размеры_БезЗаписиВФайле_ИзКаталога()
    {
        using var settings = new TemporarySettings();

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.Equal(WindowSizeCatalog.Entries.Count, viewModel.WindowSizeRows.Count);

        var main = viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main);
        var entry = WindowSizeCatalog.Find(WindowSizeCatalog.Main)!;

        Assert.Equal(entry.Width, main.Width);
        Assert.Equal(entry.Height!.Value, main.Height);
    }

    /// <summary>Запись в файле главнее каталога: пользователь задал размер — он и показан.</summary>
    [Fact]
    public void Размеры_ИзФайла_Читаются()
    {
        using var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
        {
            WindowSizes = new Dictionary<string, WindowSize>
            {
                [WindowSizeCatalog.Main] = new WindowSize { Width = 1000, Height = 700 },
            },
        });

        var viewModel = new SettingsViewModel(settings.Store);

        var main = viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main);

        Assert.Equal(1000, main.Width);
        Assert.Equal(700, main.Height);
    }

    /// <summary>Сохранение пишет размеры всех окон: секция читается целиком.</summary>
    [Fact]
    public void Save_ПишетРазмерыОконВФайл()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main).Width = 1000;

        viewModel.SaveCommand.Execute(null);

        var loaded = new AppSettingsStore(settings.FilePath).Load();

        Assert.Equal(1000, loaded.WindowSizes[WindowSizeCatalog.Main].Width);
        Assert.Equal(WindowSizeCatalog.Entries.Count, loaded.WindowSizes.Count);
    }

    /// <summary>
    /// Ширина меньше минимума окна — объяснение и гаснущая «Сохранить»: размер, который WPF
    /// всё равно не примет, записывать незачем.
    /// </summary>
    [Fact]
    public void Размер_МеньшеМинимума_СохранитьГасится_ИОбъяснениеПоказано()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var main = viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main);

        main.Width = 10;

        Assert.True(viewModel.HasSizeError);
        Assert.Contains("Журнал тренировок", viewModel.SizeError);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// Перетаскивание меняет число и вписывает его в границы окна: правило одно на оба способа
    /// правки, иначе перетаскивание и ввод руками разошлись бы в том, что считается годным.
    /// </summary>
    [Fact]
    public void Перетаскивание_ШиринаМеняется_ИВписываетсяВГраницы()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var main = viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main);
        var start = main.Width;

        main.WidthCommand.Execute(start + 100);

        Assert.Equal(start + 100, main.Width);

        main.WidthCommand.Execute(1);

        Assert.Equal(main.MinimumWidth, main.Width);
    }

    /// <summary>У окна правки упражнения высоты нет: она следует за содержимым, и задать её нельзя.</summary>
    [Fact]
    public void Размеры_ОкноПравкиУпражнения_ВысотыПоСодержимому()
    {
        using var settings = new TemporarySettings();

        var viewModel = new SettingsViewModel(settings.Store);

        var row = viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.EditExercise);

        Assert.False(row.HasHeight);
        Assert.True(row.IsContentHeight);
    }

    /// <summary>
    /// Правка размера поднимает событие: окно по нему применяет предпросмотр к открытым окнам.
    /// </summary>
    [Fact]
    public void Размеры_Правка_ПоднимаетСобытие()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var raised = 0;
        viewModel.WindowSizesChanged += () => raised++;

        viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main).Width = 1000;

        Assert.Equal(1, raised);
    }

    /// <summary>Предпросмотр берёт значение строки: годное значение применяется сразу.</summary>
    [Fact]
    public void PreviewWindowSizes_ГодноеЗначение_БерётсяИзСтроки()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main).Width = 1000;

        Assert.Equal(1000, viewModel.PreviewWindowSizes()[WindowSizeCatalog.Main].Width);
    }

    /// <summary>
    /// Негодное значение не применяется: прежнее, пока число не станет допустимым. Иначе окно
    /// дёргалось бы на каждой цифре.
    /// </summary>
    [Fact]
    public void PreviewWindowSizes_НегодноеЗначение_БерётсяПрежнее()
    {
        using var settings = new TemporarySettings();
        var viewModel = new SettingsViewModel(settings.Store);

        var entry = WindowSizeCatalog.Find(WindowSizeCatalog.Main)!;

        viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main).Width = 10;

        Assert.Equal(entry.Width, viewModel.PreviewWindowSizes()[WindowSizeCatalog.Main].Width);
    }

    /// <summary>Срез размеров заполнен при открытии окна: откату есть откуда взять.</summary>
    [Fact]
    public void SavedWindowSizes_ЗаполненПриОткрытии()
    {
        using var settings = new TemporarySettings();

        var viewModel = new SettingsViewModel(settings.Store);

        Assert.Equal(WindowSizeCatalog.Entries.Count, viewModel.SavedWindowSizes.Count);
        Assert.Equal(
            WindowSizeCatalog.Find(WindowSizeCatalog.Main)!.Width,
            viewModel.SavedWindowSizes[WindowSizeCatalog.Main].Width);
    }

    /// <summary>Строка поиска по id: каталог у теста один на все группы.</summary>
    private static HotkeyRowViewModel HotkeyRow(SettingsViewModel viewModel, string id) =>
        viewModel.HotkeyGroups.SelectMany(group => group.Rows).Single(row => row.Id == id);

    /// <summary>Строки перечня без заголовков: что видно пользователю как строки.</summary>
    private static List<HotkeyRowViewModel> ListingRows(SettingsViewModel viewModel) =>
        viewModel.HotkeyListing.OfType<HotkeyRowViewModel>().ToList();

    /// <summary>Заголовки окон в перечне: без них не понять, чьи строки показаны.</summary>
    private static List<string> ListingSections(SettingsViewModel viewModel) =>
        viewModel.HotkeyListing.OfType<HotkeySectionViewModel>().Select(section => section.WindowName).ToList();
}
