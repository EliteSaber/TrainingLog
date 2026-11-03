using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;
using TrainingLog.Services;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

public partial class PlansWindow : Window
{
    private readonly IExerciseRepository _exerciseRepository;
    private readonly HotkeyBinder _binder;

    public PlansWindow(PlansViewModel viewModel, IExerciseRepository exerciseRepository, HotkeyService hotkeys)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(exerciseRepository);
        ArgumentNullException.ThrowIfNull(hotkeys);

        InitializeComponent();
        DataContext = viewModel;

        _exerciseRepository = exerciseRepository;

        // Строки планов работают через выделение: без него клавиши правки и удаления молчат.
        _binder = new HotkeyBinder(this, hotkeys)
            .Row("Plans.Edit", PlanList, row =>
            {
                if (row is TrainingPlan plan)
                {
                    Run(viewModel.EditCommand, plan);
                }
            })
            .Row("Plans.Delete", PlanList, row =>
            {
                if (row is TrainingPlan plan)
                {
                    ConfirmDelete(plan);
                }
            });

        _binder.Rebuild();

        Loaded += OnLoaded;
        Closed += OnClosed;

        // Справочник упражнений и планы открываются в разных окнах, и ни одно из них не знает
        // о правках другого. Уведомление поднимает репозиторий упражнений: он единственный,
        // кто пишет, и новая команда не сможет про него забыть.
        //
        // Подписка живёт здесь, а не в модели представления: репозиторий синглтон, модель
        // планов одноразовая, и подписка из модели без отписки удержала бы закрытое окно
        // вместе с визуальным деревом. У окна срок жизни явный — Closed.
        _exerciseRepository.Changed += OnExercisesChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is PlansViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }

    /// <summary>
    /// Переименование и удаление упражнения меняют и планы: ссылка на упражнение в них, его
    /// наименование и номера в порядке выполнения. Поэтому окно планов перечитывает список
    /// само, как только справочник изменился, — не дожидаясь, когда пользователь в него
    /// вернётся.
    /// </summary>
    private void OnExercisesChanged(object? sender, EventArgs e)
    {
        if (DataContext is PlansViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }

    private void OnClosed(object? sender, EventArgs e) => _exerciseRepository.Changed -= OnExercisesChanged;

    /// <summary>Выполняет команду, если её CanExecute разрешает: RelayCommand.Execute проверку
    /// CanExecute не делает, и командное поведение кнопки обязано сохраниться.</summary>
    private static void Run(ICommand command, object? parameter)
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    /// <summary>Удаление плана с подтверждением: как клику по кнопке из строки.</summary>
    private async void ConfirmDelete(TrainingPlan plan)
    {
        if (DataContext is not PlansViewModel viewModel)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Удалить план «{plan.Name}»?",
            "Удаление плана",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer == MessageBoxResult.Yes && viewModel.DeleteCommand.CanExecute(plan))
        {
            await viewModel.DeleteCommand.ExecuteAsync(plan);
        }
    }

    /// <summary>
    /// Удаление необратимо, поэтому спрашиваем подтверждение здесь, а не в модели представления:
    /// <see cref="MessageBox"/> — работа с интерфейсом, и в представлении ей не место.
    /// </summary>
    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: TrainingPlan plan })
        {
            ConfirmDelete(plan);
        }
    }
}
