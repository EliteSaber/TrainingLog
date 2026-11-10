using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TrainingLog.Controls;

/// <summary>
/// Полоса заголовка вместо системной: логотип, название окна и кнопки свернуть, развернуть,
/// закрыть. Перетаскивание и захват за края делает WindowChrome (Themes/Chrome.xaml), контрол
/// отвечает только за содержимое.
/// </summary>
/// <remarks>
/// Название читается у окна (Title), а не держится свойством контрола: привязки вида
/// <c>Title="{Binding Title}"</c> продолжают работать без изменений — у главного окна и окна
/// дня заголовок задаёт модель представления.
///
/// Команды SystemCommands управляют окном по ссылке <c>Window.GetWindow</c>, потому что
/// <c>SystemCommands.*Command</c> без CommandBinding для окна не привязываются: у окна нужен
/// обработчик в любом случае, и путь через события кнопок короче. Окно дня закрывается
/// «Закрыть» так же, как и Esc: без DialogResult, модель оповещают события сохранения, а не
/// результат диалога.
/// </remarks>
public partial class TitleBar : UserControl
{
    public TitleBar()
    {
        InitializeComponent();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is not null)
        {
            SystemCommands.MinimizeWindow(window);
        }
    }

    private void OnStateClick(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(window);
        }
        else
        {
            SystemCommands.MaximizeWindow(window);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is not null)
        {
            SystemCommands.CloseWindow(window);
        }
    }
}
