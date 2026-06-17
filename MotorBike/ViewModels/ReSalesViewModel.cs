using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MotorBike.ViewModels;

public partial class ReSalesViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<ReSale> _reSaleRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<Item> _itemRepository;
    private readonly IRepository<Store> _storeRepository;
    private readonly IRepository<Unit> _unitRepository;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty] private ObservableCollection<Customer> _customers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<Item> _items = [];
    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private ObservableCollection<Unit> _units = [];
    [ObservableProperty] private ObservableCollection<Unit> _currentItemUnits = [];

    [ObservableProperty] private ObservableCollection<ReSale> _invoices = [];
    [ObservableProperty] private ObservableCollection<ReSale> _filteredInvoices = [];

    [ObservableProperty] private ReSale _formItem = new();
    [ObservableProperty] private ObservableCollection<ReSalesSub> _formSubItems = [];
    [ObservableProperty] private ObservableCollection<ReSalesPayment> _formPayments = [];

    [ObservableProperty] private ReSalesSub _currentSubItem = new();
    [ObservableProperty] private ReSalesPayment _currentPayment = new();

    [ObservableProperty] private string _itemSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Item> _filteredItemsList = [];
    [ObservableProperty] private bool _isItemSearchPopupOpen;

    // --- Customer Search ---
    [ObservableProperty] private string _customerSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Customer> _filteredCustomersList = [];
    [ObservableProperty] private bool _isCustomerSearchPopupOpen;
    private bool _isSelectingCustomer;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

    [ObservableProperty] private ReSale? _selectedInvoice;
    [ObservableProperty] private ReSalesSub? _selectedSubItem;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;
    [ObservableProperty] private double _remaining;
    [ObservableProperty] private bool _isCashPaymentMode;
    partial void OnIsCashPaymentModeChanged(bool value) => HandleCashModeChanged();

    [ObservableProperty] private double _currentCustomerBalance;
    [ObservableProperty] private double _currentSafeBalance;

    private int _selectedCashId;
    public int SelectedCashId
    {
        get => _selectedCashId;
        set
        {
            if (SetProperty(ref _selectedCashId, value))
            {
                if (FormPayments != null && FormPayments.Any()) FormPayments[0].CashId = value;
                if (CurrentPayment != null) CurrentPayment.CashId = value;
                CurrentSafeBalance = Cashes.FirstOrDefault(c => c.CashId == value)?.Bal ?? 0;
            }
        }
    }

    [ObservableProperty] private bool _isInvoiceDiscountPer = true;
    [ObservableProperty] private bool _isSubItemDiscountPer = true;

    [ObservableProperty] private double _subItemQty;
    [ObservableProperty] private int _subItemUnitId;

    partial void OnSubItemUnitIdChanged(int value)
    {
        if (CurrentSubItem != null)
        {
            CurrentSubItem.UnitId = value;
            UpdatePriceBasedOnUnit();
        }
    }

    private void UpdatePriceBasedOnUnit()
    {
        if (CurrentSubItem == null || CurrentSubItem.ItemId == 0) return;

        var item = Items.FirstOrDefault(i => i.ItemId == CurrentSubItem.ItemId);
        if (item == null) return;

        var basePrice = item.Price1 > 0 ? item.Price1 : item.Price0;

        if (SubItemUnitId == item.Unit2 && item.Unit2 > 0 && item.Unit2Qty > 0)
        {
            // Large Unit
            SubItemPrice = Math.Round(basePrice * item.Unit2Qty, 2);
            CurrentSubItem.UnitQty = item.Unit2Qty;
        }
        else
        {
            // Small Unit
            SubItemPrice = basePrice;
            CurrentSubItem.UnitQty = 1;
        }
    }

    public double SubItemTotal => Math.Round(SubItemQty * (SubItemPrice - (CurrentSubItem?.Disc ?? 0)), 2);

    [ObservableProperty] private double _netBeforeTax;

    partial void OnSubItemQtyChanged(double value)
    {
        if (CurrentSubItem != null) CurrentSubItem.Qty = value;
        OnPropertyChanged(nameof(SubItemTotal));
    }

    private double _discountPercentInput;
    public double DiscountPercentInput
    {
        get => _discountPercentInput;
        set
        {
            if (SetProperty(ref _discountPercentInput, Math.Round(value, 2)))
            {
                if (_isUpdatingDiscount) return;
                _isUpdatingDiscount = true;
                if (FormItem != null && IsInvoiceDiscountPer) { FormItem.IsPer = true; double exactValue = FormItem.Total * (value / 100.0); FormItem.Disc = exactValue; _discountValueInput = Math.Round(exactValue, 2); OnPropertyChanged(nameof(DiscountValueInput)); CalculateTotalsInternal(); }
                _isUpdatingDiscount = false;
            }
        }
    }

    private double _discountValueInput;
    public double DiscountValueInput
    {
        get => _discountValueInput;
        set
        {
            if (SetProperty(ref _discountValueInput, Math.Round(value, 2)))
            {
                if (_isUpdatingDiscount) return;
                _isUpdatingDiscount = true;
                if (FormItem != null && !IsInvoiceDiscountPer) { FormItem.IsPer = false; FormItem.Disc = value; double exactPercent = FormItem.Total > 0 ? (value / FormItem.Total) * 100.0 : 0; _discountPercentInput = Math.Round(exactPercent, 2); OnPropertyChanged(nameof(DiscountPercentInput)); CalculateTotalsInternal(); }
                _isUpdatingDiscount = false;
            }
        }
    }
    private bool _isUpdatingDiscount;

    private double _subItemPrice;
    public double SubItemPrice
    {
        get => _subItemPrice;
        set { if (SetProperty(ref _subItemPrice, value)) { if (CurrentSubItem != null) CurrentSubItem.Price = value; if (!_isUpdatingSubDiscount) { _isUpdatingSubDiscount = true; if (IsSubItemDiscountPer) { double exactValue = value * (SubItemDiscountPercent / 100.0); if (CurrentSubItem != null) CurrentSubItem.Disc = exactValue; _subItemDiscountValue = Math.Round(exactValue, 2); OnPropertyChanged(nameof(SubItemDiscountValue)); } else { double exactPercent = value > 0 ? (SubItemDiscountValue / value) * 100.0 : 0; if (CurrentSubItem != null) CurrentSubItem.DiscPer = exactPercent; _subItemDiscountPercent = Math.Round(exactPercent, 2); OnPropertyChanged(nameof(SubItemDiscountPercent)); } _isUpdatingSubDiscount = false; } OnPropertyChanged(nameof(SubItemTotal)); } }
    }

    private double _subItemDiscountPercent;
    public double SubItemDiscountPercent
    {
        get => _subItemDiscountPercent;
        set { if (SetProperty(ref _subItemDiscountPercent, Math.Round(value, 2))) { if (_isUpdatingSubDiscount) return; _isUpdatingSubDiscount = true; if (CurrentSubItem != null && IsSubItemDiscountPer) { CurrentSubItem.DiscPer = value; double exactValue = CurrentSubItem.Price * (value / 100.0); CurrentSubItem.Disc = exactValue; _subItemDiscountValue = Math.Round(exactValue, 2); OnPropertyChanged(nameof(SubItemDiscountValue)); } _isUpdatingSubDiscount = false; OnPropertyChanged(nameof(SubItemTotal)); } }
    }

    private double _subItemDiscountValue;
    public double SubItemDiscountValue
    {
        get => _subItemDiscountValue;
        set { if (SetProperty(ref _subItemDiscountValue, Math.Round(value, 2))) { if (_isUpdatingSubDiscount) return; _isUpdatingSubDiscount = true; if (CurrentSubItem != null && !IsSubItemDiscountPer) { CurrentSubItem.Disc = value; double exactPercent = CurrentSubItem.Price > 0 ? (value / CurrentSubItem.Price) * 100.0 : 0; CurrentSubItem.DiscPer = exactPercent; _subItemDiscountPercent = Math.Round(exactPercent, 2); OnPropertyChanged(nameof(SubItemDiscountPercent)); } _isUpdatingSubDiscount = false; OnPropertyChanged(nameof(SubItemTotal)); } }
    }
    private bool _isUpdatingSubDiscount;

    private double _vatTaxPercent;
    public double VatTaxPercent
    {
        get => _vatTaxPercent;
        set { if (SetProperty(ref _vatTaxPercent, value)) { CalculateTotalsInternal(); } }
    }

    private double _whtTaxPercent;
    public double WhtTaxPercent
    {
        get => _whtTaxPercent;
        set { if (SetProperty(ref _whtTaxPercent, value)) { CalculateTotalsInternal(); } }
    }
    private bool _isPaymentsPopupOpen;
    public bool IsPaymentsPopupOpen
    {
        get => _isPaymentsPopupOpen;
        set { _isPaymentsPopupOpen = value; OnPropertyChanged(); }
    }

    public ICommand OpenPaymentsPopupCommand => new RelayCommand(() => IsPaymentsPopupOpen = true);
    public ICommand ClosePaymentsPopupCommand => new RelayCommand(() => IsPaymentsPopupOpen = false);


    public ReSalesViewModel(IDbConnectionFactory dbFactory, IRepository<ReSale> reSaleRepository, IRepository<Customer> customerRepository, IRepository<Cash> cashRepository, IRepository<Item> itemRepository, IRepository<Store> storeRepository, IRepository<Unit> unitRepository, CompositeKeyRepository compositeRepo)
    {
        _dbFactory = dbFactory; _reSaleRepository = reSaleRepository; _customerRepository = customerRepository;
        _cashRepository = cashRepository; _itemRepository = itemRepository; _storeRepository = storeRepository; _unitRepository = unitRepository;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            Customers = new ObservableCollection<Customer>(await _customerRepository.GetAllAsync());
            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes.Where(c => c.OmlaId == 0 || c.OmlaId == null));
            Items = new ObservableCollection<Item>(await _itemRepository.GetAllAsync());
            Stores = new ObservableCollection<Store>(await _storeRepository.GetAllAsync());
            Units = new ObservableCollection<Unit>(await _unitRepository.GetAllAsync());
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}"; }
    }

    private async Task LoadInvoicesAsync()
    {
        var dataList = (await _reSaleRepository.GetAllAsync()).ToList();

        using var db = _dbFactory.CreateConnection();
        var payments = await db.QueryAsync<ReSalesPayment>("SELECT SalesID, PayMoney FROM ReSales_Payments");
        var paymentsGroups = payments.GroupBy(p => p.SalesId).ToDictionary(g => g.Key, g => g.Sum(p => p.PayMoney));

        foreach (var s in dataList)
        {
            if (s.IsCash)
            {
                s.PaidAmount = s.Net;
                s.RemainingAmount = 0;
            }
            else
            {
                paymentsGroups.TryGetValue(s.SalesId, out double paid);
                s.PaidAmount = paid;
                s.RemainingAmount = s.Net - paid;
            }
        }

        Invoices = new ObservableCollection<ReSale>(dataList);
        FilterInvoices();
    }

    partial void OnSearchTextChanged(string value) => FilterInvoices();
    private void FilterInvoices()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredInvoices = new ObservableCollection<ReSale>(Invoices);
            return;
        }

        var keywords = SearchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Invoices.Where(i =>
        {
            var cusName = Customers.FirstOrDefault(c => c.CusId == i.CusId)?.CusName?.ToLower() ?? string.Empty;
            var idStr = i.SalesId.ToString();
            return keywords.All(k => idStr.Contains(k) || cusName.Contains(k));
        });
        FilteredInvoices = new ObservableCollection<ReSale>(filtered);
    }

    [RelayCommand] private void ShowSearchPanel() { IsSearchPanelVisible = true; SearchText = ""; FilterInvoices(); }
    [RelayCommand] private void HideSearchPanel() { IsSearchPanelVisible = false; }

    private double _originalVatTax;
    private double _originalTax;
    partial void OnSelectedInvoiceChanged(ReSale? value)
    {
        if (value is not null)
        {
            IsSearchPanelVisible = false;
            _isInsertMode = false;
            IsEditing = false;

            FormItem = CloneInvoice(value);

            _originalVatTax = value.VatTax;
            _originalTax = value.Tax;

            _isUpdatingDiscount = true;
            IsInvoiceDiscountPer = FormItem.IsPer;
            if (FormItem.IsPer)
            {
                DiscountPercentInput = Math.Round(FormItem.DiscPer * 100.0, 2);
                DiscountValueInput = Math.Round(FormItem.Disc, 2);
            }
            else
            {
                DiscountValueInput = Math.Round(FormItem.Disc, 2);
                DiscountPercentInput = FormItem.Total > 0
                    ? Math.Round((FormItem.Disc / FormItem.Total) * 100.0, 2)
                    : 0;
            }
            _isUpdatingDiscount = false;

            _isSelectingCustomer = true;
            CustomerSearchText = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.CusName ?? string.Empty;
            CurrentCustomerBalance = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.Bal ?? 0;
            IsCustomerSearchPopupOpen = false;
            _isSelectingCustomer = false;

            IsCashPaymentMode = FormItem.IsCash;
            if (FormItem.IsCash && FormPayments.Any()) SelectedCashId = FormPayments[0].CashId;

            _vatTaxPercent = 0;
            _whtTaxPercent = 0;

            _ = LoadSubItemsAsync(value.SalesId);
        }
    }

    private async Task LoadSubItemsAsync(int salesId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();

            FormSubItems = new ObservableCollection<ReSalesSub>(
                await db.QueryAsync<ReSalesSub>(
                    "SELECT * FROM ReSales_Sub WHERE SalesId = @SalesId",
                    new { SalesId = salesId }));

            WireSubItemsCollection();

            var payments = await db.QueryAsync<ReSalesPayment>(
                "SELECT * FROM ReSales_Payments WHERE SalesId = @SalesId",
                new { SalesId = salesId });

            FormPayments = new ObservableCollection<ReSalesPayment>(payments);
            CalculatePayedTotal();

            CalculateTotals();

            if (FormItem.IsTax)
            {
                double netBase = NetBeforeTax;

                if (netBase > 0)
                {
                    _vatTaxPercent = Math.Round((_originalVatTax / netBase) * 100.0, 2);
                    _whtTaxPercent = Math.Round((_originalTax / netBase) * 100.0, 2);
                }
                else
                {
                    _vatTaxPercent = 0;
                    _whtTaxPercent = 0;
                }

                OnPropertyChanged(nameof(VatTaxPercent));
                OnPropertyChanged(nameof(WhtTaxPercent));

                CalculateTotalsInternal();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "خطأ في تحميل الأصناف والدفعات: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task AddNewAsync()
    {
        var item = new ReSale { SalesDate = DateTime.Now, CusId = 0, AddPc = Environment.MachineName, AddDate = DateTime.Now, IsPer = true };
        _isInsertMode = true; IsEditing = true; SelectedInvoice = null; FormItem = item;
        IsInvoiceDiscountPer = true;
        _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
        FormSubItems.Clear();
        FormPayments.Clear();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        
        CurrentSubItem = new ReSalesSub { SalesId = item.SalesId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        _isUpdatingSubDiscount = true; IsSubItemDiscountPer = true; SubItemQty = 1; SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0; _isUpdatingSubDiscount = false;

        CurrentPayment = new ReSalesPayment { SalesId = item.SalesId, PayDate = FormItem.SalesDate.AddSeconds(20), CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
        SelectedCashId = Cashes.FirstOrDefault()?.CashId ?? 0;
        CurrentSafeBalance = Cashes.FirstOrDefault(c => c.CashId == SelectedCashId)?.Bal ?? 0;

        _isSelectingCustomer = true;
        CustomerSearchText = string.Empty;
        CurrentCustomerBalance = 0;

        IsCustomerSearchPopupOpen = false;
        _isSelectingCustomer = false;
        
        VatTaxPercent = 0;
        WhtTaxPercent = 0;
        
        WireSubItemsCollection();
    }

    [RelayCommand] 
    public void EditSelected() 
    { 
        if (SelectedInvoice is null) return; 
        FormItem = CloneInvoice(SelectedInvoice); 
        _isInsertMode = false; 
        IsEditing = true; 
        IsCashPaymentMode = FormItem.IsCash;
        if (FormItem.IsCash && FormPayments.Any()) SelectedCashId = FormPayments[0].CashId;
        _isSelectingCustomer = true;
        CustomerSearchText = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.CusName ?? string.Empty;
        CurrentCustomerBalance = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.Bal ?? 0;
        _isSelectingCustomer = false;
    }
    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false; IsEditing = false; FormItem = new ReSale();
        IsInvoiceDiscountPer = true;
        _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
        FormSubItems.Clear(); 
        FormPayments.Clear();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        CurrentSubItem = new ReSalesSub(); 
        CurrentPayment = new ReSalesPayment();
        _isUpdatingSubDiscount = true; IsSubItemDiscountPer = true; SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0; StatusMessage = null; _isUpdatingSubDiscount = false;
        
        _isSelectingCustomer = true;
        CustomerSearchText = string.Empty;
        CurrentCustomerBalance = 0;

        SelectedCashId = 0;
        IsCustomerSearchPopupOpen = false;
        _isSelectingCustomer = false;

        VatTaxPercent = 0;
        WhtTaxPercent = 0;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;

        var requiredAbility = _isInsertMode ? AppAbility.Add : AppAbility.Edit;
        if (!AppSession.HasPermission(ScreenId.ReSales, requiredAbility))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية لإجراء هذه العملية.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        if (FormItem.CusId <= 0) { StatusMessage = "⚠️ يجب اختيار العميل."; return; }
        if (!FormSubItems.Any()) { StatusMessage = "⚠️ لا يمكن حفظ مرتجع بدون أصناف."; return; }

        try
        {
            CalculateTotals();

            var affectedItemIds = FormSubItems.Select(s => s.ItemId).Distinct().ToList();
            var affectedCashIds = FormPayments.Select(p => p.CashId).Where(id => id > 0).Distinct().ToList();
            int? oldCusId = null;

            if (!_isInsertMode)
            {
                using var dbPre = _dbFactory.CreateConnection();

                var oldItemIds = await dbPre.QueryAsync<int>(
                    "SELECT DISTINCT ItemId FROM ReSales_Sub WHERE SalesId = @SalesId",
                    new { SalesId = FormItem.SalesId });
                foreach (var id in oldItemIds)
                    if (!affectedItemIds.Contains(id)) affectedItemIds.Add(id);

                var oldCashIds = await dbPre.QueryAsync<int>(
                    "SELECT DISTINCT CashID FROM ReSales_Payments WHERE SalesId = @SalesId",
                    new { SalesId = FormItem.SalesId });
                foreach (var cid in oldCashIds)
                    if (cid > 0 && !affectedCashIds.Contains(cid)) affectedCashIds.Add(cid);

                oldCusId = await dbPre.QueryFirstOrDefaultAsync<int?>(
                    "SELECT CusID FROM ReSales WHERE Sales_ID = @SalesId",
                    new { SalesId = FormItem.SalesId });
            }

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                if (FormItem.IsTax && string.IsNullOrWhiteSpace(FormItem.TaxNo))
                {
                    var maxTaxNoStr = await db.QueryFirstOrDefaultAsync<string>(
                        "SELECT CAST(MAX(CAST(TaxNo AS INT)) AS VARCHAR) FROM ReSales WHERE ISNUMERIC(TaxNo) = 1 AND TaxNo NOT LIKE '%[^0-9]%'", transaction: tx);
                    int.TryParse(maxTaxNoStr, out int maxTaxNo);
                    FormItem.TaxNo = (maxTaxNo + 1).ToString();
                }
                else if (!FormItem.IsTax)
                {
                    FormItem.TaxNo = null;
                }

                if (_isInsertMode)
                {
                    FormItem.SalesId = await _reSaleRepository.GetNextIdAsync();
                    OnPropertyChanged(nameof(FormItem));
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;

                    await db.ExecuteAsync(@"
                    INSERT INTO ReSales (Sales_ID, SalesDate, CusId, Total, Disc, AddMony, IsPer, IsCash, Notes, AddDate, AddPc, AddUser, IsTax, VatTax, Tax, TaxNo) 
                    VALUES (@SalesId, @SalesDate, @CusId, @Total, @Disc, @AddMony, @IsPer, @IsCash, @Notes, @AddDate, @AddPc, @AddUser, @IsTax, @VatTax, @Tax, @TaxNo)",
                        FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;

                    await db.ExecuteAsync(@"
                    UPDATE ReSales SET SalesDate=@SalesDate, CusId=@CusId, Total=@Total, Disc=@Disc, 
                    AddMony=@AddMony, IsPer=@IsPer, IsCash=@IsCash, Notes=@Notes, 
                    EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser,
                    IsTax=@IsTax, VatTax=@VatTax, Tax=@Tax, TaxNo=@TaxNo
                    WHERE Sales_ID = @SalesId",
                        FormItem, tx);

                    await db.ExecuteAsync("DELETE FROM ReSales_Sub WHERE SalesId = @SalesId",
                        new { SalesId = FormItem.SalesId }, tx);
                    await db.ExecuteAsync("DELETE FROM ReSales_Payments WHERE SalesId = @SalesId",
                        new { SalesId = FormItem.SalesId }, tx);
                }

                int maxSubId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(ID), 0) FROM ReSales_Sub", transaction: tx);
                foreach (var s in FormSubItems)
                {
                    s.Id = ++maxSubId;
                    s.SalesId = FormItem.SalesId;
                    await db.ExecuteAsync(@"
                    INSERT INTO ReSales_Sub (ID, SalesId, StoreId, ItemId, UnitId, Qty, Price, Disc, DiscPer, UnitQty) 
                    VALUES (@Id, @SalesId, @StoreId, @ItemId, @UnitId, @Qty, @Price, @Disc, @DiscPer, @UnitQty)",
                        s, tx);
                }

                int maxPayId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(Pay_ID), 0) FROM ReSales_Payments", transaction: tx);
                foreach (var p in FormPayments)
                {
                    p.PayDate = FormItem.SalesDate.AddSeconds(20);
                    p.PayId = ++maxPayId;
                    p.SalesId = FormItem.SalesId;
                    await db.ExecuteAsync(@"
                    INSERT INTO ReSales_Payments (Pay_ID, PayDate, PayMoney, CashID, Notes, SalesID) 
                    VALUES (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @SalesId)",
                        p, tx);
                }

                tx.Commit();
                StatusMessage = "تم الحفظ بنجاح ✓";
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            // إعادة حساب متوسط التكلفة لكل الأصناف المتأثرة ابتداءً من تاريخ المرتجع
            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcAvgCostForItemAsync(itemId, FormItem.SalesDate.Date);

            if (oldCusId.HasValue && oldCusId.Value != FormItem.CusId)
                await _compositeRepo.RecalcBalanceForCustomerAsync(oldCusId.Value);
            await _compositeRepo.RecalcBalanceForCustomerAsync(FormItem.CusId);

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            _isInsertMode = false;
            await LoadInvoicesAsync();
            var savedInvoice = Invoices.FirstOrDefault(x => x.SalesId == FormItem.SalesId);
            if (savedInvoice != null)
            {
                SelectedInvoice = savedInvoice;
                IsEditing = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحفظ: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedInvoice is null) return;

        if (!AppSession.HasPermission(ScreenId.ReSales, AppAbility.Delete))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية للحذف.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        var result = System.Windows.MessageBox.Show("هل أنت متأكد من الحذف نهائياً؟", "تأكيد الحذف", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            var affectedItemIds = FormSubItems.Select(s => s.ItemId).Distinct().ToList();

            using var db = _dbFactory.CreateConnection(); db.Open();
            using var tx = db.BeginTransaction();
            try 
            { 
                await db.ExecuteAsync("DELETE FROM ReSales_Payments WHERE SalesId = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
                await db.ExecuteAsync("DELETE FROM ReSales_Sub WHERE SalesId = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx); 
                await db.ExecuteAsync("DELETE FROM ReSales WHERE Sales_ID = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx); 
                tx.Commit(); 
            }
            catch { tx.Rollback(); throw; }

            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            // إعادة حساب متوسط التكلفة من البداية (بعد الحذف نحسب من أول حركة)
            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcAvgCostForItemAsync(itemId, DateTime.MinValue);

            // إعادة حساب رصيد العميل من كل الحركات
            await _compositeRepo.RecalcBalanceForCustomerAsync(SelectedInvoice.CusId);

            // إعادة حساب رصيد كل خزينة متأثرة من كل الحركات
            var affectedCashIds = FormPayments.Select(p => p.CashId).Distinct().ToList();
            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            StatusMessage = "تم حذف المرتجع بنجاح ✓"; IsEditing = false; FormItem = new ReSale();
            _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
            FormSubItems.Clear(); 
            FormPayments.Clear();
            SelectedInvoice = null;
            CurrentCustomerBalance = 0;

            SelectedCashId = 0;
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحذف: {ex.Message}"; }
    }

    private bool _isSelectingItem;

    partial void OnItemSearchTextChanged(string value)
    {
        if (_isSelectingItem) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredItemsList = new ObservableCollection<Item>(Items.Take(100));
            IsItemSearchPopupOpen = FilteredItemsList.Any();
            return;
        }

        var keywords = value.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Items.Where(item =>
        {
            return keywords.All(k =>
                (item.ItemName != null && item.ItemName.ToLower().Contains(k)) ||
                (item.Bar1 != null && item.Bar1.ToLower().Contains(k)) ||
                (item.Bar2 != null && item.Bar2.ToLower().Contains(k))
            );
        }).Take(100);

        FilteredItemsList = new ObservableCollection<Item>(filtered);
        IsItemSearchPopupOpen = FilteredItemsList.Any();
    }

    [RelayCommand]
    private void SelectItem(Item item)
    {
        if (item == null) return;
        _isSelectingItem = true;
        
        // Filter units linked to this item (primary + secondary)
        var filtered = Units.Where(u => u.UnitId == item.UnitId).ToList();
        if (item.Unit2 > 0)
        {
            var secondUnit = Units.FirstOrDefault(u => u.UnitId == item.Unit2);
            if (secondUnit != null) filtered.Add(secondUnit);
        }
        CurrentItemUnits = new ObservableCollection<Unit>(filtered);
        
        var price = item.Price1 > 0 ? item.Price1 : item.Price0;
        CurrentSubItem = new ReSalesSub 
        { 
            SalesId = FormItem.SalesId, 
            StoreId = CurrentSubItem.StoreId, 
            ItemId = item.ItemId, 
            UnitId = item.UnitId, 
            Price = price, 
            Qty = 1, 
            UnitQty = 1, 
            QtyAll = 1, 
            Total = price 
        };
        
        _isUpdatingSubDiscount = true; 
        IsSubItemDiscountPer = true;
        SubItemQty = 1;
        SubItemPrice = price; 
        SubItemDiscountPercent = 0; 
        SubItemDiscountValue = 0; 
        SubItemUnitId = item.UnitId;
        _isUpdatingSubDiscount = false;
        
        ItemSearchText = item.ItemName; 
        IsItemSearchPopupOpen = false;
        _isSelectingItem = false;
    }

    [RelayCommand]
    private void AddSubItem()
    {
        if (CurrentSubItem.ItemId == 0 || CurrentSubItem.Qty <= 0) return;
        CurrentSubItem.Total = CurrentSubItem.Qty * (CurrentSubItem.Price - CurrentSubItem.Disc);
        FormSubItems.Add(CurrentSubItem);
        CurrentSubItem = new ReSalesSub { SalesId = FormItem.SalesId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        _isUpdatingSubDiscount = true; IsSubItemDiscountPer = true; SubItemQty = 1; SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0; _isUpdatingSubDiscount = false;
        ItemSearchText = string.Empty; CalculateTotals();
        CurrentItemUnits = [];
        SubItemUnitId = 0;
    }

    [RelayCommand] private void RemoveSubItem(ReSalesSub sub) { if (sub != null && FormSubItems.Contains(sub)) { FormSubItems.Remove(sub); CalculateTotals(); } }

    private void CalculateTotals()
    {
        if (FormItem == null || _isUpdatingDiscount) return;
        _isUpdatingDiscount = true;
        FormItem.Total = FormSubItems.Sum(x => x.Total);
        if (IsInvoiceDiscountPer) { double exactValue = FormItem.Total * (DiscountPercentInput / 100.0); FormItem.Disc = exactValue; FormItem.DiscPer = DiscountPercentInput / 100.0; _discountValueInput = Math.Round(exactValue, 2); OnPropertyChanged(nameof(DiscountValueInput)); }
        else { double exactPercent = FormItem.Total > 0 ? (DiscountValueInput / FormItem.Total) * 100.0 : 0; FormItem.DiscPer = exactPercent / 100.0; _discountPercentInput = Math.Round(exactPercent, 2); OnPropertyChanged(nameof(DiscountPercentInput)); }
        CalculateTotalsInternal(); _isUpdatingDiscount = false;
    }

    private void CalculateTotalsInternal() 
    { 
        NetBeforeTax = FormItem.Total - FormItem.Disc + FormItem.AddMony;
        
        if (FormItem.IsTax)
        {
            FormItem.VatTax = Math.Round(NetBeforeTax * (VatTaxPercent / 100.0), 2);
            FormItem.Tax = Math.Round(NetBeforeTax * (WhtTaxPercent / 100.0), 2);
        }
        else
        {
            FormItem.Tax = 0;
            FormItem.VatTax = 0;
        }

        FormItem.Net = NetBeforeTax + FormItem.VatTax - FormItem.Tax;
        FormItem.NetPer = FormItem.Total > 0 ? Math.Round(FormItem.Net / FormItem.Total, 4) : 1;

        UpdateRemaining();
        if (IsCashPaymentMode)
        {
            HandleCashModeChanged();
        }
        OnPropertyChanged(nameof(FormItem)); 
    }

    [RelayCommand]
    private void AddPayment()
    {
        if (IsCashPaymentMode) return;
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;

        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();

        CurrentPayment = new ReSalesPayment
        {
            SalesId = FormItem.SalesId,
            PayDate = FormItem.SalesDate.AddSeconds(20),
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };
    }

    [RelayCommand]
    private void RemovePayment(ReSalesPayment payment)
    {
        if (IsCashPaymentMode) return;
        if (payment != null && FormPayments.Contains(payment))
        {
            FormPayments.Remove(payment);
            CalculatePayedTotal();
        }
    }

    private void CalculatePayedTotal()
    {
        TotalPayed = FormPayments.Sum(p => p.PayMoney);
        UpdateRemaining();
    }

    private void UpdateRemaining()
    {
        Remaining = (FormItem?.Net ?? 0) - TotalPayed;
    }

    public void HandleCashModeChanged()
    {
        if (FormItem == null) return;
        FormItem.IsCash = IsCashPaymentMode;

        if (IsCashPaymentMode)
        {
            // حفظ الخزينة الحالية إذا كانت موجودة في الدفعة لضمان عدم ضياع التعديل
            int existingCashId = FormPayments.FirstOrDefault()?.CashId ?? Cashes.FirstOrDefault()?.CashId ?? 0;

            FormPayments.Clear();
            FormPayments.Add(new ReSalesPayment
            {
                SalesId = FormItem.SalesId,
                PayDate = FormItem.SalesDate.AddSeconds(20),
                PayMoney = FormItem.Net,
                CashId = SelectedCashId > 0 ? SelectedCashId : Cashes.FirstOrDefault()?.CashId ?? 0,
                Notes = "سداد كامل (كاش) - مرتجع"
            });
        }
        else
        {
            FormPayments.Clear();
        }
        CalculatePayedTotal();
    }
    
    public void RecalculateTotals() { if (!_isUpdatingDiscount) CalculateTotals(); }

    private ReSale CloneInvoice(ReSale s) => new()
    {
        SalesId = s.SalesId, SalesDate = s.SalesDate, CusId = s.CusId, Total = s.Total, Disc = s.Disc,
        DiscPer = s.DiscPer, AddMony = s.AddMony, Net = s.Net, IsPer = s.IsPer, NetPer = s.NetPer,
        IsCash = s.IsCash, Notes = s.Notes, IsTax = s.IsTax, VatTax = s.VatTax, Tax = s.Tax, TaxNo = s.TaxNo,
        AddUser = s.AddUser, AddDate = s.AddDate, AddPc = s.AddPc
    };

    private void WireSubItemsCollection()
    {
        FormSubItems.CollectionChanged -= OnSubItemsCollectionChanged;
        FormSubItems.CollectionChanged += OnSubItemsCollectionChanged;

        foreach (var sub in FormSubItems)
        {
            sub.PropertyChanged -= OnSubItemPropertyChanged;
            sub.PropertyChanged += OnSubItemPropertyChanged;
        }
    }

    private void OnSubItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (ReSalesSub sub in e.NewItems)
            {
                sub.PropertyChanged -= OnSubItemPropertyChanged;
                sub.PropertyChanged += OnSubItemPropertyChanged;
            }

        if (e.OldItems != null)
            foreach (ReSalesSub sub in e.OldItems)
                sub.PropertyChanged -= OnSubItemPropertyChanged;
    }

    private void OnSubItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReSalesSub.Total))
        {
            CalculateTotals();
        }
    }

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (_isSelectingCustomer) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredCustomersList = new ObservableCollection<Customer>(Customers.Take(100));
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
    }

    [RelayCommand]
    private async Task PrintInvoiceAsync()
    {
        if (FormItem == null || FormItem.SalesId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ المرتجع أو اختيار مرتجع أولاً لطباعته.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        if (FormSubItems == null || !FormSubItems.Any())
        {
            System.Windows.MessageBox.Show("المرتجع لا يحتوي على أصناف.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
            double previousBalance = await _compositeRepo.GetCustomerOldBalanceAsync(FormItem.CusId, FormItem.SalesDate);

            var paymentsList = new System.Collections.Generic.List<(double Amount, string CashName, string Notes)>();
            foreach (var p in FormPayments)
            {
                var cashName = Cashes.FirstOrDefault(c => c.CashId == p.CashId)?.CashName ?? "";
                paymentsList.Add((p.PayMoney, cashName, p.Notes ?? ""));
            }

            var model = new MotorBike.Services.ReSalesInvoiceModel
            {
                InvoiceNo = FormItem.SalesId.ToString(),
                IssueDate = FormItem.SalesDate.ToString("yyyy-MM-dd"),
                Time = FormItem.AddDate.ToString("hh:mm tt") ?? "-",
                CustomerName = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.CusName ?? "",
                Notes = FormItem.Notes ?? "",
                IsCash = FormItem.IsCash,
                Total = FormItem.Total,
                Discount = FormItem.Disc,
                AddMoney = FormItem.AddMony,
                IsTax = FormItem.IsTax,
                VatTax = FormItem.VatTax,
                WhtTax = FormItem.Tax,
                NetAmount = FormItem.Net,
                PreviousBalance = previousBalance,
                PaidAmount = TotalPayed,
                RemainingAmount = Remaining,
                Payments = paymentsList
            };

            foreach (var sub in FormSubItems)
            {
                model.Items.Add(new MotorBike.Services.ReSalesInvoiceItemModel
                {
                    ItemName = Items.FirstOrDefault(i => i.ItemId == sub.ItemId)?.ItemName ?? "",
                    Quantity = sub.Qty,
                    Price = sub.Price,
                    Discount = sub.Disc,
                    Total = sub.Total
                });
            }

            var document = new MotorBike.Services.ReSalesInvoiceDocument(model, company);
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "فاتورة مرتجع مبيعات رقم " + FormItem.SalesId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
