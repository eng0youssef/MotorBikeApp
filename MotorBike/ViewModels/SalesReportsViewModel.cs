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

    [ObservableProperty] private ObservableCollection<string> _reportTypes = ["المبيعات بالشهور", "المبيعات بالأصناف", "مبيعات عميل معين", "كشف حساب عميل", "كشف حساب تفصيلي للعميل"];
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

    public bool IsCustomerVisible => SelectedReportType == "مبيعات عميل معين" || SelectedReportType == "كشف حساب عميل" || SelectedReportType == "كشف حساب تفصيلي للعميل";
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
            else if (SelectedReportType == "كشف حساب عميل")
            {
                if (SelectedCustomer == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار العميل أولاً", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                parameters.Add("CusId", SelectedCustomer.CusId);

                sql = @"
                    SELECT 'رصيد سابق' AS [البيان], 0 AS [مدين], 0 AS [دائن] WHERE 1=0 -- Placeholder for logic if needed
                    UNION ALL
                    SELECT 'إجمالي المبيعات' AS [البيان], ISNULL(SUM(Total - Disc + AddMony), 0) AS [مدين], 0 AS [دائن] FROM Sales WHERE CusId = @CusId AND SalesDate >= @FromDate AND SalesDate <= @ToDate
                    UNION ALL
                    SELECT 'إجمالي المرتجعات' AS [البيان], 0 AS [مدين], ISNULL(SUM(Total - Disc + AddMony), 0) AS [دائن] FROM ReSales WHERE CusId = @CusId AND SalesDate >= @FromDate AND SalesDate <= @ToDate
                    UNION ALL
                    SELECT 'إجمالي المدفوعات' AS [البيان], 0 AS [مدين], ISNULL(SUM(PayMoney), 0) AS [دائن] FROM Cus_Payments WHERE CusId = @CusId AND PayDate >= @FromDate AND PayDate <= @ToDate AND PayType IN (0, 1, 3)";
            }
            else if (SelectedReportType == "كشف حساب تفصيلي للعميل")
            {
                if (SelectedCustomer == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار العميل أولاً", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                parameters.Add("CusId", SelectedCustomer.CusId);

                sql = @"
                    -- Sales Invoices with Items
                    SELECT M.SalesDate AS [SortDate], CONVERT(VARCHAR, M.SalesDate, 103) AS [التاريخ], 
                           'فاتورة مبيعات رقم ' + CAST(M.Sales_ID AS VARCHAR) AS [البيان],
                           I.ItemName AS [اسم الصنف], S.Qty AS [الكمية], S.Price AS [السعر], (S.Qty * (S.Price - S.Disc)) AS [مدين], 0 AS [دائن] 
                    FROM Sales M 
                    INNER JOIN Sales_Sub S ON M.Sales_ID = S.SalesId 
                    INNER JOIN Items I ON S.ItemId = I.Item_ID
                    WHERE M.CusId = @CusId AND M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                    
                    UNION ALL
                    
                    -- Returns with Items
                    SELECT M.SalesDate AS [SortDate], CONVERT(VARCHAR, M.SalesDate, 103) AS [التاريخ], 
                           'مرتجع مبيعات رقم ' + CAST(M.Sales_ID AS VARCHAR) AS [البيان],
                           I.ItemName AS [اسم الصنف], S.Qty AS [الكمية], S.Price AS [السعر], 0 AS [مدين], (S.Qty * (S.Price - S.Disc)) AS [دائن] 
                    FROM ReSales M 
                    INNER JOIN ReSales_Sub S ON M.Sales_ID = S.SalesId 
                    INNER JOIN Items I ON S.ItemId = I.Item_ID
                    WHERE M.CusId = @CusId AND M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                    
                    UNION ALL
                    
                    -- Payments
                    SELECT PayDate AS [SortDate], CONVERT(VARCHAR, PayDate, 103) AS [التاريخ], 
                           CASE PayType WHEN 0 THEN 'سداد عميل' WHEN 1 THEN 'تحصيل عميل' WHEN 2 THEN 'رد' WHEN 3 THEN 'خصم' END AS [البيان],
                           ISNULL(Notes, '') AS [اسم الصنف],
                           0 AS [الكمية], 0 AS [السعر],
                           CASE WHEN PayType IN (2) THEN PayMoney ELSE 0 END AS [مدين],
                           CASE WHEN PayType IN (0, 1, 3) THEN PayMoney ELSE 0 END AS [دائن] 
                    FROM Cus_Payments WHERE CusId = @CusId AND PayDate >= @FromDate AND PayDate <= @ToDate
                    
                    ORDER BY SortDate ASC";
            }

            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, parameters))
            {
                dt.Load(reader);
            }

            if (SelectedReportType == "كشف حساب تفصيلي للعميل")
            {
                // Add الرصيد column for running balance
                dt.Columns.Add("الرصيد", typeof(double));
                double runningBalance = 0;

                // We need to fetch the initial balance BEFORE @FromDate if applicable.
                // For simplicity, let's start from 0 or calculate it.
                var opParams = new DynamicParameters();
                opParams.Add("CusId", SelectedCustomer.CusId);
                opParams.Add("FromDate", queryFromDate);

                var opSales = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total - Disc + AddMony), 0) FROM Sales WHERE CusId = @CusId AND SalesDate < @FromDate", opParams);
                var opReturns = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total - Disc + AddMony), 0) FROM ReSales WHERE CusId = @CusId AND SalesDate < @FromDate", opParams);
                var opPayments = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney), 0) FROM Cus_Payments WHERE CusId = @CusId AND PayDate < @FromDate AND PayType IN (0, 1, 3)", opParams);
                var opRefunds = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney), 0) FROM Cus_Payments WHERE CusId = @CusId AND PayDate < @FromDate AND PayType = 2", opParams);
                
                runningBalance = opSales - opReturns - opPayments + opRefunds;

                // Add Opening Balance Row at the top if it's not zero
                var opRow = dt.NewRow();
                if (dt.Columns.Contains("SortDate"))
                {
                    dt.Columns["SortDate"]!.AllowDBNull = true;
                    opRow["SortDate"] = queryFromDate; // Or DBNull.Value
                }
                if (dt.Columns.Contains("الكمية")) dt.Columns["الكمية"]!.AllowDBNull = true;
                if (dt.Columns.Contains("السعر")) dt.Columns["السعر"]!.AllowDBNull = true;

                opRow["التاريخ"] = queryFromDate.ToString("dd/MM/yyyy");
                opRow["اسم الصنف"] = "رصيد سابق";
                opRow["الكمية"] = 0;
                opRow["السعر"] = 0;
                opRow["مدين"] = 0;
                opRow["دائن"] = 0;
                opRow["الرصيد"] = runningBalance;
                dt.Rows.InsertAt(opRow, 0);

                // Calculate running balance for each row (starting from index 1 because index 0 is Balance Forward)
                for (int i = 1; i < dt.Rows.Count; i++)
                {
                    double debit = Convert.ToDouble(dt.Rows[i]["مدين"] == DBNull.Value ? 0 : dt.Rows[i]["مدين"]);
                    double credit = Convert.ToDouble(dt.Rows[i]["دائن"] == DBNull.Value ? 0 : dt.Rows[i]["دائن"]);
                    runningBalance += (debit - credit);
                    dt.Rows[i]["الرصيد"] = runningBalance;
                }
                
                // Remove the SortDate column from being visible in PDF
                if (dt.Columns.Contains("SortDate")) dt.Columns.Remove("SortDate");
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
            else if (SelectedReportType == "كشف حساب عميل" || SelectedReportType == "كشف حساب تفصيلي للعميل")
            {
                parameters.Add("CusId", SelectedCustomer?.CusId ?? 0);
                var salesSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT ISNULL(SUM(Total - Disc + AddMony), 0) AS Tot FROM Sales WHERE CusId = @CusId AND SalesDate >= @FromDate AND SalesDate <= @ToDate", parameters);
                totalSales = salesSum?.Tot ?? 0;

                var resalesSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT ISNULL(SUM(Total - Disc + AddMony), 0) AS Tot FROM ReSales WHERE CusId = @CusId AND SalesDate >= @FromDate AND SalesDate <= @ToDate", parameters);
                totalReturns = resalesSum?.Tot ?? 0;

                var paymentsSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT ISNULL(SUM(PayMoney), 0) AS Tot FROM Cus_Payments WHERE CusId = @CusId AND PayDate >= @FromDate AND PayDate <= @ToDate AND PayType IN (0, 1, 3)", parameters);
                double totalPayments = paymentsSum?.Tot ?? 0;

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "إجمالي المديونية", totalSales.ToString("N2") },
                    { "إجمالي المرتجعات", totalReturns.ToString("N2") },
                    { "إجمالي المدفوعات", totalPayments.ToString("N2") },
                    { "الرصيد المتبقي", (totalSales - totalReturns - totalPayments).ToString("N2") }
                };
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

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد المرتجعات", countReturns.ToString() },
                    { "عدد الفواتير", countSales.ToString() },
                    { "إجمالي المبيعات", totalSales.ToString("N2") },
                    { "إجمالي المرتجعات", totalReturns.ToString("N2") },
                    { "صافي المبيعات", (totalSales - totalReturns).ToString("N2") }
                };
            }

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
