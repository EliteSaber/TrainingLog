using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Settings;

namespace TrainingLog.ViewModels;

/// <summary>Окно параметров: число дней и адрес файла базы данных.</summary>
/// <remarks>
/// Настройки живут в файле рядом с программой, это окно — их редактор. Новые директория и
/// наименование базы применяются при следующем запуске: соединение строится один раз, при старте
/// приложения. Диалоги выбора адресов адресуются окном, а не моделью — по тем же причинам,
/// по которым здесь нет <c>MessageBox</c>.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsStore _store;

    /// <param name="store">Файл настроек.</param>
    public SettingsViewModel(AppSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;

        // Из файла, а не из умолчания: пустое поле означало бы потерю настройки первым же
        // нажатием «Сохранить». Наименование переводится правилом: в файле настроек имя файла с
        // расширением, а поле показывает его без хвостого .db.
        var settings = store.Load();
        DaysCount = settings.DaysCount;
        DatabaseDirectory = settings.DatabaseDirectory;
        DatabaseName = DatabaseNameRules.ForDisplay(settings.DatabaseName);
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

    /// <summary>Подсказка об отказе по вводу.</summary>
    public HintState Hint { get; } = new();

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
        && !string.IsNullOrWhiteSpace(DatabaseName);

    /// <summary>
    /// Записывает количество дней и адрес базы в файл настроек.
    /// </summary>
    /// <remarks>
    /// Проверка значений повторяется в теле команды, а не только в предикате: предикат гасит
    /// кнопку, но <c>Execute</c> его не спрашивает и зовёт метод (ловушка 46).
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
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
}
