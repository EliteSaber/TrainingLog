using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Settings;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

/// <summary>Окно параметров: число дней, адрес базы и горячие клавиши.</summary>
/// <remarks>
/// Настройки живут в файле рядом с программой, это окно — их редактор. Новые директория и
/// наименование базы применяются при следующем запуске: соединение строится один раз, при старте
/// приложения. Диалоги выбора адресов адресуются окном, а не моделью — по тем же причинам,
/// по которым здесь нет <c>MessageBox</c>.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsStore _store;

    // Поле под SelectedFont: свойство написано вручную, см. комментарий у него.
    private FontCatalog.FontFamilyOption _selectedFont;

    // Поле под SelectedTheme: свойство тоже написано вручную, см. комментарий у него.
    private ThemeOption _selectedTheme;

    /// <param name="store">Файл настроек.</param>
    /// <param name="fonts">Каталог шрифтов системы; без него (тесты) в списке только «по цепочке».</param>
    public SettingsViewModel(AppSettingsStore store, FontCatalog? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;

        // Из файла, а не из умолчания: пустое поле означало бы потерю настройки первым же
        // нажатием «Сохранить». Наименование переводится правилом: в файле настроек имя файла с
        // расширением, а поле показывает его без хвостового .db.
        var settings = store.Load();
        DaysCount = settings.DaysCount;
        DatabaseDirectory = settings.DatabaseDirectory;
        DatabaseName = DatabaseNameRules.ForDisplay(settings.DatabaseName);

        // Пустая запись в файле — «по умолчанию» (Segoe UI), а не вторая пустота: поле и откат
        // показывают одной честно: что и применял бы WPF-слой.
        var fontFamily = ResolveFontName(settings.FontFamily);
        FontOptions = BuildFontOptions(fonts);

        // Пункта из списка может не быть, если файл правили руками или шрифт удалили: выбор
        // остаётся с тем же именем, Apply сам сведёт его к умолчанию. Null у ComboBox никогда
        // не «модельный»: свойство пишут только непустыми значениями.
        _selectedFont = FontOptions.FirstOrDefault(option =>
                            string.Equals(option.Name, fontFamily, StringComparison.OrdinalIgnoreCase))
                        ?? new FontCatalog.FontFamilyOption(fontFamily);
        _selectedScale = FontRules.IsScaleValid(settings.FontSizePercent)
            ? settings.FontSizePercent
            : AppSettings.DefaultFontSizePercent;

        // Срез файла на момент открытия: окно по нему откатывает предпросмотр, когда закрылось
        // без сохранения. Перечитывать файл на закрытии незачем: правки в окно извне не приходят.
        SavedFontName = fontFamily;
        SavedFontSizePercent = _selectedScale;

        // Тема — тем же порядком: из файла, срез файла для отката, а предпросмотр перекрашивает
        // окна сразу. Канон задаёт правило (ThemeRules), подписи — список пунктов ниже.
        var theme = ThemeRules.Normalize(settings.Theme);

        ThemeOptions = BuildThemeOptions();
        _selectedTheme = ThemeOptions.FirstOrDefault(option => option.Value == theme)
            ?? ThemeOptions.First(option => option.Value == ThemeRules.Default);
        SavedTheme = theme;

        HotkeyGroups = BuildHotkeyGroups(settings.Hotkeys);

        // Кнопки-переходы строятся по группам, а выбранной сразу становится первая — «Все окна».
        // Сеттер сам расставит признаки и пересоберёт перечень.
        HotkeyChips = BuildHotkeyChips(HotkeyGroups);
        SelectedChip = HotkeyChips[0];

        RecalculateHotkeyStatus();
    }

    /// <summary>Сколько дней показывать.</summary>
    /// <remarks>
    /// Проверяется то же <see cref="DaysCountRules"/>, что и у «Применить» в главном окне: одно
    /// число в двух формах не должно иметь два мнения о годности.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _daysCount;

    /// <summary>Где лежит база, включая диск: путь к каталогу без имени файла.</summary>
    /// <remarks>
    /// Подставляется и диалогом выбора папки, и каталогом выбранного файла базы — тогда оба
    /// поля согласованы между собой и с диском.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _databaseDirectory = string.Empty;

    /// <summary>Как называется файл базы без каталога, диска и расширения.</summary>
    /// <remarks>
    /// Расширение <see cref="DatabaseNameRules.Extension"/> дописывается при сохранении всегда и
    /// в поле не показывается: пользователь видит и вводит только имя. Впечатанный вручную хвост
    /// <c>.db</c> правилом снимается, чтобы при сохранении не удвоиться.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _databaseName = string.Empty;

    /// <summary>Каталог выбора шрифта: все установленные семейства.</summary>
    /// <remarks>
    /// Строится один раз в конструкторе: переустановка шрифта живому окну не нужна, а пересборка
    /// сотен пунктов на каждом показе вкладки была бы лишней работой.
    /// </remarks>
    public IReadOnlyList<FontCatalog.FontFamilyOption> FontOptions { get; }

    /// <summary>Поисковая строка списка шрифтов.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredFonts))]
    private string _fontSearch = string.Empty;

    /// <summary>Поле поиска шрифта не пусто: крестик очистки показан.</summary>
    /// <remarks>
    /// Факт один на двоих: и показ крестика, и его готовность читают его — как у поиска
    /// горячих клавиш.
    /// </remarks>
    public bool HasFontSearch => !string.IsNullOrEmpty(FontSearch);

    /// <summary>Подсказка в поле поиска шрифта: чем поле занято.</summary>
    /// <remarks>
    /// Без неё поле рядом с подписью «Шрифт» выглядит пустым белым прямоугольником, и что
    /// в нём искать — догадываться.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFontSearchPlaceholder))]
    private string _fontSearchPlaceholder = "Поиск по имени шрифта";

    /// <summary>Поле поиска шрифта пусто: показать подсказку.</summary>
    /// <remarks>
    /// Отдельный признак от <see cref="HasFontSearch"/>: подсказка видна, когда поле пустое,
    /// а крестик — когда поле непустое. Показывать их по одному признаку нельзя.
    /// </remarks>
    public bool HasFontSearchPlaceholder => FontSearchPlaceholder.Length > 0 && !HasFontSearch;

    /// <summary>Список шрифтов пуст: показать «шрифтов не найдено».</summary>
    public bool IsFontListEmpty => FilteredFonts.Count == 0;

    /// <summary>Список шрифтов под поиск.</summary>
    /// <remarks>
    /// Вычисляемым, а не обслуживаемой коллекцией: пересчёт сотен пунктов дёшев, а второй
    /// коллекцией пришлось бы синхронизировать обе половины состояния.
    /// </remarks>
    public IReadOnlyList<FontCatalog.FontFamilyOption> FilteredFonts =>
        FontCatalog.Filter(FontOptions, FontSearch);

    partial void OnFontSearchChanged(string value)
    {
        OnPropertyChanged(nameof(HasFontSearch));
        OnPropertyChanged(nameof(HasFontSearchPlaceholder));
        OnPropertyChanged(nameof(IsFontListEmpty));

        // Команда очистки уведомление свойства не слушает — её будить надо отдельно.
        ClearFontSearchCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Очистка поиска шрифта: поле пусто, список снова полный.</summary>
    [RelayCommand(CanExecute = nameof(CanClearFontSearch))]
    private void ClearFontSearch()
    {
        // Проверка повторяется в теле команды: Execute предикат не спрашивает (ловушка 46).
        if (!CanClearFontSearch())
        {
            return;
        }

        FontSearch = string.Empty;
    }

    /// <summary>Очищать есть что: пустое поле очищать незачем, и кнопка гаснет.</summary>
    private bool CanClearFontSearch() => HasFontSearch;

    /// <summary>Выбранный пункт списка шрифтов.</summary>
    /// <remarks>
    /// Свойство написано вручную, а не <c>[ObservableProperty]</c>: список под поиск убирает
    /// выбранный пункт, и список присылает null. Игнорирование null держит выбор в модели,
    /// пока список на экране пуст, — очистка поиска возвращает пункт на место, и повторное
    /// выделение не теряет сохранённого значения.
    /// </remarks>
    public FontCatalog.FontFamilyOption SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (value is not null)
            {
                SetProperty(ref _selectedFont, value);
            }
        }
    }

    /// <summary>Размер шрифта в процентах: один из пресетов.</summary>
    [ObservableProperty]
    private int _selectedScale = FontRules.DefaultScalePercent;

    /// <summary>Пресеты масштаба: значения внутри границ <see cref="FontRules"/>, и ComboBox им верит заранее.</summary>
    public static IReadOnlyList<int> ScaleOptions { get; } = [80, 90, 100, 110, 125, 150];

    /// <summary>Гарнитура, записанная в файле на момент открытия окна.</summary>
    /// <remarks>
    /// Отдаётся окну отката: выход без сохранения возвращает предпросмотр к этому срезу.
    /// Читающим свойством, а не полем-значением окна: что было сохранено, знает модель,
    /// окно знает лишь, когда откатывать.
    /// </remarks>
    public string SavedFontName { get; }

    /// <summary>Масштаб кегля, записанный в файле на момент открытия окна.</summary>
    public int SavedFontSizePercent { get; }

    /// <summary>Пункты выбора темы: значение и подпись.</summary>
    /// <remarks>
    /// Строятся один раз в конструкторе: список из трёх пунктов не меняется, а пересобирать его
    /// при каждом показе вкладки незачем.
    /// </remarks>
    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    /// <summary>Выбранная тема оформления.</summary>
    /// <remarks>
    /// Свойство написано вручную, а не <c>[ObservableProperty]</c>: ComboBox присылает null, когда
    /// выбранный пункт пропадает из списка, и null в модели означал бы «темы нет». Тот же приём,
    /// что у выбранного шрифта.
    /// </remarks>
    public ThemeOption SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (value is not null)
            {
                SetProperty(ref _selectedTheme, value);
            }
        }
    }

    /// <summary>Тема, записанная в файл на момент открытия окна.</summary>
    /// <remarks>
    /// Отдаётся окну отката: выход без сохранения возвращает предпросмотр к этому срезу.
    /// </remarks>
    public string SavedTheme { get; }

    /// <summary>Подсказка об отказе по вводу.</summary>
    public HintState Hint { get; } = new();

    /// <summary>Группы каталога горячих клавиш: порядок групп и строк = порядок вкладки.</summary>
    /// <remarks>
    /// Групповая структура нужна расчётам (конфликт и назначаемый словарь обходят её); списку
    /// вкладки она не подходит — см. <see cref="HotkeyListing"/>.
    /// </remarks>
    public IReadOnlyList<HotkeyGroupViewModel> HotkeyGroups { get; }

    /// <summary>Кнопки-переходы по окнам: «Все окна» и по одной на каждое окно каталога.</summary>
    /// <remarks>
    /// Кнопки перечисляют окна, а не группы: пользователь знает окно, а группу видит только
    /// заголовком. Порядок кнопок — порядок каталога.
    /// </remarks>
    public IReadOnlyList<HotkeyChipViewModel> HotkeyChips { get; }

    /// <summary>Выбранная кнопка-переход: перечень показывает назначения этого окна.</summary>
    [ObservableProperty]
    private HotkeyChipViewModel? _selectedChip;

    /// <summary>Окно, чьи назначения показывает перечень; null — показываются все окна.</summary>
    public HotkeyGroupViewModel? SelectedHotkeyGroup => SelectedChip?.Group;

    /// <summary>Поисковая строка перечня: по имени действия или имени окна.</summary>
    /// <remarks>
    /// Пустая строка означает, что показан весь перечень.
    /// </remarks>
    [ObservableProperty]
    private string _hotkeySearch = string.Empty;

    /// <summary>Показывать только те назначения, что отличаются от умолчания каталога.</summary>
    [ObservableProperty]
    private bool _hotkeyChangedOnly;

    /// <summary>Поле поиска не пусто: крестик очистки показан.</summary>
    /// <remarks>
    /// Факт один на двоих: и показ крестика, и его готовность читают его. Отдельный признак
    /// у команды разошёлся бы с полем при первом же изменении.
    /// </remarks>
    public bool HasHotkeySearch => !string.IsNullOrEmpty(HotkeySearch);

    /// <summary>Плоский список вкладки: за заголовком окна идут его строки — источник виртуализации.</summary>
    /// <remarks>
    /// Группа-в-группе не виртуализируется никогда: высота внутренней панели не ограничена,
    /// и она построила бы все ~60 строк разом — это была бы цена первого показа вкладки.
    /// Плоский перечень с разделителями-заголовками позволяет VirtualizingStackPanel строить
    /// только видимые строки.
    ///
    /// Коллекция обслуживаемая, а не вычисляемая: перечень пересобирается при смене поиска,
    /// окна и галочки, и пересборка через <see cref="VisibleSync"/> не пересоздаёт контейнеры
    /// уцелевших строк — иначе правка комбинации роняла бы фокус из поля захвата.
    /// </remarks>
    public ObservableCollection<object> HotkeyListing { get; } = [];

    /// <summary>Перечень пуст: показать «ничего не найдено».</summary>
    public bool IsHotkeyListingEmpty => HotkeyListing.Count == 0;

    /// <summary>Объяснение конфликта назначений, а при пустом значении — конфликта нет.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHotkeyConflict))]
    private string _hotkeyConflict = string.Empty;

    /// <summary>Есть ли конфликт назначений: он гасит «Сохранить».</summary>
    public bool HasHotkeyConflict => !string.IsNullOrEmpty(HotkeyConflict);

    /// <summary>Настройки записаны. Окно выставляет <c>DialogResult</c> по нему.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Объяснение отказа записи, а при пустом значении — ничего.</summary>
    /// <remarks>
    /// Живёт до успешного сохранения, а не пять секунд в подсказке: настройка не записалась, и
    /// объяснение обязано дождаться, пока им займутся.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    /// <summary>Показывать ли объяснение отказа.</summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    private bool CanSave() =>
        DaysCountRules.IsValid(DaysCount)
        && !string.IsNullOrWhiteSpace(DatabaseDirectory)
        && !string.IsNullOrWhiteSpace(DatabaseName)
        && !HasHotkeyConflict;

    /// <summary>
    /// Записывает количество дней, адрес базы и горячие клавиши в файл настроек.
    /// </summary>
    /// <remarks>
    /// Проверка значений повторяется в теле команды, а не только в предикате: предикат гасит
    /// кнопку, но <c>Execute</c> его не спрашивает и зовёт метод (ловушка 46). Конфликт
    /// пересчитывается здесь же, а не читается из прошлого подсчёта: строки вкладки правятся
    /// напрямую, и кэш отстал бы от них.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        RecalculateHotkeyStatus();

        if (!CanSave())
        {
            return;
        }

        if (!_store.Save(new AppSettings
            {
                DaysCount = DaysCount,
                DatabaseDirectory = DatabaseDirectory,
                // Расширение живёт в файле, а не в поле: настоящим именем файла владеет файл настроек.
                DatabaseName = DatabaseNameRules.ToFileName(DatabaseName),
                FontFamily = SelectedFont.Name,
                FontSizePercent = SelectedScale,
                Theme = SelectedTheme.Value,
                Hotkeys = AssignedHotkeys(),
            }))
        {
            Status = "Не удалось записать файл настроек. Программа стоит в папке без права записи?";

            return;
        }

        Status = string.Empty;
        Accepted = true;

        // Приватный сеттер не даёт сгенерировать уведомление автоматически, а окно ждёт его,
        // чтобы выставить DialogResult.
        OnPropertyChanged(nameof(Accepted));
    }

    /// <summary>Назначения, отличающиеся от умолчания: словарь для секции файла.</summary>
    /// <remarks>
    /// Умолчание в файл не пишется: «убрать назначение» равнозначно возврату к умолчанию, и
    /// отсутствие записи в файле уже означает «умолчание» — интерпретировать оба механизма
    /// дублированием не нужно, иначе снятое и умолчание разъехались бы.
    /// Захваченная строка пропускается с флагом <see cref="HotkeyRowViewModel.IsCapturing"/>:
    /// «Сохранить» может быть нажат прямо во время набора, когда поле показывает приглашение
    /// или промежуточный набор «Ctrl+…», — такой текст назначением быть не может, и в файл
    /// ему дорога вела бы как мёртвому значению.
    /// </remarks>
    private Dictionary<string, string> AssignedHotkeys() =>
        HotkeyGroups
            .SelectMany(group => group.Rows)
            .Where(row => !row.IsCapturing
                && row.GestureText.Length > 0
                && row.GestureText != row.DefaultGesture)
            .ToDictionary(row => row.Id, row => row.GestureText);

    /// <summary>Служба, которую вызывает строка, когда её значение изменилось.</summary>
    /// <remarks>
    /// Пересчитывает конфликт и <see cref="SaveCommand"/>: правки строк приходят методами
    /// строки, а не через поля модели, поэтому уведомление команды здесь.
    /// </remarks>
    internal void OnHotkeyChanged()
    {
        RecalculateHotkeyStatus();

        SaveCommand.NotifyCanExecuteChanged();

        // Перечень трогаем только при включённом «только изменённые»: там состав перечня зависит
        // от комбинации, а в обычном режиме правка ничего в перечне не меняет — а пересборка
        // уничтожила бы контейнеры строк и унесла бы фокус из поля захвата.
        if (HotkeyChangedOnly)
        {
            RebuildHotkeyListing();
        }
    }

    /// <summary>Переход к окну: перечень показывает только его назначения.</summary>
    [RelayCommand]
    private void SelectChip(HotkeyChipViewModel chip) => SelectedChip = chip;

    /// <summary>Пересобирает перечень под текущий поиск, окно и галочку.</summary>
    /// <remarks>
    /// Пересборка идёт через <see cref="VisibleSync"/>: коллекция приводится к целевому составу
    /// без <c>Reset</c>, и контейнеры уцелевших строк переиспользуются. Полная пересборка
    /// пересоздала бы контейнеры всех строк, и фокус из поля захвата улетел бы на каждой
    /// правке комбинации.
    /// </remarks>
    private void RebuildHotkeyListing()
    {
        var target = HotkeyFilter.BuildListing(HotkeyGroups, HotkeySearch, HotkeyChangedOnly, SelectedHotkeyGroup);

        VisibleSync.Sync(HotkeyListing, target);

        OnPropertyChanged(nameof(IsHotkeyListingEmpty));
    }

    partial void OnHotkeySearchChanged(string value)
    {        RebuildHotkeyListing();

        OnPropertyChanged(nameof(HasHotkeySearch));

        // Команда очистки уведомление свойства не слушает — её будить надо отдельно.
        ClearHotkeySearchCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Очистка поиска: поле пусто, перечень снова полный.</summary>
    /// <remarks>
    /// Команда, а не присваивание в разметке: кнопка должна гаснуть на пустом поле, а серое
    /// состояние WPF берёт из <c>CanExecute</c> привязанной команды.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanClearHotkeySearch))]
    private void ClearHotkeySearch()
    {
        // Проверка повторяется в теле команды: Execute предикат не спрашивает (ловушка 46).
        if (!CanClearHotkeySearch())
        {
            return;
        }

        HotkeySearch = string.Empty;
    }

    /// <summary>Очищать есть что: пустое поле очищать незачем, и кнопка гаснет.</summary>
    private bool CanClearHotkeySearch() => HasHotkeySearch;

    partial void OnHotkeyChangedOnlyChanged(bool value) => RebuildHotkeyListing();

    partial void OnSelectedChipChanged(HotkeyChipViewModel? value)
    {
        // Признак «выбрана» живёт на кнопке: разметка помечает ею кнопку, и держать второе
        // состояние в модели рядом с выбранной кнопкой незачем — расходиться им нельзя.
        foreach (var chip in HotkeyChips)
        {
            chip.IsSelected = ReferenceEquals(chip, value);
        }

        RebuildHotkeyListing();
    }

    /// <summary>Ищет первую пару строк с одинаковой комбинацией внутри окна и ставит сообщение о ней.</summary>
    /// <remarks>
    /// Сравнение по <see cref="HotkeyRowViewModel.GestureText"/> — строки хранят значения
    /// уже в canonical виде, поэтому дубликат здесь совпадает с конфликтом в файле. Окна
    /// сравниваются по отдельности: KeyBinding живёт в окне, и одна комбинация в двух окнах
    /// выполняет два разных действия, а не спорит за одно. Сообщение живёт до исправления,
    /// а не пять секунд: сохранение невыполнимо, пока конфликт не снят.
    /// </remarks>
    private void RecalculateHotkeyStatus()
    {
        foreach (var group in HotkeyGroups)
        {
            Dictionary<string, HotkeyRowViewModel> occupied = [];

            foreach (var row in group.Rows)
            {
                var gesture = row.GestureText;

                // Захваченная строка показывает приглашение или промежуточный набор — не решение,
                // и в конфликты ему дорога закрыта так же, как в сохранение.
                if (row.IsCapturing)
                {
                    continue;
                }

                if (gesture.Length > 0)
                {
                    if (occupied.TryGetValue(gesture, out var other))
                    {
                        // Окно названо рядом с действием: конфликт считается по всем окнам, и при
                        // включённом поиске или выбранном окне источник может быть не на экране —
                        // без имени окна объяснение было бы половиной ответа.
                        HotkeyConflict =
                            $"Комбинация «{gesture}» уже назначена действию «{other.Name}» в окне «{group.WindowName}».";

                        return;
                    }

                    occupied[gesture] = row;
                }
            }
        }

        HotkeyConflict = string.Empty;
    }

    /// <summary>Все установленные семейства; без каталога (тесты) — только гарнитура по умолчанию.</summary>
    private static List<FontCatalog.FontFamilyOption> BuildFontOptions(FontCatalog? fonts) =>
        fonts is null
            ? [new FontCatalog.FontFamilyOption(FontRules.DefaultFontFamily)]
            : [.. FontCatalog.Families()];

    /// <summary>Пункты выбора темы: подписи на русском, значения канонические.</summary>
    /// <remarks>
    /// Порядок пунктов — порядок вкладки, а не порядок правила: пользователь выбирает сверху
    /// вниз, и «системная» стоит последней, как запасной вариант.
    /// </remarks>
    private static IReadOnlyList<ThemeOption> BuildThemeOptions() =>
    [
        new ThemeOption(ThemeRules.Light, "Светлая"),
        new ThemeOption(ThemeRules.Dark, "Тёмная"),
        new ThemeOption(ThemeRules.System, "Системная"),
    ];

    /// <summary>Имя из файла: непустое — как есть, пустое — гарнитура по умолчанию.</summary>
    private static string ResolveFontName(string? font) =>
        FontRules.NormalizeName(font) is { Length: > 0 } name
            ? name
            : FontRules.DefaultFontFamily;

    /// <summary>Строит строки каталога: значения показываются как действующие — назначение или умолчание.</summary>
    private List<HotkeyGroupViewModel> BuildHotkeyGroups(Dictionary<string, string> assigned)
    {
        List<HotkeyGroupViewModel> groups = [];

        foreach (var catalogGroup in HotkeyCatalog.Groups)
        {
            List<HotkeyRowViewModel> rows = [];

            foreach (var action in catalogGroup.Actions)
            {
                rows.Add(new HotkeyRowViewModel(
                    this,
                    action,
                    assigned.TryGetValue(action.Id, out var text) && HotkeyRules.IsValid(text)
                        ? HotkeyRules.Normalize(text)
                        : action.DefaultGesture ?? string.Empty));
            }

            groups.Add(new HotkeyGroupViewModel(catalogGroup.WindowName, catalogGroup.ShortName, rows));
        }

        return groups;
    }

    /// <summary>Кнопки-переходы: «Все окна» и по одной на каждое окно.</summary>
    /// <remarks>
    /// «Все окна» идёт первым и ссылается на null-группу: так «показать всё» — обычный
    /// переход, а не отдельное состояние модели с отдельной командой.
    /// </remarks>
    private static List<HotkeyChipViewModel> BuildHotkeyChips(IReadOnlyList<HotkeyGroupViewModel> groups)
    {
        List<HotkeyChipViewModel> chips = [new HotkeyChipViewModel("Все окна", null)];

        foreach (var group in groups)
        {
            chips.Add(new HotkeyChipViewModel(group.ShortName, group));
        }

        return chips;
    }
}

/// <summary>Пункт выбора темы: каноническое значение и подпись для окна.</summary>
/// <remarks>
/// Отдельный тип, а не строка: в списке показывается подпись, а в файл пишется значение, и одно
/// поле не может быть и тем, и другим. Значение — канон правила (ThemeRules), подпись — текст
/// окна.
/// </remarks>
public sealed record ThemeOption(string Value, string Display);

/// <summary>Группа вкладки горячих клавиш: назначения одного окна.</summary>
public sealed class HotkeyGroupViewModel(
    string windowName,
    string shortName,
    IReadOnlyList<HotkeyRowViewModel> rows)
{
    public string WindowName { get; } = windowName;

    /// <summary>Короткое имя окна — подпись кнопки-перехода.</summary>
    public string ShortName { get; } = shortName;

    public IReadOnlyList<HotkeyRowViewModel> Rows { get; } = rows;

    /// <summary>Заголовок окна в плоском перечне.</summary>
    /// <remarks>
    /// Живёт в группе, а не строится на каждый пересбор перечня: пересборка идёт через
    /// <see cref="VisibleSync"/>, и новый заголовок каждый раз считался бы ушедшим элементом —
    /// контейнер заголовка пересоздавался бы направо.
    /// </remarks>
    public HotkeySectionViewModel Section { get; } = new(windowName);
}

/// <summary>Кнопка-переход к окну вкладки горячих клавиш.</summary>
/// <remarks>
/// Отдельный тип, а не строка: у кнопки есть признак «выбрана», и он должен уметь измениться
/// сам — иначе разметка и модель держали бы одно и то же дважды.
/// </remarks>
public sealed partial class HotkeyChipViewModel : ObservableObject
{
    /// <param name="label">Подпись кнопки.</param>
    /// <param name="group">Окно, к которому кнопка ведёт; null — «Все окна».</param>
    public HotkeyChipViewModel(string label, HotkeyGroupViewModel? group)
    {
        Label = label;
        Group = group;

        // Подсказка называет окно полным именем: короткая подпись кнопки («Журнал») не говорит,
        // что это именно «Главное окно «Журнал тренировок»».
        Tooltip = group is null ? "Показать назначения всех окон" : group.WindowName;
    }

    /// <summary>Подпись кнопки: короткое имя окна или «Все окна».</summary>
    public string Label { get; }

    /// <summary>Подсказка кнопки: полное имя окна.</summary>
    public string Tooltip { get; }

    /// <summary>Окно, к которому ведёт кнопка; null — показываются все окна.</summary>
    public HotkeyGroupViewModel? Group { get; }

    /// <summary>Выбрана ли кнопка: её рамка рисуется акцентом.</summary>
    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Разделитель плоского списка вкладки: прежний заголовок группы, а строку класс не трогает.</summary>
/// <remarks>
/// Отдельный тип, а не пометка в <see cref="HotkeyRowViewModel"/>: разметка выбирает шаблон
/// строки по типу элемента, и перемешивать заголовок со строками было бы проверками по признаку.
/// </remarks>
public sealed class HotkeySectionViewModel(string windowName)
{
    public string WindowName { get; } = windowName;
}

/// <summary>Строка вкладки горячих клавиш: одно назначение и его редактор захвата.</summary>
public sealed partial class HotkeyRowViewModel : ObservableObject
{
    private readonly SettingsViewModel _owner;

    // Значение до начала захвата: Esc возвращает именно его, а не пустое поле.
    private string _savedGesture = string.Empty;

    // Поле под рукописным свойством GestureText — см. комментарий у него.
    private string _gestureText;

    public HotkeyRowViewModel(SettingsViewModel owner, HotkeyAction action, string gesture)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(action);

        _owner = owner;

        Id = action.Id;
        Name = action.Name;
        DefaultGesture = action.DefaultGesture;
        Marker = action.Marker;
        _gestureText = gesture;
        ClearCommand = new RelayCommand(Clear);
    }

    /// <summary>Id каталога: он же ключ в файле настроек.</summary>
    public string Id { get; }

    /// <summary>Имя действия.</summary>
    public string Name { get; }

    /// <summary>Умолчание каталога или пустая строка, когда его нет.</summary>
    public string? DefaultGesture { get; }

    /// <summary>Пометка «строка»/«упражнение»: у Window-действий пустая.</summary>
    public string Marker { get; }

    /// <summary>Текст строки вкладки: имя плюс пометка в скобках, когда она есть.</summary>
    /// <remarks>
    /// Вычисляемым, а не двумя TextBlock'ами: пустая пометка у Window-действий оставила бы
    /// парные скобки на экране, и развязка Visibility на этом заработала бы только через
    /// конвертер.
    /// </remarks>
    public string DisplayText => Marker.Length > 0 ? $"{Name} ({Marker})" : Name;

    /// <summary>Действующая комбинация в canonical виде или пустая строка.</summary>
    /// <remarks>
    /// Открытый сеттер нужен захвату в окне: он пишет в эту же строку и промежуточный набор
    /// «Ctrl+…», и подтверждённое значение. Свойство написано руками, а не
    /// <c>[ObservableProperty]</c>: у сгенерированного из поля сеттер приватный, а окну
    /// захвата надо писать в строку извне модели.
    /// </remarks>
    public string GestureText
    {
        get => _gestureText;

        set => SetProperty(ref _gestureText, value);
    }

    /// <summary>Команда «Убрать»: возврат к умолчанию или к пустой строке.</summary>
    public IRelayCommand ClearCommand { get; }

    /// <summary>Идёт ли захват: поле показывает приглашение и подсветку, пока это подмножество так.</summary>
    /// <remarks>
    /// Признак живёт у строки, а не в окне: и подсветка в разметке, и защита сохранения
    /// (см. <see cref="SettingsViewModel.AssignedHotkeys"/>) смотрят одно и то же состояние,
    /// а не догадываются по тексту поля. Текст поля похожим носителем быть не может:
    /// и приглашение, и промежуточный набор «Ctrl+…» там «похожи» на настоящую комбинацию.
    /// </remarks>
    [ObservableProperty]
    private bool _isCapturing;

    /// <summary>Начало захвата: запоминает действующее значение для Esc.</summary>
    public void BeginCapture()
    {
        _savedGesture = GestureText;
        IsCapturing = true;
    }

    /// <summary>Промежуточный набор модификаторов: «Ctrl+…» без подтверждения.</summary>
    /// <remarks>
    /// Конфликт не пересчитывается: набор ещё не решение, и мигание сообщения на каждом
    /// модификаторе работало бы против пользователя. Окончательный подсчёт — в подтверждении.
    /// </remarks>
    public void CapturePreview(string text) => GestureText = text;

    /// <summary>Подтверждение комбинации: канонизирует и пересчитывает конфликт.</summary>
    public void CommitCapture(string text)
    {
        // Негодный набор подтверждением быть не может: поле остаётся с прежним значением.
        GestureText = HotkeyRules.Normalize(text) is { Length: > 0 } canonical
            ? canonical
            : _savedGesture;

        // Захват окончается в любом исходе: и с подтверждением, и возвратом к прежнему —
        // иначе подсветка и защита сохранения пережили бы набор и остались бы с флагом навсегда.
        IsCapturing = false;

        // После подтверждения «до захвата» — само подтверждённое значение: уход фокуса
        // вернёт ровно то же, а не старый хвост.
        _savedGesture = GestureText;

        UpdateOwner();
    }

    /// <summary>Захват отменён (Esc или уход фокуса): восстанавливает значение до захвата.</summary>
    public void CancelCapture()
    {
        GestureText = _savedGesture;
        IsCapturing = false;
    }

    private void Clear()
    {
        GestureText = DefaultGesture ?? string.Empty;

        UpdateOwner();
    }

    private void UpdateOwner() => _owner.OnHotkeyChanged();
}
