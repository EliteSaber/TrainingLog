using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TrainingLog.ViewModels;

/// <summary>
/// Состояние всплывающей подсказки об отказе по вводу. Общее для всех окон: правило, что текст
/// и показ — разные состояния, не должно повторяться в каждой модели представления.
/// </summary>
/// <remarks>
/// Показ гасится сразу, а текст остаётся в модели ещё на всё время, пока подсказка доигрывает
/// затухание, и убирается последним — по событию закрытия подсказки. Иначе содержимое пустеет
/// раньше окна, и подсказка гаснет в два шага: сперва пропадает надпись, потом крестик.
/// </remarks>
public sealed partial class HintState : ObservableObject
{
    /// <summary>
    /// Текст отказа. Пусто — показывать нечего.
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
    /// Показывает подсказку с текстом отказа.
    /// </summary>
    /// <remarks>
    /// Текст и показ пишутся одним действием, потому что инвариант «показ включён ⇒ текст есть»
    /// принадлежит состоянию: окно, гасящее и включающее показ по отдельности, рано или поздно
    /// показало бы подсказку без текста.
    /// </remarks>
    /// <param name="message">Объяснение отказа для показа пользователю.</param>
    [RelayCommand]
    public void Show(string message)
    {
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