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
        "أعلى الموديلات مبيعاً",
        "حركة موتوسيكل معين"
    ];

    [ObservableProperty] private string _selectedReportType = "مخزون الموتوسيكلات الحالي";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate   = DateTime.Now;
    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked   = true;

    // CarModel filter
    [ObservableProperty] private ObservableCollection<CarModel> _carModels = [];
    [ObservableProperty] private CarModel? _selectedCarModel;

    // Individual car filter (for حركة موتوسيكل معين)
    [ObservableProperty] private ObservableCollection<Car> _cars = [];
    [ObservableProperty] private Car? _selectedCar;
    [ObservableProperty] private string _carDisplayText = "";

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private string? _statusMessage;

    public bool IsDateRangeVisible => SelectedReportType != "مخزون الموتوسيكلات الحالي";
    public bool IsCarModelVisible  => SelectedReportType is "الموتوسيكلات المباعة"
                                                         or "الموتوسيكلات المشتراة"
                                                         or "ربحية الموتوسيكلات"
                                                         or "أعلى الموديلات مبيعاً";
    public bool IsCarVisible       => SelectedReportType == "حركة موتوسيكل معين";

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDateRangeVisible));
        OnPropertyChanged(nameof(IsCarModelVisible));
        OnPropertyChanged(nameof(IsCarVisible));
        ReportData           = new System.Data.DataView();
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
                           CM.ModelName                                   AS [الموديل],
                           C.YearNo                                       AS [السنة],
                           CL.ColorName                                   AS [اللون],
                           C.ChassisNo                                    AS [رقم الشاسيه],
                           C.MotorNo                                      AS [رقم الموتور],
                           C.PlateNo                                      AS [رقم اللوحة],
                           C.Mileage                                      AS [الكيلومتراج],
                           CASE C.StatusId WHEN 1 THEN 'مخزن'
                                           WHEN 2 THEN 'مباع'
                                           WHEN 3 THEN 'صيانة' ELSE 'غير محدد' END AS [الحالة],
                           C.PurchasePrice                                AS [سعر الشراء]
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
                           CB.BrandName + ' - ' + CM.ModelName             AS [الموديل],
                           C.YearNo                                        AS [السنة],
                           C.ChassisNo                                     AS [رقم الشاسيه],
                           C.PlateNo                                       AS [رقم اللوحة],
                           S.Total                                         AS [سعر البيع],
                           C.PurchasePrice                                 AS [سعر الشراء],
                           (S.Total - C.PurchasePrice)                     AS [صافي الربح]
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
                           CB.BrandName + ' - ' + CM.ModelName             AS [الموديل],
                           C.YearNo                                        AS [السنة],
                           C.ChassisNo                                     AS [رقم الشاسيه],
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
                           CB.BrandName + ' - ' + CM.ModelName             AS [الموديل],
                           C.YearNo                                        AS [السنة],
                           C.ChassisNo                                     AS [رقم الشاسيه],
                           CU.CusName                                      AS [العميل],
                           C.PurchasePrice                                 AS [سعر الشراء],
                           S.Total                                         AS [سعر البيع],
                           (S.Total - C.PurchasePrice)                     AS [الربح],
                           CASE WHEN C.PurchasePrice > 0 
                                THEN CAST(ROUND(((S.Total - C.PurchasePrice) / C.PurchasePrice) * 100, 2) AS VARCHAR) + ' %'
                                ELSE '0 %' END                             AS [نسبة الربح]
                    FROM Sales_Car S
                    INNER JOIN Cars      C  ON S.CarID    = C.Car_ID
                    INNER JOIN CarModels CM ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
                    LEFT  JOIN Customers CU ON S.CusID    = CU.Cus_ID
                    WHERE S.SalesDate >= @FromDate AND S.SalesDate <= @ToDate
                    " + modelFilter + @"
                    ORDER BY [الربح] DESC";
            }
            // ── 5. أعلى الموديلات مبيعاً ─────────────────────────────────
            else if (SelectedReportType == "أعلى الموديلات مبيعاً")
            {
                sql = @"
                    SELECT CB.BrandName + ' - ' + CM.ModelName             AS [الموديل],
                           COUNT(S.Sales_ID)                               AS [عدد المبيعات],
                           SUM(S.Total)                                    AS [إجمالي البيع],
                           SUM(C.PurchasePrice)                           AS [إجمالي الشراء],
                           SUM(S.Total - C.PurchasePrice)                 AS [إجمالي الربح]
                    FROM Sales_Car S
                    INNER JOIN Cars      C  ON S.CarID    = C.Car_ID
                    INNER JOIN CarModels CM ON C.ModelID  = CM.Model_ID
                    INNER JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
                    WHERE S.SalesDate >= @FromDate AND S.SalesDate <= @ToDate
                    GROUP BY CB.BrandName, CM.ModelName
                    ORDER BY [عدد المبيعات] DESC";
            }
            // ── 6. حركة موتوسيكل معين ────────────────────────────────────
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
                    -- بيانات الموتوسيكل
                    SELECT 'شراء' AS [النوع],
                           CONVERT(VARCHAR, B.BuyDate, 103) AS [التاريخ],
                           B.Buy_ID AS [رقم العملية],
                           B.OwnerName AS [الطرف الآخر],
                           B.Total AS [المبلغ],
                           B.Notes AS [ملاحظات]
                    FROM Buy_Car B WHERE B.CarID = @CarId

                    UNION ALL

                    SELECT 'بيع',
                           CONVERT(VARCHAR, S.SalesDate, 103),
                           S.Sales_ID,
                           CU.CusName,
                           S.Total,
                           S.Notes
                    FROM Sales_Car S
                    LEFT JOIN Customers CU ON S.CusID = CU.Cus_ID
                    WHERE S.CarID = @CarId

                    ORDER BY [التاريخ] ASC";
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
        else if (SelectedReportType == "أعلى الموديلات مبيعاً")
        {
            var cnt  = await db.ExecuteScalarAsync<int>   ("SELECT COUNT(*) FROM Sales_Car WHERE SalesDate>=@FromDate AND SalesDate<=@ToDate", p);
            var tRev = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(Total),0) FROM Sales_Car WHERE SalesDate>=@FromDate AND SalesDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي الموتوسيكلات المباعة", cnt.ToString()       },
                { "إجمالي الإيرادات",             tRev.ToString("N2") }
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
        if (SelectedCarModel != null) d.Add("الموديل", SelectedCarModel.ModelName);
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
        var dlg      = new Microsoft.Win32.SaveFileDialog
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
