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

    [ObservableProperty] private ObservableCollection<ReBuy> _invoices = [];
    [ObservableProperty] private ObservableCollection<ReBuy> _filteredInvoices = [];

    [ObservableProperty] private ReBuy _formItem = new();
    [ObservableProperty] private ObservableCollection<ReBuySub> _formSubItems = [];

    [ObservableProperty] private ReBuySub _currentSubItem = new();

    [ObservableProperty] private string _itemSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Item> _filteredItemsList = [];
    [ObservableProperty] private bool _isItemSearchPopupOpen;

    [ObservableProperty] private ReBuy? _selectedInvoice;
    [ObservableProperty] private ReBuySub? _selectedSubItem;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;

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
        set { if (SetProperty(ref _subItemPrice, value)) { if (CurrentSubItem != null) CurrentSubItem.Price = value; if (!_isUpdatingSubDiscount) { _isUpdatingSubDiscount = true; SubItemDiscountValue = Math.Round(value * (SubItemDiscountPercent / 100.0), 2); if (CurrentSubItem != null) CurrentSubItem.Disc = SubItemDiscountValue; _isUpdatingSubDiscount = false; } } }
    }

    private double _subItemDiscountPercent;
    public double SubItemDiscountPercent
    {
        get => _subItemDiscountPercent;
        set { if (SetProperty(ref _subItemDiscountPercent, value)) { if (_isUpdatingSubDiscount) return; _isUpdatingSubDiscount = true; if (CurrentSubItem != null) { CurrentSubItem.DiscPer = value; SubItemDiscountValue = Math.Round(CurrentSubItem.Price * (value / 100.0), 2); CurrentSubItem.Disc = SubItemDiscountValue; } _isUpdatingSubDiscount = false; } }
    }

    private double _subItemDiscountValue;
    public double SubItemDiscountValue
    {
        get => _subItemDiscountValue;
        set { if (SetProperty(ref _subItemDiscountValue, value)) { if (_isUpdatingSubDiscount) return; _isUpdatingSubDiscount = true; if (CurrentSubItem != null) { CurrentSubItem.Disc = value; SubItemDiscountPercent = CurrentSubItem.Price > 0 ? Math.Round((value / CurrentSubItem.Price) * 100.0, 2) : 0; CurrentSubItem.DiscPer = SubItemDiscountPercent; } _isUpdatingSubDiscount = false; } }
    }
    private bool _isUpdatingSubDiscount;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

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

    partial void OnSelectedInvoiceChanged(ReBuy? value)
    {
        if (value is not null && !IsEditing)
        {
            IsSearchPanelVisible = false;
            FormItem = CloneInvoice(value);
            _isUpdatingDiscount = true;
            if (FormItem.IsPer) { DiscountPercentInput = Math.Round(FormItem.DiscPer * 100.0, 2); DiscountValueInput = FormItem.Disc; }
            else { DiscountValueInput = FormItem.Disc; DiscountPercentInput = FormItem.Total > 0 ? Math.Round((FormItem.Disc / FormItem.Total) * 100.0, 2) : 0; }
            _isUpdatingDiscount = false;
            LoadSubItemsAsync(value.BuyId).ConfigureAwait(false);
        }
    }

    private async Task LoadSubItemsAsync(int buyId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            FormSubItems = new ObservableCollection<ReBuySub>(await db.QueryAsync<ReBuySub>("SELECT * FROM ReBuy_Sub WHERE BuyId = @BuyId", new { BuyId = buyId }));
            CalculateTotals();
        }
        catch (Exception ex) { StatusMessage = "خطأ في تحميل الأصناف: " + ex.Message; }
    }

    [RelayCommand]
    public async Task AddNewAsync()
    {
        var item = new ReBuy { BuyDate = DateTime.Now, SuppId = Suppliers.FirstOrDefault()?.SuppId ?? 0, AddPc = Environment.MachineName, AddDate = DateTime.Now, IsPer = true };
        item.BuyId = await _reBuyRepository.GetNextIdAsync();
        _isInsertMode = true; IsEditing = true; SelectedInvoice = null; FormItem = item;
        _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
        FormSubItems.Clear();
        CurrentSubItem = new ReBuySub { BuyId = item.BuyId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0;
        StatusMessage = "مرتجع جديد — أدخل البيانات ثم اضغط حفظ";
    }

    [RelayCommand] public void EditSelected() { if (SelectedInvoice is null) return; FormItem = CloneInvoice(SelectedInvoice); _isInsertMode = false; IsEditing = true; StatusMessage = "تعديل المرتجع — غيّر البيانات ثم اضغط حفظ"; }
    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false; IsEditing = false; FormItem = new ReBuy();
        _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
        FormSubItems.Clear(); CurrentSubItem = new ReBuySub(); SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0; StatusMessage = null;
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
            using var db = _dbFactory.CreateConnection(); db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                if (_isInsertMode)
                {
                    FormItem.AddPc ??= Environment.MachineName; FormItem.AddDate = DateTime.Now; FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"INSERT INTO ReBuy (Buy_ID, BuyDate, SuppId, Total, Disc, AddMoney, IsPer, IsCash, Notes, AddDate, AddPc, AddUser) VALUES (@BuyId, @BuyDate, @SuppId, @Total, @Disc, @AddMoney, @IsPer, @IsCash, @Notes, @AddDate, @AddPc, @AddUser)", FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName; FormItem.EditDate = DateTime.Now; FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"UPDATE ReBuy SET BuyDate=@BuyDate, SuppId=@SuppId, Total=@Total, Disc=@Disc, AddMoney=@AddMoney, IsPer=@IsPer, IsCash=@IsCash, Notes=@Notes, EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser WHERE Buy_ID = @BuyId", FormItem, tx);
                    await db.ExecuteAsync("DELETE FROM ReBuy_Sub WHERE BuyId = @BuyId", new { BuyId = FormItem.BuyId }, tx);
                }
                int maxSubId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(ID), 0) FROM ReBuy_Sub", transaction: tx);
                foreach (var s in FormSubItems) { s.Id = ++maxSubId; s.BuyId = FormItem.BuyId; await db.ExecuteAsync(@"INSERT INTO ReBuy_Sub (ID, BuyId, StoreId, ItemId, UnitId, Qty, Price, Disc, DiscPer, UnitQty) VALUES (@Id, @BuyId, @StoreId, @ItemId, @UnitId, @Qty, @Price, @Disc, @DiscPer, @UnitQty)", s, tx); }
                tx.Commit(); StatusMessage = "تم الحفظ بنجاح ✓";
            }
            catch { tx.Rollback(); throw; }

            // إعادة حساب Stock لكل الأصناف المتأثرة
            foreach (var itemId in FormSubItems.Select(s => s.ItemId).Distinct())
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            _isInsertMode = false; IsEditing = false; await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحفظ: {ex.Message}"; }
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
            try { await db.ExecuteAsync("DELETE FROM ReBuy_Sub WHERE BuyId = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx); await db.ExecuteAsync("DELETE FROM ReBuy WHERE Buy_ID = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx); tx.Commit(); }
            catch { tx.Rollback(); throw; }

            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            StatusMessage = "تم حذف المرتجع بنجاح ✓"; IsEditing = false; FormItem = new ReBuy();
            _isUpdatingDiscount = true; DiscountPercentInput = 0; DiscountValueInput = 0; _isUpdatingDiscount = false;
            FormSubItems.Clear(); SelectedInvoice = null; await LoadInvoicesAsync();
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
        _isUpdatingSubDiscount = true; SubItemPrice = 0; SubItemDiscountPercent = 0; SubItemDiscountValue = 0; _isUpdatingSubDiscount = false;
        ItemSearchText = string.Empty; CalculateTotals();
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

    private void CalculateTotalsInternal() { FormItem.Net = FormItem.Total - FormItem.Disc + FormItem.AddMoney; OnPropertyChanged(nameof(FormItem)); }
    public void RecalculateTotals() { if (!_isUpdatingDiscount) CalculateTotals(); }

    private ReBuy CloneInvoice(ReBuy s) => new()
    {
        BuyId = s.BuyId, BuyDate = s.BuyDate, SuppId = s.SuppId, Total = s.Total, Disc = s.Disc,
        DiscPer = s.DiscPer, AddMoney = s.AddMoney, Net = s.Net, IsPer = s.IsPer, NetPer = s.NetPer,
        IsCash = s.IsCash, Notes = s.Notes, AddUser = s.AddUser, AddDate = s.AddDate, AddPc = s.AddPc
    };
}
