using System.Globalization;

namespace TrainingLog.Tests;

/// <summary>
/// Культура на время проверки: сокращение дня недели берётся у <see cref="CultureInfo.CurrentCulture"/>,
/// и без фиксации ожидаемое значение зависело бы от культуры машины. Прежняя культура возвращается
/// в <see cref="Dispose"/>.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    private CultureScope(string name) => CultureInfo.CurrentCulture = new CultureInfo(name);

    public static CultureScope Use(string name) => new(name);

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
