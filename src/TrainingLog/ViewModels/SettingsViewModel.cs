using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Settings;

namespace TrainingLog.ViewModels;

/// <summary>
/// Окно параметров: сколько дней показывать в журнале.
/// </summary>
/// <remarks>
/// Настройка одна и она же показывается в главном окне, поэтому значение живёт в файле рядом с
/// программой, а окно — его редактор. Главное окно читает файл при старте и перечитывает после
/// того, как параметры сохранены, поэтому список дней сам по себе не перечитывается: он
/// меняется кнопкой «Применить», как и любое другое значение поля.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsStore _store;

    /// <param name="store">Файл настроек.</param>
    public SettingsViewModel(AppSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;

        // Значение берётся из файла, а не из молчаливого умолчания: окно открывают правки
        // того, что сохранено, и пустое поле означало бы потерю настройки при первом же
        // нажатии «Сохранить».
        DaysCount = store.Load().DaysCount;
    }

    /// <summary>
    /// Сколько дней показывать.
    /// </summary>
    /// <remarks>
    /// Проверяется то же правило <see cref="DaysCountRules"/>, что и у «Применить» в главном
    /// окне, и по той же причине: одно число в двух формах, у которых своё мнение о годности,
    /// расходится при первой же правке.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _daysCount;

    /// <summary>
    /// Подсказка об отказе по вводу в поле количества дней.
    /// </summary>
    /// <remarks>
    /// Общее состояние с остальными окнами: отказ по вводу объясняется одинаково везде, и
    /// заводить его в каждой модели представления значило бы завести копию правила.
    /// </remarks>
    public HintState Hint { get; } = new();

    /// <summary>
    /// Признак, что настройки приняты и записаны. Окно выставляет <c>DialogResult</c> по нему.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Объяснение отказа записи, а при пустом значении — ничего.
    /// </summary>
    /// <remarks>
    /// Отказ о записи живёт до успешного сохранения, а не пять секунд в подсказке: настройка
    /// не записалась, и объяснение обязано дождаться, пока им займутся. Правило то же, что у
    /// отказа о сохранении дня.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    /// <summary>Показывать ли объяснение отказа.</summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    private bool CanSave() => DaysCountRules.IsValid(DaysCount);

    /// <summary>
    /// Записывает количество дней в файл настроек.
    /// </summary>
    /// <remarks>
    /// Проверка значения повторяется в теле команды, а не только в предикате: предикат гасит
    /// кнопку, но команду зовут и программно, и <c>CanExecute</c> её не останавливает. Записать
    /// негодное значение можно было бы и по прямой, из кода.
    ///
    /// Отказ ввода-вывода не бросается наружу, а показывается надписью: программа может стоять
    /// в папке без права записи, и падение на введённом числе означало бы отказ приложения
    /// там, где всё ещё можно работать.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!DaysCountRules.IsValid(DaysCount))
        {
            return;
        }

        if (!_store.Save(new AppSettings { DaysCount = DaysCount }))
        {
            Status = "Не удалось записать файл настроек. Программа стоит в папке без права записи?";

            return;
        }

        Status = string.Empty;
        Accepted = true;

        // Приватный сеттер не даёт сгенерировать уведомление автоматически, а окно ждёт его,
        // чтобы выставить DialogResult: по одному значению Accepted оно не узнает, что
        // настройка записана.
        OnPropertyChanged(nameof(Accepted));
    }
}
