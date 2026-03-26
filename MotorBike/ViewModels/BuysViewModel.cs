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

public partial class BuysViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<Buy> _buyRepository;
    private readonly IRepository<Supplier> _supplierRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<Item> _itemRepository;
    private readonly IRepository<Store> _storeRepository;
    private readonly IRepository<Unit> _unitRepository;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<Item> _items = [];
    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private ObservableCollection<Unit> _units = [];
    [ObservableProperty] private ObservableCollection<Unit> _currentItemUnits = [];

    [ObservableProperty] private ObservableCollection<Buy> _invoices = [];
    [ObservableProperty] private ObservableCollection<Buy> _filteredInvoices = [];

    [ObservableProperty] private Buy _formItem = new();
    [ObservableProperty] private ObservableCollection<BuySub> _formSubItems = [];
    [ObservableProperty] private ObservableCollection<BuyPayment> _formPayments = [];

    [ObservableProperty] private BuySub _currentSubItem = new();
    [ObservableProperty] private BuyPayment _currentPayment = new();
    
    [ObservableProperty] private string _itemSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Item> _filteredItemsList = [];
    [ObservableProperty] private bool _isItemSearchPopupOpen;

    // --- Supplier Search ---
    [ObservableProperty] private string _supplierSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Supplier> _filteredSuppliersList = [];
    [ObservableProperty] private bool _isSupplierSearchPopupOpen;
    private bool _isSelectingSupplier;

    [ObservableProperty] private Buy? _selectedInvoice;
    [ObservableProperty] private BuySub? _selectedSubItem;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;
    [ObservableProperty] private double _remaining;
    [ObservableProperty] private bool _isCashPaymentMode;
    [ObservableProperty] private double _subItemQty;
    public double SubItemTotal => Math.Round(SubItemQty * (SubItemPrice - SubItemDiscountValue), 2);

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

    [ObservableProperty] private double _netBeforeTax;

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

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

    public BuysViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Buy> buyRepository,
        IRepository<Supplier> supplierRepository,
        IRepository<Cash> cashRepository,
        IRepository<Item> itemRepository,
        IRepository<Store> storeRepository,
        IRepository<Unit> unitRepository,
        CompositeKeyRepository compositeRepo)
    {
        _dbFactory = dbFactory;
        _buyRepository = buyRepository;
        _supplierRepository = supplierRepository;
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
            var suppliers = await _supplierRepository.GetAllAsync();
            Suppliers = new ObservableCollection<Supplier>(suppliers);

            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes);

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
        var data = await _buyRepository.GetAllAsync();
        Invoices = new ObservableCollection<Buy>(data);
        FilterInvoices();
    }

    partial void OnSearchTextChanged(string value) => FilterInvoices();

    private void FilterInvoices()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredInvoices = new ObservableCollection<Buy>(Invoices);
            return;
        }

        var keywords = SearchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Invoices.Where(i =>
        {
            var suppName = Suppliers.FirstOrDefault(s => s.SuppId == i.SuppId)?.SuppName?.ToLower() ?? string.Empty;
            var idStr = i.BuyId.ToString();
            return keywords.All(k => idStr.Contains(k) || suppName.Contains(k));
        });
        FilteredInvoices = new ObservableCollection<Buy>(filtered);
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

    partial void OnSelectedInvoiceChanged(Buy? value)
    {
        if (value is not null)
        {
            // Selected an invoice from search
            IsSearchPanelVisible = false;
            _isInsertMode = false;
            IsEditing = false;
            
            // Clone it to form
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

            // Set supplier search text
            _isSelectingSupplier = true;
            SupplierSearchText = Suppliers.FirstOrDefault(s => s.SuppId == FormItem.SuppId)?.SuppName ?? string.Empty;
            IsSupplierSearchPopupOpen = false;
            _isSelectingSupplier = false;

            // Update cash mode
            IsCashPaymentMode = FormItem.IsCash;

            // Infer tax percentages from saved amounts
            if (FormItem.IsTax)
            {
                double netBefore = FormItem.Total - FormItem.Disc + FormItem.AddMoney;
                _vatTaxPercent = netBefore > 0 ? Math.Round((FormItem.VatTax / netBefore) * 100.0, 2) : 0;
                _whtTaxPercent = netBefore > 0 ? Math.Round((FormItem.Tax / netBefore) * 100.0, 2) : 0;
                OnPropertyChanged(nameof(VatTaxPercent));
                OnPropertyChanged(nameof(WhtTaxPercent));
            }
            else
            {
                _vatTaxPercent = 0;
                _whtTaxPercent = 0;
                OnPropertyChanged(nameof(VatTaxPercent));
                OnPropertyChanged(nameof(WhtTaxPercent));
            }

            // Load SubItems
            LoadSubItemsAsync(value.BuyId).ConfigureAwait(false);
        }
    }

    private async Task LoadSubItemsAsync(int buyId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var subItems = await db.QueryAsync<BuySub>("SELECT * FROM Buy_Sub WHERE BuyId = @BuyId", new { BuyId = buyId });
            FormSubItems = new ObservableCollection<BuySub>(subItems);
            WireSubItemsCollection();

            var payments = await db.QueryAsync<BuyPayment>("SELECT * FROM Buy_Payments WHERE BuyId = @BuyId", new { BuyId = buyId });
            FormPayments = new ObservableCollection<BuyPayment>(payments);
            CalculatePayedTotal();

            CalculateTotals();
        }
        catch (Exception ex)
        {
            StatusMessage = "خطأ في تحميل الأصناف: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task AddNewAsync()
    {
        var item = new Buy
        {
            BuyDate = DateTime.Now,
            SuppId = 0,
            AddPc = Environment.MachineName,
            AddDate = DateTime.Now
        };
        
        // item.BuyId = await _buyRepository.GetNextIdAsync(); // Delayed until save

        _isInsertMode = true;
        IsEditing = true;
        SelectedInvoice = null;
        item.IsPer = true; // Default in DB is 1
        FormItem = item;
        
        _isUpdatingDiscount = true;
        DiscountPercentInput = 0;
        DiscountValueInput = 0;
        _isUpdatingDiscount = false;
        
        FormSubItems.Clear();
        WireSubItemsCollection();
        FormPayments.Clear();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        
        CurrentSubItem = new BuySub { BuyId = item.BuyId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        SubItemQty = 1;
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;

        CurrentPayment = new BuyPayment { BuyId = item.BuyId, PayDate = DateTime.Now, CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
        
        // Reset supplier search
        _isSelectingSupplier = true;
        SupplierSearchText = string.Empty;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;

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
    }

    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false;
        IsEditing = false;
        FormItem = new Buy();
        
        _isUpdatingDiscount = true;
        DiscountPercentInput = 0;
        DiscountValueInput = 0;
        _isUpdatingDiscount = false;
        
        FormSubItems.Clear();
        FormPayments.Clear();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        CurrentSubItem = new BuySub();
        CurrentPayment = new BuyPayment();
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        
        _isSelectingSupplier = true;
        SupplierSearchText = string.Empty;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;

        VatTaxPercent = 0;
        WhtTaxPercent = 0;
        StatusMessage = null;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;

        if (FormItem.SuppId <= 0 || !Suppliers.Any(s => s.SuppId == FormItem.SuppId))
        {
            StatusMessage = "⚠️ يجب اختيار المورد من القائمة لإتمام حفظ الفاتورة.";
            return;
        }

        if (FormSubItems == null || !FormSubItems.Any())
        {
            StatusMessage = "⚠️ لا يمكن حفظ فاتورة شراء بدون إضافة أصناف.";
            return;
        }

        try
        {
            CalculateTotals();

            var affectedItemIds = FormSubItems.Select(s => s.ItemId).Distinct().ToList();
            var affectedCashIds = FormPayments.Select(p => p.CashId).Where(id => id > 0).Distinct().ToList();
            int? oldSuppId = null;

            if (!_isInsertMode)
            {
                using var dbPre = _dbFactory.CreateConnection();

                var oldItemIds = await dbPre.QueryAsync<int>(
                    "SELECT DISTINCT ItemId FROM Buy_Sub WHERE BuyId = @BuyId",
                    new { BuyId = FormItem.BuyId });
                foreach (var id in oldItemIds)
                    if (!affectedItemIds.Contains(id)) affectedItemIds.Add(id);

                var oldCashIds = await dbPre.QueryAsync<int>(
                    "SELECT DISTINCT CashID FROM Buy_Payments WHERE BuyId = @BuyId",
                    new { BuyId = FormItem.BuyId });
                foreach (var cid in oldCashIds)
                    if (cid > 0 && !affectedCashIds.Contains(cid)) affectedCashIds.Add(cid);

                oldSuppId = await dbPre.QueryFirstOrDefaultAsync<int?>(
                    "SELECT SuppID FROM Buy WHERE Buy_ID = @BuyId",
                    new { BuyId = FormItem.BuyId });
            }

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                if (FormItem.IsTax && string.IsNullOrWhiteSpace(FormItem.TaxNo))
                {
                    var maxTaxNoStr = await db.QueryFirstOrDefaultAsync<string>(
                        "SELECT CAST(MAX(CAST(TaxNo AS INT)) AS VARCHAR) FROM Buy WHERE ISNUMERIC(TaxNo) = 1 AND TaxNo NOT LIKE '%[^0-9]%'",
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
                    FormItem.BuyId = await _buyRepository.GetNextIdAsync();
                    OnPropertyChanged(nameof(FormItem));
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                    INSERT INTO Buy (Buy_ID, BuyDate, SuppId, Total, Disc, AddMoney, IsPer, IsCash, Notes, AddDate, AddPc, AddUser, IsTax, VatTax, Tax, TaxNo) 
                    VALUES (@BuyId, @BuyDate, @SuppId, @Total, @Disc, @AddMoney, @IsPer, @IsCash, @Notes, @AddDate, @AddPc, @AddUser, @IsTax, @VatTax, @Tax, @TaxNo)",
                        FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                    UPDATE Buy SET BuyDate=@BuyDate, SuppId=@SuppId, Total=@Total,
                    Disc=@Disc, AddMoney=@AddMoney, IsPer=@IsPer, IsCash=@IsCash, Notes=@Notes, 
                    EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser,
                    IsTax=@IsTax, VatTax=@VatTax, Tax=@Tax, TaxNo=@TaxNo
                    WHERE Buy_ID = @BuyId",
                        FormItem, tx);

                    await db.ExecuteAsync("DELETE FROM Buy_Sub WHERE BuyId = @BuyId",
                        new { BuyId = FormItem.BuyId }, tx);
                    await db.ExecuteAsync("DELETE FROM Buy_Payments WHERE BuyId = @BuyId",
                        new { BuyId = FormItem.BuyId }, tx);
                }

                int maxSubId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(ID), 0) FROM Buy_Sub", transaction: tx);
                foreach (var s in FormSubItems)
                {
                    s.Id = ++maxSubId;
                    s.BuyId = FormItem.BuyId;
                    await db.ExecuteAsync(@"
                    INSERT INTO Buy_Sub (ID, BuyId, StoreId, ItemId, UnitId, Qty, Price, Disc, DiscPer, UnitQty) 
                    VALUES (@Id, @BuyId, @StoreId, @ItemId, @UnitId, @Qty, @Price, @Disc, @DiscPer, @UnitQty)",
                        s, tx);
                }

                int maxPayId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(Pay_ID), 0) FROM Buy_Payments", transaction: tx);
                foreach (var p in FormPayments)
                {
                    p.PayId = ++maxPayId;
                    p.BuyId = FormItem.BuyId;
                    await db.ExecuteAsync(@"
                    INSERT INTO Buy_Payments (Pay_ID, PayDate, PayMoney, CashID, Notes, BuyID) 
                    VALUES (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @BuyId)",
                        p, tx);
                }

                tx.Commit();
                // Retained IsEditing to enable further modifications
                StatusMessage = "تم حفظ فاتورة الشراء بنجاح ✓ ";
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            if (oldSuppId.HasValue && oldSuppId.Value != FormItem.SuppId)
                await _compositeRepo.RecalcBalanceForSupplierAsync(oldSuppId.Value);
            await _compositeRepo.RecalcBalanceForSupplierAsync(FormItem.SuppId);

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
            // حفظ الأصناف المتأثرة قبل الحذف
            var affectedItemIds = FormSubItems.Select(s => s.ItemId).Distinct().ToList();

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try {
                await db.ExecuteAsync("DELETE FROM Buy_Payments WHERE BuyId = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx);
                await db.ExecuteAsync("DELETE FROM Buy_Sub WHERE BuyId = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx);
                await db.ExecuteAsync("DELETE FROM Buy WHERE Buy_ID = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }

            // إعادة حساب Stock لكل الأصناف المتأثرة
            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            // إعادة حساب رصيد المورد من كل الحركات
            await _compositeRepo.RecalcBalanceForSupplierAsync(SelectedInvoice.SuppId);

            // إعادة حساب رصيد كل خزينة متأثرة من كل الحركات
            var affectedCashIds = FormPayments.Select(p => p.CashId).Distinct().ToList();
            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            StatusMessage = "تم حذف الفاتورة بنجاح ✓";
            IsEditing = false;
            FormItem = new Buy();
            
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
            await LoadInvoicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
        }
    }

    // --- Supplier Search ---

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
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;
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
        
        // Filter units linked to this item (primary + secondary)
        var filtered = Units.Where(u => u.UnitId == item.UnitId).ToList();
        if (item.Unit2 > 0)
        {
            var secondUnit = Units.FirstOrDefault(u => u.UnitId == item.Unit2);
            if (secondUnit != null) filtered.Add(secondUnit);
        }
        CurrentItemUnits = new ObservableCollection<Unit>(filtered);
        
        CurrentSubItem = new BuySub
        {
            BuyId = FormItem.BuyId,
            StoreId = CurrentSubItem.StoreId,
            ItemId = item.ItemId,
            UnitId = item.UnitId,
            Price = item.Price0,
            Qty = 1,
            UnitQty = 1,
            QtyAll = 1,
            Total = item.Price0
        };
        
        _isUpdatingSubDiscount = true;
        SubItemQty = 1;
        SubItemPrice = item.Price0;
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
        
        CurrentSubItem = new BuySub { BuyId = FormItem.BuyId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
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
    private void RemoveSubItem(BuySub sub)
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
        if (IsCashPaymentMode) return; // No manual payments in cash mode
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;

        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();

        CurrentPayment = new BuyPayment
        {
            BuyId = FormItem.BuyId,
            PayDate = DateTime.Now,
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };
    }

    [RelayCommand]
    private void RemovePayment(BuyPayment payment)
    {
        if (IsCashPaymentMode) return; // No manual removal in cash mode
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

    /// <summary>
    /// Handle IsCash mode: auto-populate payment with full amount
    /// </summary>
    public void HandleCashModeChanged()
    {
        if (FormItem == null) return;
        IsCashPaymentMode = FormItem.IsCash;

        if (FormItem.IsCash)
        {
            // Auto-fill: single payment for full Net
            FormPayments.Clear();
            FormPayments.Add(new BuyPayment
            {
                BuyId = FormItem.BuyId,
                PayDate = DateTime.Now,
                PayMoney = FormItem.Net,
                CashId = Cashes.FirstOrDefault()?.CashId ?? 0,
                Notes = "سداد كامل (كاش)"
            });
            CalculatePayedTotal();
        }
        else
        {
            // Switch to credit: clear auto-payment so user can add manually
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
        }
        else
        {
            DiscountPercentInput = FormItem.Total > 0 ? Math.Round((FormItem.Disc / FormItem.Total) * 100.0, 2) : 0;
            DiscountValueInput = FormItem.Disc;
        }
        
        CalculateTotalsInternal();
        _isUpdatingDiscount = false;

        // If cash mode, update the auto-payment amount
        if (IsCashPaymentMode && FormPayments.Any())
        {
            FormPayments[0].PayMoney = FormItem.Net;
            OnPropertyChanged(nameof(FormPayments));
            CalculatePayedTotal();
        }
    }

    private void CalculateTotalsInternal()
    {
        NetBeforeTax = FormItem.Total - FormItem.Disc + FormItem.AddMoney;
        
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
        OnPropertyChanged(nameof(FormItem));
        UpdateRemaining();
    }
    
    public void RecalculateTotals()
    {
        if (!_isUpdatingDiscount) CalculateTotals();
    }

    /// <summary>
    /// Wire collection change listener to recalculate totals when sub-items change
    /// </summary>
    private void WireSubItemsCollection()
    {
        FormSubItems.CollectionChanged -= OnSubItemsCollectionChanged;
        FormSubItems.CollectionChanged += OnSubItemsCollectionChanged;

        // Subscribe to each item's PropertyChanged
        foreach (var sub in FormSubItems)
        {
            sub.PropertyChanged -= OnSubItemPropertyChanged;
            sub.PropertyChanged += OnSubItemPropertyChanged;
        }
    }

    private void OnSubItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (BuySub sub in e.NewItems)
            {
                sub.PropertyChanged -= OnSubItemPropertyChanged;
                sub.PropertyChanged += OnSubItemPropertyChanged;
            }

        if (e.OldItems != null)
            foreach (BuySub sub in e.OldItems)
                sub.PropertyChanged -= OnSubItemPropertyChanged;
    }

    private void OnSubItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuySub.Total))
        {
            CalculateTotals();
        }
    }

    private Buy CloneInvoice(Buy source)
    {
        return new Buy
        {
            BuyId = source.BuyId,
            BuyDate = source.BuyDate,
            SuppId = source.SuppId,
            Total = source.Total,
            Disc = source.Disc,
            DiscPer = source.DiscPer,
            AddMoney = source.AddMoney,
            Net = source.Net,
            IsPer = source.IsPer,
            NetPer = source.NetPer,
            IsCash = source.IsCash,
            Notes = source.Notes,
            AddUser = source.AddUser,
            AddDate = source.AddDate,
            AddPc = source.AddPc,
            IsTax = source.IsTax,
            VatTax = source.VatTax,
            Tax = source.Tax,
            TaxNo = source.TaxNo
        };
    }
}
