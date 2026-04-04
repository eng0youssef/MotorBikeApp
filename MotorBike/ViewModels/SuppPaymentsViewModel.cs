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

public partial class SuppPaymentsViewModel : LookupViewModelBase<SuppPayment>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<Supplier> _supplierRepo;
    private readonly IRepository<Cash> _cashRepo;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];

    public ObservableCollection<KeyValuePair<byte, string>> PayTypes { get; } =
    [
        new(0, "سداد لمورد"),
        new(1, "تحصيل من مورد"),
    ];

    // ── لحفظ القيم القديمة عند التعديل ──
    private int? _oldSuppId;
    private int? _oldCashId;

    public SuppPaymentsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<SuppPayment> repository,
        IRepository<Supplier> supplierRepo,
        IRepository<Cash> cashRepo,
        CompositeKeyRepository compositeRepo) : base(repository)
    {
        _dbFactory = dbFactory;
        _supplierRepo = supplierRepo;
        _cashRepo = cashRepo;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var suppliers = await _supplierRepo.GetAllAsync();
            Suppliers = new ObservableCollection<Supplier>(suppliers.Where(x => x.Active));

            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(SuppPayment entity) => entity.PayId;
    protected override bool IsNewRecord(SuppPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(SuppPayment entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(SuppPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;
        entity.PayType = 0; // Default: سداد لمورد

        if (Suppliers.Any()) entity.SuppId = Suppliers.First().SuppId;
        if (CashList.Any()) entity.CashId = CashList.First().CashId;
    }

    // ── Balance Recalculation Hooks ─────────────────────────────────

    protected override async Task BeforeSaveAsync(bool isInsert)
    {
        if (!isInsert && FormItem != null)
        {
            // جلب القيم القديمة من قاعدة البيانات قبل التعديل
            using var db = _dbFactory.CreateConnection();
            var old = await db.QueryFirstOrDefaultAsync<SuppPayment>(
                "SELECT SuppID, CashID FROM Supp_Payments WHERE Pay_ID = @PayId",
                new { FormItem.PayId });

            if (old != null)
            {
                _oldSuppId = old.SuppId;
                _oldCashId = old.CashId;
            }
        }
        else
        {
            _oldSuppId = null;
            _oldCashId = null;
        }
    }

    protected override async Task AfterSaveAsync(bool wasInsert)
    {
        if (FormItem == null) return;

        // إعادة حساب رصيد المورد القديم لو اتغير
        if (_oldSuppId.HasValue && _oldSuppId.Value != FormItem.SuppId)
            await _compositeRepo.RecalcBalanceForSupplierAsync(_oldSuppId.Value);

        // إعادة حساب رصيد المورد الحالي
        await _compositeRepo.RecalcBalanceForSupplierAsync(FormItem.SuppId);

        // إعادة حساب رصيد الخزينة القديمة لو اتغيرت
        if (_oldCashId.HasValue && _oldCashId.Value != (FormItem.CashId ?? 0) && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);

        // إعادة حساب رصيد الخزينة الحالية
        if (FormItem.CashId.HasValue && FormItem.CashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(FormItem.CashId.Value);
    }

    protected override Task BeforeDeleteAsync()
    {
        // حفظ بيانات السجل المحدد قبل الحذف
        if (SelectedItem != null)
        {
            _oldSuppId = SelectedItem.SuppId;
            _oldCashId = SelectedItem.CashId;
        }
        return Task.CompletedTask;
    }

    protected override async Task AfterDeleteAsync()
    {
        // إعادة حساب رصيد المورد بعد الحذف
        if (_oldSuppId.HasValue)
            await _compositeRepo.RecalcBalanceForSupplierAsync(_oldSuppId.Value);

        // إعادة حساب رصيد الخزينة بعد الحذف
        if (_oldCashId.HasValue && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);
    }

    [RelayCommand]
    private async Task PrintReceiptAsync()
    {
        if (FormItem == null || FormItem.PayId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ الإيصال أولاً لطباعته.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
            double previousBalance = await _compositeRepo.GetSupplierOldBalanceAsync(FormItem.SuppId, FormItem.PayDate);
            
            // For supplier: PayType 0 (سداد للمورد) decreases our debt (-), 
            // PayType 1 (استلام من المورد) increases our debt (+).
            // Convention: PayType 0 = Paid (-), PayType 1 = Collected (+)
            double amount = FormItem.PayMoney;
            double balanceAfter = previousBalance + (FormItem.PayType == 0 ? -amount : amount);

            var model = new MotorBike.Services.SuppPaymentReceiptModel
            {
                ReceiptNo = FormItem.PayId.ToString(),
                IssueDate = FormItem.PayDate.ToString("yyyy-MM-dd"),
                SupplierName = Suppliers.FirstOrDefault(s => s.SuppId == FormItem.SuppId)?.SuppName ?? "",
                CashName = CashList.FirstOrDefault(c => c.CashId == FormItem.CashId)?.CashName ?? "",
                PayTypeName = PayTypes.FirstOrDefault(t => t.Key == FormItem.PayType).Value ?? "إيصال",
                Amount = amount,
                Notes = FormItem.Notes ?? "",
                PreviousBalance = previousBalance,
                BalanceAfter = balanceAfter
            };

            var document = new MotorBike.Services.SuppPaymentReceiptDocument(model, company);
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                Title = "حفظ الإيصال كـ PDF",
                FileName = $"إيصال_مورد_{FormItem.PayId}_{DateTime.Now:yyyyMMdd}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                QuestPDF.Fluent.GenerateExtensions.GeneratePdf(document, saveFileDialog.FileName);
                var result = System.Windows.MessageBox.Show("تم حفظ الإيصال بنجاح. هل تريد فتح الملف الآن لطباعته؟", "حفظ وطباعة", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try { var process = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo { FileName = saveFileDialog.FileName, UseShellExecute = true } }; process.Start(); }
                    catch (Exception exInner) { System.Windows.MessageBox.Show("لا يمكن فتح الملف تلقائياً.\nالخطأ: " + exInner.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); }
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
