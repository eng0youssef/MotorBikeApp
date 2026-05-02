using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ImportInvoiceViewModel : ObservableObject
{
    private readonly IRepository<ImportInvoice> _invoiceRepo;
    private readonly IRepository<ImportInvItem> _itemRepo;
    private readonly IRepository<ImportInvCar> _carRepo;
    private readonly IRepository<ImportExp> _expRepo;
    private readonly IRepository<ImportPayment> _paymentRepo;
    private readonly CompositeKeyRepository _compositeRepo;
    private readonly IDbConnectionFactory _dbFactory;

    // Lookup Repos
    private readonly IRepository<ImportSupplier> _supplierRepo;
    private readonly IRepository<Omla> _omlaRepo;
    private readonly IRepository<Cash> _cashRepo;
    private readonly IRepository<ImportExpense> _expenseLookupRepo;
    private readonly IRepository<Store> _storeRepo;
    private readonly IRepository<Unit> _unitRepo;
    private readonly IRepository<Item> _itemsLookupRepo;
    private readonly IRepository<Car> _carsLookupRepo;
    private readonly IRepository<CarModel> _carModelRepo;
    private readonly IRepository<Color> _colorRepo;

    public ImportInvoiceViewModel(
        IRepository<ImportInvoice> invoiceRepo,
        IRepository<ImportInvItem> itemRepo,
        IRepository<ImportInvCar> carRepo,
        IRepository<ImportExp> expRepo,
        IRepository<ImportPayment> paymentRepo,
        IRepository<ImportSupplier> supplierRepo,
        IRepository<Omla> omlaRepo,
        IRepository<Cash> cashRepo,
        IRepository<ImportExpense> expenseLookupRepo,
        IRepository<Store> storeRepo,
        IRepository<Unit> unitRepo,
        IDbConnectionFactory dbFactory,
        IRepository<Item> itemsLookupRepo,
        IRepository<Car> carsLookupRepo,
        IRepository<CarModel> carModelRepo,
        IRepository<Color> colorRepo,
        CompositeKeyRepository compositeRepo)
    {
        _invoiceRepo = invoiceRepo;
        _itemRepo = itemRepo;
        _carRepo = carRepo;
        _expRepo = expRepo;
        _paymentRepo = paymentRepo;
        _compositeRepo = compositeRepo;
        // في الـ parameters
        _dbFactory = dbFactory;
        _supplierRepo = supplierRepo;
        _omlaRepo = omlaRepo;
        _cashRepo = cashRepo;
        _expenseLookupRepo = expenseLookupRepo;
        _storeRepo = storeRepo;
        _unitRepo = unitRepo;
        _itemsLookupRepo = itemsLookupRepo;
        _carsLookupRepo = carsLookupRepo;
        _carModelRepo = carModelRepo;
        _colorRepo = colorRepo;

        FormItem = new ImportInvoice { InvDate = DateTime.Now };
    }

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isSearchPanelVisible;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // Tracks if current form item is a new record or modifying an existing one
    private bool _isNewInvoice = true;
    private int? _oldSuppId; // لتتبع المورد القديم عند التعديل

    [ObservableProperty] private ObservableCollection<ImportInvoice> _invoices = [];
    [ObservableProperty] private ObservableCollection<ImportInvoice> _filteredInvoices = [];
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(EditSelectedCommand))] [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private ImportInvoice? _selectedInvoice;

    [ObservableProperty] private ImportInvoice _formItem;

    // Sub-collections for the active FormItem
    [ObservableProperty] private ObservableCollection<ImportInvItem> _formItems = [];
    [ObservableProperty] private ObservableCollection<ImportInvCar> _formCars = [];
    [ObservableProperty] private ObservableCollection<ImportExp> _formExps = [];
    [ObservableProperty] private ObservableCollection<ImportPayment> _formPayments = [];

    // Selected items in sub-collections
    [ObservableProperty] private ImportInvItem? _selectedSubItem;
    [ObservableProperty] private ImportInvCar? _selectedSubCar;
    [ObservableProperty] private ImportExp? _selectedSubExp;
    [ObservableProperty] private ImportPayment? _selectedSubPayment;

    // Current Entry Objects for Adding to sub-collections
    [ObservableProperty] private ImportInvItem _currentSubItem = new();
    [ObservableProperty] private ImportInvCar _currentSubCar = new();
    [ObservableProperty] private ImportExp _currentSubExp = new() { PayDate = DateTime.Now };
    [ObservableProperty] private ImportPayment _currentSubPayment = new() { PayDate = DateTime.Now.AddSeconds(20) };

    [ObservableProperty] private int _currentSubItemUnitId;
    private Item? _selectedItemForSubItem;

    partial void OnCurrentSubItemUnitIdChanged(int value)
    {
        if (_isSelectingItem || _selectedItemForSubItem == null) return;
        
        double unitQty = 1;
        if (value > 0 && value == _selectedItemForSubItem.Unit2)
        {
            unitQty = _selectedItemForSubItem.Unit2Qty > 0 ? _selectedItemForSubItem.Unit2Qty : 1;
        }
        
        CurrentSubItem = new ImportInvItem
        {
            StoreId = CurrentSubItem.StoreId,
            ItemId = CurrentSubItem.ItemId,
            UnitId = value,
            Qty = CurrentSubItem.Qty,
            UnitQty = unitQty,
            Price = Math.Round(_selectedItemForSubItem.ImpPrice * unitQty, 4)
        };
    }

    // Lookups
    [ObservableProperty] private ObservableCollection<ImportSupplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Omla> _omlas = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];
    [ObservableProperty] private ObservableCollection<ImportExpense> _expenseTypes = [];
    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private ObservableCollection<Unit> _units = [];
    [ObservableProperty] private ObservableCollection<Item> _itemsList = [];
    [ObservableProperty] private ObservableCollection<Car> _carsList = [];
    [ObservableProperty] private ObservableCollection<CarModel> _carModels = [];
    [ObservableProperty] private ObservableCollection<Color> _colors = [];
    [ObservableProperty] private ObservableCollection<Cash> _filteredPaymentCashList = [];

    // New car entry fields
    [ObservableProperty] private string _newCarBrandName = string.Empty;
    [ObservableProperty] private string _newCarModelName = string.Empty;
    [ObservableProperty] private string _newCarColorName = string.Empty;

    [ObservableProperty] private ObservableCollection<CarBrand> _carBrands = [];
    [ObservableProperty] private ObservableCollection<CarModel> _filteredCarModels = [];

    partial void OnNewCarBrandNameChanged(string value)
    {
        UpdateFilteredModels();
    }

    private void UpdateFilteredModels()
    {
        if (string.IsNullOrWhiteSpace(NewCarBrandName))
        {
            FilteredCarModels = new ObservableCollection<CarModel>(CarModels);
            return;
        }

        var brand = CarBrands.FirstOrDefault(b => string.Equals(b.BrandName, NewCarBrandName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (brand != null)
        {
            FilteredCarModels = new ObservableCollection<CarModel>(CarModels.Where(m => m.BrandId == brand.BrandId));
        }
        else
        {
            FilteredCarModels = new ObservableCollection<CarModel>(CarModels);
        }
    }

    partial void OnNewCarModelNameChanged(string value)
    {
        var model = CarModels.FirstOrDefault(m => string.Equals(m.ModelName, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (model != null)
        {
            var brand = CarBrands.FirstOrDefault(b => b.BrandId == model.BrandId);
            if (brand != null)
            {
                NewCarBrandName = brand.BrandName;
            }
        }
    }

    [ObservableProperty] private short _newCarYearNo = (short)DateTime.Now.Year;
    [ObservableProperty] private string _newCarChassisNo = string.Empty;
    [ObservableProperty] private string _newCarMotorNo = string.Empty;
    [ObservableProperty] private string _newCarPlateNo = string.Empty;
    [ObservableProperty] private int _newCarMileage = 0;
    [ObservableProperty] private string _newCarNotes = string.Empty;
    [ObservableProperty] private double _newCarTotal;
    [ObservableProperty] private int? _newCarCC;

    // Selected car for editing
    [ObservableProperty] private ImportInvCar _selectedFormCar;
    [ObservableProperty] private bool _isEditingFormCar = false;

    partial void OnSelectedFormCarChanged(ImportInvCar value)
    {
        if (value == null) { IsEditingFormCar = false; return; }
        var realCar = value.Car ?? CarsList.FirstOrDefault(c => c.CarId == value.CarId);
        if (realCar == null) { IsEditingFormCar = false; return; }

        IsEditingFormCar = true;
        NewCarMileage = realCar.Mileage;
        NewCarChassisNo = realCar.ChassisNo;
        NewCarMotorNo = realCar.MotorNo;
        NewCarPlateNo = realCar.PlateNo;
        NewCarNotes = realCar.Notes;
        NewCarYearNo = realCar.YearNo;
        NewCarTotal = value.Total ?? 0;
        NewCarCC = realCar.CC;

        var model = CarModels.FirstOrDefault(m => m.ModelId == realCar.ModelId);
        NewCarModelName = model?.ModelName ?? string.Empty;
        if (model != null)
        {
            var brand = CarBrands.FirstOrDefault(b => b.BrandId == model.BrandId);
            NewCarBrandName = brand?.BrandName ?? string.Empty;
        }
        else
        {
            NewCarBrandName = string.Empty;
        }
        var color = Colors.FirstOrDefault(c => c.ColorId == realCar.ColorId);
        NewCarColorName = color?.ColorName ?? string.Empty;
    }

    [RelayCommand]
    public void CancelCarEdit()
    {
        IsEditingFormCar = false;
        SelectedFormCar = null;
        NewCarBrandName = string.Empty;
        NewCarModelName = string.Empty;
        NewCarColorName = string.Empty;
        NewCarChassisNo = string.Empty;
        NewCarMotorNo = string.Empty;
        NewCarPlateNo = string.Empty;
        NewCarNotes = string.Empty;
        NewCarYearNo = (short)DateTime.Now.Year;
        NewCarMileage = 0;
        NewCarTotal = 0;
    }

    [RelayCommand]
    public void UpdateSelectedCar()
    {
        if (SelectedFormCar == null) return;

        // Must capture before RemoveAt — DataGrid TwoWay binding will null SelectedFormCar when item is removed
        var carToRefresh = SelectedFormCar;
        var realCar = carToRefresh.Car ?? CarsList.FirstOrDefault(c => c.CarId == carToRefresh.CarId);
        if (realCar == null) return;

        // Update chassis, motor, plate, notes, mileage, year
        realCar.ChassisNo = NewCarChassisNo ?? string.Empty;
        realCar.MotorNo   = NewCarMotorNo   ?? string.Empty;
        realCar.PlateNo   = NewCarPlateNo   ?? string.Empty;
        realCar.Notes     = NewCarNotes     ?? string.Empty;
        realCar.Mileage = NewCarMileage;
        realCar.YearNo = NewCarYearNo;
        realCar.CC = NewCarCC;

        // Resolve model
        var modelName = NewCarModelName?.Trim() ?? string.Empty;
        var brandName = NewCarBrandName?.Trim() ?? string.Empty;
        var model = CarModels.FirstOrDefault(m => string.Equals(m.ModelName, modelName, StringComparison.OrdinalIgnoreCase) &&
            CarBrands.Any(b => b.BrandId == m.BrandId && string.Equals(b.BrandName, brandName, StringComparison.OrdinalIgnoreCase)));
        if (model != null) realCar.ModelId = model.ModelId;

        // Resolve color
        var colorName = NewCarColorName?.Trim() ?? string.Empty;
        var color = Colors.FirstOrDefault(c => string.Equals(c.ColorName, colorName, StringComparison.OrdinalIgnoreCase));
        if (color != null) realCar.ColorId = color.ColorId;

        // Update importCar fields using safe local reference
        carToRefresh.Total      = NewCarTotal;
        carToRefresh.Mileage    = NewCarMileage;
        carToRefresh.CarDetails = $"{NewCarBrandName} - {NewCarModelName} - {NewCarColorName} - {NewCarYearNo} - {NewCarChassisNo}";

        // Refresh DataGrid row — use carToRefresh because RemoveAt will null SelectedFormCar via binding
        var idx = FormCars.IndexOf(carToRefresh);
        if (idx >= 0) { FormCars.RemoveAt(idx); FormCars.Insert(idx, carToRefresh); }

        CalculateTotals(PercentageMode.None);
        CancelCarEdit();
        StatusMessage = "تم تحديث بيانات الموتوسيكل ✓";
    }

    // ── Smart Supplier Search ──────────────────────────────────────────────
    [ObservableProperty] private string _supplierSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<ImportSupplier> _filteredSuppliersList = [];
    [ObservableProperty] private bool _isSupplierSearchPopupOpen;
    private bool _isSelectingSupplier;

    partial void OnSupplierSearchTextChanged(string value)
    {
        if (_isSelectingSupplier) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredSuppliersList = new(Suppliers);
            IsSupplierSearchPopupOpen = FilteredSuppliersList.Any();
            return;
        }
        var keywords = value.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Suppliers.Where(s =>
        {
            var name = s.SuppName?.ToLower() ?? string.Empty;
            return keywords.All(k => name.Contains(k));
        });
        FilteredSuppliersList = new(filtered);
        IsSupplierSearchPopupOpen = FilteredSuppliersList.Any();
    }

    [RelayCommand]
    private void SelectSupplier(ImportSupplier supplier)
    {
        if (supplier == null) return;

        // ── التحقق من تغيير العملة ──────────────────────────────────────
        bool currencyChanging = FormItem.OmlaId > 0 && FormItem.OmlaId != supplier.OmlaId;

        if (currencyChanging && FormPayments.Any())
        {
            var result = MessageBox.Show(
                "تغيير المورد سيؤدي لتغيير العملة.\nالدفعات الحالية لن تُحذف لكن سيتم إزالة الخزينة المختارة من كل دفعة.\nيرجى اختيار الخزينة الجديدة المناسبة لكل دفعة بعد التغيير.\n\nهل تريد المتابعة؟",
                "تنبيه تغيير العملة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                // رجّع المورد القديم
                _isSelectingSupplier = true;
                var oldSupp = Suppliers.FirstOrDefault(s => s.SuppId == FormItem.SuppId);
                SupplierSearchText = oldSupp?.SuppName ?? string.Empty;
                IsSupplierSearchPopupOpen = false;
                _isSelectingSupplier = false;
                return;
            }

            // المستخدم وافق — شيل الخزينة من كل دفعة بدل ما نحذفها
            foreach (var pay in FormPayments)
            {
                pay.CashId = 0;
            }
        }

        // ── تعيين المورد ─────────────────────────────────────────────────
        _isSelectingSupplier = true;
        FormItem.SuppId = supplier.SuppId;
        SupplierSearchText = supplier.SuppName;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;

        // ── تحديث العملة وسعر الصرف والخزائن ─────────────────────────────
        FormItem.OmlaId = supplier.OmlaId;
        var omla = Omlas.FirstOrDefault(o => o.OmlaId == supplier.OmlaId);
        if (omla != null) FormItem.OmlaRate = omla.OmlaRate;
        SelectedOmlaId = supplier.OmlaId;
        FilteredPaymentCashList = new(CashList.Where(c => c.OmlaId == supplier.OmlaId));

        // تحديث عملة الدفعات الموجودة للعملة الجديدة
        foreach (var pay in FormPayments)
        {
            pay.OmlaId = supplier.OmlaId;
        }

        // إعادة تحميل جريد الدفعات لعرض التحديثات
        if (currencyChanging && FormPayments.Any())
        {
            var tempPayments = FormPayments.ToList();
            FormPayments = new(tempPayments);
            StatusMessage = "⚠️ تم تغيير العملة — يرجى اختيار الخزينة المناسبة لكل دفعة في جدول الدفعات";
        }

        OnPropertyChanged(nameof(FormItem));
        CalculateTotals();
    }

    // ── InvType (FOB / CIF) ────────────────────────────────────────────────
    public record InvTypeItem(byte Id, string Name);

    public List<InvTypeItem> InvTypeList { get; } =
    [
        new(1, "FOB"),
        new(2, "CIF")
    ];

    [ObservableProperty] private byte _selectedInvType = 1;

    partial void OnSelectedInvTypeChanged(byte value)
    {
        if (FormItem != null)
        {
            FormItem.InvType = value;
            OnPropertyChanged(nameof(FormItem));
        }
    }

    // ── Currency Rate (read-only, set from supplier) ─────────────────────
    [ObservableProperty] private byte _selectedOmlaId;

    // ── Smart Item Search ──────────────────────────────────────────────────
    [ObservableProperty] private string _itemSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Item> _filteredItemsList = [];
    [ObservableProperty] private bool _isItemSearchPopupOpen;
    private bool _isSelectingItem;

    // Units for current selected item only
    [ObservableProperty] private ObservableCollection<Unit> _currentItemUnits = [];

    partial void OnItemSearchTextChanged(string value)
    {
        if (_isSelectingItem) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredItemsList = new(ItemsList.Take(100));
            IsItemSearchPopupOpen = FilteredItemsList.Any();
            return;
        }
        var keywords = value.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = ItemsList.Where(item =>
            keywords.All(k =>
                (item.ItemName != null && item.ItemName.ToLower().Contains(k)) ||
                (item.Bar1 != null && item.Bar1.ToLower().Contains(k)) ||
                (item.Bar2 != null && item.Bar2.ToLower().Contains(k))
            )).Take(100);
        FilteredItemsList = new(filtered);
        IsItemSearchPopupOpen = FilteredItemsList.Any();
    }

    [RelayCommand]
    private void SelectItem(Item item)
    {
        if (item == null) return;
        _isSelectingItem = true;
        _selectedItemForSubItem = item;

        var itemUnits = Units.Where(u => (item.UnitId > 0 && u.UnitId == item.UnitId) || (item.Unit2 > 0 && u.UnitId == item.Unit2)).ToList();
        CurrentItemUnits = new(itemUnits);

        CurrentSubItem = new ImportInvItem
        {
            StoreId = CurrentSubItem.StoreId,
            ItemId = item.ItemId,
            UnitId = item.UnitId,
            Price = item.ImpPrice,
            Qty = 1,
            UnitQty = 1
        };
        CurrentSubItemUnitId = item.UnitId;

        ItemSearchText = item.ItemName ?? string.Empty;
        IsItemSearchPopupOpen = false;
        _isSelectingItem = false;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            var supp = await _supplierRepo.GetAllAsync(); Suppliers = new(supp.Where(x => x.Active));
            var omlas = await _omlaRepo.GetAllAsync(); Omlas = new(omlas.Where(x => x.Active));
            var cash = await _cashRepo.GetAllAsync(); CashList = new(cash.Where(x => x.Active));
            var exps = await _expenseLookupRepo.GetAllAsync(); ExpenseTypes = new(exps.Where(x => x.Active));
            var stores = await _storeRepo.GetAllAsync(); Stores = new(stores.Where(x => x.Active));
            var units = await _unitRepo.GetAllAsync(); Units = new(units.Where(x => x.Active));
            var items = await _itemsLookupRepo.GetAllAsync(); ItemsList = new(items.Where(x => x.Active));
            var cars = await _carsLookupRepo.GetAllAsync(); CarsList = new(cars);
            var models = await _carModelRepo.GetAllAsync(); CarModels = new(models.Where(x => x.Active));
            FilteredCarModels = new(CarModels);
            var colors = await _colorRepo.GetAllAsync(); Colors = new(colors.Where(x => x.Active));

            using var db = _dbFactory.CreateConnection();
            var brands = await db.QueryAsync<CarBrand>("SELECT * FROM CarBrands WHERE Active = 1");
            CarBrands = new(brands);

            var invs = await _invoiceRepo.GetAllAsync();
            Invoices = new(invs.OrderByDescending(x => x.InvDate));
            FilteredInvoices = new(Invoices);

            if (!Stores.Any() || !Units.Any() || !Omlas.Any() || !CashList.Any() || !ExpenseTypes.Any() || !Suppliers.Any())
            {
                StatusMessage = "يرجى التأكد من إدخال البيانات الأساسية (موردين، مخازن، وحدات، عملات، خزائن، مصروفات) أولاً.";
            }

            await AddNewRecord();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في التحميل: {ex.Message}";
        }
    }

    // ── Search & Filter ──────────────────────────────────────────────────

    [RelayCommand]
    public void ShowSearchPanel()
    {
        IsSearchPanelVisible = true;
        SearchText = string.Empty;
        FilteredInvoices = new(Invoices);
    }

    [RelayCommand]
    public void HideSearchPanel()
    {
        IsSearchPanelVisible = false;
        if (SelectedInvoice != null)
        {
            _ = LoadInvoiceDetailsAsync(SelectedInvoice.InvId);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            FilteredInvoices = new(Invoices);
        else
        {
            var lower = value.ToLower();
            FilteredInvoices = new(Invoices.Where(x => 
                x.InvId.ToString().Contains(lower) || 
                (x.InvName != null && x.InvName.ToLower().Contains(lower))));
        }
    }

    // ── CRUD Operations ──────────────────────────────────────────────────

    [RelayCommand]
    public async Task AddNewRecord()
    {
        _isNewInvoice = true;
        await Task.CompletedTask;
        FormItem = new ImportInvoice
        {
            InvId = 0,
            InvDate = DateTime.Now,
            OmlaRate = 1
        };
        FormItem.SuppId = 0;
        _oldSuppId = null;
        SelectedOmlaId = 0;

        _isSelectingSupplier = true;
        SupplierSearchText = string.Empty;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;

        FormItems.Clear();
        FormCars.Clear();
        FormExps.Clear();
        FormPayments.Clear();

        SelectedInvType = 1; // Default: FOB
        ResetSubEntries();
        IsEditing = true;
        SelectedInvoice = null;
        StatusMessage = "نموذج فاتورة استيراد جديد";
    }

    private void ResetSubEntries()
    {
        CurrentSubItem = new ImportInvItem();
        if (Stores.Any()) CurrentSubItem.StoreId = Stores.First().StoreId;
        if (Units.Any()) CurrentSubItem.UnitId = Units.First().UnitId;

        _isSelectingItem = true;
        ItemSearchText = string.Empty;
        IsItemSearchPopupOpen = false;
        CurrentItemUnits = [];
        _isSelectingItem = false;

        CurrentSubCar = new ImportInvCar();
        if (CarsList.Any()) CurrentSubCar.CarId = CarsList.First().CarId;

        NewCarBrandName = string.Empty;
        NewCarModelName = string.Empty;
        NewCarColorName = string.Empty;
        NewCarChassisNo = string.Empty;
        NewCarMotorNo = string.Empty;
        NewCarPlateNo = string.Empty;
        NewCarNotes = string.Empty;
        NewCarYearNo = (short)DateTime.Now.Year;
        NewCarMileage = 0;
        NewCarTotal = 0;
        NewCarCC = null;

        CurrentSubExp = new ImportExp { PayDate = DateTime.Now, OmlaRate = 1 };
        if (Omlas.Any()) CurrentSubExp.OmlaId = Omlas.First().OmlaId;
        if (CashList.Any()) CurrentSubExp.CashId = CashList.First().CashId;
        if (ExpenseTypes.Any()) CurrentSubExp.ExpId = ExpenseTypes.First().ExpId;

        CurrentSubPayment = new ImportPayment { PayDate = DateTime.Now, OmlaRate = 1 };
        if (Omlas.Any()) CurrentSubPayment.OmlaId = Omlas.First().OmlaId;
        if (CashList.Any()) CurrentSubPayment.CashId = CashList.First().CashId;
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task EditSelectedAsync()
    {
        if (SelectedInvoice == null) return;
        await LoadInvoiceDetailsAsync(SelectedInvoice.InvId);
        IsEditing = true;
    }

    [RelayCommand]
    public async Task CancelEdit()
    {
        IsEditing = false;
        if (SelectedInvoice != null)
            await LoadInvoiceDetailsAsync(SelectedInvoice.InvId);
        else
            await AddNewRecord();
    }

    private bool CanEditOrDelete() => SelectedInvoice != null;

    private async Task LoadInvoiceDetailsAsync(int invId)
    {
        _isNewInvoice = false;
        try
        {
            var inv = await _invoiceRepo.GetByIdAsync(invId);
            if (inv != null)
            {
                FormItem = inv;
                _oldSuppId = inv.SuppId;
                _isSelectingSupplier = true;
                SupplierSearchText = Suppliers.FirstOrDefault(s => s.SuppId == inv.SuppId)?.SuppName ?? string.Empty;
                IsSupplierSearchPopupOpen = false;
                _isSelectingSupplier = false;
                
                SelectedOmlaId = (byte)inv.OmlaId;
                SelectedInvType = inv.InvType > 0 ? inv.InvType : (byte)1;
                FilteredPaymentCashList = new(CashList.Where(c => c.OmlaId == inv.OmlaId));
            }

            var items = await _itemRepo.GetAllAsync();
            FormItems = new(items.Where(x => x.InvId == invId));

            var cars = await _carRepo.GetAllAsync();
            var invoiceCars = cars.Where(x => x.InvId == invId).ToList();
            foreach (var car in invoiceCars)
            {
                var realCar = CarsList.FirstOrDefault(c => c.CarId == car.CarId);
                if (realCar != null)
                {
                    var model = CarModels.FirstOrDefault(m => m.ModelId == realCar.ModelId);
                    string brandName = "";
                    string modelName = model?.ModelName ?? "";
                    if (model != null)
                    {
                        var brand = CarBrands.FirstOrDefault(b => b.BrandId == model.BrandId);
                        brandName = brand?.BrandName ?? "";
                    }
                    var color = Colors.FirstOrDefault(c => c.ColorId == realCar.ColorId);
                    string colorName = color?.ColorName ?? "";
                    
                    car.CarDetails = $"{brandName} - {modelName} - {colorName} - {realCar.YearNo} - {realCar.ChassisNo}";
                }
            }
            FormCars = new(invoiceCars);

            var exps = await _expRepo.GetAllAsync();
            FormExps = new(exps.Where(x => x.InvId == invId));

            var payments = await _paymentRepo.GetAllAsync();
            FormPayments = new(payments.Where(x => x.InvId == invId));

            CalculateTotals();
            StatusMessage = $"تم تحميل الفاتورة رقم {invId}";
            IsEditing = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل تفاصيل الفاتورة: {ex.Message}";
        }
    }

    // ── Adding Sub-Items ─────────────────────────────────────────────────

    [RelayCommand]
    public void AddSubItem()
    {
        if (CurrentSubItem.ItemId == 0 || CurrentSubItem.Qty <= 0)
        {
            StatusMessage = "يرجى تحديد صنف وكمية صحيحة.";
            return;
        }

        CurrentSubItem.Total = Math.Round(CurrentSubItem.Qty * CurrentSubItem.Price, 2);
        FormItems.Add(CurrentSubItem);
        
        var oldStoreId = CurrentSubItem.StoreId;
        var oldUnitId = CurrentSubItem.UnitId;
        
        CurrentSubItem = new ImportInvItem
        {
            StoreId = oldStoreId,
            UnitId = oldUnitId
        };
        
        _isSelectingItem = true;
        ItemSearchText = string.Empty;
        IsItemSearchPopupOpen = false;
        CurrentItemUnits = [];
        _isSelectingItem = false;

        CalculateTotals(PercentageMode.NewOnly);
    }

    [RelayCommand]
    public void RemoveSubItem(ImportInvItem item)
    {
        if (item != null && FormItems.Contains(item))
        {
            FormItems.Remove(item);
            CalculateTotals(PercentageMode.None);
        }
    }

    // ── Adding Sub-Cars ──────────────────────────────────────────────────

    [RelayCommand]
    public void AddSubCar()
    {
        if (CurrentSubCar.CarId == 0)
        {
            StatusMessage = "يرجى تحديد مركبة (موتوسيكل).";
            return;
        }

        FormCars.Add(CurrentSubCar);
        CurrentSubCar = new ImportInvCar();
        if (CarsList.Any()) CurrentSubCar.CarId = CarsList.First().CarId;
        CalculateTotals(PercentageMode.NewOnly);
    }

    [RelayCommand]
    public void RemoveSubCar(ImportInvCar car)
    {
        if (car != null && FormCars.Contains(car))
        {
            FormCars.Remove(car);
            CalculateTotals(PercentageMode.None);
        }
    }

    [RelayCommand]
    public async Task AddNewCarAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCarChassisNo) || string.IsNullOrWhiteSpace(NewCarModelName) || string.IsNullOrWhiteSpace(NewCarColorName))
        {
            StatusMessage = "يرجى إدخال الطراز، اللون، ورقم الشاسيه للاستمرار.";
            return;
        }

        try
        {
            using var db = _dbFactory.CreateConnection();
            db.Open();

            // Resolve or Create Color
            var colorName = NewCarColorName.Trim();
            var colorObj = Colors.FirstOrDefault(c => string.Equals(c.ColorName, colorName, StringComparison.OrdinalIgnoreCase));
            int colorId;
            if (colorObj != null)
            {
                colorId = colorObj.ColorId;
            }
            else
            {
                colorId = await _colorRepo.GetNextIdAsync();
                var newColor = new Color { ColorId = colorId, ColorName = colorName, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                await db.ExecuteAsync("INSERT INTO Colors (Color_ID, ColorName, Active, AddDate, AddPC, AddUser) VALUES (@ColorId, @ColorName, @Active, @AddDate, @AddPc, @AddUser)", newColor);
                Colors.Add(newColor);
            }

            // Resolve or Create Brand
            var brandName = string.IsNullOrWhiteSpace(NewCarBrandName) ? "بدون" : NewCarBrandName.Trim();
            var brandObj = CarBrands.FirstOrDefault(b => string.Equals(b.BrandName, brandName, StringComparison.OrdinalIgnoreCase));
            int brandId;
            if (brandObj != null)
            {
                brandId = brandObj.BrandId;
            }
            else
            {
                brandId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(Brand_ID), 0) + 1 FROM CarBrands");
                var newBrand = new CarBrand { BrandId = brandId, BrandName = brandName, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                await db.ExecuteAsync("INSERT INTO CarBrands (Brand_ID, BrandName, Active, AddDate, AddPC, AddUser) VALUES (@BrandId, @BrandName, @Active, @AddDate, @AddPc, @AddUser)", newBrand);
                CarBrands.Add(newBrand);
            }

            // Resolve or Create Model
            var modelName = NewCarModelName.Trim();
            var modelObj = CarModels.FirstOrDefault(m => string.Equals(m.ModelName, modelName, StringComparison.OrdinalIgnoreCase) && m.BrandId == brandId);
            int modelId;
            if (modelObj != null)
            {
                modelId = modelObj.ModelId;
            }
            else
            {
                modelId = await _carModelRepo.GetNextIdAsync();
                var newModel = new CarModel { ModelId = modelId, ModelName = modelName, BrandId = brandId, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                await db.ExecuteAsync("INSERT INTO CarModels (Model_ID, ModelName, BrandID, Active, AddDate, AddPC, AddUser) VALUES (@ModelId, @ModelName, @BrandId, @Active, @AddDate, @AddPc, @AddUser)", newModel);
                CarModels.Add(newModel);
                FilteredCarModels.Add(newModel);
            }

            int tempId = CarsList.Any(c => c.CarId < 0) ? CarsList.Min(c => c.CarId) - 1 : -1;

            var newCar = new Car
            {
                CarId = tempId,
                ModelId = modelId,
                YearNo = NewCarYearNo,
                ChassisNo = NewCarChassisNo,
                MotorNo = NewCarMotorNo ?? string.Empty,
                PlateNo = NewCarPlateNo ?? string.Empty,
                Mileage = NewCarMileage,
                CC = NewCarCC,
                ColorId = colorId,
                Notes = NewCarNotes,
                IsStock = true
            };
            CarsList.Add(newCar);

            var importCar = new ImportInvCar
            {
                CarId = newCar.CarId,
                Total = NewCarTotal,
                Mileage = NewCarMileage,
                Car = newCar,
                CarDetails = $"{brandName} - {modelName} - {colorName} - {NewCarYearNo} - {NewCarChassisNo}"
            };
            FormCars.Add(importCar);

            StatusMessage = $"تم إضافة الموتوسيكل للشحنة (شاسيه: {NewCarChassisNo}). سيتم تسجيله نهائياً عند حفظ الفاتورة.";
            
            NewCarBrandName = string.Empty;
            NewCarModelName = string.Empty;
            NewCarColorName = string.Empty;
            NewCarChassisNo = string.Empty;
            NewCarMotorNo = string.Empty;
            NewCarPlateNo = string.Empty;
            NewCarNotes = string.Empty;
            NewCarMileage = 0;
            NewCarTotal = 0;
            NewCarCC = null;

            CalculateTotals(PercentageMode.NewOnly);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في إضافة الموتوسيكل: {ex.Message}";
        }
    }

    // ── Adding Sub-Expenses ──────────────────────────────────────────────

    [RelayCommand]
    public void AddSubExp()
    {
        if (CurrentSubExp.ExpId == 0 || CurrentSubExp.PayTotal <= 0)
        {
            StatusMessage = "يرجى تحديد بند المصروف ومبلغ مالي.";
            return;
        }

        // تعيين سعر صرف الخزينة تلقائياً
        var cash = CashList.FirstOrDefault(c => c.CashId == CurrentSubExp.CashId);
        if (cash != null) CurrentSubExp.OmlaRate = cash.OmlaRate;

        FormExps.Add(CurrentSubExp);
        var oldCashId = CurrentSubExp.CashId;
        
        CurrentSubExp = new ImportExp { PayDate = DateTime.Now, OmlaRate = 1, CashId = oldCashId };
        if (Omlas.Any()) CurrentSubExp.OmlaId = Omlas.First().OmlaId;
        if (ExpenseTypes.Any()) CurrentSubExp.ExpId = ExpenseTypes.First().ExpId;
        CalculateTotals();
    }

    [RelayCommand]
    public void RemoveSubExp(ImportExp exp)
    {
        if (exp != null && FormExps.Contains(exp))
        {
            FormExps.Remove(exp);
            CalculateTotals();
        }
    }

    // ── Adding Sub-Payments ──────────────────────────────────────────────

    [RelayCommand]
    public async Task AddSubPayment()
    {
        if (CurrentSubPayment.PayMoney <= 0)
        {
            StatusMessage = "يرجى إدخال مبلغ دفع صحيح.";
            return;
        }

        if (CurrentSubPayment.CashId == 0)
        {
            StatusMessage = "يرجى اختيار الخزينة المسدد منها.";
            return;
        }

        // جلب سعر الصرف الحالي للخزينة كقيمة افتراضية للدفعة
        // لكن المستخدم يقدر يعدّل سعر الصرف بعد الإضافة من الجدول
        var liveCash = await _cashRepo.GetByIdAsync(CurrentSubPayment.CashId);
        if (liveCash != null) 
        {
            // نضع سعر الصرف الحالي للخزينة فقط لو المستخدم لم يعدّله يدوياً
            // (إذا كانت القيمة الافتراضية 1 أو لم يتم تغييرها)
            CurrentSubPayment.OmlaRate = liveCash.OmlaRate;
            
            // تحديث القائمة المحلية
            var localCash = CashList.FirstOrDefault(c => c.CashId == CurrentSubPayment.CashId);
            if (localCash != null) localCash.OmlaRate = liveCash.OmlaRate;
        }

        // تعيين عملة الدفعة من عملة الفاتورة
        CurrentSubPayment.OmlaId = FormItem.OmlaId;

        FormPayments.Add(CurrentSubPayment);
        var oldCashId = CurrentSubPayment.CashId;
        
        CurrentSubPayment = new ImportPayment { PayDate = FormItem.InvDate.AddSeconds(20), OmlaRate = 1, CashId = oldCashId };
        if (Omlas.Any()) CurrentSubPayment.OmlaId = FormItem.OmlaId;

        CalculateFrokOmla();
        CalculateTotals();
    }

    [RelayCommand]
    public void RemoveSubPayment(ImportPayment payment)
    {
        if (payment != null && FormPayments.Contains(payment))
        {
            FormPayments.Remove(payment);
            CalculateFrokOmla();
            CalculateTotals();
        }
    }

    /// <summary>
    /// يُستدعى عند تعديل خلية في جدول الدفعات (المبلغ، سعر الصرف، أو الخزينة)
    /// لإعادة حساب فرق العملات والإجماليات
    /// </summary>
    public void RecalculatePaymentTotals()
    {
        CalculateFrokOmla();
        CalculateTotals();
    }

    /// <summary>
    /// حساب فرق العملات تلقائياً:
    /// FrokOmla = مجموع كل دفعة × (سعر صرف الخزينة وقت الدفع - سعر صرف الفاتورة)
    /// </summary>
    private void CalculateFrokOmla()
    {
        if (FormItem == null) return;
        double invRate = (double)FormItem.OmlaRate;
        double frok = 0;
        foreach (var pay in FormPayments)
        {
            double payRate = (double)pay.OmlaRate;
            frok += pay.PayMoney * (payRate - invRate);
        }
        FormItem.FrokOmla = Math.Round(frok, 2);
        OnPropertyChanged(nameof(FormItem));
    }

    // ── Calculations ─────────────────────────────────────────────────────

    /// <summary>أوضاع حساب النسبة المئوية</summary>
    private enum PercentageMode
    {
        /// <summary>لا تغيّر أي نسب (تحديث التوتال والتكاليف فقط)</summary>
        None,
        /// <summary>احسب النسبة فقط لآخر صنف/موتوسيكل مضاف (بدون المساس بالنسب القائمة)</summary>
        NewOnly,
        /// <summary>أعِد حساب كل النسب لكل الأصناف والموتوسيكلات</summary>
        All
    }

    public void RecalculateTotalsFromGrid(bool isPercentageEdit)
    {
        CalculateTotals(isPercentageEdit ? PercentageMode.None : PercentageMode.All);
    }

    /// <summary>
    /// زرار "إعادة حساب النسبة % تلقائي" — يعيد حساب النسبة لكل الأصناف والموتوسيكلات
    /// </summary>
    [RelayCommand]
    public void RecalcAllPercentages()
    {
        CalculateTotals(PercentageMode.All);
        StatusMessage = "تم إعادة حساب جميع النسب تلقائياً ✓";
    }

    private void CalculateTotals(PercentageMode percentageMode = PercentageMode.None)
    {
        if (FormItem == null) return;

        double omlaRate = (double)FormItem.OmlaRate;
        if (omlaRate <= 0) omlaRate = 1;

        // 1) حساب Total و QtyAll لكل صنف محلياً (بالعملة الأجنبية)
        foreach (var item in FormItems)
        {
            item.Total = Math.Round(item.Qty * item.Price, 2);
            item.QtyAll = Math.Round(item.Qty * (item.UnitQty > 0 ? item.UnitQty : 1), 2);
        }

        // 2) إجمالي الفاتورة (بالعملة الأجنبية)
        double itemsTotal = FormItems.Sum(x => x.Total);
        double carsTotal = FormCars.Sum(x => x.Total ?? 0);
        FormItem.InvTotal = Math.Round(itemsTotal + carsTotal, 2);

        // 3) إجمالي المصروفات = كل مصروف × سعر صرف خزينته (بالعملة المحلية)
        double expTotal = 0;
        foreach (var exp in FormExps)
        {
            var cash = CashList.FirstOrDefault(c => c.CashId == exp.CashId);
            decimal cashRate = cash?.OmlaRate ?? 1m;
            expTotal += exp.PayTotal * (double)cashRate;
        }
        FormItem.ExpTotal = Math.Round(expTotal, 2);

        // حساب فرق العملة من المدفوعات:
        // لكل دفعة: (سعر صرف الدفعة - سعر صرف الفاتورة) * المبلغ المدفوع
        double calculatedFrokOmla = 0;
        foreach (var pay in FormPayments)
        {
            calculatedFrokOmla += pay.PayMoney * (double)(pay.OmlaRate - (decimal)omlaRate);
        }
        FormItem.FrokOmla = Math.Round(calculatedFrokOmla, 2);

        // 4) التكلفة الإجمالية بالعملة المحلية:
        //    (إجمالي الفاتورة × سعر الصرف) + المصروفات + فرق العملات
        double invTotalLocal = FormItem.InvTotal * omlaRate;
        FormItem.TotalCost = Math.Round(invTotalLocal + FormItem.ExpTotal + FormItem.FrokOmla, 2);

        // 5) توزيع التكاليف على الأصناف والموتوسيكلات
        double totalCost = FormItem.TotalCost ?? 0;
        double invTotal = FormItem.InvTotal;
        if (invTotal > 0 && totalCost > 0)
        {
            if (percentageMode == PercentageMode.All)
            {
                // إعادة حساب النسبة لكل الأصناف والموتوسيكلات
                foreach (var item in FormItems)
                {
                    item.CostPer = Math.Round((decimal)(item.Total / invTotal) * 100m, 4);
                }
                foreach (var car in FormCars)
                {
                    double carTotal = car.Total ?? 0;
                    car.CostPer = Math.Round((decimal)(carTotal / invTotal) * 100m, 4);
                }
            }
            else if (percentageMode == PercentageMode.NewOnly)
            {
                // حساب النسبة فقط لآخر عنصر مضاف (آخر صنف أو آخر موتوسيكل)
                // بدون تغيير النسب القائمة التي عدّلها المستخدم يدوياً
                var lastItem = FormItems.LastOrDefault();
                var lastCar = FormCars.LastOrDefault();

                // نحدد مين اللي اتضاف آخر مرة: لو عدد الأصناف أو الموتوسيكلات اتغير...
                // هنحسب نسبة آخر صنف لو آخر عنصر مضاف كان صنف
                // ونحسب نسبة آخر موتوسيكل لو آخر عنصر مضاف كان موتوسيكل
                // الطريقة: نحسب النسبة لأي عنصر نسبته لسه صفر (جديد)
                foreach (var item in FormItems)
                {
                    if (item.CostPer == 0m)
                    {
                        item.CostPer = Math.Round((decimal)(item.Total / invTotal) * 100m, 4);
                    }
                }
                foreach (var car in FormCars)
                {
                    if (car.CostPer == 0m)
                    {
                        double carTotal = car.Total ?? 0;
                        car.CostPer = Math.Round((decimal)(carTotal / invTotal) * 100m, 4);
                    }
                }
            }
            // PercentageMode.None → لا تعدّل أي نسب

            double expensesOnly = FormItem.ExpTotal + FormItem.FrokOmla;

            foreach (var item in FormItems)
            {
                item.TotalLocal = Math.Round(item.Total * omlaRate, 2);
                item.ExpShareLocal = Math.Round(expensesOnly * (double)(item.CostPer / 100m), 2);
                item.CostTotal = Math.Round(totalCost * (double)(item.CostPer / 100m), 2);
                item.CostUnit = item.QtyAll > 0
                    ? Math.Round(item.CostTotal.Value / item.QtyAll, 2)
                    : 0;
            }

            foreach (var car in FormCars)
            {
                double carTotal = car.Total ?? 0;
                car.TotalLocal = Math.Round(carTotal * omlaRate, 2);
                car.ExpShareLocal = Math.Round(expensesOnly * (double)(car.CostPer / 100m), 2);
                car.CostTotal = Math.Round(totalCost * (double)(car.CostPer / 100m), 2);
            }
        }

        OnPropertyChanged(nameof(FormItem));
    }

    // ── Save/Delete ──────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SaveAsync()
    {
        var requiredAbility = _isNewInvoice ? AppAbility.Add : AppAbility.Edit;
        if (!AppSession.HasPermission(ScreenId.ImportInvoice, requiredAbility))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية لإجراء هذه العملية.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        if (FormItem.SuppId == 0)
        {
            StatusMessage = "يرجى اختيار المورد.";
            return;
        }

        CalculateTotals(); // Only recalculate totals without overriding user's manual CostPer percentages

        if (FormItems.Any() || FormCars.Any())
        {
            decimal totalCostPer = FormItems.Sum(x => x.CostPer) + FormCars.Sum(x => x.CostPer);
            if (Math.Abs(totalCostPer - 100m) > 0.01m)
            {
                StatusMessage = $"مجموع النسب المئوية يجب أن يكون 100% (الحالي: {totalCostPer:N4}%). يرجى مراجعتها وتعديلها.";
                return;
            }
        }

        // التحقق من أن كل دفعة فيها خزينة مختارة
        if (FormPayments.Any(p => p.CashId == 0))
        {
            StatusMessage = "⚠️ يوجد دفعات بدون خزينة محددة. يرجى اختيار الخزينة لكل دفعة قبل الحفظ.";
            return;
        }

        try
        {
            bool isNew = _isNewInvoice;
            if (isNew)
            {
                if (FormItem.InvId == 0) // Just in case it wasn't populated
                    FormItem.InvId = await _invoiceRepo.GetNextIdAsync();

                FormItem.AddDate = DateTime.Now;
                FormItem.AddPc = Environment.MachineName;
                FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                await _invoiceRepo.InsertAsync(FormItem);
                
                _isNewInvoice = false; // Next save will update instead of inserting
            }
            else
            {
                FormItem.EditDate = DateTime.Now;
                FormItem.EditPc = Environment.MachineName;
                FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                await _invoiceRepo.UpdateAsync(FormItem);
                
                var existingItems = (await _itemRepo.GetAllAsync()).Where(x => x.InvId == FormItem.InvId);
                foreach (var i in existingItems) await _itemRepo.DeleteAsync(i.Id);

                var existingCars = (await _carRepo.GetAllAsync()).Where(x => x.InvId == FormItem.InvId);
                foreach (var c in existingCars) await _carRepo.DeleteAsync(c.Id);

                var existingExps = (await _expRepo.GetAllAsync()).Where(x => x.InvId == FormItem.InvId);
                foreach (var e in existingExps) await _expRepo.DeleteAsync(e.Id);

                var existingPayments = (await _paymentRepo.GetAllAsync()).Where(x => x.InvId == FormItem.InvId);
                foreach (var p in existingPayments) await _paymentRepo.DeleteAsync(p.PayId);
            }

            // Insert new sub-items
            int nextItemId = Math.Max(await _itemRepo.GetNextIdAsync(), FormItems.Any() ? FormItems.Max(i => i.Id) + 1 : 1);
            foreach (var item in FormItems)
            {
                if (item.Id == 0) item.Id = nextItemId++;
                item.InvId = FormItem.InvId;
                await _itemRepo.InsertAsync(item);
            }

            int nextCarId = await _carsLookupRepo.GetNextIdAsync(); // For actual Car records
            int nextImportCarId = Math.Max(await _carRepo.GetNextIdAsync(), FormCars.Any() ? FormCars.Max(c => c.Id) + 1 : 1);
            foreach (var car in FormCars) 
            {
                if (car.Id == 0) car.Id = nextImportCarId++;
                
                // If it's a completely new car (we assigned a negative temp ID)
                if (car.CarId < 0 && car.Car != null)
                {
                    car.Car.CarId = nextCarId++;
                    car.Car.AddDate = DateTime.Now;
                    car.Car.AddPc = Environment.MachineName;
                    car.Car.AddUser = AppSession.CurrentUserId ?? 1;
                    car.Car.SupplierId = FormItem.SuppId;
                    car.Car.IsLocalSupplier = false;
                    car.Car.IsFromCustomer = false;
                    car.Car.SourceCustomerId = null;
                    car.Car.StatusId = 1;
                    car.Car.OwnerId = null;
                    car.Car.PurchasePrice = (double)(car.CostTotal ?? 0);
                    await _carsLookupRepo.InsertAsync(car.Car);
                    car.CarId = car.Car.CarId;
                }
                else if (car.CarId > 0 && car.Car != null && (car.Car.IsLocalSupplier == false))
                {
                    // Update the final local cost for an existing imported car
                    car.Car.PurchasePrice = (double)(car.CostTotal ?? 0);
                    car.Car.EditDate = DateTime.Now;
                    car.Car.EditPc = Environment.MachineName;
                    car.Car.EditUser = AppSession.CurrentUserId ?? 1;
                    await _carsLookupRepo.UpdateAsync(car.Car);
                }

                car.InvId = FormItem.InvId;
                await _carRepo.InsertAsync(car);
            }

            int nextExpId = Math.Max(await _expRepo.GetNextIdAsync(), FormExps.Any() ? FormExps.Max(e => e.Id) + 1 : 1);
            foreach (var exp in FormExps)
            {
                if (exp.Id == 0) exp.Id = nextExpId++;
                exp.InvId = FormItem.InvId;
                exp.AddDate = DateTime.Now;
                exp.AddPc = Environment.MachineName;
                exp.AddUser = AppSession.CurrentUserId ?? 1;
                await _expRepo.InsertAsync(exp);
            }

            int nextPayId = Math.Max(await _paymentRepo.GetNextIdAsync(), FormPayments.Any() ? FormPayments.Max(p => p.PayId) + 1 : 1);
            foreach (var pay in FormPayments)
            {
                pay.PayDate = FormItem.InvDate.AddSeconds(20);
                if (pay.PayId == 0) pay.PayId = nextPayId++;
                pay.InvId = FormItem.InvId;
                pay.SuppId = FormItem.SuppId;
                pay.AddDate = DateTime.Now;
                pay.AddPc = Environment.MachineName;
                pay.AddUser = AppSession.CurrentUserId ?? 1;
                await _paymentRepo.InsertAsync(pay);
            }

            // ── إعادة حساب الأرصدة بعد الحفظ ──────────────────────────

            // 1) إعادة حساب المخزون لكل صنف متأثر
            var affectedItemIds = FormItems.Select(x => x.ItemId).Distinct().ToList();
            if (!isNew)
            {
                // أضف الأصناف القديمة اللي ممكن تكون اتحذفت من الفاتورة
                var oldItems = (await _itemRepo.GetAllAsync()).Where(x => x.InvId == FormItem.InvId);
                foreach (var oi in oldItems)
                    if (!affectedItemIds.Contains(oi.ItemId)) affectedItemIds.Add(oi.ItemId);
            }
            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            // 1b) إعادة حساب متوسط التكلفة لكل صنف متأثر ابتداءً من تاريخ الفاتورة
            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcAvgCostForItemAsync(itemId, FormItem.InvDate.Date);

            // 2) إعادة حساب رصيد مورد الاستيراد
            if (!isNew && _oldSuppId.HasValue && _oldSuppId.Value != FormItem.SuppId)
                await _compositeRepo.RecalcBalanceForImportSupplierAsync(_oldSuppId.Value);
            await _compositeRepo.RecalcBalanceForImportSupplierAsync(FormItem.SuppId);

            // 3) إعادة حساب رصيد كل خزينة متأثرة (من المدفوعات + المصروفات)
            var affectedCashIds = FormPayments.Select(p => p.CashId).Distinct().ToList();
            foreach (var exp in FormExps)
                if (!affectedCashIds.Contains(exp.CashId)) affectedCashIds.Add(exp.CashId);
            if (!isNew)
            {
                var oldPayments = (await _paymentRepo.GetAllAsync()).Where(x => x.InvId == FormItem.InvId);
                foreach (var op in oldPayments)
                    if (!affectedCashIds.Contains(op.CashId)) affectedCashIds.Add(op.CashId);
                var oldExps = (await _expRepo.GetAllAsync()).Where(x => x.InvId == FormItem.InvId);
                foreach (var oe in oldExps)
                    if (!affectedCashIds.Contains(oe.CashId)) affectedCashIds.Add(oe.CashId);
            }
            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            StatusMessage = "تم حفظ فاتورة الاستيراد بنجاح ✓ ";
            var invs = await _invoiceRepo.GetAllAsync();
            Invoices = new(invs.OrderByDescending(x => x.InvDate));
            FilteredInvoices = new(Invoices);
            var savedInvoice = Invoices.FirstOrDefault(x => x.InvId == FormItem.InvId);
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
    private async Task PrintInvoiceAsync()
    {
        // ── Guard: لازم يكون فيه فاتورة محفوظة ──────────────────────────────
        if (FormItem == null || FormItem.InvId <= 0)
        {
            MessageBox.Show(
                "يجب حفظ الفاتورة أو اختيار فاتورة أولاً لطباعتها.",
                "تنبيه",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!FormItems.Any() && !FormCars.Any())
        {
            MessageBox.Show(
                "الفاتورة لا تحتوي على أصناف أو موتوسيكلات.",
                "تنبيه",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            // ── 1) جلب بيانات الشركة ─────────────────────────────────────────
            Company? company;
            using (var db = _dbFactory.CreateConnection())
            {
                company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
            }

            // ── 2) بناء الـ model الرئيسي ─────────────────────────────────────
            var omla = Omlas.FirstOrDefault(o => o.OmlaId == FormItem.OmlaId);

            var model = new MotorBike.Services.ImportInvoiceModel
            {
                InvoiceNo = FormItem.InvId.ToString(),
                InvName = FormItem.InvName ?? string.Empty,
                IssueDate = FormItem.InvDate.ToString("yyyy-MM-dd"),
                SupplierName = Suppliers.FirstOrDefault(s => s.SuppId == FormItem.SuppId)?.SuppName ?? string.Empty,
                MadeIn = FormItem.MadeIn ?? string.Empty,
                ShipPort = FormItem.ShipPort ?? string.Empty,
                InvType = FormItem.InvType == 2 ? "CIF" : "FOB",
                OmlaName = omla?.OmlaName ?? string.Empty,
                OmlaRate = ((double)FormItem.OmlaRate).ToString("N4"),
                Notes = FormItem.Notes ?? string.Empty,

                InvTotal = FormItem.InvTotal ,
                InvTotalLocal = FormItem.InvTotalLocal ,
                ExpTotal = FormItem.ExpTotal ,
                FrokOmla = FormItem.FrokOmla ,
                TotalCost = FormItem.TotalCost ?? 0,
            };

            // ── 3) الأصناف ────────────────────────────────────────────────────
            foreach (var item in FormItems)
            {
                var itemName = ItemsList.FirstOrDefault(i => i.ItemId == item.ItemId)?.ItemName ?? string.Empty;
                model.Items.Add(new MotorBike.Services.ImportInvoiceItemModel
                {
                    ItemName = itemName,
                    Qty = item.Qty,
                    Price = item.Price,
                    Total = item.Total,
                    TotalLocal = item.TotalLocal ,
                    CostPer = item.CostPer,
                    ExpShareLocal = item.ExpShareLocal ,
                    CostTotal = item.CostTotal ?? 0,
                    CostUnit = item.CostUnit ?? 0,
                });
            }

            // ── 4) الموتوسيكلات ───────────────────────────────────────────────
            foreach (var car in FormCars)
            {
                var carInfo = CarsList.FirstOrDefault(c => c.CarId == car.CarId);
                var modelName = CarModels.FirstOrDefault(m => m.ModelId == carInfo?.ModelId)?.ModelName ?? string.Empty;
                var colorName = Colors.FirstOrDefault(col => col.ColorId == carInfo?.ColorId)?.ColorName ?? string.Empty;

                model.Cars.Add(new MotorBike.Services.ImportInvoiceCarModel
                {
                    ChassisNo = carInfo?.ChassisNo ?? string.Empty,
                    ModelName = modelName,
                    ColorName = colorName,
                    Total = car.Total ?? 0,
                    TotalLocal = car.TotalLocal ,
                    CostPer = car.CostPer,
                    ExpShareLocal = car.ExpShareLocal ,
                    CostTotal = car.CostTotal ?? 0,
                });
            }

            // ── 5) المصروفات ──────────────────────────────────────────────────
            foreach (var exp in FormExps)
            {
                model.Expenses.Add(new MotorBike.Services.ImportInvoiceExpModel
                {
                    ExpName = ExpenseTypes.FirstOrDefault(e => e.ExpId == exp.ExpId)?.ExpName ?? string.Empty,
                    CashName = CashList.FirstOrDefault(c => c.CashId == exp.CashId)?.CashName ?? string.Empty,
                    PayTotal = exp.PayTotal,
                    PayDate = exp.PayDate,
                });
            }

            // ── 6) الدفعات ────────────────────────────────────────────────────
            foreach (var pay in FormPayments)
            {
                // نجرب FilteredPaymentCashList أولاً (عملة أجنبية) وإلا CashList
                var cashName = FilteredPaymentCashList.FirstOrDefault(c => c.CashId == pay.CashId)?.CashName
                            ?? CashList.FirstOrDefault(c => c.CashId == pay.CashId)?.CashName
                            ?? string.Empty;

                model.Payments.Add(new MotorBike.Services.ImportInvoicePaymentModel
                {
                    PayMoney = pay.PayMoney,
                    OmlaRate = (double)pay.OmlaRate,
                    CashName = cashName,
                    PayDate = pay.PayDate,
                    Notes = pay.Notes ?? string.Empty,
                });
            }

            // ── 7) إنشاء الـ Document ─────────────────────────────────────────
            var document = new MotorBike.Services.ImportInvoiceDocument(model, company!);

            // ── 8) حوار الحفظ والمعاينة ─────────────────────────────────────
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "فاتورة استيراد رقم " + FormItem.InvId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "حدث خطأ أثناء الطباعة: " + ex.Message,
                "خطأ",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task DeleteAsync()
    {
        if (SelectedInvoice == null) return;

        if (!AppSession.HasPermission(ScreenId.ImportInvoice, AppAbility.Delete))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية للحذف.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        var res = MessageBox.Show("هل أنت متأكد من حذف هذه الفاتورة وكافة تفاصيلها؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            // حفظ البيانات المتأثرة قبل الحذف لإعادة الحساب بعدها
            var affectedItemIds = FormItems.Select(x => x.ItemId).Distinct().ToList();
            var affectedCashIds = FormPayments.Select(p => p.CashId).Distinct().ToList();
            foreach (var exp in FormExps)
                if (!affectedCashIds.Contains(exp.CashId)) affectedCashIds.Add(exp.CashId);
            int deletedSuppId = SelectedInvoice.SuppId;

            // Delete sub-items first
            var existingItems = (await _itemRepo.GetAllAsync()).Where(x => x.InvId == SelectedInvoice.InvId);
            foreach (var i in existingItems) await _itemRepo.DeleteAsync(i.Id);

            var existingCars = (await _carRepo.GetAllAsync()).Where(x => x.InvId == SelectedInvoice.InvId);
            foreach (var c in existingCars) await _carRepo.DeleteAsync(c.Id);

            var existingExps = (await _expRepo.GetAllAsync()).Where(x => x.InvId == SelectedInvoice.InvId);
            foreach (var e in existingExps) await _expRepo.DeleteAsync(e.Id);

            var existingPayments = (await _paymentRepo.GetAllAsync()).Where(x => x.InvId == SelectedInvoice.InvId);
            foreach (var p in existingPayments) await _paymentRepo.DeleteAsync(p.PayId);

            // Delete main invoice
            await _invoiceRepo.DeleteAsync(SelectedInvoice.InvId);

            // ── إعادة حساب الأرصدة بعد الحذف ──────────────────────────

            // 1) إعادة حساب المخزون لكل صنف متأثر
            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcStockForItemAsync(itemId);

            // 1b) إعادة حساب متوسط التكلفة من البداية (بعد الحذف نحسب من أول حركة)
            foreach (var itemId in affectedItemIds)
                await _compositeRepo.RecalcAvgCostForItemAsync(itemId, DateTime.MinValue);

            // 2) إعادة حساب رصيد مورد الاستيراد
            await _compositeRepo.RecalcBalanceForImportSupplierAsync(deletedSuppId);

            // 3) إعادة حساب رصيد كل خزينة متأثرة
            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            StatusMessage = "تم الحذف بنجاح ✓";
            var invs = await _invoiceRepo.GetAllAsync();
            Invoices = new(invs.OrderByDescending(x => x.InvDate));
            FilteredInvoices = new(Invoices);
            await AddNewRecord();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
        }
    }
}
