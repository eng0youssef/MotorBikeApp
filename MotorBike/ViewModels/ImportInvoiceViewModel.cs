using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    // Lookup Repos
    private readonly IRepository<ImportSupplier> _supplierRepo;
    private readonly IRepository<Omla> _omlaRepo;
    private readonly IRepository<Cash> _cashRepo;
    private readonly IRepository<ImportExpense> _expenseLookupRepo;
    private readonly IRepository<Store> _storeRepo;
    private readonly IRepository<Unit> _unitRepo;
    private readonly IRepository<Item> _itemsLookupRepo;
    private readonly IRepository<Car> _carsLookupRepo;

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
        IRepository<Item> itemsLookupRepo,
        IRepository<Car> carsLookupRepo)
    {
        _invoiceRepo = invoiceRepo;
        _itemRepo = itemRepo;
        _carRepo = carRepo;
        _expRepo = expRepo;
        _paymentRepo = paymentRepo;

        _supplierRepo = supplierRepo;
        _omlaRepo = omlaRepo;
        _cashRepo = cashRepo;
        _expenseLookupRepo = expenseLookupRepo;
        _storeRepo = storeRepo;
        _unitRepo = unitRepo;
        _itemsLookupRepo = itemsLookupRepo;
        _carsLookupRepo = carsLookupRepo;

        FormItem = new ImportInvoice { InvDate = DateTime.Now };
    }

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isSearchPanelVisible;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // Tracks if current form item is a new record or modifying an existing one
    private bool _isNewInvoice = true;

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
    [ObservableProperty] private ImportPayment _currentSubPayment = new() { PayDate = DateTime.Now };

    // Lookups
    [ObservableProperty] private ObservableCollection<ImportSupplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Omla> _omlas = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];
    [ObservableProperty] private ObservableCollection<ImportExpense> _expenseTypes = [];
    [ObservableProperty] private ObservableCollection<Store> _stores = [];
    [ObservableProperty] private ObservableCollection<Unit> _units = [];
    [ObservableProperty] private ObservableCollection<Item> _itemsList = [];
    [ObservableProperty] private ObservableCollection<Car> _carsList = [];

    // New car entry fields (for cars not yet in DB)
    [ObservableProperty] private string _newCarName = string.Empty;
    [ObservableProperty] private string _newCarShasehNo = string.Empty;
    [ObservableProperty] private string _newCarEngineNo = string.Empty;
    [ObservableProperty] private double _newCarTotal;

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
        _isSelectingSupplier = true;
        FormItem.SuppId = supplier.SuppId;
        SupplierSearchText = supplier.SuppName;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;
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

    // ── Currency Rate ──────────────────────────────────────────────────────
    [ObservableProperty] private byte _selectedOmlaId;

    partial void OnSelectedOmlaIdChanged(byte value)
    {
        if (FormItem != null && Omlas != null && Omlas.Any())
        {
            FormItem.OmlaId = value;
            var omla = Omlas.FirstOrDefault(o => o.OmlaId == value);
            if (omla != null) FormItem.OmlaRate = omla.OmlaRate;
            OnPropertyChanged(nameof(FormItem));
        }
    }

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

        CurrentSubItem = new ImportInvItem
        {
            StoreId = CurrentSubItem.StoreId,
            ItemId = item.ItemId,
            UnitId = item.UnitId,
            Price = item.Price0,   // Auto-fill price
            Qty = 1,
            UnitQty = 1
        };

        var itemUnits = Units.Where(u => u.UnitId == item.UnitId || u.UnitId == item.Unit2).ToList();
        CurrentItemUnits = itemUnits.Any() ? new(itemUnits) : new(Units);

        ItemSearchText = string.Empty;
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
        int nextId = await _invoiceRepo.GetNextIdAsync();
        FormItem = new ImportInvoice
        {
            InvId = nextId,
            InvDate = DateTime.Now,
            OmlaRate = 1
        };
        if (Omlas.Any())
        {
            var firstOmla = Omlas.First();
            FormItem.OmlaId = firstOmla.OmlaId;
            FormItem.OmlaRate = firstOmla.OmlaRate;
            SelectedOmlaId = firstOmla.OmlaId;
        }
        FormItem.SuppId = 0;

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

        NewCarName = string.Empty;
        NewCarShasehNo = string.Empty;
        NewCarEngineNo = string.Empty;
        NewCarTotal = 0;

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
                _isSelectingSupplier = true;
                SupplierSearchText = Suppliers.FirstOrDefault(s => s.SuppId == inv.SuppId)?.SuppName ?? string.Empty;
                IsSupplierSearchPopupOpen = false;
                _isSelectingSupplier = false;
                
                SelectedOmlaId = (byte)inv.OmlaId;
                SelectedInvType = inv.InvType > 0 ? inv.InvType : (byte)1;
            }

            var items = await _itemRepo.GetAllAsync();
            FormItems = new(items.Where(x => x.InvId == invId));

            var cars = await _carRepo.GetAllAsync();
            FormCars = new(cars.Where(x => x.InvId == invId));

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

        CalculateTotals();
    }

    [RelayCommand]
    public void RemoveSubItem(ImportInvItem item)
    {
        if (item != null && FormItems.Contains(item))
        {
            FormItems.Remove(item);
            CalculateTotals();
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
        CalculateTotals();
    }

    [RelayCommand]
    public void RemoveSubCar(ImportInvCar car)
    {
        if (car != null && FormCars.Contains(car))
        {
            FormCars.Remove(car);
            CalculateTotals();
        }
    }

    [RelayCommand]
    public void AddNewCar()
    {
        if (string.IsNullOrWhiteSpace(NewCarShasehNo))
        {
            StatusMessage = "يرجى إدخال رقم الشاسيه على الأقل.";
            return;
        }

        var newCar = new ImportInvCar
        {
            CarId = 0,
            Total = NewCarTotal
        };
        FormCars.Add(newCar);

        StatusMessage = $"تم إضافة موتوسيكل جديد مؤقتاً (شاسيه: {NewCarShasehNo}) — يجب تسجيله في بيانات الموتوسيكلات أولاً قبل الحفظ.";
        
        NewCarName = string.Empty;
        NewCarShasehNo = string.Empty;
        NewCarEngineNo = string.Empty;
        NewCarTotal = 0;

        CalculateTotals();
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
    public void AddSubPayment()
    {
        if (CurrentSubPayment.PayMoney <= 0)
        {
            StatusMessage = "يرجى إدخال مبلغ دفع صحيح.";
            return;
        }

        FormPayments.Add(CurrentSubPayment);
        var oldCashId = CurrentSubPayment.CashId;
        
        CurrentSubPayment = new ImportPayment { PayDate = DateTime.Now, OmlaRate = 1, CashId = oldCashId };
        if (Omlas.Any()) CurrentSubPayment.OmlaId = Omlas.First().OmlaId;
        CalculateTotals();
    }

    [RelayCommand]
    public void RemoveSubPayment(ImportPayment payment)
    {
        if (payment != null && FormPayments.Contains(payment))
        {
            FormPayments.Remove(payment);
            CalculateTotals();
        }
    }

    // ── Calculations ─────────────────────────────────────────────────────

    private void CalculateTotals()
    {
        if (FormItem == null) return;

        double itemsTotal = FormItems.Sum(x => x.Total);
        double carsTotal = FormCars.Sum(x => x.Total ?? 0);
        FormItem.InvTotal = Math.Round(itemsTotal + carsTotal, 2);

        FormItem.ExpTotal = Math.Round(FormExps.Sum(x => x.PayTotal), 2);
        
        FormItem.TotalCost = Math.Round(FormItem.InvTotal + FormItem.ExpTotal, 2);
        
        OnPropertyChanged(nameof(FormItem));
    }

    // ── Save/Delete ──────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem.SuppId == 0)
        {
            StatusMessage = "يرجى اختيار المورد.";
            return;
        }

        CalculateTotals();
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
            int nextItemId = await _itemRepo.GetNextIdAsync();
            foreach (var item in FormItems)
            {
                if (item.Id == 0) item.Id = nextItemId++;
                item.InvId = FormItem.InvId;
                await _itemRepo.InsertAsync(item);
            }

            int nextCarId = await _carRepo.GetNextIdAsync();
            foreach (var car in FormCars.Where(c => c.CarId > 0 || c.CarId == 0)) 
            {
                if (car.Id == 0) car.Id = nextCarId++;
                car.InvId = FormItem.InvId;
                await _carRepo.InsertAsync(car);
            }

            int nextExpId = await _expRepo.GetNextIdAsync();
            foreach (var exp in FormExps)
            {
                if (exp.Id == 0) exp.Id = nextExpId++;
                exp.InvId = FormItem.InvId;
                exp.AddDate = DateTime.Now;
                exp.AddPc = Environment.MachineName;
                await _expRepo.InsertAsync(exp);
            }

            int nextPayId = await _paymentRepo.GetNextIdAsync();
            foreach (var pay in FormPayments)
            {
                if (pay.PayId == 0) pay.PayId = nextPayId++;
                pay.InvId = FormItem.InvId;
                pay.SuppId = FormItem.SuppId;
                pay.AddDate = DateTime.Now;
                pay.AddPc = Environment.MachineName;
                await _paymentRepo.InsertAsync(pay);
            }

            IsEditing = false;
            StatusMessage = "تم حفظ فاتورة الاستيراد بنجاح.";

            var invs = await _invoiceRepo.GetAllAsync();
            Invoices = new(invs.OrderByDescending(x => x.InvDate));
            FilteredInvoices = new(Invoices);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحفظ: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task DeleteAsync()
    {
        if (SelectedInvoice == null) return;
        var res = MessageBox.Show("هل أنت متأكد من حذف هذه الفاتورة وكافة تفاصيلها؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        try
        {
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

            StatusMessage = "تم الحذف بنجاح.";
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
