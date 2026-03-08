using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotorBike.DataAccess;
using MotorBike.ViewModels;

namespace MotorBike;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
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

        // ViewModels
        services.AddTransient<CarBrandsViewModel>();
        services.AddTransient<CarModelsViewModel>();
        services.AddTransient<CashViewModel>();
        services.AddTransient<CitiesViewModel>();
        services.AddTransient<ColorsViewModel>();
        services.AddTransient<CompanyViewModel>();
        services.AddTransient<ExpGroupsViewModel>();
        services.AddTransient<ItemCategoriesViewModel>();
        services.AddTransient<OmlaViewModel>();
        services.AddTransient<StoresViewModel>();
        services.AddTransient<UnitsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<BasicDataViewModel>();
        services.AddTransient<MainViewModel>();

        // MainWindow
        services.AddTransient<MainWindow>();

        Services = services.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
