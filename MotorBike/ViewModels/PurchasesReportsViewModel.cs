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

public partial class PurchasesReportsViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;

    [ObservableProperty]
    private ObservableCollection<string> _reportTypes =
    [
        "المشتريات بالشهور",
        "المشتريات بالأصناف",
        "مشتريات مورد معين",
        "كشف حساب مورد",
        "كشف حساب تفصيلي مورد",
        "مشتريات موتوسيكلات",
        "مرتجعات المشتريات"
    ];

    [ObservableProperty] private string _selectedReportType = "المشتريات بالشهور";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate   = DateTime.Now;

    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked   = true;

    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = [];
    [ObservableProperty] private Supplier? _selectedSupplier;

    [ObservableProperty] private ObservableCollection<Item> _items = [];
    [ObservableProperty] private Item? _selectedItem;

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private string? _statusMessage;

    // Visibility helpers
    public bool IsSupplierVisible => SelectedReportType is "مشتريات مورد معين"
                                                        or "كشف حساب مورد"
                                                        or "كشف حساب تفصيلي مورد";

    public bool IsItemVisible => SelectedReportType == "المشتريات بالأصناف";

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSupplierVisible));
        OnPropertyChanged(nameof(IsItemVisible));
        ReportData           = new System.Data.DataView();
        StatusMessage        = null;
        _currentFooterTotals = null;
        _currentHeaderInfo   = null;
    }

    private Dictionary<string, string>? _currentHeaderInfo;
    private Dictionary<string, string>? _currentFooterTotals;

    public PurchasesReportsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Supplier> suppRepo,
        IRepository<Item> itemRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(suppRepo, itemRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(IRepository<Supplier> suppRepo, IRepository<Item> itemRepo)
    {
        var supps = await suppRepo.GetAllAsync();
        Suppliers = new ObservableCollection<Supplier>(supps);

        var itms = await itemRepo.GetAllAsync();
        Items = new ObservableCollection<Item>(itms);
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

            _currentHeaderInfo  = BuildHeaderInfo(qFrom, qTo);
            _currentFooterTotals = null;

            string sql;

            // ── 1. المشتريات بالشهور ──────────────────────────────────────
            if (SelectedReportType == "المشتريات بالشهور")
            {
                sql = @"
                    SELECT FORMAT(BuyDate,'yyyy-MM')       AS [الشهر],
                           COUNT(Buy_ID)                   AS [عدد الفواتير],
                           SUM(Total)                      AS [الإجمالي],
                           SUM(Disc)                       AS [الخصم],
                           SUM(Total - Disc + AddMoney)    AS [الصافي]
                    FROM Buy
                    WHERE BuyDate >= @FromDate AND BuyDate <= @ToDate
                    GROUP BY FORMAT(BuyDate,'yyyy-MM')
                    ORDER BY [الشهر] DESC";
            }
            // ── 2. المشتريات بالأصناف ──────────────────────────────────────
            else if (SelectedReportType == "المشتريات بالأصناف")
            {
                string itemFilter = "";
                if (SelectedItem != null) { itemFilter = " AND S.ItemID = @ItemId "; p.Add("ItemId", SelectedItem.ItemId); }

                sql = @"
                    SELECT I.ItemName                               AS [الصنف],
                           SUM(S.Qty)                               AS [الكمية],
                           SUM(S.Qty * (S.Price - S.Disc))         AS [إجمالي المشتريات],
                           AVG(S.Price)                             AS [متوسط السعر]
                    FROM Buy_Sub S
                    INNER JOIN Items I ON S.ItemID = I.Item_ID
                    INNER JOIN Buy   M ON S.BuyID  = M.Buy_ID
                    WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate
                    " + itemFilter + @"
                    GROUP BY I.ItemName
                    ORDER BY [إجمالي المشتريات] DESC";
            }
            // ── 3. مشتريات مورد معين ──────────────────────────────────────
            else if (SelectedReportType == "مشتريات مورد معين")
            {
                string suppFilter = "";
                if (SelectedSupplier != null) { suppFilter = " AND M.SuppID = @SuppId "; p.Add("SuppId", SelectedSupplier.SuppId); }

                sql = @"
                    SELECT CONVERT(VARCHAR, M.BuyDate, 103)     AS [التاريخ],
                           M.Buy_ID                             AS [رقم الفاتورة],
                           S.SuppName                           AS [المورد],
                           M.Total                              AS [الإجمالي],
                           M.Disc                               AS [الخصم],
                           (M.Total - M.Disc + M.AddMoney)     AS [الصافي]
                    FROM Buy M
                    LEFT JOIN Suppliers S ON M.SuppID = S.Supp_ID
                    WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate
                    " + suppFilter + @"
                    ORDER BY M.BuyDate DESC";
            }
            // ── 4. كشف حساب مورد (ملخص) ──────────────────────────────────
            else if (SelectedReportType == "كشف حساب مورد")
            {
                if (SelectedSupplier == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار المورد أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("SuppId", SelectedSupplier.SuppId);

                sql = @"
                    SELECT 'إجمالي المشتريات'  AS [البيان],
                           ISNULL(SUM(Total - Disc + AddMoney), 0) AS [مدين],
                           0                                        AS [دائن]
                    FROM Buy WHERE SuppID = @SuppId AND BuyDate >= @FromDate AND BuyDate <= @ToDate
                    UNION ALL
                    SELECT 'إجمالي المرتجعات',
                           0,
                           ISNULL(SUM(Total - Disc + AddMoney), 0)
                    FROM ReBuy WHERE SuppID = @SuppId AND BuyDate >= @FromDate AND BuyDate <= @ToDate
                    UNION ALL
                    SELECT 'إجمالي المدفوعات',
                           0,
                           ISNULL(SUM(PayMoney), 0)
                    FROM Supp_Payments WHERE SuppID = @SuppId AND PayDate >= @FromDate AND PayDate <= @ToDate AND PayType IN (0,1,3)";
            }
            // ── 5. كشف حساب تفصيلي مورد ──────────────────────────────────
            else if (SelectedReportType == "كشف حساب تفصيلي مورد")
            {
                if (SelectedSupplier == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار المورد أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("SuppId", SelectedSupplier.SuppId);

                sql = @"
                    -- فواتير الشراء
                    SELECT M.BuyDate AS [SortDate],
                           CONVERT(VARCHAR, M.BuyDate, 103)                              AS [التاريخ],
                           'فاتورة شراء رقم ' + CAST(M.Buy_ID AS VARCHAR)              AS [البيان],
                           I.ItemName                                                    AS [الصنف],
                           S.Qty                                                         AS [الكمية],
                           S.Price                                                       AS [السعر],
                           (S.Qty * (S.Price - S.Disc))                                AS [مدين],
                           0                                                             AS [دائن]
                    FROM Buy M
                    INNER JOIN Buy_Sub S ON M.Buy_ID = S.BuyID
                    INNER JOIN Items   I ON S.ItemID  = I.Item_ID
                    WHERE M.SuppID = @SuppId AND M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate

                    UNION ALL

                    -- مرتجعات الشراء
                    SELECT M.BuyDate,
                           CONVERT(VARCHAR, M.BuyDate, 103),
                           'مرتجع شراء رقم ' + CAST(M.Buy_ID AS VARCHAR),
                           I.ItemName, S.Qty, S.Price,
                           0,
                           (S.Qty * (S.Price - S.Disc))
                    FROM ReBuy M
                    INNER JOIN ReBuy_Sub S ON M.Buy_ID = S.BuyID
                    INNER JOIN Items     I ON S.ItemID  = I.Item_ID
                    WHERE M.SuppID = @SuppId AND M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate

                    UNION ALL

                    -- المدفوعات
                    SELECT PayDate,
                           CONVERT(VARCHAR, PayDate, 103),
                           CASE PayType WHEN 0 THEN 'سداد مورد' WHEN 1 THEN 'دفع مورد'
                                        WHEN 2 THEN 'استرداد'   WHEN 3 THEN 'خصم' END,
                           ISNULL(Notes,''), 0, 0,
                           CASE WHEN PayType = 2 THEN PayMoney ELSE 0 END,
                           CASE WHEN PayType IN (0,1,3) THEN PayMoney ELSE 0 END
                    FROM Supp_Payments
                    WHERE SuppID = @SuppId AND PayDate >= @FromDate AND PayDate <= @ToDate

                    ORDER BY SortDate ASC";
            }
            // ── 6. مشتريات موتوسيكلات ────────────────────────────────────
            else if (SelectedReportType == "مشتريات موتوسيكلات")
            {
                sql = @"
                    SELECT CONVERT(VARCHAR, B.BuyDate, 103)                           AS [التاريخ],
                           B.Buy_ID                                                    AS [رقم الفاتورة],
                           B.OwnerName                                                 AS [اسم البائع],
                           B.OwnerTel                                                  AS [التليفون],
                           CB.BrandName + ' - ' + CM.ModelName                        AS [الموديل],
                           C.YearNo                                                    AS [السنة],
                           C.ChassisNo                                                 AS [رقم الشاسيه],
                           C.PlateNo                                                   AS [رقم اللوحة],
                           B.Total                                                     AS [السعر]
                    FROM Buy_Car B
                    LEFT JOIN Cars       C  ON B.CarID   = C.Car_ID
                    LEFT JOIN CarModels  CM ON C.ModelID = CM.Model_ID
                    LEFT JOIN CarBrands  CB ON CM.BrandID = CB.Brand_ID
                    WHERE B.BuyDate >= @FromDate AND B.BuyDate <= @ToDate
                    ORDER BY B.BuyDate DESC";
            }
            // ── 7. مرتجعات المشتريات ──────────────────────────────────────
            else
            {
                sql = @"
                    SELECT CONVERT(VARCHAR, M.BuyDate, 103)    AS [التاريخ],
                           M.Buy_ID                            AS [رقم المرتجع],
                           S.SuppName                          AS [المورد],
                           M.Total                             AS [الإجمالي],
                           M.Disc                              AS [الخصم],
                           (M.Total - M.Disc + M.AddMoney)    AS [الصافي]
                    FROM ReBuy M
                    LEFT JOIN Suppliers S ON M.SuppID = S.Supp_ID
                    WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate
                    ORDER BY M.BuyDate DESC";
            }

            // ── Execute ───────────────────────────────────────────────────
            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, p))
                dt.Load(reader);

            // Post-process: running balance for تفصيلي
            if (SelectedReportType == "كشف حساب تفصيلي مورد")
            {
                dt.Columns.Add("الرصيد", typeof(double));

                // Opening balance before FromDate
                var op = new DynamicParameters();
                op.Add("SuppId", SelectedSupplier!.SuppId);
                op.Add("FromDate", qFrom);

                var opBuys    = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total - Disc + AddMoney),0) FROM Buy         WHERE SuppID=@SuppId AND BuyDate <@FromDate", op);
                var opReturns = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total - Disc + AddMoney),0) FROM ReBuy       WHERE SuppID=@SuppId AND BuyDate <@FromDate", op);
                var opPayments= await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney),0) FROM Supp_Payments WHERE SuppID=@SuppId AND PayDate<@FromDate AND PayType IN(0,1,3)", op);

                double runningBal = opBuys - opReturns - opPayments;

                // Insert opening row
                if (dt.Columns.Contains("SortDate")) dt.Columns["SortDate"]!.AllowDBNull = true;
                if (dt.Columns.Contains("الكمية"))   dt.Columns["الكمية"]!.AllowDBNull = true;
                if (dt.Columns.Contains("السعر"))    dt.Columns["السعر"]!.AllowDBNull = true;

                var opRow = dt.NewRow();
                opRow["التاريخ"]  = qFrom.ToString("dd/MM/yyyy");
                opRow["البيان"]   = "رصيد سابق";
                opRow["الصنف"]    = "";
                opRow["الكمية"]   = 0;
                opRow["السعر"]    = 0;
                opRow["مدين"]     = 0;
                opRow["دائن"]     = 0;
                opRow["الرصيد"]   = runningBal;
                dt.Rows.InsertAt(opRow, 0);

                for (int i = 1; i < dt.Rows.Count; i++)
                {
                    double debit  = Convert.ToDouble(dt.Rows[i]["مدين"]  == DBNull.Value ? 0 : dt.Rows[i]["مدين"]);
                    double credit = Convert.ToDouble(dt.Rows[i]["دائن"]  == DBNull.Value ? 0 : dt.Rows[i]["دائن"]);
                    runningBal   += (debit - credit);
                    dt.Rows[i]["الرصيد"] = runningBal;
                }

                if (dt.Columns.Contains("SortDate")) dt.Columns.Remove("SortDate");
            }

            ReportData = dt.DefaultView;

            // ── Footer Totals ──────────────────────────────────────────────
            await BuildFooterTotalsAsync(db, p, qFrom, qTo, dt);

            StatusMessage = dt.Rows.Count > 0
                ? $"تم العثور على {dt.Rows.Count} سجل"
                : "⚠️ لا توجد بيانات في الفترة المحددة";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            System.Windows.MessageBox.Show("خطأ في استعلام البيانات:\n" + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task BuildFooterTotalsAsync(
        System.Data.IDbConnection db, DynamicParameters p,
        DateTime qFrom, DateTime qTo, System.Data.DataTable dt)
    {
        if (SelectedReportType == "كشف حساب مورد" || SelectedReportType == "كشف حساب تفصيلي مورد")
        {
            var sp = new DynamicParameters();
            sp.Add("SuppId",   SelectedSupplier!.SuppId);
            sp.Add("FromDate", qFrom);
            sp.Add("ToDate",   qTo);

            var buysTotal    = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total-Disc+AddMoney),0) FROM Buy         WHERE SuppID=@SuppId AND BuyDate>=@FromDate AND BuyDate<=@ToDate", sp);
            var returnsTotal = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total-Disc+AddMoney),0) FROM ReBuy       WHERE SuppID=@SuppId AND BuyDate>=@FromDate AND BuyDate<=@ToDate", sp);
            var payTotal     = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney),0) FROM Supp_Payments WHERE SuppID=@SuppId AND PayDate>=@FromDate AND PayDate<=@ToDate AND PayType IN(0,1,3)", sp);

            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي المشتريات",  buysTotal.ToString("N2")                 },
                { "إجمالي المرتجعات",  returnsTotal.ToString("N2")              },
                { "إجمالي المدفوعات",  payTotal.ToString("N2")                  },
                { "الرصيد المتبقي",    (buysTotal - returnsTotal - payTotal).ToString("N2") }
            };
        }
        else if (SelectedReportType == "المشتريات بالشهور")
        {
            var buysTot  = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total-Disc+AddMoney),0) FROM Buy   WHERE BuyDate>=@FromDate AND BuyDate<=@ToDate", p);
            var reTot    = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total-Disc+AddMoney),0) FROM ReBuy WHERE BuyDate>=@FromDate AND BuyDate<=@ToDate", p);
            var cntBuys  = await db.ExecuteScalarAsync<int>   ("SELECT COUNT(Buy_ID) FROM Buy   WHERE BuyDate>=@FromDate AND BuyDate<=@ToDate", p);
            var cntRe    = await db.ExecuteScalarAsync<int>   ("SELECT COUNT(Buy_ID) FROM ReBuy WHERE BuyDate>=@FromDate AND BuyDate<=@ToDate", p);

            _currentFooterTotals = new Dictionary<string, string>
            {
                { "عدد الشهور",         dt.Rows.Count.ToString()     },
                { "عدد الفواتير",        cntBuys.ToString()           },
                { "إجمالي المشتريات",   buysTot.ToString("N2")       },
                { "إجمالي المرتجعات",   reTot.ToString("N2")         },
                { "صافي المشتريات",     (buysTot - reTot).ToString("N2") }
            };
        }
        else if (SelectedReportType == "المشتريات بالأصناف")
        {
            var sumQ = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(S.Qty*(S.Price-S.Disc)),0) FROM Buy_Sub S INNER JOIN Buy M ON S.BuyID=M.Buy_ID WHERE M.BuyDate>=@FromDate AND M.BuyDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي المشتريات", sumQ.ToString("N2") }
            };
        }
        else
        {
            var buysTot = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total-Disc+AddMoney),0) FROM Buy   WHERE BuyDate>=@FromDate AND BuyDate<=@ToDate", p);
            var reTot   = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total-Disc+AddMoney),0) FROM ReBuy WHERE BuyDate>=@FromDate AND BuyDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي المشتريات",  buysTot.ToString("N2")           },
                { "إجمالي المرتجعات",  reTot.ToString("N2")             },
                { "صافي المشتريات",    (buysTot - reTot).ToString("N2") }
            };
        }
    }

    private Dictionary<string, string> BuildHeaderInfo(DateTime from, DateTime to)
    {
        var d = new Dictionary<string, string>
        {
            { "من تاريخ", from.ToString("dd/MM/yyyy") },
            { "إلى تاريخ", to.ToString("dd/MM/yyyy")  }
        };
        if (SelectedSupplier != null) d.Add("المورد", SelectedSupplier.SuppName);
        if (SelectedItem     != null) d.Add("الصنف",  SelectedItem.ItemName);
        return d;
    }

    // ══════════════════════════════════════════════════════════════════
    //  EXPORT PDF
    // ══════════════════════════════════════════════════════════════════
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (ReportData == null || ReportData.Count == 0)
        {
            System.Windows.MessageBox.Show("لا توجد بيانات ليتم تصديرها", "تنبيه",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        using var db = _dbFactory.CreateConnection();
        var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter      = "PDF File (*.pdf)|*.pdf",
            DefaultExt  = "pdf",
            FileName    = SelectedReportType + " " + DateTime.Now.ToString("yyyy-MM-dd")
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var pdf = MotorBike.Services.ReportGenerator.GeneratePdf(
                    company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
                System.IO.File.WriteAllBytes(dlg.FileName, pdf);
                System.Windows.MessageBox.Show("تم حفظ التقرير بنجاح", "نجاح",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("خطأ أثناء التصدير: " + ex.Message, "خطأ",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  PRINT PDF
    // ══════════════════════════════════════════════════════════════════
    [RelayCommand]
    private async Task PrintPdfAsync()
    {
        if (ReportData == null || ReportData.Count == 0)
        {
            System.Windows.MessageBox.Show("لا توجد بيانات ليتم طباعتها", "تنبيه",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        using var db = _dbFactory.CreateConnection();
        var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

        try
        {
            var pdf = MotorBike.Services.ReportGenerator.GeneratePdf(
                company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "MotorBikeReport_" + Guid.NewGuid() + ".pdf");
            System.IO.File.WriteAllBytes(tmp, pdf);
            MotorBike.Services.ReportGenerator.PrintPdf(tmp);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("خطأ أثناء الطباعة: " + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
