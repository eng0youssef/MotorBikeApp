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

    [ObservableProperty] private ObservableCollection<string> _reportTypes =
    [
        "المشتريات بالشهور",
        "المشتريات بالأصناف",
        "المشتريات بالموردين",
        "كشف حساب مورد",
        "كشف حساب تفصيلي للمورد",
        // ── التقارير الإضافية ──
        "المشتريات اليومية",
        "مشتريات الموتوسيكلات",
        "المشتريات بالفواتير",
        "المشتريات بالفواتير مفصل",
        "أعلى الموردين مشترياً",
        "أرصدة الموردين"
    ];
    [ObservableProperty] private string _selectedReportType = "المشتريات بالشهور";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate = DateTime.Now;

    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked = true;

    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = [];
    [ObservableProperty] private Supplier? _selectedSupplier;

    [ObservableProperty] private ObservableCollection<Item> _items = [];
    [ObservableProperty] private Item? _selectedItem;

    [ObservableProperty] private ObservableCollection<CarModel> _carModels = [];
    [ObservableProperty] private CarModel? _selectedCarModel;

    [ObservableProperty] private ObservableCollection<CarBrand> _carBrands = [];
    [ObservableProperty] private CarBrand? _selectedCarBrand;

    [ObservableProperty] private ObservableCollection<Color> _carColors = [];
    [ObservableProperty] private Color? _selectedCarColor;

    [ObservableProperty] private ObservableCollection<short> _carYears = [];
    [ObservableProperty] private short? _selectedCarYear;

    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private Store? _selectedStore;

    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private Cash? _selectedSafe;

    [ObservableProperty] private ObservableCollection<City> _cities = [];
    [ObservableProperty] private City? _selectedCity;

    // ── فلتر نوع الفاتورة (مشتريات / مرتجعات) ──
    public ObservableCollection<string> InvoiceStatusFilterTypes { get; } = ["الكل", "فواتير مشتريات", "فواتير مرتجعات"];
    [ObservableProperty] private string _selectedInvoiceStatusFilter = "الكل";

    // فلتر نوع الحركة يظهر فقط لتقارير الفواتير
    public bool IsInvoiceStatusFilterVisible => SelectedReportType is "المشتريات بالفواتير مفصل" or "المشتريات بالفواتير";

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private ObservableCollection<DetailedAccountRow> _detailedReportData = [];
    [ObservableProperty] private bool _isDetailedReport;
    [ObservableProperty] private bool _isInvoiceMode;   // true → تقرير المشتريات بالفواتير مفصل

    public bool IsCashVisible => SelectedReportType
                                                    is "المشتريات بالفواتير مفصل"
                                                    or "المشتريات بالفواتير"
                                                    or "المشتريات بالشهور"
                                                    or "المشتريات اليومية"
                                                    or "المشتريات بالأصناف"
                                                    or "المشتريات بالموردين";
    public bool IsStoreVisible => SelectedReportType
                                                    is "المشتريات بالفواتير مفصل"
                                                    or "المشتريات بالفواتير"
                                                    or "المشتريات بالشهور"
                                                    or "المشتريات اليومية"
                                                    or "المشتريات بالأصناف"
                                                    or "المشتريات بالموردين";
    public bool IsSupplierVisible => SelectedReportType is "المشتريات بالموردين"
                                                        or "كشف حساب مورد"
                                                        or "كشف حساب تفصيلي للمورد"
                                                        or "المشتريات بالفواتير مفصل"
                                                        or "المشتريات بالفواتير"
                                                        or "أرصدة الموردين";
    public bool IsItemVisible      => SelectedReportType == "المشتريات بالأصناف";
    public bool IsCarModelVisible  => SelectedReportType == "مشتريات الموتوسيكلات";
    public bool IsMotorcycleReport => SelectedReportType == "مشتريات الموتوسيكلات";
    public bool IsCityFilterVisible => SelectedReportType == "أرصدة الموردين";

    [ObservableProperty] private int _motorcyclesCount;
    [ObservableProperty] private double _motorcyclesTotalBuys;

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSupplierVisible));
        OnPropertyChanged(nameof(IsItemVisible));
        OnPropertyChanged(nameof(IsCarModelVisible));
        OnPropertyChanged(nameof(IsMotorcycleReport));
        OnPropertyChanged(nameof(IsCashVisible));
        OnPropertyChanged(nameof(IsStoreVisible));
        OnPropertyChanged(nameof(IsInvoiceStatusFilterVisible));
        OnPropertyChanged(nameof(IsCityFilterVisible));
        ReportData              = new System.Data.DataView();
        DetailedReportData      = [];
        IsDetailedReport        = false;
        IsInvoiceMode           = false;
        StatusMessage           = null;
        _currentFooterTotals    = null;
        _currentHeaderInfo      = null;
    }

    public PurchasesReportsViewModel(IDbConnectionFactory dbFactory,
        IRepository<Supplier> supplierRepo,
        IRepository<Item>     itemRepo,
        IRepository<CarModel> carModelRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(supplierRepo, itemRepo, carModelRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(
        IRepository<Supplier> supplierRepo,
        IRepository<Item>     itemRepo,
        IRepository<CarModel> carModelRepo)
    {
        Suppliers = new ObservableCollection<Supplier>(await supplierRepo.GetAllAsync());
        Items     = new ObservableCollection<Item>    (await itemRepo.GetAllAsync());
        CarModels = new ObservableCollection<CarModel>(await carModelRepo.GetAllAsync());

        using var db = _dbFactory.CreateConnection();
        CarBrands = new ObservableCollection<CarBrand>(await db.QueryAsync<CarBrand>("SELECT * FROM CarBrands WHERE Active = 1"));
        CarColors = new ObservableCollection<Color>(await db.QueryAsync<Color>("SELECT Color_ID AS ColorId, ColorName, Notes, Active FROM Colors WHERE Active = 1"));
        CarYears  = new ObservableCollection<short>(await db.QueryAsync<short>("SELECT DISTINCT YearNo FROM Cars ORDER BY YearNo DESC"));
        Stores    = new ObservableCollection<Store>(await db.QueryAsync<Store>("SELECT * FROM Stores"));
        Cashes    = new ObservableCollection<Cash>(await db.QueryAsync<Cash>("SELECT Cash_ID AS CashId, * FROM Cash WHERE ISNULL(OmlaId,0) = 0"));
        Cities    = new ObservableCollection<City>(await db.QueryAsync<City>("SELECT City_ID AS CityId, CityName FROM City ORDER BY CityName"));
    }

    [RelayCommand] private void ClearSupplier()  => SelectedSupplier  = null;
    [RelayCommand] private void ClearItem()       => SelectedItem      = null;
    [RelayCommand] private void ClearCarBrand()   => SelectedCarBrand  = null;
    [RelayCommand] private void ClearCarModel()   => SelectedCarModel  = null;
    [RelayCommand] private void ClearCarColor()   => SelectedCarColor  = null;
    [RelayCommand] private void ClearCarYear()    => SelectedCarYear   = null;
    [RelayCommand] private void ClearStore()      => SelectedStore     = null;
    [RelayCommand] private void ClearSafe()       => SelectedSafe      = null;
    [RelayCommand] private void ClearCity()       => SelectedCity      = null;

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

            _currentHeaderInfo = new Dictionary<string, string>
            {
                { "من تاريخ", IsFromDateChecked ? FromDate.ToString("dd/MM/yyyy") : "الكل" },
                { "إلى تاريخ", IsToDateChecked ? ToDate.ToString("dd/MM/yyyy") : "الكل" }
            };
            if (SelectedSupplier != null) _currentHeaderInfo.Add("المورد", SelectedSupplier.SuppName);
            if (SelectedStore != null) _currentHeaderInfo.Add("المخزن", SelectedStore.StoreName);
            if (SelectedSafe != null) _currentHeaderInfo.Add("الخزينة", SelectedSafe.CashName);
            if (IsInvoiceStatusFilterVisible && SelectedInvoiceStatusFilter != "الكل") _currentHeaderInfo.Add("حالة الفاتورة", SelectedInvoiceStatusFilter);

            string buysExtraFilter = "";
            string reBuysExtraFilter = "";
            if (SelectedStore != null)
            {
                buysExtraFilter   += " AND EXISTS (SELECT 1 FROM Buy_Sub BS WHERE BS.BuyID = M.Buy_ID AND BS.StoreId = @StoreId) ";
                reBuysExtraFilter += " AND EXISTS (SELECT 1 FROM ReBuy_Sub RS WHERE RS.BuyID = M.Buy_ID AND RS.StoreId = @StoreId) ";
                parameters.Add("StoreId", SelectedStore.StoreId);
            }
            if (SelectedSafe != null)
            {
                buysExtraFilter   += " AND EXISTS (SELECT 1 FROM Buy_Payments BP WHERE BP.BuyID = M.Buy_ID AND BP.CashId = @CashId) ";
                reBuysExtraFilter += " AND EXISTS (SELECT 1 FROM ReBuy_Payments RP WHERE RP.BuyID = M.Buy_ID AND RP.CashId = @CashId) ";
                parameters.Add("CashId", SelectedSafe.CashId);
            }

            // ── 1. المشتريات بالشهور ──────────────────────────────────────
            if (SelectedReportType == "المشتريات بالشهور")
            {
                sql = $@"
                    ;WITH BuysMonths AS (
                        SELECT FORMAT(M.BuyDate, 'yyyy-MM') AS MonthStr,
                               COUNT(DISTINCT M.Buy_ID) AS CountBills,
                               SUM(M.Net) AS SumTotal
                        FROM Buy M WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {buysExtraFilter} GROUP BY FORMAT(M.BuyDate, 'yyyy-MM')
                    ),
                    ReturnMonths AS (
                        SELECT FORMAT(M.BuyDate, 'yyyy-MM') AS MonthStr,
                               COUNT(DISTINCT M.Buy_ID) AS CountRet,
                               SUM(M.Net) AS SumRetTotal
                        FROM ReBuy M WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {reBuysExtraFilter} GROUP BY FORMAT(M.BuyDate, 'yyyy-MM')
                    ),
                    SuppMonths AS (
                        SELECT MonthStr, COUNT(DISTINCT SuppId) AS SuppCount FROM (
                            SELECT FORMAT(M.BuyDate, 'yyyy-MM') AS MonthStr, M.SuppId FROM Buy M WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {buysExtraFilter}
                            UNION
                            SELECT FORMAT(M.BuyDate, 'yyyy-MM') AS MonthStr, M.SuppId FROM ReBuy M WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {reBuysExtraFilter}
                        ) AS AllSupp GROUP BY MonthStr
                    ),
                    AllMonths AS (
                        SELECT MonthStr FROM BuysMonths UNION SELECT MonthStr FROM ReturnMonths
                    )
                    SELECT m.MonthStr AS [الشهر],
                           ISNULL(s.SumTotal, 0) AS [قيمة المشتريات],
                           ISNULL(r.SumRetTotal, 0) AS [قيمة المرتجعات],
                           (ISNULL(s.SumTotal, 0) - ISNULL(r.SumRetTotal, 0)) AS [صافي المشتريات],
                           ISNULL(s.CountBills, 0) AS [عدد الفواتير],
                           ISNULL(r.CountRet, 0) AS [عدد المرتجعات],
                           ISNULL(c.SuppCount, 0) AS [عدد الموردين]
                    FROM AllMonths m
                    LEFT JOIN BuysMonths s ON m.MonthStr = s.MonthStr
                    LEFT JOIN ReturnMonths r ON m.MonthStr = r.MonthStr
                    LEFT JOIN SuppMonths c ON m.MonthStr = c.MonthStr
                    ORDER BY m.MonthStr DESC";
            }
            // ── 2. المشتريات بالأصناف ──────────────────────────────────────
            else if (SelectedReportType == "المشتريات بالأصناف")
            {
                string itemFilter = "";
                if (SelectedItem != null)
                {
                    itemFilter = " AND A.ItemId = @ItemId ";
                    parameters.Add("ItemId", SelectedItem.ItemId);
                }
                sql = $@"
                    ;WITH BuysData AS (
                        SELECT S.ItemID AS ItemId,
                               SUM(S.Qty) AS BQty,
                               SUM(S.Qty * (S.Price - S.Disc)) AS BValue,
                               COUNT(DISTINCT M.Buy_ID) AS BInvCount
                        FROM Buy_Sub S JOIN Buy M ON S.BuyID = M.Buy_ID
                        WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {buysExtraFilter}
                        GROUP BY S.ItemID
                    ),
                    ReturnsData AS (
                        SELECT S.ItemID AS ItemId,
                               SUM(S.Qty) AS RQty,
                               SUM(S.Qty * (S.Price - S.Disc)) AS RValue,
                               COUNT(DISTINCT M.Buy_ID) AS RInvCount
                        FROM ReBuy_Sub S JOIN ReBuy M ON S.BuyID = M.Buy_ID
                        WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {reBuysExtraFilter}
                        GROUP BY S.ItemID
                    ),
                    AllItems AS (
                        SELECT ItemId FROM BuysData UNION SELECT ItemId FROM ReturnsData
                    ),
                    ItemStats AS (
                        SELECT A.ItemId, I.ItemName, C.CatName,
                               ISNULL(BD.BQty, 0) AS BQty, ISNULL(BD.BValue, 0) AS BValue, ISNULL(BD.BInvCount, 0) AS BInvCount,
                               ISNULL(RD.RQty, 0) AS RQty, ISNULL(RD.RValue, 0) AS RValue, ISNULL(RD.RInvCount, 0) AS RInvCount,
                               (ISNULL(BD.BQty, 0) - ISNULL(RD.RQty, 0)) AS NetQty,
                               (ISNULL(BD.BValue, 0) - ISNULL(RD.RValue, 0)) AS NetValue,
                               (SELECT COUNT(DISTINCT SuppId) FROM (
                                   SELECT M.SuppId FROM Buy_Sub Sub JOIN Buy M ON Sub.BuyID = M.Buy_ID WHERE Sub.ItemID = A.ItemId AND M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {buysExtraFilter}
                                   UNION
                                   SELECT M.SuppId FROM ReBuy_Sub Sub JOIN ReBuy M ON Sub.BuyID = M.Buy_ID WHERE Sub.ItemID = A.ItemId AND M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {reBuysExtraFilter}
                               ) T) AS SuppCount
                        FROM AllItems A
                        JOIN Items I ON A.ItemId = I.Item_ID
                        JOIN Item_Category C ON I.CatID = C.Cat_ID
                        LEFT JOIN BuysData BD ON A.ItemId = BD.ItemId
                        LEFT JOIN ReturnsData RD ON A.ItemId = RD.ItemId
                        WHERE 1=1 {itemFilter}
                    )
                    SELECT
                        CatName AS [المجموعة],
                        ISNULL(ItemName, 'إجمالي المجموعة ->') AS [الصنف],
                        SUM(BQty) AS [كمية الشراء],
                        SUM(RQty) AS [كمية المرتجع],
                        SUM(NetQty) AS [صافي الكمية],
                        SUM(BInvCount) AS [عدد فواتير الشراء],
                        SUM(BValue) AS [قيمة الشراء],
                        SUM(RInvCount) AS [عدد فواتير المرتجع],
                        SUM(RValue) AS [قيمة المرتجع],
                        SUM(NetValue) AS [صافي المشتريات],
                        CASE WHEN SUM(BQty) > 0 THEN SUM(BValue) / SUM(BQty) ELSE 0 END AS [متوسط سعر الشراء],
                        SUM(SuppCount) AS [عدد الموردين]
                    FROM ItemStats
                    GROUP BY GROUPING SETS (
                        (CatName, ItemName),
                        (CatName)
                    )
                    ORDER BY CatName, CASE WHEN ItemName IS NULL THEN 1 ELSE 0 END, ItemName";
            }
            // ── 3. المشتريات بالموردين ─────────────────────────────────────
            else if (SelectedReportType == "المشتريات بالموردين")
            {
                string suppFilter = "";
                if (SelectedSupplier != null)
                {
                    suppFilter += " AND SuppId = @SuppId ";
                    parameters.Add("SuppId", SelectedSupplier.SuppId);
                }

                sql = $@"
                    ;WITH BuysData AS (
                        SELECT M.SuppId,
                               COUNT(M.Buy_ID) AS BuysCount,
                               SUM(M.Net) AS BuysTotal
                        FROM Buy M
                        WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {suppFilter} {buysExtraFilter}
                        GROUP BY M.SuppId
                    ),
                    ReturnsData AS (
                        SELECT M.SuppId,
                               COUNT(M.Buy_ID) AS ReturnsCount,
                               SUM(M.Net) AS ReturnsTotal
                        FROM ReBuy M
                        WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {suppFilter} {reBuysExtraFilter}
                        GROUP BY M.SuppId
                    ),
                    AllSuppliers AS (
                        SELECT SuppId FROM BuysData
                        UNION
                        SELECT SuppId FROM ReturnsData
                    )
                    SELECT
                        ISNULL(S.Supp_ID, 0) AS [الكود],
                        ISNULL(S.SuppName, 'مورد نقدي/غير محدد') AS [الاسم],
                        ISNULL(B.BuysCount, 0) AS [عدد فواتير شراء],
                        ISNULL(B.BuysTotal, 0) AS [قيمة المشتريات],
                        ISNULL(R.ReturnsCount, 0) AS [عدد فواتير مرتجع],
                        ISNULL(R.ReturnsTotal, 0) AS [قيمة المرتجعات],
                        (ISNULL(B.BuysTotal, 0) - ISNULL(R.ReturnsTotal, 0)) AS [صافي المشتريات]
                    FROM AllSuppliers A
                    LEFT JOIN Suppliers S ON A.SuppId = S.Supp_ID
                    LEFT JOIN BuysData B ON A.SuppId = B.SuppId
                    LEFT JOIN ReturnsData R ON A.SuppId = R.SuppId
                    ORDER BY [صافي المشتريات] DESC";
            }
            // ── 4. كشف حساب مورد ──────────────────────────────────────────
            else if (SelectedReportType == "كشف حساب مورد")
            {
                if (SelectedSupplier == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار المورد أولاً", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                parameters.Add("SuppId", SelectedSupplier.SuppId);

                sql = @"
                    SELECT SortDate,
                           CONVERT(VARCHAR, SortDate, 103) AS [التاريخ],
                           RefNo AS [رقم الحركة],
                           TransType AS [نوع الحركة],
                           Details AS [البيان],
                           Debit AS [مدين (عليه)],
                           Credit AS [دائن (له)]
                    FROM (
                        -- فواتير شراء الأصناف
                        SELECT BuyDate AS SortDate, CAST(Buy_ID AS VARCHAR) AS RefNo, 'فاتورة شراء' AS TransType, ISNULL(Notes, '') AS Details, (Total - Disc + AddMoney) AS Debit, 0 AS Credit
                        FROM Buy WHERE SuppId = @SuppId AND BuyDate >= @FromDate AND BuyDate <= @ToDate

                        UNION ALL

                        -- مدفوعات فواتير الشراء المعمولة مع الفاتورة
                        SELECT BP.PayDate, CAST(BP.BuyID AS VARCHAR), 'سداد مع الفاتورة', ISNULL(BP.Notes, ''), 0, BP.PayMoney
                        FROM Buy_Payments BP
                        INNER JOIN Buy B ON BP.BuyID = B.Buy_ID
                        WHERE B.SuppId = @SuppId AND BP.PayDate >= @FromDate AND BP.PayDate <= @ToDate

                        UNION ALL

                        -- فواتير شراء موتوسيكلات من المورد
                        SELECT BC.BuyDate, CAST(BC.Buy_ID AS VARCHAR), 'شراء موتوسيكل', ISNULL(BC.Notes, ''), BC.Net, 0
                        FROM Buy_Car BC
                        INNER JOIN Cars C ON BC.CarID = C.Car_ID
                        WHERE C.IsLocalSupplier = 1 AND C.SupplierId = @SuppId AND BC.BuyDate >= @FromDate AND BC.BuyDate <= @ToDate

                        UNION ALL

                        -- مدفوعات فواتير شراء الموتوسيكلات من المورد
                        SELECT BCP.PayDate, CAST(BCP.BuyID AS VARCHAR), 'سداد مع الفاتورة', ISNULL(BCP.Notes, ''), 0, BCP.PayMoney
                        FROM Buy_Car_Payments BCP
                        INNER JOIN Buy_Car BC ON BCP.BuyID = BC.Buy_ID
                        INNER JOIN Cars C ON BC.CarID = C.Car_ID
                        WHERE C.IsLocalSupplier = 1 AND C.SupplierId = @SuppId AND BCP.PayDate >= @FromDate AND BCP.PayDate <= @ToDate

                        UNION ALL

                        -- مرتجعات المشتريات
                        SELECT BuyDate, CAST(Buy_ID AS VARCHAR), 'مرتجع شراء', ISNULL(Notes, ''), 0, (Total - Disc + AddMoney)
                        FROM ReBuy WHERE SuppId = @SuppId AND BuyDate >= @FromDate AND BuyDate <= @ToDate

                        UNION ALL

                        -- سداد مع مرتجع الشراء
                        SELECT RP.PayDate, CAST(RP.BuyID AS VARCHAR), 'سداد مع المرتجع', ISNULL(RP.Notes, ''), RP.PayMoney, 0
                        FROM ReBuy_Payments RP
                        INNER JOIN ReBuy RB ON RP.BuyID = RB.Buy_ID
                        WHERE RB.SuppId = @SuppId AND RP.PayDate >= @FromDate AND RP.PayDate <= @ToDate

                        UNION ALL

                        -- مدفوعات ومتحصلات منفصلة للمورد
                        SELECT PayDate, CAST(Pay_ID AS VARCHAR),
                               CASE PayType WHEN 0 THEN 'سداد' WHEN 1 THEN 'سداد' WHEN 2 THEN 'استرداد' WHEN 3 THEN 'خصم مكتسب' END,
                               ISNULL(Notes, ''),
                               CASE WHEN PayType = 2 THEN PayMoney ELSE 0 END,
                               CASE WHEN PayType IN (0, 1, 3) THEN PayMoney ELSE 0 END
                        FROM Supp_Payments WHERE SuppId = @SuppId AND PayDate >= @FromDate AND PayDate <= @ToDate
                    ) T
                    ORDER BY SortDate ASC";
            }
            // ── 5. كشف حساب تفصيلي للمورد ────────────────────────────────
            else if (SelectedReportType == "كشف حساب تفصيلي للمورد")
            {
                if (SelectedSupplier == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار المورد أولاً", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                await GenerateDetailedStatementAsync(db, parameters, queryFromDate, queryToDate);
                return;  // early return — no further DataTable processing needed
            }
            // ── 6. المشتريات اليومية ──────────────────────────────────────
            else if (SelectedReportType == "المشتريات اليومية")
            {
                sql = $@"
        ;WITH BuysDaily AS (
            SELECT CAST(M.BuyDate AS DATE) AS TransDate,
                   COUNT(M.Buy_ID) AS BuysCount,
                   SUM(M.Net) AS NetBuys
            FROM Buy M
            WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {buysExtraFilter}
            GROUP BY CAST(M.BuyDate AS DATE)
        ),
        ReturnsDaily AS (
            SELECT CAST(M.BuyDate AS DATE) AS TransDate,
                   COUNT(M.Buy_ID) AS RetCount,
                   SUM(M.Net) AS NetReturns
            FROM ReBuy M
            WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate {reBuysExtraFilter}
            GROUP BY CAST(M.BuyDate AS DATE)
        ),
        AllDates AS (
            SELECT TransDate FROM BuysDaily
            UNION
            SELECT TransDate FROM ReturnsDaily
        )
        SELECT
            CONVERT(VARCHAR, D.TransDate, 103)               AS [التاريخ],
            ISNULL(S.BuysCount, 0)                          AS [عدد فواتير الشراء],
            ISNULL(S.NetBuys, 0)                            AS [قيمة المشتريات],
            ISNULL(R.RetCount, 0)                            AS [عدد فواتير المرتجع],
            ISNULL(R.NetReturns, 0)                          AS [قيمة المرتجعات],
            (ISNULL(S.NetBuys, 0) - ISNULL(R.NetReturns, 0)) AS [صافي المشتريات]
        FROM AllDates D
        LEFT JOIN BuysDaily S ON D.TransDate = S.TransDate
        LEFT JOIN ReturnsDaily R ON D.TransDate = R.TransDate
        ORDER BY D.TransDate DESC";
            }
            // ── 7. مشتريات الموتوسيكلات ───────────────────────────────────
            else if (SelectedReportType == "مشتريات الموتوسيكلات")
            {
                string modelFilter = "";
                if (SelectedCarModel != null)
                {
                    modelFilter += " AND CM.Model_ID = @ModelId ";
                    parameters.Add("ModelId", SelectedCarModel.ModelId);
                }
                if (SelectedCarBrand != null)
                {
                    modelFilter += " AND CB.Brand_ID = @BrandId ";
                    parameters.Add("BrandId", SelectedCarBrand.BrandId);
                }
                if (SelectedCarColor != null)
                {
                    modelFilter += " AND C.ColorId = @ColorId ";
                    parameters.Add("ColorId", SelectedCarColor.ColorId);
                }
                if (SelectedCarYear.HasValue)
                {
                    modelFilter += " AND C.YearNo = @YearNo ";
                    parameters.Add("YearNo", SelectedCarYear.Value);
                }
                // ملاحظة: جدول Buy_Car لا يحتوي على SuppId، لذلك لا يمكن تصفية الموتوسيكلات بالمورد

                sql = @"
                    SELECT CONVERT(VARCHAR, BC.BuyDate, 103)           AS [التاريخ],
                           BC.Buy_ID                                   AS [رقم الفاتورة],
                           BC.OwnerName                                AS [البائع],
                           ISNULL(BC.OwnerTel,'')                     AS [التليفون],
                           CB.BrandName + ' - ' + CM.ModelName         AS [الموديل],
                           COL.ColorName                               AS [اللون],
                           C.YearNo                                    AS [السنة],
                           C.ChassisNo                                 AS [رقم الشاسيه],
                           C.PlateNo                                   AS [رقم اللوحة],
                           BC.Net                                      AS [سعر الشراء]
                    FROM Buy_Car BC
                    INNER JOIN Cars       C   ON BC.CarID   = C.Car_ID
                    INNER JOIN CarModels  CM  ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands  CB  ON CM.BrandID = CB.Brand_ID
                    LEFT  JOIN Colors     COL ON C.ColorId  = COL.Color_ID
                    WHERE BC.BuyDate >= @FromDate AND BC.BuyDate <= @ToDate
                    " + modelFilter + @"
                    ORDER BY BC.BuyDate DESC";
            }
            // ── 8. المشتريات بالفواتير مفصل ──────────────────────────────
            else if (SelectedReportType == "المشتريات بالفواتير مفصل")
            {
                if (SelectedSupplier != null) parameters.Add("SuppId", SelectedSupplier.SuppId);
                await GenerateInvoicesBuysDetailedAsync(db, queryFromDate, queryToDate, SelectedInvoiceStatusFilter);
                return;
            }
            // ── 9. المشتريات بالفواتير (بدون تفاصيل) ─────────────────────
            else if (SelectedReportType == "المشتريات بالفواتير")
            {
                string suppFlt = "";
                if (SelectedSupplier != null)
                {
                    suppFlt = " AND M.SuppId = @SuppId ";
                    parameters.Add("SuppId", SelectedSupplier.SuppId);
                }

                string buysQuery = $@"
                        SELECT M.BuyDate AS SortDate,
                               CONVERT(VARCHAR, M.BuyDate, 103)         AS [التاريخ],
                               M.Buy_ID                                 AS [رقم الفاتورة],
                               N'شراء'                                  AS [نوع الحركة],
                               ISNULL(SP.SuppName, N'مورد نقدي')       AS [المورد],
                               M.Total                                   AS [إجمالي الفاتورة],
                               M.Disc                                    AS [الخصم],
                               M.AddMoney                                AS [الإضافة],
                               M.Net                                     AS [صافي الفاتورة]
                        FROM Buy M
                        LEFT JOIN Suppliers SP ON M.SuppId = SP.Supp_ID
                        WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate
                        {suppFlt} {buysExtraFilter}";

                string returnsQuery = $@"
                        SELECT M.BuyDate AS SortDate,
                               CONVERT(VARCHAR, M.BuyDate, 103)         AS [التاريخ],
                               M.Buy_ID                                 AS [رقم الفاتورة],
                               N'مرتجع شراء'                            AS [نوع الحركة],
                               ISNULL(SP.SuppName, N'مورد نقدي')       AS [المورد],
                               -(M.Total)                               AS [إجمالي الفاتورة],
                               -(M.Disc)                                AS [الخصم],
                               -(M.AddMoney)                            AS [الإضافة],
                               -(M.Net)                                 AS [صافي الفاتورة]
                        FROM ReBuy M
                        LEFT JOIN Suppliers SP ON M.SuppId = SP.Supp_ID
                        WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate
                        {suppFlt} {reBuysExtraFilter}";

                string cteBody = "";
                if (SelectedInvoiceStatusFilter == "فواتير مشتريات")
                    cteBody = buysQuery;
                else if (SelectedInvoiceStatusFilter == "فواتير مرتجعات")
                    cteBody = returnsQuery;
                else
                    cteBody = buysQuery + " \n UNION ALL \n " + returnsQuery;

                sql = $@"
                    ;WITH AllInvoices AS (
                        {cteBody}
                    )
                    SELECT [التاريخ], [رقم الفاتورة], [نوع الحركة], [المورد],
                           [إجمالي الفاتورة], [الخصم], [الإضافة], [صافي الفاتورة]
                    FROM AllInvoices
                    ORDER BY SortDate DESC, [رقم الفاتورة] DESC";
            }
            // ── 10. أعلى الموردين مشترياً ─────────────────────────────────
            else if (SelectedReportType == "أعلى الموردين مشترياً")
            {
                sql = $@"
                    SELECT SP.SuppName                                          AS [المورد],
                           COUNT(DISTINCT M.Buy_ID)                            AS [عدد الفواتير],
                           SUM(M.Net)                                          AS [إجمالي المشتريات],
                           ISNULL(RET.TotalReturns, 0)                         AS [إجمالي المرتجعات],
                           SUM(M.Net) - ISNULL(RET.TotalReturns, 0)           AS [صافي المشتريات],
                           ISNULL(PAY.TotalPayments, 0)                        AS [إجمالي المدفوع],
                           SUM(M.Net) - ISNULL(RET.TotalReturns, 0)
                               - ISNULL(PAY.TotalPayments, 0)                 AS [الرصيد المتبقي]
                    FROM Buy M
                    LEFT JOIN Suppliers SP ON M.SuppId = SP.Supp_ID
                    OUTER APPLY (
                        SELECT ISNULL(SUM(Net), 0) AS TotalReturns
                        FROM ReBuy
                        WHERE SuppId = M.SuppId
                          AND BuyDate >= @FromDate AND BuyDate <= @ToDate
                    ) RET
                    OUTER APPLY (
                        SELECT ISNULL(SUM(PayMoney), 0) AS TotalPayments
                        FROM Supp_Payments
                        WHERE SuppId = M.SuppId
                          AND PayDate >= @FromDate AND PayDate <= @ToDate
                          AND PayType IN (0, 1, 3)
                    ) PAY
                    WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate
                    GROUP BY SP.SuppName, RET.TotalReturns, PAY.TotalPayments
                    ORDER BY [صافي المشتريات] DESC";
            }
            // ── 11. أرصدة الموردين ────────────────────────────────────────
            else if (SelectedReportType == "أرصدة الموردين")
            {
                string cityFilter = "";
                if (SelectedCity != null)
                {
                    cityFilter = " AND SP.CityId = @CityId ";
                    parameters.Add("CityId", SelectedCity.CityId);
                }

                string suppFilter = "";
                if (SelectedSupplier != null)
                {
                    suppFilter = " AND SP.Supp_ID = @SuppId ";
                    parameters.Add("SuppId", SelectedSupplier.SuppId);
                }

                sql = $@"
                    ;WITH SupplierMovements AS (
                        -- الرصيد الافتتاحي للمورد
                        SELECT Supp_ID AS SuppId,
                               ISNULL(Debit,0) - ISNULL(Credit,0) AS NetBal
                        FROM Suppliers

                        UNION ALL

                        -- فواتير المشتريات (مدين)
                        SELECT SuppId, Net
                        FROM Buy
                        WHERE BuyDate >= @FromDate AND BuyDate <= @ToDate

                        UNION ALL

                        -- مرتجعات المشتريات (دائن)
                        SELECT SuppId, -Net
                        FROM ReBuy
                        WHERE BuyDate >= @FromDate AND BuyDate <= @ToDate

                        UNION ALL

                        -- شراء موتوسيكلات من المورد (مدين) — via Cars.SupplierId
                        SELECT C.SupplierId AS SuppId, BC.Net
                        FROM Buy_Car BC
                        INNER JOIN Cars C ON BC.CarID = C.Car_ID
                        WHERE C.IsLocalSupplier = 1 AND C.SupplierId IS NOT NULL
                          AND BC.BuyDate >= @FromDate AND BC.BuyDate <= @ToDate

                        UNION ALL

                        -- مدفوعات مع فواتير شراء الموتوسيكلات (دائن)
                        SELECT C.SupplierId, -BCP.PayMoney
                        FROM Buy_Car_Payments BCP
                        INNER JOIN Buy_Car BC ON BCP.BuyID = BC.Buy_ID
                        INNER JOIN Cars C ON BC.CarID = C.Car_ID
                        WHERE C.IsLocalSupplier = 1 AND C.SupplierId IS NOT NULL
                          AND BCP.PayDate >= @FromDate AND BCP.PayDate <= @ToDate

                        UNION ALL

                        -- مدفوعات مع فواتير المشتريات العادية (دائن)
                        SELECT B.SuppId, -BP.PayMoney
                        FROM Buy_Payments BP
                        INNER JOIN Buy B ON BP.BuyID = B.Buy_ID
                        WHERE BP.PayDate >= @FromDate AND BP.PayDate <= @ToDate

                        UNION ALL

                        -- سداد مع مرتجع الشراء (مدين)
                        SELECT RB.SuppId, RP.PayMoney
                        FROM ReBuy_Payments RP
                        INNER JOIN ReBuy RB ON RP.BuyID = RB.Buy_ID
                        WHERE RP.PayDate >= @FromDate AND RP.PayDate <= @ToDate

                        UNION ALL

                        -- مدفوعات ومتحصلات منفصلة للمورد
                        SELECT SuppId,
                               CASE WHEN PayType IN (0,1,3) THEN -PayMoney ELSE PayMoney END
                        FROM Supp_Payments
                        WHERE PayDate >= @FromDate AND PayDate <= @ToDate
                    ),
                    SupplierBalance AS (
                        SELECT SuppId, SUM(NetBal) AS NetBalance
                        FROM SupplierMovements
                        GROUP BY SuppId
                        HAVING SUM(NetBal) <> 0
                    )
                    SELECT
                        SP.Supp_ID                              AS [كود المورد],
                        SP.SuppName                             AS [اسم المورد],
                        ISNULL(SP.Tel, '')                      AS [تليفون],
                        ISNULL(CI.CityName, '')                 AS [المدينة],
                        CASE WHEN SB.NetBalance > 0 THEN SB.NetBalance  ELSE 0 END AS [مدين (عليه)],
                        CASE WHEN SB.NetBalance < 0 THEN ABS(SB.NetBalance) ELSE 0 END AS [دائن (له)]
                    FROM SupplierBalance SB
                    INNER JOIN Suppliers SP ON SB.SuppId = SP.Supp_ID
                    LEFT  JOIN City      CI ON SP.CityId = CI.City_ID
                    WHERE SP.Active = 1 {cityFilter} {suppFilter}
                    ORDER BY [مدين (عليه)] DESC, [دائن (له)] DESC";
            }

            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, parameters))
            {
                dt.Load(reader);
            }

            if (SelectedReportType == "كشف حساب مورد")
            {
                // Add الرصيد columns
                dt.Columns.Add("رصيد مدين", typeof(double));
                dt.Columns.Add("رصيد دائن", typeof(double));

                double runningBalance = 0;

                var opParams = new DynamicParameters();
                opParams.Add("SuppId", SelectedSupplier!.SuppId);
                opParams.Add("FromDate", queryFromDate);

                // Initial Balance from Suppliers table
                var supplierInfo = await db.QueryFirstOrDefaultAsync<dynamic>("SELECT ISNULL(Debit, 0) AS Debit, ISNULL(Credit, 0) AS Credit, OpenDate FROM Suppliers WHERE Supp_ID = @SuppId", opParams);
                double suppIniDebit  = Convert.ToDouble(supplierInfo?.Debit  ?? 0);
                double suppIniCredit = Convert.ToDouble(supplierInfo?.Credit ?? 0);

                // Previous Transactions before FromDate
                double prevBuys       = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Net), 0) FROM Buy WHERE SuppId = @SuppId AND BuyDate < @FromDate", opParams);
                double prevReBuys     = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Net), 0) FROM ReBuy WHERE SuppId = @SuppId AND BuyDate < @FromDate", opParams);
                double prevPayCredit  = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney), 0) FROM Supp_Payments WHERE SuppId = @SuppId AND PayDate < @FromDate AND PayType IN (0, 1, 3)", opParams);
                double prevPayDebit   = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney), 0) FROM Supp_Payments WHERE SuppId = @SuppId AND PayDate < @FromDate AND PayType = 2", opParams);
                double prevBuyPay     = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BP.PayMoney), 0) FROM Buy_Payments BP JOIN Buy B ON BP.BuyID = B.Buy_ID WHERE B.SuppId = @SuppId AND BP.PayDate < @FromDate", opParams);
                double prevBuysCar    = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BC.Net), 0) FROM Buy_Car BC INNER JOIN Cars C ON BC.CarID = C.Car_ID WHERE C.IsLocalSupplier = 1 AND C.SupplierId = @SuppId AND BC.BuyDate < @FromDate", opParams);
                double prevBuyCarPay  = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BCP.PayMoney), 0) FROM Buy_Car_Payments BCP INNER JOIN Buy_Car BC ON BCP.BuyID = BC.Buy_ID INNER JOIN Cars C ON BC.CarID = C.Car_ID WHERE C.IsLocalSupplier = 1 AND C.SupplierId = @SuppId AND BCP.PayDate < @FromDate", opParams);
                double prevReBuyPay   = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(RP.PayMoney), 0) FROM ReBuy_Payments RP JOIN ReBuy RB ON RP.BuyID = RB.Buy_ID WHERE RB.SuppId = @SuppId AND RP.PayDate < @FromDate", opParams);

                DateTime openDate    = (supplierInfo?.OpenDate != null) ? Convert.ToDateTime((object?)supplierInfo.OpenDate) : new DateTime(1900, 1, 1);
                bool openDateBefore  = openDate < queryFromDate;

                double prevDebit  = prevBuys + prevBuysCar + prevPayDebit + prevReBuyPay + (openDateBefore ? suppIniDebit : 0);
                double prevCredit = prevReBuys + prevPayCredit + prevBuyPay + prevBuyCarPay + (openDateBefore ? suppIniCredit : 0);

                if (dt.Columns.Contains("SortDate")) dt.Columns["SortDate"]!.AllowDBNull = true;

                int insertedRows = 0;

                string colDebit  = "مدين (عليه)";
                string colCredit = "دائن (له)";

                if (dt.Columns.Contains("نوع الحركة")) dt.Columns["نوع الحركة"]!.AllowDBNull = true;
                if (dt.Columns.Contains("رقم الحركة")) dt.Columns["رقم الحركة"]!.AllowDBNull = true;

                var rowSabiq = dt.NewRow();
                rowSabiq["التاريخ"]  = queryFromDate.ToString("dd/MM/yyyy");
                rowSabiq["نوع الحركة"] = "ما قبله";
                rowSabiq["البيان"]   = "الرصيد السابق";
                rowSabiq[colDebit]   = prevDebit;
                rowSabiq[colCredit]  = prevCredit;
                runningBalance += (prevDebit - prevCredit);

                rowSabiq["رصيد مدين"] = runningBalance > 0 ? runningBalance : 0;
                rowSabiq["رصيد دائن"] = runningBalance < 0 ? Math.Abs(runningBalance) : 0;
                dt.Rows.InsertAt(rowSabiq, insertedRows++);

                if (!openDateBefore && (suppIniDebit > 0 || suppIniCredit > 0))
                {
                    runningBalance += (suppIniDebit - suppIniCredit);
                    var rowIft = dt.NewRow();
                    rowIft["التاريخ"]  = $"{openDate:dd/MM/yyyy}";
                    rowIft["نوع الحركة"] = "افتتاحي";
                    rowIft["البيان"]   = "الرصيد الافتتاحي";
                    rowIft[colDebit]   = suppIniDebit;
                    rowIft[colCredit]  = suppIniCredit;

                    rowIft["رصيد مدين"] = runningBalance > 0 ? runningBalance : 0;
                    rowIft["رصيد دائن"] = runningBalance < 0 ? Math.Abs(runningBalance) : 0;
                    dt.Rows.InsertAt(rowIft, insertedRows++);
                }

                for (int i = insertedRows; i < dt.Rows.Count; i++)
                {
                    double d = Convert.ToDouble(dt.Rows[i][colDebit]  == DBNull.Value ? 0 : dt.Rows[i][colDebit]);
                    double c = Convert.ToDouble(dt.Rows[i][colCredit] == DBNull.Value ? 0 : dt.Rows[i][colCredit]);
                    runningBalance += (d - c);

                    dt.Rows[i]["رصيد مدين"] = runningBalance > 0 ? runningBalance : 0;
                    dt.Rows[i]["رصيد دائن"] = runningBalance < 0 ? Math.Abs(runningBalance) : 0;
                }
                if (dt.Columns.Contains("SortDate")) dt.Columns.Remove("SortDate");
            }

            ReportData = dt.DefaultView;

            // ── Generate Totals and Metadata for PDF Report ──
            if (SelectedReportType == "المشتريات بالأصناف")
            {
                double sumBQty = 0, sumRQty = 0, sumNetQty = 0;
                double sumBVal = 0, sumRVal = 0, sumNetVal = 0;
                int itemsCount = 0;

                foreach (System.Data.DataRowView row in dt.DefaultView)
                {
                    if (row["الصنف"].ToString() != "إجمالي المجموعة ->")
                    {
                        itemsCount++;
                        sumBQty   += Convert.ToDouble(row["كمية الشراء"]   == DBNull.Value ? 0 : row["كمية الشراء"]);
                        sumRQty   += Convert.ToDouble(row["كمية المرتجع"]  == DBNull.Value ? 0 : row["كمية المرتجع"]);
                        sumNetQty += Convert.ToDouble(row["صافي الكمية"]   == DBNull.Value ? 0 : row["صافي الكمية"]);
                        sumBVal   += Convert.ToDouble(row["قيمة الشراء"]   == DBNull.Value ? 0 : row["قيمة الشراء"]);
                        sumRVal   += Convert.ToDouble(row["قيمة المرتجع"]  == DBNull.Value ? 0 : row["قيمة المرتجع"]);
                        sumNetVal += Convert.ToDouble(row["صافي المشتريات"] == DBNull.Value ? 0 : row["صافي المشتريات"]);
                    }
                }

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد الأصناف",          itemsCount.ToString()       },
                    { "كمية الشراء",           sumBQty.ToString("N2")      },
                    { "كمية المرتجع",          sumRQty.ToString("N2")      },
                    { "صافي الكمية",           sumNetQty.ToString("N2")    },
                    { "إجمالي المشتريات",      sumBVal.ToString("N2")      },
                    { "إجمالي المرتجعات",      sumRVal.ToString("N2")      },
                    { "صافي المشتريات",        sumNetVal.ToString("N2")    }
                };
            }
            else if (SelectedReportType == "كشف حساب مورد" || SelectedReportType == "كشف حساب تفصيلي للمورد")
            {
                double sumDebit = 0, sumCredit = 0;
                string colDebit  = "مدين (عليه)";
                string colCredit = "دائن (له)";

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    if (row[colDebit]  != DBNull.Value) sumDebit  += Convert.ToDouble(row[colDebit]);
                    if (row[colCredit] != DBNull.Value) sumCredit += Convert.ToDouble(row[colCredit]);
                }

                double finalBal = sumDebit - sumCredit;

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "إجمالي مدين",  sumDebit.ToString("N2")  },
                    { "إجمالي دائن",  sumCredit.ToString("N2") },
                    { "الرصيد", finalBal >= 0 ? finalBal.ToString("N2") + " (مدين)" : Math.Abs(finalBal).ToString("N2") + " (دائن)" }
                };
            }
            else if (SelectedReportType == "المشتريات بالموردين")
            {
                double sumBuysVal = 0, sumReturnsVal = 0, sumNetVal = 0;
                int sumBuysCount = 0, sumReturnsCount = 0;
                int suppliersCount = dt.Rows.Count;

                foreach (System.Data.DataRowView row in dt.DefaultView)
                {
                    sumBuysCount    += Convert.ToInt32(row["عدد فواتير شراء"]   == DBNull.Value ? 0 : row["عدد فواتير شراء"]);
                    sumReturnsCount += Convert.ToInt32(row["عدد فواتير مرتجع"]  == DBNull.Value ? 0 : row["عدد فواتير مرتجع"]);
                    sumBuysVal      += Convert.ToDouble(row["قيمة المشتريات"]   == DBNull.Value ? 0 : row["قيمة المشتريات"]);
                    sumReturnsVal   += Convert.ToDouble(row["قيمة المرتجعات"]   == DBNull.Value ? 0 : row["قيمة المرتجعات"]);
                    sumNetVal       += Convert.ToDouble(row["صافي المشتريات"]    == DBNull.Value ? 0 : row["صافي المشتريات"]);
                }

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد الموردين",         suppliersCount.ToString()       },
                    { "عدد فواتير شراء",      sumBuysCount.ToString()         },
                    { "عدد فواتير مرتجع",     sumReturnsCount.ToString()      },
                    { "إجمالي المشتريات",     sumBuysVal.ToString("N2")       },
                    { "إجمالي المرتجعات",     sumReturnsVal.ToString("N2")    },
                    { "صافي المشتريات",       sumNetVal.ToString("N2")        }
                };
            }
            else if (SelectedReportType == "مشتريات الموتوسيكلات")
            {
                double sumNetVal = 0;
                int countCars = dt.Rows.Count;

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    if (row["سعر الشراء"] != DBNull.Value)
                        sumNetVal += Convert.ToDouble(row["سعر الشراء"]);
                }

                MotorcyclesCount    = countCars;
                MotorcyclesTotalBuys = sumNetVal;

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد الموتوسيكلات", countCars.ToString()         },
                    { "إجمالي المشتريات", sumNetVal.ToString("N2")     }
                };
            }
            else if (SelectedReportType == "المشتريات بالفواتير")
            {
                double sumBuys = 0, sumReturns = 0;
                int cntBuys = 0, cntReturns = 0;
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    string txType = row["نوع الحركة"]?.ToString() ?? "";
                    double net    = Convert.ToDouble(row["صافي الفاتورة"] == DBNull.Value ? 0 : row["صافي الفاتورة"]);

                    if (txType == "شراء")
                    { sumBuys += net; cntBuys++; }
                    else
                    { sumReturns += Math.Abs(net); cntReturns++; }
                }
                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد فواتير الشراء",   cntBuys.ToString()                       },
                    { "إجمالي المشتريات",    sumBuys.ToString("N2")                   },
                    { "عدد المرتجعات",        cntReturns.ToString()                    },
                    { "إجمالي المرتجعات",    sumReturns.ToString("N2")                },
                    { "صافي المشتريات",      (sumBuys - sumReturns).ToString("N2")    }
                };
            }
            else
            {
                var buysSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT COUNT(Buy_ID) AS Cnt, ISNULL(SUM(Net), 0) AS Tot FROM Buy WHERE BuyDate >= @FromDate AND BuyDate <= @ToDate", parameters);
                int   countBuys   = buysSum?.Cnt ?? 0;
                double totalBuys  = buysSum?.Tot ?? 0;

                var rebuysSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT COUNT(Buy_ID) AS Cnt, ISNULL(SUM(Net), 0) AS Tot FROM ReBuy WHERE BuyDate >= @FromDate AND BuyDate <= @ToDate", parameters);
                int   countReturns   = rebuysSum?.Cnt ?? 0;
                double totalReturns  = rebuysSum?.Tot ?? 0;

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد المرتجعات",     countReturns.ToString()                      },
                    { "عدد الفواتير",      countBuys.ToString()                          },
                    { "إجمالي المشتريات", totalBuys.ToString("N2")                      },
                    { "إجمالي المرتجعات", totalReturns.ToString("N2")                   },
                    { "صافي المشتريات",   (totalBuys - totalReturns).ToString("N2")     }
                };
            }

            if (SelectedReportType == "المشتريات بالشهور")
            {
                _currentFooterTotals!.Add("عدد الشهور", dt.Rows.Count.ToString());
            }

            if (SelectedReportType == "أرصدة الموردين")
            {
                double totalDebit = 0, totalCredit = 0;
                foreach (System.Data.DataRowView rv in dt.DefaultView)
                {
                    totalDebit  += Convert.ToDouble(rv["مدين (عليه)"] == DBNull.Value ? 0 : rv["مدين (عليه)"]);
                    totalCredit += Convert.ToDouble(rv["دائن (له)"]   == DBNull.Value ? 0 : rv["دائن (له)"]);
                }
                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد الموردين",   dt.Rows.Count.ToString()              },
                    { "إجمالي مدين",   totalDebit.ToString("N2")              },
                    { "إجمالي دائن",   totalCredit.ToString("N2")             },
                    { "صافي الرصيد",   (totalDebit - totalCredit).ToString("N2") }
                };
            }

            if (SelectedReportType == "كشف حساب مورد" || SelectedReportType == "كشف حساب تفصيلي للمورد")
            {
                _currentHeaderInfo = new Dictionary<string, string>
                {
                    { "اسم المورد", SelectedSupplier?.SuppName ?? "" },
                    { "الفترة من", IsFromDateChecked ? queryFromDate.ToString("yyyy/MM/dd") : "بداية التعامل" },
                    { "الفترة إلى", IsToDateChecked ? queryToDate.ToString("yyyy/MM/dd") : "حتى الآن" }
                };
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
        bool hasData = IsDetailedReport
            ? (DetailedReportData != null && DetailedReportData.Count > 0)
            : (ReportData != null && ReportData.Count > 0);

        if (!hasData)
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
                var pdfBytes = IsDetailedReport
                    ? (IsInvoiceMode
                        ? MotorBike.Services.ReportGenerator.GenerateInvoiceDetailedPdf(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals)
                        : MotorBike.Services.ReportGenerator.GenerateDetailedPdf(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals))
                    : MotorBike.Services.ReportGenerator.GeneratePdf(company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);

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
        bool hasData = IsDetailedReport
            ? (DetailedReportData != null && DetailedReportData.Count > 0)
            : (ReportData != null && ReportData.Count > 0);

        if (!hasData)
        {
            System.Windows.MessageBox.Show("لا توجد بيانات ليتم طباعتها", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        using var db = _dbFactory.CreateConnection();
        var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

        try
        {
            var pdfBytes = IsDetailedReport
                ? (IsInvoiceMode
                    ? MotorBike.Services.ReportGenerator.GenerateInvoiceDetailedPdf(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals)
                    : MotorBike.Services.ReportGenerator.GenerateDetailedPdf(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals))
                : MotorBike.Services.ReportGenerator.GeneratePdf(company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);

            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MotorBikeReport_" + Guid.NewGuid() + ".pdf");
            System.IO.File.WriteAllBytes(tempFile, pdfBytes);
            MotorBike.Services.ReportGenerator.PrintPdf(tempFile);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    // ── كشف حساب تفصيلي للمورد ──────────────────────────────────────────────
    private async Task GenerateDetailedStatementAsync(
        System.Data.IDbConnection db,
        DynamicParameters parameters,
        DateTime queryFromDate,
        DateTime queryToDate)
    {
        int suppId = SelectedSupplier!.SuppId;

        var opParams = new DynamicParameters();
        opParams.Add("SuppId", suppId);
        opParams.Add("FromDate", queryFromDate);

        var supplierInfo = await db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT ISNULL(Debit,0) AS Debit, ISNULL(Credit,0) AS Credit, OpenDate FROM Suppliers WHERE Supp_ID=@SuppId", opParams);

        double suppIniDebit  = Convert.ToDouble(supplierInfo?.Debit  ?? 0);
        double suppIniCredit = Convert.ToDouble(supplierInfo?.Credit ?? 0);

        double prevBuys      = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Net),0) FROM Buy         WHERE SuppId=@SuppId AND BuyDate<@FromDate", opParams);
        double prevBuysCar   = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BC.Net),0) FROM Buy_Car BC INNER JOIN Cars C ON BC.CarID=C.Car_ID WHERE C.IsLocalSupplier=1 AND C.SupplierId=@SuppId AND BC.BuyDate<@FromDate", opParams);
        double prevReBuys    = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Net),0) FROM ReBuy       WHERE SuppId=@SuppId AND BuyDate<@FromDate", opParams);
        double prevPayCC     = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney),0) FROM Supp_Payments WHERE SuppId=@SuppId AND PayDate<@FromDate AND PayType IN(0,1,3)", opParams);
        double prevPayCD     = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney),0) FROM Supp_Payments WHERE SuppId=@SuppId AND PayDate<@FromDate AND PayType=2", opParams);
        double prevBuyPay    = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BP.PayMoney),0) FROM Buy_Payments BP JOIN Buy B ON BP.BuyID=B.Buy_ID WHERE B.SuppId=@SuppId AND BP.PayDate<@FromDate", opParams);
        double prevCarPay    = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BCP.PayMoney),0) FROM Buy_Car_Payments BCP INNER JOIN Buy_Car BC ON BCP.BuyID=BC.Buy_ID INNER JOIN Cars C ON BC.CarID=C.Car_ID WHERE C.IsLocalSupplier=1 AND C.SupplierId=@SuppId AND BCP.PayDate<@FromDate", opParams);
        double prevReBuyPay  = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(RP.PayMoney),0) FROM ReBuy_Payments RP JOIN ReBuy RB ON RP.BuyID=RB.Buy_ID WHERE RB.SuppId=@SuppId AND RP.PayDate<@FromDate", opParams);

        DateTime openDate    = (supplierInfo?.OpenDate != null) ? Convert.ToDateTime((object?)supplierInfo.OpenDate) : new DateTime(1900, 1, 1);
        bool openDateBefore  = openDate < queryFromDate;

        double prevDebit  = prevBuys + prevBuysCar + prevPayCD + prevReBuyPay + (openDateBefore ? suppIniDebit : 0);
        double prevCredit = prevReBuys + prevPayCC + prevBuyPay + prevCarPay + (openDateBefore ? suppIniCredit : 0);

        double runBal = 0;
        var rows = new List<DetailedAccountRow>();

        // سطر الرصيد السابق
        runBal += prevDebit - prevCredit;
        rows.Add(new DetailedAccountRow {
            RawDate = queryFromDate.AddSeconds(-1),
            Date = queryFromDate.ToString("dd/MM/yyyy"),
            TransType = "ما قبله",
            Notes = "الرصيد السابق",
            Debit = prevDebit, Credit = prevCredit,
            RunningDebit  = runBal > 0 ? runBal : 0,
            RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
        });

        // سطر الرصيد الافتتاحي (يظهر فقط لو في نفس الفترة)
        if (!openDateBefore && (suppIniDebit > 0 || suppIniCredit > 0))
        {
            runBal += suppIniDebit - suppIniCredit;
            rows.Add(new DetailedAccountRow {
                RawDate = openDate,
                Date = openDate.ToString("dd/MM/yyyy"),
                TransType = "افتتاحي",
                Notes = "الرصيد الافتتاحي",
                Debit = suppIniDebit, Credit = suppIniCredit,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
            });
        }

        var txParams = new DynamicParameters();
        txParams.Add("SuppId", suppId);
        txParams.Add("FromDate", queryFromDate);
        txParams.Add("ToDate",   queryToDate);

        // ── فواتير شراء الأصناف ──
        var buys = await db.QueryAsync<dynamic>(@"
            SELECT M.Buy_ID AS Id, M.BuyDate AS TxDate, CAST(M.Buy_ID AS VARCHAR) AS RefNo,
                   'فاتورة شراء' AS TransType, ISNULL(M.Notes,'') AS Notes,
                   M.Net AS Debit, 0 AS Credit,
                   ISNULL(ST.StoreName,'') AS Branch, ISNULL(U.UserName,'') AS Agent
            FROM Buy M
            LEFT JOIN Buy_Sub BS ON BS.BuyID=M.Buy_ID
            LEFT JOIN Stores ST ON BS.StoreId=ST.Store_ID
            LEFT JOIN Users U ON M.AddUser=U.User_ID
            WHERE M.SuppId=@SuppId AND M.BuyDate>=@FromDate AND M.BuyDate<=@ToDate
            GROUP BY M.Buy_ID, M.BuyDate, M.Net, M.Notes, ST.StoreName, U.UserName", txParams);

        foreach (var b in buys)
        {
            int bid = Convert.ToInt32(b.Id);
            var subItems = await db.QueryAsync<dynamic>(@"
                SELECT I.ItemName, UN.UnitName AS Unit, BS.Qty, BS.Price, BS.DiscPer, (BS.Qty*(BS.Price-BS.Disc)) AS Total
                FROM Buy_Sub BS
                JOIN Items I ON BS.ItemID=I.Item_ID
                LEFT JOIN Units UN ON BS.UnitId=UN.Unit_ID
                WHERE BS.BuyID=@BuyId", new { BuyId = bid });

            double d = Convert.ToDouble(b.Debit);
            runBal += d;
            DateTime txDate = Convert.ToDateTime((object)b.TxDate);
            rows.Add(new DetailedAccountRow {
                RawDate = txDate,
                Date = txDate.ToString("dd/MM/yyyy"),
                RefNo = b.RefNo, Branch = b.Branch, Agent = b.Agent,
                TransType = b.TransType, Notes = b.Notes,
                Debit = d, Credit = 0,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0,
                Items = subItems.Select(x => new InvoiceSubItem {
                    ItemName = x.ItemName, Unit = x.Unit ?? "",
                    Qty = Convert.ToDouble(x.Qty), Price = Convert.ToDouble(x.Price),
                    DiscPer = Convert.ToDouble(x.DiscPer), Total = Convert.ToDouble(x.Total)
                }).ToList()
            });

            // مدفوعات مع الفاتورة
            var buyPays = await db.QueryAsync<dynamic>(
                "SELECT CAST(BuyID AS VARCHAR) AS RefNo, PayDate, PayMoney, ISNULL(Notes,'') AS Notes FROM Buy_Payments WHERE BuyID=@BuyId", new { BuyId = bid });
            foreach (var p in buyPays)
            {
                double c = Convert.ToDouble(p.PayMoney);
                runBal -= c;
                DateTime pDate = Convert.ToDateTime((object)p.PayDate);
                rows.Add(new DetailedAccountRow {
                    RawDate = pDate.AddSeconds(1),
                    Date = pDate.ToString("dd/MM/yyyy"),
                    RefNo = p.RefNo, TransType = "سداد مع الفاتورة", Notes = p.Notes,
                    Debit = 0, Credit = c,
                    RunningDebit  = runBal > 0 ? runBal : 0,
                    RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
                });
            }
        }

        // ── شراء موتوسيكلات من المورد ──
        var buysCar = await db.QueryAsync<dynamic>(@"
            SELECT M.Buy_ID AS Id, M.BuyDate AS TxDate, CAST(M.Buy_ID AS VARCHAR) AS RefNo,
                   'شراء موتوسيكل' AS TransType, ISNULL(M.Notes,'') AS Notes, M.Net AS Debit, 0 AS Credit,
                   ISNULL(CB.BrandName,'') AS Brand, ISNULL(CM.ModelName,'') AS ModelName,
                   ISNULL(C.ChassisNo,'') AS Chassis, ISNULL(C.MotorNo,'') AS MotorNo, ISNULL(C.PlateNo,'') AS PlateNo, ISNULL(C.Mileage,0) AS Mileage
            FROM Buy_Car M
            INNER JOIN Cars C ON M.CarID=C.Car_ID
            LEFT JOIN CarModels CM ON C.ModelID=CM.Model_ID
            LEFT JOIN CarBrands CB ON CM.BrandID=CB.Brand_ID
            WHERE C.IsLocalSupplier=1 AND C.SupplierId=@SuppId AND M.BuyDate>=@FromDate AND M.BuyDate<=@ToDate", txParams);

        foreach (var bc in buysCar)
        {
            int bcid = Convert.ToInt32(bc.Id);
            double d = Convert.ToDouble(bc.Debit);
            runBal += d;
            DateTime txDate = Convert.ToDateTime((object)bc.TxDate);
            rows.Add(new DetailedAccountRow {
                RawDate = txDate,
                Date = txDate.ToString("dd/MM/yyyy"),
                RefNo = bc.RefNo, TransType = bc.TransType, Notes = bc.Notes,
                Debit = d, Credit = 0,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0,
                IsCarTransaction = true,
                Items = new List<InvoiceSubItem> {
                    new() { ItemName = $"{bc.Brand} - {bc.ModelName}",
                            ChassisNo = bc.Chassis, MotorNo = bc.MotorNo, PlateNo = bc.PlateNo, Mileage = bc.Mileage,
                            Price = d, Total = d }
                }
            });

            var carPays = await db.QueryAsync<dynamic>(
                "SELECT CAST(BuyID AS VARCHAR) AS RefNo, PayDate, PayMoney, ISNULL(Notes,'') AS Notes FROM Buy_Car_Payments WHERE BuyID=@BuyId", new { BuyId = bcid });
            foreach (var p in carPays)
            {
                double c = Convert.ToDouble(p.PayMoney);
                runBal -= c;
                DateTime pDate = Convert.ToDateTime((object)p.PayDate);
                rows.Add(new DetailedAccountRow {
                    RawDate = pDate.AddSeconds(1),
                    Date = pDate.ToString("dd/MM/yyyy"),
                    RefNo = p.RefNo, TransType = "سداد مع الفاتورة", Notes = p.Notes,
                    Debit = 0, Credit = c,
                    RunningDebit  = runBal > 0 ? runBal : 0,
                    RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
                });
            }
        }

        // ── مرتجعات المشتريات ──
        var rebuys = await db.QueryAsync<dynamic>(@"
            SELECT M.Buy_ID AS Id, M.BuyDate AS TxDate, CAST(M.Buy_ID AS VARCHAR) AS RefNo,
                   'مرتجع شراء' AS TransType, ISNULL(M.Notes,'') AS Notes, 0 AS Debit, M.Net AS Credit
            FROM ReBuy M WHERE M.SuppId=@SuppId AND M.BuyDate>=@FromDate AND M.BuyDate<=@ToDate", txParams);

        foreach (var r in rebuys)
        {
            int rid = Convert.ToInt32(r.Id);
            var subItems = await db.QueryAsync<dynamic>(@"
                SELECT I.ItemName, UN.UnitName AS Unit, RS.Qty, RS.Price, RS.DiscPer, (RS.Qty*(RS.Price-RS.Disc)) AS Total
                FROM ReBuy_Sub RS
                JOIN Items I ON RS.ItemID=I.Item_ID
                LEFT JOIN Units UN ON RS.UnitId=UN.Unit_ID
                WHERE RS.BuyID=@BuyId", new { BuyId = rid });

            double c = Convert.ToDouble(r.Credit);
            runBal -= c;
            DateTime txDate = Convert.ToDateTime((object)r.TxDate);
            rows.Add(new DetailedAccountRow {
                RawDate = txDate,
                Date = txDate.ToString("dd/MM/yyyy"),
                RefNo = r.RefNo, TransType = r.TransType, Notes = r.Notes,
                Debit = 0, Credit = c,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0,
                Items = subItems.Select(x => new InvoiceSubItem {
                    ItemName = x.ItemName, Unit = x.Unit ?? "",
                    Qty = Convert.ToDouble(x.Qty), Price = Convert.ToDouble(x.Price),
                    DiscPer = Convert.ToDouble(x.DiscPer), Total = Convert.ToDouble(x.Total)
                }).ToList()
            });

            // سداد مع مرتجع الشراء
            var rebuyPays = await db.QueryAsync<dynamic>(
                "SELECT CAST(RP.BuyID AS VARCHAR) AS RefNo, RP.PayDate, RP.PayMoney, ISNULL(RP.Notes,'') AS Notes FROM ReBuy_Payments RP WHERE RP.BuyID=@BuyId", new { BuyId = rid });
            foreach (var p in rebuyPays)
            {
                double pd = Convert.ToDouble(p.PayMoney);
                runBal += pd;
                DateTime pDate = Convert.ToDateTime((object)p.PayDate);
                rows.Add(new DetailedAccountRow {
                    RawDate = pDate.AddSeconds(1),
                    Date = pDate.ToString("dd/MM/yyyy"),
                    RefNo = p.RefNo, TransType = "سداد مع المرتجع", Notes = p.Notes,
                    Debit = pd, Credit = 0,
                    RunningDebit  = runBal > 0 ? runBal : 0,
                    RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
                });
            }
        }

        // ── مدفوعات منفصلة للمورد ──
        var supppays = await db.QueryAsync<dynamic>(@"
            SELECT Pay_ID AS Id, PayDate, CAST(Pay_ID AS VARCHAR) AS RefNo, PayType, PayMoney, ISNULL(Notes,'') AS Notes
            FROM Supp_Payments WHERE SuppId=@SuppId AND PayDate>=@FromDate AND PayDate<=@ToDate", txParams);

        foreach (var p in supppays)
        {
            string typeName = Convert.ToInt32(p.PayType) switch {
                0 => "سداد", 1 => "سداد", 2 => "استرداد", 3 => "خصم مكتسب", _ => "سداد"
            };
            int pt = Convert.ToInt32(p.PayType);
            double amount = Convert.ToDouble(p.PayMoney);
            double d2 = pt == 2 ? amount : 0;
            double c2 = pt != 2 ? amount : 0;
            runBal += d2 - c2;
            DateTime pDate = Convert.ToDateTime((object)p.PayDate);
            rows.Add(new DetailedAccountRow {
                RawDate = pDate,
                Date = pDate.ToString("dd/MM/yyyy"),
                RefNo = p.RefNo, TransType = typeName, Notes = p.Notes,
                Debit = d2, Credit = c2,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
            });
        }

        // ── ترتيب حسب التاريخ ثم رقم الحركة ──
        var sorted = rows.OrderBy(r => r.RawDate).ThenBy(r => r.RefNo).ToList();
        double rb = 0;
        foreach (var row in sorted)
        {
            rb += row.Debit - row.Credit;
            row.RunningDebit  = rb > 0 ? rb : 0;
            row.RunningCredit = rb < 0 ? Math.Abs(rb) : 0;
        }

        DetailedReportData = new ObservableCollection<DetailedAccountRow>(sorted);
        IsDetailedReport   = true;

        double sumD = sorted.Sum(r => r.Debit);
        double sumC = sorted.Sum(r => r.Credit);
        double bal  = sumD - sumC;

        _currentHeaderInfo = new Dictionary<string, string> {
            { "اسم المورد", SelectedSupplier.SuppName },
            { "الفترة من", IsFromDateChecked ? queryFromDate.ToString("yyyy/MM/dd") : "بداية التعامل" },
            { "الفترة إلى", IsToDateChecked  ? queryToDate.ToString("yyyy/MM/dd")  : "حتى الآن" }
        };

        _currentFooterTotals = new Dictionary<string, string> {
            { "إجمالي مدين", sumD.ToString("N2") },
            { "إجمالي دائن", sumC.ToString("N2") },
            { "الرصيد", bal >= 0 ? bal.ToString("N2") + " (مدين)" : Math.Abs(bal).ToString("N2") + " (دائن)" }
        };

        StatusMessage = sorted.Count > 0
            ? $"تم العثور على {sorted.Count} حركة"
            : "⚠️ لا توجد بيانات في الفترة المحددة";
    }

    // ── تقرير المشتريات بالفواتير مفصل ──────────────────────────────────────
    private async Task GenerateInvoicesBuysDetailedAsync(
        System.Data.IDbConnection db,
        DateTime queryFromDate,
        DateTime queryToDate,
        string statusFilter)
    {
        var p = new DynamicParameters();
        p.Add("FromDate", queryFromDate);
        p.Add("ToDate",   queryToDate);

        string suppWhere = "";
        if (SelectedSupplier != null)
        {
            suppWhere = " AND M.SuppId = @SuppId ";
            p.Add("SuppId", SelectedSupplier.SuppId);
        }

        string buysExtraFilter = "";
        string reBuysExtraFilter = "";
        if (SelectedStore != null)
        {
            buysExtraFilter   += " AND EXISTS (SELECT 1 FROM Buy_Sub BS WHERE BS.BuyID = M.Buy_ID AND BS.StoreId = @StoreId) ";
            reBuysExtraFilter += " AND EXISTS (SELECT 1 FROM ReBuy_Sub RS WHERE RS.BuyID = M.Buy_ID AND RS.StoreId = @StoreId) ";
            p.Add("StoreId", SelectedStore.StoreId);
        }
        if (SelectedSafe != null)
        {
            buysExtraFilter   += " AND EXISTS (SELECT 1 FROM Buy_Payments BP WHERE BP.BuyID = M.Buy_ID AND BP.CashId = @CashId) ";
            reBuysExtraFilter += " AND EXISTS (SELECT 1 FROM ReBuy_Payments RP WHERE RP.BuyID = M.Buy_ID AND RP.CashId = @CashId) ";
            p.Add("CashId", SelectedSafe.CashId);
        }

        // ── فواتير المشتريات ──
        IEnumerable<dynamic> buyInvoices = new List<dynamic>();
        if (statusFilter != "فواتير مرتجعات")
        {
            buyInvoices = await db.QueryAsync<dynamic>($@"
                SELECT M.Buy_ID AS Id, M.BuyDate AS TxDate,
                       CAST(M.Buy_ID AS VARCHAR) AS RefNo,
                       ISNULL(SP.SuppName, N'مورد نقدي') AS SuppName,
                       ISNULL(M.Notes, '') AS Notes,
                       M.Total    AS InvTotal,
                       M.Disc     AS InvDisc,
                       M.AddMoney AS InvAdd,
                       M.Net      AS InvNet
                FROM Buy M
                LEFT JOIN Suppliers SP ON M.SuppId = SP.Supp_ID
                WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate
                {suppWhere} {buysExtraFilter}
                ORDER BY M.BuyDate DESC, M.Buy_ID DESC", p);
        }

        // ── فواتير مرتجعات المشتريات ──
        IEnumerable<dynamic> returnInvoices = new List<dynamic>();
        if (statusFilter != "فواتير مشتريات")
        {
            returnInvoices = await db.QueryAsync<dynamic>($@"
                SELECT M.Buy_ID AS Id, M.BuyDate AS TxDate,
                       CAST(M.Buy_ID AS VARCHAR) AS RefNo,
                       ISNULL(SP.SuppName, N'مورد نقدي') AS SuppName,
                       ISNULL(M.Notes, '') AS Notes,
                       -(M.Total)    AS InvTotal,
                       -(M.Disc)     AS InvDisc,
                       -(M.AddMoney) AS InvAdd,
                       -(M.Net)      AS InvNet
                FROM ReBuy M
                LEFT JOIN Suppliers SP ON M.SuppId = SP.Supp_ID
                WHERE M.BuyDate >= @FromDate AND M.BuyDate <= @ToDate
                {suppWhere} {reBuysExtraFilter}
                ORDER BY M.BuyDate DESC, M.Buy_ID DESC", p);
        }

        var rows = new List<DetailedAccountRow>();
        double totalBuys = 0, totalReturns = 0;
        int cntBuys = 0, cntReturns = 0;

        // ── معالجة فواتير المشتريات ──
        foreach (var inv in buyInvoices)
        {
            int id     = Convert.ToInt32(inv.Id);
            double net = Convert.ToDouble(inv.InvNet);
            totalBuys += net;
            cntBuys++;

            var subItems = await db.QueryAsync<dynamic>(@"
                SELECT I.ItemName, ISNULL(UN.UnitName,'') AS Unit,
                       BS.Qty, BS.Price, BS.DiscPer,
                       (BS.Qty*(BS.Price-BS.Disc)) AS Total
                FROM Buy_Sub BS
                JOIN Items I ON BS.ItemID=I.Item_ID
                LEFT JOIN Units UN ON BS.UnitId=UN.Unit_ID
                WHERE BS.BuyID=@Id", new { Id = id });

            DateTime txDate  = Convert.ToDateTime((object)inv.TxDate);
            double invTotal  = Convert.ToDouble(inv.InvTotal);
            double invDisc   = Convert.ToDouble(inv.InvDisc);
            double invAdd    = Convert.ToDouble(inv.InvAdd);

            rows.Add(new DetailedAccountRow
            {
                RawDate      = txDate,
                Date         = txDate.ToString("dd/MM/yyyy"),
                RefNo        = inv.RefNo,
                TransType    = "شراء",
                Notes        = inv.Notes ?? "",
                CustomerName = inv.SuppName,
                InvoiceTotal = invTotal,
                VatTax       = 0,
                Tax          = 0,
                InvoiceDisc  = invDisc,
                InvoiceAdd   = invAdd,
                InvoiceNet   = net,
                Items = subItems.Select(x => new InvoiceSubItem
                {
                    ItemName = x.ItemName, Unit = x.Unit ?? "",
                    Qty = Convert.ToDouble(x.Qty), Price = Convert.ToDouble(x.Price),
                    DiscPer = Convert.ToDouble(x.DiscPer), Total = Convert.ToDouble(x.Total)
                }).ToList()
            });
        }

        // ── معالجة مرتجعات المشتريات ──
        foreach (var inv in returnInvoices)
        {
            int id     = Convert.ToInt32(inv.Id);
            double net = Convert.ToDouble(inv.InvNet);
            totalReturns += Math.Abs(net);
            cntReturns++;

            var subItems = await db.QueryAsync<dynamic>(@"
                SELECT I.ItemName, ISNULL(UN.UnitName,'') AS Unit,
                       RS.Qty, RS.Price, RS.DiscPer,
                       (RS.Qty*(RS.Price-RS.Disc)) AS Total
                FROM ReBuy_Sub RS
                JOIN Items I ON RS.ItemID=I.Item_ID
                LEFT JOIN Units UN ON RS.UnitId=UN.Unit_ID
                WHERE RS.BuyID=@Id", new { Id = id });

            DateTime txDate = Convert.ToDateTime((object)inv.TxDate);
            rows.Add(new DetailedAccountRow
            {
                RawDate      = txDate,
                Date         = txDate.ToString("dd/MM/yyyy"),
                RefNo        = inv.RefNo,
                TransType    = "مرتجع شراء",
                Notes        = inv.Notes ?? "",
                CustomerName = inv.SuppName,
                InvoiceTotal = Convert.ToDouble(inv.InvTotal),
                VatTax       = 0,
                Tax          = 0,
                InvoiceDisc  = Convert.ToDouble(inv.InvDisc),
                InvoiceAdd   = Convert.ToDouble(inv.InvAdd),
                InvoiceNet   = net,
                Items = subItems.Select(x => new InvoiceSubItem
                {
                    ItemName = x.ItemName, Unit = x.Unit ?? "",
                    Qty = Convert.ToDouble(x.Qty), Price = Convert.ToDouble(x.Price),
                    DiscPer = Convert.ToDouble(x.DiscPer), Total = Convert.ToDouble(x.Total)
                }).ToList()
            });
        }

        // ترتيب تنازلي حسب التاريخ
        var sorted = rows.OrderByDescending(r => r.RawDate).ThenByDescending(r => r.RefNo).ToList();
        DetailedReportData = new ObservableCollection<DetailedAccountRow>(sorted);
        IsDetailedReport   = true;
        IsInvoiceMode      = true;

        _currentFooterTotals = new Dictionary<string, string>
        {
            { "عدد فواتير الشراء",   cntBuys.ToString()                    },
            { "إجمالي المشتريات",    totalBuys.ToString("N2")              },
            { "عدد المرتجعات",        cntReturns.ToString()                 },
            { "إجمالي المرتجعات",    totalReturns.ToString("N2")           },
            { "صافي المشتريات",      (totalBuys - totalReturns).ToString("N2") }
        };

        StatusMessage = sorted.Count > 0
            ? $"تم العثور على {cntBuys} فاتورة شراء و{cntReturns} مرتجع"
            : "⚠️ لا توجد بيانات في الفترة المحددة";
    }
}
