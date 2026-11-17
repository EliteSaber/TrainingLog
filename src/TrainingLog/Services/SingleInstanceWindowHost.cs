using System.Windows;

namespace TrainingLog.Services;

/// <summary>
/// Управляет жизненным циклом единственного экземпляра окна <typeparamref name="TWindow"/>.
/// Повторный вызов <see cref="Show"/> выводит уже открытое окно на передний план, после закрытия
/// окно создаётся заново.
/// </summary>
/// <remarks>
/// Окна у хоста бывают двух сортов. **Окно-владелец** (owned) — «своё» окно главного: у него
/// есть владелец, и сворачивание главного прячет и его, а в панели задач его нет. **Отдельное
/// окно** — самостоятельное: владельца у него нет, своя кнопка на панели задач есть, и после
/// сворачивания главного до него можно переключиться. Так устроено окно дня, и так же сделаны
/// справочники и параметры.
/// </remarks>
/// <typeparam name="TWindow">
/// Тип окна. Конструктор без параметров не требуется: окна создаются фабрикой, которая обычно
/// берёт нужные зависимости из контейнера.
/// </typeparam>
public sealed class SingleInstanceWindowHost<TWindow> where TWindow : Window
{
    private readonly Func<TWindow> _factory;
    private readonly bool _owned;
    private TWindow? _window;

    /// <summary>
    /// Текущий экземпляр окна, если оно уже создано и ещё не закрыто.
    /// </summary>
    /// <remarks>
    /// Нужен, чтобы производные окна знали, к кому они должны привязать владельца и куда
    /// вернуть активацию, вместо того чтобы искать активное окно перебором.
    /// </remarks>
    public TWindow? Current => _window;

    /// <param name="factory">Создаёт новый экземпляр окна. Вызывается при каждом открытии.</param>
    /// <param name="owned">
    /// Окно принадлежит владельцу (главному окну): сворачивание главного прячет и его, и в
    /// панели задач его нет. <c>false</c> — окно отдельное: владельца у него нет, своя кнопка
    /// на панели задач есть, и после сворачивания главного до него можно переключиться.
    /// </param>
    public SingleInstanceWindowHost(Func<TWindow> factory, bool owned = true)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _owned = owned;
    }

    /// <summary>
    /// Показывает окно, либо выводит на передний план уже открытое.
    /// </summary>
    /// <param name="owner">
    /// Владелец окна. У окна-владельца он обязателен, у отдельного окна игнорируется: владельца
    /// у него нет и быть не должно.
    /// </param>
    public void Show(Window? owner = null)
    {
        if (_window is not null)
        {
            if (_window.IsVisible)
            {
                if (_window.WindowState == WindowState.Minimized)
                {
                    _window.WindowState = WindowState.Normal;
                }

                BringToFront(_window);

                return;
            }

            _window.Closed -= OnWindowClosed;
        }

        var window = _factory();

        if (_owned)
        {
            window.Owner = owner ?? Application.Current?.MainWindow;
        }

        window.Closed += OnWindowClosed;
        _window = window;
        window.Show();
    }

    /// <summary>
    /// Выводит окно на передний план.
    /// </summary>
    /// <remarks>
    /// Отдельному окну <c>Activate()</c> не хватает: он даёт фокус, но не поднимает окно по
    /// z-порядку, и после клика по главному окну оно осталось бы за ним (ловушка 9). У окна-владельца
    /// разницы нет — владелец всегда стоит выше.
    /// </remarks>
    /// <param name="window">Окно, которое выводится вперёд.</param>
    private void BringToFront(TWindow window)
    {
        if (_owned)
        {
            window.Activate();

            return;
        }

        WindowAttention.BringToFront(window);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        var owner = (sender as TWindow)?.Owner;

        if (sender is TWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        _window = null;

        // Возвращаем активацию владельцу явно: если этого не сделать, активацию забирает
        // приложение, которое было активно до нас. См. WindowService.ShowEditExercise.
        //
        // У отдельного окна владельца нет — возвращать некому, и активацию забирает главное
        // окно. Оно получает фокус, но остаётся свёрнутым, если журнал его свернули: журнал
        // возвращает пользователь сам, как и у окна дня.
        var target = owner ?? Application.Current?.MainWindow;

        if (target is { IsVisible: true })
        {
            target.Activate();
        }
    }
}
