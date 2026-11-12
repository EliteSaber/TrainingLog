using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingLog.Controls;
using TrainingLog.Services;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Окно добавления дня тренировки. Одно и на добавление, и на правку: запись за дату одна,
/// и модель сама определяет, есть она в базе или нет.
/// </summary>
/// <remarks>
/// Окно немодальное: главное окно при открытом дне остаётся рабочим, у окна дня владельца нет.
/// Отсюда два отличия от диалогов: <c>DialogResult</c> на немодальном окне делать нельзя
/// (присваивание кинет <see cref="InvalidOperationException"/>, и нечего им сообщать), и
/// «Отменить» с Esc закрываются руками — <c>IsCancel</c> срабатывает только в диалоге, а
/// сохранять результат окну некому. Сохранение окно оповещает событием
/// <see cref="SavedChanged"/>, и его ждёт <see cref="Services.WindowService"/>.
/// </remarks>
public partial class AddDayWindow : Window
{
    private readonly AddDayViewModel _viewModel;

    /// <summary>Собирает и пересобирает горячие клавиши окна по событию сервиса.</summary>
    private readonly HotkeyBinder _binder;

    /// <summary>День записан в базу: журнал в главном окне надо перечитать.</summary>
    /// <remarks>
    /// Источник — <see cref="AddDayViewModel.Saved"/>: модель поднимает PropertyChanged при
    /// каждом успешном сохранении, поэтому «Сохранить» без закрытия попадает сюда наравне с
    /// «Сохранить и закрыть». Подписка снимается в <see cref="OnClosed"/>.
    /// </remarks>
    public event Action? SavedChanged;

    public AddDayWindow(AddDayViewModel viewModel, HotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(hotkeys);

        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Подписка здесь, а не в OnLoaded: план подбирается прямо при Initialize, и событие смены
        // упражнения проходит раньше первого показа. Отложенный вызов подстраховывает: фокус
        // ставится уже по показанному окну, у которого есть цепочка активации (ловушка 9).
        _viewModel.CurrentExerciseChanged += OnCurrentExerciseChanged;

        // Слоты — единственные назначения, у которых исполнитель не кнопка: клавиша выбирает
        // упражнение по его номеру, как клик по кнопке-номеру. Упражнения пересобираются при
        // каждой загрузке плана, поэтому номер ищется в момент нажатия, а не в момент бинда.
        _binder = new HotkeyBinder(this, hotkeys)
            .Slots(
                HotkeyCatalog.SlotIdPrefix,
                number => SelectSlot(number));

        _binder.Rebuild();

        // Подписка на отказ в окне, а не в модели: событие приходит от поля ввода, живёт в
        // поведении Controls, и модель о нём знать не должна. Отписка в Closed обязательна —
        // иначе закрытое окно вместе с визуальным деревом удерживалось бы подпиской.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>Выбирает упражнение дня по номеру кнопки-слота.</summary>
    private void SelectSlot(int number)
    {
        if (_viewModel.Exercises.FirstOrDefault(row => row.Number == number) is { } exercise)
        {
            _viewModel.SelectExerciseCommand.Execute(exercise);
        }
    }

    /// <summary>Активное упражнение сменилось: фокус переносится в повторения первого подхода.</summary>
    /// <remarks>
    /// Перенос откладывается до пустой диспетчерской очереди: уведомление поднимается из модели в
    /// момент смены, когда привязка <c>Current.Sets</c> ещё не разметила контейнеры подходов, и
    /// искать в них поле рано. Подходов нет — фокус не переносится никуда, поведение не обязано
    /// исполняться всегда.
    /// </remarks>
    private void OnCurrentExerciseChanged() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Background, FocusFirstRepetitions);

    /// <summary>Ставит фокус в поле повторений первого подхода и выделяет набранное.</summary>
    /// <remarks>
    /// Проверка предмета привязки обязательна: отложенный вызов может прийти к моменту, когда
    /// сменился план, и <c>Current</c> указывает уже на подходы другого упражнения.
    ///
    /// Поле ищется только в контейнере первого подхода, а не по всему списку: полей повторений
    /// сколько угодно, с <see cref="SetFieldNavigation.Part"/> их помечать приходилось ради
    /// Tab-цепочки, и поиск по всему поддереву нашёл бы первое, а не принадлежащее первому
    /// подходу.
    /// </remarks>
    private void FocusFirstRepetitions()
    {
        if (_viewModel.Current is not { Sets.Count: > 0 } row ||
            !ReferenceEquals(SetsList.ItemsSource, row.Sets))
        {
            return;
        }

        if (SetsList.ItemContainerGenerator.ContainerFromIndex(0) is not DependencyObject container ||
            FindRepetitionsBox(container) is not { } repetitions)
        {
            return;
        }

        repetitions.Focus();
        repetitions.SelectAll();
    }

    /// <summary>Ищет в поддереве поле повторений: <see cref="TextBox"/> c <see cref="SetFieldNavigation.Part"/>, равным <see cref="SetField.Repetitions"/>.</summary>
    private static TextBox? FindRepetitionsBox(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is TextBox box && SetFieldNavigation.GetPart(box) == SetField.Repetitions)
            {
                return box;
            }

            if (FindRepetitionsBox(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // Фокус в конструкторе ставить нельзя: у ещё не показанного окна нет в цепочке активации,
        // и Focus() отработает вхолостую (ловушка 9).
        _viewModel.InitializeCommand.Execute(null);
    }

    /// <summary>
    /// Сохранение оповещается событием, а закрытие по «Сохранить и закрыть» — Close(): присваивать
    /// <c>DialogResult</c> на немодальном окне нельзя.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddDayViewModel.Saved) && _viewModel.Saved)
        {
            SavedChanged?.Invoke();
        }
        else if (e.PropertyName == nameof(AddDayViewModel.Accepted) && _viewModel.Accepted)
        {
            Close();
        }
    }

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

    /// <summary>Нажата кнопка «Отменить»: окно закрывается без сохранения.</summary>
    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>В поле подхода введено или вставлено недопустимое.</summary>
    /// <remarks>
    /// Якорь задаётся здесь, а не в разметке: полей подходов сколько угодно, они создаются по
    /// составу упражнения и имени в разметке не имеют.
    ///
    /// Якорь берётся из <see cref="AllowedCharsInput.RejectedEventArgs.Field"/>, а **не** из
    /// <c>e.Source</c>: событие всплывает от поля вверх, через контейнер подходов, и на окне
    /// <c>Source</c> уже не поле — на Source повесили подсказку, и она вставала по центру всех
    /// подходов сразу (ловушка 41).
    ///
    /// Отсчёт перезапускается явно: повторный отказ с тем же текстом состояние подсказки не меняет,
    /// и без этого она погасла бы на середине набора.
    /// </remarks>
    private void OnInputRejected(object sender, RoutedEventArgs e)
    {
        if (e is not AllowedCharsInput.RejectedEventArgs rejected)
        {
            return;
        }

        InputHint.Target = rejected.Field;

        _viewModel.Hint.ShowCommand.Execute(rejected.Message);

        InputHint.RestartCountdown();
    }

    /// <summary>
    /// В поле подхода введён или вставлен допустимый символ: гасит висящее объяснение. Отказ сюда
    /// не приводит — иначе подсказка мигала бы на каждом негодном символе.
    /// </summary>
    private void OnInputAccepted(object sender, RoutedEventArgs e) =>
        _viewModel.Hint.DismissCommand.Execute(null);

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CurrentExerciseChanged -= OnCurrentExerciseChanged;
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);
    }
}
