using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;using TrainingLog.Controls;
using TrainingLog.Core.Settings;
using TrainingLog.Services;
using TrainingLog.ViewModels;
using TrainingLog.Windows;

namespace TrainingLog.Tests;

/// <summary>
/// Окно параметров, вкладка горячих клавиш: поиск, кнопки-переходы и перечень.
/// </summary>
/// <remarks>
/// Тесту нужна настоящая окно: разметка вкладки строится лениво (вкладка не создаётся, пока
/// её не выбрали), и без окна не соберётся ни один контейнер. Окно уводится за экран и не
/// активируется, чтобы тест не мигал и не забирал фокус у пользователя.
/// </remarks>
[Collection(WpfTestGroup.Name)]
public sealed class SettingsWindowTests
{
    /// <summary>Кнопки-переходы на месте: «Все окна» и по одной на каждое окно каталога.</summary>
    [Fact]
    public void КнопкиПереходы_НаМесте()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings);

            try
            {
                var chips = (ItemsControl)window.FindName("HotkeyChips")!;

                Assert.Equal(9, chips.Items.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Нажатие кнопки-перехода оставляет в перечне только строки своего окна.
    /// </summary>
    /// <remarks>
    /// Проверяется и команда кнопки, и её параметр: без привязки кнопка молча ничего бы не
    /// делала, а тест модели этого не заметил бы.
    /// </remarks>
    [Fact]
    public void КнопкаПерехода_ОставляетСтрокиСвоегоОкна()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings);

            try
            {
                var viewModel = (SettingsViewModel)window.DataContext;
                var chips = (ItemsControl)window.FindName("HotkeyChips")!;

                // Кнопка ищется по подписи, а не по номеру: нумерация кнопок — следствие
                // порядка окон каталога, и тест обязан читать то же, что читает человек.
                var index = viewModel.HotkeyChips
                    .Select((chip, position) => (chip, position))
                    .Single(pair => pair.chip.Label == "Планы")
                    .position;

                var button = ChipButton(chips, index);
                var chip = (HotkeyChipViewModel)button.CommandParameter!;

                Assert.NotNull(button.Command);

                button.Command.Execute(chip);

                window.UpdateLayout();

                var plansRows = viewModel.HotkeyGroups.Single(group => group.WindowName == "Планы").Rows;

                Assert.NotEmpty(viewModel.HotkeyListing);
                Assert.All(
                    viewModel.HotkeyListing.OfType<HotkeyRowViewModel>(),
                    row => Assert.Contains(row, plansRows));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Ввод в поиск сужает перечень: поле связано с моделью.</summary>
    [Fact]
    public void Поиск_СужаетПеречень()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings);

            try
            {
                var viewModel = (SettingsViewModel)window.DataContext;
                var search = (TextBox)window.FindName("HotkeySearchBox")!;

                search.Text = "Экспорт";

                window.UpdateLayout();

                var rows = viewModel.HotkeyListing.OfType<HotkeyRowViewModel>().ToList();

                Assert.Single(rows);
                Assert.Equal("Exercises.Export", rows[0].Id);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Вкладка размеров: строка на каждое окно приложения, по две колонки чисел.
    /// </summary>
    [Fact]
    public void ВкладкаОкон_СтрокаНаКаждоеОкно()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings, tab: 2);

            try
            {
                var list = (ItemsControl)window.FindName("WindowSizeList")!;

                Assert.Equal(WindowSizeCatalog.Entries.Count, list.Items.Count);

                // Шаблон строки построен, и поле ширины связано с перетаскиванием: иначе
                // перетаскивание молча не работало бы, а тест модели этого не заметил бы.
                var container = list.ItemContainerGenerator.ContainerFromIndex(0)!;
                var box = FindBox(container);

                Assert.NotNull(box);
                Assert.NotNull(DragValue.GetCommand(box!));
                Assert.Equal(
                    ((WindowSizeRowViewModel)list.Items[0]).Width,
                    DragValue.GetValue(box!));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// «Сохранить» закрывает окно: окно немодальное, и ответ «принято» ему возвращать нечем.
    /// </summary>
    [Fact]
    public void Save_ЗакрываетОкно()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings, tab: 0);

            try
            {
                var viewModel = (SettingsViewModel)window.DataContext;

                viewModel.SaveCommand.Execute(null);

                Assert.False(window.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// «Отменить» закрывает окно без записи: в немодальном окне <c>IsCancel</c> не работает,
    /// и закрывает его обработчик кнопки.
    /// </summary>
    [Fact]
    public void Отмена_ЗакрываетОкноБезЗаписи()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings, tab: 0);

            try
            {
                var button = ButtonByHotkeyId(window, "Settings.Cancel");

                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.False(window.IsVisible);
                Assert.Empty(settings.Store.Load().WindowSizes);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Правка размера применяется к окнам сразу: предпросмотр, как у шрифта и темы. Файл при
    /// этом не меняется — он пишется по «Сохранить».
    /// </summary>
    [Fact]
    public void ПравкаРазмера_ПрименяетсяСразу()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            settings.Store.Save(new AppSettings
            {
                WindowSizes = new Dictionary<string, WindowSize>
                {
                    [WindowSizeCatalog.Main] = new WindowSize { Width = 1000, Height = 700 },
                },
            });

            var window = OpenWindow(settings, tab: 2);

            try
            {
                var viewModel = (SettingsViewModel)window.DataContext;

                // Текущие размеры — общее состояние приложения, и тест задаёт его сам, чтобы
                // не зависеть от порядка прогонов.
                WindowSizes.Initialize(new Dictionary<string, WindowSize>
                {
                    [WindowSizeCatalog.Main] = new WindowSize { Width = 1000, Height = 700 },
                });

                viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main).Width = 1200;

                Assert.Equal(1200, WindowSizes.Current[WindowSizeCatalog.Main].Width);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Выход без сохранения возвращает размеры как было: предпросмотр откатывается к срезу
    /// файла, как шрифт и тема.
    /// </summary>
    [Fact]
    public void Отмена_РазмерыВозвращаютсяКакБыло()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            settings.Store.Save(new AppSettings
            {
                WindowSizes = new Dictionary<string, WindowSize>
                {
                    [WindowSizeCatalog.Main] = new WindowSize { Width = 1000, Height = 700 },
                },
            });

            var window = OpenWindow(settings, tab: 2);

            try
            {
                var viewModel = (SettingsViewModel)window.DataContext;

                // Текущие размеры — общее состояние приложения, и тест задаёт его сам, чтобы
                // не зависеть от порядка прогонов.
                WindowSizes.Initialize(new Dictionary<string, WindowSize>
                {
                    [WindowSizeCatalog.Main] = new WindowSize { Width = 1000, Height = 700 },
                });

                viewModel.WindowSizeRows.Single(row => row.Id == WindowSizeCatalog.Main).Width = 1200;

                window.Close();

                Assert.Equal(1000, WindowSizes.Current[WindowSizeCatalog.Main].Width);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Смена окна возвращает перечень к началу: иначе он остался бы прокрученным на прежнем месте.
    /// </summary>
    /// <remarks>
    /// Прокрутка при смене содержимого не сбрасывается сама — WPF только подрезает её по новой
    /// высоте, — и перечень показывался бы с середины.
    /// </remarks>
    [Fact]
    public void СменаОкна_ПереченьВозвращаетсяКНачалу()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings);

            try
            {
                var chips = (ItemsControl)window.FindName("HotkeyChips")!;
                var list = (ItemsControl)window.FindName("HotkeyList")!;
                var scroll = (ScrollViewer)list.Template.FindName("HotkeyScroll", list)!;

                scroll.ScrollToVerticalOffset(5);
                window.UpdateLayout();

                Assert.True(scroll.VerticalOffset > 0, "Перечень не прокрутился — проверять нечего.");

                var button = ChipButton(chips, 2);

                button.Command!.Execute(button.CommandParameter);

                window.UpdateLayout();

                Assert.Equal(0, scroll.VerticalOffset);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Выбранный сегмент помечен: акцентная линия снизу и утопленное содержимое.
    /// </summary>
    /// <remarks>
    /// Проверяется то, что читает глаз: линия под выбранным сегментом — акцентная и вдвое
    /// выше обычной, а содержимое сдвинуто вниз на пиксель. Ошибка в разметке убрала бы
    /// выделение совсем, и это видно только глазами.
    /// </remarks>
    [Fact]
    public void ВыбранныйСегмент_ПомеченАкцентом()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings);

            try
            {
                var viewModel = (SettingsViewModel)window.DataContext;
                var chips = (ItemsControl)window.FindName("HotkeyChips")!;

                var index = viewModel.HotkeyChips
                    .Select((chip, position) => (chip, position))
                    .Single(pair => pair.chip.Label == "Планы")
                    .position;

                var button = ChipButton(chips, index);

                // До выбора сегмент обычный: линия тонкая и серая.
                var before = (Rectangle)button.Template.FindName("BottomLine", button)!;

                Assert.Equal(1, before.Height);
                Assert.Equal(PaletteColor(button, "LineSoft"), ((SolidColorBrush)before.Fill).Color);

                button.Command!.Execute(button.CommandParameter);

                window.UpdateLayout();

                var after = (Rectangle)button.Template.FindName("BottomLine", button)!;

                Assert.Equal(2, after.Height);
                Assert.Equal(PaletteColor(button, "Accent"), ((SolidColorBrush)after.Fill).Color);

                // Содержимое утоплено: сдвиг на пиксель вниз.
                var content = (FrameworkElement)button.Template.FindName("Content", button)!;

                var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);

                Assert.Equal(1, shift.Y);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Прежний вид переходов сохранён и применим: стиль из ресурсов окна собирается.
    /// </summary>
    /// <remarks>
    /// Стиль лежит рядом с новым, но кнопками сейчас не используется — и ошибка в нём (например,
    /// ненайденный BasedOn) всплыла бы только в момент возврата прежнего вида. Поэтому стиль
    /// применяется здесь по-настоящему.
    /// </remarks>
    [Fact]
    public void ПрежнийВидПереходов_Сохранился()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings);

            try
            {
                var style = Assert.IsType<Style>(window.FindResource("HotkeyChipGlass"));

                var button = new Button { Style = style };

                // Стиль применился: ненайденный BasedOn роняет элемент в момент применения.
                Assert.NotNull(button.Template);
                Assert.Equal(28, button.MinHeight);
                Assert.Equal(new Thickness(4, 2, 4, 2), button.Padding);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Блок кнопок-переходов отделён от перечня границей во всю ширину.
    /// </summary>
    /// <remarks>
    /// Граница — нижняя граница самого блока кнопок, а не верхняя граница перечня: она
    /// примыкает к блоку вплотную, и без неё переходы и перечень сливаются в одно.
    /// </remarks>
    [Fact]
    public void ПолосаПереходов_ОтделенаГраницей()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings);

            try
            {
                var chips = (ItemsControl)window.FindName("HotkeyChips")!;
                var frame = Assert.IsType<Border>(chips.Parent);

                Assert.Equal(1, frame.BorderThickness.Bottom);
                Assert.Equal(0, frame.BorderThickness.Top);
                Assert.Equal(PaletteColor(frame, "LineSoft"), ((SolidColorBrush)frame.BorderBrush).Color);

                // Зазор между линиями сегментов и общей линией: без него они сливаются
                // в одну толстую полосу.
                Assert.Equal(1, frame.Padding.Bottom);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Крестик очистки поиска: появляется на непустом поле, чистит его по нажатию.
    /// </summary>
    /// <remarks>
    /// Нажатие — через <see cref="UIElementAutomationPeer"/>: <c>RaiseEvent(ClickEvent)</c>
    /// команду кнопки не выполняет (ловушка 7), и тест прошёл бы на пустом месте.
    /// </remarks>
    [Fact]
    public void КрестикОчистки_ЧиститПоиск()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings);

            try
            {
                var viewModel = (SettingsViewModel)window.DataContext;
                var search = (TextBox)window.FindName("HotkeySearchBox")!;
                var clear = (Button)window.FindName("ClearSearchButton")!;

                Assert.Equal(Visibility.Collapsed, clear.Visibility);

                search.Text = "Экспорт";

                window.UpdateLayout();

                Assert.Equal(Visibility.Visible, clear.Visibility);
                Assert.Single(viewModel.HotkeyListing.OfType<HotkeyRowViewModel>());

                Press(clear);

                window.UpdateLayout();

                Assert.True(string.IsNullOrEmpty(viewModel.HotkeySearch), $"модель: «{viewModel.HotkeySearch}»");
                Assert.True(string.IsNullOrEmpty(search.Text), $"поле: «{search.Text}»");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Крестик очистки поиска шрифта: появляется на непустом поле и чистит его.
    /// </summary>
    /// <remarks>
    /// Поле поиска шрифта живёт на вкладке «Общие», а окно для этого теста открывается на ней.
    /// </remarks>
    [Fact]
    public void КрестикОчисткиШрифта_ЧиститПоиск()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings, tab: 0);

            try
            {
                var viewModel = (SettingsViewModel)window.DataContext;
                var search = (TextBox)window.FindName("FontSearchBox")!;
                var clear = (Button)window.FindName("ClearFontSearchButton")!;

                Assert.Equal(Visibility.Collapsed, clear.Visibility);

                search.Text = "Segoe";

                window.UpdateLayout();

                Assert.Equal(Visibility.Visible, clear.Visibility);

                Press(clear);

                window.UpdateLayout();

                Assert.True(string.IsNullOrEmpty(viewModel.FontSearch), $"модель: «{viewModel.FontSearch}»");
                Assert.True(string.IsNullOrEmpty(search.Text), $"поле: «{search.Text}»");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Вкладка «Общие» прокручивается, когда содержимое не помещается в окно.
    /// </summary>
    /// <remarks>
    /// WPF окна не прокручивает сам, и лишнее уходило за край: уменьшили окно или увеличили
    /// шрифт — и часть полей пропадала. Окно уводится на минимальную высоту, где содержимое
    /// заведомо не помещается.
    /// </remarks>
    [Fact]
    public void Общие_ПрокручиваютсяКогдаНеПомещаются()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var window = OpenWindow(settings, tab: 0);

            try
            {
                var tabs = (TabControl)window.FindName("SettingsTabs")!;
                var scroll = Assert.IsType<ScrollViewer>(tabs.SelectedContent);

                window.Height = window.MinHeight;
                window.UpdateLayout();

                Assert.True(scroll.ScrollableHeight > 0, "Содержимое не помещается, а полосы нет.");
                Assert.Equal(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);

                // Полоса прижата к краю окна, а содержимое подходит к ней вплотную: отступ
                // справа снят, пока полоса на экране (как в главном окне).
                var frame = Assert.IsType<Border>(scroll.Content);

                Assert.Equal(0, frame.Margin.Right);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Нажатие кнопки: тот же приём, что в HotkeyBinder.</summary>
    private static void Press(Button button)
    {
        var invoker = (IInvokeProvider)UIElementAutomationPeer
            .CreatePeerForElement(button)!
            .GetPattern(PatternInterface.Invoke);

        invoker.Invoke();

        // Invoke провайдера кладёт клик в очередь с приоритетом Input: без прокачки
        // диспетчера на тестовой STA-нитке нажатие так и осталось бы нераспечатанным.
        button.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
    }

    /// <summary>Кнопка кнопки-перехода по её номеру в списке.</summary>
    /// <remarks>
    /// Контейнер элемента <c>ItemsControl</c> — это <c>ContentPresenter</c>, а кнопка из шаблона
    /// лежит его визуальным ребёнком: в самом контейнере лежит элемент данных, а не кнопка.
    /// </remarks>
    private static Button ChipButton(ItemsControl chips, int index)
    {
        var container = chips.ItemContainerGenerator.ContainerFromIndex(index)!;

        for (var child = 0; child < VisualTreeHelper.GetChildrenCount(container); child++)
        {
            if (VisualTreeHelper.GetChild(container, child) is Button button)
            {
                return button;
            }
        }

        throw new InvalidOperationException($"Кнопка под номером {index} не найдена.");
    }

    /// <summary>Поле ввода в поддереве: ищется первое, у строки размеров оно одно на колонку.</summary>
    /// <param name="root">Корень поиска.</param>
    private static TextBox? FindBox(DependencyObject root)
    {
        if (root is TextBox box)
        {
            return box;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindBox(VisualTreeHelper.GetChild(root, index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Кнопка окна по её адресу назначения: id ищется по дереву окна.</summary>
    /// <param name="window">Окно, в дереве которого ищется кнопка.</param>
    /// <param name="hotkeyId">Адрес назначения из каталога.</param>
    private static Button ButtonByHotkeyId(Window window, string hotkeyId) =>
        FindButton(window, hotkeyId)
            ?? throw new InvalidOperationException($"Кнопка «{hotkeyId}» в окне не найдена.");

    /// <summary>Ищет кнопку по адресу назначения в визуальном дереве окна.</summary>
    /// <param name="root">Корень поиска.</param>
    /// <param name="hotkeyId">Адрес назначения из каталога.</param>
    private static Button? FindButton(DependencyObject root, string hotkeyId)
    {
        if (root is FrameworkElement element && element is Button button && Hotkey.GetId(element) == hotkeyId)
        {
            return button;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindButton(VisualTreeHelper.GetChild(root, index), hotkeyId) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Цвет токена палитры так, как его видит элемент.</summary>
    private static Color PaletteColor(FrameworkElement element, string key) =>
        ((SolidColorBrush)element.TryFindResource(key)).Color;

    /// <summary>Палитра оформления из сборки приложения.</summary>
    private static ResourceDictionary LoadPalette()
    {
        var assembly = typeof(AppTheme).Assembly.GetName().Name;
        var uri = new Uri($"/{assembly};component/Themes/Palette.Light.xaml", UriKind.Relative);

        return (ResourceDictionary)Application.LoadComponent(uri)!;
    }

    /// <summary>Прокачка диспетчера: окну нужно дать исполнить отложенное.</summary>
    private static void Pump(Window window)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }
    }

    /// <summary>Окно параметров на указанной вкладке.</summary>
    /// <param name="settings">Файл настроек окна.</param>
    /// <param name="tab">Номер вкладки: 0 — «Общие», 1 — «Горячие клавиши».</param>
    private static SettingsWindow OpenWindow(TemporarySettings settings, int tab = 1)
    {
        var viewModel = new SettingsViewModel(settings.Store);
        var window = new SettingsWindow(viewModel, new HotkeyService(settings.Store));

        // Палитра в окне: без неё ссылка DynamicResource не разрешается, и проверять
        // выделение сегмента было бы не о чем (ловушка 6).
        window.Resources.MergedDictionaries.Add(LoadPalette());

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.ShowActivated = false;

        window.Show();
        window.UpdateLayout();

        // Вкладка создаётся лениво: пока её не выбрали, её содержимого нет.
        var tabs = (TabControl)window.FindName("SettingsTabs")!;

        tabs.SelectedIndex = tab;
        window.UpdateLayout();

        Pump(window);

        return window;
    }
}
