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
        "فواتير استيراد تفصيلي",
        "تفاصيل شحنة (أصناف)",
        "تفاصيل شحنة (موتوسيكلات)",
        "مصروفات الاستيراد",
        "كشف حساب مورد استيراد",
        "كشف حساب تفصيلي مورد استيراد"
    ];

    [ObservableProperty] private string _selectedReportType = "قائمة شحنات الاستيراد";

    [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddMonths(-1);
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
    [ObservableProperty] private ObservableCollection<DetailedAccountRow> _detailedReportData = [];
    [ObservableProperty] private bool _isDetailedReport;
    [ObservableProperty] private bool _isInvoiceMode;

    [ObservableProperty] private string? _statusMessage;

    public bool IsSupplierVisible => SelectedReportType is "قائمة شحنات الاستيراد"
                                                        or "مصروفات الاستيراد"
                                                        or "فواتير استيراد تفصيلي"
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
        ReportData           = new System.Data.DataView();
        DetailedReportData   = [];
        IsDetailedReport     = false;
        IsInvoiceMode        = false;
        StatusMessage        = null;
        _currentFooterTotals = null;
        _currentHeaderInfo   = null;
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

            string sql = "";

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
                    SELECT CB.BrandName + ' - ' + CM.ModelName              AS [الطراز],
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
            // ── 5. كشف حساب مورد استيراد (ملخص -> تفصيلي عادي) ─────────────
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
                    SELECT SortDate,
                           CONVERT(VARCHAR, SortDate, 103) AS [التاريخ], 
                           RefNo AS [رقم الحركة],
                           TransType AS [نوع الحركة],
                           Details AS [البيان],
                           Debit AS [مدين (عليه)],
                           Credit AS [دائن (له)]
                    FROM (
                        -- فواتير الاستيراد (الشحنات)
                        SELECT InvDate AS SortDate, CAST(Inv_ID AS VARCHAR) AS RefNo, N'فاتورة استيراد' AS TransType, ISNULL(InvName, '') AS Details, 
                               0 AS Debit, ISNULL(InvTotal, 0) AS Credit 
                        FROM Import_Invoice WHERE SuppID = @SuppId AND InvDate >= @FromDate AND InvDate <= @ToDate
                        
                        UNION ALL
                        
                        -- مدفوعات المورد
                        SELECT PayDate AS SortDate, CAST(Pay_ID AS VARCHAR) AS RefNo, N'دفعة لمورد' AS TransType, ISNULL(Notes, '') AS Details, 
                               ISNULL(PayMoney, 0) AS Debit, 0 AS Credit
                        FROM Import_Payments WHERE SuppID = @SuppId AND PayDate >= @FromDate AND PayDate <= @ToDate
                    ) T
                    ORDER BY SortDate ASC";
            }
            // ── 6. كشف حساب تفصيلي مورد استيراد ─────────────────────────
            else if (SelectedReportType == "كشف حساب تفصيلي مورد استيراد")
            {
                if (SelectedImportSupplier == null)
                {
                    System.Windows.MessageBox.Show("يرجى اختيار المورد أولاً", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                await GenerateDetailedSupplierStatementAsync(db, p, qFrom, qTo);
                return;
            }

            else if (SelectedReportType == "فواتير استيراد تفصيلي")
            {
                await GenerateDetailedImportInvoicesAsync(db, p, qFrom, qTo);
                return;
            }

            // ── Execute ───────────────────────────────────────────────────
            var dt = new System.Data.DataTable();
            using (var reader = await db.ExecuteReaderAsync(sql, p))
            {
                dt.Load(reader);
            }

            if (SelectedReportType == "كشف حساب مورد استيراد")
            {
                // Add الرصيد columns
                dt.Columns.Add("رصيد مدين", typeof(double));
                dt.Columns.Add("رصيد دائن", typeof(double));

                double runningBalance = 0; // Positive = Debit, Negative = Credit
                
                var opParams = new DynamicParameters();
                opParams.Add("SuppId", SelectedImportSupplier!.SuppId);
                opParams.Add("FromDate", qFrom);

                // Initial Balance from Import_Suppliers table
                var suppInfo = await db.QueryFirstOrDefaultAsync<dynamic>("SELECT ISNULL(Debit, 0) AS Debit, ISNULL(Credit, 0) AS Credit, OpenDate FROM Import_Suppliers WHERE Supp_ID = @SuppId", opParams);
                double suppIniDebit = Convert.ToDouble(suppInfo?.Debit ?? 0);
                double suppIniCredit = Convert.ToDouble(suppInfo?.Credit ?? 0);

                // Previous Transactions before FromDate
                double prevInv = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(InvTotal * OmlaRate), 0) FROM Import_Invoice WHERE SuppID = @SuppId AND InvDate < @FromDate", opParams);
                double prevPay = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney * OmlaRate), 0) FROM Import_Payments WHERE SuppID = @SuppId AND PayDate < @FromDate", opParams);

                double prevDebit = prevPay;
                double prevCredit = prevInv;
                
                if (dt.Columns.Contains("SortDate")) dt.Columns["SortDate"]!.AllowDBNull = true;
                
                int insertedRows = 0;

                string colDebit  = "مدين (عليه)";
                string colCredit = "دائن (له)";

                if (dt.Columns.Contains("نوع الحركة")) dt.Columns["نوع الحركة"]!.AllowDBNull = true;
                if (dt.Columns.Contains("رقم الحركة")) dt.Columns["رقم الحركة"]!.AllowDBNull = true;

                var rowSabiq = dt.NewRow();
                rowSabiq["التاريخ"] = qFrom.ToString("dd/MM/yyyy");
                rowSabiq["نوع الحركة"] = "ما قبله";
                rowSabiq["البيان"] = "الرصيد السابق";
                rowSabiq[colDebit] = prevDebit;
                rowSabiq[colCredit] = prevCredit;
                runningBalance += (prevDebit - prevCredit);

                rowSabiq["رصيد مدين"] = runningBalance > 0 ? runningBalance : 0;
                rowSabiq["رصيد دائن"] = runningBalance < 0 ? Math.Abs(runningBalance) : 0;
                dt.Rows.InsertAt(rowSabiq, insertedRows++);

                // 2. Raseed Iftitahy
                if (suppIniDebit > 0 || suppIniCredit > 0)
                {
                    runningBalance += (suppIniDebit - suppIniCredit);
                    var rowIft = dt.NewRow();
                    rowIft["التاريخ"] = (suppInfo?.OpenDate != null) ? $"{Convert.ToDateTime((object?)suppInfo.OpenDate):dd/MM/yyyy}" : "";
                    rowIft["نوع الحركة"] = "افتتاحي";
                    rowIft["البيان"] = "الرصيد الافتتاحي";
                    rowIft[colDebit] = suppIniDebit;
                    rowIft[colCredit] = suppIniCredit;

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
        else if (SelectedReportType == "كشف حساب مورد استيراد")
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

            _currentHeaderInfo = new Dictionary<string, string>
            {
                { "اسم المورد", SelectedImportSupplier?.SuppName ?? "" },
                { "الفترة من", IsFromDateChecked ? qFrom.ToString("yyyy/MM/dd") : "بداية التعامل" },
                { "الفترة إلى", IsToDateChecked ? qTo.ToString("yyyy/MM/dd") : "حتى الآن" }
            };

            _currentFooterTotals = new Dictionary<string, string>
            {
                { "إجمالي مدين", sumDebit.ToString("N2") },
                { "إجمالي دائن", sumCredit.ToString("N2") },
                { "الرصيد", finalBal >= 0 ? finalBal.ToString("N2") + " (مدين)" : Math.Abs(finalBal).ToString("N2") + " (دائن)" }
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

        try
        {
            QuestPDF.Infrastructure.IDocument document;
            if (IsDetailedReport)
            {
                if (SelectedReportType == "فواتير استيراد تفصيلي")
                    document = MotorBike.Services.ReportGenerator.CreateImportInvoiceDetailedPdfDocument(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals);
                else if (IsInvoiceMode)
                    document = MotorBike.Services.ReportGenerator.CreateInvoiceDetailedPdfDocument(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals);
                else
                    document = MotorBike.Services.ReportGenerator.CreateDetailedPdfDocument(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals);
            }
            else
            {
                document = MotorBike.Services.ReportGenerator.CreatePdfDocument(company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
            }

            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, SelectedReportType);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء التصدير: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
            QuestPDF.Infrastructure.IDocument document;
            if (IsDetailedReport)
            {
                if (SelectedReportType == "فواتير استيراد تفصيلي")
                    document = MotorBike.Services.ReportGenerator.CreateImportInvoiceDetailedPdfDocument(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals);
                else if (IsInvoiceMode)
                    document = MotorBike.Services.ReportGenerator.CreateInvoiceDetailedPdfDocument(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals);
                else
                    document = MotorBike.Services.ReportGenerator.CreateDetailedPdfDocument(company, SelectedReportType, DetailedReportData, _currentHeaderInfo, _currentFooterTotals);
            }
            else
            {
                document = MotorBike.Services.ReportGenerator.CreatePdfDocument(company, SelectedReportType, ReportData, _currentHeaderInfo, _currentFooterTotals);
            }

            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, SelectedReportType);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task GenerateDetailedImportInvoicesAsync(System.Data.IDbConnection db, DynamicParameters p, DateTime queryFromDate, DateTime queryToDate)
    {
        string suppFilter = "";
        if (SelectedImportSupplier != null)
        {
            suppFilter = " AND I.SuppID = @SuppId ";
            p.Add("SuppId", SelectedImportSupplier.SuppId);
        }

        var invoices = await db.QueryAsync<dynamic>($@"
            SELECT I.Inv_ID, I.InvName, I.InvDate, 
                   I.InvType, I.InvTotal, I.OmlaRate, I.ExpTotal, ISNULL(I.TotalCost, 0) AS TotalCost,
                   S.SuppName
            FROM Import_Invoice I
            LEFT JOIN Import_Suppliers S ON I.SuppID = S.Supp_ID
            WHERE I.InvDate >= @FromDate AND I.InvDate <= @ToDate {suppFilter}
            ORDER BY I.InvDate ASC", p);

        var rows = new List<DetailedAccountRow>();
        double sumCost = 0;

        foreach (var inv in invoices)
        {
            int invId = Convert.ToInt32(inv.Inv_ID);
            double omlaRate = Convert.ToDouble(inv.OmlaRate);
            double actCost = Convert.ToDouble(inv.TotalCost);
            sumCost += actCost;

            DateTime txDate = Convert.ToDateTime((object)inv.InvDate);

            var row = new DetailedAccountRow
            {
                RawDate = txDate,
                Date = txDate.ToString("dd/MM/yyyy"),
                RefNo = inv.Inv_ID.ToString(),
                TransType = "شحنة استيراد",
                Branch = inv.InvName ?? "",
                CustomerName = inv.SuppName ?? "",
                InvoiceNet = Convert.ToDouble(inv.InvTotal),
                VatTaxDisplay = omlaRate.ToString("N2"),
                InvoiceDisc = Convert.ToDouble(inv.InvTotal) * omlaRate,
                InvoiceAdd = Convert.ToDouble(inv.ExpTotal),
                InvoiceTotal = actCost,
                Items = new List<InvoiceSubItem>()
            };

            // الأصناف
            var subItems = await db.QueryAsync<dynamic>(@"
                SELECT I.ItemName, ISNULL(II.Qty, 0) AS Qty, ISNULL(II.Price, 0) AS Price, ISNULL(II.Total, 0) AS Total 
                FROM Import_Inv_Item II
                JOIN Items I ON II.ItemID = I.Item_ID
                WHERE II.InvID = @InvId", new { InvId = invId });

            // الموتوسيكلات
            var subCars = await db.QueryAsync<dynamic>(@"
                SELECT ISNULL(CB.BrandName, '') + ' - ' + ISNULL(CM.ModelName, '') AS ItemName,
                       ISNULL(C.ChassisNo, '') AS ChassisNo, ISNULL(IC.Total, 0) AS Total 
                FROM Import_Inv_Car IC
                JOIN Cars C ON IC.CarID = C.Car_ID
                LEFT JOIN CarModels CM ON C.ModelID = CM.Model_ID
                LEFT JOIN CarBrands CB ON CM.BrandID = CB.Brand_ID
                WHERE IC.InvID = @InvId", new { InvId = invId });

            foreach(var item in subItems) {
                row.Items.Add(new InvoiceSubItem {
                    ItemName = item.ItemName,
                    Qty = Convert.ToDouble(item.Qty),
                    Price = Convert.ToDouble(item.Price),
                    Total = Convert.ToDouble(item.Total)
                });
            }
            foreach(var car in subCars) {
                row.Items.Add(new InvoiceSubItem {
                    ItemName = car.ItemName + (string.IsNullOrEmpty(car.ChassisNo) ? "" : $" (شاسيه: {car.ChassisNo})"),
                    Qty = 1,
                    Total = Convert.ToDouble(car.Total)
                });
            }

            rows.Add(row);
        }

        DetailedReportData = new ObservableCollection<DetailedAccountRow>(rows);
        IsDetailedReport = true;
        IsInvoiceMode = true;

        _currentHeaderInfo = new Dictionary<string, string> {
            { "الفترة من", IsFromDateChecked ? queryFromDate.ToString("yyyy/MM/dd") : "بداية التعامل" },
            { "الفترة إلى", IsToDateChecked  ? queryToDate.ToString("yyyy/MM/dd")  : "حتى الآن" }
        };
        if (SelectedImportSupplier != null) _currentHeaderInfo.Add("المورد", SelectedImportSupplier.SuppName);

        _currentFooterTotals = new Dictionary<string, string> {
            { "إجمالي الشحنات", rows.Count.ToString() },
            { "إجمالي التكلفة الكلية", sumCost.ToString("N2") }
        };

        StatusMessage = rows.Count > 0
            ? $"تم العثور على {rows.Count} شحنة تفصيلية"
            : "⚠️ لا توجد شحنات في الفترة المحددة";
    }

    private async Task GenerateDetailedSupplierStatementAsync(
        System.Data.IDbConnection db,
        DynamicParameters p,
        DateTime queryFromDate,
        DateTime queryToDate)
    {
        int suppId = SelectedImportSupplier!.SuppId;

        var opParams = new DynamicParameters();
        opParams.Add("SuppId", suppId);
        opParams.Add("FromDate", queryFromDate);

        // الرصيد الافتتاحي
        var suppInfo = await db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT ISNULL(Debit,0) AS Debit, ISNULL(Credit,0) AS Credit, OpenDate FROM Import_Suppliers WHERE Supp_ID=@SuppId", opParams);

        double suppIniDebit  = Convert.ToDouble(suppInfo?.Debit  ?? 0);
        double suppIniCredit = Convert.ToDouble(suppInfo?.Credit ?? 0);

        // ما قبله
        double prevInv = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(InvTotal),0) FROM Import_Invoice WHERE SuppID=@SuppId AND InvDate<@FromDate", opParams);
        double prevPay = await db.ExecuteScalarAsync<double>("SELECT ISNULL(SUM(PayMoney),0) FROM Import_Payments WHERE SuppID=@SuppId AND PayDate<@FromDate", opParams);

        double prevDebit  = prevPay;
        double prevCredit = prevInv;

        double runBal = 0;
        var rows = new List<DetailedAccountRow>();

        // 1. الرصيد السابق
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

        // 2. الرصيد الافتتاحي
        if (suppIniDebit > 0 || suppIniCredit > 0)
        {
            runBal += suppIniDebit - suppIniCredit;
            DateTime rawIftDate = (suppInfo?.OpenDate != null) ? Convert.ToDateTime((object?)suppInfo.OpenDate) : queryFromDate;
            rows.Add(new DetailedAccountRow {
                RawDate = rawIftDate,
                Date = rawIftDate.ToString("dd/MM/yyyy"),
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
        txParams.Add("ToDate", queryToDate);

        // 3. فواتير الاستيراد (الشحنات)
        var invoices = await db.QueryAsync<dynamic>(@"
            SELECT Inv_ID AS Id, InvDate AS TxDate, CAST(Inv_ID AS VARCHAR) AS RefNo,
                   N'فاتورة استيراد' AS TransType, ISNULL(InvName,'') AS Notes,
                   InvTotal AS Credit, OmlaRate
            FROM Import_Invoice
            WHERE SuppID=@SuppId AND InvDate>=@FromDate AND InvDate<=@ToDate", txParams);

        foreach (var inv in invoices)
        {
            int invId = Convert.ToInt32(inv.Id);
            double omlaRate = Convert.ToDouble(inv.OmlaRate);
            
            // الأصناف
            var subItems = await db.QueryAsync<dynamic>(@"
                SELECT I.ItemName, ISNULL(II.Qty, 0) AS Qty, ISNULL(II.Price, 0) AS Price, ISNULL(II.Total, 0) AS Total 
                FROM Import_Inv_Item II
                JOIN Items I ON II.ItemID=I.Item_ID
                WHERE II.InvID=@InvId", new { InvId = invId });

            // الموتوسيكلات
            var subCars = await db.QueryAsync<dynamic>(@"
                SELECT ISNULL(CB.BrandName, '') + ' - ' + ISNULL(CM.ModelName, '') AS ItemName,
                       ISNULL(C.ChassisNo, '') AS ChassisNo, ISNULL(C.MotorNo, '') AS MotorNo, ISNULL(IC.Total, 0) AS Total 
                FROM Import_Inv_Car IC
                JOIN Cars C ON IC.CarID=C.Car_ID
                LEFT JOIN CarModels CM ON C.ModelID=CM.Model_ID
                LEFT JOIN CarBrands CB ON CM.BrandID=CB.Brand_ID
                WHERE IC.InvID=@InvId", new { InvId = invId });

            var itemsList = new List<InvoiceSubItem>();
            foreach(var item in subItems) {
                itemsList.Add(new InvoiceSubItem {
                    ItemName = item.ItemName,
                    Qty = Convert.ToDouble(item.Qty),
                    Price = Convert.ToDouble(item.Price), // foreign currency as-is
                    Total = Convert.ToDouble(item.Total)
                });
            }
            foreach(var car in subCars) {
                itemsList.Add(new InvoiceSubItem {
                    ItemName = car.ItemName + (string.IsNullOrEmpty(car.ChassisNo) ? "" : $" (شاسيه: {car.ChassisNo})"),
                    Qty = 1,
                    Total = Convert.ToDouble(car.Total)
                });
            }

            double c = Convert.ToDouble(inv.Credit);
            runBal -= c; // Invoice adds to the supplier's credit -> decreases our balance vs them (more debt)

            DateTime txDate = Convert.ToDateTime((object)inv.TxDate);
            rows.Add(new DetailedAccountRow {
                RawDate = txDate,
                Date = txDate.ToString("dd/MM/yyyy"),
                RefNo = inv.RefNo,
                TransType = inv.TransType, Notes = inv.Notes,
                Debit = 0, Credit = c,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0,
                Items = itemsList
            });

            // مدفوعات مرتبطة بالشحنة
            var invPays = await db.QueryAsync<dynamic>(
                "SELECT CAST(Pay_ID AS VARCHAR) AS RefNo, PayDate, PayMoney AS Debit, ISNULL(Notes,'') AS Notes FROM Import_Payments WHERE InvId=@InvId", new { InvId = invId });
            foreach (var p_item in invPays)
            {
                double d = Convert.ToDouble(p_item.Debit);
                runBal += d; // payment adds to debit
                DateTime pDate = Convert.ToDateTime((object)p_item.PayDate);
                rows.Add(new DetailedAccountRow {
                    RawDate = pDate.AddSeconds(1),
                    Date = pDate.ToString("dd/MM/yyyy"),
                    RefNo = p_item.RefNo, TransType = "دفعة للشحنة", Notes = p_item.Notes,
                    Debit = d, Credit = 0,
                    RunningDebit  = runBal > 0 ? runBal : 0,
                    RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
                });
            }
        }

        // 4. مدفوعات أخرى (غير مرتبطة بشحنة محددة)
        var otherPays = await db.QueryAsync<dynamic>(@"
            SELECT PayDate, CAST(Pay_ID AS VARCHAR) AS RefNo, N'دفعة لمورد' AS TransType, ISNULL(Notes,'') AS Notes,
                   PayMoney AS Debit
            FROM Import_Payments
            WHERE SuppID=@SuppId AND PayDate>=@FromDate AND PayDate<=@ToDate AND InvId IS NULL", txParams);

        foreach (var op in otherPays)
        {
            double d = Convert.ToDouble(op.Debit);
            runBal += d;
            DateTime pDate = Convert.ToDateTime((object)op.PayDate);
            rows.Add(new DetailedAccountRow {
                RawDate = pDate,
                Date = pDate.ToString("dd/MM/yyyy"),
                RefNo = op.RefNo, TransType = op.TransType, Notes = op.Notes,
                Debit = d, Credit = 0,
                RunningDebit  = runBal > 0 ? runBal : 0,
                RunningCredit = runBal < 0 ? Math.Abs(runBal) : 0
            });
        }

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
            { "اسم المورد", SelectedImportSupplier.SuppName },
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
    [RelayCommand]
    private void ClearImportSupplier() => SelectedImportSupplier = null;

    [RelayCommand]
    private void ClearInvoice() => SelectedInvoice = null;
}
