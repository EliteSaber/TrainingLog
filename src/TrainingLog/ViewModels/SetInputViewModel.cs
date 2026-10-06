using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TrainingLog.ViewModels;

/// <summary>
/// Один подход в окне добавления дня: поле веса и поле повторений.
/// </summary>
/// <remarks>
/// Значения хранятся строками, а не числами: <see cref="decimal"/> и <see cref="int"/> в поле
/// ввода ломают ввод — запятая как десятичный разделитель не переваривается, а пустое поле
/// превращается в ноль ещё до того, как пользователь закончил. Разбор отложен до
/// сохранения, а <see cref="IsValid"/> следит за ним на лету, чтобы «Сохранить» гасла
/// на негодном тексте, а не падала при разборе.
/// </remarks>
public sealed partial class SetInputViewModel : ObservableObject
{
    private const NumberStyles NumberStyle = NumberStyles.Number | NumberStyles.AllowDecimalPoint;

    /// <summary>
    /// Вес подхода в килограммах. Пусто — вес не указан.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private string _weightText = string.Empty;

    /// <summary>
    /// Количество повторений. Пусто — не указано.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private string _repetitionText = string.Empty;

    /// <summary>
    /// Пригодны ли оба поля для сохранения.
    /// </summary>
    public bool IsValid => TryParseWeight(out _) && TryParseRepetitions(out _);

    /// <summary>
    /// Введён ли хоть один из двух значений. Пустой подход в день не попадает: иначе кнопка
    /// «Сохранить» была бы активна с пустым окном на руках.
    /// </summary>
    public bool HasData => !string.IsNullOrWhiteSpace(WeightText) || !string.IsNullOrWhiteSpace(RepetitionText);

    /// <summary>
    /// Разбирает вес подхода. Пустое поле даёт ноль — упражнение без дополнительного веса.
    /// </summary>
    /// <param name="weight">Вес в килограммах.</param>
    /// <returns><c>false</c>, если текст не число или отрицательный.</returns>
    public bool TryParseWeight(out decimal weight)
    {
        if (string.IsNullOrWhiteSpace(WeightText))
        {
            weight = 0m;
            return true;
        }

        var text = WeightText.Trim();

        // Разбор сначала в текущей культуре, потом в инвариантной: в русской раскладке
        // десятичный разделитель — запятая, и точка в весе отвергается, а набирают её
        // привычкой. Значение с двумя разделителями не подходит ни под одну культуру —
        // это правильный отказ, а не повод угадывать.
        if (decimal.TryParse(text, NumberStyle, CultureInfo.CurrentCulture, out weight)
            || decimal.TryParse(text, NumberStyle, CultureInfo.InvariantCulture, out weight))
        {
            return weight >= 0m;
        }

        weight = 0m;
        return false;
    }

    /// <summary>
    /// Разбирает количество повторений. Пустое поле даёт ноль.
    /// </summary>
    /// <param name="repetitions">Количество повторений.</param>
    /// <returns><c>false</c>, если текст не целое число или отрицательное.</returns>
    public bool TryParseRepetitions(out int repetitions)
    {
        if (string.IsNullOrWhiteSpace(RepetitionText))
        {
            repetitions = 0;
            return true;
        }

        if (int.TryParse(RepetitionText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out repetitions))
        {
            return repetitions >= 0;
        }

        repetitions = 0;
        return false;
    }
}