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
    [ObservableProperty] private double _currentSupplierBalance;
    [ObservableProperty] private double _currentCashBalance;
    [ObservableProperty] private string _supplierSearchText = string.Empty;
    [ObservableProperty] private bool _isSupplierSearchPopupOpen;
    [ObservableProperty] private ObservableCollection<Supplier> _filteredSuppliersList = [];
    private bool _isSelectingSupplier;

    public int? SelectedCashId
    {
        get => FormItem?.CashId;
        set
        {
            if (FormItem != null && FormItem.CashId != value)
            {
                FormItem.CashId = value;
                OnPropertyChanged(nameof(SelectedCashId));
                CurrentCashBalance = CashList.FirstOrDefault(c => c.CashId == value)?.Bal ?? 0;
            }
        }
    }

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
            FilteredSuppliersList = new ObservableCollection<Supplier>(Suppliers);

            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active && x.OmlaId == 0));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(SuppPayment entity) => entity.PayId;
    protected override bool IsNewRecord(SuppPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(SuppPayment entity, int id) => entity.PayId = id;

    protected override void OnFormItemChangedHook(SuppPayment value)
    {
        if (value != null)
        {
            CurrentSupplierBalance = Suppliers.FirstOrDefault(c => c.SuppId == value.SuppId)?.Bal ?? 0;
            CurrentCashBalance = CashList.FirstOrDefault(c => c.CashId == value.CashId)?.Bal ?? 0;
            
            _isSelectingSupplier = true;
            SupplierSearchText = Suppliers.FirstOrDefault(c => c.SuppId == value.SuppId)?.SuppName ?? string.Empty;
            _isSelectingSupplier = false;
            
            OnPropertyChanged(nameof(SelectedCashId));
        }
    }

    protected override void SetDefaultValues(SuppPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;
        entity.PayType = 0; // Default: سداد لمورد

        entity.SuppId = 0;
        entity.CashId = 0;
        entity.PayMoney = 0;
        entity.Notes = string.Empty;
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

        await LoadRelatedDataAsync(); // Refresh balances
        OnFormItemChangedHook(FormItem); // Update local balance properties
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

        await LoadRelatedDataAsync();
        OnFormItemChangedHook(FormItem);
    }

    partial void OnSupplierSearchTextChanged(string value)
    {
        if (_isSelectingSupplier) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredSuppliersList = new ObservableCollection<Supplier>(Suppliers);
            IsSupplierSearchPopupOpen = FilteredSuppliersList.Any();
            return;
        }

        var keywords = value.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Suppliers.Where(s =>
        {
            var name = s.SuppName?.ToLower() ?? string.Empty;
            var tel = s.Tel?.ToLower() ?? string.Empty;
            return keywords.All(k => name.Contains(k) || tel.Contains(k));
        });

        FilteredSuppliersList = new ObservableCollection<Supplier>(filtered);
        IsSupplierSearchPopupOpen = FilteredSuppliersList.Any();
    }

    [RelayCommand]
    private void SelectSupplier(Supplier supplier)
    {
        if (supplier == null) return;
        _isSelectingSupplier = true;
        FormItem.SuppId = supplier.SuppId;
        SupplierSearchText = supplier.SuppName;
        CurrentSupplierBalance = supplier.Bal ?? 0;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;
        OnPropertyChanged(nameof(FormItem));
    }

    [RelayCommand]
    private async Task PrintReceiptAsync()
    {
        if (FormItem == null || FormItem.PayId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ الإيصال أولاً لطباعته.", "تنبيه",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
            double previousBalance = await _compositeRepo.GetSupplierOldBalanceAsync(FormItem.SuppId, FormItem.PayDate);

            // PayType 0 = سداد للمورد (-) | PayType 1 = استلام من المورد (+)
            double amount      = FormItem.PayMoney;
            double balanceAfter = previousBalance + (FormItem.PayType == 0 ? -amount : amount);

            var model = new MotorBike.Services.SuppPaymentReceiptModel
            {
                ReceiptNo    = FormItem.PayId.ToString(),
                IssueDate    = FormItem.PayDate.ToString("yyyy-MM-dd"),
                SupplierName = Suppliers.FirstOrDefault(s => s.SuppId == FormItem.SuppId)?.SuppName ?? "",
                CashName     = CashList.FirstOrDefault(c => c.CashId == FormItem.CashId)?.CashName ?? "",
                PayTypeName  = PayTypes.FirstOrDefault(t => t.Key == FormItem.PayType).Value ?? "إيصال",
                Amount          = amount,
                Notes           = FormItem.Notes ?? "",
                PreviousBalance = previousBalance,
                BalanceAfter    = balanceAfter
            };

            var document = new MotorBike.Services.SuppPaymentReceiptDocument(model, company);

            // فتح نافذة المعاينة والطباعة
            var previewTitle = $"معاينة إيصال مورد رقم {FormItem.PayId}";
            var preview = new MotorBike.Views.PrintPreviewWindow(document, previewTitle)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            preview.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء تحضير الطباعة: " + ex.Message, "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
