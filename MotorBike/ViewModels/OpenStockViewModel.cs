using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class OpenStockViewModel : ObservableObject
{
    private readonly IRepository<Store> _storeRepo;
    private readonly IRepository<Item> _itemRepo;
    private readonly IRepository<Unit> _unitRepo;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty] private ObservableCollection<OpenStock> _items = [];
    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private ObservableCollection<Item> _itemList = [];
    [ObservableProperty] private ObservableCollection<Unit> _units = [];

    [ObservableProperty] private OpenStock _formItem = new();
    [ObservableProperty] private OpenStock? _selectedItem;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // رسالة خطأ معروضة بشكل مميز (تغيب تلقائياً بعد 3 ثوان)
    [ObservableProperty] private string _errorMessage = string.Empty;

    // الصنف المختار في الفورم — لمراقبة تغييره وجلب سعر الشراء وفلترة الوحدات
    [ObservableProperty] private Item? _selectedFormItem;

    // الوحدات المفلترة بحسب الصنف المختار (الأساسية + الثانوية فقط)
    [ObservableProperty] private ObservableCollection<Unit> _formUnits = [];

    // الوحدة المختارة في الفورم — لضبط عامل الوحدة تلقائياً
    [ObservableProperty] private Unit? _selectedFormUnit;

    // flag لمنع الكتابة على السعر/UnitQty عند تحميل سجل للتعديل
    private bool _skipAutoFill;

    public OpenStockViewModel(
        IRepository<Store> storeRepo,
        IRepository<Item> itemRepo,
        IRepository<Unit> unitRepo,
        CompositeKeyRepository compositeRepo)
    {
        _storeRepo = storeRepo;
        _itemRepo = itemRepo;
        _unitRepo = unitRepo;
        _compositeRepo = compositeRepo;
        SetDefaultValues();
    }

    public async Task LoadDataAsync()
    {
        try
        {
            var data = await _compositeRepo.GetAllOpenStockAsync();
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
        SelectedFormItem = null;
        SelectedFormUnit = null;
        FormUnits = [];
        FormItem = new OpenStock
        {
            OpenDate = DateTime.Now,
            Qty = 1,
            Price = 0,
            UnitQty = 1
        };

        if (Stores.Any()) FormItem.StoreId = Stores.First().StoreId;
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

        // ضبط الصنف المختار في الفورم — نمنع الكتابة التلقائية لأن السعر والوحدة محفوظان من السجل
        _skipAutoFill = true;
        SelectedFormItem = ItemList.FirstOrDefault(i => i.ItemId == SelectedItem.ItemId);
        // بعد فلترة الوحدات، نختار الوحدة الصحيحة
        SelectedFormUnit = FormUnits.FirstOrDefault(u => u.UnitId == SelectedItem.UnitId);
        _skipAutoFill = false;

        IsEditing = true;
        StatusMessage = "جاري تعديل الرصيد المحدد...";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

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

        // تحقق من عدم تكرار الصنف في نفس المخزن (فقط عند الإضافة)
        bool isNew = SelectedItem == null;
        if (isNew)
        {
            bool alreadyExists = Items.Any(x =>
                x.StoreId == FormItem.StoreId &&
                x.ItemId  == FormItem.ItemId);

            if (alreadyExists)
            {
                ErrorMessage = "الصنف الذي قمت باختياره موجود بالفعل في الرصيد الافتتاحي";
                return;
            }
        }

        try
        {
            // حساب الأعمدة المحسوبة
            FormItem.Total = (FormItem.Qty * FormItem.Price) - FormItem.Disc;
            FormItem.QtyAll = FormItem.Qty * FormItem.UnitQty;

            if (isNew)
            {
                await _compositeRepo.InsertOpenStockAsync(FormItem);
                StatusMessage = "تم إضافة الرصيد بنجاح.";
            }
            else
            {
                await _compositeRepo.UpdateOpenStockAsync(FormItem);
                StatusMessage = "تم تحديث الرصيد بنجاح.";
            }

            // إعادة حساب Stock للصنف
            await _compositeRepo.RecalcStockForItemAsync(FormItem.ItemId);

            // IsEditing = false; // Retain edit mode so user can save again
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ أثناء الحفظ. التفاصيل: {ex.Message}";
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
            var deletedItemId = SelectedItem.ItemId;
            await _compositeRepo.DeleteOpenStockAsync(SelectedItem.StoreId, SelectedItem.ItemId);

            // إعادة حساب Stock للصنف
            await _compositeRepo.RecalcStockForItemAsync(deletedItemId);

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

    /// <summary>
    /// عند تغيير الصنف: يجلب سعر الشراء، ويفلتر الوحدات المرتبطة بالصنف فقط
    /// </summary>
    partial void OnSelectedFormItemChanged(Item? value)
    {
        // فلترة الوحدات بحسب الصنف دائماً
        if (value == null)
        {
            FormUnits = [];
            SelectedFormUnit = null;
            return;
        }

        // بناء قائمة وحدات الصنف: الوحدة الأساسية + الثانوية (إن وُجدت)
        var filtered = Units.Where(u => u.UnitId == value.UnitId).ToList();
        if (value.Unit2 > 0)
        {
            var secondUnit = Units.FirstOrDefault(u => u.UnitId == value.Unit2);
            if (secondUnit != null) filtered.Add(secondUnit);
        }
        FormUnits = new ObservableCollection<Unit>(filtered);

        if (_skipAutoFill) return;

        // تعبئة سعر الشراء تلقائياً
        FormItem.ItemId = value.ItemId;
        FormItem.Price = value.Price0;

        // اختيار الوحدة الأساسية تلقائياً
        SelectedFormUnit = FormUnits.FirstOrDefault(u => u.UnitId == value.UnitId);

        OnPropertyChanged(nameof(FormItem));
    }

    /// <summary>
    /// عند تغيير الوحدة: يضبط عامل الوحدة والسعر تلقائياً
    /// - الوحدة الأساسية  → UnitQty = 1        ، Price = Price0
    /// - الوحدة الثانوية  → UnitQty = Unit2Qty  ، Price = Price0 × Unit2Qty
    /// مثال: قطعة بـ 5 جنيه، كارتون = 7 قطع → سعر الكارتون = 35
    /// </summary>
    partial void OnSelectedFormUnitChanged(Unit? value)
    {
        if (value == null || _skipAutoFill) return;

        FormItem.UnitId = value.UnitId;

        if (SelectedFormItem != null && value.UnitId == SelectedFormItem.Unit2)
        {
            // وحدة ثانوية: UnitQty = عدد الوحدات الأساسية فيها، والسعر = سعر × العدد
            FormItem.UnitQty = SelectedFormItem.Unit2Qty;
            FormItem.Price   = SelectedFormItem.Price0 * SelectedFormItem.Unit2Qty;
        }
        else
        {
            // وحدة أساسية: السعر الأصلي وعامل = 1
            FormItem.UnitQty = 1;
            FormItem.Price   = SelectedFormItem?.Price0 ?? FormItem.Price;
        }

        OnPropertyChanged(nameof(FormItem));
    }
}
