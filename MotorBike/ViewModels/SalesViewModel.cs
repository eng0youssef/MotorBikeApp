using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;
using Dapper;

namespace MotorBike.ViewModels;

public partial class SalesViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<Sale> _saleRepository;
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

    // ── Customer car selection (optional) ────────────────────────────────
    [ObservableProperty] private ObservableCollection<Car> _customerCars = [];

    [ObservableProperty] private ObservableCollection<Sale> _invoices = [];
    [ObservableProperty] private ObservableCollection<Sale> _filteredInvoices = [];

    [ObservableProperty] private Sale _formItem = new();
    [ObservableProperty] private ObservableCollection<SalesSub> _formSubItems = [];
    [ObservableProperty] private ObservableCollection<SalesPayment> _formPayments = [];

    [ObservableProperty] private SalesSub _currentSubItem = new();
    [ObservableProperty] private SalesPayment _currentPayment = new();
    
    [ObservableProperty] private string _itemSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Item> _filteredItemsList = [];
    [ObservableProperty] private bool _isItemSearchPopupOpen;

    // --- Customer Search ---
    [ObservableProperty] private string _customerSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Customer> _filteredCustomersList = [];
    [ObservableProperty] private bool _isCustomerSearchPopupOpen;
    private bool _isSelectingCustomer;

    [ObservableProperty] private Sale? _selectedInvoice;
    [ObservableProperty] private SalesSub? _selectedSubItem;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;
    [ObservableProperty] private double _remaining;
    [ObservableProperty] private bool _isCashPaymentMode;
    [ObservableProperty] private bool _isPaymentsPopupOpen;
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

    [ObservableProperty] private double _subItemQty;
    public double SubItemTotal => Math.Round(SubItemQty * (SubItemPrice - SubItemDiscountValue), 2);

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
            if (SetProperty(ref _discountPercentInput, value))
            {
                if (_isUpdatingDiscount) return;
                _isUpdatingDiscount = true;
                if (FormItem != null)
                {
                    FormItem.IsPer = true;
                    DiscountValueInput = Math.Round(FormItem.Total * (value / 100.0), 2);
                    FormItem.Disc = DiscountValueInput;
                    CalculateTotalsInternal();
                }
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
            if (SetProperty(ref _discountValueInput, value))
            {
                if (_isUpdatingDiscount) return;
                _isUpdatingDiscount = true;
                if (FormItem != null)
                {
                    FormItem.IsPer = false;
                    FormItem.Disc = value;
                    DiscountPercentInput = FormItem.Total > 0 ? Math.Round((value / FormItem.Total) * 100.0, 2) : 0;
                    CalculateTotalsInternal();
                }
                _isUpdatingDiscount = false;
            }
        }
    }
    
    private bool _isUpdatingDiscount;

    private double _subItemPrice;
    public double SubItemPrice
    {
        get => _subItemPrice;
        set
        {
            if (SetProperty(ref _subItemPrice, value))
            {
                if (CurrentSubItem != null) CurrentSubItem.Price = value;
                if (!_isUpdatingSubDiscount)
                {
                    _isUpdatingSubDiscount = true;
                    SubItemDiscountValue = Math.Round(value * (SubItemDiscountPercent / 100.0), 2);
                    if (CurrentSubItem != null) CurrentSubItem.Disc = SubItemDiscountValue;
                    _isUpdatingSubDiscount = false;
                }
                OnPropertyChanged(nameof(SubItemTotal));
            }
        }
    }

    private double _subItemDiscountPercent;
    public double SubItemDiscountPercent
    {
        get => _subItemDiscountPercent;
        set
        {
            if (SetProperty(ref _subItemDiscountPercent, value))
            {
                if (_isUpdatingSubDiscount) return;
                _isUpdatingSubDiscount = true;
                if (CurrentSubItem != null)
                {
                    CurrentSubItem.DiscPer = value;
                    SubItemDiscountValue = Math.Round(CurrentSubItem.Price * (value / 100.0), 2);
                    CurrentSubItem.Disc = SubItemDiscountValue;
                }
                _isUpdatingSubDiscount = false;
                OnPropertyChanged(nameof(SubItemTotal));
            }
        }
    }

    private double _subItemDiscountValue;
    public double SubItemDiscountValue
    {
        get => _subItemDiscountValue;
        set
        {
            if (SetProperty(ref _subItemDiscountValue, value))
            {
                if (_isUpdatingSubDiscount) return;
                _isUpdatingSubDiscount = true;
                if (CurrentSubItem != null)
                {
                    CurrentSubItem.Disc = value;
                    SubItemDiscountPercent = CurrentSubItem.Price > 0 ? Math.Round((value / CurrentSubItem.Price) * 100.0, 2) : 0;
                    CurrentSubItem.DiscPer = SubItemDiscountPercent;
                }
                _isUpdatingSubDiscount = false;
                OnPropertyChanged(nameof(SubItemTotal));
            }
        }
    }
    
    private bool _isUpdatingSubDiscount;

    private double _vatTaxPercent;
    public double VatTaxPercent
    {
        get => _vatTaxPercent;
        set
        {
            if (SetProperty(ref _vatTaxPercent, value))
            {
                CalculateTotalsInternal();
            }
        }
    }

    private double _whtTaxPercent;
    public double WhtTaxPercent
    {
        get => _whtTaxPercent;
        set
        {
            if (SetProperty(ref _whtTaxPercent, value))
            {
                CalculateTotalsInternal();
            }
        }
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

    private double _originalVatTax;
    private double _originalTax;

    public SalesViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Sale> saleRepository,
        IRepository<Customer> customerRepository,
        IRepository<Cash> cashRepository,
        IRepository<Item> itemRepository,
        IRepository<Store> storeRepository,
        IRepository<Unit> unitRepository,
        CompositeKeyRepository compositeRepo)
    {
        _dbFactory = dbFactory;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _cashRepository = cashRepository;
        _itemRepository = itemRepository;
        _storeRepository = storeRepository;
        _unitRepository = unitRepository;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var customers = await _customerRepository.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers);

            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes.Where(c => c.OmlaId == 0 || c.OmlaId == null));

            var items = await _itemRepository.GetAllAsync();
            Items = new ObservableCollection<Item>(items);

            var stores = await _storeRepository.GetAllAsync();
            Stores = new ObservableCollection<Store>(stores);

            var units = await _unitRepository.GetAllAsync();
            Units = new ObservableCollection<Unit>(units);

            await LoadInvoicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    private async Task LoadInvoicesAsync()
    {
        var data = await _saleRepository.GetAllAsync();
        Invoices = new ObservableCollection<Sale>(data);
        FilterInvoices();
    }

    partial void OnSearchTextChanged(string value) => FilterInvoices();

    private void FilterInvoices()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredInvoices = new ObservableCollection<Sale>(Invoices);
            return;
        }

        var keywords = SearchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Invoices.Where(i =>
        {
            var cusName = Customers.FirstOrDefault(c => c.CusId == i.CusId)?.CusName?.ToLower() ?? string.Empty;
            var idStr = i.SalesId.ToString();
            return keywords.All(k => idStr.Contains(k) || cusName.Contains(k));
        });
        FilteredInvoices = new ObservableCollection<Sale>(filtered);
    }

    [RelayCommand]
    private void ShowSearchPanel()
    {
        IsSearchPanelVisible = true;
        SearchText = string.Empty;
        FilterInvoices();
    }

    [RelayCommand]
    private void HideSearchPanel()
    {
        IsSearchPanelVisible = false;
    }

    [RelayCommand]
    private void OpenPaymentsPopup()
    {
        IsPaymentsPopupOpen = true;
    }

    [RelayCommand]
    private void ClosePaymentsPopup()
    {
        IsPaymentsPopupOpen = false;
    }

    partial void OnSelectedInvoiceChanged(Sale? value)
    {
        if (value is not null)
        {
            IsSearchPanelVisible = false;
            _isInsertMode = false;
            IsEditing = false;
            
            FormItem = CloneInvoice(value);
            
            _isUpdatingDiscount = true;
            if (FormItem.IsPer)
            {
                DiscountPercentInput = Math.Round(FormItem.DiscPer * 100.0, 2);
                DiscountValueInput = FormItem.Disc;
            }
            else
            {
                DiscountValueInput = FormItem.Disc;
            DiscountPercentInput = FormItem.Total > 0 ? Math.Round((FormItem.Disc / FormItem.Total) * 100.0, 2) : 0;
            }
            _isUpdatingDiscount = false;

            _originalVatTax = value.VatTax;
            _originalTax = value.Tax;

            _vatTaxPercent = 0;
            _whtTaxPercent = 0;

            // Set customer search text
            _isSelectingCustomer = true;
            CustomerSearchText = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.CusName ?? string.Empty;
            CurrentCustomerBalance = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.Bal ?? 0;
            IsCustomerSearchPopupOpen = false;
            _isSelectingCustomer = false;

            // Update cash mode
            IsCashPaymentMode = FormItem.IsCash;
            if (FormItem.IsCash && FormPayments.Any()) SelectedCashId = FormPayments[0].CashId;

            LoadSubItemsAsync(value.SalesId).ConfigureAwait(false);
            if (FormItem.CusId > 0)
                LoadCustomerCarsAsync(FormItem.CusId).ConfigureAwait(false);
            else
                CustomerCars.Clear();
        }
    }

    private async Task LoadSubItemsAsync(int salesId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var subItems = await db.QueryAsync<SalesSub>("SELECT * FROM Sales_Sub WHERE SalesId = @SalesId", new { SalesId = salesId });
            FormSubItems = new ObservableCollection<SalesSub>(subItems);
            WireSubItemsCollection();

            var payments = await db.QueryAsync<SalesPayment>("SELECT * FROM Sales_Payments WHERE SalesId = @SalesId", new { SalesId = salesId });
            FormPayments = new ObservableCollection<SalesPayment>(payments);
            WirePaymentsCollection();
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
            StatusMessage = "خطأ في تحميل الأصناف: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task AddNewAsync()
    {
        var item = new Sale
        {
            SalesDate = DateTime.Now,
            CusId = 0,
            AddPc = Environment.MachineName,
            AddDate = DateTime.Now
        };
        
        // item.SalesId = await _saleRepository.GetNextIdAsync(); // Delayed until save

        _isInsertMode = true;
        IsEditing = true;
        SelectedInvoice = null;
        item.IsPer = true;
        FormItem = item;
        
        _isUpdatingDiscount = true;
        DiscountPercentInput = 0;
        DiscountValueInput = 0;
        _isUpdatingDiscount = false;
        
        FormSubItems.Clear();
        WireSubItemsCollection();
        FormPayments.Clear();
        WirePaymentsCollection();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        
        CurrentSubItem = new SalesSub { SalesId = item.SalesId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        SubItemQty = 1;
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;

        CurrentPayment = new SalesPayment { SalesId = item.SalesId, PayDate = item.SalesDate.AddSeconds(20), CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
        SelectedCashId = Cashes.FirstOrDefault()?.CashId ?? 0;
        CurrentSafeBalance = Cashes.FirstOrDefault(c => c.CashId == SelectedCashId)?.Bal ?? 0;
        
        // Reset customer search
        _isSelectingCustomer = true;
        CustomerSearchText = string.Empty;
        IsCustomerSearchPopupOpen = false;
        _isSelectingCustomer = false;

        // Reset car selection
        CustomerCars.Clear();

        VatTaxPercent = 0;
        WhtTaxPercent = 0;
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
    }

    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false;
        IsEditing = false;
        FormItem = new Sale();
        
        _isUpdatingDiscount = true;
        DiscountPercentInput = 0;
        DiscountValueInput = 0;
        _isUpdatingDiscount = false;
        
        FormSubItems.Clear();
        FormPayments.Clear();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        CurrentSubItem = new SalesSub();
        CurrentPayment = new SalesPayment();
        SelectedCashId = 0;
        CurrentSafeBalance = 0;
        CurrentCustomerBalance = 0;
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        
        _isSelectingCustomer = true;
        CustomerSearchText = string.Empty;
        IsCustomerSearchPopupOpen = false;
        _isSelectingCustomer = false;

        VatTaxPercent = 0;
        WhtTaxPercent = 0;
        StatusMessage = null;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;

        if (FormItem.CusId <= 0 || !Customers.Any(c => c.CusId == FormItem.CusId))
        {
            StatusMessage = "⚠️ يجب اختيار العميل من القائمة لإتمام حفظ الفاتورة.";
            return;
        }

        if (FormSubItems == null || !FormSubItems.Any())
        {
            StatusMessage = "⚠️ لا يمكن حفظ فاتورة بيع بدون إضافة أصناف.";
            return;
        }

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
                    "SELECT DISTINCT ItemId FROM Sales_Sub WHERE SalesId = @SalesId",
                    new { SalesId = FormItem.SalesId });
                foreach (var id in oldItemIds)
                    if (!affectedItemIds.Contains(id)) affectedItemIds.Add(id);

                var oldCashIds = await dbPre.QueryAsync<int>(
                    "SELECT DISTINCT CashID FROM Sales_Payments WHERE SalesId = @SalesId",
                    new { SalesId = FormItem.SalesId });
                foreach (var cid in oldCashIds)
                    if (cid > 0 && !affectedCashIds.Contains(cid)) affectedCashIds.Add(cid);

                oldCusId = await dbPre.QueryFirstOrDefaultAsync<int?>(
                    "SELECT CusID FROM Sales WHERE Sales_ID = @SalesId",
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
                        "SELECT CAST(MAX(CAST(TaxNo AS INT)) AS VARCHAR) FROM Sales WHERE ISNUMERIC(TaxNo) = 1 AND TaxNo NOT LIKE '%[^0-9]%'",
                        transaction: tx);
                    int.TryParse(maxTaxNoStr, out int maxTaxNo);
                    FormItem.TaxNo = (maxTaxNo + 1).ToString();
                }
                else if (!FormItem.IsTax)
                {
                    FormItem.TaxNo = null;
                }

                if (_isInsertMode)
                {
                    FormItem.SalesId = await _saleRepository.GetNextIdAsync();
                    OnPropertyChanged(nameof(FormItem));
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                    INSERT INTO Sales (Sales_ID, SalesDate, CusId, Total, Disc, AddMony, IsPer, IsCash, Notes, AddDate, AddPc, AddUser, IsTax, VatTax, Tax, TaxNo, CarID) 
                    VALUES (@SalesId, @SalesDate, @CusId, @Total, @Disc, @AddMony, @IsPer, @IsCash, @Notes, @AddDate, @AddPc, @AddUser, @IsTax, @VatTax, @Tax, @TaxNo, @CarId)",
                        FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                    UPDATE Sales SET SalesDate=@SalesDate, CusId=@CusId, Total=@Total,
                    Disc=@Disc, AddMony=@AddMony, IsPer=@IsPer, IsCash=@IsCash, Notes=@Notes, 
                    EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser,
                    IsTax=@IsTax, VatTax=@VatTax, Tax=@Tax, TaxNo=@TaxNo, CarID=@CarId
                    WHERE Sales_ID = @SalesId",
                        FormItem, tx);

                    await db.ExecuteAsync("DELETE FROM Sales_Sub WHERE SalesId = @SalesId",
                        new { SalesId = FormItem.SalesId }, tx);
                    await db.ExecuteAsync("DELETE FROM Sales_Payments WHERE SalesId = @SalesId",
                        new { SalesId = FormItem.SalesId }, tx);
                }

                int maxSubId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(ID), 0) FROM Sales_Sub", transaction: tx);
                foreach (var s in FormSubItems)
                {
                    s.Id = ++maxSubId;
                    s.SalesId = FormItem.SalesId;
                    await db.ExecuteAsync(@"
                    INSERT INTO Sales_Sub (ID, SalesId, StoreId, ItemId, UnitId, Qty, Price, Disc, DiscPer, UnitQty) 
                    VALUES (@Id, @SalesId, @StoreId, @ItemId, @UnitId, @Qty, @Price, @Disc, @DiscPer, @UnitQty)",
                        s, tx);
                }

                int maxPayId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(Pay_ID), 0) FROM Sales_Payments", transaction: tx);
                foreach (var p in FormPayments)
                {
                    p.PayId = ++maxPayId;
                    p.SalesId = FormItem.SalesId;
                    p.PayDate = FormItem.SalesDate.AddSeconds(20);
                    await db.ExecuteAsync(@"
                    INSERT INTO Sales_Payments (Pay_ID, PayDate, PayMoney, CashID, Notes, SalesID) 
                    VALUES (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @SalesId)",
                        p, tx);
                }

                tx.Commit();
                // Retained IsEditing to enable further modifications
                StatusMessage = "تم حفظ فاتورة البيع بنجاح ✓ ";
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            // إعادة حساب متوسط التكلفة لكل الأصناف المتأثرة ابتداءً من تاريخ الفاتورة
            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcAvgCostForItemAsync(itemId, FormItem.SalesDate.Date);

            if (oldCusId.HasValue && oldCusId.Value != FormItem.CusId)
                await _compositeRepo.RecalcBalanceForCustomerAsync(oldCusId.Value);
            await _compositeRepo.RecalcBalanceForCustomerAsync(FormItem.CusId);

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            _isInsertMode = false;
            // IsEditing = false; // left true to allow further edits
            await LoadInvoicesAsync();
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
        try
        {
            var affectedItemIds = FormSubItems.Select(s => s.ItemId).Distinct().ToList();

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try {
                await db.ExecuteAsync("DELETE FROM Sales_Payments WHERE SalesId = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
                await db.ExecuteAsync("DELETE FROM Sales_Sub WHERE SalesId = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
                await db.ExecuteAsync("DELETE FROM Sales WHERE Sales_ID = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
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

            StatusMessage = "تم حذف الفاتورة بنجاح ✓";
            IsEditing = false;
            FormItem = new Sale();
            
            _isUpdatingDiscount = true;
            DiscountPercentInput = 0;
            DiscountValueInput = 0;
            _isUpdatingDiscount = false;
            
            FormSubItems.Clear();
            FormPayments.Clear();
            TotalPayed = 0;
            Remaining = 0;
            IsCashPaymentMode = false;
            SelectedInvoice = null;
            SelectedCashId = 0;
            CurrentSafeBalance = 0;
            CurrentCustomerBalance = 0;
            await LoadInvoicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
        }
    }

    // --- Customer Search ---

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

        // Load this customer's motorcycles
        LoadCustomerCarsAsync(customer.CusId).ConfigureAwait(false);
    }

    private async Task LoadCustomerCarsAsync(int cusId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var cars = await db.QueryAsync<Car>(
                @"SELECT * FROM Cars WHERE OwnerID = @CusId",
                new { CusId = cusId });
            CustomerCars = new ObservableCollection<Car>(cars);
        }
        catch
        {
            CustomerCars.Clear();
        }
    }

    // --- Sub Items Management ---
    
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
        
        var price = item.Price1 > 0 ? item.Price1 : item.Price0;
        
        // Filter units linked to this item (primary + secondary)
        var filtered = Units.Where(u => u.UnitId == item.UnitId).ToList();
        if (item.Unit2 > 0)
        {
            var secondUnit = Units.FirstOrDefault(u => u.UnitId == item.Unit2);
            if (secondUnit != null) filtered.Add(secondUnit);
        }
        CurrentItemUnits = new ObservableCollection<Unit>(filtered);
        
        CurrentSubItem = new SalesSub
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
        SubItemQty = 1;
        SubItemPrice = price;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
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
        
        CurrentSubItem = new SalesSub { SalesId = FormItem.SalesId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        _isUpdatingSubDiscount = true;
        SubItemQty = 1;
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        _isUpdatingSubDiscount = false;
        
        ItemSearchText = string.Empty;
        CurrentItemUnits = [];
        
        CalculateTotals();
    }

    [RelayCommand]
    private void RemoveSubItem(SalesSub sub)
    {
        if (sub != null && FormSubItems.Contains(sub))
        {
            FormSubItems.Remove(sub);
            CalculateTotals();
        }
    }

    // --- Payments Management ---

    [RelayCommand]
    private void AddPayment()
    {
        if (IsCashPaymentMode) return;
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;

        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();

        CurrentPayment = new SalesPayment
        {
            SalesId = FormItem.SalesId,
            PayDate = FormItem.SalesDate.AddSeconds(20),
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };
    }

    [RelayCommand]
    private void RemovePayment(SalesPayment payment)
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
        IsCashPaymentMode = FormItem.IsCash;

        if (FormItem.IsCash)
        {
            FormPayments.Clear();
            FormPayments.Add(new SalesPayment
            {
                SalesId = FormItem.SalesId,
                PayDate = FormItem.SalesDate.AddSeconds(20),
                PayMoney = FormItem.Net,
                CashId = Cashes.FirstOrDefault()?.CashId ?? 0,
                Notes = "سداد كامل (كاش)"
            });
            CalculatePayedTotal();
        }
        else
        {
            FormPayments.Clear();
            CalculatePayedTotal();
        }
    }

    private void CalculateTotals()
    {
        if (FormItem == null || _isUpdatingDiscount) return;
        
        _isUpdatingDiscount = true;
        FormItem.Total = FormSubItems.Sum(x => x.Total);
        
        if (FormItem.IsPer)
        {
            DiscountValueInput = Math.Round(FormItem.Total * (DiscountPercentInput / 100.0), 2);
            FormItem.Disc = DiscountValueInput;
            FormItem.DiscPer = DiscountPercentInput / 100.0;
        }
        else
        {
            DiscountPercentInput = FormItem.Total > 0 ? Math.Round((FormItem.Disc / FormItem.Total) * 100.0, 2) : 0;
            DiscountValueInput = FormItem.Disc;
            FormItem.DiscPer = DiscountPercentInput / 100.0;
        }
        
        CalculateTotalsInternal();
        _isUpdatingDiscount = false;

        if (IsCashPaymentMode && FormPayments.Any())
        {
            FormPayments[0].PayMoney = FormItem.Net;
            OnPropertyChanged(nameof(FormPayments));
            CalculatePayedTotal();
        }
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
        OnPropertyChanged(nameof(FormItem));
        UpdateRemaining();
    }
    
    public void RecalculateTotals()
    {
        if (!_isUpdatingDiscount) CalculateTotals();
    }

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

    private void OnSubItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (SalesSub sub in e.NewItems)
            {
                sub.PropertyChanged -= OnSubItemPropertyChanged;
                sub.PropertyChanged += OnSubItemPropertyChanged;
            }

        if (e.OldItems != null)
            foreach (SalesSub sub in e.OldItems)
                sub.PropertyChanged -= OnSubItemPropertyChanged;
    }

    private void OnSubItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SalesSub.Total))
        {
            CalculateTotals();
        }
    }

    private void WirePaymentsCollection()
    {
        FormPayments.CollectionChanged -= OnPaymentsCollectionChanged;
        FormPayments.CollectionChanged += OnPaymentsCollectionChanged;

        foreach (var p in FormPayments)
        {
            p.PropertyChanged -= OnPaymentPropertyChanged;
            p.PropertyChanged += OnPaymentPropertyChanged;
        }
    }

    private void OnPaymentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (SalesPayment p in e.NewItems)
            {
                p.PropertyChanged -= OnPaymentPropertyChanged;
                p.PropertyChanged += OnPaymentPropertyChanged;
            }

        if (e.OldItems != null)
            foreach (SalesPayment p in e.OldItems)
                p.PropertyChanged -= OnPaymentPropertyChanged;
    }

    private void OnPaymentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SalesPayment.PayMoney))
        {
            CalculatePayedTotal();
        }
    }

    private Sale CloneInvoice(Sale source)
    {
        return new Sale
        {
            SalesId = source.SalesId,
            SalesDate = source.SalesDate,
            CusId = source.CusId,
            Total = source.Total,
            Disc = source.Disc,
            DiscPer = source.DiscPer,
            AddMony = source.AddMony,
            Net = source.Net,
            IsPer = source.IsPer,
            NetPer = source.NetPer,
            IsCash = source.IsCash,
            Notes = source.Notes,
            IsTax = source.IsTax,
            VatTax = source.VatTax,
            Tax = source.Tax,
            TaxNo = source.TaxNo,
            CarId = source.CarId,
            AddUser = source.AddUser,
            AddDate = source.AddDate,
            AddPc = source.AddPc
        };
    }

    [RelayCommand]
    private async Task PrintInvoiceAsync()
    {
        if (FormItem == null || FormItem.SalesId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ الفاتورة أو اختيار فاتورة أولاً لطباعتها.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        if (FormSubItems == null || !FormSubItems.Any())
        {
            System.Windows.MessageBox.Show("الفاتورة لا تحتوي على أصناف.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
            double previousBalance = await _compositeRepo.GetCustomerOldBalanceAsync(FormItem.CusId, FormItem.SalesDate);

            var model = new MotorBike.Services.SalesInvoiceModel
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
                RemainingAmount = Remaining
            };

            foreach (var sub in FormSubItems)
            {
                model.Items.Add(new MotorBike.Services.SalesInvoiceItemModel
                {
                    ItemName = Items.FirstOrDefault(i => i.ItemId == sub.ItemId)?.ItemName ?? "",
                    Quantity = sub.Qty,
                    Price = sub.Price,
                    Discount = sub.Disc,
                    Total = sub.Total
                });
            }

            var document = new MotorBike.Services.SalesInvoiceDocument(model, company);
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf", DefaultExt = "pdf",
                Title = "حفظ الفاتورة كـ PDF",
                FileName = $"فاتورة_مبيعات_{FormItem.SalesId}_{DateTime.Now:yyyyMMdd}"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                QuestPDF.Fluent.GenerateExtensions.GeneratePdf(document, saveFileDialog.FileName);
                var result = System.Windows.MessageBox.Show("تم حفظ الفاتورة بنجاح. هل تريد فتح الملف الآن لطباعته؟", "حفظ وطباعة", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
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
