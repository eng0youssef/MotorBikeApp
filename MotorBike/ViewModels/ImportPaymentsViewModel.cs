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

public partial class ImportPaymentsViewModel : LookupViewModelBase<ImportPayment>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<ImportSupplier> _supplierRepo;
    private readonly IRepository<Cash> _cashRepo;
    private readonly IRepository<Omla> _omlaRepo;
    private readonly IRepository<ImportInvoice> _invoiceRepo;
    private readonly CompositeKeyRepository _compositeRepo;

    private List<Cash> _allCash = [];

    [ObservableProperty] private ObservableCollection<ImportSupplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];
    [ObservableProperty] private ObservableCollection<Omla> _omlas = [];
    [ObservableProperty] private ObservableCollection<ImportInvoice> _invoices = [];
    [ObservableProperty] private double _currentSupplierBalance;
    [ObservableProperty] private double _currentCashBalance;

    public int? SelectedCashId
    {
        get => FormItem?.CashId;
        set
        {
            if (FormItem != null && FormItem.CashId != value)
            {
                FormItem.CashId = value ?? 0;
                OnPropertyChanged(nameof(SelectedCashId));
                CurrentCashBalance = CashList.FirstOrDefault(c => c.CashId == value)?.Bal ?? 0;
            }
        }
    }


    // Fields for tracking old values
    private int? _oldSuppId;
    private int? _oldCashId;

    [ObservableProperty] private ImportSupplier? _selectedSupplier;

    partial void OnSelectedSupplierChanged(ImportSupplier? value)
    {
        if (value != null)
        {
            FormItem.SuppId = value.SuppId;
            FormItem.OmlaId = value.OmlaId;
            FormItem.OmlaRate = value.OmlaRate;
            FilterCashByOmla(value.OmlaId);
            CurrentSupplierBalance = value.Bal ?? 0;
        }
        else
        {
            CashList = new ObservableCollection<Cash>(_allCash);
            CurrentSupplierBalance = 0;
        }
    }

    private void FilterCashByOmla(byte omlaId)
    {
        var filtered = _allCash.Where(c => c.OmlaId == omlaId).ToList();
        CashList = new ObservableCollection<Cash>(filtered);

        // Default to first available cash in filtered list if current CashId is not in it
        if (filtered.Any() && !filtered.Any(c => c.CashId == FormItem.CashId))
        {
            SelectedCashId = filtered.First().CashId;
        }
        else if (FormItem != null)
        {
            CurrentCashBalance = CashList.FirstOrDefault(c => c.CashId == FormItem.CashId)?.Bal ?? 0;
        }
    }

    public ImportPaymentsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<ImportPayment> repository,
        IRepository<ImportSupplier> supplierRepo,
        IRepository<Cash> cashRepo,
        IRepository<Omla> omlaRepo,
        IRepository<ImportInvoice> invoiceRepo,
        CompositeKeyRepository compositeRepo) : base(repository)
    {
        _dbFactory = dbFactory;
        _supplierRepo = supplierRepo;
        _cashRepo = cashRepo;
        _omlaRepo = omlaRepo;
        _invoiceRepo = invoiceRepo;
        _compositeRepo = compositeRepo;

        FormItem.PayDate = DateTime.Now;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var supp = await _supplierRepo.GetAllAsync(); Suppliers = new(supp.Where(x => x.Active));
            var cash = await _cashRepo.GetAllAsync(); _allCash = cash.Where(x => x.Active).ToList();
            CashList = new(_allCash);
            var omlas = await _omlaRepo.GetAllAsync(); Omlas = new(omlas.Where(x => x.Active));
            var invs = await _invoiceRepo.GetAllAsync(); Invoices = new(invs);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override void OnFormItemChangedHook(ImportPayment value)
    {
        base.OnFormItemChangedHook(value);
        if (value != null)
        {
            // Update SelectedSupplier without triggering circular loop too much
            // Selecting via Suppliers collection to get the object with the same ID
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.SuppId == value.SuppId);
            
            CurrentCashBalance = CashList.FirstOrDefault(c => c.CashId == value.CashId)?.Bal ?? 0;
            OnPropertyChanged(nameof(SelectedCashId));
        }
    }

    protected override object GetEntityId(ImportPayment entity) => entity.PayId;
    protected override bool IsNewRecord(ImportPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(ImportPayment entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(ImportPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;
        entity.OmlaRate = 1;

        if (Suppliers.Any()) entity.SuppId = Suppliers.First().SuppId;
        if (CashList.Any()) entity.CashId = CashList.First().CashId;
        if (Omlas.Any()) entity.OmlaId = Omlas.First().OmlaId;
    }

    // ── Balance Recalculation Hooks ─────────────────────────────────

    protected override async Task BeforeSaveAsync(bool isInsert)
    {
        if (!isInsert && FormItem != null)
        {
            // جلب القيم القديمة من قاعدة البيانات قبل التعديل
            using var db = _dbFactory.CreateConnection();
            var old = await db.QueryFirstOrDefaultAsync<ImportPayment>(
                "SELECT SuppID, CashID FROM Import_Payments WHERE Pay_ID = @PayId",
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

        // إعادة حساب رصيد المورد الاستيراد القديم لو اتغير
        if (_oldSuppId.HasValue && _oldSuppId.Value != FormItem.SuppId)
            await _compositeRepo.RecalcBalanceForImportSupplierAsync(_oldSuppId.Value);

        // إعادة حساب رصيد المورد الاستيراد الحالي
        await _compositeRepo.RecalcBalanceForImportSupplierAsync(FormItem.SuppId);

        // إعادة حساب رصيد الخزينة القديمة لو اتغيرت
        if (_oldCashId.HasValue && _oldCashId.Value != (FormItem.CashId) && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);

        // إعادة حساب رصيد الخزينة الحالية
        if (FormItem.CashId > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(FormItem.CashId);

        await LoadRelatedDataAsync();
        OnFormItemChangedHook(FormItem);
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
        // إعادة حساب الأرصدة بعد الحذف
        if (_oldSuppId.HasValue)
            await _compositeRepo.RecalcBalanceForImportSupplierAsync(_oldSuppId.Value);

        if (_oldCashId.HasValue && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);

        await LoadRelatedDataAsync();
        OnFormItemChangedHook(FormItem);
    }

    [RelayCommand]
    private async Task PrintReceiptAsync()
    {
        if (FormItem == null || FormItem.PayId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ الإيصال قبل الطباعة", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
            double previousBalance = await _compositeRepo.GetImportSupplierOldBalanceAsync(FormItem.SuppId, FormItem.PayDate);

            // Import payments are payouts that decrease debt
            double amount = FormItem.PayMoney;
            double balanceAfter = previousBalance - amount;

            var model = new MotorBike.Services.ImportPaymentReceiptModel
            {
                ReceiptNo = FormItem.PayId.ToString(),
                IssueDate = FormItem.PayDate.ToString("yyyy-MM-dd"),
                SupplierName = Suppliers.FirstOrDefault(s => s.SuppId == FormItem.SuppId)?.SuppName ?? "",
                CashName = CashList.FirstOrDefault(c => c.CashId == FormItem.CashId)?.CashName ?? "",
                OmlaName = Omlas.FirstOrDefault(o => o.OmlaId == FormItem.OmlaId)?.OmlaName ?? "",
                OmlaRate = FormItem.OmlaRate,
                Amount = amount,
                // If the payment is linked to a specific import invoice, look it up
                InvNo = FormItem.InvId > 0 ? Invoices.FirstOrDefault(i => i.InvId == FormItem.InvId)?.InvName : null,
                Notes = FormItem.Notes ?? "",
                PreviousBalance = previousBalance,
                BalanceAfter = balanceAfter
            };

            var document = new MotorBike.Services.ImportPaymentReceiptDocument(model, company);
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "إيصال دفع مورد استيراد رقم " + FormItem.PayId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
