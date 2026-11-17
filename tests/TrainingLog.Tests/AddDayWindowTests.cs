using System.Windows;
using System.Windows.Controls;
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
}
