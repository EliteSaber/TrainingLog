using System.Windows.Controls;
using TrainingLog.Controls;

namespace TrainingLog.Tests;

/// <summary>Подписка выделения строк: тип делегата обязан совпадать с типом события.</summary>
public sealed class RowSelectionTests
{
    /// <summary>Применение и снятие Enabled не бросает: делегат подписки типизирован событием.</summary>
    /// <remarks>
    /// Регрессия ловушки №24: метод-группа в параметре AddHandler'а типа Delegate компилятор
    /// оборачивал в natural-тип вместо MouseButtonEventHandler, и WPF рантаймом бросал
    /// «Несоответствие типа обработчика» при каждом применении свойства.
    /// </remarks>
    [Fact]
    public void Enabled_ПрименениеИСнятие_НеБросает()
    {
        StaRunner.Run(() =>
        {
            var list = new ListBox();

            RowSelection.SetEnabled(list, true);
            RowSelection.SetEnabled(list, false);
        });
    }
}
