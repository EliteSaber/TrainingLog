using System.Windows;

namespace TrainingLog.Services;

/// <summary>
/// Возврат активации после закрытия окна приложения.
/// </summary>
/// <remarks>
/// У окон приложения нет владельца, и передний план после закрытия Windows никому не
/// возвращает (ловушка 9). Если закрыто последнее окно, кроме главного, активацию получает
/// журнал, а свёрнутый журнал разворачивается: иначе фокус уходил бы в никуда, и окно висело
/// бы свёрнутым на панели задач.
/// </remarks>
public static class WindowReturn
{
    /// <summary>
    /// Возвращает активацию <paramref name="target"/>, а главное окно, если оно последнее,
    /// ещё и разворачивает.
    /// </summary>
    /// <param name="target">Кому возвращать активацию; <c>null</c> — возвращать некому.</param>
    /// <param name="closed">Окно, которое закрылось.</param>
    public static void Activate(Window? target, Window closed)
    {
        ArgumentNullException.ThrowIfNull(closed);

        var application = Application.Current;

        Activate(
            target,
            closed,
            application?.MainWindow,
            application is null ? null : application.Windows.Cast<Window>());
    }

    /// <summary>
    /// То же правило без приложения: главное окно и список окон заданы явно, так его проверяет
    /// тест.
    /// </summary>
    /// <param name="target">Кому возвращать активацию; <c>null</c> — возвращать некому.</param>
    /// <param name="closed">Окно, которое закрылось.</param>
    /// <param name="main">Главное окно приложения; <c>null</c> — приложения нет.</param>
    /// <param name="windows">Все окна приложения; <c>null</c> — других окон нет.</param>
    public static void Activate(Window? target, Window closed, Window? main, IEnumerable<Window>? windows)
    {
        ArgumentNullException.ThrowIfNull(closed);

        if (target is not { IsVisible: true })
        {
            return;
        }

        if (main is not null
            && ReferenceEquals(target, main)
            && IsLastOtherWindow(closed, main, windows ?? []))
        {
            Restore(target);
        }

        target.Activate();
    }

    /// <summary>
    /// Закрытое окно — последнее, кроме главного: других видимых окон приложения нет.
    /// </summary>
    /// <remarks>
    /// Окнами приложения считаются только окна из сборки главного: отладчик Visual Studio
    /// подселяет в <see cref="Application.Windows"/> своё видимое окно (AdornerWindow из Live
    /// Visual Tree), и без этого фильтра правило решало бы, что закрыто не последнее окно.
    /// </remarks>
    /// <param name="closed">Окно, которое закрылось.</param>
    /// <param name="main">Главное окно приложения.</param>
    /// <param name="windows">Все окна приложения.</param>
    /// <returns><c>true</c>, если других видимых окон приложения, кроме закрываемого и главного, нет.</returns>
    public static bool IsLastOtherWindow(Window closed, Window main, IEnumerable<Window> windows)
    {
        ArgumentNullException.ThrowIfNull(closed);
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(windows);

        return !windows.Any(window =>
            !ReferenceEquals(window, closed)
            && !ReferenceEquals(window, main)
            && window.IsVisible
            && IsApplicationWindow(window, main));
    }

    /// <summary>
    /// Окно объявлено в сборке приложения, а не подселено отладчиком или сторонней библиотекой.
    /// </summary>
    /// <param name="window">Проверяемое окно.</param>
    /// <param name="main">Главное окно приложения.</param>
    private static bool IsApplicationWindow(Window window, Window main) =>
        window.GetType().Assembly == main.GetType().Assembly;

    /// <summary>
    /// Разворачивает свёрнутое окно, сохраняя прежний вид.
    /// </summary>
    /// <remarks>
    /// Механизм — <see cref="WindowAttention.Restore"/>: разворот делает Windows по
    /// <c>SW_RESTORE</c>, а не присваивание <c>WindowState</c>. Свойство WPF отражает его
    /// собственное представление о состоянии окна и может с настоящим разойтись, а система
    /// возвращает окно в то состояние, из которого его свернули, — развёрнутое до сворачивания
    /// останется развёрнутым.
    /// </remarks>
    /// <param name="window">Окно, которое надо развернуть.</param>
    public static void Restore(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        WindowAttention.Restore(window);
    }
}
