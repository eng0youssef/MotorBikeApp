using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ItemsViewModel : LookupViewModelBase<Item>
{
    private readonly IRepository<ItemCategory> _categoryRepo;
    private readonly IRepository<Unit> _unitRepo;
    private readonly IRepository<Store> _storeRepo;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty]
    private ObservableCollection<ItemCategory> _categories = [];

    partial void OnCategoriesChanged(ObservableCollection<ItemCategory> value) => RefreshFilteredItems();

    [ObservableProperty]
    private ObservableCollection<Unit> _units = [];

    [ObservableProperty]
    private ObservableCollection<Store> _stores = [];

    [ObservableProperty]
    private ObservableCollection<Unit> _itemFormUnits = [];

    [ObservableProperty]
    private double _openBalanceQty;

    [ObservableProperty]
    private int _openBalanceStoreId;

    [ObservableProperty]
    private ObservableCollection<OpenStock> _currentItemOpenStocks = [];

    // Fields for the detailed "Add Opening Stock" form in the UI
    [ObservableProperty] private DateTime _newOS_Date = System.DateTime.Now;
    [ObservableProperty] private int _newOS_StoreId;
    [ObservableProperty] private int _newOS_UnitId;
    [ObservableProperty] private double _newOS_Qty = 1;
    [ObservableProperty] private double _newOS_Price;
    [ObservableProperty] private double _newOS_DiscPer;
    [ObservableProperty] private double _newOS_Disc;
    
    // Computed property for the "Total" in the entry form
    public double NewOS_Total => (_newOS_Qty * _newOS_Price) - _newOS_Disc;

    private bool _isSyncingDisc;
    partial void OnNewOS_DiscPerChanged(double value) 
    {
        if (_isSyncingDisc) return;
        _isSyncingDisc = true;
        NewOS_Disc = (_newOS_Qty * _newOS_Price) * (value / 100);
        _isSyncingDisc = false;
        OnPropertyChanged(nameof(NewOS_Total));
    }

    partial void OnNewOS_DiscChanged(double value)
    {
        if (_isSyncingDisc) return;
        _isSyncingDisc = true;
        var totalBefore = _newOS_Qty * _newOS_Price;
        NewOS_DiscPer = totalBefore > 0 ? (value / totalBefore) * 100 : 0;
        _isSyncingDisc = false;
        OnPropertyChanged(nameof(NewOS_Total));
    }

    partial void OnNewOS_QtyChanged(double value) => UpdateOSDiscAndTotal();
    partial void OnNewOS_PriceChanged(double value) => UpdateOSDiscAndTotal();

    private void UpdateOSDiscAndTotal()
    {
        _isSyncingDisc = true;
        NewOS_Disc = (_newOS_Qty * _newOS_Price) * (_newOS_DiscPer / 100);
        _isSyncingDisc = false;
        OnPropertyChanged(nameof(NewOS_Total));
    }

    public ItemsViewModel(
        IRepository<Item> repository, 
        IRepository<ItemCategory> categoryRepo, 
        IRepository<Unit> unitRepo,
        IRepository<Store> storeRepo,
        CompositeKeyRepository compositeRepo) : base(repository) 
    { 
        _categoryRepo = categoryRepo;
        _unitRepo = unitRepo;
        _storeRepo = storeRepo;
        _compositeRepo = compositeRepo;
    }

    public async Task LoadLookupsAsync()
    {
        var cats = await _categoryRepo.GetAllAsync();
        Categories = new ObservableCollection<ItemCategory>(cats.Where(c => c.Active));

        var units = await _unitRepo.GetAllAsync();
        Units = new ObservableCollection<Unit>(units.Where(u => u.Active));

        var stores = await _storeRepo.GetAllAsync();
        Stores = new ObservableCollection<Store>(stores.Where(s => s.Active));

        if (Stores.Any())
        {
            OpenBalanceStoreId = Stores.First().StoreId;
            NewOS_StoreId = Stores.First().StoreId;
        }
    }
        
    public int FormUnitId
    {
        get => FormItem?.UnitId ?? 0;
        set
        {
            if (FormItem != null && FormItem.UnitId != value)
            {
                FormItem.UnitId = value;
                OnPropertyChanged(nameof(FormUnitId));
                UpdateItemFormUnits();
            }
        }
    }

    public int FormUnit2
    {
        get => FormItem?.Unit2 ?? 0;
        set
        {
            if (FormItem != null && FormItem.Unit2 != value)
            {
                FormItem.Unit2 = value;
                OnPropertyChanged(nameof(FormUnit2));
                UpdateItemFormUnits();
            }
        }
    }

    protected override async void OnFormItemChangedHook(Item value)
    {
        UpdateItemFormUnits();
        OnPropertyChanged(nameof(FormUnitId));
        OnPropertyChanged(nameof(FormUnit2));

        if (value != null && value.ItemId > 0)
        {
            // Load existing opening stocks for this item
            var osList = await _compositeRepo.GetAllOpenStockAsync();
            CurrentItemOpenStocks = new ObservableCollection<OpenStock>(osList.Where(x => x.ItemId == value.ItemId));
        }
        else
        {
            CurrentItemOpenStocks = [];
        }
        
        // Reset the "Add" form
        NewOS_Date = System.DateTime.Now;
        if (Stores.Any()) NewOS_StoreId = Stores.First().StoreId;
        NewOS_Qty = 1;
        NewOS_Price = value?.Price0 ?? 0;
        NewOS_DiscPer = 0;
        
        if (!ItemFormUnits.Any(u => u.UnitId == NewOS_UnitId))
        {
            NewOS_UnitId = ItemFormUnits.FirstOrDefault()?.UnitId ?? (value?.UnitId ?? 0);
        }
    }

    private void UpdateItemFormUnits()
    {
        var filtered = new System.Collections.Generic.List<Unit>();
        if (FormItem != null)
        {
            if (FormItem.UnitId > 0)
            {
                var u1 = Units.FirstOrDefault(u => u.UnitId == FormItem.UnitId);
                if (u1 != null && !filtered.Any(x => x.UnitId == u1.UnitId)) filtered.Add(u1);
            }
            if (FormItem.Unit2 > 0)
            {
                var u2 = Units.FirstOrDefault(u => u.UnitId == FormItem.Unit2);
                if (u2 != null && !filtered.Any(x => x.UnitId == u2.UnitId)) filtered.Add(u2);
            }
        }
        ItemFormUnits = new ObservableCollection<Unit>(filtered);

        if (!ItemFormUnits.Any(u => u.UnitId == NewOS_UnitId))
        {
            NewOS_UnitId = ItemFormUnits.FirstOrDefault()?.UnitId ?? 0;
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task AddOSAsync()
    {
        if (NewOS_StoreId == 0)
        {
            StatusMessage = "⚠️ يرجى تحديد المخزن أولاً";
            return;
        }
        if (NewOS_Qty <= 0)
        {
            StatusMessage = "⚠️ الكمية يجب أن تكون أكبر من صفر";
            return;
        }

        if (CurrentItemOpenStocks.Any(x => x.StoreId == NewOS_StoreId))
        {
            System.Windows.MessageBox.Show("تم ادخال الرصيد الافتتاحي الي المخزن المحدد من قبل", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        double unitQtyValue = 1.0;
        if (NewOS_UnitId == FormItem.Unit2 && FormItem.Unit2Qty > 0)
        {
            unitQtyValue = FormItem.Unit2Qty;
        }

        var os = new OpenStock
        {
            ItemId = FormItem.ItemId,
            StoreId = NewOS_StoreId,
            OpenDate = NewOS_Date,
            UnitId = NewOS_UnitId,
            Qty = NewOS_Qty,
            Price = NewOS_Price,
            DiscPer = NewOS_DiscPer,
            Disc = NewOS_Disc,
            UnitQty = unitQtyValue,
            QtyAll = NewOS_Qty * unitQtyValue,
            Total = (NewOS_Qty * NewOS_Price) - NewOS_Disc
        };

        if (FormItem.ItemId > 0)
        {
            // Existing item: Save to DB immediately
            try 
            {
                await _compositeRepo.InsertOpenStockAsync(os);
                CurrentItemOpenStocks.Add(os);
                StatusMessage = "✅ تم إضافة الرصيد بنجاح";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"❌ خطأ في الإضافة: {ex.Message}";
            }
        }
        else
        {
            // New item: Add to memory list
            CurrentItemOpenStocks.Add(os);
            StatusMessage = "📝 تم إضافة الرصيد للقائمة (سيتم الحفظ مع الصنف)";
        }
        
        // Reset partial form
        NewOS_Qty = 1;
    }

    public override async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormItem.ItemName))
        {
            StatusMessage = "⚠️ يرجى إدخال اسم الصنف أولاً";
            return;
        }

        if (FormItem.CatId == 0)
        {
            StatusMessage = "⚠️ يرجى اختيار مجموعة الصنف";
            return;
        }

        if (FormItem.UnitId > 0 && FormItem.UnitId == FormItem.Unit2)
        {
            StatusMessage = "⚠️ لا يمكن أن تكون الوحدة الكبرى هي نفسها الوحدة الصغرى";
            return;
        }

        if (FormItem.Unit2 > 0 && FormItem.Unit2Qty <= 0)
        {
            StatusMessage = "⚠️ يرجى إدخال العدد للوحدة الكبرى";
            return;
        }

        await base.SaveAsync();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task RemoveOSAsync(OpenStock os)
    {
        if (os == null) return;
        
        if (FormItem.ItemId > 0)
        {
            try 
            {
                await _compositeRepo.DeleteOpenStockAsync(os.StoreId, os.ItemId);
                CurrentItemOpenStocks.Remove(os);
                StatusMessage = "تم حذف الرصيد ✓";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"خطأ في الحذف: {ex.Message}";
            }
        }
        else
        {
            CurrentItemOpenStocks.Remove(os);
        }
    }

    /// <summary>
    /// بحث مخصص: يبحث في كود الصنف أو اسم الصنف أو اسم المجموعة أو الباركود.
    /// </summary>
    protected override void RefreshFilteredItems()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredItems = new System.Collections.ObjectModel.ObservableCollection<Item>(Items);
            return;
        }

        var lower = SearchText.Trim().ToLower();

        var filtered = Items.Where(item =>
        {
            // البحث بـ كود الصنف
            if (item.ItemId.ToString().Contains(lower))
                return true;

            // البحث بـ اسم الصنف
            if (item.ItemName?.ToLower().Contains(lower) == true)
                return true;

            // البحث بـ اسم المجموعة
            var cat = Categories.FirstOrDefault(c => c.CatId == item.CatId);
            if (cat?.CatName?.ToLower().Contains(lower) == true)
                return true;

            // البحث بـ الباركود
            if (item.Bar1?.ToLower().Contains(lower) == true)
                return true;
            if (item.Bar2?.ToLower().Contains(lower) == true)
                return true;

            return false;
        });

        FilteredItems = new System.Collections.ObjectModel.ObservableCollection<Item>(filtered);
    }

    protected override object GetEntityId(Item entity) => entity.ItemId;
    protected override bool IsNewRecord(Item entity) => entity.ItemId == 0;
    protected override void SetEntityId(Item entity, int id) => entity.ItemId = id;

    protected override void SetDefaultValues(Item entity)
    {
        base.SetDefaultValues(entity);
        entity.IsStock = true;
        OpenBalanceQty = 0;
        if (Stores.Any()) OpenBalanceStoreId = Stores.First().StoreId;
    }

    protected override async Task AfterSaveAsync(bool wasInsert)
    {
        // If it was a new item, save the collected opening stocks
        if (wasInsert && CurrentItemOpenStocks.Any())
        {
            foreach (var os in CurrentItemOpenStocks)
            {
                os.ItemId = FormItem.ItemId; // Assign the newly generated ID
                await _compositeRepo.InsertOpenStockAsync(os);
            }
        }
        
        // Legacy single field fallback (optional, could be removed now)
        else if (wasInsert && OpenBalanceQty > 0 && OpenBalanceStoreId > 0)
        {
            // ... (rest of old logic)
        }
    }
}
