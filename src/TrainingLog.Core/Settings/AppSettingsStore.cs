using System.Text.Json;

namespace TrainingLog.Core.Settings;

/// <summary>
/// Чтение и запись файла настроек рядом с программой.
/// </summary>
/// <remarks>
/// Файл лежит рядом с исполняемым файлом, а не в профиле, и это осознанно: настройка — это
/// часть запущенной копии программы, и перенос папки переносит её вместе с ней. База при этом
/// остаётся в <c>%LOCALAPPDATA%</c> (<c>SqliteDatabasePath</c>): она машинная и к облачному
/// бэкапу профиля отношения не имеет. Совпадения папок у них нет, и специально их не связывали.
/// </remarks>
public sealed class AppSettingsStore
{
    /// <summary>
    /// Имя файла настроек.
    /// </summary>
    public const string FileName = "settings.json";

    /// <summary>
    /// Отступы при записи: в файл заглядывают глазами, когда правят его руками.
    /// </summary>
    /// <remarks>
    /// Объект общий и неизменяемый: <c>JsonSerializerOptions</c> читает его при каждой
    /// сериализации, и новый на каждый вызов создавать незачем.
    /// </remarks>
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;

    /// <summary>
    /// Хранилище файла настроек рядом с программой.
    /// </summary>
    public AppSettingsStore()
        : this(Resolve())
    {
    }

    /// <summary>
    /// Хранилище настроек по указанному пути.
    /// </summary>
    /// <param name="path">
    /// Путь к файлу настроек. Отдельный путь нужен тестам: настоящий файл лежит рядом с
    /// программой, и тест, писавший бы в него, портил бы настройки запущенного экземпляра.
    /// </param>
    public AppSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    /// <summary>
    /// Путь к файлу настроек рядом с программой.
    /// </summary>
    /// <remarks>
    /// <see cref="AppContext.BaseDirectory"/>, а не рабочий каталог: рабочий каталог у процесса
    /// свой, он зависит от способа запуска (<c>dotnet run</c>, ярлык, командная строка), и
    /// настройки завелись бы не там. Каталог программы один — тот, что рядом с <c>.exe</c>.
    /// </remarks>
    public static string Resolve() => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// Читает настройки, создавая файл, если его нет.
    /// </summary>
    /// <remarks>
    /// Исключений не бросает никогда, потому что зовётся при старте приложения, до показа
    /// окон: негодный файл не должен превращаться в отказ запуска. Отсюда три случая, и все
    /// три отдают годные настройки:
    /// <list type="bullet">
    /// <item>файла нет — он создаётся со значением по умолчанию, и отдаётся оно же;</item>
    /// <item>файл не читается или в нём не разобрать JSON — отдаётся значение по умолчанию;</item>
    /// <item>значение не по диапазону — отдаётся значение по умолчанию.</item>
    /// </list>
    ///
    /// В двух последних случаях файл не переписывается: его правят руками, и молчаливая замена
    /// испортила бы то, что человек ещё не дописал. Приведённое к году значение запишется
    /// само, когда пользователь сохранит параметры.
    /// </remarks>
    public AppSettings Load()
    {
        // File.Exists исключений не бросает: на любой ошибке он просто отвечает false.
        if (!File.Exists(_path))
        {
            Save(new AppSettings());

            return new AppSettings();
        }

        var loaded = TryRead();

        return loaded is not null && DaysCountRules.IsValid(loaded.DaysCount) ? loaded : new AppSettings();
    }

    /// <summary>
    /// Записывает настройки в файл.
    /// </summary>
    /// <remarks>
    /// Отказ ввода-вывода возвращается значением, а не исключением: окно параметров показывает
    /// его красной строкой, и падать на только что введённом числе нельзя — тем более что
    /// программа может стоять в папке, куда записать нельзя (<c>Program Files</c>).
    /// </remarks>
    /// <param name="settings">Настройки для записи.</param>
    /// <returns><c>true</c>, если файл записан.</returns>
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

    /// <summary>
    /// Читает файл, отдавая <c>null</c> вместо исключения.
    /// </summary>
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

    /// <summary>
    /// Не прочитано ли содержимое файла.
    /// </summary>
    /// <remarks>
    /// Список исключений явный, а не «всё подряд»: опечатка внутри JSON — это <c>JsonException</c>,
    /// он к файлу отношения не имеет, но пользователю от неё не легче, поэтому она в списке.
    /// Ошибки разбора JSON ловить нужно точечно ещё и потому, что <see cref="OutOfMemoryException"/>
    /// ловить нельзя, а «поймать всё» — значит поймать и его.
    /// </remarks>
    private static bool IsUnreadable(Exception exception) =>
        exception is JsonException || IsFileProblem(exception);

    /// <summary>
    /// Относится ли отказ к файлу, а не к самой настройке.
    /// </summary>
    /// <remarks>
    /// <see cref="NotSupportedException"/> и <see cref="System.Security.SecurityException"/> — из
    /// полноты списка: путь приходит извне, и при негодном ни одном из них
    /// <c>File</c> бросает именно их.
    /// </remarks>
    private static bool IsFileProblem(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Security.SecurityException;
}
