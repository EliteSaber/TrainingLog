using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingLog.Core.Settings;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>
/// Палитры тем: у тёмной те же ключи, что у светлой.
/// </summary>
/// <remarks>
/// Опечатка в ключе тихо оставила бы токен в прежней теме: приложение собралось бы, а часть
/// интерфейса осталась бы светлой. Проверка ключей ловит это до запуска.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class ThemePaletteTests
{
    /// <summary>Наборы ключей у палитр совпадают: палитра — контракт, а не украшение.</summary>
    [Fact]
    public void Палитры_ОдинаковыйНаборКлючей()
    {
        StaRunner.Run(() =>
        {
            var light = LoadPalette("Palette.Light.xaml");
            var dark = LoadPalette("Palette.Dark.xaml");

            Assert.Equal(Keys(light), Keys(dark));
        });
    }

    /// <summary>Ключи палитры по алфавиту: сравнение множеств не должно зависеть от порядка.</summary>
    private static string[] Keys(ResourceDictionary palette) =>
        palette.Keys
            .Cast<object>()
            .Select(key => key.ToString() ?? string.Empty)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Палитра из сборки приложения: адрес относительный, с указанием сборки, — иначе в
    /// тестовом хосте искался бы пакет приложения-теста, а не словарь программы.
    /// </summary>
    private static ResourceDictionary LoadPalette(string name)
    {
        var assembly = typeof(AppTheme).Assembly.GetName().Name;

        var uri = new Uri($"/{assembly};component/Themes/{name}", UriKind.Relative);

        return (ResourceDictionary)Application.LoadComponent(uri)!;
    }

    /// <summary>
    /// Значения палитры меняются на значения другой темы: иначе переключатель ничего бы не
    /// перекрасил.
    /// </summary>
    [Fact]
    public void ApplyTo_Тёмная_ЗначенияПалитрыСтановятсяТёмными()
    {
        StaRunner.Run(() =>
        {
            var live = LoadPalette("Palette.Light.xaml");
            var dark = LoadPalette("Palette.Dark.xaml");

            AppTheme.ApplyTo(live, ThemeRules.Dark);

            Assert.Equal(ThemeRules.Dark, live[AppTheme.PaletteIdKey]);

            // Сравнение с тёмной палитрой, а не с зашитым цветом: палитра — дело вкуса, и
            // тест должен ломаться от ошибки подмены, а не от правки оттенка.
            Assert.Equal(
                ((SolidColorBrush)dark["WindowBackground"]).Color,
                ((SolidColorBrush)live["WindowBackground"]).Color);
        });
    }

    /// <summary>
    /// Ссылка DynamicResource перечитывает значение: без этого окна остались бы в прежней теме.
    /// </summary>
    /// <remarks>
    /// Окно настоящее: отложенная ссылка на ресурс разрешается, когда элемент стоит в дереве,
    /// и без окна дефект просто не воспроизводится. Окно уводится за экран и не активируется.
    /// </remarks>
    [Fact]
    public void ApplyTo_Тёмная_СсылкаDynamicResource_ПолучаетНовыйЦвет()
    {
        StaRunner.Run(() =>
        {
            var palette = LoadPalette("Palette.Light.xaml");

            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(palette);

            var border = new Border();
            border.Resources.MergedDictionaries.Add(resources);
            border.SetResourceReference(Border.BackgroundProperty, "WindowBackground");

            var window = new Window
            {
                Width = 1,
                Height = 1,
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Content = border,
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

                var before = ((SolidColorBrush)border.Background).Color;

                AppTheme.ApplyTo(palette, ThemeRules.Dark);

                var after = ((SolidColorBrush)border.Background).Color;

                Assert.NotEqual(before, after);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
