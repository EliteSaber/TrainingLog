using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TrainingLog.Core.Models;

namespace TrainingLog.ViewModels;

/// <summary>
/// Упражнение плана в окне добавления дня: номер кнопки, название, его подходы и примечание.
/// </summary>
/// <remarks>
/// Отдельный класс, а не <see cref="Exercise"/> из справочника: «третье упражнение этого дня
/// и подходы, введённые в него» — свойство сеанса правки, а не упражнения. Родственник
/// <see cref="PlanExerciseSelection"/> из окна правки плана.
///
/// Примечание живёт здесь, а не в подходе: оно принадлежит упражнению, а не одному его
/// повторению, и в базе хранится в <see cref="ExerciseEntry.Notes"/> — на записи выполненного
/// упражнения, рядом с его названием.
/// </remarks>
public sealed partial class PlanExerciseViewModel : ObservableObject
{
    /// <param name="exercise">Упражнение из справочника.</param>
    /// <param name="number">Номер кнопки в плане, с единицы.</param>
    public PlanExerciseViewModel(Exercise exercise, int number)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        Exercise = exercise;
        Number = number;
    }

    /// <summary>Упражнение из справочника.</summary>
    public Exercise Exercise { get; }

    /// <summary>Название упражнения: показывается и по кнопкой, и по центру окна.</summary>
    public string Name => Exercise.Name;

    /// <summary>Номер кнопки в плане, с единицы.</summary>
    public int Number { get; }

    /// <summary>Активно ли это упражнение: его подходы показаны в центре окна.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Примечание к упражнению. Пусто — примечания нет.
    /// </summary>
    /// <remarks>
    /// Обновление уведомляется обоими признаками: <see cref="HasNote"/> отвечает на вопрос
    /// индикации в свёрнутом виде, а <see cref="HasNotePlaceholder"/> гаснет по введённому
    /// тексту, как и подсказка повторений.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    [NotifyPropertyChangedFor(nameof(HasNotePlaceholder))]
    private string _noteText = string.Empty;

    /// <summary>
    /// Примечание прошлого раза — плейсхолдером, то есть подсказкой, а не значением.
    /// </summary>
    /// <remarks>
    /// Тот же приём, что у повторений, и по той же причине: молчаливая подстановка прошлой
    /// заметки записала бы в журнал то, чего в этот раз не писали. Набор с весом в прошлый
    /// раз отличался от сегодняшнего, а примечание «болело левое плечо» к сегодняшнему
    /// подходу отношения не имеет.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotePlaceholder))]
    private string _notePlaceholder = string.Empty;

    /// <summary>
    /// Раскрыто ли поле примечания. Состояние живёт в строке упражнения, а не в окне:
    /// у каждого упражнения оно своё, и переключение «‹»/«›» не должно закрывать то, что
    /// пользователь открыл у соседнего.
    /// </summary>
    [ObservableProperty]
    private bool _isNotesExpanded;

    /// <summary>Подходы упражнения в порядке ввода — слева направо в центре окна.</summary>
    public ObservableCollection<SetInputViewModel> Sets { get; } = [];

    /// <summary>
    /// Введено ли примечание. Пробелы примечанием не считаются: пробелами его заполнить
    /// нельзя, а иначе пустое после стирания поле считалось бы данными.
    /// </summary>
    public bool HasNote => !string.IsNullOrWhiteSpace(NoteText);

    /// <summary>
    /// Показывать ли подсказку примечания: подсказка есть, а поле ещё пусто.
    /// </summary>
    public bool HasNotePlaceholder =>
        NotePlaceholder.Length > 0 && NoteText.Length == 0;

    /// <summary>
    /// Есть ли в упражнении что сохранять.
    /// </summary>
    /// <remarks>
    /// Упражнение без подходов в запись дня не попадает: план выполнен не целиком, а значит
    /// невыполненные упражнения в журнале лишние. Примечание — исключение, и оно осознанное:
    /// упражнение, к которому записали только примечание, в журнале тоже нужно. Молча выбросить
    /// набранный текст при сохранении хуже, чем показать лишнее упражнение.
    ///
    /// Плейсхолдер <see cref="NotePlaceholder"/> данными не считается — по той же причине, по
    /// какой он не участвует в <see cref="SetInputViewModel.HasData"/> у повторений.
    /// </remarks>
    public bool HasData => Sets.Any(set => set.HasData) || HasNote;

    /// <summary>
    /// Создаёт строку упражнения с одним пустым подходом.
    /// </summary>
    /// <remarks>
    /// Подход один и пустой, а не ноль: нулевой набор полей требовал бы первого нажатия «+»
    /// ради того, чтобы просто начать ввод. Лишний подход убирается кнопкой «−».
    /// </remarks>
    /// <param name="exercise">Упражнение из справочника.</param>
    /// <param name="number">Номер кнопки в плане.</param>
    public static PlanExerciseViewModel Create(Exercise exercise, int number) =>
        new(exercise, number)
        {
            Sets = { new SetInputViewModel() },
        };
}