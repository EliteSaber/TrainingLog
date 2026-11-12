using System.Text.Json;
using TrainingLog.Core.Data;

namespace TrainingLog.Core.Settings;

/// <summary>
/// Чтение и запись файла настроек рядом с программой.
/// </summary>
/// <remarks>
/// Файл принадлежит копии программы, а база — машине. По умолчанию база лежит в
/// <c>%LOCALAPPDATA%</c>, но директория и наименование файла вынесены в настройки, откуда
/// адрес собирает <see cref="AppSettings.DatabasePath"/>.
/// </remarks>
public sealed class AppSettingsStore
{
    public const string FileName = "settings.json";

    // Отступы при записи: в файл заглядывают глазами, когда правят его руками.
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;

    public AppSettingsStore()
        : this(Resolve())
    {
    }

    /// <param name="path">
    /// Путь к файлу. Отдельный путь нужен тестам: писали бы в настоящий, испортили бы настройки
    /// запущенного экземпляра.
    /// </param>
    public AppSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    /// <summary>
    /// Каталог программы, а не рабочий: рабочий у процесса свой и зависит от способа запуска.
    /// </summary>
    public static string Resolve() => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// Читает настройки, создавая файл, если его нет. Никогда не бросает: зовётся при старте до
    /// показа окон, и негодный файл превратился бы в отказ запуска.
    /// </summary>
    /// <remarks>
    /// Значение вне <see cref="DaysCountRules"/> и нечитаемый файл дают значение по умолчанию.
    /// Файл при этом не переписывается: его правят руками, и молчаливая замена испортила бы
    /// недописанное.
    ///
    /// Поля базы — исключение из правила «не переписывать»: их места в файле не ждут, и отсутствие
    /// значения означает брошенное на середине редактирование, а не правку руками. Пустые
    /// (или пробельные) директория и наименование заменяются умолчанием и дописываются в файл,
    /// как требовал набор настроек целиком.
    /// </remarks>
    public AppSettings Load()
    {
        // File.Exists исключений не бросает: на любой ошибке отвечает false.
        if (!File.Exists(_path))
        {
            Save(new AppSettings());

            return new AppSettings();
        }

        var settings = TryRead();

        if (settings is null)
        {
            return new AppSettings();
        }

        if (!DaysCountRules.IsValid(settings.DaysCount))
        {
            settings.DaysCount = AppSettings.DefaultDaysCount;
        }

        // Пустой шрифт — «по цепочке», а не пропажа настройки: пробельные края к нему и приравниваем.
        // Негодный масштаб откатывает к умолчанию только в памяти: файл правят руками, и переписывать
        // его после опечатанного процента не нужно — тот же договор, что у DaysCount.
        settings.FontFamily = FontRules.NormalizeName(settings.FontFamily);

        if (!FontRules.IsScaleValid(settings.FontSizePercent))
        {
            settings.FontSizePercent = AppSettings.DefaultFontSizePercent;
        }

        // Тема — тем же договором, что и масштаб шрифта: негодное значение откатывается к
        // умолчанию только в памяти, файл правят руками, и переписывать его после опечатки
        // не нужно.
        settings.Theme = ThemeRules.Normalize(settings.Theme);

        // «Hotkeys»: null JSON-десериализатор пишет и поверх инициализатора AppSettings, и
        // дальше HotkeyService упал бы на null-источнике. Умолчание вместо секции — тот же
        // договор, что у DaysCount вне диапазона: значение в памяти заменяется, файл не трогается.
        // Записи с пустой комбинацией отбрасываются: назначения они не дают, а словарь должен
        // остаться чистым.
        settings.Hotkeys = settings.Hotkeys?
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? [];

        if (IsMissing(settings.DatabaseDirectory) || IsMissing(settings.DatabaseName))
        {
            settings.DatabaseDirectory = IsMissing(settings.DatabaseDirectory)
                ? SqliteDatabasePath.DefaultDirectory
                : settings.DatabaseDirectory;
            settings.DatabaseName = IsMissing(settings.DatabaseName)
                ? SqliteDatabasePath.FileName
                : settings.DatabaseName;

            Save(settings);
        }

        return settings;
    }

    // Пробельный каталог или имя — тоже «нет значения»: база по такому адресу не соберётся,
    // а отфильтровывать пробелы второй раз, уже на подключении, значило бы держать правило в двух местах.
    private static bool IsMissing(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Записывает настройки в файл и отвечает, записались ли они.
    /// </summary>
    /// <remarks>
    /// Отказ возвращается значением, а не исключением: программа может стоять в папке без права
    /// записи, и падать на введённом числе там нельзя.
    ///
    /// Запись идёт через временный файл рядом и подмену: оборванная запись не должна оставить
    /// обрезанный JSON. <see cref="Load"/> на нечитаемом файле молча берёт умолчания, и
    /// пользователь вместо своей базы открыл бы другую — со стороны это выглядело бы как
    /// пропавшая история.
    /// </remarks>
    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var temporary = _path + ".tmp";

        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, WriteOptions));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            TryDelete(temporary);
            return false;
        }

        return true;
    }

    /// <summary>Убирает временный файл после неудачной записи, не подменяя исходную ошибку.</summary>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            // Файл настроек остался прежним; неубранный .tmp — меньшее зло, чем исключение
            // из уборки, которое скрыло бы настоящую причину отказа.
        }
    }

    private AppSettings? TryRead()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path));
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return null;
        }
    }

    // Опечатка внутри JSON к файлу не относится, но пользователю от неё не легче. Ловить всё
    // подряд нельзя: под «всё» попал бы OutOfMemoryException.
    private static bool IsUnreadable(Exception exception) =>
        exception is JsonException || IsFileProblem(exception);

    private static bool IsFileProblem(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Security.SecurityException;
}
