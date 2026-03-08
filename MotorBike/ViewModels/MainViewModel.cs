using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace MotorBike.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private string _currentPageTitle = "الرئيسية";

    [RelayCommand]
    private void NavigateTo(string page)
    {
        var services = App.Services;

        (CurrentViewModel, CurrentPageTitle) = page switch
        {
            "Company"        => ((ObservableObject)services.GetRequiredService<CompanyViewModel>(),        "بيانات الشركة"),
            "Customers"      => (services.GetRequiredService<CustomersViewModel>(),      "العملاء"),
            "Suppliers"      => (services.GetRequiredService<SuppliersViewModel>(),      "الموردين"),
            "Users"          => (services.GetRequiredService<UsersViewModel>(),          "المستخدمين"),
            "Units"          => (services.GetRequiredService<UnitsViewModel>(),          "وحدات الأصناف"),
            "ItemCategories" => (services.GetRequiredService<ItemCategoriesViewModel>(), "مجموعات الأصناف"),
            "Stores"         => (services.GetRequiredService<StoresViewModel>(),         "المخازن"),
            "CarBrands"      => (services.GetRequiredService<CarBrandsViewModel>(),      "ماركات الموتوسيكلات"),
            "CarModels"      => (services.GetRequiredService<CarModelsViewModel>(),      "موديلات الماركة"),
            "Colors"         => (services.GetRequiredService<ColorsViewModel>(),         "الألوان"),
            "Cash"           => (services.GetRequiredService<CashViewModel>(),           "الخزائن"),
            "Omla"           => (services.GetRequiredService<OmlaViewModel>(),           "العملات"),
            "ExpGroups"      => (services.GetRequiredService<ExpGroupsViewModel>(),      "مجموعات المصروفات"),
            "Cities"         => (services.GetRequiredService<CitiesViewModel>(),         "المدن"),
            "BasicData"      => (services.GetRequiredService<BasicDataViewModel>(),      "البيانات الأساسية"),
            _ => (null!, "الرئيسية")
        };
    }
}
