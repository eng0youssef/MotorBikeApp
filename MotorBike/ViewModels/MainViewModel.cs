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
            "Items"          => (services.GetRequiredService<ItemsViewModel>(),          "الأصناف"),
            "Stores"         => (services.GetRequiredService<StoresViewModel>(),         "المخازن"),
            "CarBrands"      => (services.GetRequiredService<CarBrandsViewModel>(),      "ماركات الموتوسيكلات"),
            "CarModels"      => (services.GetRequiredService<CarModelsViewModel>(),      "موديلات الماركة"),
            "Colors"         => (services.GetRequiredService<ColorsViewModel>(),         "الألوان"),
            "Cash"           => (services.GetRequiredService<CashViewModel>(),           "الخزائن"),
            "Omla"           => (services.GetRequiredService<OmlaViewModel>(),           "العملات"),
            "ExpGroups"      => (services.GetRequiredService<ExpGroupsViewModel>(),      "مجموعات المصروفات"),
            "Expenses"       => (services.GetRequiredService<ExpensesViewModel>(),       "المصروفات"),
            "Cities"         => (services.GetRequiredService<CitiesViewModel>(),         "المدن"),
            "Inspections"    => (services.GetRequiredService<InspectionsViewModel>(),    "الكشف"),
            "BasicData"      => (services.GetRequiredService<BasicDataViewModel>(),      "البيانات الأساسية"),
            "Buys"           => (services.GetRequiredService<BuysViewModel>(),           "فاتورة الشراء"),
            "Cars"           => (services.GetRequiredService<CarsViewModel>(),           "الموتوسيكلات"),
            "Sales"          => (services.GetRequiredService<SalesViewModel>(),          "فاتورة المبيعات"),
            "BuyCar"         => (services.GetRequiredService<BuyCarViewModel>(),         "شراء موتوسيكل"),
            "SalesCar"       => (services.GetRequiredService<SalesCarViewModel>(),       "بيع موتوسيكل"),
            "ReBuy"          => (services.GetRequiredService<ReBuyViewModel>(),          "مرتجع المشتريات"),
            "ReSales"        => (services.GetRequiredService<ReSalesViewModel>(),        "مرتجع المبيعات"),
            "ImportData"     => (services.GetRequiredService<ImportDataViewModel>(),     "إدارة الاستيراد"),
            "ImportSuppliers"=> (services.GetRequiredService<ImportSuppliersViewModel>(),"موردين الاستيراد"),
            "ImportExpenses" => (services.GetRequiredService<ImportExpensesViewModel>(), "بنود مصروفات الاستيراد"),
            "ImportInvoice"  => (services.GetRequiredService<ImportInvoiceViewModel>(),  "فاتورة الاستيراد"),
            "ImportPayments" => (services.GetRequiredService<ImportPaymentsViewModel>(), "مدفوعات الاستيراد"),

            "ReportsData"      => (services.GetRequiredService<ReportsDataViewModel>(),      "التقارير والإحصائيات"),
            "StoreReports"     => (services.GetRequiredService<StoreReportsViewModel>(),     "تقارير المخازن"),
            "SalesReports"     => (services.GetRequiredService<SalesReportsViewModel>(),     "تقارير المبيعات"),
            "PurchasesReports" => (services.GetRequiredService<PurchasesReportsViewModel>(), "تقارير المشتريات"),
            "ProfitsReports"   => (services.GetRequiredService<ProfitsReportsViewModel>(),   "تقارير الأرباح"),
            "CarsReports"      => (services.GetRequiredService<CarsReportsViewModel>(),      "تقارير الموتوسيكلات"),
            "ImportReports"    => (services.GetRequiredService<ImportReportsViewModel>(),    "تقارير الاستيراد"),
            "CashReports"      => (services.GetRequiredService<CashReportsViewModel>(),      "تقارير الخزينة"),

            "PaymentsData"   => (services.GetRequiredService<PaymentsDataViewModel>(),   "المدفوعات والتحويلات"),

            "CusPayments"   => (services.GetRequiredService<CusPaymentsViewModel>(),   "سداد ومقبوضات العملاء"),
            "SuppPayments"  => (services.GetRequiredService<SuppPaymentsViewModel>(),  "سداد ومقبوضات الموردين"),
            "ExpPayments"   => (services.GetRequiredService<ExpPaymentsViewModel>(),   "صرف المصروفات"),
            "CashTransfers" => (services.GetRequiredService<CashTransfersViewModel>(), "تحويلات الخزينة"),
            
            "OpenStock"     => (services.GetRequiredService<OpenStockViewModel>(),     "أرصدة أول المدة"),

            _ => (null!, "الرئيسية")
        };
    }
}
