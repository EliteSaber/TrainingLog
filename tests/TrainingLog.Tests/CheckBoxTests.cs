using System.Windows;
using System.Windows.Controls;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>
/// Чекбокс шапки с квадратом справа.
/// </summary>
/// <remarks>
/// Квадрат стоит по центру правой зоны, ширина которой равна полю «Количество дней»: его центр
/// должен совпадать с центром поля под ним, а не с правым краем. Высота квадрата равна высоте
/// контрола, а её задаёт то же поле числа, — проверка держит обе привязки, иначе квадрат снова
/// сместится или уменьшится.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class CheckBoxTests
{
    [Fact]
    public void Квадрат_ПоЦентруПоля_ИРавенВысотеКонтрола()
    {
        StaRunner.Run(() =>
        {
            var box = CreateCheckBox();
            box.Height = 30;
            box.Tag = 52d;

            box.ApplyTemplate();
            box.Measure(new Size(200, 30));
            box.Arrange(new Rect(0, 0, 200, 30));
            box.UpdateLayout();

            var square = box.Template.FindName("Box", box) as Border;
            var label = box.Template.FindName("Label", box) as ContentPresenter;

            Assert.NotNull(square);
            Assert.NotNull(label);

            Assert.InRange(square!.ActualHeight, 29.5, 30.5);
            Assert.InRange(square.ActualWidth, 29.5, 30.5);

            var labelOrigin = label!.TranslatePoint(new Point(0, 0), box);
            var squareOrigin = square.TranslatePoint(new Point(0, 0), box);

            Assert.Equal(0, labelOrigin.X, 1);
            Assert.True(
                squareOrigin.X >= label.ActualWidth,
                $"Квадрат начинается на {squareOrigin.X:F1}, а подпись занимает {label.ActualWidth:F1}.");

            // Правая зона шириной 52 прижата к правому краю, квадрат центрирован в ней.
            var squareCenter = squareOrigin.X + (square.ActualWidth / 2);
            var squareCenterY = squareOrigin.Y + (square.ActualHeight / 2);

            Assert.Equal(200 - (52 / 2), squareCenter, 1);
            Assert.Equal(30 / 2, squareCenterY, 1);
        });
    }

    private static CheckBox CreateCheckBox()
    {
        var box = new CheckBox { Content = "Показать все" };

        box.Resources.MergedDictionaries.Add(Load("Themes/Styles.xaml"));
        box.Resources.MergedDictionaries.Add(Load("Themes/Palette.Light.xaml"));
        box.Style = (Style)box.FindResource("CheckBoxSquareRight");

        return box;
    }

    /// <summary>Словарь из сборки приложения: адрес относительный, с указанием сборки.</summary>
    private static ResourceDictionary Load(string path)
    {
        var assembly = typeof(AppTheme).Assembly.GetName().Name;

        var uri = new Uri($"/{assembly};component/{path}", UriKind.Relative);

        return (ResourceDictionary)Application.LoadComponent(uri)!;
    }
}
