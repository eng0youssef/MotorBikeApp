using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class StoreReportsViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;

    [ObservableProperty]
    private ObservableCollection<string> _reportTypes =
    [
        "رصيد المخزون الحالي",
        "حركة صنف معين",
        "الأصناف الأكثر حركة",
        "الأصناف تحت الحد الأدنى",
        "رصيد المخازن بالفئات"
    ];

    [ObservableProperty] private string _selectedReportType = "رصيد المخزون الحالي";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate   = DateTime.Now;
    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked   = true;

    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private Store? _selectedStore;

    [ObservableProperty] private ObservableCollection<Item> _items = [];
    [ObservableProperty] private Item? _selectedItem;

    [ObservableProperty] private ObservableCollection<ItemCategory> _categories = [];
    [ObservableProperty] private ItemCategory? _selectedCategory;

    // خيار نوع السعر المستخدم في حسابات القيمة
    [ObservableProperty]
    private ObservableCollection<string> _priceTypes =
    [
        "متوسط  التكلفة",
        "سعر الشراء"
    ];
    [ObservableProperty] private string _selectedPriceType = "متوسط التكلفة";

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private string? _statusMessage;

    public bool IsDateRangeVisible  => SelectedReportType is "حركة صنف معين" or "الأصناف الأكثر حركة";
    public bool IsStoreVisible      => SelectedReportType is "رصيد المخزون الحالي" or "رصيد المخازن بالفئات";
    public bool IsItemVisible       => SelectedReportType == "حركة صنف معين";
    public bool IsCategoryVisible   => SelectedReportType == "رصيد المخازن بالفئات";
    public bool IsPriceTypeVisible  => SelectedReportType is "رصيد المخزون الحالي" or "رصيد المخازن بالفئات";

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDateRangeVisible));
        OnPropertyChanged(nameof(IsStoreVisible));
        OnPropertyChanged(nameof(IsItemVisible));
        OnPropertyChanged(nameof(IsCategoryVisible));
        OnPropertyChanged(nameof(IsPriceTypeVisible));
        ReportData    = new System.Data.DataView();
        StatusMessage = null;
    }

    private Dictionary<string, string>? _currentHeaderInfo;
    private Dictionary<string, string>? _currentFooterTotals;

    public StoreReportsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Store> storeRepo,
        IRepository<Item> itemRepo,
        IRepository<ItemCategory> catRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(storeRepo, itemRepo, catRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(
        IRepository<Store> storeRepo, IRepository<Item> itemRepo, IRepository<ItemCategory> catRepo)
    {
        Stores     = new ObservableCollection<Store>        (await storeRepo.GetAllAsync());
        Items      = new ObservableCollection<Item>         (await itemRepo.GetAllAsync());
        Categories = new ObservableCollection<ItemCategory> (await catRepo.GetAllAsync());
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

            // اختيار عمود السعر حسب خيار المستخدم
            string priceCol = SelectedPriceType == "سعر الشراء" ? "Price0" : "AvrgCost";
            string priceLabel = SelectedPriceType == "سعر الشراء" ? "سعر الشراء" : "متوسط التكلفة";

            // ── 1. رصيد المخزون الحالي ────────────────────────────────────
            if (SelectedReportType == "رصيد المخزون الحالي")
            {
                string storeFilter = "";
                if (SelectedStore != null) { storeFilter = " AND S.StoreID = @StoreId "; p.Add("StoreId", SelectedStore.StoreId); }

                sql = $@"
                    SELECT IC.CatName                                      AS [الفئة],
                           I.ItemName                                     AS [الصنف],
                           ST.StoreName                                   AS [المخزن],
                           S.Qty                                          AS [الكمية المتاحة],
                           ISNULL(I.{priceCol}, 0)                        AS [{priceLabel}],
                           (S.Qty * ISNULL(I.{priceCol}, 0))             AS [إجمالي القيمة]
                    FROM Stock S
                    INNER JOIN Items         I  ON S.ItemID  = I.Item_ID
                    INNER JOIN Stores        ST ON S.StoreID = ST.Store_ID
                    INNER JOIN Item_Category IC ON I.CatID   = IC.Cat_ID
                    WHERE S.Qty > 0
                    {storeFilter}
                    ORDER BY IC.CatName, I.ItemName";
            }
            // ── 2. حركة صنف معين ──────────────────────────────────────────
            else if (SelectedReportType == "حركة صنف معين")
            {
                if (SelectedItem == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار الصنف أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("ItemId", SelectedItem.ItemId);

                // ── حركة صنف معين من الجداول الأصلية ─────────────────────
                // رصيد أول المدة = مجموع كل الحركات السابقة لتاريخ البداية
                // ثم كل الحركات (شراء / بيع / مرتجعات / استيراد) في النطاق الزمني
                // الرصيد التراكمي محسوب بـ SUM OVER (ORDER BY ...)
                sql = @"
                    ;WITH Movements AS (

                        -- ما قبله (كل الواردة - الصادرة قبل تاريخ البداية)
                        SELECT
                            CAST(@FromDate AS DATETIME)         AS SortDate,
                            0                                    AS RefId,
                            N'ما قبله'                          AS MovType,
                            ISNULL((
                                SELECT SUM(QtyAll) FROM Open_Stock      WHERE ItemID = @ItemId AND OpenDate  < @FromDate
                            ),0)
                            + ISNULL((
                                SELECT SUM(bs.QtyAll) FROM Buy_Sub bs INNER JOIN Buy b ON bs.BuyID = b.Buy_ID WHERE bs.ItemID = @ItemId AND b.BuyDate < @FromDate
                            ),0)
                            + ISNULL((
                                SELECT SUM(ii.QtyAll) FROM Import_Inv_Item ii INNER JOIN Import_Invoice inv ON ii.InvID = inv.Inv_ID WHERE ii.ItemID = @ItemId AND inv.InvDate < @FromDate
                            ),0)
                            + ISNULL((
                                SELECT SUM(rs.QtyAll) FROM ReSales_Sub rs INNER JOIN ReSales r ON rs.SalesId = r.Sales_ID WHERE rs.ItemID = @ItemId AND r.SalesDate < @FromDate
                            ),0)
                            - ISNULL((
                                SELECT SUM(ss.QtyAll) FROM Sales_Sub ss INNER JOIN Sales s ON ss.SalesId = s.Sales_ID WHERE ss.ItemID = @ItemId AND s.SalesDate < @FromDate
                            ),0)
                            - ISNULL((
                                SELECT SUM(rb.QtyAll) FROM ReBuy_Sub rb INNER JOIN ReBuy r ON rb.BuyID = r.Buy_ID WHERE rb.ItemID = @ItemId AND r.BuyDate < @FromDate
                            ),0)                                 AS InQty,
                            0                                    AS OutQty,
                            0                                    AS SortOrder

                        UNION ALL

                        -- فواتير الشراء
                        SELECT
                            b.BuyDate                            AS SortDate,
                            b.Buy_ID                             AS RefId,
                            N'شراء'                             AS MovType,
                            bs.QtyAll                            AS InQty,
                            0                                    AS OutQty,
                            1                                    AS SortOrder
                        FROM Buy_Sub bs
                        INNER JOIN Buy b ON bs.BuyID = b.Buy_ID
                        WHERE bs.ItemID = @ItemId
                          AND b.BuyDate >= @FromDate AND b.BuyDate <= @ToDate

                        UNION ALL

                        -- فواتير الاستيراد
                        SELECT
                            CAST(inv.InvDate AS DATETIME)        AS SortDate,
                            inv.Inv_ID                           AS RefId,
                            N'استيراد'                          AS MovType,
                            ii.QtyAll                            AS InQty,
                            0                                    AS OutQty,
                            1                                    AS SortOrder
                        FROM Import_Inv_Item ii
                        INNER JOIN Import_Invoice inv ON ii.InvID = inv.Inv_ID
                        WHERE ii.ItemID = @ItemId
                          AND inv.InvDate >= @FromDate AND inv.InvDate <= @ToDate

                        UNION ALL

                        -- فواتير البيع
                        SELECT
                            s.SalesDate                          AS SortDate,
                            s.Sales_ID                           AS RefId,
                            N'بيع'                              AS MovType,
                            0                                    AS InQty,
                            ss.QtyAll                            AS OutQty,
                            1                                    AS SortOrder
                        FROM Sales_Sub ss
                        INNER JOIN Sales s ON ss.SalesId = s.Sales_ID
                        WHERE ss.ItemID = @ItemId
                          AND s.SalesDate >= @FromDate AND s.SalesDate <= @ToDate

                        UNION ALL

                        -- مرتجع مشتريات (صادر من المخزن)
                        SELECT
                            r.BuyDate                            AS SortDate,
                            r.Buy_ID                             AS RefId,
                            N'مرتجع مشتريات'                    AS MovType,
                            0                                    AS InQty,
                            rb.QtyAll                            AS OutQty,
                            1                                    AS SortOrder
                        FROM ReBuy_Sub rb
                        INNER JOIN ReBuy r ON rb.BuyID = r.Buy_ID
                        WHERE rb.ItemID = @ItemId
                          AND r.BuyDate >= @FromDate AND r.BuyDate <= @ToDate

                        UNION ALL

                        -- مرتجع مبيعات (وارد للمخزن)
                        SELECT
                            r.SalesDate                          AS SortDate,
                            r.Sales_ID                           AS RefId,
                            N'مرتجع مبيعات'                     AS MovType,
                            rs.QtyAll                            AS InQty,
                            0                                    AS OutQty,
                            1                                    AS SortOrder
                        FROM ReSales_Sub rs
                        INNER JOIN ReSales r ON rs.SalesId = r.Sales_ID
                        WHERE rs.ItemID = @ItemId
                          AND r.SalesDate >= @FromDate AND r.SalesDate <= @ToDate

                        UNION ALL

                        -- رصيد افتتاحي داخل الفترة
                        SELECT
                            CAST(os.OpenDate AS DATETIME)        AS SortDate,
                            0                                    AS RefId,
                            N'رصيد افتتاحي'                     AS MovType,
                            os.QtyAll                            AS InQty,
                            0                                    AS OutQty,
                            0                                    AS SortOrder
                        FROM Open_Stock os
                        WHERE os.ItemID = @ItemId
                          AND os.OpenDate >= @FromDate AND os.OpenDate <= @ToDate
                    ),
                    Numbered AS (
                        SELECT *,
                               ROW_NUMBER() OVER (ORDER BY SortDate ASC, SortOrder ASC, RefId ASC) AS RowNum
                        FROM Movements
                    ),
                    WithBalance AS (
                        SELECT
                            n.SortDate,
                            n.RefId,
                            n.MovType,
                            n.InQty,
                            n.OutQty,
                            SUM(n.InQty - n.OutQty) OVER (ORDER BY n.SortDate ASC, n.SortOrder ASC, n.RefId ASC
                                                           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningQty
                        FROM Numbered n
                    )
                    SELECT
                        SortDate                               AS [SortDate],
                        CONVERT(VARCHAR, SortDate, 103)        AS [التاريخ],
                        MovType                                AS [نوع الحركة],
                        CASE WHEN RefId = 0 THEN N'-' ELSE CAST(RefId AS VARCHAR) END AS [رقم الحركة],
                        CASE WHEN InQty  = 0 THEN NULL ELSE InQty  END AS [وارد],
                        CASE WHEN OutQty = 0 THEN NULL ELSE OutQty END AS [صادر],
                        RunningQty                             AS [الرصيد]
                    FROM WithBalance
                    WHERE NOT (MovType = N'ما قبله' AND RunningQty = 0)
                    ORDER BY SortDate ASC, MovType ASC, RefId ASC";
            }
            // ── 3. الأصناف الأكثر حركة ───────────────────────────────────
            else if (SelectedReportType == "الأصناف الأكثر حركة")
            {
                sql = @"
                    SELECT I.ItemName                              AS [الصنف],
                           IC.CatName                             AS [الفئة],
                           SUM(SS.Qty)                            AS [إجمالي المباع],
                           COUNT(DISTINCT M.Sales_ID)             AS [عدد الفواتير],
                           SUM(SS.Qty * (SS.Price - SS.Disc))    AS [إجمالي الإيرادات]
                    FROM Sales_Sub SS
                    INNER JOIN Items         I  ON SS.ItemId = I.Item_ID
                    INNER JOIN Item_Category IC ON I.CatID   = IC.Cat_ID
                    INNER JOIN Sales         M  ON SS.SalesId = M.Sales_ID
                    WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                    GROUP BY I.ItemName, IC.CatName
                    ORDER BY [إجمالي المباع] DESC";
            }
            // ── 4. الأصناف تحت الحد الأدنى ───────────────────────────────
            else if (SelectedReportType == "الأصناف تحت الحد الأدنى")
            {
                sql = @"
                    SELECT IC.CatName                          AS [الفئة],
                           I.ItemName                         AS [الصنف],
                           ST.StoreName                       AS [المخزن],
                           S.Qty                              AS [الكمية المتاحة],
                           I.Limit                            AS [الحد الأدنى],
                           (I.Limit - S.Qty)                  AS [الكمية الناقصة]
                    FROM Stock S
                    INNER JOIN Items         I  ON S.ItemID  = I.Item_ID
                    INNER JOIN Stores        ST ON S.StoreID = ST.Store_ID
                    INNER JOIN Item_Category IC ON I.CatID   = IC.Cat_ID
                    WHERE I.IsLimit = 1 AND S.Qty < I.Limit
                    ORDER BY [الكمية الناقصة] DESC";
            }
            // ── 5. رصيد المخازن بالفئات ──────────────────────────────────
            else
            {
                string catFilter   = "";
                string storeFilter = "";
                if (SelectedCategory != null) { catFilter   = " AND I.CatID = @CatId ";    p.Add("CatId",   SelectedCategory.CatId); }
                if (SelectedStore    != null) { storeFilter = " AND S.StoreID = @StoreId "; p.Add("StoreId", SelectedStore.StoreId); }

                sql = $@"
                    SELECT IC.CatName                                        AS [الفئة],
                           ST.StoreName                                      AS [المخزن],
                           COUNT(DISTINCT I.Item_ID)                         AS [عدد الأصناف],
                           SUM(S.Qty)                                        AS [إجمالي الكمية],
                           SUM(S.Qty * ISNULL(I.{priceCol}, 0))             AS [إجمالي القيمة]
                    FROM Stock S
                    INNER JOIN Items         I  ON S.ItemID  = I.Item_ID
                    INNER JOIN Stores        ST ON S.StoreID = ST.Store_ID
                    INNER JOIN Item_Category IC ON I.CatID   = IC.Cat_ID
                    WHERE S.Qty > 0
                    {catFilter} {storeFilter}
                    GROUP BY IC.CatName, ST.StoreName
                    ORDER BY IC.CatName, ST.StoreName";
            }

            // ── Execute ───────────────────────────────────────────────────
            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, p))
                dt.Load(reader);

            // Remove SortDate from حركة صنف معين
            if (dt.Columns.Contains("SortDate")) dt.Columns.Remove("SortDate");

            ReportData = dt.DefaultView;
            await BuildFooterTotalsAsync(db, p, dt);

            StatusMessage = dt.Rows.Count > 0
                ? $"تم العثور على {dt.Rows.Count} سجل"
                : "⚠️ لا توجد بيانات";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            System.Windows.MessageBox.Show("خطأ:\n" + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task BuildFooterTotalsAsync(
        System.Data.IDbConnection db, DynamicParameters p, System.Data.DataTable dt)
    {
        string priceCol = SelectedPriceType == "سعر الشراء" ? "Price0" : "AvrgCost";

        if (SelectedReportType == "رصيد المخزون الحالي")
        {
            var totVal = await db.ExecuteScalarAsync<double>(
                $"SELECT ISNULL(SUM(S.Qty * ISNULL(I.{priceCol},0)),0) FROM Stock S INNER JOIN Items I ON S.ItemID=I.Item_ID WHERE S.Qty>0");
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "عدد الأصناف المتاحة",  dt.Rows.Count.ToString()  },
                { "إجمالي قيمة المخزون", totVal.ToString("N2")      }
            };
        }
        else if (SelectedReportType == "الأصناف تحت الحد الأدنى")
        {
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "عدد الأصناف تحت الحد", dt.Rows.Count.ToString() }
            };
        }
        else if (SelectedReportType == "رصيد المخازن بالفئات")
        {
            var totVal = await db.ExecuteScalarAsync<double>(
                $"SELECT ISNULL(SUM(S.Qty * ISNULL(I.{priceCol},0)),0) FROM Stock S INNER JOIN Items I ON S.ItemID=I.Item_ID WHERE S.Qty>0");
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي قيمة المخزون", totVal.ToString("N2") }
            };
        }
    }

    private Dictionary<string, string> BuildHeaderInfo(DateTime from, DateTime to)
    {
        var d = new Dictionary<string, string>();
        if (IsDateRangeVisible)
        {
            d.Add("من تاريخ",  from.ToString("dd/MM/yyyy"));
            d.Add("إلى تاريخ", to.ToString("dd/MM/yyyy"));
        }
        if (SelectedStore    != null) d.Add("المخزن",  SelectedStore.StoreName);
        if (SelectedItem     != null) d.Add("الصنف",   SelectedItem.ItemName);
        if (SelectedCategory != null) d.Add("الفئة",   SelectedCategory.CatName);
        return d;
    }

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
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF File (*.pdf)|*.pdf", DefaultExt = "pdf",
            FileName = SelectedReportType + " " + DateTime.Now.ToString("yyyy-MM-dd")
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
                System.Windows.MessageBox.Show("خطأ: " + ex.Message, "خطأ",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
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
            var pdf = MotorBike.Services.ReportGenerator.GeneratePdf(
                company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "MotorBikeReport_" + Guid.NewGuid() + ".pdf");
            System.IO.File.WriteAllBytes(tmp, pdf);
            MotorBike.Services.ReportGenerator.PrintPdf(tmp);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("خطأ: " + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
