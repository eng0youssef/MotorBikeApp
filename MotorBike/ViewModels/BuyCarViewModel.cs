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
using System.Windows.Media;
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
    private readonly CompositeKeyRepository _compositeRepo;

    // ── Lookup collections ────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<CarModel> _carModels = [];
    [ObservableProperty] private ObservableCollection<Color> _colors = [];

    // ── Invoice lists ─────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BuyCar> _invoices = [];
    [ObservableProperty] private ObservableCollection<BuyCar> _filteredInvoices = [];

    // ── Invoice form ──────────────────────────────────────────────────────
    [ObservableProperty] private BuyCar _formItem = new();

    // ── Inline car fields ─────────────────────────────────────────────────
    [ObservableProperty] private int _carModelId;
    [ObservableProperty] private short _carYearNo = (short)DateTime.Now.Year;
    [ObservableProperty] private int _carColorId;
    [ObservableProperty] private string? _carChassisNo;
    [ObservableProperty] private string? _carMotorNo;
    [ObservableProperty] private string? _carPlateNo;
    [ObservableProperty] private string? _carNotes;
    [ObservableProperty] private bool _carActive = true;

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

    // ── Search ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

    private bool _isPaymentsPopupOpen;
    public bool IsPaymentsPopupOpen
    {
        get => _isPaymentsPopupOpen;
        set { _isPaymentsPopupOpen = value; OnPropertyChanged(); }
    }

    public ICommand OpenPaymentsPopupCommand => new RelayCommand(() => IsPaymentsPopupOpen = true);
    public ICommand ClosePaymentsPopupCommand => new RelayCommand(() => IsPaymentsPopupOpen = false);

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

    // ── Proxy Properties for Tax Calculation ───────────────────────────
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

    //public bool FormIsTax
    //{
    //    get => FormItem?.IsTax ?? false;
    //    set
    //    {
    //        if (FormItem != null && FormItem.IsTax != value)
    //        {
    //            FormItem.IsTax = value;
    //            OnPropertyChanged();
    //            CalculateTotalsInternal();
    //        }
    //    }
    //}

    // ── Tax ───────────────────────────────────────────────────────────────
    [ObservableProperty] private double _netBeforeTax;

    private double _vatTaxPercent;
    public double VatTaxPercent
    {
        get => _vatTaxPercent;
        set
        {
            if (SetProperty(ref _vatTaxPercent, value))
            {
                CalculateTotalsInternal();
            }
        }
    }

    private double _whtTaxPercent;
    public double WhtTaxPercent
    {
        get => _whtTaxPercent;
        set
        {
            if (SetProperty(ref _whtTaxPercent, value))
            {
                CalculateTotalsInternal();
            }
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────
    public BuyCarViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<BuyCar> buyCarRepository,
        IRepository<Cash> cashRepository,
        IRepository<Car> carRepository,
        IRepository<CarModel> carModelRepository,
        IRepository<Color> colorRepository,
        CompositeKeyRepository compositeRepo)
    {
        _dbFactory = dbFactory;
        _buyCarRepository = buyCarRepository;
        _cashRepository = cashRepository;
        _carRepository = carRepository;
        _carModelRepository = carModelRepository;
        _colorRepository = colorRepository;
        _compositeRepo = compositeRepo;
    }

    // ── Initial data load ─────────────────────────────────────────────────
    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes);

            var models = await _carModelRepository.GetAllAsync();
            CarModels = new ObservableCollection<CarModel>(models.Where(m => m.Active));

            var colors = await _colorRepository.GetAllAsync();
            Colors = new ObservableCollection<Color>(colors.Where(c => c.Active));

            // Set defaults for the car ComboBoxes
            CarModelId = CarModels.FirstOrDefault()?.ModelId ?? 0;
            CarColorId = Colors.FirstOrDefault()?.ColorId ?? 0;

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

    // ── Selected invoice → load form + car details ────────────────────────
    partial void OnSelectedInvoiceChanged(BuyCar? value)
    {
        if (value is not null)
        {
            IsSearchPanelVisible = false;
            _isInsertMode = false;
            IsEditing = false; // View mode after selection
            
            FormItem = CloneInvoice(value);

            // Infer tax percentages from saved amounts
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
                _vatTaxPercent = 0;
                _whtTaxPercent = 0;
                OnPropertyChanged(nameof(VatTaxPercent));
                OnPropertyChanged(nameof(WhtTaxPercent));
            }
            
            CalculateTotalsInternal();

            Task.Run(async () =>
            {
                await LoadPaymentsAsync(value.BuyId);
                if (value.CarId.HasValue) 
                    await LoadCarDetailsAsync(value.CarId.Value);
            });
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
            CarModelId = car.ModelId;
            CarYearNo = car.YearNo;
            CarColorId = car.ColorId;
            CarChassisNo = car.ChassisNo;
            CarMotorNo = car.MotorNo;
            CarPlateNo = car.PlateNo;
            CarNotes = car.Notes;
            CarActive = car.Active;
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

    // ── Add new invoice ───────────────────────────────────────────────────
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
            AddDate = DateTime.Now
        };

        // Reset car fields
        CarModelId = CarModels.FirstOrDefault()?.ModelId ?? 0;
        CarYearNo = (short)DateTime.Now.Year;
        CarColorId = Colors.FirstOrDefault()?.ColorId ?? 0;
        CarChassisNo = null;
        CarMotorNo = null;
        CarPlateNo = null;
        CarNotes = null;
        CarActive = true;

        FormPayments.Clear();
        TotalPayed = 0;
        CurrentPayment = new BuyCarPayment
        {
            PayDate = DateTime.Now,
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };

        VatTaxPercent = 0;
        WhtTaxPercent = 0;
    }

    // ── Edit selected ─────────────────────────────────────────────────────
    [RelayCommand]
    public void EditSelected()
    {
        if (SelectedInvoice is null) return;
        FormItem = CloneInvoice(SelectedInvoice);
        _isInsertMode = false;
        IsEditing = true;
        // Car fields already loaded in OnSelectedInvoiceChanged
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
        CarChassisNo = null;
        CarMotorNo = null;
        CarPlateNo = null;
        StatusMessage = null;
        VatTaxPercent = 0;
        WhtTaxPercent = 0;
    }

    // ── Save ──────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;

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

                // Calculate tax values before saving
                CalculateTotalsInternal();
                // Initialize common metadata fields early for both records
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

                // ── 1. Car record ────────────────────────────────────────
                if (_isInsertMode)
                {
                    // Create a brand-new Car row
                    int carId = await _carRepository.GetNextIdAsync();
                    await db.ExecuteAsync(@"
                        INSERT INTO Cars
                            (Car_ID, ModelID, YearNo, ChassisNo, MotorNo, PlateNo,
                             Mileage, ColorID, Active, Notes, AddDate, AddPC, AddUser)
                        VALUES
                            (@CarId, @ModelId, @YearNo, @ChassisNo, @MotorNo, @PlateNo,
                             @Mileage, @ColorId, @Active, @Notes, @AddDate, @AddPc, @AddUser)",
                        new
                        {
                            CarId = carId,
                            ModelId = CarModelId,
                            YearNo = CarYearNo,
                            ChassisNo = CarChassisNo,
                            MotorNo = CarMotorNo,
                            PlateNo = CarPlateNo,
                            Mileage = FormItem.Mileage,
                            ColorId = CarColorId,
                            Active = CarActive,
                            Notes = CarNotes,
                            AddDate = FormItem.AddDate,
                            AddPc = FormItem.AddPc,
                            AddUser = FormItem.AddUser
                        }, tx);

                    FormItem.CarId = carId;
                }
                else
                {
                    // Update the existing Car row
                    await db.ExecuteAsync(@"
                        UPDATE Cars
                        SET ModelID   = @ModelId,
                            YearNo    = @YearNo,
                            ChassisNo = @ChassisNo,
                            MotorNo   = @MotorNo,
                            PlateNo   = @PlateNo,
                            Mileage   = @Mileage,
                            ColorID   = @ColorId,
                            Active    = @Active,
                            Notes     = @Notes,
                            EditDate  = @EditDate,
                            EditPC    = @EditPc,
                            EditUser  = @EditUser
                        WHERE Car_ID  = @CarId",
                        new
                        {
                            CarId = FormItem.CarId,
                            ModelId = CarModelId,
                            YearNo = CarYearNo,
                            ChassisNo = CarChassisNo,
                            MotorNo = CarMotorNo,
                            PlateNo = CarPlateNo,
                            Mileage = FormItem.Mileage,
                            ColorId = CarColorId,
                            Active = CarActive,
                            Notes = CarNotes,
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
                             CarID, Mileage, Total, Notes, AddDate, AddPC, AddUser,
                             IsTax, VatTax, Tax, TaxNo)
                        VALUES
                            (@BuyId, @BuyDate, @OwnerName, @OwnerTel, @OwnerKawmy, @OwnerAdress,
                             @CarId, @Mileage, @Total, @Notes, @AddDate, @AddPc, @AddUser,
                             @IsTax, @VatTax, @Tax, @TaxNo)",
                        FormItem, tx);
                }
                else
                {
                    await db.ExecuteAsync(@"
                        UPDATE Buy_Car
                        SET BuyDate      = @BuyDate,
                            OwnerName    = @OwnerName,
                            OwnerTel     = @OwnerTel,
                            OwnerKawmy   = @OwnerKawmy,
                            OwnerAdress  = @OwnerAdress,
                            CarID        = @CarId,
                            Mileage      = @Mileage,
                            Total        = @Total,
                            Notes        = @Notes,
                            EditDate     = @EditDate,
                            EditPC       = @EditPc,
                            EditUser     = @EditUser,
                            IsTax        = @IsTax,
                            VatTax       = @VatTax,
                            Tax          = @Tax,
                            TaxNo        = @TaxNo
                        WHERE Buy_ID    = @BuyId",
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
                    // Ensure valid dates to prevent SqlDateTime overflow (1/1/1753)
                    if (p.PayDate < new DateTime(1753, 1, 1)) p.PayDate = DateTime.Now;

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
                // Retained IsEditing to enable further modifications
                StatusMessage = "تم حفظ فاتورة الشراء بنجاح ✓ ";
            }
            catch { tx.Rollback(); throw; }

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            _isInsertMode = false;
            // IsEditing = false; // left true so the user can continue editing
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحفظ: {ex.Message}"; }
    }

    // ── Delete ────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedInvoice is null) return;
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
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحذف: {ex.Message}"; }
    }

    // ── Payments ──────────────────────────────────────────────────────────
    [RelayCommand]
    private void AddPayment()
    {
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;
        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();
        CurrentPayment = new BuyCarPayment
        {
            BuyId = FormItem.BuyId,
            PayDate = DateTime.Now,
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

    private void CalculatePayedTotal() =>
        TotalPayed = FormPayments.Sum(p => p.PayMoney);

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
            FormItem.Tax = 0;
            FormItem.VatTax = 0;
        }

        FormItem.Net = NetBeforeTax + FormItem.VatTax - FormItem.Tax;
        
        // Notify UI of changes
        OnPropertyChanged(nameof(FormItem));
        OnPropertyChanged(nameof(FormTotal));
        OnPropertyChanged(nameof(FormIsTax));
    }
}