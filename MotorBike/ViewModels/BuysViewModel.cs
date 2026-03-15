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

public partial class BuysViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<Buy> _buyRepository;
    private readonly IRepository<Supplier> _supplierRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<Item> _itemRepository;
    private readonly IRepository<Store> _storeRepository;
    private readonly IRepository<Unit> _unitRepository;

    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<Item> _items = [];
    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private ObservableCollection<Unit> _units = [];

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

    [ObservableProperty] private Buy? _selectedInvoice;
    [ObservableProperty] private BuySub? _selectedSubItem;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;

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
        IRepository<Unit> unitRepository)
    {
        _dbFactory = dbFactory;
        _buyRepository = buyRepository;
        _supplierRepository = supplierRepository;
        _cashRepository = cashRepository;
        _itemRepository = itemRepository;
        _storeRepository = storeRepository;
        _unitRepository = unitRepository;
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
        if (value is not null && !IsEditing)
        {
            // Selected an invoice from search
            IsSearchPanelVisible = false;
            
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
            SuppId = Suppliers.FirstOrDefault()?.SuppId ?? 0,
            AddPc = Environment.MachineName,
            AddDate = DateTime.Now
        };
        
        item.BuyId = await _buyRepository.GetNextIdAsync();

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
        FormPayments.Clear();
        TotalPayed = 0;
        
        CurrentSubItem = new BuySub { BuyId = item.BuyId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;

        CurrentPayment = new BuyPayment { BuyId = item.BuyId, PayDate = DateTime.Now, CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
        
        StatusMessage = "فاتورة جديدة — أدخل البيانات ثم اضغط حفظ";
    }

    [RelayCommand]
    public void EditSelected()
    {
        if (SelectedInvoice is null) return;
        FormItem = CloneInvoice(SelectedInvoice);
        _isInsertMode = false;
        IsEditing = true;
        StatusMessage = "تعديل الفاتورة — غيّر البيانات ثم اضغط حفظ";
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
        CurrentSubItem = new BuySub();
        CurrentPayment = new BuyPayment();
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        
        StatusMessage = null;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;

        // مراجعة المنطق (Validation Logic)
        if (FormItem.SuppId <= 0)
        {
            StatusMessage = "⚠️ يجب اختيار المورد لإتمام حفظ الفاتورة.";
            return;
        }

        if (FormSubItems == null || !FormSubItems.Any())
        {
            StatusMessage = "⚠️ لا يمكن حفظ فاتورة شراء بدون إضافة أصناف.";
            return;
        }

        // Payed/CashId validation removed — payments now handled via Buy_Payments table

        try
        {
            CalculateTotals();
            
            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                if (_isInsertMode)
                {
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1; // Fallback to 1 if no user is logged in
                    await db.ExecuteAsync(@"
                        INSERT INTO Buy (Buy_ID, BuyDate, SuppId, Total, IsTax, VatTax, Tax, Disc, AddMoney, IsPer, IsCash, Notes, AddDate, AddPc, AddUser) 
                        VALUES (@BuyId, @BuyDate, @SuppId, @Total, @IsTax, @VatTax, @Tax, @Disc, @AddMoney, @IsPer, @IsCash, @Notes, @AddDate, @AddPc, @AddUser)", FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1; // Fallback to 1 if no user is logged in
                    await db.ExecuteAsync(@"
                        UPDATE Buy SET BuyDate=@BuyDate, SuppId=@SuppId, Total=@Total, IsTax=@IsTax, VatTax=@VatTax, Tax=@Tax,
                        Disc=@Disc, AddMoney=@AddMoney, IsPer=@IsPer, IsCash=@IsCash, Notes=@Notes, 
                        EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser
                        WHERE Buy_ID = @BuyId", FormItem, tx);
                        
                    // Delete old subItems and payments
                    await db.ExecuteAsync("DELETE FROM Buy_Sub WHERE BuyId = @BuyId", new { BuyId = FormItem.BuyId }, tx);
                    await db.ExecuteAsync("DELETE FROM Buy_Payments WHERE BuyId = @BuyId", new { BuyId = FormItem.BuyId }, tx);
                }

                int maxSubId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(ID), 0) FROM Buy_Sub", transaction: tx);

                foreach (var s in FormSubItems)
                {
                    s.Id = ++maxSubId;
                    s.BuyId = FormItem.BuyId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Buy_Sub (ID, BuyId, StoreId, ItemId, UnitId, Qty, Price, Disc, DiscPer, UnitQty) 
                        VALUES (@Id, @BuyId, @StoreId, @ItemId, @UnitId, @Qty, @Price, @Disc, @DiscPer, @UnitQty)", s, tx);
                }

                // Save payments
                int maxPayId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(Pay_ID), 0) FROM Buy_Payments", transaction: tx);
                foreach (var p in FormPayments)
                {
                    p.PayId = ++maxPayId;
                    p.BuyId = FormItem.BuyId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Buy_Payments (Pay_ID, PayDate, PayMoney, CashID, Notes, BuyID) 
                        VALUES (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @BuyId)", p, tx);
                }

                tx.Commit();
                StatusMessage = "تم الحفظ بنجاح ✓";
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            _isInsertMode = false;
            IsEditing = false;
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
            SelectedInvoice = null;
            await LoadInvoicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
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
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        _isUpdatingSubDiscount = false;
        
        ItemSearchText = string.Empty;
        
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
        if (payment != null && FormPayments.Contains(payment))
        {
            FormPayments.Remove(payment);
            CalculatePayedTotal();
        }
    }

    private void CalculatePayedTotal()
    {
        TotalPayed = FormPayments.Sum(p => p.PayMoney);
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
    }

    private void CalculateTotalsInternal()
    {
        // Net is a computed column in DB; calculate locally for display
        FormItem.Net = FormItem.Total - FormItem.Disc + FormItem.AddMoney;
        OnPropertyChanged(nameof(FormItem));
    }
    
    public void RecalculateTotals()
    {
        if (!_isUpdatingDiscount) CalculateTotals();
    }

    private Buy CloneInvoice(Buy source)
    {
        return new Buy
        {
            BuyId = source.BuyId,
            BuyDate = source.BuyDate,
            SuppId = source.SuppId,
            Total = source.Total,
            IsTax = source.IsTax,
            VatTax = source.VatTax,
            Tax = source.Tax,
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
            AddPc = source.AddPc
        };
    }
}
