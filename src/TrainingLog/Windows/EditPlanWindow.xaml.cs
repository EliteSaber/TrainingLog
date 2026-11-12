using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TrainingLog.Controls;
using TrainingLog.Services;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Окно добавления и правки плана. Одно на оба случая: различаются только набор планов
/// для проверки дубля и команда сохранения.
/// </summary>
public partial class EditPlanWindow : Window
{
    private readonly EditPlanViewModel _viewModel;
    private readonly HotkeyBinder _binder;
    private readonly HotkeyService _hotkeys;

    /// <summary>Шаги выделения строк: id назначения к сдвигу строк.</summary>
    /// <remarks>
    /// Один статичный перечень: обработчик срабатывает по каждой вертикальной стрелке, кем бы
    /// ни держался клавиатурный фокус в окне.
    /// </remarks>
    private static readonly (string Id, int Delta)[] StepKeys =
    [
        ("EditPlan.SelectedUp", -1),
        ("EditPlan.SelectedDown", 1),
    ];

    /// <summary>Строка справочника, нажатая мышью: отметка меняется при отпускании в ней же.</summary>
    private PlanExerciseSelection? _pressedRow;

    public EditPlanWindow(EditPlanViewModel viewModel, HotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(hotkeys);

        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _hotkeys = hotkeys;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Строки состава работают через выделение: без него клавиши порядка и убирания молчат.
        _binder = new HotkeyBinder(this, hotkeys)
            .Row("EditPlan.MoveUp", SelectedList, row => Run(_viewModel.MoveUpCommand, row))
            .Row("EditPlan.MoveDown", SelectedList, row => Run(_viewModel.MoveDownCommand, row))
            .Row("EditPlan.Remove", SelectedList, row => Run(_viewModel.RemoveCommand, row));

        _binder.Rebuild();

        // Стрелки, «Пробел» и смена блока слушаются на окне: туннель у корня маршрута доходит
        // до обработчика при любом клавиатурном фокусе, и скролл над списком не съедает стрелку
        // раньше — так же, как в главном окне.
        PreviewKeyDown += OnWindowPreviewKeyDown;

        // Клик мышью переводит стрелки в тот блок, по которому кликнули.
        PreviewMouseDown += OnWindowPreviewMouseDown;

        // Пересборка состава меняет строки целиком: прежнего выделения на экране больше нет,
        // и снимать его надо вместе с ними.
        //
        // Подписка на коллекцию списка, а не на модель: список подписан на свою коллекцию раньше
        // окна, и перестановка строк доходит до окна уже сделанной. Из модели она пришла бы
        // раньше, и раскладка в момент обработчика была бы ещё прежней — полоса прокрутилась бы
        // по старому месту.
        ((INotifyCollectionChanged)SelectedList.Items).CollectionChanged += OnSelectedExercisesChanged;

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>Выполняет команду, если её CanExecute разрешает: RelayCommand.Execute проверку
    /// CanExecute не делает, и командное поведение кнопки обязано сохраниться.</summary>
    private static void Run(ICommand command, object? parameter)
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    /// <summary>Список, в котором работают стрелки и «Пробел»: активный блок окна.</summary>
    private ItemsControl ActiveList =>
        _viewModel.ActiveBlock == PlanBlock.Exercises ? AllList : SelectedList;

    /// <summary>Фокус в поле ввода или в поле «На основе»: там стрелки и пробел — свои.</summary>
    /// <remarks>
    /// Чужое окно в счёт не идёт: клавиши в него уходят мимо нашего окна, и каретка чужого поля
    /// не должна отнимать у нас стрелки.
    /// </remarks>
    private bool IsEditingField =>
        Keyboard.FocusedElement is TextBoxBase or ComboBox && IsInThisWindow(Keyboard.FocusedElement);

    /// <summary>Находится ли элемент в этом окне.</summary>
    private bool IsInThisWindow(object focused) =>
        focused is DependencyObject element && ReferenceEquals(Window.GetWindow(element), this);

    /// <summary>
    /// Двигает выделение строк, переключает блок и ставит отметку по назначенным жестам.
    /// </summary>
    /// <remarks>
    /// Обработчик висит на окне (туннель у корня маршрута), а не на списке: так клавиша доходит
    /// сюда при любом положении клавиатурного фокуса. Совпадение берётся с назначенными жестами
    /// через <see cref="HotkeyService"/>, а не с захардкоженным Up/Down: клавишу назначают в окне
    /// параметров, и файл настроек обязан звучать здесь же.
    ///
    /// Поле ввода и поле «На основе» — исключения: там стрелки остаются кареткой и выбором,
    /// и выделение строк их не отнимает. Пробел у кнопки и галочки — тоже: это их собственное
    /// дело, иначе отметка менялась бы дважды.
    /// </remarks>
    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsEditingField)
        {
            return;
        }

        if (e.Key == Key.Space && Keyboard.FocusedElement is ButtonBase)
        {
            return;
        }

        foreach (var (id, delta) in StepKeys)
        {
            if (Matches(id, e))
            {
                RowSelection.Move(ActiveList, delta);

                e.Handled = true;

                return;
            }
        }

        if (Matches("EditPlan.BlockLeft", e))
        {
            SwitchTo(PlanBlock.Exercises);

            e.Handled = true;

            return;
        }

        if (Matches("EditPlan.BlockRight", e))
        {
            SwitchTo(PlanBlock.Order);

            e.Handled = true;

            return;
        }

        if (Matches("EditPlan.ToggleCheck", e))
        {
            ToggleSelectedCheck();

            e.Handled = true;
        }
    }

    /// <summary>
    /// Клик мышью переводит стрелки в тот блок, по которому кликнули, и уводит туда фокус.
    /// </summary>
    /// <remarks>
    /// Фокус уходит в список по той же причине, по которой в главном окне стрелки слушаются на
    /// окне: пока фокус в поле названия, стрелки глотает каретка, и выделение строк их не видит.
    /// Клик по кнопке строки переводит блок, но фокус забирает сама кнопка.
    /// </remarks>
    private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var list = ListUnder(source);

        if (list is null)
        {
            return;
        }

        _viewModel.ActiveBlock = list == AllList ? PlanBlock.Exercises : PlanBlock.Order;

        list.Focus();
    }

    /// <summary>Нажали строку справочника: запоминаем её, отметка придёт при отпускании.</summary>
    private void OnAllListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressedRow = e.ChangedButton == MouseButton.Left
            ? RowUnder(AllList, e.OriginalSource as DependencyObject)
            : null;
    }

    /// <summary>
    /// Отпустили строку справочника: отметка переключается, как у CheckBox.
    /// </summary>
    /// <remarks>
    /// Отметка меняется при отпускании в той же строке, а не при нажатии: нажали в одной строке и
    /// отпустили в другой — отмечать нечего. Сама галочка переключает отметку сама и нажатие
    /// гасит, поэтому её строки этот обработчик не видит и дважды ничего не переключает.
    /// </remarks>
    private void OnAllListMouseUp(object sender, MouseButtonEventArgs e)
    {
        var pressed = _pressedRow;

        _pressedRow = null;

        if (pressed is null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (RowUnder(AllList, e.OriginalSource as DependencyObject) != pressed)
        {
            return;
        }

        Run(_viewModel.ToggleCheckCommand, pressed);
    }

    /// <summary>Состав изменился: пересборка снимает выделение, перестановка ведёт строку в вид.</summary>
    /// <remarks>
    /// Перестановка строк (Move) выделением не трогает: контейнер едет вместе со строкой, и
    /// выделение остаётся видимым. А вот пересборка («На основе», «Сбросить») пересоздаёт
    /// контейнеры, и выделение в данных пережило бы их — горячие клавиши работали бы по невидимой
    /// строке.
    /// </remarks>
    private void OnSelectedExercisesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                RowSelection.Clear(SelectedList);
                break;

            case NotifyCollectionChangedAction.Move:
                if (e.NewItems?[0] is PlanExerciseSelection moved)
                {
                    BringIntoView(moved);
                }

                break;
        }
    }

    /// <summary>Показывает перемещённую строку: уехав за край окна, она должна вернуться в вид.</summary>
    /// <remarks>
    /// Так же ведёт себя выбор стрелками (<see cref="RowSelection.Move"/>): контейнер строки просит
    /// показать себя. Просьба отрабатывает при раскладке — той самой, что переставляет строки, —
    /// поэтому полоса сдвигается вместе с ними и строка за край не уезжает даже на кадр. Раскладку
    /// прогонять отсюда нельзя: список переставит строки позже, и полоса прокрутилась бы по
    /// старому месту.
    /// </remarks>
    private void BringIntoView(PlanExerciseSelection row)
    {
        if (RowSelection.Container(SelectedList, row) is { } container)
        {
            container.BringIntoView();
        }
    }

    /// <summary>Совпадает ли нажатие с назначенным жестом действия.</summary>
    private bool Matches(string id, KeyEventArgs e) =>
        _hotkeys.Resolve(id) is { } gesture && HotkeyService.Matches(gesture, e.Key, Keyboard.Modifiers);

    /// <summary>Переход в блок: блок становится активным, фокус уходит в его список.</summary>
    /// <remarks>
    /// Выделенная строка не появляется сама: выделение в блоке — от стрелок, и строка, выделенная
    /// без приказа, была бы отмечена «Пробелом» по первому нажатию. Активный блок виден по
    /// заголовку и рамке.
    /// </remarks>
    private void SwitchTo(PlanBlock block)
    {
        _viewModel.ActiveBlock = block;

        var list = block == PlanBlock.Exercises ? AllList : SelectedList;

        list.Focus();
    }

    /// <summary>Отметка упражнения выделенной стрелками строки справочника.</summary>
    /// <remarks>
    /// Отметки есть только в справочнике: в составе их нет, и «Пробел» там — пустое место.
    /// </remarks>
    private void ToggleSelectedCheck()
    {
        if (_viewModel.ActiveBlock != PlanBlock.Exercises)
        {
            return;
        }

        if (RowSelection.Item(AllList) is PlanExerciseSelection row)
        {
            Run(_viewModel.ToggleCheckCommand, row);
        }
    }

    /// <summary>Список, в котором оказался элемент под мышью, иначе <c>null</c>.</summary>
    private ItemsControl? ListUnder(DependencyObject source)
    {
        if (AllList.ContainerFromElement(source) is not null)
        {
            return AllList;
        }

        return SelectedList.ContainerFromElement(source) is not null ? SelectedList : null;
    }

    /// <summary>Строка списка под элементом, иначе <c>null</c>.</summary>
    private static PlanExerciseSelection? RowUnder(ItemsControl list, DependencyObject? source) =>
        source is not null && list.ContainerFromElement(source) is FrameworkElement container
            ? container.DataContext as PlanExerciseSelection
            : null;

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
    /// Кнопка подтверждения привязана к команде, поэтому WPF сама снимает активность кнопки,
    /// когда правки нет. А вот результат сохранения известен только здесь: по одному значению
    /// <see cref="EditPlanViewModel.Accepted"/> вызывающий <c>ShowDialog</c> поймёт, что план
    /// сохранён. Поэтому <see cref="DialogResult"/> выставляется по уведомлению.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditPlanViewModel.Accepted) && _viewModel.Accepted)
        {
            DialogResult = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        PreviewKeyDown -= OnWindowPreviewKeyDown;
        PreviewMouseDown -= OnWindowPreviewMouseDown;
        ((INotifyCollectionChanged)SelectedList.Items).CollectionChanged -= OnSelectedExercisesChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
