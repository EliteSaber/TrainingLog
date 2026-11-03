namespace TrainingLog.Tests;

/// <summary>Запускает действие на STA-нитке: WPF-объекты вне STA не создаются.</summary>
/// <remarks>
/// Постоянная обвязка вместо одноразовых харнессах: подключаемые свойства и элементы WPF
/// в тестах требует STA-нити, и до этой обвязки тесты их не проверяли вовсе. Исключение
/// действия ловится и перебрасывается на вызывающую нитку — тест честно падает, а не молчит.
/// </remarks>
internal static class StaRunner
{
    /// <summary>Исполняет действие в STA-нитке и дожидается её окончания.</summary>
    public static void Run(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw failure;
        }
    }
}
