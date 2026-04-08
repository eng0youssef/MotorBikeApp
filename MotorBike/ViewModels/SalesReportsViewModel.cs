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

    [ObservableProperty] private ObservableCollection<string> _reportTypes =
    [
        "المبيعات بالشهور",
        "المبيعات بالأصناف",
        "المبيعات بالعملاء",
        "كشف حساب عميل",
        "كشف حساب تفصيلي للعميل",
        // ── التقارير الإضافية ──
        "المبيعات اليومية",
        "مبيعات الموتوسيكلات",
        "كشف حساب عميل (موتوسيكلات)",
        "تقرير المرتجعات المفصل",
        "أعلى العملاء مبيعاً"
    ];
    [ObservableProperty] private string _selectedReportType = "المبيعات بالشهور";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate = DateTime.Now;

    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked = true;

    [ObservableProperty] private ObservableCollection<Customer> _customers = [];
    [ObservableProperty] private Customer? _selectedCustomer;
    
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

    // ── فلتر نوع الفاتورة ──
    public ObservableCollection<string> InvoiceFilterTypes { get; } = ["الكل", "ضريبي", "عادي"];
    [ObservableProperty] private string _selectedInvoiceFilter = "الكل";

    // فلتر نوع الفاتورة يظهر فقط للتقارير التي تدعمه
    public bool IsTaxFilterVisible => SelectedReportType is "المبيعات بالشهور" or "المبيعات بالعملاء"
                                                          or "المبيعات اليومية" or "أعلى العملاء مبيعاً";
    // هل الفلتر الحالي ضريبي (لإظهار أعمدة الضريبة)
    public bool IsShowingTax => SelectedInvoiceFilter == "ضريبي";

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private ObservableCollection<DetailedAccountRow> _detailedReportData = [];
    [ObservableProperty] private bool _isDetailedReport;

    public bool IsCustomerVisible => SelectedReportType is "المبيعات بالعملاء"
                                                        or "كشف حساب عميل"
                                                        or "كشف حساب تفصيلي للعميل"
                                                        or "كشف حساب عميل (موتوسيكلات)";
    public bool IsItemVisible     => SelectedReportType == "المبيعات بالأصناف";
    public bool IsCarModelVisible => SelectedReportType is "مبيعات الموتوسيكلات"
                                                        or "كشف حساب عميل (موتوسيكلات)";
    public bool IsMotorcycleReport => SelectedReportType == "مبيعات الموتوسيكلات";

    [ObservableProperty] private int _motorcyclesCount;
    [ObservableProperty] private double _motorcyclesTotalSales;

    partial void OnSelectedInvoiceFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsShowingTax));
    }

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomerVisible));
        OnPropertyChanged(nameof(IsItemVisible));
        OnPropertyChanged(nameof(IsCarModelVisible));
        OnPropertyChanged(nameof(IsMotorcycleReport));
        OnPropertyChanged(nameof(IsTaxFilterVisible));
        ReportData              = new System.Data.DataView();
        DetailedReportData      = [];
        IsDetailedReport        = false;
        StatusMessage           = null;
        _currentFooterTotals    = null;
        _currentHeaderInfo      = null;
    }

    public SalesReportsViewModel(IDbConnectionFactory dbFactory,
        IRepository<Customer> customerRepo,
        IRepository<Item>     itemRepo,
        IRepository<CarModel> carModelRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(customerRepo, itemRepo, carModelRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(
        IRepository<Customer> customerRepo,
        IRepository<Item>     itemRepo,
        IRepository<CarModel> carModelRepo)
    {
        Customers = new ObservableCollection<Customer>(await customerRepo.GetAllAsync());
        Items     = new ObservableCollection<Item>    (await itemRepo.GetAllAsync());
        CarModels = new ObservableCollection<CarModel>(await carModelRepo.GetAllAsync());

        using var db = _dbFactory.CreateConnection();
        CarBrands = new ObservableCollection<CarBrand>(await db.QueryAsync<CarBrand>("SELECT * FROM CarBrands WHERE Active = 1"));
        CarColors = new ObservableCollection<Color>(await db.QueryAsync<Color>("SELECT Color_ID AS ColorId, ColorName, Notes, Active FROM Colors WHERE Active = 1"));
        CarYears  = new ObservableCollection<short>(await db.QueryAsync<short>("SELECT DISTINCT YearNo FROM Cars ORDER BY YearNo DESC"));
    }

    [RelayCommand] private void ClearCustomer()   => SelectedCustomer  = null;
    [RelayCommand] private void ClearItem()        => SelectedItem      = null;
    [RelayCommand] private void ClearCarBrand()    => SelectedCarBrand  = null;
    [RelayCommand] private void ClearCarModel()    => SelectedCarModel  = null;
    [RelayCommand] private void ClearCarColor()    => SelectedCarColor  = null;
    [RelayCommand] private void ClearCarYear()     => SelectedCarYear   = null;

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
            
            // بناء فلتر نوع الفاتورة
            string taxFilter = SelectedInvoiceFilter == "ضريبي" ? " AND IsTax = 1 "
                             : SelectedInvoiceFilter == "عادي"  ? " AND IsTax = 0 "
                             : "";
            bool showTax = SelectedInvoiceFilter == "ضريبي";

            if (SelectedReportType == "المبيعات بالشهور")
            {
                string taxCols = showTax ? ", SUM(VatTax) AS SumVatTax, SUM(Tax) AS SumTax" : "";
                string taxSelectCols = showTax
                    ? ", ISNULL(s.SumVatTax, 0) AS [ض.ق.م], ISNULL(s.SumTax, 0) AS [ض.أ.ت.ص]"
                    : "";
                sql = $@"
                    ;WITH SalesMonths AS (
                        SELECT FORMAT(SalesDate, 'yyyy-MM') AS MonthStr,
                               COUNT(DISTINCT Sales_ID) AS CountBills,
                               SUM(Net) AS SumTotal {taxCols}
                        FROM Sales WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate {taxFilter} GROUP BY FORMAT(SalesDate, 'yyyy-MM')
                    ),
                    ReturnMonths AS (
                        SELECT FORMAT(SalesDate, 'yyyy-MM') AS MonthStr,
                               COUNT(DISTINCT Sales_ID) AS CountRet,
                               SUM(Net) AS SumRetTotal
                        FROM ReSales WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate GROUP BY FORMAT(SalesDate, 'yyyy-MM')
                    ),
                    CusMonths AS (
                        SELECT MonthStr, COUNT(DISTINCT CusId) AS CusCount FROM (
                            SELECT FORMAT(SalesDate, 'yyyy-MM') AS MonthStr, CusId FROM Sales WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate {taxFilter}
                            UNION
                            SELECT FORMAT(SalesDate, 'yyyy-MM') AS MonthStr, CusId FROM ReSales WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate
                        ) AS AllCus GROUP BY MonthStr
                    ),
                    AllMonths AS (
                        SELECT MonthStr FROM SalesMonths UNION SELECT MonthStr FROM ReturnMonths
                    )
                    SELECT m.MonthStr AS [الشهر],
                           ISNULL(s.SumTotal, 0) AS [قيمة المبيعات],
                           ISNULL(r.SumRetTotal, 0) AS [قيمة المرتجعات],
                           (ISNULL(s.SumTotal, 0) - ISNULL(r.SumRetTotal, 0)) AS [صافي المبيعات],
                           ISNULL(s.CountBills, 0) AS [عدد الفواتير],
                           ISNULL(r.CountRet, 0) AS [عدد المرتجعات],
                           ISNULL(c.CusCount, 0) AS [عدد العملاء]
                           {taxSelectCols}
                    FROM AllMonths m
                    LEFT JOIN SalesMonths s ON m.MonthStr = s.MonthStr
                    LEFT JOIN ReturnMonths r ON m.MonthStr = r.MonthStr
                    LEFT JOIN CusMonths c ON m.MonthStr = c.MonthStr
                    ORDER BY m.MonthStr DESC";
            }
            else if (SelectedReportType == "المبيعات بالأصناف")
            {
                string itemFilter = "";
                if (SelectedItem != null)
                {
                    itemFilter = " AND A.ItemId = @ItemId ";
                    parameters.Add("ItemId", SelectedItem.ItemId);
                }
                sql = @"
                    ;WITH SalesData AS (
                        SELECT S.ItemId, 
                               SUM(S.Qty) AS SQty, 
                               SUM(S.Qty * (S.Price - S.Disc)) AS SValue,
                               COUNT(DISTINCT M.Sales_ID) AS SInvCount
                        FROM Sales_Sub S JOIN Sales M ON S.SalesId = M.Sales_ID
                        WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                        GROUP BY S.ItemId
                    ),
                    ReturnsData AS (
                        SELECT S.ItemId, 
                               SUM(S.Qty) AS RQty, 
                               SUM(S.Qty * (S.Price - S.Disc)) AS RValue,
                               COUNT(DISTINCT M.Sales_ID) AS RInvCount
                        FROM ReSales_Sub S JOIN ReSales M ON S.SalesId = M.Sales_ID
                        WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                        GROUP BY S.ItemId
                    ),
                    AllItems AS (
                        SELECT ItemId FROM SalesData UNION SELECT ItemId FROM ReturnsData
                    ),
                    ItemStats AS (
                        SELECT A.ItemId, I.ItemName, C.CatName,
                               ISNULL(SD.SQty, 0) AS SQty, ISNULL(SD.SValue, 0) AS SValue, ISNULL(SD.SInvCount, 0) AS SInvCount,
                               ISNULL(RD.RQty, 0) AS RQty, ISNULL(RD.RValue, 0) AS RValue, ISNULL(RD.RInvCount, 0) AS RInvCount,
                               (ISNULL(SD.SQty, 0) - ISNULL(RD.RQty, 0)) AS NetQty,
                               (ISNULL(SD.SValue, 0) - ISNULL(RD.RValue, 0)) AS NetValue,
                               (SELECT COUNT(DISTINCT CusId) FROM (
                                   SELECT M.CusId FROM Sales_Sub Sub JOIN Sales M ON Sub.SalesId = M.Sales_ID WHERE Sub.ItemId = A.ItemId AND M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                                   UNION
                                   SELECT M.CusId FROM ReSales_Sub Sub JOIN ReSales M ON Sub.SalesId = M.Sales_ID WHERE Sub.ItemId = A.ItemId AND M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                               ) T) AS CusCount
                        FROM AllItems A
                        JOIN Items I ON A.ItemId = I.Item_ID
                        JOIN Item_Category C ON I.CatID = C.Cat_ID
                        LEFT JOIN SalesData SD ON A.ItemId = SD.ItemId
                        LEFT JOIN ReturnsData RD ON A.ItemId = RD.ItemId
                        WHERE 1=1 " + itemFilter + @"
                    )
                    SELECT 
                        CatName AS [المجموعة],
                        ISNULL(ItemName, 'إجمالي المجموعة ->') AS [الصنف],
                        SUM(SQty) AS [كمية البيع],
                        SUM(RQty) AS [كمية المرتجع],
                        SUM(NetQty) AS [صافي الكمية],
                        SUM(SInvCount) AS [عدد فواتير البيع],
                        SUM(SValue) AS [قيمة البيع],
                        SUM(RInvCount) AS [عدد فواتير المرتجع],
                        SUM(RValue) AS [قيمة المرتجع],
                        SUM(NetValue) AS [صافي المبيعات],
                        CASE WHEN SUM(SQty) > 0 THEN SUM(SValue) / SUM(SQty) ELSE 0 END AS [متوسط سعر البيع],
                        SUM(CusCount) AS [عدد العملاء]
                    FROM ItemStats
                    GROUP BY GROUPING SETS (
                        (CatName, ItemName),
                        (CatName)
                    )
                    ORDER BY CatName, CASE WHEN ItemName IS NULL THEN 1 ELSE 0 END, ItemName";
            }
            else if (SelectedReportType == "المبيعات بالعملاء")
            {
                string cusFilter = taxFilter;
                if (SelectedCustomer != null)
                {
                    cusFilter += " AND CusId = @CusId ";
                    parameters.Add("CusId", SelectedCustomer.CusId);
                }
                string taxCols2 = showTax ? ", SUM(VatTax) AS SumVatTaxS, SUM(Tax) AS SumTaxS" : "";
                string taxSelectCols2 = showTax
                    ? ", ISNULL(S.SumVatTaxS, 0) AS [ض.ق.م], ISNULL(S.SumTaxS, 0) AS [ض.أ.ت.ص]"
                    : "";

                sql = $@"
                    ;WITH SalesData AS (
                        SELECT CusId, 
                               COUNT(Sales_ID) AS SalesCount, 
                               SUM(Net) AS SalesTotal {taxCols2}
                        FROM Sales
                        WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate {cusFilter}
                        GROUP BY CusId
                    ),
                    ReturnsData AS (
                        SELECT CusId, 
                               COUNT(Sales_ID) AS ReturnsCount, 
                               SUM(Net) AS ReturnsTotal
                        FROM ReSales
                        WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate
                        GROUP BY CusId
                    ),
                    AllCustomers AS (
                        SELECT CusId FROM SalesData
                        UNION
                        SELECT CusId FROM ReturnsData
                    )
                    SELECT 
                        ISNULL(C.Cus_ID, 0) AS [الكود],
                        ISNULL(C.CusName, 'عميل نقدي/غير محدد') AS [الاسم],
                        ISNULL(S.SalesCount, 0) AS [عدد فواتير بيع],
                        ISNULL(S.SalesTotal, 0) AS [قيمة المبيعات],
                        ISNULL(R.ReturnsCount, 0) AS [عدد فواتير مرتجع],
                        ISNULL(R.ReturnsTotal, 0) AS [قيمة المرتجعات],
                        (ISNULL(S.SalesTotal, 0) - ISNULL(R.ReturnsTotal, 0)) AS [صافي المبيعات]
                        {taxSelectCols2}
                    FROM AllCustomers A
                    LEFT JOIN Customers C ON A.CusId = C.Cus_ID
                    LEFT JOIN SalesData S ON A.CusId = S.CusId
                    LEFT JOIN ReturnsData R ON A.CusId = R.CusId
                    ORDER BY [صافي المبيعات] DESC";
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
                    SELECT SortDate, 
                           CONVERT(VARCHAR, SortDate, 103) AS [التاريخ], 
                           RefNo AS [رقم الحركة],
                           TransType AS [نوع الحركة],
                           Details AS [البيان],
                           Debit AS [مدين (عليه)],
                           Credit AS [دائن (له)]
                    FROM (
                        -- فواتير مبيعات الأصناف
                        SELECT SalesDate AS SortDate, CAST(Sales_ID AS VARCHAR) AS RefNo, 'فاتورة مبيعات' AS TransType, ISNULL(Notes, '') AS Details, (Total - Disc + AddMony) AS Debit, 0 AS Credit 
                        FROM Sales WHERE CusId = @CusId AND SalesDate >= @FromDate AND SalesDate <= @ToDate
                        
                        UNION ALL
                        
                        -- مدفوعات فواتير الأصناف المعموله مع الفاتورة
                        SELECT SP.PayDate, CAST(SP.SalesId AS VARCHAR), 'تحصيل مع الفاتورة', ISNULL(SP.Notes, ''), 0, SP.PayMoney
                        FROM Sales_Payments SP
                        INNER JOIN Sales S ON SP.SalesId = S.Sales_ID
                        WHERE S.CusId = @CusId AND SP.PayDate >= @FromDate AND SP.PayDate <= @ToDate

                        UNION ALL
                        
                        -- فواتير بيع موتوسيكلات
                        SELECT SalesDate, CAST(Sales_ID AS VARCHAR), 'بيع موتوسيكل', ISNULL(Notes, ''), Total, 0 
                        FROM Sales_Car WHERE CusId = @CusId AND SalesDate >= @FromDate AND SalesDate <= @ToDate

                        UNION ALL
                        
                        -- مدفوعات فواتير الموتوسيكلات المعموله مع الفاتورة
                        SELECT SCP.PayDate, CAST(SCP.SalesId AS VARCHAR), 'تحصيل مع الفاتورة', ISNULL(SCP.Notes, ''), 0, SCP.PayMoney
                        FROM Sales_Car_Payments SCP
                        INNER JOIN Sales_Car SC ON SCP.SalesId = SC.Sales_ID
                        WHERE SC.CusId = @CusId AND SCP.PayDate >= @FromDate AND SCP.PayDate <= @ToDate

                        UNION ALL
                        
                        -- مرتجعات المبيعات
                        SELECT SalesDate, CAST(Sales_ID AS VARCHAR), 'مرتجع بيع', ISNULL(Notes, ''), 0, (Total - Disc + AddMony) 
                        FROM ReSales WHERE CusId = @CusId AND SalesDate >= @FromDate AND SalesDate <= @ToDate

                        UNION ALL

                        -- فواتير شراء موتوسيكلات من العميل
                        SELECT BC.BuyDate, CAST(BC.Buy_ID AS VARCHAR), 'شراء موتوسيكل', ISNULL(BC.Notes, ''), 0, BC.Net 
                        FROM Buy_Car BC
                        INNER JOIN Cars C ON BC.CarID = C.Car_ID
                        WHERE C.IsFromCustomer = 1 AND C.SourceCustomerID = @CusId AND BC.BuyDate >= @FromDate AND BC.BuyDate <= @ToDate

                        UNION ALL

                        -- مدفوعات فواتير شراء الموتوسيكلات من العميل
                        SELECT BCP.PayDate, CAST(BCP.BuyID AS VARCHAR), 'سداد مع فاتورة الشراء', ISNULL(BCP.Notes, ''), BCP.PayMoney, 0
                        FROM Buy_Car_Payments BCP
                        INNER JOIN Buy_Car BC ON BCP.BuyID = BC.Buy_ID
                        INNER JOIN Cars C ON BC.CarID = C.Car_ID
                        WHERE C.IsFromCustomer = 1 AND C.SourceCustomerID = @CusId AND BCP.PayDate >= @FromDate AND BCP.PayDate <= @ToDate

                        UNION ALL
                        
                        -- تحصيلات ومدفوعات منفصلة
                        SELECT PayDate, CAST(Pay_ID AS VARCHAR), 
                               CASE PayType WHEN 0 THEN 'تحصيل' WHEN 1 THEN 'تحصيل' WHEN 2 THEN 'رد نقدي' WHEN 3 THEN 'خصم مسموح' END, 
                               ISNULL(Notes, ''), 
                               CASE WHEN PayType = 2 THEN PayMoney ELSE 0 END, 
                               CASE WHEN PayType IN (0, 1, 3) THEN PayMoney ELSE 0 END 
                        FROM Cus_Payments WHERE CusId = @CusId AND PayDate >= @FromDate AND PayDate <= @ToDate
                    ) T
                    ORDER BY SortDate ASC";
            }
            else if (SelectedReportType == "كشف حساب تفصيلي للعميل")
            {
                if (SelectedCustomer == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار العميل أولاً", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                await GenerateDetailedStatementAsync(db, parameters, queryFromDate, queryToDate);
                return;  // early return — no further DataTable processing needed
            }

            // ── 6. المبيعات اليومية ────────────────────────────────────────
            else if (SelectedReportType == "المبيعات اليومية")
            {
                string taxColsD = showTax ? ", SUM(VatTax) AS SumVatTaxD, SUM(Tax) AS SumTaxD" : "";
                string taxSelectColsD = showTax
                    ? ", ISNULL(S.SumVatTaxD, 0) AS [ض.ق.م], ISNULL(S.SumTaxD, 0) AS [ض.أ.ت.ص]"
                    : "";
                sql = $@"
        ;WITH SalesDaily AS (
            SELECT CAST(SalesDate AS DATE) AS TransDate,
                   COUNT(Sales_ID) AS SalesCount,
                   SUM(Net) AS NetSales {taxColsD}
            FROM Sales
            WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate {taxFilter}
            GROUP BY CAST(SalesDate AS DATE)
        ),
        ReturnsDaily AS (
            SELECT CAST(SalesDate AS DATE) AS TransDate,
                   COUNT(Sales_ID) AS RetCount,
                   SUM(Net) AS NetReturns
            FROM ReSales
            WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate
            GROUP BY CAST(SalesDate AS DATE)
        ),
        AllDates AS (
            SELECT TransDate FROM SalesDaily
            UNION
            SELECT TransDate FROM ReturnsDaily
        )
        SELECT 
            CONVERT(VARCHAR, D.TransDate, 103)               AS [التاريخ],
            ISNULL(S.SalesCount, 0)                          AS [عدد فواتير البيع],
            ISNULL(S.NetSales, 0)                            AS [قيمة المبيعات],
            ISNULL(R.RetCount, 0)                            AS [عدد فواتير المرتجع],
            ISNULL(R.NetReturns, 0)                          AS [قيمة المرتجعات],
            (ISNULL(S.NetSales, 0) - ISNULL(R.NetReturns, 0)) AS [صافي المبيعات]
            {taxSelectColsD}
        FROM AllDates D
        LEFT JOIN SalesDaily S ON D.TransDate = S.TransDate
        LEFT JOIN ReturnsDaily R ON D.TransDate = R.TransDate
        ORDER BY D.TransDate DESC";
            }
            else if (SelectedReportType == "مبيعات الموتوسيكلات")
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
                if (SelectedCustomer != null)
                {
                    parameters.Add("CusId", SelectedCustomer.CusId);
                    modelFilter += " AND SC.CusID = @CusId ";
                }

                sql = @"
                    SELECT CONVERT(VARCHAR, SC.SalesDate, 103)          AS [التاريخ],
                           SC.Sales_ID                                  AS [رقم الفاتورة],
                           CU.CusName                                   AS [العميل],
                           CB.BrandName + ' - ' + CM.ModelName          AS [الموديل],
                           COL.ColorName                                AS [اللون],
                           C.YearNo                                     AS [السنة],
                           C.ChassisNo                                  AS [رقم الشاسيه],
                           C.PlateNo                                    AS [رقم اللوحة],
                           SC.Total                                     AS [سعر البيع]
                    FROM Sales_Car SC
                    INNER JOIN Cars       C   ON SC.CarID   = C.Car_ID
                    INNER JOIN CarModels  CM  ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands  CB  ON CM.BrandID = CB.Brand_ID
                    LEFT  JOIN Colors     COL ON C.ColorId  = COL.Color_ID
                    LEFT  JOIN Customers  CU  ON SC.CusID   = CU.Cus_ID
                    WHERE SC.SalesDate >= @FromDate AND SC.SalesDate <= @ToDate
                    " + modelFilter + @"
                    ORDER BY SC.SalesDate DESC";
            }
            // ── 8. كشف حساب عميل (موتوسيكلات) ───────────────────────────
            else if (SelectedReportType == "كشف حساب عميل (موتوسيكلات)")
            {
                if (SelectedCustomer == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار العميل أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                parameters.Add("CusId", SelectedCustomer.CusId);

                sql = @"
                    SELECT CONVERT(VARCHAR, SC.SalesDate, 103)         AS [التاريخ],
                           'بيع موتوسيكل رقم ' + CAST(SC.Sales_ID AS VARCHAR) AS [البيان],
                           CB.BrandName + ' - ' + CM.ModelName         AS [الموديل],
                           C.ChassisNo                                 AS [الشاسيه],
                           SC.Total                                    AS [مدين],
                           0                                           AS [دائن]
                    FROM Sales_Car SC
                    INNER JOIN Cars       C  ON SC.CarID   = C.Car_ID
                    INNER JOIN CarModels  CM ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands  CB ON CM.BrandID = CB.Brand_ID
                    WHERE SC.CusID = @CusId
                      AND SC.SalesDate >= @FromDate AND SC.SalesDate <= @ToDate

                    UNION ALL

                    SELECT CONVERT(VARCHAR, PayDate, 103),
                           CASE PayType WHEN 0 THEN 'سداد' WHEN 1 THEN 'تحصيل'
                                        WHEN 2 THEN 'رد'   WHEN 3 THEN 'خصم' END,
                           ISNULL(Notes, ''), '', 
                           CASE WHEN PayType = 2 THEN PayMoney ELSE 0 END,
                           CASE WHEN PayType IN (0,1,3) THEN PayMoney ELSE 0 END
                    FROM Cus_Payments
                    WHERE CusId = @CusId
                      AND PayDate >= @FromDate AND PayDate <= @ToDate

                    ORDER BY [التاريخ] ASC";
            }
            // ── 9. تقرير المرتجعات المفصل ─────────────────────────────────
            else if (SelectedReportType == "تقرير المرتجعات المفصل")
            {
                string cusFilter = "";
                if (SelectedCustomer != null)
                {
                    cusFilter = " AND M.CusId = @CusId ";
                    parameters.Add("CusId", SelectedCustomer.CusId);
                }

                sql = @"
                    SELECT CONVERT(VARCHAR, M.SalesDate, 103)          AS [التاريخ],
                           M.Sales_ID                                   AS [رقم المرتجع],
                           CU.CusName                                   AS [العميل],
                           I.ItemName                                   AS [الصنف],
                           S.Qty                                        AS [الكمية],
                           S.Price                                      AS [السعر],
                           S.Disc                                       AS [الخصم],
                           (S.Qty * (S.Price - S.Disc))                AS [الإجمالي],
                           M.Notes                                      AS [ملاحظات]
                    FROM ReSales M
                    INNER JOIN ReSales_Sub S ON M.Sales_ID = S.SalesId
                    INNER JOIN Items       I ON S.ItemId   = I.Item_ID
                    LEFT  JOIN Customers  CU ON M.CusId    = CU.Cus_ID
                    WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate
                    " + cusFilter + @"
                    ORDER BY M.SalesDate DESC";
            }
            // ── 10. أعلى العملاء مبيعاً ──────────────────────────────────
            else if (SelectedReportType == "أعلى العملاء مبيعاً")
            {
                sql = $@"
                    SELECT CU.CusName                                           AS [العميل],
                           COUNT(DISTINCT M.Sales_ID)                           AS [عدد الفواتير],
                           SUM(M.Net)                                           AS [إجمالي المبيعات],
                           ISNULL(RET.TotalReturns, 0)                          AS [إجمالي المرتجعات],
                           SUM(M.Net) - ISNULL(RET.TotalReturns, 0)            AS [صافي المبيعات],
                           ISNULL(PAY.TotalPayments, 0)                         AS [إجمالي المتحصل],
                           SUM(M.Net) - ISNULL(RET.TotalReturns, 0)
                               - ISNULL(PAY.TotalPayments, 0)                  AS [الرصيد المتبقي]
                    FROM Sales M
                    LEFT JOIN Customers CU ON M.CusId = CU.Cus_ID
                    OUTER APPLY (
                        SELECT ISNULL(SUM(Net), 0) AS TotalReturns
                        FROM ReSales
                        WHERE CusId = M.CusId
                          AND SalesDate >= @FromDate AND SalesDate <= @ToDate
                    ) RET
                    OUTER APPLY (
                        SELECT ISNULL(SUM(PayMoney), 0) AS TotalPayments
                        FROM Cus_Payments
                        WHERE CusId = M.CusId
                          AND PayDate >= @FromDate AND PayDate <= @ToDate
                          AND PayType IN (0, 1, 3)
                    ) PAY
                    WHERE M.SalesDate >= @FromDate AND M.SalesDate <= @ToDate {taxFilter}
                    GROUP BY CU.CusName, RET.TotalReturns, PAY.TotalPayments
                    ORDER BY [صافي المبيعات] DESC";
            }

            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, parameters))
            {
                dt.Load(reader);
            }

            if (SelectedReportType == "كشف حساب عميل" || SelectedReportType == "كشف حساب تفصيلي للعميل")
            {
                // Add الرصيد columns
                dt.Columns.Add("رصيد مدين", typeof(double));
                dt.Columns.Add("رصيد دائن", typeof(double));

                double runningBalance = 0; // Positive = Debit, Negative = Credit
                
                var opParams = new DynamicParameters();
                opParams.Add("CusId", SelectedCustomer.CusId);
                opParams.Add("FromDate", queryFromDate);

                // Initial Balance from Customers table
                var customerInfo = await db.QueryFirstOrDefaultAsync<dynamic>("SELECT ISNULL(Debit, 0) AS Debit, ISNULL(Credit, 0) AS Credit, OpenDate FROM Customers WHERE Cus_ID = @CusId", opParams);
                double cusIniDebit = Convert.ToDouble(customerInfo?.Debit ?? 0);
                double cusIniCredit = Convert.ToDouble(customerInfo?.Credit ?? 0);

                // Previous Transactions before FromDate
                double prevSales = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Net), 0) FROM Sales WHERE CusId = @CusId AND SalesDate < @FromDate", opParams);
                double prevSalesCar = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total), 0) FROM Sales_Car WHERE CusId = @CusId AND SalesDate < @FromDate", opParams);
                double prevReSales = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Net), 0) FROM ReSales WHERE CusId = @CusId AND SalesDate < @FromDate", opParams);
                double prevPaymentsCredit = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney), 0) FROM Cus_Payments WHERE CusId = @CusId AND PayDate < @FromDate AND PayType IN (0, 1, 3)", opParams);
                double prevPaymentsDebit = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney), 0) FROM Cus_Payments WHERE CusId = @CusId AND PayDate < @FromDate AND PayType = 2", opParams);
                double prevSalPay = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(SP.PayMoney), 0) FROM Sales_Payments SP JOIN Sales S ON SP.SalesId = S.Sales_ID WHERE S.CusId = @CusId AND SP.PayDate < @FromDate", opParams);
                double prevSalCarPay = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(SCP.PayMoney), 0) FROM Sales_Car_Payments SCP JOIN Sales_Car SC ON SCP.SalesId = SC.Sales_ID WHERE SC.CusId = @CusId AND SCP.PayDate < @FromDate", opParams);
                double prevBuyCar = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BC.Net), 0) FROM Buy_Car BC JOIN Cars C ON BC.CarID = C.Car_ID WHERE C.IsFromCustomer = 1 AND C.SourceCustomerID = @CusId AND BC.BuyDate < @FromDate", opParams);
                double prevBuyCarPay = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BCP.PayMoney), 0) FROM Buy_Car_Payments BCP JOIN Buy_Car BC ON BCP.BuyID = BC.Buy_ID JOIN Cars C ON BC.CarID = C.Car_ID WHERE C.IsFromCustomer = 1 AND C.SourceCustomerID = @CusId AND BCP.PayDate < @FromDate", opParams);

                double prevDebit = prevSales + prevSalesCar + prevPaymentsDebit + prevBuyCarPay;
                double prevCredit = prevReSales + prevPaymentsCredit + prevSalPay + prevSalCarPay + prevBuyCar;
                
                if (dt.Columns.Contains("SortDate")) dt.Columns["SortDate"]!.AllowDBNull = true;
                
                int insertedRows = 0;

                string colDebit  = "مدين (عليه)";
                string colCredit = "دائن (له)";

                if (dt.Columns.Contains("نوع الحركة")) dt.Columns["نوع الحركة"]!.AllowDBNull = true;
                if (dt.Columns.Contains("رقم الحركة")) dt.Columns["رقم الحركة"]!.AllowDBNull = true;

                var rowSabiq = dt.NewRow();
                rowSabiq["التاريخ"] = queryFromDate.ToString("dd/MM/yyyy");
                rowSabiq["نوع الحركة"] = "ما قبله";
                rowSabiq["البيان"] = "الرصيد السابق";
                rowSabiq[colDebit] = prevDebit;
                rowSabiq[colCredit] = prevCredit;
                runningBalance += (prevDebit - prevCredit);

                rowSabiq["رصيد مدين"] = runningBalance > 0 ? runningBalance : 0;
                rowSabiq["رصيد دائن"] = runningBalance < 0 ? Math.Abs(runningBalance) : 0;
                dt.Rows.InsertAt(rowSabiq, insertedRows++);

                // 2. Raseed Iftitahy
                if (cusIniDebit > 0 || cusIniCredit > 0)
                {
                    runningBalance += (cusIniDebit - cusIniCredit);
                    var rowIft = dt.NewRow();
                    rowIft["التاريخ"] = (customerInfo?.OpenDate != null) ? $"{Convert.ToDateTime((object?)customerInfo.OpenDate):dd/MM/yyyy}" : "";
                    rowIft["نوع الحركة"] = "افتتاحي";
                    rowIft["البيان"] = "الرصيد الافتتاحي";
                    rowIft[colDebit] = cusIniDebit;
                    rowIft[colCredit] = cusIniCredit;

                    rowIft["رصيد مدين"] = runningBalance > 0 ? runningBalance : 0;
                    rowIft["رصيد دائن"] = runningBalance < 0 ? Math.Abs(runningBalance) : 0;
                    dt.Rows.InsertAt(rowIft, insertedRows++);
                }

                // Loop Data
                for (int i = insertedRows; i < dt.Rows.Count; i++)
                {
                    double d = Convert.ToDouble(dt.Rows[i][colDebit] == DBNull.Value ? 0 : dt.Rows[i][colDebit]);
                    double c = Convert.ToDouble(dt.Rows[i][colCredit] == DBNull.Value ? 0 : dt.Rows[i][colCredit]);
                    runningBalance += (d - c);
                    
                    dt.Rows[i]["رصيد مدين"] = runningBalance > 0 ? runningBalance : 0;
                    dt.Rows[i]["رصيد دائن"] = runningBalance < 0 ? Math.Abs(runningBalance) : 0;
                }
                if (dt.Columns.Contains("SortDate")) dt.Columns.Remove("SortDate");
            }

            ReportData = dt.DefaultView;
            
            // Generate Totals and Metadata for PDF Report
            double totalSales = 0, totalReturns = 0;
            int countSales = 0, countReturns = 0;

            if (SelectedReportType == "المبيعات بالأصناف")
            {
                double sumSQty = 0, sumRQty = 0, sumNetQty = 0;
                double sumSVal = 0, sumRVal = 0, sumNetVal = 0;
                int itemsCount = 0;

                foreach (System.Data.DataRowView row in dt.DefaultView)
                {
                    if (row["الصنف"].ToString() != "إجمالي المجموعة ->")
                    {
                        itemsCount++;
                        sumSQty += Convert.ToDouble(row["كمية البيع"] == DBNull.Value ? 0 : row["كمية البيع"]);
                        sumRQty += Convert.ToDouble(row["كمية المرتجع"] == DBNull.Value ? 0 : row["كمية المرتجع"]);
                        sumNetQty += Convert.ToDouble(row["صافي الكمية"] == DBNull.Value ? 0 : row["صافي الكمية"]);
                        sumSVal += Convert.ToDouble(row["قيمة البيع"] == DBNull.Value ? 0 : row["قيمة البيع"]);
                        sumRVal += Convert.ToDouble(row["قيمة المرتجع"] == DBNull.Value ? 0 : row["قيمة المرتجع"]);
                        sumNetVal += Convert.ToDouble(row["صافي المبيعات"] == DBNull.Value ? 0 : row["صافي المبيعات"]);
                    }
                }

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد الأصناف", itemsCount.ToString() },
                    { "كمية البيع", sumSQty.ToString("N2") },
                    { "كمية المرتجع", sumRQty.ToString("N2") },
                    { "صافي الكمية", sumNetQty.ToString("N2") },
                    { "إجمالي المبيعات", sumSVal.ToString("N2") },
                    { "إجمالي المرتجعات", sumRVal.ToString("N2") },
                    { "صافي المبيعات", sumNetVal.ToString("N2") }
                };
            }
            else if (SelectedReportType == "كشف حساب عميل" || SelectedReportType == "كشف حساب تفصيلي للعميل")
            {
                double sumDebit = 0, sumCredit = 0;
                string colDebit  = "مدين (عليه)";
                string colCredit = "دائن (له)";

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    if (row[colDebit] != DBNull.Value) sumDebit += Convert.ToDouble(row[colDebit]);
                    if (row[colCredit] != DBNull.Value) sumCredit += Convert.ToDouble(row[colCredit]);
                }
                
                double finalBal = sumDebit - sumCredit;

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "إجمالي مدين", sumDebit.ToString("N2") },
                    { "إجمالي دائن", sumCredit.ToString("N2") },
                    { "الرصيد", finalBal >= 0 ? finalBal.ToString("N2") + " (مدين)" : Math.Abs(finalBal).ToString("N2") + " (دائن)" }
                };
            }
            else if (SelectedReportType == "المبيعات بالعملاء")
            {
                double sumSalesVal = 0, sumReturnsVal = 0, sumNetVal = 0;
                int sumSalesCount = 0, sumReturnsCount = 0;
                int customersCount = dt.Rows.Count;

                foreach (System.Data.DataRowView row in dt.DefaultView)
                {
                    sumSalesCount += Convert.ToInt32(row["عدد فواتير بيع"] == DBNull.Value ? 0 : row["عدد فواتير بيع"]);
                    sumReturnsCount += Convert.ToInt32(row["عدد فواتير مرتجع"] == DBNull.Value ? 0 : row["عدد فواتير مرتجع"]);

                    sumSalesVal += Convert.ToDouble(row["قيمة المبيعات"] == DBNull.Value ? 0 : row["قيمة المبيعات"]);
                    sumReturnsVal += Convert.ToDouble(row["قيمة المرتجعات"] == DBNull.Value ? 0 : row["قيمة المرتجعات"]);
                    sumNetVal += Convert.ToDouble(row["صافي المبيعات"] == DBNull.Value ? 0 : row["صافي المبيعات"]);
                }

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد العملاء", customersCount.ToString() },
                    { "عدد فواتير بيع", sumSalesCount.ToString() },
                    { "عدد فواتير مرتجع", sumReturnsCount.ToString() },
                    { "إجمالي المبيعات", sumSalesVal.ToString("N2") },
                    { "إجمالي المرتجعات", sumReturnsVal.ToString("N2") },
                    { "صافي المبيعات", sumNetVal.ToString("N2") },
                    { "إجمالي ض.ق.م", "0.00" },   // مجرد شكل للطباعة
                    { "إجمالي ض.أ.ت.ص", "0.00" } // مجرد شكل للطباعة
                };
            }
            else if (SelectedReportType == "مبيعات الموتوسيكلات")
            {
                double sumNetVal = 0;
                int countCars = dt.Rows.Count;

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    if (row["سعر البيع"] != DBNull.Value)
                        sumNetVal += Convert.ToDouble(row["سعر البيع"]);
                }

                MotorcyclesCount = countCars;
                MotorcyclesTotalSales = sumNetVal;

                _currentFooterTotals = new Dictionary<string, string>
                {
                    { "عدد الموتوسيكلات", countCars.ToString() },
                    { "إجمالي المبيعات", sumNetVal.ToString("N2") }
                };
            }
            else
            {
                string taxCondition = taxFilter;
                var salesSum = await db.QueryFirstOrDefaultAsync(
                    $"SELECT COUNT(Sales_ID) AS Cnt, ISNULL(SUM(Net), 0) AS Tot FROM Sales WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate {taxCondition}", parameters);
                countSales = salesSum?.Cnt ?? 0;
                totalSales = salesSum?.Tot ?? 0;

                var resalesSum = await db.QueryFirstOrDefaultAsync(
                    "SELECT COUNT(Sales_ID) AS Cnt, ISNULL(SUM(Net), 0) AS Tot FROM ReSales WHERE SalesDate >= @FromDate AND SalesDate <= @ToDate", parameters);
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

            if (SelectedReportType == "كشف حساب عميل" || SelectedReportType == "كشف حساب تفصيلي للعميل" || SelectedReportType == "كشف حساب عميل (موتوسيكلات)")
            {
                _currentHeaderInfo = new Dictionary<string, string>
                {
                    { "اسم العميل", SelectedCustomer?.CusName ?? "" },
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
                    ? MotorBike.Services.ReportGenerator.GenerateDetailedPdf(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals)
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
                ? MotorBike.Services.ReportGenerator.GenerateDetailedPdf(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals)
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

    private async Task GenerateDetailedStatementAsync(
        System.Data.IDbConnection db,
        DynamicParameters parameters,
        DateTime queryFromDate,
        DateTime queryToDate)
    {
        int cusId = SelectedCustomer!.CusId;

        // ── 1. الرصيد السابق (قبل الفترة) ──
        var opParams = new DynamicParameters();
        opParams.Add("CusId", cusId);
        opParams.Add("FromDate", queryFromDate);

        var customerInfo = await db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT ISNULL(Debit,0) AS Debit, ISNULL(Credit,0) AS Credit, OpenDate FROM Customers WHERE Cus_ID=@CusId", opParams);

        double cusIniDebit  = Convert.ToDouble(customerInfo?.Debit  ?? 0);
        double cusIniCredit = Convert.ToDouble(customerInfo?.Credit ?? 0);

        double prevSales        = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total-Disc+AddMony),0) FROM Sales         WHERE CusId=@CusId AND SalesDate<@FromDate", opParams);
        double prevSalesCar     = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total),0)              FROM Sales_Car     WHERE CusId=@CusId AND SalesDate<@FromDate", opParams);
        double prevReSales      = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total-Disc+AddMony),0) FROM ReSales       WHERE CusId=@CusId AND SalesDate<@FromDate", opParams);
        double prevPayCC        = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney),0) FROM Cus_Payments WHERE CusId=@CusId AND PayDate<@FromDate AND PayType IN(0,1,3)", opParams);
        double prevPayCD        = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney),0) FROM Cus_Payments WHERE CusId=@CusId AND PayDate<@FromDate AND PayType=2", opParams);
        double prevSalPay       = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(SP.PayMoney),0) FROM Sales_Payments SP JOIN Sales S ON SP.SalesId=S.Sales_ID WHERE S.CusId=@CusId AND SP.PayDate<@FromDate", opParams);
        double prevCarPay       = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(SCP.PayMoney),0) FROM Sales_Car_Payments SCP JOIN Sales_Car SC ON SCP.SalesId=SC.Sales_ID WHERE SC.CusId=@CusId AND SCP.PayDate<@FromDate", opParams);
        double prevBuyCar       = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BC.Net),0) FROM Buy_Car BC JOIN Cars C ON BC.CarID=C.Car_ID WHERE C.IsFromCustomer=1 AND C.SourceCustomerID=@CusId AND BC.BuyDate<@FromDate", opParams);
        double prevBuyCarPay    = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(BCP.PayMoney),0) FROM Buy_Car_Payments BCP JOIN Buy_Car BC ON BCP.BuyID=BC.Buy_ID JOIN Cars C ON BC.CarID=C.Car_ID WHERE C.IsFromCustomer=1 AND C.SourceCustomerID=@CusId AND BCP.PayDate<@FromDate", opParams);

        double prevDebit  = prevSales + prevSalesCar + prevPayCD + prevBuyCarPay;
        double prevCredit = prevReSales + prevPayCC + prevSalPay + prevCarPay + prevBuyCar;

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

        // سطر الرصيد الافتتاحي
        if (cusIniDebit > 0 || cusIniCredit > 0)
        {
            runBal += cusIniDebit - cusIniCredit;
            DateTime rawIftDate = (customerInfo?.OpenDate != null) ? Convert.ToDateTime((object?)customerInfo.OpenDate) : queryFromDate;
            rows.Add(new DetailedAccountRow {
                RawDate = rawIftDate,
                Date = rawIftDate.ToString("dd/MM/yyyy"),
                TransType = "افتتاحي",
                Notes = "الرصيد الافتتاحي",
                Debit = cusIniDebit, Credit = cusIniCredit,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
            });
        }

        // ── 2. جلب الحركات في الفترة ──
        var txParams = new DynamicParameters();
        txParams.Add("CusId", cusId);
        txParams.Add("FromDate", queryFromDate);
        txParams.Add("ToDate", queryToDate);

        // فواتير مبيعات الأصناف
        var sales = await db.QueryAsync<dynamic>(@"
            SELECT M.Sales_ID AS Id, M.SalesDate AS TxDate, CAST(M.Sales_ID AS VARCHAR) AS RefNo,
                   'فاتورة مبيعات' AS TransType, ISNULL(M.Notes,'') AS Notes,
                   (M.Total-M.Disc+M.AddMony) AS Debit, 0 AS Credit,
                   ISNULL(ST.StoreName,'') AS Branch, ISNULL(U.UserName,'') AS Agent
            FROM Sales M
            LEFT JOIN Sales_Sub SS ON SS.SalesId=M.Sales_ID
            LEFT JOIN Stores ST ON SS.StoreId=ST.Store_ID
            LEFT JOIN Users U ON M.AddUser=U.User_ID
            WHERE M.CusId=@CusId AND M.SalesDate>=@FromDate AND M.SalesDate<=@ToDate
            GROUP BY M.Sales_ID, M.SalesDate, M.Total, M.Disc, M.AddMony, M.Notes, ST.StoreName, U.UserName", txParams);

        foreach (var s in sales)
        {
            int sid = Convert.ToInt32(s.Id);
            var subItems = await db.QueryAsync<dynamic>(@"
                SELECT I.ItemName, UN.UnitName AS Unit, SS.Qty, SS.Price, SS.DiscPer, (SS.Qty*(SS.Price-SS.Disc)) AS Total
                FROM Sales_Sub SS
                JOIN Items I ON SS.ItemId=I.Item_ID
                LEFT JOIN Units UN ON SS.UnitId=UN.Unit_ID
                WHERE SS.SalesId=@SalesId", new { SalesId = sid });

            double d = Convert.ToDouble(s.Debit);
            runBal += d;
            DateTime txDate = Convert.ToDateTime((object)s.TxDate);
            rows.Add(new DetailedAccountRow {
                RawDate = txDate,
                Date = txDate.ToString("dd/MM/yyyy"),
                RefNo = s.RefNo, Branch = s.Branch, Agent = s.Agent,
                TransType = s.TransType, Notes = s.Notes,
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
            var salPays = await db.QueryAsync<dynamic>(
                "SELECT CAST(SalesId AS VARCHAR) AS RefNo, PayDate, PayMoney, ISNULL(Notes,'') AS Notes FROM Sales_Payments WHERE SalesId=@SalesId", new { SalesId = sid });
            foreach (var p in salPays)
            {
                double c = Convert.ToDouble(p.PayMoney);
                runBal -= c;
                DateTime pDate = Convert.ToDateTime((object)p.PayDate);
                rows.Add(new DetailedAccountRow {
                    RawDate = pDate.AddSeconds(1), // To show payment right after its invoice if same second
                    Date = pDate.ToString("dd/MM/yyyy"),
                    RefNo = p.RefNo, TransType = "تحصيل مع الفاتورة", Notes = p.Notes,
                    Debit = 0, Credit = c,
                    RunningDebit  = runBal > 0 ? runBal : 0,
                    RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
                });
            }
        }

        // مبيعات الموتوسيكلات
        var salesCar = await db.QueryAsync<dynamic>(@"
            SELECT M.Sales_ID AS Id, M.SalesDate AS TxDate, CAST(M.Sales_ID AS VARCHAR) AS RefNo,
                   'بيع موتوسيكل' AS TransType, ISNULL(M.Notes,'') AS Notes, M.Total AS Debit, 0 AS Credit,
                   ISNULL(CB.BrandName,'') AS Brand, ISNULL(CM.ModelName,'') AS ModelName, 
                   ISNULL(C.ChassisNo,'') AS Chassis, ISNULL(C.MotorNo,'') AS MotorNo, ISNULL(C.PlateNo,'') AS PlateNo, ISNULL(C.Mileage,0) AS Mileage
            FROM Sales_Car M
            LEFT JOIN Cars C ON M.CarID=C.Car_ID
            LEFT JOIN CarModels CM ON C.ModelID=CM.Model_ID
            LEFT JOIN CarBrands CB ON CM.BrandID=CB.Brand_ID
            WHERE M.CusId=@CusId AND M.SalesDate>=@FromDate AND M.SalesDate<=@ToDate", txParams);

        foreach (var sc in salesCar)
        {
            int scid = Convert.ToInt32(sc.Id);
            double d = Convert.ToDouble(sc.Debit);
            runBal += d;
            DateTime txDate = Convert.ToDateTime((object)sc.TxDate);
            rows.Add(new DetailedAccountRow {
                RawDate = txDate,
                Date = txDate.ToString("dd/MM/yyyy"),
                RefNo = sc.RefNo, TransType = sc.TransType, Notes = sc.Notes,
                Debit = d, Credit = 0,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0,
                IsCarTransaction = true,
                Items = new List<InvoiceSubItem> {
                    new() { ItemName = $"{sc.Brand} - {sc.ModelName}", 
                            ChassisNo = sc.Chassis, MotorNo = sc.MotorNo, PlateNo = sc.PlateNo, Mileage = sc.Mileage,
                            Price = d, Total = d }
                }
            });

            var carPays = await db.QueryAsync<dynamic>(
                "SELECT CAST(SalesId AS VARCHAR) AS RefNo, PayDate, PayMoney, ISNULL(Notes,'') AS Notes FROM Sales_Car_Payments WHERE SalesId=@SalesId", new { SalesId = scid });
            foreach (var p in carPays)
            {
                double c = Convert.ToDouble(p.PayMoney);
                runBal -= c;
                DateTime pDate = Convert.ToDateTime((object)p.PayDate);
                rows.Add(new DetailedAccountRow {
                    RawDate = pDate.AddSeconds(1),
                    Date = pDate.ToString("dd/MM/yyyy"),
                    RefNo = p.RefNo, TransType = "تحصيل مع الفاتورة", Notes = p.Notes,
                    Debit = 0, Credit = c,
                    RunningDebit  = runBal > 0 ? runBal : 0,
                    RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
                });
            }
        }

        // شراء موتوسيكلات من العميل
        var buyCarRows = await db.QueryAsync<dynamic>(@"
            SELECT BC.Buy_ID AS Id, BC.BuyDate AS TxDate, CAST(BC.Buy_ID AS VARCHAR) AS RefNo,
                   'شراء موتوسيكل' AS TransType, ISNULL(BC.Notes,'') AS Notes, 0 AS Debit, BC.Net AS Credit,
                   ISNULL(CB.BrandName,'') AS Brand, ISNULL(CM.ModelName,'') AS ModelName, 
                   ISNULL(C.ChassisNo,'') AS Chassis, ISNULL(C.MotorNo,'') AS MotorNo, ISNULL(C.PlateNo,'') AS PlateNo, ISNULL(C.Mileage,0) AS Mileage
            FROM Buy_Car BC
            INNER JOIN Cars C ON BC.CarID = C.Car_ID
            LEFT JOIN CarModels CM ON C.ModelID = CM.Model_ID
            LEFT JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
            WHERE C.IsFromCustomer = 1 AND C.SourceCustomerID = @CusId AND BC.BuyDate >= @FromDate AND BC.BuyDate <= @ToDate", txParams);

        foreach (var bc in buyCarRows)
        {
            int bcid = Convert.ToInt32(bc.Id);
            double c = Convert.ToDouble(bc.Credit);
            runBal -= c;
            DateTime txDate = Convert.ToDateTime((object)bc.TxDate);
            rows.Add(new DetailedAccountRow {
                RawDate = txDate,
                Date = txDate.ToString("dd/MM/yyyy"),
                RefNo = bc.RefNo, TransType = bc.TransType, Notes = bc.Notes,
                Debit = 0, Credit = c,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0,
                IsCarTransaction = true,
                Items = new List<InvoiceSubItem> {
                    new() { ItemName = $"{bc.Brand} - {bc.ModelName}", 
                            ChassisNo = bc.Chassis, MotorNo = bc.MotorNo, PlateNo = bc.PlateNo, Mileage = bc.Mileage,
                            Price = c, Total = c }
                }
            });

            var buyCarPays = await db.QueryAsync<dynamic>(
                "SELECT CAST(BuyID AS VARCHAR) AS RefNo, PayDate, PayMoney, ISNULL(Notes,'') AS Notes FROM Buy_Car_Payments WHERE BuyID=@BuyId", new { BuyId = bcid });
            foreach (var p in buyCarPays)
            {
                double d = Convert.ToDouble(p.PayMoney);
                runBal += d; // payment TO customer is debit for them
                DateTime pDate = Convert.ToDateTime((object)p.PayDate);
                rows.Add(new DetailedAccountRow {
                    RawDate = pDate.AddSeconds(1),
                    Date = pDate.ToString("dd/MM/yyyy"),
                    RefNo = p.RefNo, TransType = "سداد مع فاتورة الشراء", Notes = p.Notes,
                    Debit = d, Credit = 0,
                    RunningDebit  = runBal > 0 ? runBal : 0,
                    RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
                });
            }
        }

        // مرتجعات المبيعات
        var resales = await db.QueryAsync<dynamic>(@"
            SELECT M.Sales_ID AS Id, M.SalesDate AS TxDate, CAST(M.Sales_ID AS VARCHAR) AS RefNo,
                   'مرتجع بيع' AS TransType, ISNULL(M.Notes,'') AS Notes, 0 AS Debit, (M.Total-M.Disc+M.AddMony) AS Credit
            FROM ReSales M WHERE M.CusId=@CusId AND M.SalesDate>=@FromDate AND M.SalesDate<=@ToDate", txParams);

        foreach (var r in resales)
        {
            int rid = Convert.ToInt32(r.Id);
            var subItems = await db.QueryAsync<dynamic>(@"
                SELECT I.ItemName, UN.UnitName AS Unit, SS.Qty, SS.Price, SS.DiscPer, (SS.Qty*(SS.Price-SS.Disc)) AS Total
                FROM ReSales_Sub SS
                JOIN Items I ON SS.ItemId=I.Item_ID
                LEFT JOIN Units UN ON SS.UnitId=UN.Unit_ID
                WHERE SS.SalesId=@SalesId", new { SalesId = rid });

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
        }

        // تحصيلات منفصلة
        var cuspays = await db.QueryAsync<dynamic>(@"
            SELECT Pay_ID AS Id, PayDate, CAST(Pay_ID AS VARCHAR) AS RefNo, PayType, PayMoney, ISNULL(Notes,'') AS Notes
            FROM Cus_Payments WHERE CusId=@CusId AND PayDate>=@FromDate AND PayDate<=@ToDate", txParams);

        foreach (var p in cuspays)
        {
            string typeName = Convert.ToInt32(p.PayType) switch {
                0 => "تحصيل", 1 => "تحصيل", 2 => "رد نقدي", 3 => "خصم مسموح", _ => "تحصيل"
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

        // ── 3. ترتيب حسب التاريخ (الخام) ثم رقم الحركة ──
        var sorted = rows.OrderBy(r => r.RawDate).ThenBy(r => r.RefNo).ToList();
        // إعادة حساب الرصيد بعد الترتيب
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
            { "اسم العميل", SelectedCustomer.CusName },
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
}

