using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Color = MotorBike.Models.Color;

namespace MotorBike.ViewModels;

public partial class BuyCarViewModel : ObservableObject
{
    // ── Dependencies ────────────────────────────────────────────────────────
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<BuyCar> _buyCarRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<Car> _carRepository;
    private readonly IRepository<CarModel> _carModelRepository;
    private readonly IRepository<Color> _colorRepository;
    private readonly IRepository<CarBrand> _carBrandRepository;
    private readonly IRepository<Supplier> _supplierRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly CompositeKeyRepository _compositeRepo;

    // ── Lookup collections ────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<CarModel> _carModels = [];
    [ObservableProperty] private ObservableCollection<Color> _colors = [];
    [ObservableProperty] private ObservableCollection<CarBrand> _carBrands = [];
    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Customer> _customers = [];

    // ── Invoice lists ─────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BuyCar> _invoices = [];
    [ObservableProperty] private ObservableCollection<BuyCar> _filteredInvoices = [];

    // ── Invoice form ──────────────────────────────────────────────────────
    [ObservableProperty] private BuyCar _formItem = new();

    // ── Inline car fields ─────────────────────────────────────────────────
    private int _carModelId;
    private int _carColorId;
    [ObservableProperty] private string _carModelName = string.Empty;
    [ObservableProperty] private string _carBrandName = string.Empty;
    [ObservableProperty] private string _carColorName = string.Empty;
    [ObservableProperty] private bool _isBrandEnabled = true;
    [ObservableProperty] private short _carYearNo = (short)DateTime.Now.Year;

    partial void OnCarModelNameChanged(string value)
    {
        var model = CarModels.FirstOrDefault(m => string.Equals(m.ModelName, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (model != null)
        {
            var brand = CarBrands.FirstOrDefault(b => b.BrandId == model.BrandId);
            if (brand != null)
            {
                CarBrandName = brand.BrandName;
            }
            IsBrandEnabled = false;
        }
        else
        {
            IsBrandEnabled = true;
        }
    }
    [ObservableProperty] private string? _carChassisNo;
    [ObservableProperty] private string? _carMotorNo;
    [ObservableProperty] private string? _carPlateNo;
    [ObservableProperty] private string? _carNotes;
    [ObservableProperty] private bool _carActive = true;

    // ── Source selection ──────────────────────────────────────────────────
    [ObservableProperty] private bool _isFromCustomer;
    [ObservableProperty] private bool _isExistingCar;

    // Smart search — Supplier
    [ObservableProperty] private string _supplierSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Supplier> _filteredSuppliersList = [];
    [ObservableProperty] private bool _isSupplierSearchPopupOpen;
    private bool _isSelectingSupplier;
    [ObservableProperty] private int _selectedSupplierId;

    // Smart search — Customer (as source)
    [ObservableProperty] private string _customerSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Customer> _filteredCustomersList = [];
    [ObservableProperty] private bool _isCustomerSearchPopupOpen;
    private bool _isSelectingCustomer;
    [ObservableProperty] private int _selectedSourceCustomerId;

    // Existing car selection
    [ObservableProperty] private ObservableCollection<Car> _sourceCars = [];
    [ObservableProperty] private int _selectedExistingCarId;

    // ── Payments ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BuyCarPayment> _formPayments = [];
    [ObservableProperty] private BuyCarPayment _currentPayment = new();

    // ── State ─────────────────────────────────────────────────────────────
    [ObservableProperty] private BuyCar? _selectedInvoice;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;
    [ObservableProperty] private double _remaining;
    [ObservableProperty] private bool _isCashPaymentMode;   // ← تمت إضافته كـ ObservableProperty
    [ObservableProperty] private int _selectedCashId;
    [ObservableProperty] private double _currentSupplierBalance;
    [ObservableProperty] private double _currentCustomerBalance;
    [ObservableProperty] private double _currentSafeBalance;

    partial void OnSelectedCashIdChanged(int value)
    {
        if (FormPayments != null && FormPayments.Any()) FormPayments[0].CashId = value;
        if (CurrentPayment != null) CurrentPayment.CashId = value;
        CurrentSafeBalance = Cashes.FirstOrDefault(c => c.CashId == value)?.Bal ?? 0;
    }

    // ── Search ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

    // ── Payments Popup ────────────────────────────────────────────────────
    private bool _isPaymentsPopupOpen;
    public bool IsPaymentsPopupOpen
    {
        get => _isPaymentsPopupOpen;
        set { _isPaymentsPopupOpen = value; OnPropertyChanged(); }
    }

    [RelayCommand] private void OpenPaymentsPopup() => IsPaymentsPopupOpen = true;
    [RelayCommand] private void ClosePaymentsPopup() => IsPaymentsPopupOpen = false;

    // ── Tax proxy properties ──────────────────────────────────────────────
    public bool FormIsTax
    {
        get => FormItem?.IsTax ?? false;
        set
        {
            if (FormItem != null && FormItem.IsTax != value)
            {
                FormItem.IsTax = value;
                OnPropertyChanged();
                CalculateTotalsInternal();
            }
        }
    }

    public double FormTotal
    {
        get => FormItem?.Total ?? 0;
        set
        {
            if (FormItem != null && FormItem.Total != value)
            {
                FormItem.Total = value;
                OnPropertyChanged();
                CalculateTotalsInternal();
            }
        }
    }

    // ── Tax percentages ───────────────────────────────────────────────────
    [ObservableProperty] private double _netBeforeTax;

    private double _vatTaxPercent;
    public double VatTaxPercent
    {
        get => _vatTaxPercent;
        set { if (SetProperty(ref _vatTaxPercent, value)) CalculateTotalsInternal(); }
    }

    private double _whtTaxPercent;
    public double WhtTaxPercent
    {
        get => _whtTaxPercent;
        set { if (SetProperty(ref _whtTaxPercent, value)) CalculateTotalsInternal(); }
    }

    // ── Constructor ───────────────────────────────────────────────────────
    public BuyCarViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<BuyCar> buyCarRepository,
        IRepository<Cash> cashRepository,
        IRepository<Car> carRepository,
        IRepository<CarModel> carModelRepository,
        IRepository<Color> colorRepository,
        IRepository<CarBrand> carBrandRepository,
        IRepository<Supplier> supplierRepository,
        IRepository<Customer> customerRepository,
        CompositeKeyRepository compositeRepo)
    {
        _dbFactory = dbFactory;
        _buyCarRepository = buyCarRepository;
        _cashRepository = cashRepository;
        _carRepository = carRepository;
        _carModelRepository = carModelRepository;
        _colorRepository = colorRepository;
        _carBrandRepository = carBrandRepository;
        _supplierRepository = supplierRepository;
        _customerRepository = customerRepository;
        _compositeRepo = compositeRepo;
    }

    // ── Initial data load ─────────────────────────────────────────────────
    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes.Where(c => c.OmlaId == 0 || c.OmlaId == null));

            var models = await _carModelRepository.GetAllAsync();
            CarModels = new ObservableCollection<CarModel>(models.Where(m => m.Active));

            var colors = await _colorRepository.GetAllAsync();
            Colors = new ObservableCollection<Color>(colors.Where(c => c.Active));

            var brands = await _carBrandRepository.GetAllAsync();
            CarBrands = new ObservableCollection<CarBrand>(brands.Where(b => b.Active));

            var suppliers = await _supplierRepository.GetAllAsync();
            Suppliers = new ObservableCollection<Supplier>(suppliers);

            var customers = await _customerRepository.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers);

            CarModelName = string.Empty;
            CarColorName = string.Empty;

            await LoadInvoicesAsync();
            await AddNewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات: {ex.Message}";
        }
    }

    private async Task LoadInvoicesAsync()
    {
        var data = await _buyCarRepository.GetAllAsync();
        Invoices = new ObservableCollection<BuyCar>(data);
        FilterInvoices();
    }

    // ── Search / filter ───────────────────────────────────────────────────
    partial void OnSearchTextChanged(string value) => FilterInvoices();

    private void FilterInvoices()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredInvoices = new ObservableCollection<BuyCar>(Invoices);
            return;
        }
        var lower = SearchText.ToLower();
        FilteredInvoices = new ObservableCollection<BuyCar>(
            Invoices.Where(i =>
                i.BuyId.ToString().Contains(lower) ||
                (i.OwnerName?.ToLower().Contains(lower) == true)));
    }

    [RelayCommand] private void ShowSearchPanel() { IsSearchPanelVisible = true; SearchText = ""; FilterInvoices(); }
    [RelayCommand] private void HideSearchPanel() { IsSearchPanelVisible = false; }

    // ── Selected invoice → load form ──────────────────────────────────────
    partial void OnSelectedInvoiceChanged(BuyCar? value)
    {
        if (value is not null)
        {
            IsSearchPanelVisible = false;
            _isInsertMode = false;
            IsEditing = false;

            FormItem = CloneInvoice(value);
            IsCashPaymentMode = FormItem.IsCash;

            double netBefore = FormItem.Total;
            if (FormItem.IsTax && netBefore > 0)
            {
                _vatTaxPercent = Math.Round((FormItem.VatTax / netBefore) * 100.0, 2);
                _whtTaxPercent = Math.Round((FormItem.Tax / netBefore) * 100.0, 2);
                OnPropertyChanged(nameof(VatTaxPercent));
                OnPropertyChanged(nameof(WhtTaxPercent));
            }
            else
            {
                _vatTaxPercent = 0; _whtTaxPercent = 0;
                OnPropertyChanged(nameof(VatTaxPercent));
                OnPropertyChanged(nameof(WhtTaxPercent));
            }

            CalculateTotalsInternal();



            _ = LoadPaymentsAsync(value.BuyId);
            if (value.CarId.HasValue)
                _ = LoadCarDetailsAsync(value.CarId.Value);
        }
    }

    private async Task LoadCarDetailsAsync(int carId)
    {
        if (carId <= 0) return;
        try
        {
            using var db = _dbFactory.CreateConnection();
            var car = await db.QuerySingleOrDefaultAsync<Car>(
                "SELECT * FROM Cars WHERE Car_ID = @CarId", new { CarId = carId });

            if (car is null) return;

            _carModelId = car.ModelId;
            var model = CarModels.FirstOrDefault(m => m.ModelId == car.ModelId);
            CarModelName = model?.ModelName ?? string.Empty;

            _carColorId = car.ColorId;
            var color = Colors.FirstOrDefault(c => c.ColorId == car.ColorId);
            CarColorName = color?.ColorName ?? string.Empty;

            if (model != null)
            {
                var brand = CarBrands.FirstOrDefault(b => b.BrandId == model.BrandId);
                CarBrandName = brand?.BrandName ?? string.Empty;
                IsBrandEnabled = false;
            }
            else
            {
                CarBrandName = string.Empty;
                IsBrandEnabled = true;
            }

            CarYearNo = car.YearNo;
            CarChassisNo = car.ChassisNo;
            CarMotorNo = car.MotorNo;
            CarPlateNo = car.PlateNo;
            CarNotes = car.Notes;
            CarActive = car.IsStock;

            IsFromCustomer = car.IsFromCustomer;
            
            _isSelectingSupplier = true;
            SelectedSupplierId = car.SupplierId ?? 0;
            SupplierSearchText = Suppliers.FirstOrDefault(s => s.SuppId == SelectedSupplierId)?.SuppName ?? string.Empty;
            CurrentSupplierBalance = Suppliers.FirstOrDefault(s => s.SuppId == SelectedSupplierId)?.Bal ?? 0;
            _isSelectingSupplier = false;
            
            _isSelectingCustomer = true;
            SelectedSourceCustomerId = car.SourceCustomerId ?? 0;
            CustomerSearchText = Customers.FirstOrDefault(c => c.CusId == SelectedSourceCustomerId)?.CusName ?? string.Empty;
            CurrentCustomerBalance = Customers.FirstOrDefault(c => c.CusId == SelectedSourceCustomerId)?.Bal ?? 0;
            _isSelectingCustomer = false;
        }
        catch (Exception ex) { StatusMessage = "خطأ في تحميل بيانات الموتوسيكل: " + ex.Message; }
    }

    private async Task LoadPaymentsAsync(int buyId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var payments = await db.QueryAsync<BuyCarPayment>(
                "SELECT * FROM Buy_Car_Payments WHERE BuyId = @BuyId", new { BuyId = buyId });
            FormPayments = new ObservableCollection<BuyCarPayment>(payments);
            CalculatePayedTotal();
        }
        catch (Exception ex) { StatusMessage = "خطأ في تحميل المدفوعات: " + ex.Message; }
    }

    // ── Add new ───────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task AddNewAsync()
    {
        await Task.CompletedTask;

        _isInsertMode = true;
        IsEditing = true;
        SelectedInvoice = null;

        FormItem = new BuyCar
        {
            BuyDate = DateTime.Now,
            AddPc = Environment.MachineName,
            AddDate = DateTime.Now,
            IsCash = true
        };
        IsCashPaymentMode = true;
        SelectedCashId = Cashes.FirstOrDefault()?.CashId ?? 0;
        CurrentSafeBalance = Cashes.FirstOrDefault(c => c.CashId == SelectedCashId)?.Bal ?? 0;

        _carModelId = 0;
        _carColorId = 0;
        CarModelName = string.Empty;
        CarBrandName = string.Empty;
        CarColorName = string.Empty;
        IsBrandEnabled = true;

        CarYearNo = (short)DateTime.Now.Year;
        CarChassisNo = null; CarMotorNo = null; CarPlateNo = null; CarNotes = null;
        CarActive = true;

        IsFromCustomer = false; IsExistingCar = false;
        SelectedSupplierId = 0; SelectedSourceCustomerId = 0; SelectedExistingCarId = 0;
        SourceCars.Clear();

        _isSelectingSupplier = true;
        SupplierSearchText = string.Empty; IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;

        _isSelectingCustomer = true;
        CustomerSearchText = string.Empty; IsCustomerSearchPopupOpen = false;
        _isSelectingCustomer = false;

        CurrentSupplierBalance = 0;
        CurrentCustomerBalance = 0;

        FormPayments.Clear();
        TotalPayed = 0;
        CurrentPayment = new BuyCarPayment { PayDate = FormItem.BuyDate.AddSeconds(20), CashId = SelectedCashId };

        VatTaxPercent = 0; WhtTaxPercent = 0;
    }

    // ── Edit selected ─────────────────────────────────────────────────────
    [RelayCommand]
    public void EditSelected()
    {
        if (SelectedInvoice is null) return;
        FormItem = CloneInvoice(SelectedInvoice);
        IsCashPaymentMode = FormItem.IsCash;
        _isInsertMode = false;
        IsEditing = true;

        SelectedCashId = FormItem.IsCash && FormPayments.Any()
            ? FormPayments.First().CashId
            : Cashes.FirstOrDefault()?.CashId ?? 0;
    }

    // ── Cancel ────────────────────────────────────────────────────────────
    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false;
        IsEditing = false;
        FormItem = new BuyCar();
        FormPayments.Clear();
        TotalPayed = 0;
        CurrentPayment = new BuyCarPayment();
        CarChassisNo = null; CarMotorNo = null; CarPlateNo = null;

        IsFromCustomer = false; IsExistingCar = false;
        SelectedSupplierId = 0; SelectedSourceCustomerId = 0; SelectedExistingCarId = 0;
        SourceCars.Clear();

        _isSelectingSupplier = true;
        SupplierSearchText = string.Empty; IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;

        _isSelectingCustomer = true;
        CustomerSearchText = string.Empty; IsCustomerSearchPopupOpen = false;
        _isSelectingCustomer = false;

        StatusMessage = null;
        VatTaxPercent = 0; WhtTaxPercent = 0;
    }

    // ── Save ──────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;

        var requiredAbility = _isInsertMode ? AppAbility.Add : AppAbility.Edit;
        if (!AppSession.HasPermission(ScreenId.BuyCar, requiredAbility))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية لإجراء هذه العملية.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        if (string.IsNullOrWhiteSpace(FormItem.OwnerName))
        { StatusMessage = "⚠️ يجب إدخال اسم المالك."; return; }

        if (string.IsNullOrWhiteSpace(CarChassisNo))
        { StatusMessage = "⚠️ يجب إدخال رقم الشاسيه."; return; }

        try
        {
            var affectedCashIds = FormPayments.Select(p => p.CashId).Where(id => id > 0).Distinct().ToList();

            if (!_isInsertMode)
            {
                using var dbPre = _dbFactory.CreateConnection();
                var oldCashIds = await dbPre.QueryAsync<int>(
                    "SELECT DISTINCT CashID FROM Buy_Car_Payments WHERE BuyId = @BuyId",
                    new { BuyId = FormItem.BuyId });
                foreach (var cid in oldCashIds)
                    if (cid > 0 && !affectedCashIds.Contains(cid)) affectedCashIds.Add(cid);
            }

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                if (FormItem.IsTax && string.IsNullOrWhiteSpace(FormItem.TaxNo))
                {
                    var maxTaxNoStr = await db.QueryFirstOrDefaultAsync<string>(
                        "SELECT CAST(MAX(CAST(TaxNo AS INT)) AS VARCHAR) FROM Buy_Car WHERE ISNUMERIC(TaxNo) = 1 AND TaxNo NOT LIKE '%[^0-9]%'",
                        transaction: tx);
                    int.TryParse(maxTaxNoStr, out int maxTaxNo);
                    FormItem.TaxNo = (maxTaxNo + 1).ToString();
                }
                else if (!FormItem.IsTax)
                {
                    FormItem.TaxNo = null;
                }

                FormItem.IsCash = IsCashPaymentMode;

                if (IsCashPaymentMode)
                {
                    FormPayments.Clear();
                    FormPayments.Add(new BuyCarPayment
                    {
                        BuyId = FormItem.BuyId,
                        PayDate = FormItem.BuyDate.AddSeconds(20),
                        PayMoney = FormItem.Net,
                        CashId = SelectedCashId,
                        Notes = "دفع كاش للفاتورة"
                    });
                }

                CalculateTotalsInternal();

                if (_isInsertMode)
                {
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                }
                else
                {
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditPc = Environment.MachineName;
                }

                // Color check
                var selectedColorName = CarColorName?.Trim() ?? "بدون";
                var colorObj = Colors.FirstOrDefault(c => string.Equals(c.ColorName, selectedColorName, StringComparison.OrdinalIgnoreCase));
                if (colorObj != null)
                {
                    _carColorId = colorObj.ColorId;
                }
                else
                {
                    _carColorId = await _colorRepository.GetNextIdAsync();
                    var newColor = new Color { ColorId = _carColorId, ColorName = selectedColorName, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                    await db.ExecuteAsync("INSERT INTO Colors (Color_ID, ColorName, Active, AddDate, AddPC, AddUser) VALUES (@ColorId, @ColorName, @Active, @AddDate, @AddPc, @AddUser)", newColor, tx);
                    Colors.Add(newColor);
                }

                // Brand and Model check
                var selectedModelName = CarModelName?.Trim() ?? "بدون";
                var modelObj = CarModels.FirstOrDefault(m => string.Equals(m.ModelName, selectedModelName, StringComparison.OrdinalIgnoreCase));
                if (modelObj != null)
                {
                    _carModelId = modelObj.ModelId;
                }
                else
                {
                    var selectedBrandName = CarBrandName?.Trim() ?? "بدون";
                    var brandObj = CarBrands.FirstOrDefault(b => string.Equals(b.BrandName, selectedBrandName, StringComparison.OrdinalIgnoreCase));
                    int currentBrandId;
                    if (brandObj != null)
                    {
                        currentBrandId = brandObj.BrandId;
                    }
                    else
                    {
                        currentBrandId = await _carBrandRepository.GetNextIdAsync();
                        var newBrand = new CarBrand { BrandId = currentBrandId, BrandName = selectedBrandName, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                        await db.ExecuteAsync("INSERT INTO CarBrands (Brand_ID, BrandName, Active, AddDate, AddPC, AddUser) VALUES (@BrandId, @BrandName, @Active, @AddDate, @AddPc, @AddUser)", newBrand, tx);
                        CarBrands.Add(newBrand);
                    }

                    _carModelId = await _carModelRepository.GetNextIdAsync();
                    var newModel = new CarModel { ModelId = _carModelId, ModelName = selectedModelName, BrandId = currentBrandId, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                    await db.ExecuteAsync("INSERT INTO CarModels (Model_ID, ModelName, BrandID, Active, AddDate, AddPC, AddUser) VALUES (@ModelId, @ModelName, @BrandId, @Active, @AddDate, @AddPc, @AddUser)", newModel, tx);
                    CarModels.Add(newModel);
                }

                if (_isInsertMode)
                {
                    int carId;
                    if (IsExistingCar && SelectedExistingCarId > 0)
                    {
                        carId = SelectedExistingCarId;
                        await db.ExecuteAsync(@"
                            UPDATE Cars
                            SET StatusID = 1, OwnerID = NULL, IsStock = 1,
                                IsLocalSupplier = @IsLocalSupplier, SupplierID = @SupplierId,
                                IsFromCustomer = @IsFromCustomer, SourceCustomerID = @SourceCustomerId,
                                PurchasePrice = @PurchasePrice, Mileage = @Mileage,
                                EditDate = @EditDate, EditPC = @EditPc, EditUser = @EditUser
                            WHERE Car_ID = @CarId",
                            new
                            {
                                CarId = carId,
                                IsLocalSupplier = IsFromCustomer ? (bool?)null : true,
                                SupplierId = IsFromCustomer ? (int?)null : (SelectedSupplierId > 0 ? SelectedSupplierId : (int?)null),
                                IsFromCustomer = IsFromCustomer,
                                SourceCustomerId = IsFromCustomer ? (SelectedSourceCustomerId > 0 ? SelectedSourceCustomerId : (int?)null) : (int?)null,
                                PurchasePrice = FormItem.Net,
                                Mileage = FormItem.Mileage ?? 0,
                                EditDate = DateTime.Now,
                                EditPc = Environment.MachineName,
                                EditUser = AppSession.CurrentUserId ?? 1
                            }, tx);
                    }
                    else
                    {
                        carId = await _carRepository.GetNextIdAsync();
                        await db.ExecuteAsync(@"
                            INSERT INTO Cars
                                (Car_ID, ModelID, YearNo, ChassisNo, MotorNo, PlateNo,
                                 Mileage, ColorID, IsStock, Notes,
                                 OwnerID, StatusID, IsLocalSupplier, SupplierID,
                                 IsFromCustomer, SourceCustomerID, PurchasePrice,
                                 AddDate, AddPC, AddUser)
                            VALUES
                                (@CarId, @ModelId, @YearNo, @ChassisNo, @MotorNo, @PlateNo,
                                 @Mileage, @ColorId, 1, @Notes,
                                 NULL, 1, @IsLocalSupplier, @SupplierId,
                                 @IsFromCustomer, @SourceCustomerId, @PurchasePrice,
                                 @AddDate, @AddPc, @AddUser)",
                            new
                            {
                                CarId = carId,
                                ModelId = _carModelId,
                                YearNo = CarYearNo,
                                ChassisNo = CarChassisNo,
                                MotorNo = CarMotorNo,
                                PlateNo = CarPlateNo,
                                Mileage = FormItem.Mileage ?? 0,
                                ColorId = _carColorId,
                                Notes = CarNotes,
                                IsLocalSupplier = IsFromCustomer ? (bool?)null : true,
                                SupplierId = IsFromCustomer ? (int?)null : (SelectedSupplierId > 0 ? SelectedSupplierId : (int?)null),
                                IsFromCustomer = IsFromCustomer,
                                SourceCustomerId = IsFromCustomer ? (SelectedSourceCustomerId > 0 ? SelectedSourceCustomerId : (int?)null) : (int?)null,
                                PurchasePrice = FormItem.Net,
                                AddDate = FormItem.AddDate,
                                AddPc = FormItem.AddPc,
                                AddUser = FormItem.AddUser
                            }, tx);
                    }
                    FormItem.CarId = carId;
                }
                else
                {
                    await db.ExecuteAsync(@"
                        UPDATE Cars
                        SET ModelID   = @ModelId,  YearNo    = @YearNo,
                            ChassisNo = @ChassisNo, MotorNo   = @MotorNo,
                            PlateNo   = @PlateNo,   Mileage   = @Mileage,
                            ColorID   = @ColorId,   IsStock   = 1,
                            Notes     = @Notes,     StatusID  = 1, OwnerID = NULL,
                            IsLocalSupplier  = @IsLocalSupplier, SupplierID = @SupplierId,
                            IsFromCustomer   = @IsFromCustomer, SourceCustomerID = @SourceCustomerId,
                            PurchasePrice    = @PurchasePrice,
                            EditDate = @EditDate, EditPC = @EditPc, EditUser = @EditUser
                        WHERE Car_ID = @CarId",
                        new
                        {
                            CarId = FormItem.CarId,
                            ModelId = _carModelId,
                            YearNo = CarYearNo,
                            ChassisNo = CarChassisNo,
                            MotorNo = CarMotorNo,
                            PlateNo = CarPlateNo,
                            Mileage = FormItem.Mileage,
                            ColorId = _carColorId,
                            Notes = CarNotes,
                            IsLocalSupplier = IsFromCustomer ? (bool?)null : true,
                            SupplierId = IsFromCustomer ? (int?)null : (SelectedSupplierId > 0 ? SelectedSupplierId : (int?)null),
                            IsFromCustomer = IsFromCustomer,
                            SourceCustomerId = IsFromCustomer ? (SelectedSourceCustomerId > 0 ? SelectedSourceCustomerId : (int?)null) : (int?)null,
                            PurchasePrice = FormItem.Net,
                            EditDate = FormItem.EditDate,
                            EditPc = FormItem.EditPc,
                            EditUser = FormItem.EditUser
                        }, tx);
                }

                // ── 2. Invoice row ───────────────────────────────────────
                if (_isInsertMode)
                {
                    FormItem.BuyId = await _buyCarRepository.GetNextIdAsync();
                    await db.ExecuteAsync(@"
                    INSERT INTO Buy_Car
                        (Buy_ID, BuyDate, OwnerName, OwnerTel, OwnerKawmy, OwnerAdress,
                         CarID, Mileage, Total, Net, Notes, AddDate, AddPC, AddUser,
                         IsTax, VatTax, Tax, TaxNo, IsCash)
                    VALUES
                        (@BuyId, @BuyDate, @OwnerName, @OwnerTel, @OwnerKawmy, @OwnerAdress,
                         @CarId, @Mileage, @Total, @Net, @Notes, @AddDate, @AddPc, @AddUser,
                         @IsTax, @VatTax, @Tax, @TaxNo, @IsCash)",
                        FormItem, tx);
                }
                else
                {
                    await db.ExecuteAsync(@"
                    UPDATE Buy_Car
                    SET BuyDate     = @BuyDate,   OwnerName   = @OwnerName,
                        OwnerTel    = @OwnerTel,  OwnerKawmy  = @OwnerKawmy,
                        OwnerAdress = @OwnerAdress, CarID     = @CarId,
                        Mileage     = @Mileage,   Total       = @Total,
                        Net         = @Net,       Notes       = @Notes,
                        EditDate    = @EditDate,  EditPC      = @EditPc,
                        EditUser    = @EditUser,  IsTax       = @IsTax,
                        VatTax      = @VatTax,   Tax         = @Tax,
                        TaxNo       = @TaxNo,    IsCash      = @IsCash
                    WHERE Buy_ID = @BuyId",
                        FormItem, tx);

                    await db.ExecuteAsync(
                        "DELETE FROM Buy_Car_Payments WHERE BuyID = @BuyId",
                        new { BuyId = FormItem.BuyId }, tx);
                }

                // ── 3. Payments ──────────────────────────────────────────
                int maxPayId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(Pay_ID), 0) FROM Buy_Car_Payments",
                    transaction: tx);

                foreach (var p in FormPayments)
                {
                    p.PayDate = FormItem.BuyDate.AddSeconds(20);
                    p.PayId = ++maxPayId;
                    p.BuyId = FormItem.BuyId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Buy_Car_Payments
                            (Pay_ID, PayDate, PayMoney, CashID, Notes, BuyID)
                        VALUES
                            (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @BuyId)",
                        p, tx);
                }

                tx.Commit();
                StatusMessage = "تم حفظ فاتورة الشراء بنجاح ✓";
            }
            catch { tx.Rollback(); throw; }

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            if (IsCashPaymentMode && !affectedCashIds.Contains(SelectedCashId))
                await _compositeRepo.RecalcBalanceForCashAsync(SelectedCashId);

            if (IsFromCustomer && SelectedSourceCustomerId > 0)
                await _compositeRepo.RecalcBalanceForCustomerAsync(SelectedSourceCustomerId);
            else if (!IsFromCustomer && SelectedSupplierId > 0)
                await _compositeRepo.RecalcBalanceForSupplierAsync(SelectedSupplierId);

            _isInsertMode = false;
            await LoadInvoicesAsync();
            var savedInvoice = Invoices.FirstOrDefault(x => x.BuyId == FormItem.BuyId);
            if (savedInvoice != null)
            {
                SelectedInvoice = savedInvoice;
                IsEditing = true;
            }
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحفظ: {ex.Message}"; }
    }

    // ── Delete ────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedInvoice is null) return;

        if (!AppSession.HasPermission(ScreenId.BuyCar, AppAbility.Delete))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية للحذف.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        var result = System.Windows.MessageBox.Show("هل أنت متأكد من الحذف نهائياً؟", "تأكيد الحذف", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            var affectedCashIds = FormPayments.Select(p => p.CashId).Distinct().ToList();

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync(
                    "DELETE FROM Buy_Car_Payments WHERE BuyID = @BuyId",
                    new { BuyId = SelectedInvoice.BuyId }, tx);
                await db.ExecuteAsync(
                    "DELETE FROM Buy_Car WHERE Buy_ID = @BuyId",
                    new { BuyId = SelectedInvoice.BuyId }, tx);

                if (SelectedInvoice.CarId.HasValue)
                {
                    int carId = SelectedInvoice.CarId.Value;
                    var salesCount = await db.QueryFirstOrDefaultAsync<int>(
                        "SELECT COUNT(*) FROM Sales_Car WHERE CarID = @CarId",
                        new { CarId = carId }, tx);

                    if (salesCount == 0)
                    {
                        // No sales history, safe to delete the car completely
                        await db.ExecuteAsync("DELETE FROM Cars WHERE Car_ID = @CarId", new { CarId = carId }, tx);
                    }
                    else
                    {
                        // Car was likely a return, revert its status to sold
                        var lastSaleOwner = await db.QueryFirstOrDefaultAsync<int?>(
                            "SELECT TOP 1 CusId FROM Sales_Car WHERE CarID = @CarId ORDER BY SalesDate DESC",
                            new { CarId = carId }, tx);

                        await db.ExecuteAsync(
                            "UPDATE Cars SET IsStock = 0, StatusID = 2, OwnerID = @OwnerId, IsFromCustomer = 0, SourceCustomerID = NULL WHERE Car_ID = @CarId",
                            new { CarId = carId, OwnerId = lastSaleOwner }, tx);
                    }
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            StatusMessage = "تم حذف الفاتورة بنجاح ✓";
            IsEditing = false;
            FormItem = new BuyCar();
            FormPayments.Clear();
            TotalPayed = 0;
            SelectedInvoice = null;
            CurrentSupplierBalance = 0;
            CurrentCustomerBalance = 0;
            CurrentSafeBalance = 0;
            SelectedCashId = 0;
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحذف: {ex.Message}"; }
    }

    // ── Smart search — Supplier ────────────────────────────────────────────
    partial void OnSupplierSearchTextChanged(string value)
    {
        if (_isSelectingSupplier) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredSuppliersList = new ObservableCollection<Supplier>(Suppliers);
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
        SelectedSupplierId = supplier.SuppId;
        SupplierSearchText = supplier.SuppName ?? string.Empty;
        CurrentSupplierBalance = supplier.Bal ?? 0;
        CurrentCustomerBalance = 0;
        IsSupplierSearchPopupOpen = false;
        _isSelectingSupplier = false;

        FormItem.OwnerName = supplier.SuppName;
        FormItem.OwnerTel = supplier.Tel;
        FormItem.OwnerKawmy = supplier.Kawmy;
        FormItem.OwnerAdress = supplier.Adress;
        OnPropertyChanged(nameof(FormItem));

        LoadSourceCarsAsync().ConfigureAwait(false);
    }

    // ── Smart search — Customer (as source) ────────────────────────────────
    partial void OnCustomerSearchTextChanged(string value)
    {
        if (_isSelectingCustomer) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredCustomersList = new ObservableCollection<Customer>(Customers);
            IsCustomerSearchPopupOpen = FilteredCustomersList.Any();
            return;
        }
        var keywords = value.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = Customers.Where(c =>
        {
            var name = c.CusName?.ToLower() ?? string.Empty;
            var tel = c.Tel?.ToLower() ?? string.Empty;
            return keywords.All(k => name.Contains(k) || tel.Contains(k));
        });
        FilteredCustomersList = new ObservableCollection<Customer>(filtered);
        IsCustomerSearchPopupOpen = FilteredCustomersList.Any();
    }

    [RelayCommand]
    private void SelectCustomer(Customer customer)
    {
        if (customer == null) return;
        _isSelectingCustomer = true;
        SelectedSourceCustomerId = customer.CusId;
        CustomerSearchText = customer.CusName ?? string.Empty;
        CurrentCustomerBalance = customer.Bal ?? 0;
        CurrentSupplierBalance = 0;
        IsCustomerSearchPopupOpen = false;
        _isSelectingCustomer = false;

        FormItem.OwnerName = customer.CusName;
        FormItem.OwnerTel = customer.Tel;
        FormItem.OwnerKawmy = customer.Kawmy;
        FormItem.OwnerAdress = customer.Adress;
        OnPropertyChanged(nameof(FormItem));

        LoadSourceCarsAsync().ConfigureAwait(false);
    }

    // ── Load cars owned by selected source ─────────────────────────────────
    private async Task LoadSourceCarsAsync()
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            IEnumerable<Car> cars;
            if (IsFromCustomer && SelectedSourceCustomerId > 0)
                cars = await db.QueryAsync<Car>(
                    "SELECT * FROM Cars WHERE OwnerID = @CusId",
                    new { CusId = SelectedSourceCustomerId });
            else if (!IsFromCustomer && SelectedSupplierId > 0)
                cars = await db.QueryAsync<Car>(
                    "SELECT * FROM Cars WHERE OwnerID IS NULL AND IsLocalSupplier = 1 AND SupplierID = @SuppId AND StatusID = 1",
                    new { SuppId = SelectedSupplierId });
            else
                cars = [];

            SourceCars = new ObservableCollection<Car>(cars);
        }
        catch (Exception ex) { StatusMessage = $"خطأ في تحميل الموتوسيكلات: {ex.Message}"; }
    }

    partial void OnSelectedExistingCarIdChanged(int value)
    {
        if (value <= 0) return;
        var car = SourceCars.FirstOrDefault(c => c.CarId == value);
        if (car == null) return;
        _carModelId = car.ModelId;
        var model = CarModels.FirstOrDefault(m => m.ModelId == car.ModelId);
        CarModelName = model?.ModelName ?? string.Empty;

        _carColorId = car.ColorId;
        var color = Colors.FirstOrDefault(c => c.ColorId == car.ColorId);
        CarColorName = color?.ColorName ?? string.Empty;

        if (model != null)
        {
            var brand = CarBrands.FirstOrDefault(b => b.BrandId == model.BrandId);
            CarBrandName = brand?.BrandName ?? string.Empty;
            IsBrandEnabled = false;
        }
        else
        {
            CarBrandName = string.Empty;
            IsBrandEnabled = true;
        }

        CarYearNo = car.YearNo;
        CarChassisNo = car.ChassisNo;
        CarMotorNo = car.MotorNo;
        CarPlateNo = car.PlateNo;
        CarNotes = car.Notes;
    }

    partial void OnIsFromCustomerChanged(bool value)
    {
        if (value)
        {
            _isSelectingSupplier = true;
            SupplierSearchText = string.Empty; SelectedSupplierId = 0;
            _isSelectingSupplier = false;
        }
        else
        {
            _isSelectingCustomer = true;
            CustomerSearchText = string.Empty; SelectedSourceCustomerId = 0;
            _isSelectingCustomer = false;
        }
        SourceCars.Clear();
        IsExistingCar = false;
    }

    // ── Payments ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void AddPayment()
    {
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;
        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();
        CurrentPayment = new BuyCarPayment
        {
            BuyId = FormItem.BuyId,
            PayDate = FormItem.BuyDate.AddSeconds(20),
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };
    }

    [RelayCommand]
    private void RemovePayment(BuyCarPayment payment)
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
        UpdateRemaining();
    }

    private void UpdateRemaining() =>
        Remaining = (FormItem?.Net ?? 0) - TotalPayed;

    public void HandleCashModeChanged()
    {
        if (FormItem == null) return;
        IsCashPaymentMode = FormItem.IsCash;

        if (FormItem.IsCash)
        {
            FormPayments.Clear();
            FormPayments.Add(new BuyCarPayment
            {
                BuyId = FormItem.BuyId,
                PayDate = FormItem.BuyDate.AddSeconds(20),
                PayMoney = FormItem.Net,
                CashId = SelectedCashId,
                Notes = "سداد كامل (كاش)"
            });
            CalculatePayedTotal();
        }
        else
        {
            FormPayments.Clear();
            CalculatePayedTotal();
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────
    private static BuyCar CloneInvoice(BuyCar s) => new()
    {
        BuyId = s.BuyId,
        BuyDate = s.BuyDate,
        OwnerName = s.OwnerName,
        OwnerTel = s.OwnerTel,
        OwnerKawmy = s.OwnerKawmy,
        OwnerAdress = s.OwnerAdress,
        CarId = s.CarId,
        Mileage = s.Mileage,
        Total = s.Total,
        Net = s.Net,
        Notes = s.Notes,
        AddUser = s.AddUser,
        AddDate = s.AddDate,
        AddPc = s.AddPc,
        IsCash = s.IsCash,
        IsTax = s.IsTax,
        VatTax = s.VatTax,
        Tax = s.Tax,
        TaxNo = s.TaxNo
    };

    private void CalculateTotalsInternal()
    {
        if (FormItem == null) return;
        NetBeforeTax = FormItem.Total;

        if (FormItem.IsTax)
        {
            FormItem.VatTax = Math.Round(NetBeforeTax * (VatTaxPercent / 100.0), 2);
            FormItem.Tax = Math.Round(NetBeforeTax * (WhtTaxPercent / 100.0), 2);
        }
        else
        {
            FormItem.Tax = 0; FormItem.VatTax = 0;
        }

        FormItem.Net = NetBeforeTax + FormItem.VatTax - FormItem.Tax;

        if (IsCashPaymentMode && FormPayments.Any())
        {
            FormPayments[0].PayMoney = FormItem.Net;
            CalculatePayedTotal();
        }
        else
        {
            UpdateRemaining();
        }

        OnPropertyChanged(nameof(FormItem));
        OnPropertyChanged(nameof(FormTotal));
        OnPropertyChanged(nameof(FormIsTax));
    }

    [RelayCommand]
    private async Task PrintInvoiceAsync()
    {
        if (FormItem == null || FormItem.BuyId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ الفاتورة أو اختيار فاتورة أولاً لطباعتها.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

            // الرصيد السابق: مورد أو عميل حسب مصدر الموتوسيكل
            double previousBalance = 0;
            if (IsFromCustomer && SelectedSourceCustomerId > 0)
                previousBalance = await _compositeRepo.GetCustomerOldBalanceAsync(SelectedSourceCustomerId, FormItem.BuyDate);
            else if (!IsFromCustomer && SelectedSupplierId > 0)
                previousBalance = await _compositeRepo.GetSupplierOldBalanceAsync(SelectedSupplierId, FormItem.BuyDate);

            // قائمة المدفوعات للفاتورة الآجلة
            var paymentsList = new System.Collections.Generic.List<(double Amount, string CashName, string Notes)>();
            foreach (var p in FormPayments)
            {
                var cashName = Cashes.FirstOrDefault(c => c.CashId == p.CashId)?.CashName ?? "";
                paymentsList.Add((p.PayMoney, cashName, p.Notes ?? ""));
            }

            var model = new MotorBike.Services.BuyCarInvoiceModel
            {
                InvoiceNo = FormItem.BuyId.ToString(),
                IssueDate = FormItem.BuyDate.ToString("yyyy-MM-dd"),
                Time = FormItem.AddDate.ToString("hh:mm tt") ?? "-",
                IsCash = FormItem.IsCash,
                Notes = FormItem.Notes ?? "",
                OwnerName = FormItem.OwnerName ?? "",
                OwnerTel = FormItem.OwnerTel ?? "",
                OwnerAddress = FormItem.OwnerAdress ?? "",
                OwnerKawmy = FormItem.OwnerKawmy ?? "",
                CarModel = CarModelName ?? "",
                CarBrand = CarBrandName ?? "",
                ChassisNo = CarChassisNo ?? "",
                MotorNo = CarMotorNo ?? "",
                PlateNo = CarPlateNo ?? "",
                ColorName = CarColorName ?? "",
                YearNo = CarYearNo,
                Mileage = FormItem.Mileage ?? 0,
                Total = FormItem.Total,
                IsTax = FormItem.IsTax,
                VatTax = FormItem.VatTax,
                WhtTax = FormItem.Tax,
                NetAmount = FormItem.Net,
                PreviousBalance = previousBalance,
                PaidAmount = TotalPayed,
                RemainingAmount = Remaining,
                Payments = paymentsList,
                IsSupplier = !IsFromCustomer
            };

            var document = new MotorBike.Services.BuyCarInvoiceDocument(model, company);
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "فاتورة شراء موتوسيكل رقم " + FormItem.BuyId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}