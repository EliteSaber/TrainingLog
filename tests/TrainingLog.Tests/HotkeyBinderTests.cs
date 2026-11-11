using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TrainingLog.Controls;
using TrainingLog.Core.Settings;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>Нажатие Window-кнопки биндером: выключенная молчит, включённая исполняет команду.</summary>
public sealed class HotkeyBinderTests
{
    /// <summary>Клавиша по выключенной кнопке — тишина, а не исключение.</summary>
    /// <remarks>
    /// Регрессия: кнопки «‹»/«›» окна дня на краях списка глушатся ложным CanExecute команды, и
    /// Invoke автоматизационного провайдера бросал ElementNotEnabledException. Окно не
    /// показывается: биндеру хватает логического дерева, чтобы кнопку найти, а провайдеру —
    /// элемента. Прямая правка IsEnabled, а не ложный CanExecute: ButtonBase пересчитывает
    /// IsEnabled через CommandManager, и без рантайма окна он недетерминирован.
    /// </remarks>
    [Fact]
    public void Нажатие_ВыключеннаяКнопка_Молчит()
    {
        StaRunner.Run(() =>
        {
            var (window, button, command) = WindowWithButton();
            button.IsEnabled = false;

            using var settings = AssignedDayPrevious();
            new HotkeyBinder(window, new HotkeyService(settings.Store)).Rebuild();

            Press(window);

            Assert.Equal(0, command.Executions);
        });
    }

    /// <summary>Клавиша по включённой кнопке исполняет команду: запрет не расширять на все кнопки.</summary>
    [Fact]
    public void Нажатие_ВключеннаяКнопка_ИсполняетКоманду()
    {
        StaRunner.Run(() =>
        {
            var (window, button, command) = WindowWithButton();

            using var settings = AssignedDayPrevious();
            new HotkeyBinder(window, new HotkeyService(settings.Store)).Rebuild();

            Assert.True(button.IsEnabled);

            Press(window);

            Assert.Equal(1, command.Executions);
        });
    }

    /// <summary>Имитация нажатия: исполнитель KeyBinding биндера, как при настоящей клавише.</summary>
    private static void Press(Window window)
    {
        window.InputBindings.OfType<KeyBinding>().Single().Command!.Execute(null);

        // Invoke провайдера кладёт клик в очередь с приоритетом Input: без прокачки диспетчера
        // на тестовой STA-нитке нажатие так и осталось бы нераспечатанным.
        window.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
    }

    /// <summary>Окно с одной Window-кнопкой слота «предыдущее упражнение» и с команду-счётчиком.</summary>
    private static (Window Window, Button Button, BoundCommand Command) WindowWithButton()
    {
        var command = new BoundCommand();
        var button = new Button { Command = command };

        Hotkey.SetId(button, "Day.Previous");

        var window = new Window { Content = button };

        return (window, button, command);
    }

    /// <summary>Назначение в файле: у «Day.Previous» умолчания нет — как у пользователя на деле.</summary>
    private static TemporarySettings AssignedDayPrevious()
    {
        var settings = new TemporarySettings();
        settings.Store.Save(new AppSettings
        {
            Hotkeys = new Dictionary<string, string> { ["Day.Previous"] = "Ctrl+Alt+P" },
        });

        return settings;
    }

    /// <summary>Команда-счётчик: факт исполнения виднее CanExecute, того проверяет сам тест.</summary>
    private sealed class BoundCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executions++;

        public int Executions { get; private set; }
    }
}
