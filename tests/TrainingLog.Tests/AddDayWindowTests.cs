using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TrainingLog.Core.Models;
using TrainingLog.Services;
using TrainingLog.ViewModels;
using TrainingLog.Windows;

namespace TrainingLog.Tests;

/// <summary>
/// Окно дня: подпись дня недели живёт в шаблоне DatePicker, а поле «План» по высоте
/// приравнено к полю даты.
/// </summary>
[Collection(WpfTestGroup.Name)]
public sealed class AddDayWindowTests
{
    [Fact]
    public void ПолеДаты_ПодписьДняНедели_ВШаблонеИНеРедактируется()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var viewModel = new AddDayViewModel(
                database.SessionRepository,
                database.PlanRepository,
                new DateOnly(2026, 11, 9));
            var window = new AddDayWindow(viewModel, new HotkeyService(settings.Store));

            try
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -32000;
                window.Top = -32000;
                window.ShowActivated = false;
                window.Show();
                window.UpdateLayout();

                var picker = FindVisualChild<DatePicker>(window)!;
                Assert.NotNull(picker);

                var suffix = picker.Template.FindName("WeekdaySuffix", picker) as TextBlock;
                Assert.NotNull(suffix);
                Assert.False(suffix.IsHitTestVisible);
                Assert.Equal(viewModel.DateWeekdayText, suffix.Text);

                var plan = FindVisualChild<ComboBox>(window)!;
                Assert.NotNull(plan);

                // Высота поля даты задаётся кнопкой календаря в её шаблоне.
                var calendar = picker.Template.FindName("PART_Button", picker) as Button;
                Assert.NotNull(calendar);
                Assert.Equal(calendar.Height, plan.MinHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Путь приложения: дата в конструкторе, настоящее окно с DatePicker. Подсказки прошлого
    /// дня обязаны появиться после загрузки: синхронный ответ SQLite, применённый сеттером
    /// плана, стирался перестройкой LoadDateAsync, а VM-тесты без окна этот путь не видят.
    /// </summary>
    [Fact]
    public void Правка_ДатаВКонструкторе_ПодсказкиПрошлогоДня()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var squat = new Exercise { Name = "Приседание" };
            database.Repository.AddAsync(squat).GetAwaiter().GetResult();

            var plan = new TrainingPlan { Name = "Ноги" };
            plan.AddExercise(squat);
            database.PlanRepository.AddAsync(plan).GetAwaiter().GetResult();

            var previous = new TrainingSession { Date = new DateOnly(2026, 10, 8), PlanId = plan.Id, PlanName = plan.Name };
            previous.AddExercise(squat).AddSet(10, 60m);
            database.SessionRepository.AddAsync(previous).GetAwaiter().GetResult();

            var day = new DateOnly(2026, 10, 10);
            var stored = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
            stored.AddExercise(squat).AddSet(0, 0m);
            database.SessionRepository.AddAsync(stored).GetAwaiter().GetResult();

            var viewModel = new AddDayViewModel(database.SessionRepository, database.PlanRepository, day);
            var window = new AddDayWindow(viewModel, new HotkeyService(settings.Store));

            try
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -32000;
                window.Top = -32000;
                window.ShowActivated = false;

                // Продолжения асинхронных чтений возвращаются в диспетчер, как в приложении.
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));

                try
                {
                    window.Show();
                    window.UpdateLayout();

                    var deadline = DateTime.UtcNow.AddSeconds(10);

                    // Ждём самих подсказок, а не фиксированного числа кадров: перестройка после
                    // самопроизвольной записи даты DatePicker может прийти позже первого ответа.
                    // Наполнение идёт до снятия IsLoading, поэтому подсказки при погашенном
                    // индикаторе — устойчивое состояние, а не мгновение между перестройками.
                    while (!HintsReady() && DateTime.UtcNow < deadline)
                    {
                        window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                    }

                    var set = Assert.Single(viewModel.Current!.Sets);

                    Assert.Equal("10", set.RepetitionPlaceholder);
                    Assert.True(set.HasRepetitionPlaceholder);
                    Assert.Equal("60", set.WeightPlaceholder);
                    Assert.True(set.HasWeightPlaceholder);

                    bool HintsReady()
                    {
                        var row = viewModel.Current;

                        return !viewModel.IsLoading
                            && row is { Sets.Count: > 0 }
                            && row.Sets[0].HasRepetitionPlaceholder
                            && row.Sets[0].HasWeightPlaceholder;
                    }
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Очистка примечания с подсказкой не роняет окно: TargetNullValue у двусторонней привязки
    /// записывал в NoteText null, и HasNotePlaceholder падал на NoteText.Length (ловушка 65).
    /// Без подсказки дефект прятало короткое замыкание &&.
    /// </summary>
    [Fact]
    public void Примечание_ОчисткаПоляСПодсказкой_НеРоняетОкно()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            var squat = new Exercise { Name = "Приседание" };
            database.Repository.AddAsync(squat).GetAwaiter().GetResult();

            var plan = new TrainingPlan { Name = "Ноги" };
            plan.AddExercise(squat);
            database.PlanRepository.AddAsync(plan).GetAwaiter().GetResult();

            var previous = new TrainingSession { Date = new DateOnly(2026, 10, 8), PlanId = plan.Id, PlanName = plan.Name };
            previous.AddExercise(squat, "болело левое плечо").AddSet(10, 60m);
            database.SessionRepository.AddAsync(previous).GetAwaiter().GetResult();

            var day = new DateOnly(2026, 10, 10);
            var stored = new TrainingSession { Date = day, PlanId = plan.Id, PlanName = plan.Name };
            stored.AddExercise(squat).AddSet(8, 60m);
            database.SessionRepository.AddAsync(stored).GetAwaiter().GetResult();

            var viewModel = new AddDayViewModel(database.SessionRepository, database.PlanRepository, day);
            var window = new AddDayWindow(viewModel, new HotkeyService(settings.Store));

            try
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -32000;
                window.Top = -32000;
                window.ShowActivated = false;

                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));

                try
                {
                    window.Show();
                    window.UpdateLayout();

                    var deadline = DateTime.UtcNow.AddSeconds(10);

                    while ((viewModel.Current is null || viewModel.IsLoading) && DateTime.UtcNow < deadline)
                    {
                        window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                    }

                    Assert.True(viewModel.Current!.HasNotePlaceholder);

                    // Поле примечания живёт в раскрытом Expander: без раскрытия содержимое
                    // не построено и искать нечего.
                    viewModel.Current.IsNotesExpanded = true;
                    window.UpdateLayout();

                    var noteBox = FindVisualChild<TextBox>(window, box => box.AcceptsReturn);
                    Assert.NotNull(noteBox);

                    noteBox!.Text = "что-то написали";

                    Assert.Equal("что-то написали", viewModel.Current.NoteText);
                    Assert.False(viewModel.Current.HasNotePlaceholder);

                    noteBox.Text = string.Empty;

                    Assert.Equal(string.Empty, viewModel.Current.NoteText);
                    Assert.True(viewModel.Current.HasNotePlaceholder);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T found)
            {
                return found;
            }

            if (FindVisualChild<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T found && predicate(found))
            {
                return found;
            }

            if (FindVisualChild(child, predicate) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
