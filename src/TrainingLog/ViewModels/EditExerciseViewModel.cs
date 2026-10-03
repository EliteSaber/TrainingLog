using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core;
using TrainingLog.Core.Models;
using TrainingLog.Core.Repositories;

namespace TrainingLog.ViewModels;

/// <summary>
/// Правка названия одного упражнения в модальном окне.
/// </summary>
public sealed partial class EditExerciseViewModel : ObservableObject
{
    private readonly Exercise _exercise;
    private readonly IExerciseRepository _repository;

    /// <summary>
    /// Все упражнения справочника, кроме редактируемого. Нужны для проверки дубликата
    /// без обращения к базе на каждое нажатие клавиши.
    /// </summary>
    private readonly ObservableCollection<Exercise> _others = [];
    public EditExerciseViewModel(Exercise exercise, IExerciseRepository repository)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(repository);

        _exercise = exercise;
        _repository = repository;
        Name = exercise.Name;
    }

    /// <summary>Название в поле ввода.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _name = string.Empty;

    /// <summary>
    /// Признак, что изменения приняты и сохранены. Окно выставляет <c>DialogResult</c> по нему.
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// «Принять» активна, только если название изменилось, не пустое и не занятое другим
    /// упражнением. Сохранение без изменений и добавленные пробелы изменением не считаются,
    /// поэтому кнопка в таких случаях неактивна.
    /// </summary>
    private bool CanAccept() => NameRules.IsValidEdit(Name, _exercise.Name, _others.Select(exercise => exercise.Name));

    /// <summary>
    /// Загружает справочник для проверки дубликатов. Вызывается окном при загрузке.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        var all = await _repository.GetAllAsync().ConfigureAwait(true);

        _others.Clear();
        foreach (var exercise in all)
        {
            if (exercise.Id != _exercise.Id)
            {
                _others.Add(exercise);
            }
        }

        AcceptCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private async Task AcceptAsync()
    {
        var candidate = new Exercise { Id = _exercise.Id, Name = Name };

        var outcome = await _repository.UpdateAsync(candidate).ConfigureAwait(true);

        if (outcome != UpdateExerciseOutcome.Updated)
        {
            return;
        }

        _exercise.Name = candidate.Name;
        Accepted = true;

        // Приватный сеттер не даёт сгенерировать уведомление автоматически, а окно ждёт его,
        // чтобы выставить DialogResult: по одному только значению Accepted оно не узнает,
        // что правка сохранена.
        OnPropertyChanged(nameof(Accepted));
    }
}
