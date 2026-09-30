using System.Windows;

namespace TrainingLog.Services;

/// <summary>
/// Управляет жизненным циклом единственного экземпляра окна <typeparamref name="TWindow"/>.
/// Повторный вызов <see cref="Show"/> выводит уже открытое окно на передний план, после закрытия
/// окно создаётся заново.
/// </summary>
/// <typeparam name="TWindow">
/// Тип окна. Конструктор без параметров не требуется: окна создаются фабрикой, которая обычно
/// берёт нужные зависимости из контейнера.
/// </typeparam>
public sealed class SingleInstanceWindowHost<TWindow> where TWindow : Window
{
    private readonly Func<TWindow> _factory;
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
    public SingleInstanceWindowHost(Func<TWindow> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// Показывает окно, либо выводит на передний план уже открытое.
    /// </summary>
    /// <param name="owner">
    /// Владелец окна. Если не задан, используется главное окно приложения.
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

                _window.Activate();
                return;
            }

            _window.Closed -= OnWindowClosed;
        }

        var window = _factory();
        window.Owner = owner ?? Application.Current?.MainWindow;
        window.Closed += OnWindowClosed;
        _window = window;
        window.Show();
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
        if (owner is { IsVisible: true })
        {
            owner.Activate();
        }
    }
}
