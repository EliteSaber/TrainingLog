using System.Collections.ObjectModel;

namespace TrainingLog.ViewModels;

/// <summary>
/// Приводит видимую коллекцию к целевому порядку, не пересоздавая её элементы.
/// </summary>
/// <remarks>
/// Порядок действий коллекции — часть контракта: перестановка идёт через
/// <see cref="ObservableCollection{T}.Move"/>, потому что контейнер строки переезжает вместе с
/// элементом, и состояние контейнера (у строки плана это <c>Expander.IsExpanded</c>) сохраняется.
/// Вариант «очистить и добавить заново» выглядит проще, но убивает все раскрытые планы, а список
/// пересобирается на каждый ввод в поиск и на каждое переключение сортировки.
///
/// Квадратичным здесь было одно — поиск элемента. «Есть ли такой в цели» и «где он в коллекции»
/// просматривали список целиком, и на списке в сотни строк ввод в поиск стоил десятки тысяч
/// сравнений. Здесь оба ответа берутся из подготовленных структур: множества цели и очередей
/// мест, а место элемента в коллекции считается деревом Фенвика по непоставленным элементам.
///
/// Совпадение считается по самому элементу, а не по наименованию: два разных плана в базе вправе
/// называться одинаково, и переставлять их местами нельзя.
/// </remarks>
internal static class VisibleSync
{
    /// <summary>Приводит коллекцию к целевому порядку.</summary>
    /// <param name="current">Видимая коллекция, которую приводят к целевому порядку.</param>
    /// <param name="target">Порядок, который должен получиться.</param>
    /// <remarks>
    /// Пустые элементы не принимаются: словарь мест не знает пустого ключа, а список строк
    /// плана или упражнения пустых элементов не содержит.
    /// </remarks>
    public static void Sync<T>(ObservableCollection<T> current, IReadOnlyList<T> target)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);

        var comparer = EqualityComparer<T>.Default;

        // Ушедшие из цели элементы убираются с конца: так индексы ещё не просмотренных не съезжают.
        var wanted = new HashSet<T>(target, comparer);

        for (var index = current.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(current[index]))
            {
                current.RemoveAt(index);
            }
        }

        // Дальше раскладываются оставшиеся. Пока идёт раскладка, коллекция устроена так: спереди
        // стоят уже поставленные элементы (цель по порядку), за ними — ещё не поставленные, в том
        // же порядке, в каком они стояли до раскладки. Отсюда и место элемента в коллекции: номер
        // позиции плюс число непоставленных перед ним.
        var pending = new List<T>(current);
        var slots = new SlotMap<T>(pending, comparer);
        var ranks = new PendingRanks(pending.Count);

        for (var position = 0; position < target.Count; position++)
        {
            var item = target[position];

            if (!slots.TryTake(item, out var slot))
            {
                // Такого элемента в коллекции нет: пока непоставленные есть, он вставляется на своё
                // место, а если не осталось и их — добавляется в конец.
                if (ranks.Count == 0)
                {
                    current.Add(item);
                }
                else
                {
                    current.Insert(position, item);
                }

                continue;
            }

            var index = position + ranks.CountBefore(slot);

            ranks.MarkPlaced(slot);

            if (index != position)
            {
                current.Move(index, position);
            }
        }
    }

    /// <summary>Места элементов по значению: очередь индексов для каждого значения.</summary>
    /// <remarks>
    /// Очередь, а не одно место: одно значение может встречаться в коллекции несколько раз, и
    /// берётся первое ещё не поставленное вхождение — так порядок внутри равных сохраняется.
    /// </remarks>
    private sealed class SlotMap<T>
        where T : notnull
    {
        private readonly Dictionary<T, Queue<int>> _slots;

        public SlotMap(IReadOnlyList<T> items, IEqualityComparer<T> comparer)
        {
            _slots = new Dictionary<T, Queue<int>>(comparer);

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];

                if (!_slots.TryGetValue(item, out var queue))
                {
                    queue = new Queue<int>();

                    _slots.Add(item, queue);
                }

                queue.Enqueue(index);
            }
        }

        /// <summary>Берёт место первого ещё не взятого элемента с таким значением.</summary>
        /// <param name="item">Значение, место которого берётся.</param>
        /// <param name="slot">Место элемента в исходном порядке коллекции.</param>
        /// <returns>Нашлось ли место.</returns>
        public bool TryTake(T item, out int slot)
        {
            if (_slots.TryGetValue(item, out var queue) && queue.Count > 0)
            {
                slot = queue.Dequeue();

                return true;
            }

            slot = -1;

            return false;
        }
    }

    /// <summary>Число непоставленных элементов и их места.</summary>
    /// <remarks>
    /// Дерево Фенвика отвечает на вопрос «сколько непоставленных перед этим», не пересчитывая
    /// список. Пересчёт при каждой перестановке стоил бы квадратичного времени — ровно того,
    /// ради чего всё затевалось.
    /// </remarks>
    private sealed class PendingRanks
    {
        private readonly int[] _tree;

        public PendingRanks(int size)
        {
            _tree = new int[size + 1];

            // Все места заняты: сумма по любому отрезку равна его длине, а в дереве Фенвика это
            // ровно младший установленный бит индекса.
            for (var index = 1; index <= size; index++)
            {
                _tree[index] = index & -index;
            }

            Count = size;
        }

        /// <summary>Число непоставленных элементов.</summary>
        public int Count { get; private set; }

        /// <summary>Помечает элемент поставленным на своё место.</summary>
        public void MarkPlaced(int slot)
        {
            Count--;

            for (var index = slot + 1; index < _tree.Length; index += index & -index)
            {
                _tree[index]--;
            }
        }

        /// <summary>Число непоставленных элементов перед указанным местом.</summary>
        public int CountBefore(int slot)
        {
            var sum = 0;

            for (var index = slot; index > 0; index -= index & -index)
            {
                sum += _tree[index];
            }

            return sum;
        }
    }
}
