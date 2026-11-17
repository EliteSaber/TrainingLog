using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Core.Settings;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

/// <summary>
/// Строка вкладки размеров: одно окно приложения и его ширина с высотой.
/// </summary>
/// <remarks>
/// Число в строке меняется двумя способами: его вводят в поле и его тянут мышью. Второй
/// способ приходит снаружи — перетаскиванием, — и приводит к команде, а не к записи в
/// свойство: значение меняет модель, иначе привязка развалилась бы (ловушка 11).
///
/// Поле и команда разведены намеренно: команда получает готовое число и сама вписывает его в
/// границы, а поле показывает то, что модель записала. Так перетаскивание никогда не
/// оставит число вне границ, а введённое руками число проверяется при сохранении — иначе
/// промежуточное «8» на пути к «800» затиралось бы минимумом окна.
/// </remarks>
public sealed partial class WindowSizeRowViewModel : ObservableObject
{
    private readonly SettingsViewModel _owner;

    /// <param name="owner">Модель окна параметров: ей сообщается о каждой правке строки.</param>
    /// <param name="entry">Окно каталога размеров.</param>
    /// <param name="size">Размер окна: из файла настроек или из каталога.</param>
    public WindowSizeRowViewModel(SettingsViewModel owner, WindowSizeEntry entry, WindowSize size)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(size);

        _owner = owner;

        Id = entry.Id;
        Title = entry.Title;
        MinimumWidth = entry.MinWidth;
        MinimumHeight = entry.MinHeight;

        // Высота по содержимому задать нельзя: у окна правки упражнения высота следует за
        // полем с названием, и колонка высоты у такой строки не редактируется.
        HasHeight = entry.Height is not null;
        IsContentHeight = entry.Height is null;

        _width = size.Width;
        _height = size.Height ?? entry.Height ?? 0;

        WidthCommand = new RelayCommand<int>(SetWidth);
        HeightCommand = new RelayCommand<int>(SetHeight);
    }

    /// <summary>Ключ окна в файле настроек.</summary>
    public string Id { get; }

    /// <summary>Название окна: как окно называется в приложении.</summary>
    public string Title { get; }

    /// <summary>Минимальная ширина окна: уже неё окно схлопывается.</summary>
    public int MinimumWidth { get; }

    /// <summary>Минимальная высота окна.</summary>
    public int MinimumHeight { get; }

    /// <summary>Высота окна задаётся: поле высоты редактируется.</summary>
    public bool HasHeight { get; }

    /// <summary>Высота окна следует за содержимым: в колонке высоты стоит подпись.</summary>
    public bool IsContentHeight { get; }

    /// <summary>Ширина окна.</summary>
    [ObservableProperty]
    private int _width;

    /// <summary>Высота окна. У окна с высотой по содержимому значение не показывается.</summary>
    [ObservableProperty]
    private int _height;

    /// <summary>Команда перетаскивания ширины: получает готовое число.</summary>
    public IRelayCommand<int> WidthCommand { get; }

    /// <summary>Команда перетаскивания высоты: получает готовое число.</summary>
    public IRelayCommand<int> HeightCommand { get; }

    /// <summary>
    /// Объяснение негодного размера, а при пустом значении — размера годного.
    /// </summary>
    /// <remarks>
    /// Сообщение живёт до исправления, а не пять секунд в подсказке: «Сохранить» с таким
    /// размером не пишет, и причину обязано быть видно, пока не исправят.
    /// </remarks>
    public string? Problem
    {
        get
        {
            if (Width < MinimumWidth)
            {
                return $"Ширина окна «{Title}» должна быть не меньше {MinimumWidth}.";
            }

            if (Width > WindowSizeRules.MaxSize)
            {
                return $"Ширина окна «{Title}» не может быть больше {WindowSizeRules.MaxSize}.";
            }

            if (!HasHeight)
            {
                return null;
            }

            if (Height < MinimumHeight)
            {
                return $"Высота окна «{Title}» должна быть не меньше {MinimumHeight}.";
            }

            if (Height > WindowSizeRules.MaxSize)
            {
                return $"Высота окна «{Title}» не может быть больше {WindowSizeRules.MaxSize}.";
            }

            return null;
        }
    }

    partial void OnWidthChanged(int value) => _owner.OnWindowSizeChanged();

    partial void OnHeightChanged(int value) => _owner.OnWindowSizeChanged();

    /// <summary>
    /// Ширина из перетаскивания: значение вписывается в границы окна.
    /// </summary>
    /// <remarks>
    /// Границы здесь, а не в поведении перетаскивания: правило одно на оба способа правки,
    /// иначе перетаскивание и ввод руками расходились бы в том, что считается годным.
    /// </remarks>
    /// <param name="value">Новое значение.</param>
    private void SetWidth(int value) => Width = Math.Clamp(value, MinimumWidth, WindowSizeRules.MaxSize);

    /// <summary>Высота из перетаскивания: значение вписывается в границы окна.</summary>
    /// <param name="value">Новое значение.</param>
    private void SetHeight(int value) => Height = Math.Clamp(value, MinimumHeight, WindowSizeRules.MaxSize);
}
