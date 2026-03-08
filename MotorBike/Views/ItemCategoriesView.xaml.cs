using System.Windows; using System.Windows.Controls; using MotorBike.ViewModels;
namespace MotorBike.Views;
public partial class ItemCategoriesView : UserControl
{
    public ItemCategoriesView() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    { if (DataContext is ItemCategoriesViewModel vm) await vm.LoadDataCommand.ExecuteAsync(null); }
}
