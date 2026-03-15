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

            AddNewRecord();
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
    public void AddNewRecord()
    {
        FormItem = new ImportInvoice
        {
            InvDate = DateTime.Now,
            OmlaRate = 1
        };
        
        if (Omlas.Any()) FormItem.OmlaId = Omlas.First().OmlaId;
        if (Suppliers.Any()) FormItem.SuppId = Suppliers.First().SuppId;

        FormItems.Clear();
        FormCars.Clear();
        FormExps.Clear();
        FormPayments.Clear();

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
        if (ItemsList.Any()) CurrentSubItem.ItemId = ItemsList.First().ItemId;

        CurrentSubCar = new ImportInvCar();
        if (CarsList.Any()) CurrentSubCar.CarId = CarsList.First().CarId;

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
    public void CancelEdit()
    {
        IsEditing = false;
        if (SelectedInvoice != null)
            _ = LoadInvoiceDetailsAsync(SelectedInvoice.InvId);
        else
            AddNewRecord();
    }

    private bool CanEditOrDelete() => SelectedInvoice != null;

    private async Task LoadInvoiceDetailsAsync(int invId)
    {
        try
        {
            var inv = await _invoiceRepo.GetByIdAsync(invId);
            if (inv == null) return;
            FormItem = inv;

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
            bool isNew = FormItem.InvId == 0;
            if (isNew)
            {
                FormItem.AddDate = DateTime.Now;
                FormItem.AddPc = Environment.MachineName;
                await _invoiceRepo.InsertAsync(FormItem);
                
                // Fetch the generated ID.
                var allInvs = await _invoiceRepo.GetAllAsync();
                var newInv = allInvs.OrderByDescending(x => x.InvId).FirstOrDefault();
                if (newInv != null) FormItem.InvId = newInv.InvId;
            }
            else
            {
                FormItem.EditDate = DateTime.Now;
                FormItem.EditPc = Environment.MachineName;
                await _invoiceRepo.UpdateAsync(FormItem);
                
                // Delete existing sub-items
                // Note: In a real app we might write custom SQL but here we do it sequentially.
                // We fetch current sub-items and delete them.
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
            foreach (var item in FormItems)
            {
                item.InvId = FormItem.InvId;
                await _itemRepo.InsertAsync(item);
            }

            foreach (var car in FormCars)
            {
                car.InvId = FormItem.InvId;
                await _carRepo.InsertAsync(car);
            }

            foreach (var exp in FormExps)
            {
                exp.InvId = FormItem.InvId;
                exp.AddDate = DateTime.Now;
                exp.AddPc = Environment.MachineName;
                await _expRepo.InsertAsync(exp);
            }

            foreach (var pay in FormPayments)
            {
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
            AddNewRecord();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
        }
    }
}
