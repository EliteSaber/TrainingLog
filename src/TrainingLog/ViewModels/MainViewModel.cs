using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainingLog.Services;

namespace TrainingLog.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IWindowService _windowService;

    public MainViewModel(IWindowService windowService)
    {
        ArgumentNullException.ThrowIfNull(windowService);
        _windowService = windowService;
    }

    [ObservableProperty]
    private string _title = "Журнал тренировок";

    [ObservableProperty]
    private string _status = "Заготовка приложения готова к разработке.";

    [RelayCommand]
    private void IncrementCounter()
    {
        Counter++;
    }

    [RelayCommand]
    private void OpenExercises()
    {
        _windowService.OpenExercises();
    }

    [RelayCommand]
    private void OpenPlans()
    {
        _windowService.OpenPlans();
    }

    [ObservableProperty]
    private int _counter;
}
