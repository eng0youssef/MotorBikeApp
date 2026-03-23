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

    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked = true;

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

    private Dictionary<string, string>? _currentHeaderInfo;
    private Dictionary<string, string>? _currentFooterTotals;

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
            
            DateTime queryFromDate = IsFromDateChecked ? FromDate.Date : new DateTime(1900, 1, 1);
            DateTime queryToDate = IsToDateChecked ? ToDate.Date.AddDays(1).AddSeconds(-1) : new DateTime(9999, 12, 31);
            
            parameters.Add("FromDate", queryFromDate);
            parameters.Add("ToDate", queryToDate);
            
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
            
            // Generate Totals and Metadata for PDF Report
            double totalSales = 0, totalReturns = 0;
            int countSales = 0, countReturns = 0;

            if (SelectedReportType == "المبيعات بالأصناف")
            {
                string itemFilterS = SelectedItem != null ? " AND S.ItemId = @ItemId " : "";
                var salesSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT COUNT(DISTINCT M.Sales_ID) AS Cnt, ISNULL(SUM(S.Qty * (S.Price - S.Disc)), 0) AS Tot FROM Sales M INNER JOIN Sales_Sub S ON M.Sales_ID = S.SalesId WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate " + itemFilterS, parameters);
                countSales = salesSum?.Cnt ?? 0;
                totalSales = salesSum?.Tot ?? 0;

                var resalesSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT COUNT(DISTINCT M.Sales_ID) AS Cnt, ISNULL(SUM(S.Qty * (S.Price - S.Disc)), 0) AS Tot FROM ReSales M INNER JOIN ReSales_Sub S ON M.Sales_ID = S.SalesId WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate " + itemFilterS, parameters);
                countReturns = resalesSum?.Cnt ?? 0;
                totalReturns = resalesSum?.Tot ?? 0;
            }
            else
            {
                string cusFilter = (SelectedReportType == "مبيعات عميل معين" && SelectedCustomer != null) ? " AND CusId = @CusId " : "";
                var salesSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT COUNT(Sales_ID) AS Cnt, ISNULL(SUM(Total - Disc + AddMony), 0) AS Tot FROM Sales WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate " + cusFilter, parameters);
                countSales = salesSum?.Cnt ?? 0;
                totalSales = salesSum?.Tot ?? 0;

                var resalesSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT COUNT(Sales_ID) AS Cnt, ISNULL(SUM(Total - Disc + AddMony), 0) AS Tot FROM ReSales WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate " + cusFilter, parameters);
                countReturns = resalesSum?.Cnt ?? 0;
                totalReturns = resalesSum?.Tot ?? 0;
            }

            _currentHeaderInfo = new Dictionary<string, string>
            {
                { "من تاريخ", queryFromDate.ToString("yyyy/MM/dd") },
                { "إلى تاريخ", queryToDate.ToString("yyyy/MM/dd") }
            };
                
            if (SelectedReportType == "مبيعات عميل معين" && SelectedCustomer != null)
                _currentHeaderInfo.Add("اسم العميل", SelectedCustomer.CusName ?? "غير محدد");
            if (SelectedReportType == "المبيعات بالأصناف" && SelectedItem != null)
                _currentHeaderInfo.Add("الصنف", SelectedItem.ItemName);

            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي المبيعات", totalSales.ToString("N2") },
                { "إجمالي المرتجعات", totalReturns.ToString("N2") },
                { "صافي المبيعات", (totalSales - totalReturns).ToString("N2") },
                { "عدد الفواتير", countSales.ToString() },
                { "عدد المرتجعات", countReturns.ToString() }
            };

            if (SelectedReportType == "المبيعات بالشهور")
            {
                _currentFooterTotals.Add("عدد الشهور", dt.Rows.Count.ToString());
            }

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
                var pdfBytes = MotorBike.Services.ReportGenerator.GeneratePdf(company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
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
            var pdfBytes = MotorBike.Services.ReportGenerator.GeneratePdf(company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
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
