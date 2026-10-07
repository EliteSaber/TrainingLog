using System.ComponentModel;
using System.Windows;
using TrainingLog.Controls;
using TrainingLog.ViewModels;

namespace TrainingLog.Windows;

/// <summary>
/// Окно добавления дня тренировки. Одно и на добавление, и на правку: запись за дату одна,
/// и модель сама определяет, есть она в базе или нет.
/// </summary>
public partial class AddDayWindow : Window
{
    private readonly AddDayViewModel _viewModel;

    public AddDayWindow(AddDayViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Подписка на отказ в окне, а не в модели: событие приходит от поля ввода, живёт в
        // поведении Controls, и модель о нём знать не должна. Отписка в Closed обязательна —
        // иначе закрытое окно вместе с визуальным деревом удерживалось бы подпиской.
        AllowedCharsInput.AddRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.AddAcceptedHandler(this, OnInputAccepted);

        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Было ли окно закрыто кнопкой сохранения, а не крестиком или Esc.
    /// </summary>
    /// <remarks>
    /// Отдельное от <see cref="Saved"/> состояние, и различие важно вызывающему: «Сохранить»
    /// не закрывает окно, поэтому <see cref="DialogResult"/> после него остаётся <c>null</c>,
    /// и по одному значению диалога перечитывать журнал было бы не на чем. Сохранён ли день —
    /// спросим у модели в момент закрытия.
    /// </remarks>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Было ли что-то сохранено за время работы окна, включая сохранение без закрытия.
    /// </summary>
    public bool Saved { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // Фокус в конструкторе ставить нельзя: у ещё не показанного окна нет в цепочке
        // активации, и Focus() отработает вхолостую. Первым фокусируется поле даты, дальше
        // всё работает клавишей Tab.
        _viewModel.InitializeCommand.Execute(null);
    }

    /// <summary>
    /// Кнопки сохранения привязаны к командам, поэтому WPF сама снимает их активность, когда
    /// день сохранять нечем. А вот результат известен только здесь: по одному значению
    /// <see cref="AddDayViewModel.Accepted"/> вызывающий <c>ShowDialog</c> поймёт, что день
    /// сохранён и окно надо закрывать. Поэтому <see cref="DialogResult"/> выставляется по
    /// уведомлению.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddDayViewModel.Accepted) && _viewModel.Accepted)
        {
            Accepted = true;
            DialogResult = true;
        }
    }

    /// <summary>
    /// В поле подхода введено или вставлено недопустимое.
    /// </summary>
    /// <remarks>
    /// Якорь подсказки задаётся здесь, а не в разметке: полей подходов сколько угодно, они
    /// создаются по составу упражнения и имени в разметке не имеют.
    ///
    /// Якорь берётся из <see cref="AllowedCharsInput.RejectedEventArgs.Field"/>, а **не** из
    /// <c>e.Source</c>: событие всплывает от поля вверх, через контейнер подходов, и на окне
    /// <c>Source</c> уже не поле. На Source повесили подсказку — и она вставала по центру всех
    /// подходов сразу, одинаково для веса и повторений и для любого подхода.
    ///
    /// Задаётся якорь до показа: подсказка позиционируется при появлении, и без цели она бы не
    /// знала, под кем встать.
    ///
    /// Отсчёт перезапускается явно: повторный отказ с тем же текстом состояние подсказки не
    /// меняет, и без этого она погасла бы на середине набора.
    /// </remarks>
    private void OnInputRejected(object sender, RoutedEventArgs e)
    {
        if (e is not AllowedCharsInput.RejectedEventArgs rejected)
        {
            return;
        }

        InputHint.Target = rejected.Field;

        _viewModel.Hint.ShowCommand.Execute(rejected.Message);

        InputHint.RestartCountdown();
    }

    /// <summary>
    /// В поле подхода введён или вставлен допустимый символ.
    /// </summary>
    /// <remarks>
    /// Гасит висящее объяснение: пользователь сделал то, чего оно требовало, и держать её дальше
    /// незачем — она закрывала бы само поле. Команда та же, что у крестика, таймера и нажатия
    /// снаружи, поэтому текст убирается тем же путём.
    ///
    /// Отказ сюда не приводит: иначе подсказка мигала бы на каждом негодном символе.
    /// </remarks>
    private void OnInputAccepted(object sender, RoutedEventArgs e) =>
        _viewModel.Hint.DismissCommand.Execute(null);

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        AllowedCharsInput.RemoveRejectedHandler(this, OnInputRejected);
        AllowedCharsInput.RemoveAcceptedHandler(this, OnInputAccepted);

        Saved = _viewModel.Saved;
    }
}