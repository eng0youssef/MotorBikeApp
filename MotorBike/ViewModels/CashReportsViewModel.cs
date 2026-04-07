using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;
using System.Collections.Generic;

namespace MotorBike.ViewModels;

public partial class CashReportsViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;

    [ObservableProperty]
    private ObservableCollection<string> _reportTypes =
    [
        "أرصدة الخزائن الحالية",
        "حركة الصندوق اليومية",
        "كشف حساب خزينة",
        "تحويلات الخزائن والبنوك"
    ];

    [ObservableProperty] private string _selectedReportType = "أرصدة الخزائن الحالية";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate = DateTime.Now;
    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked = true;

    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private Cash? _selectedCash;

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private string? _statusMessage;

    public bool IsCashVisible => SelectedReportType is "كشف حساب خزينة" or "حركة الصندوق اليومية";
    public bool IsDateVisible => SelectedReportType is "حركة الصندوق اليومية" or "كشف حساب خزينة" or "تحويلات الخزائن والبنوك";

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCashVisible));
        OnPropertyChanged(nameof(IsDateVisible));
        ReportData           = new System.Data.DataView();
        StatusMessage        = null;
        _currentFooterTotals = null;
        _currentHeaderInfo   = null;
    }

    private Dictionary<string, string>? _currentHeaderInfo;
    private Dictionary<string, string>? _currentFooterTotals;

    public CashReportsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Cash> cashRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(cashRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(IRepository<Cash> cashRepo)
    {
        Cashes = new ObservableCollection<Cash>(await cashRepo.GetAllAsync());
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            db.Open();

            var p = new DynamicParameters();
            DateTime qFrom = IsFromDateChecked ? FromDate.Date : new DateTime(1900, 1, 1);
            DateTime qTo   = IsToDateChecked   ? ToDate.Date.AddDays(1).AddSeconds(-1) : new DateTime(9999, 12, 31);
            p.Add("FromDate", qFrom);
            p.Add("ToDate", qTo);

            _currentHeaderInfo = BuildHeaderInfo(qFrom, qTo);
            _currentFooterTotals = null;

            string sql = "";

            if (SelectedReportType == "أرصدة الخزائن الحالية")
            {
                // This is a snapshot of all current balances
                sql = @"
                    SELECT CashName AS [الخزينة/البنك],
                           CASE TypeId WHEN 0 THEN 'خزينة' WHEN 1 THEN 'بنك' WHEN 2 THEN 'جاري' ELSE 'أخرى' END AS [النوع],
                           OpenDate AS [تاريخ الافتتاح],
                           ISNULL(Bal, 0) AS [الرصيد الحالي]
                    FROM Cash
                    WHERE Active = 1
                    ORDER BY TypeId, CashName";
            }
            else if (SelectedReportType == "حركة الصندوق اليومية")
            {
                string cashFilter1 = "";
                string cashFilter2 = "";
                if (SelectedCash != null)
                {
                    cashFilter1 = " AND CashID = @CashId ";
                    cashFilter2 = " AND (CashID = @CashId OR CashTo = @CashId) ";
                    p.Add("CashId", SelectedCash.CashId);
                }

                sql = @"
                    SELECT CONVERT(VARCHAR, T.LogDate, 103) AS [التاريخ],
                           T.SourceType AS [المصدر],
                           T.Details AS [البيان],
                           T.Debit AS [وارد],
                           T.Credit AS [صادر]
                    FROM (
                        -- Sales Invoices
                        SELECT PayDate AS LogDate, 'مبيعات' AS SourceType, 'سداد فاتورة بيع رقم ' + ISNULL(CAST(SalesID AS VARCHAR), '') + ' ' + ISNULL(Notes, '') AS Details, PayMoney AS Debit, 0 AS Credit FROM Sales_Payments WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        SELECT PayDate, 'مبيعات موتوسيكلات', 'سداد بيع موتوسيكل رقم ' + ISNULL(CAST(SalesID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), PayMoney, 0 FROM Sales_Car_Payments WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        -- Purchases Invoices
                        SELECT PayDate, 'مشتريات', 'سداد فاتورة شراء رقم ' + ISNULL(CAST(BuyID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), 0, PayMoney FROM Buy_Payments WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        SELECT PayDate, 'مشتريات موتوسيكلات', 'سداد شراء موتوسيكل رقم ' + ISNULL(CAST(BuyID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), 0, PayMoney FROM Buy_Car_Payments WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        -- Import
                        SELECT PayDate, 'استيراد', 'سداد استيراد رقم ' + ISNULL(CAST(InvID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), 0, PayMoney FROM Import_Payments WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        -- Returns
                        SELECT PayDate, 'مرتجع مشتريات', 'استرداد مشتريات رقم ' + ISNULL(CAST(BuyID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), PayMoney, 0 FROM ReBuy_Payments WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        SELECT PayDate, 'مرتجع مبيعات', 'رد مبيعات رقم ' + ISNULL(CAST(SalesID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), 0, PayMoney FROM ReSales_Payments WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        -- Direct Customer Payments
                        SELECT PayDate, 'مقبوضات عملاء', 'استلام دفعة من عميل ' + ISNULL(Notes, ''), PayMoney, 0 FROM Cus_Payments WHERE PayType IN (0,1) " + cashFilter1 + @"
                        UNION ALL
                        SELECT PayDate, 'مقبوضات عملاء', 'رد مبلغ لعميل ' + ISNULL(Notes, ''), 0, PayMoney FROM Cus_Payments WHERE PayType = 2 " + cashFilter1 + @"
                        UNION ALL
                        -- Direct Supplier Payments
                        SELECT PayDate, 'مدفوعات موردين', 'دفعة لمورد ' + ISNULL(Notes, ''), 0, PayMoney FROM Supp_Payments WHERE PayType IN (0,1) " + cashFilter1 + @"
                        UNION ALL
                        SELECT PayDate, 'مدفوعات موردين', 'استرداد من مورد ' + ISNULL(Notes, ''), PayMoney, 0 FROM Supp_Payments WHERE PayType = 2 " + cashFilter1 + @"
                        UNION ALL
                        -- Expenses
                        SELECT PayDate, 'مصروفات', 'صرف ' + ISNULL(Notes, ''), 0, PayMoney FROM Exp_Payments WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        -- Transfers (Out)
                        SELECT PayDate, 'تحويل', 'تحويل صادر إلى خزينة: ' + CAST(CashTo AS VARCHAR) + ' - ' + ISNULL(Notes,''), 0, PayMoney FROM Cash_Transfer WHERE 1=1 " + cashFilter1 + @"
                        UNION ALL
                        -- Transfers (In)
                        SELECT PayDate, 'تحويل', 'تحويل وارد من خزينة: ' + CAST(CashId AS VARCHAR) + ' - ' + ISNULL(Notes,''), PayMoney, 0 FROM Cash_Transfer WHERE 1=1 AND CashTo = @CashId
                    ) T
                    WHERE T.LogDate >= @FromDate AND T.LogDate <= @ToDate
                    ORDER BY T.LogDate DESC";
            }
            else if (SelectedReportType == "كشف حساب خزينة")
            {
                if (SelectedCash == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار الخزينة أولاً", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("CashId", SelectedCash.CashId);

                sql = @"
                    SELECT LogDate AS [SortDate], CONVERT(VARCHAR, LogDate, 103) AS [التاريخ], SourceType AS [الحركة], Details AS [البيان], Debit AS [وارد], Credit AS [صادر]
                    FROM (
                        SELECT PayDate AS LogDate, 'سداد مبيعات' AS SourceType, 'سداد فاتورة بيع رقم ' + ISNULL(CAST(SalesID AS VARCHAR), '') + ' ' + ISNULL(Notes, '') AS Details, PayMoney AS Debit, 0 AS Credit FROM Sales_Payments WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'سداد مبيعات', 'سداد بيع موتوسيكل رقم ' + ISNULL(CAST(SalesID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), PayMoney, 0 FROM Sales_Car_Payments WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'سداد مشتريات', 'سداد فاتورة شراء رقم ' + ISNULL(CAST(BuyID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), 0, PayMoney FROM Buy_Payments WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'سداد مشتريات', 'سداد شراء موتوسيكل رقم ' + ISNULL(CAST(BuyID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), 0, PayMoney FROM Buy_Car_Payments WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'سداد استيراد', 'سداد استيراد رقم ' + ISNULL(CAST(InvID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), 0, PayMoney FROM Import_Payments WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'مقبوضات مرتجع', 'استرداد مشتريات رقم ' + ISNULL(CAST(BuyID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), PayMoney, 0 FROM ReBuy_Payments WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'مدفوعات مرتجع', 'رد مبيعات رقم ' + ISNULL(CAST(SalesID AS VARCHAR), '') + ' ' + ISNULL(Notes, ''), 0, PayMoney FROM ReSales_Payments WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'مقبوضات عميل', 'استلام دفعة من عميل ' + ISNULL(Notes, ''), PayMoney, 0 FROM Cus_Payments WHERE CashID = @CashId AND PayType IN (0,1)
                        UNION ALL
                        SELECT PayDate, 'رد عميل', 'رد مبلغ لعميل ' + ISNULL(Notes, ''), 0, PayMoney FROM Cus_Payments WHERE CashID = @CashId AND PayType = 2
                        UNION ALL
                        SELECT PayDate, 'دفع مورد', 'لمورد ' + ISNULL(Notes, ''), 0, PayMoney FROM Supp_Payments WHERE CashID = @CashId AND PayType IN (0,1)
                        UNION ALL
                        SELECT PayDate, 'استرداد مورد', 'استرداد من مورد ' + ISNULL(Notes, ''), PayMoney, 0 FROM Supp_Payments WHERE CashID = @CashId AND PayType = 2
                        UNION ALL
                        SELECT PayDate, 'مصروفات', ISNULL(Notes, ''), 0, PayMoney FROM Exp_Payments WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'تحويل صادر', ISNULL(Notes,''), 0, PayMoney FROM Cash_Transfer WHERE CashID = @CashId
                        UNION ALL
                        SELECT PayDate, 'تحويل وارد', ISNULL(Notes,''), PayMoney, 0 FROM Cash_Transfer WHERE CashTo = @CashId
                    ) T
                    WHERE LogDate >= @FromDate AND LogDate <= @ToDate
                    ORDER BY SortDate ASC";
            }
            else if (SelectedReportType == "تحويلات الخزائن والبنوك")
            {
                sql = @"
                    SELECT CONVERT(VARCHAR, CT.PayDate, 103) AS [التاريخ],
                           C1.CashName AS [من خزينة/بنك (صادر)],
                           C2.CashName AS [إلى خزينة/بنك (وارد)],
                           CT.PayMoney AS [المبلغ],
                           CT.Notes AS [ملاحظات]
                    FROM Cash_Transfer CT
                    INNER JOIN Cash C1 ON CT.CashID = C1.Cash_ID
                    INNER JOIN Cash C2 ON CT.CashTo = C2.Cash_ID
                    WHERE CT.PayDate >= @FromDate AND CT.PayDate <= @ToDate
                    ORDER BY CT.PayDate DESC";
            }

            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, p))
            {
                dt.Load(reader);
            }

            if (SelectedReportType == "كشف حساب خزينة")
            {
                dt.Columns.Add("الرصيد", typeof(double));

                var opParams = new DynamicParameters();
                opParams.Add("CashId", SelectedCash!.CashId);
                opParams.Add("FromDate", qFrom);

                var openBalInfo = await db.QueryFirstOrDefaultAsync<dynamic>("SELECT ISNULL(Debit,0) - ISNULL(Credit,0) AS Ini FROM Cash WHERE Cash_ID = @CashId", opParams) ?? new {Ini = 0.0};
                double initialVal = (double)(openBalInfo.Ini);

                double moveBeforeFromDate = await db.ExecuteScalarAsync<double>(@"
                    SELECT ISNULL(SUM(Debit),0) - ISNULL(SUM(Credit),0) 
                    FROM (
                        SELECT PayMoney AS Debit, 0 AS Credit FROM Cus_Payments WHERE CashID = @CashId AND PayType IN (0,1) AND PayDate < @FromDate
                        UNION ALL SELECT 0, PayMoney FROM Cus_Payments WHERE CashID = @CashId AND PayType = 2 AND PayDate < @FromDate
                        UNION ALL SELECT 0, PayMoney FROM Supp_Payments WHERE CashID = @CashId AND PayType IN (0,1) AND PayDate < @FromDate
                        UNION ALL SELECT PayMoney, 0 FROM Supp_Payments WHERE CashID = @CashId AND PayType = 2 AND PayDate < @FromDate
                        UNION ALL SELECT 0, PayMoney FROM Exp_Payments WHERE CashID = @CashId AND PayDate < @FromDate
                        UNION ALL SELECT 0, PayMoney FROM Cash_Transfer WHERE CashID = @CashId AND PayDate < @FromDate
                        UNION ALL SELECT PayMoney, 0 FROM Cash_Transfer WHERE CashTo = @CashId AND PayDate < @FromDate

                        UNION ALL SELECT PayMoney, 0 FROM Sales_Payments WHERE CashID = @CashId AND PayDate < @FromDate
                        UNION ALL SELECT PayMoney, 0 FROM Sales_Car_Payments WHERE CashID = @CashId AND PayDate < @FromDate
                        UNION ALL SELECT 0, PayMoney FROM Buy_Payments WHERE CashID = @CashId AND PayDate < @FromDate
                        UNION ALL SELECT 0, PayMoney FROM Buy_Car_Payments WHERE CashID = @CashId AND PayDate < @FromDate
                        UNION ALL SELECT 0, PayMoney FROM Import_Payments WHERE CashID = @CashId AND PayDate < @FromDate
                        UNION ALL SELECT PayMoney, 0 FROM ReBuy_Payments WHERE CashID = @CashId AND PayDate < @FromDate
                        UNION ALL SELECT 0, PayMoney FROM ReSales_Payments WHERE CashID = @CashId AND PayDate < @FromDate
                    ) T
                ", opParams);

                double runningBalance = initialVal + moveBeforeFromDate;

                var opRow = dt.NewRow();
                if (dt.Columns.Contains("SortDate")) dt.Columns["SortDate"]!.AllowDBNull = true;
                if (dt.Columns.Contains("الحركة")) dt.Columns["الحركة"]!.AllowDBNull = true;

                opRow["التاريخ"] = qFrom.ToString("dd/MM/yyyy");
                opRow["الحركة"] = "رصيد افتتاحي";
                opRow["البيان"] = "رصيد سابق افتتاحي";
                if (runningBalance >= 0)
                {
                    opRow["وارد"] = runningBalance;
                    opRow["صادر"] = 0;
                }
                else
                {
                    opRow["وارد"] = 0;
                    opRow["صادر"] = Math.Abs(runningBalance);
                }
                opRow["الرصيد"] = runningBalance;
                
                dt.Rows.InsertAt(opRow, 0);

                for (int i = 1; i < dt.Rows.Count; i++)
                {
                    double d = Convert.ToDouble(dt.Rows[i]["وارد"] == DBNull.Value ? 0 : dt.Rows[i]["وارد"]);
                    double c = Convert.ToDouble(dt.Rows[i]["صادر"] == DBNull.Value ? 0 : dt.Rows[i]["صادر"]);
                    runningBalance += (d - c);
                    dt.Rows[i]["الرصيد"] = runningBalance;
                }

                if (dt.Columns.Contains("SortDate")) dt.Columns.Remove("SortDate");
            }

            ReportData = dt.DefaultView;
            await BuildFooterTotalsAsync(db, p, qFrom, qTo, dt);

            StatusMessage = dt.Rows.Count > 0 ? $"تم العثور على {dt.Rows.Count} سجل" : "⚠️ لا توجد بيانات في الفترة المحددة";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            System.Windows.MessageBox.Show("خطأ:\n" + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task BuildFooterTotalsAsync(System.Data.IDbConnection db, DynamicParameters p, DateTime qFrom, DateTime qTo, System.Data.DataTable dt)
    {
        if (SelectedReportType == "أرصدة الخزائن الحالية")
        {
            var tCash = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Bal),0) FROM Cash WHERE TypeId = 0 AND Active=1", p);
            var tBank = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Bal),0) FROM Cash WHERE TypeId = 1 AND Active=1", p);
            var tTotal= await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Bal),0) FROM Cash WHERE Active=1", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي الخزائن", tCash.ToString("N2") },
                { "إجمالي البنوك",  tBank.ToString("N2") },
                { "الإجمالي الكلي", tTotal.ToString("N2") }
            };
        }
        else if (SelectedReportType is "حركة الصندوق اليومية" or "كشف حساب خزينة")
        {
            double totIn = 0, totOut = 0;
            foreach (System.Data.DataRow r in dt.Rows)
            {
                if(r["وارد"] != DBNull.Value) totIn += Convert.ToDouble(r["وارد"]);
                if(r["صادر"] != DBNull.Value) totOut += Convert.ToDouble(r["صادر"]);
            }
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي الوارد", totIn.ToString("N2") },
                { "إجمالي الصادر", totOut.ToString("N2") },
                { "الصافي", (totIn - totOut).ToString("N2") }
            };
        }
        else if (SelectedReportType == "تحويلات الخزائن والبنوك")
        {
            double sum = 0;
            foreach(System.Data.DataRow r in dt.Rows)
                if(r["المبلغ"] != DBNull.Value) sum += Convert.ToDouble(r["المبلغ"]);
            
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي التحويلات", sum.ToString("N2") }
            };
        }
    }

    private Dictionary<string, string> BuildHeaderInfo(DateTime from, DateTime to)
    {
        var d = new Dictionary<string, string>();
        if (IsDateVisible)
        {
            d.Add("من تاريخ",  from.ToString("dd/MM/yyyy"));
            d.Add("إلى تاريخ", to.ToString("dd/MM/yyyy"));
        }
        if (SelectedCash != null) d.Add("الخزينة/البنك", SelectedCash.CashName);
        return d;
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (ReportData == null || ReportData.Count == 0) return;
        using var db = _dbFactory.CreateConnection();
        var company  = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "PDF File (*.pdf)|*.pdf", DefaultExt = "pdf", FileName = SelectedReportType + " " + DateTime.Now.ToString("yyyy-MM-dd") };
        if (dlg.ShowDialog() == true)
        {
            try { var pdf = MotorBike.Services.ReportGenerator.GeneratePdf(company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals); System.IO.File.WriteAllBytes(dlg.FileName, pdf); }
            catch (Exception ex) { System.Windows.MessageBox.Show("خطأ: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error); }
        }
    }

    [RelayCommand]
    private async Task PrintPdfAsync()
    {
        if (ReportData == null || ReportData.Count == 0) return;
        using var db = _dbFactory.CreateConnection();
        var company  = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
        try { var pdf = MotorBike.Services.ReportGenerator.GeneratePdf(company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals); string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MotorBikeReport_" + Guid.NewGuid() + ".pdf"); System.IO.File.WriteAllBytes(tmp, pdf); MotorBike.Services.ReportGenerator.PrintPdf(tmp); }
        catch (Exception ex) { System.Windows.MessageBox.Show("خطأ: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error); }
    }
}
