using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ImportReportsViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;

    [ObservableProperty]
    private ObservableCollection<string> _reportTypes =
    [
        "قائمة شحنات الاستيراد",
        "تفاصيل شحنة (أصناف)",
        "تفاصيل شحنة (موتوسيكلات)",
        "مصروفات الاستيراد",
        "كشف حساب مورد استيراد",
        "كشف حساب تفصيلي مورد استيراد"
    ];

    [ObservableProperty] private string _selectedReportType = "قائمة شحنات الاستيراد";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-6);
    [ObservableProperty] private DateTime _toDate   = DateTime.Now;
    [ObservableProperty] private bool _isFromDateChecked = true;
    [ObservableProperty] private bool _isToDateChecked   = true;

    // Import Supplier filter
    [ObservableProperty] private ObservableCollection<ImportSupplier> _importSuppliers = [];
    [ObservableProperty] private ImportSupplier? _selectedImportSupplier;

    // Invoice filter (for تفاصيل شحنة)
    [ObservableProperty] private ObservableCollection<ImportInvoice> _invoices = [];
    [ObservableProperty] private ImportInvoice? _selectedInvoice;

    [ObservableProperty] private System.Data.DataView _reportData = new System.Data.DataView();
    [ObservableProperty] private string? _statusMessage;

    public bool IsSupplierVisible => SelectedReportType is "قائمة شحنات الاستيراد"
                                                        or "مصروفات الاستيراد"
                                                        or "كشف حساب مورد استيراد"
                                                        or "كشف حساب تفصيلي مورد استيراد";

    public bool IsInvoiceVisible  => SelectedReportType is "تفاصيل شحنة (أصناف)"
                                                        or "تفاصيل شحنة (موتوسيكلات)"
                                                        or "مصروفات الاستيراد";

    public bool IsSupplierRequired => SelectedReportType is "كشف حساب مورد استيراد"
                                                         or "كشف حساب تفصيلي مورد استيراد";

    partial void OnSelectedReportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSupplierVisible));
        OnPropertyChanged(nameof(IsInvoiceVisible));
        OnPropertyChanged(nameof(IsSupplierRequired));
        ReportData    = new System.Data.DataView();
        StatusMessage = null;
    }

    private Dictionary<string, string>? _currentHeaderInfo;
    private Dictionary<string, string>? _currentFooterTotals;

    public ImportReportsViewModel(
        IDbConnectionFactory               dbFactory,
        IRepository<ImportSupplier>        suppRepo,
        IRepository<ImportInvoice>         invRepo)
    {
        _dbFactory = dbFactory;
        LoadLookupsAsync(suppRepo, invRepo).ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(
        IRepository<ImportSupplier> suppRepo,
        IRepository<ImportInvoice>  invRepo)
    {
        ImportSuppliers = new ObservableCollection<ImportSupplier>(await suppRepo.GetAllAsync());
        Invoices        = new ObservableCollection<ImportInvoice> (await invRepo.GetAllAsync());
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

            // ── 1. قائمة شحنات الاستيراد ──────────────────────────────────
            if (SelectedReportType == "قائمة شحنات الاستيراد")
            {
                string suppFilter = "";
                if (SelectedImportSupplier != null)
                {
                    suppFilter = " AND I.SuppID = @SuppId ";
                    p.Add("SuppId", SelectedImportSupplier.SuppId);
                }

                sql = @"
                    SELECT CONVERT(VARCHAR, I.InvDate, 103) AS [التاريخ],
                           I.Inv_ID                          AS [رقم الشحنة],
                           I.InvName                         AS [اسم الشحنة],
                           S.SuppName                        AS [المورد],
                           I.MadeIn                          AS [بلد المنشأ],
                           CASE I.InvType WHEN 1 THEN 'أصناف' WHEN 2 THEN 'موتوسيكلات' ELSE 'مختلط' END AS [نوع الشحنة],
                           I.InvTotal                        AS [قيمة الفاتورة بالعملة الأجنبية],
                           I.OmlaRate                        AS [سعر العملة],
                           (I.InvTotal * I.OmlaRate)         AS [قيمة الفاتورة بالجنيه],
                           I.ExpTotal                        AS [إجمالي المصروفات],
                           ISNULL(I.TotalCost, 0)            AS [التكلفة الإجمالية]
                    FROM Import_Invoice I
                    LEFT JOIN Import_Suppliers S ON I.SuppID = S.Supp_ID
                    WHERE I.InvDate >= @FromDate AND I.InvDate <= @ToDate
                    " + suppFilter + @"
                    ORDER BY I.InvDate DESC";
            }
            // ── 2. تفاصيل شحنة (أصناف) ───────────────────────────────────
            else if (SelectedReportType == "تفاصيل شحنة (أصناف)")
            {
                if (SelectedInvoice == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار الشحنة أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("InvId", SelectedInvoice.InvId);

                sql = @"
                    SELECT I.ItemName                                        AS [الصنف],
                           II.Qty                                            AS [الكمية],
                           II.Price                                          AS [سعر الوحدة (بالعملة)],
                           II.Total                                          AS [الإجمالي (بالعملة)],
                           (II.Total * INV.OmlaRate)                        AS [الإجمالي (بالجنيه)],
                           ISNULL(II.CostUnit, 0)                           AS [تكلفة الوحدة (بالجنيه)]
                    FROM Import_Inv_Item II
                    INNER JOIN Items           I   ON II.ItemID = I.Item_ID
                    INNER JOIN Import_Invoice  INV ON II.InvID  = INV.Inv_ID
                    WHERE II.InvID = @InvId
                    ORDER BY I.ItemName";
            }
            // ── 3. تفاصيل شحنة (موتوسيكلات) ──────────────────────────────
            else if (SelectedReportType == "تفاصيل شحنة (موتوسيكلات)")
            {
                if (SelectedInvoice == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار الشحنة أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("InvId", SelectedInvoice.InvId);

                sql = @"
                    SELECT CB.BrandName + ' - ' + CM.ModelName              AS [الموديل],
                           C.YearNo                                         AS [السنة],
                           C.ChassisNo                                      AS [الشاسيه],
                           C.MotorNo                                        AS [الموتور],
                           IC.Total                                         AS [القيمة (بالعملة)],
                           (IC.Total * INV.OmlaRate)                       AS [القيمة (بالجنيه)],
                           ISNULL(IC.CostTotal, 0)                         AS [التكلفة الإجمالية (بالجنيه)]
                    FROM Import_Inv_Car IC
                    INNER JOIN Import_Invoice INV ON IC.InvID   = INV.Inv_ID
                    LEFT  JOIN Cars           C   ON IC.CarID   = C.Car_ID
                    LEFT  JOIN CarModels      CM  ON C.ModelID  = CM.Model_ID
                    LEFT  JOIN CarBrands      CB  ON CM.BrandID = CB.Brand_ID
                    WHERE IC.InvID = @InvId";
            }
            // ── 4. مصروفات الاستيراد ──────────────────────────────────────
            else if (SelectedReportType == "مصروفات الاستيراد")
            {
                string invFilter = "";
                if (SelectedInvoice != null) { invFilter = " AND E.InvID = @InvId "; p.Add("InvId", SelectedInvoice.InvId); }

                string suppFilter = "";
                if (SelectedImportSupplier != null) { suppFilter = " AND INV.SuppID = @SuppId "; p.Add("SuppId", SelectedImportSupplier.SuppId); }

                sql = @"
                    SELECT CONVERT(VARCHAR, E.PayDate, 103)                 AS [التاريخ],
                           INV.InvName                                      AS [الشحنة],
                           IE.ExpName                                       AS [نوع المصروف],
                           E.PayTotal                                       AS [المبلغ بالعملة الأجنبية],
                           E.OmlaRate                                       AS [سعر العملة],
                           (E.PayTotal * E.OmlaRate)                       AS [المبلغ بالجنيه],
                           E.Notes                                          AS [ملاحظات]
                    FROM Import_Exp E
                    INNER JOIN Import_Invoice   INV ON E.InvID  = INV.Inv_ID
                    INNER JOIN Import_Expenses  IE  ON E.ExpID  = IE.Exp_ID
                    WHERE E.PayDate >= @FromDate AND E.PayDate <= @ToDate
                    " + invFilter + suppFilter + @"
                    ORDER BY E.PayDate DESC";
            }
            // ── 5. كشف حساب مورد استيراد (ملخص) ─────────────────────────
            else if (SelectedReportType == "كشف حساب مورد استيراد")
            {
                if (SelectedImportSupplier == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار المورد أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("SuppId", SelectedImportSupplier.SuppId);

                sql = @"
                    SELECT 'إجمالي فواتير الاستيراد' AS [البيان],
                           ISNULL(SUM(InvTotal * OmlaRate), 0) AS [مدين],
                           0 AS [دائن]
                    FROM Import_Invoice
                    WHERE SuppID = @SuppId AND InvDate >= @FromDate AND InvDate <= @ToDate

                    UNION ALL

                    SELECT 'إجمالي المدفوعات',
                           0,
                           ISNULL(SUM(PayMoney * OmlaRate), 0)
                    FROM Import_Payments
                    WHERE SuppID = @SuppId AND PayDate >= @FromDate AND PayDate <= @ToDate";
            }
            // ── 6. كشف حساب تفصيلي مورد استيراد ─────────────────────────
            else
            {
                if (SelectedImportSupplier == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار المورد أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                p.Add("SuppId", SelectedImportSupplier.SuppId);

                sql = @"
                    -- الفواتير
                    SELECT InvDate AS [SortDate],
                           CONVERT(VARCHAR, InvDate, 103)                   AS [التاريخ],
                           'شحنة رقم ' + CAST(Inv_ID AS VARCHAR) + ' - ' + InvName AS [البيان],
                           (InvTotal * OmlaRate)                           AS [مدين],
                           0                                               AS [دائن]
                    FROM Import_Invoice
                    WHERE SuppID = @SuppId AND InvDate >= @FromDate AND InvDate <= @ToDate

                    UNION ALL

                    -- المدفوعات
                    SELECT PayDate,
                           CONVERT(VARCHAR, PayDate, 103),
                           'دفعة - ' + ISNULL(Notes, ''),
                           0,
                           (PayMoney * OmlaRate)
                    FROM Import_Payments
                    WHERE SuppID = @SuppId AND PayDate >= @FromDate AND PayDate <= @ToDate

                    ORDER BY SortDate ASC";
            }

            // ── Execute ───────────────────────────────────────────────────
            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, p))
                dt.Load(reader);

            // Running balance for كشف تفصيلي
            if (SelectedReportType == "كشف حساب تفصيلي مورد استيراد")
            {
                dt.Columns.Add("الرصيد", typeof(double));

                // Opening balance
                var opParams = new DynamicParameters();
                opParams.Add("SuppId",   SelectedImportSupplier!.SuppId);
                opParams.Add("FromDate", qFrom);
                var opInv = await db.ExecuteScalarAsync<double>(
                    "SELECT ISNULL(SUM(InvTotal*OmlaRate),0) FROM Import_Invoice WHERE SuppID=@SuppId AND InvDate<@FromDate", opParams);
                var opPay = await db.ExecuteScalarAsync<double>(
                    "SELECT ISNULL(SUM(PayMoney*OmlaRate),0) FROM Import_Payments WHERE SuppID=@SuppId AND PayDate<@FromDate", opParams);

                double runBal = opInv - opPay;

                if (dt.Columns.Contains("SortDate")) dt.Columns["SortDate"]!.AllowDBNull = true;
                var opRow = dt.NewRow();
                opRow["التاريخ"] = qFrom.ToString("dd/MM/yyyy");
                opRow["البيان"]  = "رصيد سابق";
                opRow["مدين"]    = 0;
                opRow["دائن"]    = 0;
                opRow["الرصيد"]  = runBal;
                dt.Rows.InsertAt(opRow, 0);

                for (int i = 1; i < dt.Rows.Count; i++)
                {
                    double d = Convert.ToDouble(dt.Rows[i]["مدين"] == DBNull.Value ? 0 : dt.Rows[i]["مدين"]);
                    double c = Convert.ToDouble(dt.Rows[i]["دائن"] == DBNull.Value ? 0 : dt.Rows[i]["دائن"]);
                    runBal += (d - c);
                    dt.Rows[i]["الرصيد"] = runBal;
                }
                if (dt.Columns.Contains("SortDate")) dt.Columns.Remove("SortDate");
            }

            ReportData = dt.DefaultView;
            await BuildFooterTotalsAsync(db, p, qFrom, qTo, dt);

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
        System.Data.IDbConnection db, DynamicParameters p,
        DateTime qFrom, DateTime qTo, System.Data.DataTable dt)
    {
        if (SelectedReportType == "قائمة شحنات الاستيراد")
        {
            var cnt  = await db.ExecuteScalarAsync<int>   ("SELECT COUNT(*) FROM Import_Invoice WHERE InvDate>=@FromDate AND InvDate<=@ToDate", p);
            var tInv = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(InvTotal*OmlaRate),0) FROM Import_Invoice WHERE InvDate>=@FromDate AND InvDate<=@ToDate", p);
            var tExp = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(ExpTotal),0) FROM Import_Invoice WHERE InvDate>=@FromDate AND InvDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "عدد الشحنات",            cnt.ToString()        },
                { "إجمالي قيمة الفواتير",   tInv.ToString("N2")   },
                { "إجمالي المصروفات",        tExp.ToString("N2")   },
                { "التكلفة الإجمالية",       (tInv + tExp).ToString("N2") }
            };
        }
        else if (SelectedReportType is "كشف حساب مورد استيراد" or "كشف حساب تفصيلي مورد استيراد")
        {
            var sp = new DynamicParameters();
            sp.Add("SuppId",   SelectedImportSupplier!.SuppId);
            sp.Add("FromDate", qFrom);
            sp.Add("ToDate",   qTo);
            var tInv = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(InvTotal*OmlaRate),0) FROM Import_Invoice  WHERE SuppID=@SuppId AND InvDate>=@FromDate AND InvDate<=@ToDate", sp);
            var tPay = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney*OmlaRate),0) FROM Import_Payments WHERE SuppID=@SuppId AND PayDate>=@FromDate AND PayDate<=@ToDate", sp);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي الفواتير",    tInv.ToString("N2")           },
                { "إجمالي المدفوعات",   tPay.ToString("N2")           },
                { "الرصيد المتبقي",     (tInv - tPay).ToString("N2")  }
            };
        }
        else if (SelectedReportType == "مصروفات الاستيراد")
        {
            var tExp = await db.ExecuteScalarAsync<double>(
                "SELECT ISNULL(SUM(PayTotal*OmlaRate),0) FROM Import_Exp WHERE PayDate>=@FromDate AND PayDate<=@ToDate", p);
            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي المصروفات (بالجنيه)", tExp.ToString("N2") }
            };
        }
    }

    private Dictionary<string, string> BuildHeaderInfo(DateTime from, DateTime to)
    {
        var d = new Dictionary<string, string>
        {
            { "من تاريخ",  from.ToString("dd/MM/yyyy") },
            { "إلى تاريخ", to.ToString("dd/MM/yyyy")   }
        };
        if (SelectedImportSupplier != null) d.Add("المورد",  SelectedImportSupplier.SuppName);
        if (SelectedInvoice        != null) d.Add("الشحنة",  SelectedInvoice.InvName);
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
            Filter   = "PDF File (*.pdf)|*.pdf", DefaultExt = "pdf",
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
