namespace TrainingLog.ViewModels;

/// <summary>Отбор строк вкладки горячих клавиш: поиск, окно и «только изменённые».</summary>
/// <remarks>
/// Правило отбора живёт здесь, а не в модели представления: оно одно и то же для перечня,
/// для счётчика совпадений и для проверок, а правило в трёх местах разъехалось бы при первой
/// правке формулировки. Функции чистые — их проверяет тест без окна.
/// </remarks>
internal static class HotkeyFilter
{
    /// <summary>Совпадает ли строка с поисковым запросом.</summary>
    /// <remarks>
    /// Ищется по имени действия и по имени окна: пользователь знает либо что ищет («Сохранить»),
    /// либо где («Окно дня»). По техническому id не ищем — он не для человека, а по текущей
    /// комбинации тоже: она меняется во время захвата, и список прыгал бы из-под рук.
    /// </remarks>
    public static bool Matches(HotkeyRowViewModel row, string windowName, string query)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return true;
        }

        return Contains(row.DisplayText, trimmed) || Contains(windowName, trimmed);
    }

    /// <summary>Отличается ли комбинация строки от умолчания каталога.</summary>
    /// <remarks>
    /// Пустая строка и отсутствие умолчания — одно и то же: действие без умолчания, которому
    /// ничего не назначено, изменённым не считается.
    /// </remarks>
    public static bool IsChanged(HotkeyRowViewModel row) =>
        row.GestureText != (row.DefaultGesture ?? string.Empty);

    /// <summary>Собирает перечень вкладки: заголовок окна и его строки.</summary>
    /// <remarks>
    /// Заголовок без строк не попадает в перечень: пустой раздел выглядит мусором, и поиск
    /// оставил бы на экране обломок структуры. Объекты заголовков переиспользуются (они лежат
    /// в группе), иначе пересборка перечня пересоздавала бы их и гоняла контейнеры заново.
    /// </remarks>
    public static List<object> BuildListing(
        IReadOnlyList<HotkeyGroupViewModel> groups,
        string query,
        bool changedOnly,
        HotkeyGroupViewModel? selectedGroup)
    {
        List<object> listing = [];

        foreach (var group in groups)
        {
            // Выбранное окно — пересечение с поиском, а не замена ему: внутри окна искать можно.
            if (selectedGroup is not null && !ReferenceEquals(selectedGroup, group))
            {
                continue;
            }

            List<HotkeyRowViewModel> rows = [];

            foreach (var row in group.Rows)
            {
                if (changedOnly && !IsChanged(row))
                {
                    continue;
                }

                if (!Matches(row, group.WindowName, query))
                {
                    continue;
                }

                rows.Add(row);
            }

            if (rows.Count == 0)
            {
                continue;
            }

            listing.Add(group.Section);
            listing.AddRange(rows);
        }

        return listing;
    }

    private static bool Contains(string text, string query) =>
        text.Contains(query, StringComparison.OrdinalIgnoreCase);
}
