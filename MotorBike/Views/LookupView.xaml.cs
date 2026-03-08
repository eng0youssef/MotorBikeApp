using System.Windows;
using System.Windows.Controls;

namespace MotorBike.Views;

public partial class LookupView : UserControl
{
    public LookupView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.LookupViewModelBase<Models.CarBrand> vm1) await vm1.LoadDataCommand.ExecuteAsync(null);
        else if (DataContext is ViewModels.LookupViewModelBase<Models.City> vm2) await vm2.LoadDataCommand.ExecuteAsync(null);
        else if (DataContext is ViewModels.LookupViewModelBase<Models.Color> vm3) await vm3.LoadDataCommand.ExecuteAsync(null);
        else if (DataContext is ViewModels.LookupViewModelBase<Models.ExpGroup> vm4) await vm4.LoadDataCommand.ExecuteAsync(null);
        else if (DataContext is ViewModels.LookupViewModelBase<Models.ItemCategory> vm5) await vm5.LoadDataCommand.ExecuteAsync(null);
        else if (DataContext is ViewModels.LookupViewModelBase<Models.Omla> vm6) await vm6.LoadDataCommand.ExecuteAsync(null);
        else if (DataContext is ViewModels.LookupViewModelBase<Models.Store> vm7) await vm7.LoadDataCommand.ExecuteAsync(null);
        else if (DataContext is ViewModels.LookupViewModelBase<Models.Unit> vm8) await vm8.LoadDataCommand.ExecuteAsync(null);
        else if (DataContext is ViewModels.LookupViewModelBase<Models.User> vm9) await vm9.LoadDataCommand.ExecuteAsync(null);
    }
}
