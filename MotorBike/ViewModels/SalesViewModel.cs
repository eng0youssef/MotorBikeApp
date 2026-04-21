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
    [ObservableProperty] private bool _printCarData = true;

    [ObservableProperty] private ObservableCollection<Sale> _invoices = [];
    [ObservableProperty] private ObservableCollection<Sale> _filteredInvoices = [];

    [ObservableProperty] private Sale _formItem = new();
    [ObservableProperty] private ObservableCollection<SalesSub> _formSubItems = [];
    [ObservableProperty] private ObservableCollection<SalesPayment> _formPayments = [];
    [ObservableProperty] private ObservableCollection<SalesMaintenance> _formMaintenanceItems = [];

    [ObservableProperty] private SalesSub _currentSubItem = new();
    [ObservableProperty] private SalesPayment _currentPayment = new();
    [ObservableProperty] private SalesMaintenance _currentMaintenanceItem = new();
    
    [ObservableProperty] private bool _isMaintenanceSectionVisible;
    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = [];
    
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

    [ObservableProperty] private bool _isInvoiceDiscountPer = true;
    [ObservableProperty] private bool _isSubItemDiscountPer = true;

    [ObservableProperty] private double _subItemQty;
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
                if (FormItem != null && IsInvoiceDiscountPer)
                {
                    FormItem.IsPer = true;
                    double exactValue = FormItem.Total * (value / 100.0);
                    FormItem.Disc = exactValue;
                    
                    _discountValueInput = Math.Round(exactValue, 2);
                    OnPropertyChanged(nameof(DiscountValueInput));
                    
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
            if (SetProperty(ref _discountValueInput, Math.Round(value, 2)))
            {
                if (_isUpdatingDiscount) return;
                _isUpdatingDiscount = true;
                if (FormItem != null && !IsInvoiceDiscountPer)
                {
                    FormItem.IsPer = false;
                    FormItem.Disc = value;
                    
                    double exactPercent = FormItem.Total > 0 ? (value / FormItem.Total) * 100.0 : 0;
                    _discountPercentInput = Math.Round(exactPercent, 2);
                    OnPropertyChanged(nameof(DiscountPercentInput));
                    
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
                    if (IsSubItemDiscountPer)
                    {
                        double exactValue = value * (SubItemDiscountPercent / 100.0);
                        if (CurrentSubItem != null) CurrentSubItem.Disc = exactValue;
                        _subItemDiscountValue = Math.Round(exactValue, 2);
                        OnPropertyChanged(nameof(SubItemDiscountValue));
                    }
                    else
                    {
                        double exactPercent = value > 0 ? (SubItemDiscountValue / value) * 100.0 : 0;
                        if (CurrentSubItem != null) CurrentSubItem.DiscPer = exactPercent;
                        _subItemDiscountPercent = Math.Round(exactPercent, 2);
                        OnPropertyChanged(nameof(SubItemDiscountPercent));
                    }
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
            if (SetProperty(ref _subItemDiscountPercent, Math.Round(value, 2)))
            {
                if (_isUpdatingSubDiscount) return;
                _isUpdatingSubDiscount = true;
                if (CurrentSubItem != null && IsSubItemDiscountPer)
                {
                    CurrentSubItem.DiscPer = value;
                    double exactValue = CurrentSubItem.Price * (value / 100.0);
                    CurrentSubItem.Disc = exactValue;
                    
                    _subItemDiscountValue = Math.Round(exactValue, 2);
                    OnPropertyChanged(nameof(SubItemDiscountValue));
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
            if (SetProperty(ref _subItemDiscountValue, Math.Round(value, 2)))
            {
                if (_isUpdatingSubDiscount) return;
                _isUpdatingSubDiscount = true;
                if (CurrentSubItem != null && !IsSubItemDiscountPer)
                {
                    CurrentSubItem.Disc = value;
                    double exactPercent = CurrentSubItem.Price > 0 ? (value / CurrentSubItem.Price) * 100.0 : 0;
                    CurrentSubItem.DiscPer = exactPercent;

                    _subItemDiscountPercent = Math.Round(exactPercent, 2);
                    OnPropertyChanged(nameof(SubItemDiscountPercent));
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

            using var db = _dbFactory.CreateConnection();
            var suppliersList = await db.QueryAsync<Supplier>("SELECT * FROM Suppliers WHERE Active = 1");
            Suppliers = new ObservableCollection<Supplier>(suppliersList);

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
            IsInvoiceDiscountPer = FormItem.IsPer;
            if (FormItem.IsPer)
            {
                DiscountPercentInput = Math.Round(FormItem.DiscPer * 100.0, 2);
                DiscountValueInput = Math.Round(FormItem.Disc, 2);
            }
            else
            {
                DiscountValueInput = Math.Round(FormItem.Disc, 2);
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

            IsMaintenanceSectionVisible = FormItem.IsMaintenance;

            LoadSubItemsAsync(value.SalesId).ConfigureAwait(false);
            if (FormItem.CusId > 0)
                LoadCustomerCarsAsync(FormItem.CusId, value.CarId).ConfigureAwait(false);
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

            var maintenance = await db.QueryAsync<SalesMaintenance>("SELECT * FROM Sales_Maintenance WHERE SalesId = @SalesId", new { SalesId = salesId });
            foreach (var m in maintenance)
            {
                m.CashName = Cashes.FirstOrDefault(c => c.CashId == m.CashId)?.CashName ?? string.Empty;
                m.SuppName = Suppliers.FirstOrDefault(s => s.SuppId == m.SuppId)?.SuppName ?? string.Empty;
            }
            FormMaintenanceItems = new ObservableCollection<SalesMaintenance>(maintenance);
            WireMaintenanceCollection();

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
        
        IsInvoiceDiscountPer = true;
        _isUpdatingDiscount = true;
        DiscountPercentInput = 0;
        DiscountValueInput = 0;
        _isUpdatingDiscount = false;
        
        FormSubItems.Clear();
        WireSubItemsCollection();
        FormPayments.Clear();
        WirePaymentsCollection();
        FormMaintenanceItems.Clear();
        WireMaintenanceCollection();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        IsMaintenanceSectionVisible = false;
        
        CurrentSubItem = new SalesSub { SalesId = item.SalesId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        _isUpdatingSubDiscount = true;
        IsSubItemDiscountPer = true;
        SubItemQty = 1;
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        _isUpdatingSubDiscount = false;

        CurrentPayment = new SalesPayment { SalesId = item.SalesId, PayDate = item.SalesDate.AddSeconds(20), CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
        SelectedCashId = Cashes.FirstOrDefault()?.CashId ?? 0;
        CurrentSafeBalance = Cashes.FirstOrDefault(c => c.CashId == SelectedCashId)?.Bal ?? 0;

        CurrentMaintenanceItem = new SalesMaintenance { SalesId = item.SalesId, CashId = Cashes.FirstOrDefault()?.CashId, IsCash = false };
        
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
        FormMaintenanceItems.Clear();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        IsMaintenanceSectionVisible = false;
        CurrentSubItem = new SalesSub();
        CurrentPayment = new SalesPayment();
        CurrentMaintenanceItem = new SalesMaintenance();
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
            var affectedSuppIds = FormMaintenanceItems.Select(m => m.SuppId).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
            var maintCashIds = FormMaintenanceItems.Select(m => m.CashId).Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
            affectedCashIds.AddRange(maintCashIds.Where(id => !affectedCashIds.Contains(id)));
            
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

                var oldMaintCashIds = await dbPre.QueryAsync<int?>(
                    "SELECT DISTINCT CashId FROM Sales_Maintenance WHERE SalesId = @SalesId",
                    new { SalesId = FormItem.SalesId });
                foreach (var cid in oldMaintCashIds)
                    if (cid.HasValue && cid.Value > 0 && !affectedCashIds.Contains(cid.Value)) affectedCashIds.Add(cid.Value);

                var oldSuppIds = await dbPre.QueryAsync<int?>(
                    "SELECT DISTINCT SuppId FROM Sales_Maintenance WHERE SalesId = @SalesId",
                    new { SalesId = FormItem.SalesId });
                foreach (var sid in oldSuppIds)
                    if (sid.HasValue && sid.Value > 0 && !affectedSuppIds.Contains(sid.Value)) affectedSuppIds.Add(sid.Value);

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
                    INSERT INTO Sales (Sales_ID, SalesDate, CusId, Total, Disc, AddMony, IsPer, IsCash, Notes, AddDate, AddPc, AddUser, IsTax, VatTax, Tax, TaxNo, CarID, MaintTotal, IsMaintenance) 
                    VALUES (@SalesId, @SalesDate, @CusId, @Total, @Disc, @AddMony, @IsPer, @IsCash, @Notes, @AddDate, @AddPc, @AddUser, @IsTax, @VatTax, @Tax, @TaxNo, @CarId, @MaintTotal, @IsMaintenance)",
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
                    IsTax=@IsTax, VatTax=@VatTax, Tax=@Tax, TaxNo=@TaxNo, CarID=@CarId,
                    MaintTotal=@MaintTotal, IsMaintenance=@IsMaintenance
                    WHERE Sales_ID = @SalesId",
                        FormItem, tx);

                    await db.ExecuteAsync("DELETE FROM Sales_Sub WHERE SalesId = @SalesId",
                        new { SalesId = FormItem.SalesId }, tx);
                    await db.ExecuteAsync("DELETE FROM Sales_Payments WHERE SalesId = @SalesId",
                        new { SalesId = FormItem.SalesId }, tx);
                    await db.ExecuteAsync("DELETE FROM Sales_Maintenance WHERE SalesId = @SalesId",
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

                foreach (var m in FormMaintenanceItems)
                {
                    m.SalesId = FormItem.SalesId;

                    // Cash case: save CashId, keep SuppId as-is (user may have selected both)
                    if (m.IsCash)
                    {
                        if (m.CashId == null || m.CashId <= 0)
                            m.CashId = SelectedCashId > 0 ? SelectedCashId : Cashes.FirstOrDefault()?.CashId;
                        // SuppId is kept as user selected
                    }
                    else
                    {
                        // Credit case: no cash, only supplier
                        m.CashId = null;
                    }

                    await db.ExecuteAsync(@"
                    INSERT INTO Sales_Maintenance (SalesId, ItemName, Cost, Price, IsCash, CashId, SuppId) 
                    VALUES (@SalesId, @ItemName, @Cost, @Price, @IsCash, @CashId, @SuppId)",
                        new { m.SalesId, m.ItemName, m.Cost, m.Price, m.IsCash, m.CashId, m.SuppId }, tx);
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
                
            foreach (var suppId in affectedSuppIds)
                await _compositeRepo.RecalcBalanceForSupplierAsync(suppId);

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
        var result = System.Windows.MessageBox.Show("هل أنت متأكد من الحذف نهائياً؟", "تأكيد الحذف", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            var affectedItemIds = FormSubItems.Select(s => s.ItemId).Distinct().ToList();

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try {
                await db.ExecuteAsync("DELETE FROM Sales_Maintenance WHERE SalesId = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
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
            var affectedMaintCash = FormMaintenanceItems.Where(m => m.IsCash && m.CashId.HasValue).Select(m => m.CashId.Value).ToList();
            affectedCashIds.AddRange(affectedMaintCash.Where(id => !affectedCashIds.Contains(id)));
            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);
                
            var affectedSuppIds = FormMaintenanceItems.Where(m => !m.IsCash && m.SuppId.HasValue).Select(m => m.SuppId.Value).Distinct().ToList();
            foreach(var suppId in affectedSuppIds)
                await _compositeRepo.RecalcBalanceForSupplierAsync(suppId);

            StatusMessage = "تم حذف الفاتورة بنجاح ✓";
            IsEditing = false;
            FormItem = new Sale();
            
            _isUpdatingDiscount = true;
            DiscountPercentInput = 0;
            DiscountValueInput = 0;
            _isUpdatingDiscount = false;
            
            FormSubItems.Clear();
            FormPayments.Clear();
            FormMaintenanceItems.Clear();
            TotalPayed = 0;
            Remaining = 0;
            IsCashPaymentMode = false;
            IsMaintenanceSectionVisible = false;
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

    private async Task LoadCustomerCarsAsync(int cusId, int? preserveCarId = null)
    {
        int? targetCarId = preserveCarId ?? FormItem?.CarId;
        try
        {
            using var db = _dbFactory.CreateConnection();
            var cars = await db.QueryAsync<Car>(
                @"SELECT * FROM Cars WHERE OwnerID = @CusId",
                new { CusId = cusId });
            CustomerCars = new ObservableCollection<Car>(cars);
            
            if (FormItem != null && targetCarId > 0)
            {
                FormItem.CarId = targetCarId;
                OnPropertyChanged(nameof(FormItem));
            }
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
        FormItem.Total = FormSubItems.Sum(x => x.Total) + FormMaintenanceItems.Sum(m => m.Price);
        
        if (IsInvoiceDiscountPer)
        {
            double exactValue = FormItem.Total * (DiscountPercentInput / 100.0);
            FormItem.Disc = exactValue;
            FormItem.DiscPer = DiscountPercentInput / 100.0;
            _discountValueInput = Math.Round(exactValue, 2);
            OnPropertyChanged(nameof(DiscountValueInput));
        }
        else
        {
            double exactPercent = FormItem.Total > 0 ? (DiscountValueInput / FormItem.Total) * 100.0 : 0;
            FormItem.DiscPer = exactPercent / 100.0;
            _discountPercentInput = Math.Round(exactPercent, 2);
            OnPropertyChanged(nameof(DiscountPercentInput));
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
        // FormItem.Total already includes FormMaintenanceItems.Sum(m => m.Price) from CalculateSubTotals
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
        FormItem.MaintTotal = FormMaintenanceItems.Sum(m => m.Price);
        FormItem.IsMaintenance = IsMaintenanceSectionVisible;
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

    // --- Maintenance Management ---

    [RelayCommand]
    private void AddMaintenanceItem()
    {
        if (string.IsNullOrWhiteSpace(CurrentMaintenanceItem.ItemName)) return;
        
        // When IsCash and no CashId set, default to the first available
        if (CurrentMaintenanceItem.IsCash && (CurrentMaintenanceItem.CashId == null || CurrentMaintenanceItem.CashId <= 0))
        {
            CurrentMaintenanceItem.CashId = SelectedCashId > 0 ? SelectedCashId : Cashes.FirstOrDefault()?.CashId;
        }

        FormMaintenanceItems.Add(new SalesMaintenance
        {
            ItemName = CurrentMaintenanceItem.ItemName,
            Cost = CurrentMaintenanceItem.Cost,
            Price = CurrentMaintenanceItem.Price,
            IsCash = CurrentMaintenanceItem.IsCash,
            CashId = CurrentMaintenanceItem.CashId,
            SuppId = CurrentMaintenanceItem.SuppId,
            CashName = Cashes.FirstOrDefault(c => c.CashId == CurrentMaintenanceItem.CashId)?.CashName ?? string.Empty,
            SuppName = Suppliers.FirstOrDefault(s => s.SuppId == CurrentMaintenanceItem.SuppId)?.SuppName ?? string.Empty
        });
        
        CalculateTotals();

        CurrentMaintenanceItem = new SalesMaintenance
        { 
            SalesId = FormItem?.SalesId ?? 0, 
            IsCash = false, 
            CashId = Cashes.FirstOrDefault()?.CashId
        };
    }

    [RelayCommand]
    private void RemoveMaintenanceItem(SalesMaintenance item)
    {
        if (item != null && FormMaintenanceItems.Contains(item))
        {
            FormMaintenanceItems.Remove(item);
            CalculateTotals();
        }
    }

    private void WireMaintenanceCollection()
    {
        FormMaintenanceItems.CollectionChanged -= OnMaintenanceCollectionChanged;
        FormMaintenanceItems.CollectionChanged += OnMaintenanceCollectionChanged;

        foreach (var m in FormMaintenanceItems)
        {
            m.PropertyChanged -= OnMaintenancePropertyChanged;
            m.PropertyChanged += OnMaintenancePropertyChanged;
        }
    }

    private void OnMaintenanceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (SalesMaintenance m in e.NewItems)
            {
                m.PropertyChanged -= OnMaintenancePropertyChanged;
                m.PropertyChanged += OnMaintenancePropertyChanged;
            }

        if (e.OldItems != null)
            foreach (SalesMaintenance m in e.OldItems)
                m.PropertyChanged -= OnMaintenancePropertyChanged;
    }

    private void OnMaintenancePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SalesMaintenance.Price) || e.PropertyName == nameof(SalesMaintenance.Cost))
        {
            CalculateTotals();
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
            IsMaintenance = source.IsMaintenance,
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

            var paymentsList = new System.Collections.Generic.List<(double Amount, string CashName, string Notes)>();
            foreach (var p in FormPayments)
            {
                var cashName = Cashes.FirstOrDefault(c => c.CashId == p.CashId)?.CashName ?? "";
                paymentsList.Add((p.PayMoney, cashName, p.Notes ?? ""));
            }

            var carDetails = await db.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT b.BrandName, m.ModelName, c.ColorName, car.YearNo, car.ChassisNo, car.MotorNo, car.PlateNo 
                FROM Cars car
                LEFT JOIN CarModels m ON car.ModelId = m.Model_ID
                LEFT JOIN CarBrands b ON m.BrandID = b.Brand_ID
                LEFT JOIN Colors c ON car.ColorId = c.Color_ID
                WHERE car.Car_ID = @CarId", new { CarId = FormItem.CarId });

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
                RemainingAmount = Remaining,
                Payments = paymentsList,
                PrintCarData = PrintCarData && FormItem.CarId != null && FormItem.CarId > 0 && carDetails != null,
                CarBrand = carDetails?.BrandName ?? "",
                CarModel = carDetails?.ModelName ?? "",
                CarColor = carDetails?.ColorName ?? "",
                CarYear = carDetails?.YearNo?.ToString() ?? "",
                CarChassisNo = carDetails?.ChassisNo ?? "",
                CarMotorNo = carDetails?.MotorNo ?? "",
                CarPlateNo = carDetails?.PlateNo ?? ""
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

            foreach (var m in FormMaintenanceItems)
            {
                var cashName = Cashes.FirstOrDefault(c => c.CashId == m.CashId)?.CashName ?? "";
                var suppName = Suppliers.FirstOrDefault(s => s.SuppId == m.SuppId)?.SuppName ?? "";
                model.MaintenanceItems.Add(new MotorBike.Services.SalesMaintenanceItemModel
                {
                    ItemName = m.ItemName,
                    Cost = m.Cost,
                    Price = m.Price,
                    IsCash = m.IsCash,
                    CashName = cashName,
                    SuppName = suppName
                });
            }

            var document = new MotorBike.Services.SalesInvoiceDocument(model, company);
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "فاتورة مبيعات رقم " + FormItem.SalesId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
