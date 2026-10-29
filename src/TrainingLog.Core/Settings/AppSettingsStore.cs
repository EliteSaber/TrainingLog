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
    /// <summary>Имя файла настроек.</summary>
    public const string FileName = "settings.json";

    // Отступы при записи: в файл заглядывают глазами, когда правят его руками.
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;

    /// <summary>Хранилище файла настроек рядом с программой.</summary>
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
    /// значения означает брошенное на середине редактирование, а не правку руками. Пустой
    /// (или пробельный) директория и наименование заменяются умолчанием и дописываются в файл,
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
    /// </remarks>
    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, WriteOptions));
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return false;
        }

        return true;
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
