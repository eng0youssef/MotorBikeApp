using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CarsReportsViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;

    [ObservableProperty]
    private ObservableCollection<string> _reportTypes =
    [
        "مخزون الموتوسيكلات الحالي",
        "الموتوسيكلات المباعة",
        "الموتوسيكلات المشتراة",
        "ربحية الموتوسيكلات",
        "أعلى الطرازات مبيعاً",
        "كشف حركة موتوسيكل"
    ];

    [ObservableProperty] private string _selectedReportType = "مخزون الموتوسيكلات الحالي";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate   = DateTime.Now;
    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked   = true;

    // CarModel filter
    [ObservableProperty] private ObservableCollection<CarModel> _carModels = [];
    [ObservableProperty] private CarModel? _selectedCarModel;

    // Individual car filter (for كشف حركة موتوسيكل)
    [ObservableProperty] private ObservableCollection<Car> _cars = [];
    [ObservableProperty] private Car? _selectedCar;
    [ObservableProperty] private string _carDisplayText = "";

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private System.Data.DataView _carStatusInfo = new System.Data.DataView();

    public bool IsDateRangeVisible => SelectedReportType != "مخزون الموتوسيكلات الحالي";
    public bool IsCarModelVisible  => SelectedReportType is "الموتوسيكلات المباعة"
                                                         or "الموتوسيكلات المشتراة"
                                                         or "ربحية الموتوسيكلات"
                                                         or "أعلى الطرازات مبيعاً";
    public bool IsCarVisible       => SelectedReportType == "كشف حركة موتوسيكل";

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDateRangeVisible));
        OnPropertyChanged(nameof(IsCarModelVisible));
        OnPropertyChanged(nameof(IsCarVisible));
        ReportData           = new System.Data.DataView();
        CarStatusInfo        = new System.Data.DataView();
        StatusMessage        = null;
        _currentFooterTotals = null;
        _currentHeaderInfo   = null;
    }

    private Dictionary<string, string>? _currentHeaderInfo;
    private Dictionary<string, string>? _currentFooterTotals;

    public CarsReportsViewModel(IDbConnectionFactory dbFactory, IRepository<CarModel> carModelRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(carModelRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(IRepository<CarModel> carModelRepo)
    {
        var models = await carModelRepo.GetAllAsync();
        CarModels = new ObservableCollection<CarModel>(models);

        // Load all cars for the individual car filter
        using var db = _dbFactory.CreateConnection();
        var carList = await db.QueryAsync<Car>(
            "SELECT C.Car_ID, C.ModelID, C.ChassisNo, C.PlateNo, C.YearNo FROM Cars C ORDER BY C.Car_ID DESC");
        Cars = new ObservableCollection<Car>(carList);
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

            // ── 1. مخزون الموتوسيكلات الحالي ─────────────────────────────
            if (SelectedReportType == "مخزون الموتوسيكلات الحالي")
            {
                sql = @"
                    SELECT CB.BrandName                                   AS [الماركة],
                           CM.ModelName                                   AS [الطراز],
                           C.YearNo                                       AS [السنة],
                           CL.ColorName                                   AS [اللون],
                           C.ChassisNo                                    AS [رقم الشاسيه],
                           C.MotorNo                                      AS [رقم الموتور],
                           C.CC                                           AS [CC],
                           C.PlateNo                                      AS [رقم اللوحة],
                           C.Mileage                                      AS [الكيلومتر],
                           CASE C.StatusId WHEN 1 THEN 'مخزن'
                                           WHEN 2 THEN 'مباع'
                                           WHEN 3 THEN 'صيانة' ELSE 'غير محدد' END AS [الحالة],
                           C.PurchasePrice                                AS [سعر الشراء],
                           CASE WHEN EXISTS (SELECT 1 FROM Sales WHERE CarID = C.Car_ID) THEN 'نعم' ELSE 'لا' END AS [تمت صيانته]
                    FROM Cars C
                    INNER JOIN CarModels CM ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
                    LEFT  JOIN Colors   CL ON C.ColorID  = CL.Color_ID
                    WHERE C.StatusId = 1
                    ORDER BY CB.BrandName, CM.ModelName, C.YearNo DESC";
            }
            // ── 2. الموتوسيكلات المباعة ───────────────────────────────────
            else if (SelectedReportType == "الموتوسيكلات المباعة")
            {
                string modelFilter = "";
                if (SelectedCarModel != null)
                {
                    modelFilter = " AND CM.Model_ID = @ModelId ";
                    p.Add("ModelId", SelectedCarModel.ModelId);
                }
                sql = @"
                    SELECT CONVERT(VARCHAR, S.SalesDate, 103)             AS [التاريخ],
                           S.Sales_ID                                      AS [رقم الفاتورة],
                           CU.CusName                                      AS [العميل],
                           CB.BrandName + ' - ' + CM.ModelName             AS [الطراز],
                           C.YearNo                                        AS [السنة],
                           C.ChassisNo                                     AS [رقم الشاسيه],
                           C.CC                                            AS [CC],
                           C.PlateNo                                       AS [رقم اللوحة],
                           S.Total                                         AS [سعر البيع],
                           C.PurchasePrice                                 AS [سعر الشراء],
                           (S.Total - C.PurchasePrice)                     AS [صافي الربح],
                           CASE WHEN EXISTS (SELECT 1 FROM Sales WHERE CarID = C.Car_ID) THEN 'نعم' ELSE 'لا' END AS [تمت صيانته]
                    FROM Sales_Car S
                    INNER JOIN Cars      C  ON S.CarID    = C.Car_ID
                    INNER JOIN CarModels CM ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
                    LEFT  JOIN Customers CU ON S.CusID    = CU.Cus_ID
                    WHERE S.SalesDate >= @FromDate AND S.SalesDate <= @ToDate
                    " + modelFilter + @"
                    ORDER BY S.SalesDate DESC";
            }
            // ── 3. الموتوسيكلات المشتراة ──────────────────────────────────
            else if (SelectedReportType == "الموتوسيكلات المشتراة")
            {
                string modelFilter = "";
                if (SelectedCarModel != null)
                {
                    modelFilter = " AND CM.Model_ID = @ModelId ";
                    p.Add("ModelId", SelectedCarModel.ModelId);
                }
                sql = @"
                    SELECT CONVERT(VARCHAR, B.BuyDate, 103)               AS [التاريخ],
                           B.Buy_ID                                        AS [رقم الفاتورة],
                           B.OwnerName                                     AS [اسم البائع],
                           B.OwnerTel                                      AS [التليفون],
                           CB.BrandName + ' - ' + CM.ModelName             AS [الطراز],
                           C.YearNo                                        AS [السنة],
                           C.ChassisNo                                     AS [رقم الشاسيه],
                           C.CC                                            AS [CC],
                           C.PlateNo                                       AS [رقم اللوحة],
                           B.Total                                         AS [سعر الشراء]
                    FROM Buy_Car B
                    LEFT  JOIN Cars      C  ON B.CarID    = C.Car_ID
                    LEFT  JOIN CarModels CM ON C.ModelID  = CM.Model_ID
                    LEFT  JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
                    WHERE B.BuyDate >= @FromDate AND B.BuyDate <= @ToDate
                    " + modelFilter + @"
                    ORDER BY B.BuyDate DESC";
            }
            // ── 4. ربحية الموتوسيكلات ─────────────────────────────────────
            else if (SelectedReportType == "ربحية الموتوسيكلات")
            {
                string modelFilter = "";
                if (SelectedCarModel != null)
                {
                    modelFilter = " AND CM.Model_ID = @ModelId ";
                    p.Add("ModelId", SelectedCarModel.ModelId);
                }
                sql = @"
                    SELECT CONVERT(VARCHAR, S.SalesDate, 103)             AS [تاريخ البيع],
                           CB.BrandName + ' - ' + CM.ModelName             AS [الطراز],
                           C.YearNo                                        AS [السنة],
                           C.ChassisNo                                     AS [رقم الشاسيه],
                           C.CC                                            AS [CC],
                           CU.CusName                                      AS [العميل],
                           C.PurchasePrice                                 AS [سعر الشراء],
                           S.Total                                         AS [سعر البيع],
                           (S.Total - C.PurchasePrice)                     AS [الربح],
                           CASE WHEN C.PurchasePrice > 0 
                                THEN CAST(ROUND(((S.Total - C.PurchasePrice) / C.PurchasePrice) * 100, 2) AS VARCHAR) + ' %'
                                ELSE '0 %' END                             AS [نسبة الربح],
                           CASE WHEN EXISTS (SELECT 1 FROM Sales WHERE CarID = C.Car_ID) THEN 'نعم' ELSE 'لا' END AS [تمت صيانته]
                    FROM Sales_Car S
                    INNER JOIN Cars      C  ON S.CarID    = C.Car_ID
                    INNER JOIN CarModels CM ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
                    LEFT  JOIN Customers CU ON S.CusID    = CU.Cus_ID
                    WHERE S.SalesDate >= @FromDate AND S.SalesDate <= @ToDate
                    " + modelFilter + @"
                    ORDER BY [الربح] DESC";
            }
            // ── 5. أعلى الطرازات مبيعاً ─────────────────────────────────
            else if (SelectedReportType == "أعلى الطرازات مبيعاً")
            {
                string modelFilter = "";
                if (SelectedCarModel != null)
                {
                    modelFilter = " AND CM.Model_ID = @ModelId ";
                    p.Add("ModelId", SelectedCarModel.ModelId);
                }

                sql = @"
                    SELECT CB.BrandName + ' - ' + CM.ModelName             AS [الطراز],
                           COUNT(S.Sales_ID)                               AS [عدد المبيعات],
                           SUM(S.Total)                                    AS [إجمالي البيع],
                           SUM(C.PurchasePrice)                           AS [إجمالي الشراء],
                           SUM(S.Total - C.PurchasePrice)                 AS [إجمالي الربح]
                    FROM Sales_Car S
                    INNER JOIN Cars      C  ON S.CarID    = C.Car_ID
                    INNER JOIN CarModels CM ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
                    WHERE S.SalesDate >= @FromDate AND S.SalesDate <= @ToDate
                    " + modelFilter + @"
                    GROUP BY CB.BrandName, CM.ModelName
                    ORDER BY [عدد المبيعات] DESC";
            }
            // ── 6. كشف حركة موتوسيكل ────────────────────────────────────
            else
            {
                if (SelectedCar == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار الموتوسيكل أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("CarId", SelectedCar.CarId);

                sql = @"
                    -- حركة الموتوسيكل (شراء، بيع، صيانة)
                    SELECT [نوع الحركة], [التاريخ], [رقم العملية], [نوع الطرف], [الطرف الآخر], [التليفون], [المبلغ], [العداد], [ملاحظات]
                    FROM (
                        SELECT 'شراء' AS [نوع الحركة],
                               CONVERT(VARCHAR, B.BuyDate, 103) AS [التاريخ],
                               B.Buy_ID AS [رقم العملية],
                               CASE WHEN C.IsFromCustomer = 1 THEN N'عميل' ELSE N'مورد' END AS [نوع الطرف],
                               B.OwnerName AS [الطرف الآخر],
                               B.OwnerTel AS [التليفون],
                               B.Total AS [المبلغ],
                               ISNULL(B.Mileage, 0) AS [العداد],
                               B.Notes AS [ملاحظات],
                               B.BuyDate AS [RawDate]
                        FROM Buy_Car B 
                        LEFT JOIN Cars C ON B.CarID = C.Car_ID
                        WHERE B.CarID = @CarId

                        UNION ALL

                        SELECT 'بيع',
                               CONVERT(VARCHAR, S.SalesDate, 103),
                               S.Sales_ID,
                               N'عميل',
                               CU.CusName,
                               CU.Tel,
                               S.Total,
                               ISNULL(S.Mileage, 0),
                               S.Notes,
                               S.SalesDate
                        FROM Sales_Car S
                        LEFT JOIN Customers CU ON S.CusID = CU.Cus_ID
                        WHERE S.CarID = @CarId

                        UNION ALL

                        SELECT 'صيانة',
                               CONVERT(VARCHAR, S.SalesDate, 103),
                               S.Sales_ID,
                               N'عميل',
                               CU.CusName,
                               CU.Tel,
                               S.Total,
                               0,
                            ISNULL(S.Notes, '') + 
                        ISNULL(' - الأصناف: ' + 
                            STUFF((
                                SELECT ', ' + I.ItemName
                                FROM Sales_Sub SS
                                INNER JOIN Items I ON SS.ItemId = I.Item_ID
                                WHERE SS.SalesId = S.Sales_ID
                                FOR XML PATH(''), TYPE
                            ).value('.', 'NVARCHAR(MAX)'), 1, 2, ''), ''),
                        S.SalesDate
                        FROM Sales S
                        LEFT JOIN Customers CU ON S.CusID = CU.Cus_ID
                        WHERE S.CarID = @CarId
                    ) AS T
                    ORDER BY [RawDate] ASC";

                // ── جلب حالة الموتوسيكل الحالية ───────────────────────────
                var statusSql = @"
                    SELECT 
                        CASE C.StatusId WHEN 1 THEN N'متاح بالمخزون' 
                                        WHEN 2 THEN N'مباع لعميل' 
                                        WHEN 3 THEN N'ملك لعميل' 
                                        ELSE N'غير محدد' END AS [الحالة الحالية],
                        ISNULL(CU.CusName, N'الوكالة') AS [المالك الحالي]
                    FROM Cars C
                    LEFT JOIN Customers CU ON C.OwnerId = CU.Cus_ID
                    LEFT JOIN Suppliers S ON C.SupplierId = S.Supp_ID
                    LEFT JOIN Customers CU2 ON C.SourceCustomerId = CU2.Cus_ID
                    WHERE C.Car_ID = @CarId";

                var dtStatus = new System.Data.DataTable();
                using (var readerStatus = await db.ExecuteReaderAsync(statusSql, p))
                    dtStatus.Load(readerStatus);
                CarStatusInfo = dtStatus.DefaultView;
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
            System.Windows.MessageBox.Show("خطأ في البحث:\n" + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task BuildFooterTotalsAsync(
        System.Data.IDbConnection db, DynamicParameters p,
        DateTime qFrom, DateTime qTo, System.Data.DataTable dt)
    {
        if (SelectedReportType == "مخزون الموتوسيكلات الحالي")
        {
            var cnt  = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Cars WHERE StatusId = 1");
            var tCost= await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PurchasePrice),0) FROM Cars WHERE StatusId = 1");
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "عدد الموتوسيكلات في المخزن", cnt.ToString()     },
                { "إجمالي تكلفة المخزون",        tCost.ToString("N2") }
            };
        }
        else if (SelectedReportType is "الموتوسيكلات المباعة" or "ربحية الموتوسيكلات")
        {
            var cnt    = await db.ExecuteScalarAsync<int>   ("SELECT COUNT(*) FROM Sales_Car WHERE SalesDate>=@FromDate AND SalesDate<=@ToDate", p);
            var tSale  = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(S.Total),0)           FROM Sales_Car S WHERE S.SalesDate>=@FromDate AND S.SalesDate<=@ToDate", p);
            var tCost  = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(C.PurchasePrice),0)   FROM Sales_Car S INNER JOIN Cars C ON S.CarID=C.Car_ID WHERE S.SalesDate>=@FromDate AND S.SalesDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "عدد الموتوسيكلات المباعة", cnt.ToString()              },
                { "إجمالي إيرادات البيع",      tSale.ToString("N2")       },
                { "إجمالي تكلفة الشراء",       tCost.ToString("N2")       },
                { "صافي الأرباح",               (tSale - tCost).ToString("N2") }
            };
        }
        else if (SelectedReportType == "الموتوسيكلات المشتراة")
        {
            var cnt   = await db.ExecuteScalarAsync<int>   ("SELECT COUNT(*) FROM Buy_Car WHERE BuyDate>=@FromDate AND BuyDate<=@ToDate", p);
            var tCost = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total),0) FROM Buy_Car WHERE BuyDate>=@FromDate AND BuyDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "عدد الموتوسيكلات المشتراة", cnt.ToString()      },
                { "إجمالي تكلفة الشراء",        tCost.ToString("N2") }
            };
        }
        else if (SelectedReportType == "أعلى الطرازات مبيعاً")
        {
            var cnt  = await db.ExecuteScalarAsync<int>   ("SELECT COUNT(*) FROM Sales_Car WHERE SalesDate>=@FromDate AND SalesDate<=@ToDate", p);
            var tRev = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total),0) FROM Sales_Car WHERE SalesDate>=@FromDate AND SalesDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي الموتوسيكلات المباعة", cnt.ToString()       },
                { "إجمالي الإيرادات",             tRev.ToString("N2") }
            };
        }
        else if (SelectedReportType == "كشف حركة موتوسيكل")
        {
            var tBuy   = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total), 0) FROM Buy_Car   WHERE CarID = @CarId", p);
            var tSale  = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total), 0) FROM Sales_Car WHERE CarID = @CarId", p);
            var tMaint = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total), 0) FROM Sales     WHERE CarID = @CarId", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي الشراء",  tBuy.ToString("N2")   },
                { "إجمالي الصيانة", tMaint.ToString("N2") },
                { "إجمالي البيع",    tSale.ToString("N2")  }
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
        if (SelectedCarModel != null) d.Add("الطراز", SelectedCarModel.ModelName);
        if (SelectedCar      != null) d.Add("الشاسيه", SelectedCar.ChassisNo);
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
        try
        {
            QuestPDF.Infrastructure.IDocument document = MotorBike.Services.ReportGenerator.CreatePdfDocument(
                company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals,
                IsCarVisible ? CarStatusInfo : null, 
                IsCarVisible ? "الحالة الحالية للموتوسيكل" : null);
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
                company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals,
                IsCarVisible ? CarStatusInfo : null, 
                IsCarVisible ? "الحالة الحالية للموتوسيكل" : null);
            
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
    private void ClearCarModel() => SelectedCarModel = null;

    [RelayCommand]
    private void ClearCar() => SelectedCar = null;
}
