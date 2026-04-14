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

public partial class CusPaymentsViewModel : LookupViewModelBase<CusPayment>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<Customer> _customerRepo;
    private readonly IRepository<Cash> _cashRepo;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty] private ObservableCollection<Customer> _customers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];
    [ObservableProperty] private double _currentCustomerBalance;
    [ObservableProperty] private double _currentCashBalance;
    [ObservableProperty] private string _customerSearchText = string.Empty;
    [ObservableProperty] private bool _isCustomerSearchPopupOpen;
    [ObservableProperty] private ObservableCollection<Customer> _filteredCustomersList = [];
    private bool _isSelectingCustomer;

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
        new(0, "سداد لعميل"),
        new(1, "تحصيل من عميل"),
    ];

    // ── لحفظ القيم القديمة عند التعديل ──
    private int? _oldCusId;
    private int? _oldCashId;

    public CusPaymentsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<CusPayment> repository,
        IRepository<Customer> customerRepo,
        IRepository<Cash> cashRepo,
        CompositeKeyRepository compositeRepo) : base(repository)
    {
        _dbFactory = dbFactory;
        _customerRepo = customerRepo;
        _cashRepo = cashRepo;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var customers = await _customerRepo.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers.Where(x => x.Active));
            FilteredCustomersList = new ObservableCollection<Customer>(Customers);

            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active && x.OmlaId == 0));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(CusPayment entity) => entity.PayId;
    protected override bool IsNewRecord(CusPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(CusPayment entity, int id) => entity.PayId = id;

    protected override void OnFormItemChangedHook(CusPayment value)
    {
        if (value != null)
        {
            CurrentCustomerBalance = Customers.FirstOrDefault(c => c.CusId == value.CusId)?.Bal ?? 0;
            CurrentCashBalance = CashList.FirstOrDefault(c => c.CashId == value.CashId)?.Bal ?? 0;
            
            _isSelectingCustomer = true;
            CustomerSearchText = Customers.FirstOrDefault(c => c.CusId == value.CusId)?.CusName ?? string.Empty;
            _isSelectingCustomer = false;
            
            OnPropertyChanged(nameof(SelectedCashId));
        }
    }

    protected override void SetDefaultValues(CusPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;
        entity.PayType = 1; // Default: تحصيل من عميل

        entity.CusId = 0;
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
            var old = await db.QueryFirstOrDefaultAsync<CusPayment>(
                "SELECT CusID, CashID FROM Cus_Payments WHERE Pay_ID = @PayId",
                new { FormItem.PayId });

            if (old != null)
            {
                _oldCusId = old.CusId;
                _oldCashId = old.CashId;
            }
        }
        else
        {
            _oldCusId = null;
            _oldCashId = null;
        }
    }

    protected override async Task AfterSaveAsync(bool wasInsert)
    {
        if (FormItem == null) return;

        // إعادة حساب رصيد العميل القديم لو اتغير
        if (_oldCusId.HasValue && _oldCusId.Value != FormItem.CusId)
            await _compositeRepo.RecalcBalanceForCustomerAsync(_oldCusId.Value);

        // إعادة حساب رصيد العميل الحالي
        await _compositeRepo.RecalcBalanceForCustomerAsync(FormItem.CusId);

        // إعادة حساب رصيد الخزينة القديمة لو اتغيرت
        if (_oldCashId.HasValue && _oldCashId.Value != (FormItem.CashId ?? 0) && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);

        // إعادة حساب رصيد الخزينة الحالية
        if (FormItem.CashId.HasValue && FormItem.CashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(FormItem.CashId.Value);
            
        await LoadRelatedDataAsync(); // Refresh balances in UI Collections
        OnFormItemChangedHook(FormItem); // Update CurrentCustomerBalance and CurrentCashBalance
    }

    protected override Task BeforeDeleteAsync()
    {
        // حفظ بيانات السجل المحدد قبل الحذف (SelectedItem لسة موجود)
        if (SelectedItem != null)
        {
            _oldCusId = SelectedItem.CusId;
            _oldCashId = SelectedItem.CashId;
        }
        return Task.CompletedTask;
    }

    protected override async Task AfterDeleteAsync()
    {
        // إعادة حساب رصيد العميل بعد الحذف
        if (_oldCusId.HasValue)
            await _compositeRepo.RecalcBalanceForCustomerAsync(_oldCusId.Value);

        // إعادة حساب رصيد الخزينة بعد الحذف
        if (_oldCashId.HasValue && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);
            
        await LoadRelatedDataAsync(); // Refresh balances in UI Collections
        OnFormItemChangedHook(FormItem); // Update balances
    }

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (_isSelectingCustomer) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredCustomersList = new ObservableCollection<Customer>(Customers);
            IsCustomerSearchPopupOpen = FilteredCustomersList.Any();
            return;
        }

        var keywords = value.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Customers.Where(c =>
        {
            var name = c.CusName?.ToLower() ?? string.Empty;
            var tel = c.Tel?.ToLower() ?? string.Empty;
            return keywords.All(k => name.Contains(k) || tel.Contains(k));
        });

        FilteredCustomersList = new ObservableCollection<Customer>(filtered);
        IsCustomerSearchPopupOpen = FilteredCustomersList.Any();
    }

    [RelayCommand]
    private void SelectCustomer(Customer customer)
    {
        if (customer == null) return;
        _isSelectingCustomer = true;
        FormItem.CusId = customer.CusId;
        CustomerSearchText = customer.CusName;
        CurrentCustomerBalance = customer.Bal ?? 0;
        IsCustomerSearchPopupOpen = false;
        _isSelectingCustomer = false;
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
            double previousBalance = await _compositeRepo.GetCustomerOldBalanceAsync(FormItem.CusId, FormItem.PayDate);

            // PayType 1 = تحصيل من عميل (+) | PayType 0 = سداد لعميل (-)
            double amount      = FormItem.PayMoney;
            double balanceAfter = previousBalance + (FormItem.PayType == 1 ? amount : -amount);

            var model = new MotorBike.Services.CusPaymentReceiptModel
            {
                ReceiptNo    = FormItem.PayId.ToString(),
                IssueDate    = FormItem.PayDate.ToString("yyyy-MM-dd"),
                CustomerName = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.CusName ?? "",
                CashName     = CashList.FirstOrDefault(c => c.CashId == FormItem.CashId)?.CashName ?? "",
                PayTypeName  = PayTypes.FirstOrDefault(t => t.Key == FormItem.PayType).Value ?? "إيصال",
                Amount          = amount,
                Notes           = FormItem.Notes ?? "",
                PreviousBalance = previousBalance,
                BalanceAfter    = balanceAfter
            };

            var document = new MotorBike.Services.CusPaymentReceiptDocument(model, company);

            // فتح نافذة المعاينة والطباعة
            var previewTitle = $"معاينة إيصال عميل رقم {FormItem.PayId}";
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
