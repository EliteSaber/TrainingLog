namespace TrainingLog.Core.Settings;

/// <summary>
/// Размер окна по умолчанию: ширина и высота в единицах WPF (device-independent pixels).
/// </summary>
/// <remarks>
/// Высота <c>null</c> — «окно само подбирает высоту под содержимое» (<c>SizeToContent="Height"</c>).
/// Так записан в файле настроек и у окна правки упражнения: у него ширина задана, а высота
/// следует за содержимым поля с названием.
/// </remarks>
public sealed class WindowSize
{
    /// <summary>Ширина окна в единицах WPF.</summary>
    public int Width { get; set; }

    /// <summary>Высота окна; <c>null</c> — высота по содержимому.</summary>
    public int? Height { get; set; }
}
