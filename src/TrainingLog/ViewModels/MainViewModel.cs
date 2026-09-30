using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TrainingLog.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Дневник тренировок";

    [ObservableProperty]
    private string _status = "Заготовка приложения готова к разработке.";

    [RelayCommand]
    private void IncrementCounter()
    {
        Counter++;
    }

    [ObservableProperty]
    private int _counter;
}
