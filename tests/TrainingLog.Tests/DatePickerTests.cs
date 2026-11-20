using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Controls;
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

    /// <summary>
    /// Подпись дня недели внутри поля: текст приходит из attached-свойства и не ловит мышь.
    /// </summary>
    [Fact]
    public void Шаблон_ПодписьДняНедели_ПоказываетсяИНеРедактируется()
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(new DateTime(2026, 11, 12));
            DatePickerSuffix.SetText(picker, "[чт]");

            picker.ApplyTemplate();
            var suffix = picker.Template.FindName("WeekdaySuffix", picker) as TextBlock;

            Assert.NotNull(suffix);
            Assert.Equal("[чт]", suffix.Text);
            Assert.False(suffix.IsHitTestVisible);
        });
    }

    /// <summary>
    /// Подпись красится как дата в обеих темах: унаследованный от DatePicker календарный цвет
    /// в тёмной теме слился бы с фоном поля.
    /// </summary>
    [Theory]
    [InlineData("Themes/Palette.Light.xaml")]
    [InlineData("Themes/Palette.Dark.xaml")]
    public void Подпись_ЦветКакУДаты_ВОбеихТемах(string palette)
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(new DateTime(2026, 11, 12));
            picker.Resources.MergedDictionaries.Add(Load(palette));
            DatePickerSuffix.SetText(picker, "[чт]");

            picker.ApplyTemplate();
            var suffix = picker.Template.FindName("WeekdaySuffix", picker) as TextBlock;
            Assert.NotNull(suffix);

            var expected = (SolidColorBrush)picker.Resources["TextPrimary"];
            Assert.Equal(expected.Color, ((SolidColorBrush)suffix.Foreground).Color);
        });
    }

    /// <summary>
    /// Подпись рисуется поверх поля, не отнимая у него ширину: тексту остаётся вся колонка
    /// между краем DatePicker и кнопкой календаря.
    /// </summary>
    [Fact]
    public void Шаблон_ПодписьНеСужаетПоле()
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(new DateTime(2026, 11, 12));
            DatePickerSuffix.SetText(picker, "[чт]");

            var size = new Size(220, 40);
            picker.Measure(size);
            picker.Arrange(new Rect(size));
            picker.UpdateLayout();

            var text = picker.Template.FindName("PART_TextBox", picker) as TextBox;
            var button = picker.Template.FindName("PART_Button", picker) as Button;
            Assert.NotNull(text);
            Assert.NotNull(button);

            Assert.Equal(size.Width - button.ActualWidth - 4, text.ActualWidth, 1);
        });
    }

    /// <summary>
    /// Крестик очистки даты: без команды в поле его нет. Шаблон общий, и окно дня, где чистить
    /// нечего, не должно получить лишнюю кнопку.
    /// </summary>
    [Fact]
    public void Шаблон_Крестик_БезКомандыСкрыт()
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(new DateTime(2026, 11, 12));

            picker.ApplyTemplate();

            var clear = picker.Template.FindName("ClearButton", picker) as Button;

            Assert.NotNull(clear);
            Assert.Equal(Visibility.Collapsed, clear.Visibility);
        });
    }

    /// <summary>Команда задана, но дата не выбрана — очищать нечего, крестик скрыт.</summary>
    [Fact]
    public void Шаблон_Крестик_БезДатыСкрыт()
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(null);
            DatePickerClear.SetCommand(picker, new RelayCommand(() => { }));

            picker.ApplyTemplate();

            var clear = picker.Template.FindName("ClearButton", picker) as Button;

            Assert.NotNull(clear);
            Assert.Equal(Visibility.Collapsed, clear.Visibility);
        });
    }

    /// <summary>
    /// Дата выбрана и команда задана — крестик виден и по нажатию выполняет команду модели.
    /// </summary>
    /// <remarks>
    /// Нажатие — через <see cref="UIElementAutomationPeer"/>: <c>RaiseEvent(ClickEvent)</c>
    /// команду кнопки не выполняет, и проверка прошла бы на пустом месте.
    /// </remarks>
    [Fact]
    public void Крестик_ДатаИКоманда_ВиденИВыполняетКоманду()
    {
        StaRunner.Run(() =>
        {
            var executed = false;

            var picker = CreatePicker(new DateTime(2026, 11, 12));
            DatePickerClear.SetCommand(picker, new RelayCommand(() => executed = true));

            picker.ApplyTemplate();

            var clear = picker.Template.FindName("ClearButton", picker) as Button;

            Assert.NotNull(clear);
            Assert.Equal(Visibility.Visible, clear.Visibility);

            var invoker = (IInvokeProvider)UIElementAutomationPeer
                .CreatePeerForElement(clear)!
                .GetPattern(PatternInterface.Invoke);

            invoker.Invoke();

            clear.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

            Assert.True(executed);
        });
    }

    /// <summary>
    /// Правый резерв текста равен тому, что в нём лежит: крестик, подпись дня или оба.
    /// </summary>
    /// <remarks>
    /// Раньше 48 px стояли всегда, и в шапке главного окна, где подписи дня нет, пустые
    /// 26 px читались белой плашкой, а дата обрезалась на её границе.
    /// </remarks>
    [Theory]
    [InlineData("", false, 5)]
    [InlineData("", true, 26)]
    [InlineData("[чт]", false, 34)]
    [InlineData("[чт]", true, 48)]
    public void Шаблон_Резерв_ПоТомуЧтоЛежит(string suffix, bool hasCommand, double expectedRight)
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(new DateTime(2026, 11, 12));
            DatePickerSuffix.SetText(picker, suffix);

            if (hasCommand)
            {
                DatePickerClear.SetCommand(picker, new RelayCommand(() => { }));
            }

            picker.ApplyTemplate();

            var text = picker.Template.FindName("PART_TextBox", picker) as TextBox;
            Assert.NotNull(text);

            Assert.Equal(expectedRight, text.Padding.Right);
        });
    }

    /// <summary>
    /// При максимальном кегле (150 % от базового 11) дата помещается в поле целиком:
    /// резерв под крестик и кнопка календаря не отнимают у неё больше, чем нужно.
    /// </summary>
    [Fact]
    public void Поле_ДатаВлезаетЦеликом_ПриКрупномКегле()
    {
        StaRunner.Run(() =>
        {
            var picker = CreatePicker(new DateTime(2026, 11, 12));
            DatePickerClear.SetCommand(picker, new RelayCommand(() => { }));
            picker.SelectedDateFormat = DatePickerFormat.Short;
            picker.FontSize = 16.5;

            var size = new Size(170, 40);
            picker.Measure(size);
            picker.Arrange(new Rect(size));
            picker.UpdateLayout();

            var text = picker.Template.FindName("PART_TextBox", picker) as TextBox;
            Assert.NotNull(text);

            var available = text.ActualWidth
                - text.BorderThickness.Left
                - text.BorderThickness.Right
                - text.Padding.Left
                - text.Padding.Right;

            var formatted = new FormattedText(
                text.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                text.FontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(text).PixelsPerDip);

            Assert.True(
                formatted.WidthIncludingTrailingWhitespace <= available,
                $"Дата «{text.Text}» шириной {formatted.WidthIncludingTrailingWhitespace:F1} "
                + $"не влезает в {available:F1}.");
        });
    }

    /// <summary>Поле даты с палитрой и стилями: без них шаблон взял бы системный вид.</summary>
    private static DatePicker CreatePicker(DateTime? date)
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
