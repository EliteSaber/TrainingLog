using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Settings;

namespace TrainingLog.ViewModels;

/// <summary>Окно параметров: сколько дней показывать в журнале.</summary>
/// <remarks>
/// Настройка одна, и её же показывает главное окно: живёт она в файле рядом с программой, а
/// это окно — её редактор.
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
        // нажатием «Сохранить».
        DaysCount = store.Load().DaysCount;
    }

    /// <summary>Сколько дней показывать.</summary>
    /// <remarks>
    /// Проверяется то же <see cref="DaysCountRules"/>, что и у «Применить» в главном окне: одно
    /// число в двух формах не должно иметь два мнения о годности.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _daysCount;

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

    private bool CanSave() => DaysCountRules.IsValid(DaysCount);

    /// <summary>
    /// Записывает количество дней в файл настроек.
    /// </summary>
    /// <remarks>
    /// Проверка значения повторяется в теле команды, а не только в предикате: предикат гасит
    /// кнопку, но <c>Execute</c> его не спрашивает и зовёт метод (ловушка 46).
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
        // чтобы выставить DialogResult.
        OnPropertyChanged(nameof(Accepted));
    }
}
