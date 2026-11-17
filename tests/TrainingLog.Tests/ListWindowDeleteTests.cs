using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TrainingLog.Core.Models;
using TrainingLog.Services;
using TrainingLog.ViewModels;
using TrainingLog.Windows;

namespace TrainingLog.Tests;

/// <summary>
/// Кнопка «Удалить» в строке списка гаснет, пока эта строка редактируется: проверяется
/// привязка в разметке, а не только состояние модели представления.
/// </summary>
/// <remarks>
/// Окна уводятся за экран и не активируются, чтобы тест не мигал и не забирал фокус.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class ListWindowDeleteTests
{
    /// <summary>У редактируемого упражнения кнопка неактивна, у другого — активна.</summary>
    [Fact]
    public void Упражнения_РедактируемоеУпражнение_КнопкаУдалитьНеактивна()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            database.Repository.AddAsync(new Exercise { Name = "Жим" }).GetAwaiter().GetResult();
            database.Repository.AddAsync(new Exercise { Name = "Тяга" }).GetAwaiter().GetResult();

            var viewModel = new ExercisesViewModel(database.Repository, new FakeWindowService());
            viewModel.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();

            var window = new ExercisesWindow(viewModel, new HotkeyService(settings.Store));
            Prepare(window);

            try
            {
                var edited = viewModel.Visible.Single(exercise => exercise.Name == "Жим");
                viewModel.EditCommand.Execute(edited);
                window.UpdateLayout();

                var list = (ItemsControl)window.FindName("ExerciseList")!;

                Assert.False(DeleteButton(Row(list, 0))!.IsEnabled);
                Assert.True(DeleteButton(Row(list, 1))!.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>У редактируемого плана кнопка неактивна, у другого — активна.</summary>
    [Fact]
    public void Планы_РедактируемыйПлан_КнопкаУдалитьНеактивна()
    {
        StaRunner.Run(() =>
        {
            using var database = new TemporaryDatabase();
            using var settings = new TemporarySettings();

            database.PlanRepository.AddAsync(new TrainingPlan { Name = "Ноги" }).GetAwaiter().GetResult();
            database.PlanRepository.AddAsync(new TrainingPlan { Name = "Руки" }).GetAwaiter().GetResult();

            var viewModel = new PlansViewModel(database.PlanRepository, new FakeWindowService());
            viewModel.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();

            var window = new PlansWindow(viewModel, database.Repository, new HotkeyService(settings.Store));
            Prepare(window);

            try
            {
                var edited = viewModel.Visible.Single(plan => plan.Name == "Ноги");
                viewModel.EditCommand.Execute(edited);
                window.UpdateLayout();

                var list = (ItemsControl)window.FindName("PlanList")!;

                Assert.False(DeleteButton(Row(list, 0))!.IsEnabled);
                Assert.True(DeleteButton(Row(list, 1))!.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Окно за экраном, с палитрой: без неё DynamicResource не разрешается (ловушка 6).</summary>
    /// <param name="window">Окно теста.</param>
    private static void Prepare(Window window)
    {
        window.Resources.MergedDictionaries.Add(LoadPalette());

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.ShowActivated = false;

        window.Show();
        window.UpdateLayout();
    }

    /// <summary>Контейнер строки списка по номеру.</summary>
    /// <param name="list">Список окон.</param>
    /// <param name="index">Номер строки.</param>
    private static DependencyObject Row(ItemsControl list, int index) =>
        (DependencyObject)list.ItemContainerGenerator.ContainerFromIndex(index)!;

    /// <summary>Кнопка «Удалить» в поддереве строки.</summary>
    /// <param name="root">Корень поиска.</param>
    private static Button? DeleteButton(DependencyObject root)
    {
        if (root is Button button && button.Content is string text && text == "Удалить")
        {
            return button;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (DeleteButton(VisualTreeHelper.GetChild(root, index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Палитра оформления из сборки приложения.</summary>
    private static ResourceDictionary LoadPalette()
    {
        var assembly = typeof(AppTheme).Assembly.GetName().Name;
        var uri = new Uri($"/{assembly};component/Themes/Palette.Light.xaml", UriKind.Relative);

        return (ResourceDictionary)Application.LoadComponent(uri)!;
    }
}
