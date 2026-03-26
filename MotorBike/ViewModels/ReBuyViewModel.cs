using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;
using Dapper;

namespace MotorBike.ViewModels;

public partial class ReBuyViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<ReBuy> _reBuyRepository;
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

    [ObservableProperty] private ObservableCollection<ReBuy> _invoices = [];
    [ObservableProperty] private ObservableCollection<ReBuy> _filteredInvoices = [];

    [ObservableProperty] private ReBuy _formItem = new();
    [ObservableProperty] private ObservableCollection<ReBuySub> _formSubItems = [];
    [ObservableProperty] private ObservableCollection<ReBuyPayment> _formPayments = [];

    [ObservableProperty] private ReBuySub _currentSubItem = new();
    [ObservableProperty] private ReBuyPayment _currentPayment = new();

    [ObservableProperty] private string _itemSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Item> _filteredItemsList = [];
    [ObservableProperty] private bool _isItemSearchPopupOpen;

    // --- Supplier Search ---
    [ObservableProperty] private string _supplierSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Supplier> _filteredSuppliersList = [];
    [ObservableProperty] private bool _isSupplierSearchPopupOpen;
    private bool _isSelectingSupplier;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

    [ObservableProperty] private ReBuy? _selectedInvoice;
    [ObservableProperty] private ReBuySub? _selectedSubItem;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;
    [ObservableProperty] private double _remaining;
    [ObservableProperty] private bool _isCashPaymentMode;
    partial void OnIsCashPaymentModeChanged(bool value) => HandleCashModeChanged();
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
                if (FormItem != null) { FormItem.IsPer = true; DiscountValueInput = Math.Round(FormItem.Total * (value / 100.0), 2); FormItem.Disc = DiscountValueInput; CalculateTotalsInternal(); }
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
                if (FormItem != null) { FormItem.IsPer = false; FormItem.Disc = value; DiscountPercentInput = FormItem.Total > 0 ? Math.Round((value / FormItem.Total) * 100.0, 2) : 0; CalculateTotalsInternal(); }
                _isUpdatingDiscount = false;
            }
        }
    }
    private bool _isUpdatingDiscount;

    private double _subItemPrice;
    public double SubItemPrice
    {
        get => _subItemPrice;
        set { if (SetProperty(ref _subItemPrice, value)) { if (CurrentSubItem != null) CurrentSubItem.Price = value; if (!_isUpdatingSubDiscount) { _isUpdatingSubDiscount = true; SubItemDiscountValue = Math.Round(value * (SubItemDiscountPercent / 100.0), 2); if (CurrentSubItem != null) CurrentSubItem.Disc = SubItemDiscountValue; _isUpdatingSubDiscount = false; } OnPropertyChanged(nameof(SubItemTotal)); } }
    }

    private double _subItemDiscountPercent;
    public double SubItemDiscountPercent
    {
        get => _subItemDiscountPercent;
        set { if (SetProperty(ref _subItemDiscountPercent, value)) { if (_isUpdatingSubDiscount) return; _isUpdatingSubDiscount = true; if (CurrentSubItem != null) { CurrentSubItem.DiscPer = value; SubItemDiscountValue = Math.Round(CurrentSubItem.Price * (value / 100.0), 2); CurrentSubItem.Disc = SubItemDiscountValue; } _isUpdatingSubDiscount = false; OnPropertyChanged(nameof(SubItemTotal)); } }
    }

    private double _subItemDiscountValue;
    public double SubItemDiscountValue
    {
        get => _subItemDiscountValue;
        set { if (SetProperty(ref _subItemDiscountValue, value)) { if (_isUpdatingSubDiscount) return; _isUpdatingSubDiscount = true; if (CurrentSubItem != null) { CurrentSubItem.Disc = value; SubItemDiscountPercent = CurrentSubItem.Price > 0 ? Math.Round((value / CurrentSubItem.Price) * 100.0, 2) : 0; CurrentSubItem.DiscPer = SubItemDiscountPercent; } _isUpdatingSubDiscount = false; OnPropertyChanged(nameof(SubItemTotal)); } }
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


    public ReBuyViewModel(IDbConnectionFactory dbFactory, IRepository<ReBuy> reBuyRepository, IRepository<Supplier> supplierRepository, IRepository<Cash> cashRepository, IRepository<Item> itemRepository, IRepository<Store> storeRepository, IRepository<Unit> unitRepository, CompositeKeyRepository compositeRepo)
    {
        _dbFactory = dbFactory; _reBuyRepository = reBuyRepository; _supplierRepository = supplierRepository;
        _cashRepository = cashRepository; _itemRepository = itemRepository; _storeRepository = storeRepository; _unitRepository = unitRepository;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            Suppliers = new ObservableCollection<Supplier>(await _supplierRepository.GetAllAsync());
            Cashes = new ObservableCollection<Cash>(await _cashRepository.GetAllAsync());
            Items = new ObservableCollection<Item>(await _itemRepository.GetAllAsync());
            Stores = new ObservableCollection<Store>(await _storeRepository.GetAllAsync());
            Units = new ObservableCollection<Unit>(await _unitRepository.GetAllAsync());
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}"; }
    }

    private async Task LoadInvoicesAsync() { Invoices = new ObservableCollection<ReBuy>(await _reBuyRepository.GetAllAsync()); FilterInvoices(); }

    partial void OnSearchTextChanged(string value) => FilterInvoices();
    private void FilterInvoices()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredInvoices = new ObservableCollection<ReBuy>(Invoices);
            return;
        }

        var keywords = SearchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Invoices.Where(i =>
        {
            var suppName = Suppliers.FirstOrDefault(s => s.SuppId == i.SuppId)?.SuppName?.ToLower() ?? string.Empty;
            var idStr = i.BuyId.ToString();
            return keywords.All(k => idStr.Contains(k) || suppName.Contains(k));
        });
        FilteredInvoices = new ObservableCollection<ReBuy>(filtered);
    }

    [RelayCommand] private void ShowSearchPanel() { IsSearchPanelVisible = true; SearchText = ""; FilterInvoices(); }
    [RelayCommand] private void HideSearchPanel() { IsSearchPanelVisible = false; }

    // Add these fields
    private double _originalVatTax;
    private double _originalTax;

    partial void OnSelectedInvoiceChanged(ReBuy? value)
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
            if (FormItem.IsPer) { DiscountPercentInput = Math.Round(FormItem.DiscPer * 100.0, 2); DiscountValueInput = FormItem.Disc; }
            else { DiscountValueInput = FormItem.Disc; DiscountPercentInput = FormItem.Total > 0 ? Math.Round((FormItem.Disc / FormItem.Total) * 100.0, 2) : 0; }
            _isUpdatingDiscount = false;

            _isSelectingSupplier = true;
            SupplierSearchText = Suppliers.FirstOrDefault(s => s.SuppId == FormItem.SuppId)?.SuppName ?? string.Empty;
            IsSupplierSearchPopupOpen = false;
            _isSelectingSupplier = false;

            IsCashPaymentMode = FormItem.IsCash;

            _vatTaxPercent = 0;
            _whtTaxPercent = 0;

            _ = LoadSubItemsAsync(value.BuyId);
        }
    }

    private async Task LoadSubItemsAsync(int buyId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            FormSubItems = new ObservableCollection<ReBuySub>(
                await db.QueryAsync<ReBuySub>("SELECT * FROM ReBuy_Sub WHERE BuyId = @BuyId", new { BuyId = buyId }));
            WireSubItemsCollection();

            var payments = await db.QueryAsync<ReBuyPayment>(
                "SELECT * FROM ReBuy_Payments WHERE BuyId = @BuyId", new { BuyId = buyId });
            FormPayments = new ObservableCollection<ReBuyPayment>(payments);
            CalculatePayedTotal();

            // Recalculate totals from loaded sub-items
            CalculateTotals();

            // ✅ Re-infer tax percentages AFTER totals are recalculated
            if (FormItem.IsTax)
            {
                double netBase = NetBeforeTax; // already computed by CalculateTotals()
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

                // Recalculate with correct percentages
                CalculateTotalsInternal();
            }
        }
        catch (Exception ex) { StatusMessage = "خطأ في تحميل الأصناف والدفعات: " + ex.Message; }
    }

    [RelayCommand]
    public async Task AddNewAsync()
    {
        var item = new ReBuy { BuyDate = DateTime.Now, SuppId = 0, AddPc = Environment.MachineName, AddDate = DateTime.Now, IsPer = true };
        _isInsertMode = true; IsEditing = true; SelectedInvoice = null; FormItem = item;
        _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
        FormSubItems.Clear();
        FormPayments.Clear();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        
        CurrentSubItem = new ReBuySub { BuyId = item.BuyId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        SubItemQty = 1; SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0;

        CurrentPayment = new ReBuyPayment { BuyId = item.BuyId, PayDate = DateTime.Now, CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };

        _isSelectingSupplier = true;
        SupplierSearchText = string.Empty;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;
        
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
        _isSelectingSupplier = true;
        SupplierSearchText = Suppliers.FirstOrDefault(s => s.SuppId == FormItem.SuppId)?.SuppName ?? string.Empty;
        _isSelectingSupplier = false;
    }
    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false; IsEditing = false; FormItem = new ReBuy();
        _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
        FormSubItems.Clear(); 
        FormPayments.Clear();
        TotalPayed = 0;
        Remaining = 0;
        IsCashPaymentMode = false;
        CurrentSubItem = new ReBuySub(); 
        CurrentPayment = new ReBuyPayment();
        SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0; StatusMessage = null;
        
        _isSelectingSupplier = true;
        SupplierSearchText = string.Empty;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;

        VatTaxPercent = 0;
        WhtTaxPercent = 0;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;
        if (FormItem.SuppId <= 0) { StatusMessage = "⚠️ يجب اختيار المورد."; return; }
        if (!FormSubItems.Any()) { StatusMessage = "⚠️ لا يمكن حفظ مرتجع بدون أصناف."; return; }

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
                    "SELECT DISTINCT ItemId FROM ReBuy_Sub WHERE BuyId = @BuyId",
                    new { BuyId = FormItem.BuyId });
                foreach (var id in oldItemIds)
                    if (!affectedItemIds.Contains(id)) affectedItemIds.Add(id);

                var oldCashIds = await dbPre.QueryAsync<int>(
                    "SELECT DISTINCT CashID FROM ReBuy_Payments WHERE BuyId = @BuyId",
                    new { BuyId = FormItem.BuyId });
                foreach (var cid in oldCashIds)
                    if (cid > 0 && !affectedCashIds.Contains(cid)) affectedCashIds.Add(cid);

                oldSuppId = await dbPre.QueryFirstOrDefaultAsync<int?>(
                    "SELECT SuppID FROM ReBuy WHERE Buy_ID = @BuyId",
                    new { BuyId = FormItem.BuyId });
            }

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                if (_isInsertMode)
                {
                    FormItem.BuyId = await _reBuyRepository.GetNextIdAsync();
                    OnPropertyChanged(nameof(FormItem));
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;

                    if (FormItem.IsTax && string.IsNullOrWhiteSpace(FormItem.TaxNo))
                    {
                        var maxTaxNoStr = await db.QueryFirstOrDefaultAsync<string>(
                            "SELECT CAST(MAX(CAST(TaxNo AS INT)) AS VARCHAR) FROM ReBuy WHERE ISNUMERIC(TaxNo) = 1 AND TaxNo NOT LIKE '%[^0-9]%'",
                            transaction: tx);
                        int.TryParse(maxTaxNoStr, out int maxTaxNo);
                        FormItem.TaxNo = (maxTaxNo + 1).ToString();
                    }
                    else if (!FormItem.IsTax)
                    {
                        FormItem.TaxNo = null;
                    }

                    await db.ExecuteAsync(@"
                    INSERT INTO ReBuy (Buy_ID, BuyDate, SuppId, Total, Disc, AddMoney, IsPer, IsCash, Notes, AddDate, AddPc, AddUser, IsTax, VatTax, Tax, TaxNo) 
                    VALUES (@BuyId, @BuyDate, @SuppId, @Total, @Disc, @AddMoney, @IsPer, @IsCash, @Notes, @AddDate, @AddPc, @AddUser, @IsTax, @VatTax, @Tax, @TaxNo)",
                        FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;

                    if (FormItem.IsTax && string.IsNullOrWhiteSpace(FormItem.TaxNo))
                    {
                        var maxTaxNoStr = await db.QueryFirstOrDefaultAsync<string>(
                            "SELECT CAST(MAX(CAST(TaxNo AS INT)) AS VARCHAR) FROM ReBuy WHERE ISNUMERIC(TaxNo) = 1 AND TaxNo NOT LIKE '%[^0-9]%'",
                            transaction: tx);
                        int.TryParse(maxTaxNoStr, out int maxTaxNo);
                        FormItem.TaxNo = (maxTaxNo + 1).ToString();
                    }
                    else if (!FormItem.IsTax)
                    {
                        FormItem.TaxNo = null;
                    }

                    await db.ExecuteAsync(@"
                    UPDATE ReBuy SET BuyDate=@BuyDate, SuppId=@SuppId, Total=@Total, Disc=@Disc, 
                    AddMoney=@AddMoney, IsPer=@IsPer, IsCash=@IsCash, Notes=@Notes, 
                    EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser,
                    IsTax=@IsTax, VatTax=@VatTax, Tax=@Tax, TaxNo=@TaxNo
                    WHERE Buy_ID = @BuyId",
                        FormItem, tx);

                    await db.ExecuteAsync("DELETE FROM ReBuy_Sub WHERE BuyId = @BuyId",
                        new { BuyId = FormItem.BuyId }, tx);
                    await db.ExecuteAsync("DELETE FROM ReBuy_Payments WHERE BuyId = @BuyId",
                        new { BuyId = FormItem.BuyId }, tx);
                }

                int maxSubId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(ID), 0) FROM ReBuy_Sub", transaction: tx);
                foreach (var s in FormSubItems)
                {
                    s.Id = ++maxSubId;
                    s.BuyId = FormItem.BuyId;
                    await db.ExecuteAsync(@"
                    INSERT INTO ReBuy_Sub (ID, BuyId, StoreId, ItemId, UnitId, Qty, Price, Disc, DiscPer, UnitQty) 
                    VALUES (@Id, @BuyId, @StoreId, @ItemId, @UnitId, @Qty, @Price, @Disc, @DiscPer, @UnitQty)",
                        s, tx);
                }

                int maxPayId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(Pay_ID), 0) FROM ReBuy_Payments", transaction: tx);
                foreach (var p in FormPayments)
                {
                    p.PayId = ++maxPayId;
                    p.BuyId = FormItem.BuyId;
                    await db.ExecuteAsync(@"
                    INSERT INTO ReBuy_Payments (Pay_ID, PayDate, PayMoney, CashID, Notes, BuyID) 
                    VALUES (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @BuyId)",
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

            if (oldSuppId.HasValue && oldSuppId.Value != FormItem.SuppId)
                await _compositeRepo.RecalcBalanceForSupplierAsync(oldSuppId.Value);
            await _compositeRepo.RecalcBalanceForSupplierAsync(FormItem.SuppId);

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            _isInsertMode = false;
            // IsEditing = false; // Retain edit mode so user can save again
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

            using var db = _dbFactory.CreateConnection(); db.Open();
            using var tx = db.BeginTransaction();
            try 
            { 
                await db.ExecuteAsync("DELETE FROM ReBuy_Payments WHERE BuyId = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx);
                await db.ExecuteAsync("DELETE FROM ReBuy_Sub WHERE BuyId = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx); 
                await db.ExecuteAsync("DELETE FROM ReBuy WHERE Buy_ID = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx); 
                tx.Commit(); 
            }
            catch { tx.Rollback(); throw; }

            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            // إعادة حساب رصيد المورد من كل الحركات
            await _compositeRepo.RecalcBalanceForSupplierAsync(SelectedInvoice.SuppId);

            // إعادة حساب رصيد كل خزينة متأثرة من كل الحركات
            var affectedCashIds = FormPayments.Select(p => p.CashId).Distinct().ToList();
            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            StatusMessage = "تم حذف المرتجع بنجاح ✓"; IsEditing = false; FormItem = new ReBuy();
            _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
            FormSubItems.Clear(); 
            FormPayments.Clear();
            SelectedInvoice = null; await LoadInvoicesAsync();
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
        
        CurrentSubItem = new ReBuySub 
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
        CurrentSubItem = new ReBuySub { BuyId = FormItem.BuyId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        _isUpdatingSubDiscount = true; SubItemQty = 1; SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0; _isUpdatingSubDiscount = false;
        ItemSearchText = string.Empty; CalculateTotals();
        CurrentItemUnits = [];
    }

    [RelayCommand] private void RemoveSubItem(ReBuySub sub) { if (sub != null && FormSubItems.Contains(sub)) { FormSubItems.Remove(sub); CalculateTotals(); } }

    private void CalculateTotals()
    {
        if (FormItem == null || _isUpdatingDiscount) return;
        _isUpdatingDiscount = true;
        FormItem.Total = FormSubItems.Sum(x => x.Total);
        if (FormItem.IsPer) { DiscountValueInput = Math.Round(FormItem.Total * (DiscountPercentInput / 100.0), 2); FormItem.Disc = DiscountValueInput; }
        else { DiscountPercentInput = FormItem.Total > 0 ? Math.Round((FormItem.Disc / FormItem.Total) * 100.0, 2) : 0; DiscountValueInput = FormItem.Disc; }
        CalculateTotalsInternal(); _isUpdatingDiscount = false;
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

        CurrentPayment = new ReBuyPayment
        {
            BuyId = FormItem.BuyId,
            PayDate = DateTime.Now,
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };
    }

    [RelayCommand]
    private void RemovePayment(ReBuyPayment payment)
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
            // حفظ الخزينة الحالية إذا كانت موجودة في الدفعة
            int existingCashId = FormPayments.FirstOrDefault()?.CashId ?? Cashes.FirstOrDefault()?.CashId ?? 0;
            
            FormPayments.Clear();
            FormPayments.Add(new ReBuyPayment
            {
                BuyId = FormItem.BuyId,
                PayDate = DateTime.Now,
                PayMoney = FormItem.Net,
                CashId = existingCashId,
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

    private ReBuy CloneInvoice(ReBuy s) => new()
    {
        BuyId = s.BuyId, BuyDate = s.BuyDate, SuppId = s.SuppId, Total = s.Total, Disc = s.Disc,
        DiscPer = s.DiscPer, AddMoney = s.AddMoney, Net = s.Net, IsPer = s.IsPer, NetPer = s.NetPer,
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
            foreach (ReBuySub sub in e.NewItems)
            {
                sub.PropertyChanged -= OnSubItemPropertyChanged;
                sub.PropertyChanged += OnSubItemPropertyChanged;
            }

        if (e.OldItems != null)
            foreach (ReBuySub sub in e.OldItems)
                sub.PropertyChanged -= OnSubItemPropertyChanged;
    }

    private void OnSubItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReBuySub.Total))
        {
            CalculateTotals();
        }
    }

    partial void OnSupplierSearchTextChanged(string value)
    {
        if (_isSelectingSupplier) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredSuppliersList = new ObservableCollection<Supplier>(Suppliers.Take(100));
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
}
