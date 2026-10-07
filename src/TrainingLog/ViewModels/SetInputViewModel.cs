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
///
/// Третье состояние поля повторений — <see cref="RepetitionPlaceholder"/>, подсказка из
/// прошлого раза. Она рисуется, но не разбирается и не сохраняется.
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
    [NotifyPropertyChangedFor(nameof(HasRepetitionPlaceholder))]
    private string _repetitionText = string.Empty;

    /// <summary>
    /// Повторения прошлого раза — плейсхолдером, то есть подсказкой, а не значением.
    /// </summary>
    /// <remarks>
    /// Вес подтягивается прошлым значением, а повторения — только подсказкой: подходы одного
    /// упражнения отличаются повторениями гораздо чаще, чем весом, и молчаливая подстановка
    /// прошлого числа записала бы в журнал то, чего в этот раз не делали.
    ///
    /// Плейсхолдер намеренно не участвует ни в <see cref="HasData"/>, ни в разборе: это
    /// подсказка, а не введённое значение, и сохраняться она не должна. Пустое поле в таком
    /// подходе разбирается как ноль — ровно как и без подсказки.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRepetitionPlaceholder))]
    private string _repetitionPlaceholder = string.Empty;

    /// <summary>
    /// Показывать ли плейсхолдер повторений: подсказка есть, а поле ещё пусто.
    /// </summary>
    /// <remarks>
    /// Гаснет по <see cref="RepetitionText"/>, а не по фокусу поля: как только пользователь
    /// начал набирать, подсказка больше не нужна, и оставлять её под введённым числом незачем.
    /// Уведомление поднято обоими полями — иначе смена подсказки при пересборке упражнения
    /// не обновила бы плейсхолдер, а он и рисуется только этим признаком.
    /// </remarks>
    public bool HasRepetitionPlaceholder =>
        RepetitionPlaceholder.Length > 0 && RepetitionText.Length == 0;

    /// <summary>
    /// Пригодны ли оба поля для сохранения.
    /// </summary>
    public bool IsValid => TryParseWeight(out _) && TryParseRepetitions(out _);

    /// <summary>
    /// Введён ли хоть один из двух значений. Пустой подход в день не попадает: иначе кнопка
    /// «Сохранить» была бы активна с пустым окном на руках.
    /// </summary>
    /// <remarks>
    /// Плейсхолдер <see cref="RepetitionPlaceholder"/> данными не считается: подсказка о
    /// прошлом разе не должна ни включать подход в запись, ни гасить надпись о несохранённом.
    /// </remarks>
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