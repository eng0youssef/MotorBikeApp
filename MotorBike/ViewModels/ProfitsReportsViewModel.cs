using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ProfitsReportsViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;

    [ObservableProperty]
    private ObservableCollection<string> _reportTypes =
    [
        "ملخص الأرباح والخسائر",
        "ربحية الأصناف (متوسط التكلفة)",
        "ربحية الأصناف (آخر سعر شراء)",
        "ربحية الموتوسيكلات",
        "المصروفات بالنوع",
        "المصروفات بالمجموعات"
    ];

    [ObservableProperty] private string _selectedReportType = "ملخص الأرباح والخسائر";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate   = DateTime.Now;
    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked   = true;

    [ObservableProperty] private ObservableCollection<Item>      _items      = [];
    [ObservableProperty] private Item?                           _selectedItem;
    [ObservableProperty] private ObservableCollection<ExpGroup>  _expGroups  = [];
    [ObservableProperty] private ExpGroup?                       _selectedExpGroup;

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private string? _statusMessage;

    public bool IsItemVisible     => SelectedReportType is "ربحية الأصناف (متوسط التكلفة)"
                                                        or "ربحية الأصناف (آخر سعر شراء)";
    public bool IsExpGroupVisible => SelectedReportType is "المصروفات بالنوع" or "المصروفات بالمجموعات";

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsItemVisible));
        OnPropertyChanged(nameof(IsExpGroupVisible));
        ReportData           = new System.Data.DataView();
        StatusMessage        = null;
        _currentFooterTotals = null;
        _currentHeaderInfo   = null;
    }

    private Dictionary<string, string>? _currentHeaderInfo;
    private Dictionary<string, string>? _currentFooterTotals;

    public ProfitsReportsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Item>     itemRepo,
        IRepository<ExpGroup> expGroupRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(itemRepo, expGroupRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(IRepository<Item> itemRepo, IRepository<ExpGroup> expGroupRepo)
    {
        Items     = new ObservableCollection<Item>     (await itemRepo.GetAllAsync());
        ExpGroups = new ObservableCollection<ExpGroup> (await expGroupRepo.GetAllAsync());
    }

    // ══════════════════════════════════════════════════════════════════
    //  GENERATE
    // ══════════════════════════════════════════════════════════════════
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
            p.Add("ToDate",   qTo);

            _currentHeaderInfo   = BuildHeaderInfo(qFrom, qTo);
            _currentFooterTotals = null;

            string sql;

            // ── 1. ملخص الأرباح والخسائر ──────────────────────────────────
            if (SelectedReportType == "ملخص الأرباح والخسائر")
            {
                sql = @"
                    SELECT البيان, المبلغ FROM (
                        SELECT 1 AS [Sort], 'إجمالي إيرادات مبيعات الأصناف' AS [البيان],
                               ISNULL(SUM(S.Qty * (S.Price - S.Disc)), 0) AS [المبلغ]
                        FROM Sales_Sub S INNER JOIN Sales M ON S.SalesId = M.Sales_ID
                        WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate

                        UNION ALL
                        SELECT 2, 'إجمالي إيرادات بيع الموتوسيكلات',
                               ISNULL(SUM(Total), 0)
                        FROM Sales_Car WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate

                        UNION ALL
                        SELECT 3, 'إجمالي مرتجعات مبيعات الأصناف',
                               -ISNULL(SUM(S.Qty * (S.Price - S.Disc)), 0)
                        FROM ReSales_Sub S INNER JOIN ReSales M ON S.SalesId = M.Sales_ID
                        WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate

                        UNION ALL
                        SELECT 4, 'تكلفة مبيعات الأصناف (متوسط التكلفة)',
                               -ISNULL(SUM(S.Qty * ISNULL(I.AvrgCost, 0)), 0)
                        FROM Sales_Sub S
                        INNER JOIN Sales M ON S.SalesId = M.Sales_ID
                        INNER JOIN Items I ON S.ItemId  = I.Item_ID
                        WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate

                        UNION ALL
                        SELECT 5, 'تكلفة شراء الموتوسيكلات المباعة',
                               -ISNULL(SUM(C.PurchasePrice), 0)
                        FROM Sales_Car SC
                        INNER JOIN Cars C ON SC.CarID = C.Car_ID
                        WHERE SC.SalesDate >= @FromDate AND SC.SalesDate <= @ToDate

                        UNION ALL
                        SELECT 6, 'إجمالي المصروفات التشغيلية',
                               -ISNULL(SUM(PayMoney), 0)
                        FROM Exp_Payments
                        WHERE PayDate >= @FromDate AND PayDate <= @ToDate
                    ) T
                    ORDER BY Sort ASC";
            }
            // ── 2. ربحية الأصناف (متوسط التكلفة) ─────────────────────────
            else if (SelectedReportType == "ربحية الأصناف (متوسط التكلفة)")
            {
                string itemFilter = "";
                if (SelectedItem != null) { itemFilter = " AND S.ItemId = @ItemId "; p.Add("ItemId", SelectedItem.ItemId); }

                sql = @"
                    SELECT I.ItemName                                     AS [الصنف],
                           SUM(S.Qty)                                     AS [الكمية المباعة],
                           AVG(S.Price - S.Disc)                          AS [متوسط سعر البيع],
                           ISNULL(AVG(I.AvrgCost), 0)                     AS [متوسط التكلفة],
                           SUM(S.Qty * (S.Price - S.Disc))                AS [إجمالي الإيرادات],
                           SUM(S.Qty * ISNULL(I.AvrgCost, 0))             AS [إجمالي التكلفة],
                           SUM(S.Qty * ((S.Price - S.Disc) - ISNULL(I.AvrgCost, 0))) AS [إجمالي الربح]
                    FROM Sales_Sub S
                    INNER JOIN Sales M ON S.SalesId = M.Sales_ID
                    INNER JOIN Items I ON S.ItemId  = I.Item_ID
                    WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                    " + itemFilter + @"
                    GROUP BY I.ItemName
                    ORDER BY [إجمالي الربح] DESC";
            }
            // ── 3. ربحية الأصناف (آخر سعر شراء) ──────────────────────────
            else if (SelectedReportType == "ربحية الأصناف (آخر سعر شراء)")
            {
                string itemFilter = "";
                if (SelectedItem != null) { itemFilter = " AND S.ItemId = @ItemId "; p.Add("ItemId", SelectedItem.ItemId); }

                sql = @"
                    SELECT I.ItemName                                                    AS [الصنف],
                           SUM(S.Qty)                                                    AS [الكمية المباعة],
                           AVG(S.Price - S.Disc)                                         AS [متوسط سعر البيع],
                           ISNULL(LP.LastPrice, 0)                                       AS [آخر سعر شراء],
                           SUM(S.Qty * (S.Price - S.Disc))                               AS [إجمالي الإيرادات],
                           SUM(S.Qty * ISNULL(LP.LastPrice, 0))                          AS [إجمالي التكلفة],
                           SUM(S.Qty * ((S.Price - S.Disc) - ISNULL(LP.LastPrice, 0)))   AS [إجمالي الربح]
                    FROM Sales_Sub S
                    INNER JOIN Sales M ON S.SalesId = M.Sales_ID
                    INNER JOIN Items I ON S.ItemId  = I.Item_ID
                    OUTER APPLY (
                        SELECT TOP 1 BS.Price AS LastPrice
                        FROM Buy_Sub BS
                        INNER JOIN Buy BM ON BS.BuyID = BM.Buy_ID
                        WHERE BS.ItemID = S.ItemId
                          AND BM.BuyDate <= M.SalesDate
                        ORDER BY BM.BuyDate DESC
                    ) LP
                    WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                    " + itemFilter + @"
                    GROUP BY I.ItemName, LP.LastPrice
                    ORDER BY [إجمالي الربح] DESC";
            }
            // ── 4. ربحية الموتوسيكلات ─────────────────────────────────────
            else if (SelectedReportType == "ربحية الموتوسيكلات")
            {
                sql = @"
                    SELECT CONVERT(VARCHAR, SC.SalesDate, 103)              AS [تاريخ البيع],
                           CB.BrandName + ' - ' + CM.ModelName              AS [الموديل],
                           C.YearNo                                         AS [السنة],
                           C.ChassisNo                                      AS [رقم الشاسيه],
                           CU.CusName                                       AS [العميل],
                           C.PurchasePrice                                  AS [سعر الشراء],
                           SC.Total                                         AS [سعر البيع],
                           (SC.Total - C.PurchasePrice)                     AS [الربح],
                           CASE WHEN C.PurchasePrice > 0
                                THEN CAST(ROUND(((SC.Total - C.PurchasePrice) / C.PurchasePrice) * 100, 1) AS VARCHAR) + ' %'
                                ELSE '0 %' END                              AS [نسبة الربح]
                    FROM Sales_Car SC
                    INNER JOIN Cars       C  ON SC.CarID   = C.Car_ID
                    INNER JOIN CarModels  CM ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands  CB ON CM.BrandID = CB.Brand_ID
                    LEFT  JOIN Customers  CU ON SC.CusID   = CU.Cus_ID
                    WHERE SC.SalesDate >= @FromDate AND SC.SalesDate <= @ToDate
                    ORDER BY [الربح] DESC";
            }
            // ── 5. المصروفات بالنوع ───────────────────────────────────────
            else if (SelectedReportType == "المصروفات بالنوع")
            {
                string grpFilter = "";
                if (SelectedExpGroup != null) { grpFilter = " AND E.GroupID = @GroupId "; p.Add("GroupId", SelectedExpGroup.GroupId); }

                sql = @"
                    SELECT CONVERT(VARCHAR, P.PayDate, 103)                 AS [التاريخ],
                           EG.GroupName                                     AS [المجموعة],
                           E.ExpName                                        AS [المصروف],
                           P.PayMoney                                       AS [المبلغ],
                           P.Notes                                          AS [ملاحظات]
                    FROM Exp_Payments P
                    INNER JOIN Expenses  E  ON P.ExpID   = E.Exp_ID
                    INNER JOIN Exp_Group EG ON E.GroupID = EG.Group_ID
                    WHERE P.PayDate >= @FromDate AND P.PayDate <= @ToDate
                    " + grpFilter + @"
                    ORDER BY P.PayDate DESC";
            }
            // ── 6. المصروفات بالمجموعات ──────────────────────────────────
            else
            {
                sql = @"
                    SELECT EG.GroupName                                         AS [المجموعة],
                           COUNT(P.Pay_ID)                                      AS [عدد العمليات],
                           SUM(P.PayMoney)                                      AS [إجمالي المصروفات]
                    FROM Exp_Payments P
                    INNER JOIN Expenses  E  ON P.ExpID   = E.Exp_ID
                    INNER JOIN Exp_Group EG ON E.GroupID = EG.Group_ID
                    WHERE P.PayDate >= @FromDate AND P.PayDate <= @ToDate
                    GROUP BY EG.GroupName
                    ORDER BY [إجمالي المصروفات] DESC";
            }

            // ── Execute ───────────────────────────────────────────────────
            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, p))
                dt.Load(reader);

            ReportData = dt.DefaultView;
            await BuildFooterTotalsAsync(db, p, qFrom, qTo, dt);

            StatusMessage = dt.Rows.Count > 0
                ? $"تم العثور على {dt.Rows.Count} سجل"
                : "⚠️ لا توجد بيانات في الفترة المحددة";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            System.Windows.MessageBox.Show("خطأ:\n" + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task BuildFooterTotalsAsync(
        System.Data.IDbConnection db, DynamicParameters p,
        DateTime qFrom, DateTime qTo, System.Data.DataTable dt)
    {
        if (SelectedReportType == "ملخص الأرباح والخسائر")
        {
            // Total revenues - total costs
            var salesRev = await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(S.Qty*(S.Price-S.Disc)),0) FROM Sales_Sub S INNER JOIN Sales M ON S.SalesId=M.Sales_ID WHERE M.SalesDate>=@FromDate AND M.SalesDate<=@ToDate", p);
            var carsRev  = await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(Total),0) FROM Sales_Car WHERE SalesDate>=@FromDate AND SalesDate<=@ToDate", p);
            var salesCost= await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(S.Qty*ISNULL(I.AvrgCost,0)),0) FROM Sales_Sub S INNER JOIN Sales M ON S.SalesId=M.Sales_ID INNER JOIN Items I ON S.ItemId=I.Item_ID WHERE M.SalesDate>=@FromDate AND M.SalesDate<=@ToDate", p);
            var carsCost = await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(C.PurchasePrice),0) FROM Sales_Car SC INNER JOIN Cars C ON SC.CarID=C.Car_ID WHERE SC.SalesDate>=@FromDate AND SC.SalesDate<=@ToDate", p);
            var expenses = await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(PayMoney),0) FROM Exp_Payments WHERE PayDate>=@FromDate AND PayDate<=@ToDate", p);

            double totalRev  = salesRev + carsRev;
            double totalCost = salesCost + carsCost + expenses;

            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي الإيرادات",  totalRev.ToString("N2")              },
                { "إجمالي التكاليف",   totalCost.ToString("N2")             },
                { "صافي الربح",         (totalRev - totalCost).ToString("N2") }
            };
        }
        else if (SelectedReportType is "ربحية الأصناف (متوسط التكلفة)" or "ربحية الأصناف (آخر سعر شراء)")
        {
            var totRev  = await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(S.Qty*(S.Price-S.Disc)),0) FROM Sales_Sub S INNER JOIN Sales M ON S.SalesId=M.Sales_ID WHERE M.SalesDate>=@FromDate AND M.SalesDate<=@ToDate", p);
            var totCost = await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(S.Qty*ISNULL(I.AvrgCost,0)),0) FROM Sales_Sub S INNER JOIN Sales M ON S.SalesId=M.Sales_ID INNER JOIN Items I ON S.ItemId=I.Item_ID WHERE M.SalesDate>=@FromDate AND M.SalesDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي الإيرادات",  totRev.ToString("N2")               },
                { "إجمالي التكاليف",   totCost.ToString("N2")              },
                { "صافي الربح",         (totRev - totCost).ToString("N2")  }
            };
        }
        else if (SelectedReportType == "ربحية الموتوسيكلات")
        {
            var cnt  = await db.ExecuteScalarAsync<int>   ("SELECT COUNT(*) FROM Sales_Car WHERE SalesDate>=@FromDate AND SalesDate<=@ToDate", p);
            var tRev = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(SC.Total),0) FROM Sales_Car SC WHERE SC.SalesDate>=@FromDate AND SC.SalesDate<=@ToDate", p);
            var tCst = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(C.PurchasePrice),0) FROM Sales_Car SC INNER JOIN Cars C ON SC.CarID=C.Car_ID WHERE SC.SalesDate>=@FromDate AND SC.SalesDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "عدد الموتوسيكلات", cnt.ToString()              },
                { "إجمالي البيع",      tRev.ToString("N2")        },
                { "إجمالي الشراء",     tCst.ToString("N2")        },
                { "صافي الأرباح",      (tRev - tCst).ToString("N2") }
            };
        }
        else if (SelectedReportType is "المصروفات بالنوع" or "المصروفات بالمجموعات")
        {
            var totExp = await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(PayMoney),0) FROM Exp_Payments WHERE PayDate>=@FromDate AND PayDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي المصروفات", totExp.ToString("N2") }
            };
        }
    }

    private Dictionary<string, string> BuildHeaderInfo(DateTime from, DateTime to) => new()
    {
        { "من تاريخ",  from.ToString("dd/MM/yyyy") },
        { "إلى تاريخ", to.ToString("dd/MM/yyyy")   }
    };

    // ── EXPORT / PRINT ────────────────────────────────────────────────
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (ReportData == null || ReportData.Count == 0)
        {
            System.Windows.MessageBox.Show("لا توجد بيانات", "تنبيه",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return;
        }
        using var db = _dbFactory.CreateConnection();
        var company  = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
        try
        {
            QuestPDF.Infrastructure.IDocument document = MotorBike.Services.ReportGenerator.CreatePdfDocument(
                company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, SelectedReportType);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("خطأ: " + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PrintPdfAsync()
    {
        if (ReportData == null || ReportData.Count == 0)
        {
            System.Windows.MessageBox.Show("لا توجد بيانات", "تنبيه",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return;
        }
        using var db = _dbFactory.CreateConnection();
        var company  = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
        try
        {
            var document = MotorBike.Services.ReportGenerator.CreatePdfDocument(
                company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
            
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, SelectedReportType);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("خطأ: " + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
    [RelayCommand]
    private void ClearItem() => SelectedItem = null;

    [RelayCommand]
    private void ClearExpGroup() => SelectedExpGroup = null;
}
