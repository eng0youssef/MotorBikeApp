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

public partial class OpenStockViewModel : ObservableObject
{
    private readonly IRepository<OpenStock> _repository;
    private readonly IRepository<Store> _storeRepo;
    private readonly IRepository<Item> _itemRepo;
    private readonly IRepository<Unit> _unitRepo;
    private readonly IDbConnectionFactory _connectionFactory;

    [ObservableProperty] private ObservableCollection<OpenStock> _items = [];
    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private ObservableCollection<Item> _itemList = [];
    [ObservableProperty] private ObservableCollection<Unit> _units = [];

    [ObservableProperty] private OpenStock _formItem = new();
    [ObservableProperty] private OpenStock? _selectedItem;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public OpenStockViewModel(
        IRepository<OpenStock> repository,
        IRepository<Store> storeRepo,
        IRepository<Item> itemRepo,
        IRepository<Unit> unitRepo,
        IDbConnectionFactory connectionFactory)
    {
        _repository = repository;
        _storeRepo = storeRepo;
        _itemRepo = itemRepo;
        _unitRepo = unitRepo;
        _connectionFactory = connectionFactory;
        SetDefaultValues();
    }

    public async Task LoadDataAsync()
    {
        try
        {
            var data = await _repository.GetAllAsync();
            Items = new ObservableCollection<OpenStock>(data.OrderByDescending(x => x.OpenDate));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل الأرصدة: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var stores = await _storeRepo.GetAllAsync();
            Stores = new ObservableCollection<Store>(stores);

            var items = await _itemRepo.GetAllAsync();
            ItemList = new ObservableCollection<Item>(items);

            var units = await _unitRepo.GetAllAsync();
            Units = new ObservableCollection<Unit>(units);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    private void SetDefaultValues()
    {
        FormItem = new OpenStock
        {
            OpenDate = DateTime.Now,
            Qty = 1,
            Price = 0,
            UnitQty = 1
        };

        if (Stores.Any()) FormItem.StoreId = Stores.First().StoreId;
        if (ItemList.Any()) FormItem.ItemId = ItemList.First().ItemId;
        if (Units.Any()) FormItem.UnitId = Units.First().UnitId;
    }

    [RelayCommand]
    private void AddNew()
    {
        SetDefaultValues();
        IsEditing = true;
        StatusMessage = "جاري إضافة رصيد جديد...";
    }

    [RelayCommand]
    private void EditSelected()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "الرجاء تحديد سجل للتعديل.";
            return;
        }

        FormItem = new OpenStock
        {
            StoreId = SelectedItem.StoreId,
            ItemId = SelectedItem.ItemId,
            OpenDate = SelectedItem.OpenDate,
            UnitId = SelectedItem.UnitId,
            Qty = SelectedItem.Qty,
            Price = SelectedItem.Price,
            Disc = SelectedItem.Disc,
            DiscPer = SelectedItem.DiscPer,
            Total = SelectedItem.Total,
            UnitQty = SelectedItem.UnitQty,
            QtyAll = SelectedItem.QtyAll
        };
        IsEditing = true;
        StatusMessage = "جاري تعديل الرصيد المحدد...";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (FormItem.StoreId == 0 || FormItem.ItemId == 0)
        {
            StatusMessage = "يرجى تحديد المخزن والصنف.";
            return;
        }

        if (FormItem.Qty <= 0)
        {
            StatusMessage = "الكمية يجب أن تكون أكبر من صفر.";
            return;
        }

        try
        {
            // Calculate computed columns before saving
            FormItem.Total = (FormItem.Qty * FormItem.Price) - FormItem.Disc;
            FormItem.QtyAll = FormItem.Qty * FormItem.UnitQty;

            bool isNew = SelectedItem == null;
            using var db = _connectionFactory.CreateConnection();

            if (isNew)
            {
                var sql = @"INSERT INTO Open_Stock (Store_ID, Item_ID, Open_Date, Unit_ID, Qty, Price, Disc, Disc_Per, Total, UnitQty, QtyAll) 
                            VALUES (@StoreId, @ItemId, @OpenDate, @UnitId, @Qty, @Price, @Disc, @DiscPer, @Total, @UnitQty, @QtyAll)";
                await db.ExecuteAsync(sql, FormItem);
                StatusMessage = "تم إضافة الرصيد بنجاح.";
            }
            else
            {
                var sql = @"UPDATE Open_Stock SET Open_Date = @OpenDate, Unit_ID = @UnitId, Qty = @Qty, Price = @Price, 
                            Disc = @Disc, Disc_Per = @DiscPer, Total = @Total, UnitQty = @UnitQty, QtyAll = @QtyAll 
                            WHERE Store_ID = @StoreId AND Item_ID = @ItemId";
                await db.ExecuteAsync(sql, FormItem);
                StatusMessage = "تم تحديث الرصيد بنجاح.";
            }

            IsEditing = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ أثناء الحفظ. تأكد من عدم تكرار الصنف في نفس المخزن. التفاصيل: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "الرجاء تحديد سجل للحذف.";
            return;
        }

        try
        {
            using var db = _connectionFactory.CreateConnection();
            var sql = "DELETE FROM Open_Stock WHERE Store_ID = @StoreId AND Item_ID = @ItemId";
            await db.ExecuteAsync(sql, new { SelectedItem.StoreId, SelectedItem.ItemId });
            
            StatusMessage = "تم الحذف بنجاح.";
            IsEditing = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        SetDefaultValues();
        StatusMessage = "تم إلغاء العملية.";
    }

    partial void OnSelectedItemChanged(OpenStock? value)
    {
        if (value != null && !IsEditing)
        {
            // View mode, do nothing special
        }
    }
}
