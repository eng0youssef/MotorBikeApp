using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotorBike.DataAccess;
using MotorBike.ViewModels;
using MotorBike.Services.Activation;
using MotorBike.Views;

namespace MotorBike;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enable Dapper to map underscore SQL columns to PascalCase C# properties
        // e.g. User_ID → UserId, Brand_ID → BrandId
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var basePath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton<IConfiguration>(configuration);

        // DataAccess
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
        services.AddTransient<CompositeKeyRepository>();

        // Activation Services
        services.AddSingleton<HardwareInfoService>();
        services.AddSingleton<ActivationService>();
        services.AddTransient<ActivationViewModel>();
        services.AddTransient<ActivationWindow>();

        // ViewModels
        services.AddTransient<CarBrandsViewModel>();
        services.AddTransient<CarModelsViewModel>();
        services.AddTransient<CashViewModel>();
        services.AddTransient<CitiesViewModel>();
        services.AddTransient<ColorsViewModel>();
        services.AddTransient<CompanyViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<ExpGroupsViewModel>();
        services.AddTransient<ItemCategoriesViewModel>();
        services.AddTransient<ItemsViewModel>();
        services.AddTransient<OmlaViewModel>();
        services.AddTransient<StoresViewModel>();
        services.AddTransient<UnitsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<BasicDataViewModel>();
        services.AddTransient<InspectionsViewModel>();
        services.AddTransient<BuysViewModel>();
        services.AddTransient<CarsViewModel>();
        services.AddTransient<SalesViewModel>();
        services.AddTransient<BuyCarViewModel>();
        services.AddTransient<SalesCarViewModel>();
        services.AddTransient<ReBuyViewModel>();
        services.AddTransient<ReSalesViewModel>();
        services.AddTransient<ImportDataViewModel>();
        services.AddTransient<ImportSuppliersViewModel>();
        services.AddTransient<ImportExpensesViewModel>();
        services.AddTransient<ImportInvoiceViewModel>();
        services.AddTransient<ImportPaymentsViewModel>();
        
        services.AddTransient<ReportsDataViewModel>();
        services.AddTransient<StoreReportsViewModel>();
        services.AddTransient<SalesReportsViewModel>();
        services.AddTransient<PurchasesReportsViewModel>();
        services.AddTransient<ProfitsReportsViewModel>();
        services.AddTransient<CarsReportsViewModel>();
        services.AddTransient<ImportReportsViewModel>();
        services.AddTransient<CashReportsViewModel>();
        
        services.AddTransient<PaymentsDataViewModel>();

        services.AddTransient<CusPaymentsViewModel>();
        services.AddTransient<SuppPaymentsViewModel>();
        services.AddTransient<ExpPaymentsViewModel>();
        services.AddTransient<CashTransfersViewModel>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<ExpensesViewModel>();
        services.AddTransient<OpenStockViewModel>();
        services.AddTransient<SettingsViewModel>();

        // MainWindow and LoginWindow
        services.AddTransient<MainWindow>();
        services.AddTransient<MotorBike.ViewModels.LoginViewModel>();
        services.AddTransient<MotorBike.Views.LoginWindow>();

        Services = services.BuildServiceProvider();

        // --- Activation Logic (Online First with Offline Fallback) ---
        var activationService = Services.GetRequiredService<ActivationService>();
        
        // 1. Try silent server check first (if internet is available)
        var (status, _) = await activationService.CheckServerAsync();
        
        if (status == ActivationService.ServerCheckStatus.NotActivated)
        {
            // Device is explicitly not activated on server - block access
            var activationWindow = Services.GetRequiredService<ActivationWindow>();
            if (activationWindow.ShowDialog() != true)
            {
                Current.Shutdown();
                return;
            }
        }
        else if (status == ActivationService.ServerCheckStatus.NetworkError)
        {
            // No internet - fall back to local 30-day grace period
            if (!activationService.IsActivatedLocally())
            {
                // Local cache expired or missing - must show activation window
                var activationWindow = Services.GetRequiredService<ActivationWindow>();
                if (activationWindow.ShowDialog() != true)
                {
                    Current.Shutdown();
                    return;
                }
            }
        }
        // If status == Success, we proceed normally

        // --- Normal Startup ---
        var loginWindow = Services.GetRequiredService<MotorBike.Views.LoginWindow>();
        loginWindow.Show();
    }
}
