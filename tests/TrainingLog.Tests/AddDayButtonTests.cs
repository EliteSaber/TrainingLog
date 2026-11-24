using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TrainingLog.Services;
using ShapePath = System.Windows.Shapes.Path;

namespace TrainingLog.Tests;

/// <summary>
/// Кнопка «Добавить день»: 216×40 — минимум по мокапу, а не предел. При большом кегле
/// кнопка растёт по содержимому, иначе надпись обрезалась бы краями.
/// </summary>
[Collection(WpfTestGroup.Name)]
public sealed class AddDayButtonTests
{
    [Fact]
    public void Кнопка_БазовыйКегль_ГабаритПоМокапу()
    {
        StaRunner.Run(() =>
        {
            var button = CreateButton(15);
            button.Measure(new Size(1000, 1000));

            Assert.Equal(216, button.DesiredSize.Width);
            Assert.Equal(40, button.DesiredSize.Height);
        });
    }

    /// <remarks>
    /// Центрирование надписи здесь не проверяется: его задаёт баланс отступов содержимого в
    /// разметке главного окна (правый отступ надписи равен левой группе «иконка + отступы»),
    /// а копия содержимого в тесте проверяла бы собственную симметрию, а не разметку приложения.
    /// </remarks>
    [Fact]
    public void Кнопка_БольшойКегль_РастётВместеСНадписью()
    {
        StaRunner.Run(() =>
        {
            var button = CreateButton(30);
            button.Measure(new Size(1000, 1000));
            var size = button.DesiredSize;

            Assert.True(size.Width > 216);
            Assert.True(size.Height > 40);
        });
    }

    /// <summary>Кнопка со стилем приложения и содержимым главного окна: кегль задан явно.</summary>
    private static Button CreateButton(double fontSize)
    {
        var resources = Load("Themes/GlassButton.xaml");

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new ShapePath
        {
            Width = 10,
            Height = 10,
            Margin = new Thickness(16, 0, 0, 0),
            Data = Geometry.Parse("M1,5 H9 M5,1 V9"),
        };
        Grid.SetColumn(icon, 0);
        content.Children.Add(icon);

        var label = new TextBlock
        {
            Margin = new Thickness(16, 0, 42, 0),
            Text = "Добавить день",
        };
        Grid.SetColumn(label, 1);
        content.Children.Add(label);

        return new Button
        {
            Style = (Style)resources["AddDayButtonSoft"],
            FontSize = fontSize,
            Content = content,
        };
    }

    /// <summary>Словарь из сборки приложения: адрес относительный, с указанием сборки.</summary>
    private static ResourceDictionary Load(string path)
    {
        var assembly = typeof(AppTheme).Assembly.GetName().Name;

        var uri = new Uri($"/{assembly};component/{path}", UriKind.Relative);

        return (ResourceDictionary)Application.LoadComponent(uri)!;
    }
}
