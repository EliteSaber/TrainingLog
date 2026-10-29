namespace TrainingLog.Core.Settings;

/// <summary>
/// Правило наименования файла базы: в файле настроек хранится настоящее имя файла — всегда с
/// расширением, а в поле ввода его показывают без него. Отдельным классом, а не свойствами у
/// <see cref="AppSettings"/>, как и <c>DaysCountRules</c>: правило одно на окно параметров и на
/// тесты, и дублировать его в двух местах нельзя.
/// </summary>
public static class DatabaseNameRules
{
    /// <summary>Единственное расширение файла базы.</summary>
    public static readonly string Extension = ".db";

    /// <summary>
    /// Имя файла базы для показа в поле ввода: без хвостового расширения. Регистр хвоста не
    /// важен: файл мог называться <c>exercises.DB</c>, и поле не должно показывать мёртвый хвост.
    /// </summary>
    /// <remarks>
    /// Срезается только хвост: точки внутри имени остаются, у <c>журнал.2026.db</c> исчезает
    /// лишь <c>.db</c>, и после сохранения имя соберётся обратно.
    /// </remarks>
    public static string ForDisplay(string fileName) =>
        fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^Extension.Length]
            : fileName;

    /// <summary>
    /// Поле ввода — имя файла базы: хвостовой <c>.db</c>, впечатанный пользователем, снимается,
    /// после чего расширение дописывается всегда. Иначе сами добавили бы второе: «база.db»
    /// превратилась бы в «база.db.db».
    /// </summary>
    public static string ToFileName(string input) =>
        ForDisplay(input) + Extension;
}
