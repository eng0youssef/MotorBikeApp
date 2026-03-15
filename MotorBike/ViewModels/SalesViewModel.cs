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

public partial class SalesViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<Sale> _saleRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<Item> _itemRepository;
    private readonly IRepository<Store> _storeRepository;
    private readonly IRepository<Unit> _unitRepository;

    [ObservableProperty] private ObservableCollection<Customer> _customers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<Item> _items = [];
    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private ObservableCollection<Unit> _units = [];

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

    [ObservableProperty] private Sale? _selectedInvoice;
    [ObservableProperty] private SalesSub? _selectedSubItem;
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

    public SalesViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Sale> saleRepository,
        IRepository<Customer> customerRepository,
        IRepository<Cash> cashRepository,
        IRepository<Item> itemRepository,
        IRepository<Store> storeRepository,
        IRepository<Unit> unitRepository)
    {
        _dbFactory = dbFactory;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
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
            var customers = await _customerRepository.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers);

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
        }
        else
        {
            var lowerSearch = SearchText.ToLower();
            var filtered = Invoices.Where(i =>
                i.SalesId.ToString().Contains(lowerSearch) ||
                (Customers.FirstOrDefault(c => c.CusId == i.CusId)?.CusName?.ToLower().Contains(lowerSearch) == true)
            );
            FilteredInvoices = new ObservableCollection<Sale>(filtered);
        }
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

    partial void OnSelectedInvoiceChanged(Sale? value)
    {
        if (value is not null && !IsEditing)
        {
            IsSearchPanelVisible = false;
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

            LoadSubItemsAsync(value.SalesId).ConfigureAwait(false);
        }
    }

    private async Task LoadSubItemsAsync(int salesId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var subItems = await db.QueryAsync<SalesSub>("SELECT * FROM Sales_Sub WHERE SalesId = @SalesId", new { SalesId = salesId });
            FormSubItems = new ObservableCollection<SalesSub>(subItems);

            var payments = await db.QueryAsync<SalesPayment>("SELECT * FROM Sales_Payments WHERE SalesId = @SalesId", new { SalesId = salesId });
            FormPayments = new ObservableCollection<SalesPayment>(payments);
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
        var item = new Sale
        {
            SalesDate = DateTime.Now,
            CusId = Customers.FirstOrDefault()?.CusId ?? 0,
            AddPc = Environment.MachineName,
            AddDate = DateTime.Now
        };
        
        item.SalesId = await _saleRepository.GetNextIdAsync();

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
        FormPayments.Clear();
        TotalPayed = 0;
        
        CurrentSubItem = new SalesSub { SalesId = item.SalesId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;

        CurrentPayment = new SalesPayment { SalesId = item.SalesId, PayDate = DateTime.Now, CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
        
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
        FormItem = new Sale();
        
        _isUpdatingDiscount = true;
        DiscountPercentInput = 0;
        DiscountValueInput = 0;
        _isUpdatingDiscount = false;
        
        FormSubItems.Clear();
        FormPayments.Clear();
        TotalPayed = 0;
        CurrentSubItem = new SalesSub();
        CurrentPayment = new SalesPayment();
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        
        StatusMessage = null;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;

        if (FormItem.CusId <= 0)
        {
            StatusMessage = "⚠️ يجب اختيار العميل لإتمام حفظ الفاتورة.";
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
            
            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                if (_isInsertMode)
                {
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                        INSERT INTO Sales (Sales_ID, SalesDate, CusId, Total, Disc, AddMony, IsPer, IsCash, Notes, AddDate, AddPc, AddUser) 
                        VALUES (@SalesId, @SalesDate, @CusId, @Total, @Disc, @AddMony, @IsPer, @IsCash, @Notes, @AddDate, @AddPc, @AddUser)", FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                        UPDATE Sales SET SalesDate=@SalesDate, CusId=@CusId, Total=@Total,
                        Disc=@Disc, AddMony=@AddMony, IsPer=@IsPer, IsCash=@IsCash, Notes=@Notes, 
                        EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser
                        WHERE Sales_ID = @SalesId", FormItem, tx);
                        
                    // Delete old subItems and payments
                    await db.ExecuteAsync("DELETE FROM Sales_Sub WHERE SalesId = @SalesId", new { SalesId = FormItem.SalesId }, tx);
                    await db.ExecuteAsync("DELETE FROM Sales_Payments WHERE SalesId = @SalesId", new { SalesId = FormItem.SalesId }, tx);
                }

                int maxSubId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(ID), 0) FROM Sales_Sub", transaction: tx);

                foreach (var s in FormSubItems)
                {
                    s.Id = ++maxSubId;
                    s.SalesId = FormItem.SalesId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Sales_Sub (ID, SalesId, StoreId, ItemId, UnitId, Qty, Price, Disc, DiscPer, UnitQty) 
                        VALUES (@Id, @SalesId, @StoreId, @ItemId, @UnitId, @Qty, @Price, @Disc, @DiscPer, @UnitQty)", s, tx);
                }

                // Save payments
                int maxPayId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(Pay_ID), 0) FROM Sales_Payments", transaction: tx);
                foreach (var p in FormPayments)
                {
                    p.PayId = ++maxPayId;
                    p.SalesId = FormItem.SalesId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Sales_Payments (Pay_ID, PayDate, PayMoney, CashID, Notes, SalesID) 
                        VALUES (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @SalesId)", p, tx);
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
                await db.ExecuteAsync("DELETE FROM Sales_Payments WHERE SalesId = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
                await db.ExecuteAsync("DELETE FROM Sales_Sub WHERE SalesId = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
                await db.ExecuteAsync("DELETE FROM Sales WHERE Sales_ID = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }

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
            SelectedInvoice = null;
            await LoadInvoicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
        }
    }

    // --- Sub Items Management ---
    
    partial void OnItemSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            IsItemSearchPopupOpen = false;
            FilteredItemsList.Clear();
        }
        else
        {
            var lower = value.ToLower();
            var filtered = Items.Where(x => 
                x.ItemName.ToLower().Contains(lower) || 
                (x.Bar1 != null && x.Bar1.ToLower().Contains(lower)) ||
                (x.Bar2 != null && x.Bar2.ToLower().Contains(lower))
            ).Take(15);
            
            FilteredItemsList = new ObservableCollection<Item>(filtered);
            IsItemSearchPopupOpen = FilteredItemsList.Any();
        }
    }

    [RelayCommand]
    private void SelectItem(Item item)
    {
        if (item == null) return;
        
        CurrentSubItem = new SalesSub
        {
            SalesId = FormItem.SalesId,
            StoreId = CurrentSubItem.StoreId,
            ItemId = item.ItemId,
            UnitId = item.UnitId,
            Price = item.Price1 > 0 ? item.Price1 : item.Price0,
            Qty = 1,
            UnitQty = 1,
            QtyAll = 1,
            Total = (item.Price1 > 0 ? item.Price1 : item.Price0) * 1
        };
        
        _isUpdatingSubDiscount = true;
        SubItemPrice = CurrentSubItem.Price;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        _isUpdatingSubDiscount = false;
        
        ItemSearchText = item.ItemName;
        IsItemSearchPopupOpen = false;
    }

    [RelayCommand]
    private void AddSubItem()
    {
        if (CurrentSubItem.ItemId == 0 || CurrentSubItem.Qty <= 0) return;
        
        CurrentSubItem.Total = CurrentSubItem.Qty * (CurrentSubItem.Price - CurrentSubItem.Disc);
        
        FormSubItems.Add(CurrentSubItem);
        
        CurrentSubItem = new SalesSub { SalesId = FormItem.SalesId, StoreId = Stores.FirstOrDefault()?.StoreId ?? 0 };
        _isUpdatingSubDiscount = true;
        SubItemPrice = 0;
        SubItemDiscountPercent = 0;
        SubItemDiscountValue = 0;
        _isUpdatingSubDiscount = false;
        
        ItemSearchText = string.Empty;
        
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
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;

        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();

        CurrentPayment = new SalesPayment
        {
            SalesId = FormItem.SalesId,
            PayDate = DateTime.Now,
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };
    }

    [RelayCommand]
    private void RemovePayment(SalesPayment payment)
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
        FormItem.Net = FormItem.Total - FormItem.Disc + FormItem.AddMony;
        OnPropertyChanged(nameof(FormItem));
    }
    
    public void RecalculateTotals()
    {
        if (!_isUpdatingDiscount) CalculateTotals();
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
            AddUser = source.AddUser,
            AddDate = source.AddDate,
            AddPc = source.AddPc
        };
    }
}
