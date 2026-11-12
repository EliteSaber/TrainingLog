using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>
/// Поле даты: свой шаблон вместо системного.
/// </summary>
/// <remarks>
/// Системный DatePicker в светлой теме от наших полей неотличим, и это скрывало, что стиля у
/// него не было вовсе. Проверяется то, на чём держится шаблон: части на месте (иначе DatePicker
/// их не найдёт и останется без текста), а текстовая часть оформлена нашими цветами, а не
/// системными.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class DatePickerTests
{
    /// <summary>Части шаблона на месте, и DatePicker показывает в поле выбранную дату.</summary>
    [Fact]
    public void Шаблон_ЧастиНаМесте_ДатаПоказывается()
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(new DateTime(2026, 11, 12));

            picker.ApplyTemplate();

            var text = picker.Template.FindName("PART_TextBox", picker) as TextBox;
            var button = picker.Template.FindName("PART_Button", picker) as Button;

            Assert.NotNull(text);
            Assert.NotNull(button);

            // Формат даты зависит от языка машины, а вот пустое поле — признак того, что
            // DatePicker не нашёл свою часть и не показал дату.
            Assert.NotEmpty(text.Text);
        });
    }

    /// <summary>Поле даты — наше: цвета берутся из палитры, а не из системной темы.</summary>
    [Fact]
    public void Поле_ОформленоПалитрой()
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(new DateTime(2026, 11, 12));

            picker.ApplyTemplate();

            var text = picker.Template.FindName("PART_TextBox", picker) as TextBox;

            Assert.NotNull(text);

            var background = (SolidColorBrush)picker.Resources["FieldBackground"];
            var foreground = (SolidColorBrush)picker.Resources["TextPrimary"];

            Assert.Equal(background.Color, ((SolidColorBrush)text.Background).Color);

            // Текст даты — наш TextPrimary, а не системный чёрный: в тёмной теме чёрным по
            // тёмному полю читаться не будет.
            Assert.Equal(foreground.Color, ((SolidColorBrush)text.Foreground).Color);
        });
    }

    /// <summary>Поле даты с палитрой и стилями: без них шаблон взял бы системный вид.</summary>
    private static DatePicker CreatePicker(DateTime date)
    {
        var picker = new DatePicker { SelectedDate = date };

        picker.Resources.MergedDictionaries.Add(Load("Themes/Styles.xaml"));
        picker.Resources.MergedDictionaries.Add(Load("Themes/Palette.Light.xaml"));

        return picker;
    }

    /// <summary>Словарь из сборки приложения: адрес относительный, с указанием сборки.</summary>
    private static ResourceDictionary Load(string path)
    {
        var assembly = typeof(AppTheme).Assembly.GetName().Name;

        var uri = new Uri($"/{assembly};component/{path}", UriKind.Relative);

        return (ResourceDictionary)Application.LoadComponent(uri)!;
    }
}
