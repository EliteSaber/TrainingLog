using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TrainingLog.ViewModels;

/// <summary>
/// Состояние всплывающей подсказки. Общее для всех окон: правило, что текст и показ —
/// разные состояния, не должно повторяться в каждой модели представления.
/// </summary>
/// <remarks>
/// Показ гасится сразу, а текст остаётся в модели ещё на всё время, пока подсказка доигрывает
/// затухание, и убирается последним — по событию закрытия подсказки. Иначе содержимое пустеет
/// раньше окна, и подсказка гаснет в два шага: сперва пропадает надпись, потом крестик.
///
/// Кроме текста и показа подсказка несёт ещё два состояния: <see cref="Kind"/> и
/// <see cref="Lifetime"/>. Оба задаёт то действие, которым подсказка показана, и оба меняются
/// вместе с ним: вид и срок — это «как показывать», а не «что показывать». Отсчёт при этом
/// запускает вызывающий: повторный показ с тем же текстом состояния не меняет (ловушка 31).
/// </remarks>
public sealed partial class HintState : ObservableObject
{
    /// <summary>
    /// Вид показа: отказ по вводу живёт пять секунд и выглядит как отказ, а примечание
    /// упражнения живёт дольше и выглядит как спокойное сообщение.
    /// </summary>
    /// <remarks>
    /// Внутри класса, а не отдельным файлом: вид показа — часть состояния подсказки и больше
    /// нигде не встречается. Вид в подсказку рисует <c>StatusHint</c>: тон текста — его дело,
    /// а решение «показать примечание, а не отказ» принимается здесь.
    /// </remarks>
    public enum HintKind
    {
        /// <summary>Отказ по вводу: короткий срок и предупреждающий тон.</summary>
        Rejection,

        /// <summary>Примечание упражнения: длинный срок и обычный тон текста.</summary>
        Note,
    }

    /// <summary>Сколько живёт подсказка об отказе по вводу.</summary>
    public static readonly TimeSpan RejectionLifetime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Сколько живёт примечание. Дольше отказа по вводу: его читают, а не замечают.
    /// </summary>
    public static readonly TimeSpan NoteLifetime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Текст отказа или примечания. Пусто — показывать нечего.
    /// </summary>
    /// <remarks>
    /// При пустом показе текст какое-то время остаётся непустым: это и есть его переживание
    /// затухания, а не забытое состояние.
    /// </remarks>
    [ObservableProperty]
    private string _text = string.Empty;

    /// <summary>
    /// Показана ли подсказка.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// Чем показана подсказка: вид текста и, вместе с ним, её срок жизни.
    /// </summary>
    [ObservableProperty]
    private HintKind _kind = HintKind.Rejection;

    /// <summary>
    /// Сколько живёт текущий показ.
    /// </summary>
    [ObservableProperty]
    private TimeSpan _lifetime = RejectionLifetime;

    /// <summary>
    /// Показывает подсказку с текстом отказа.
    /// </summary>
    /// <remarks>
    /// Текст и показ пишутся одним действием, потому что инвариант «показ включён ⇒ текст есть»
    /// принадлежит состоянию: окно, гасящее и включающее показ по отдельности, рано или поздно
    /// показало бы подсказку без текста.
    ///
    /// Вид и срок здесь заданы по умолчанию — отказом. Показ примечания идёт отдельным
    /// действием, и обычный вызов после него сам возвращает пятисекундный срок и тон отказа:
    /// иначе отказ по вводу, случившийся после показа примечания, жил бы тридцать секунд.
    /// </remarks>
    /// <param name="message">Объяснение отказа для показа пользователю.</param>
    [RelayCommand]
    public void Show(string message) => ShowCore(message, HintKind.Rejection, RejectionLifetime);

    /// <summary>
    /// Показывает примечание упражнения: обычным тоном и на <see cref="NoteLifetime"/>.
    /// </summary>
    /// <param name="message">Текст примечания.</param>
    [RelayCommand]
    public void ShowNote(string message) => ShowCore(message, HintKind.Note, NoteLifetime);

    /// <summary>
    /// Общая часть обоих показов: вид, срок, текст и сам показ.
    /// </summary>
    /// <remarks>
    /// Порядок обязателен: вид и срок ставятся раньше показа, потому что подсказка
    /// позиционируется и запускает отсчёт в момент включения показа, и не находит там прежних
    /// значений (ловушка 28 — эталонное состояние раньше уведомлений, которые его читают).
    /// </remarks>
    private void ShowCore(string message, HintKind kind, TimeSpan lifetime)
    {
        Kind = kind;
        Lifetime = lifetime;
        Text = message;
        IsVisible = true;
    }

    /// <summary>
    /// Гасит показ, оставляя текст.
    /// </summary>
    /// <remarks>
    /// Текст здесь не трогается намеренно: он должен дожить конца затухания, а убирается уже
    /// по <see cref="ClearCommand"/>. Зовут это действие крестик, таймер и само закрытие окна
    /// подсказки — все три пути должны приводить к одному состоянию.
    /// </remarks>
    [RelayCommand]
    public void Dismiss() => IsVisible = false;

    /// <summary>
    /// Убирает текст. Зовется после того, как подсказка исчезла.
    /// </summary>
    [RelayCommand]
    public void Clear() => Text = string.Empty;
}