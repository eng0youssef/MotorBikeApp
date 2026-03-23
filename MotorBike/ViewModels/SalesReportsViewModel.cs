using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class SalesReportsViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;

    [ObservableProperty] private ObservableCollection<string> _reportTypes = ["المبيعات بالشهور", "المبيعات بالأصناف", "مبيعات عميل معين"];
    [ObservableProperty] private string _selectedReportType = "المبيعات بالشهور";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate = DateTime.Now;

    [ObservableProperty] private ObservableCollection<Customer> _customers = [];
    [ObservableProperty] private Customer? _selectedCustomer;
    
    [ObservableProperty] private ObservableCollection<Item> _items = [];
    [ObservableProperty] private Item? _selectedItem;

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();

    public bool IsCustomerVisible => SelectedReportType == "مبيعات عميل معين";
    public bool IsItemVisible => SelectedReportType == "المبيعات بالأصناف";

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomerVisible));
        OnPropertyChanged(nameof(IsItemVisible));
        ReportData = new System.Data.DataView();
    }

    public SalesReportsViewModel(IDbConnectionFactory dbFactory, IRepository<Customer> customerRepo, IRepository<Item> itemRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(customerRepo, itemRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(IRepository<Customer> customerRepo, IRepository<Item> itemRepo)
    {
        var custs = await customerRepo.GetAllAsync();
        Customers = new ObservableCollection<Customer>(custs);
        
        var itms = await itemRepo.GetAllAsync();
        Items = new ObservableCollection<Item>(itms);
    }

    [ObservableProperty] private string? _statusMessage;

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            db.Open();
            string sql = "";
            var parameters = new DynamicParameters();
            parameters.Add("FromDate", FromDate.Date);
            parameters.Add("ToDate", ToDate.Date.AddDays(1).AddSeconds(-1));
            
            if (SelectedReportType == "المبيعات بالشهور")
            {
                sql = @"
                    SELECT FORMAT(SalesDate, 'yyyy-MM') AS [الشهر], 
                           COUNT(Sales_ID) AS [عدد الفواتير], 
                           SUM(Total) AS [الإجمالي], 
                           SUM(Disc) AS [الخصم], 
                           SUM(Total - Disc + AddMony) AS [الصافي]
                    FROM Sales
                    WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate
                    GROUP BY FORMAT(SalesDate, 'yyyy-MM')
                    ORDER BY [الشهر] DESC";
            }
            else if (SelectedReportType == "المبيعات بالأصناف")
            {
                string itemFilter = "";
                if (SelectedItem != null)
                {
                    itemFilter = " AND S.ItemId = @ItemId ";
                    parameters.Add("ItemId", SelectedItem.ItemId);
                }
                sql = @"
                    SELECT I.ItemName AS [الصنف], 
                           SUM(S.Qty) AS [الكمية المباعة], 
                           SUM(S.Qty * (S.Price - S.Disc)) AS [إجمالي المبيعات]
                    FROM Sales_Sub S
                    INNER JOIN Items I ON S.ItemId = I.Item_ID
                    INNER JOIN Sales M ON S.SalesId = M.Sales_ID
                    WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                    " + itemFilter + @"
                    GROUP BY I.ItemName
                    ORDER BY [الكمية المباعة] DESC";
            }
            else if (SelectedReportType == "مبيعات عميل معين")
            {
                string cusFilter = "";
                if (SelectedCustomer != null)
                {
                    cusFilter = " AND M.CusId = @CusId ";
                    parameters.Add("CusId", SelectedCustomer.CusId);
                }
                sql = @"
                    SELECT CONVERT(VARCHAR, M.SalesDate, 103) AS [التاريخ], 
                           M.Sales_ID AS [رقم الفاتورة], 
                           C.CusName AS [العميل], 
                           M.Total AS [الإجمالي], 
                           M.Disc AS [الخصم], 
                           (M.Total - M.Disc + M.AddMony) AS [الصافي]
                    FROM Sales M
                    LEFT JOIN Customers C ON M.CusId = C.Cus_ID
                    WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                    " + cusFilter + @"
                    ORDER BY M.SalesDate DESC";
            }

            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, parameters))
            {
                dt.Load(reader);
            }
            ReportData = dt.DefaultView;
            StatusMessage = dt.Rows.Count > 0 ? $"تم العثور على {dt.Rows.Count} سجل" : "⚠️ لا توجد بيانات في الفترة المحددة";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            System.Windows.MessageBox.Show("خطأ في استعلام البيانات:\n" + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (ReportData == null || ReportData.Count == 0)
        {
            System.Windows.MessageBox.Show("لا توجد بيانات ليتم تصديرها", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        using var db = _dbFactory.CreateConnection();
        var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF File (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            FileName = SelectedReportType + " " + DateTime.Now.ToString("yyyy-MM-dd")
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var pdfBytes = MotorBike.Services.ReportGenerator.GeneratePdf(company, SelectedReportType, ReportData);
                System.IO.File.WriteAllBytes(saveFileDialog.FileName, pdfBytes);
                System.Windows.MessageBox.Show("تم حفظ التقرير بنجاح", "نجاح", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("حدث خطأ أثناء التصدير: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
    
    [RelayCommand]
    private async Task PrintPdfAsync()
    {
         if (ReportData == null || ReportData.Count == 0)
        {
            System.Windows.MessageBox.Show("لا توجد بيانات ليتم طباعتها", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        using var db = _dbFactory.CreateConnection();
        var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

        try
        {
            var pdfBytes = MotorBike.Services.ReportGenerator.GeneratePdf(company, SelectedReportType, ReportData);
            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MotorBikeReport_" + Guid.NewGuid() + ".pdf");
            System.IO.File.WriteAllBytes(tempFile, pdfBytes);
            MotorBike.Services.ReportGenerator.PrintPdf(tempFile);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
