using System.Windows;
using System.Windows.Controls;
using TrainingLog.Services;

namespace TrainingLog.Tests;

/// <summary>Сборка привязок окна: обход дерева не должен падать на не-Visual узлах.</summary>
public sealed class HotkeyBinderTests
{
    /// <summary>Rebuild обходит дерево окна со столбцами Grid'а и не бросает.</summary>
    /// <remarks>
    /// Регрессия ловушки №25: логическими детьми Grid'а значатся и ColumnDefinition/RowDefinition —
    /// они DependencyObject, но не Visual, и VisualTreeHelper на них бросал «не является Visual
    /// или Visual3D» ровно при Rebuild. Поиск идёт по чужим id каталога через всё окно, поэтому
    /// биндер без явных регистраций доходит до столбцов гарантированно.
    /// </remarks>
    [Fact]
    public void Rebuild_СтолбцыГрида_НеБросает()
    {
        StaRunner.Run(() =>
        {
            using var settings = new TemporarySettings();
            var hotkeys = new HotkeyService(settings.Store);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition());

            var window = new Window { Content = grid };

            try
            {
                new HotkeyBinder(window, hotkeys).Rebuild();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
