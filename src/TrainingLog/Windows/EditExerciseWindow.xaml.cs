using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TrainingLog.Services;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Окно правки названия упражнения. Отдельное и немодальное: справочник остаётся рабочим.
/// </summary>
/// <remarks>
/// Окно немодальное, и отличий от диалога три: <c>DialogResult</c> выставлять нельзя
/// (присваивание кинет <see cref="InvalidOperationException"/>), «Отменить» закрывает окно
/// обработчиком, а Esc ловит окно — <c>IsCancel</c> работает только в диалоге. О записи окно
/// сообщает событием <see cref="Saved"/>, и его ждёт <see cref="Services.WindowService"/>.
/// </remarks>
public partial class EditExerciseWindow : Window
{
    private readonly EditExerciseViewModel _viewModel;
    private readonly HotkeyBinder _binder;

    /// <summary>Упражнение записано: справочнику пора перестроить список.</summary>
    public event Action? Saved;

    public EditExerciseWindow(EditExerciseViewModel viewModel, HotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(hotkeys);

        InitializeComponent();
        DataContext = viewModel;

        // Размер окна — из каталога или из файла настроек: в разметке его нет. Высоты у окна
        // нет вовсе: она следует за содержимым.
        WindowSizes.Apply(this);

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Явных исполнителей у окна нет: обе кнопки найдутся по Id в дереве окна.
        _binder = new HotkeyBinder(this, hotkeys);

        _binder.Rebuild();

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // Фокус ставим здесь, а не в конструкторе: у ещё не показанного окна нет в цепочке
        // активации, и Focus() в конструкторе отрабатывает вхолостую.
        NameBox.Focus();
        NameBox.SelectAll();

        _viewModel.InitializeCommand.Execute(null);
    }

    /// <summary>
    /// Правка записана: сообщаем справочнику и закрываем окно.
    /// </summary>
    /// <remarks>
    /// Окно немодальное, и <c>DialogResult</c> выставлять нельзя: присваивание кинет
    /// <see cref="InvalidOperationException"/>. Закрытие делает само окно, а вызывающий узнаёт
    /// о записи по событию <see cref="Saved"/>.
    /// </remarks>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditExerciseViewModel.Accepted) && _viewModel.Accepted)
        {
            Saved?.Invoke();

            Close();
        }
    }

    /// <summary>Нажата кнопка «Отменить»: окно закрывается без записи.</summary>
    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Esc закрывает окно, как в диалоге закрывала кнопка <c>IsCancel</c>.</summary>
    /// <remarks>
    /// В немодальном окне WPF связку «Esc → Отменить» больше никто не держит: <c>IsCancel</c>
    /// работает только в диалоге. Здесь обработка на окне, а не KeyBinding'ом, — команды
    /// «закрыть у окна» в модели нет, и заводить её ради клавиши незачем.
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
