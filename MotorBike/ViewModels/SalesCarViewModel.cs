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

namespace MotorBike.ViewModels;

public partial class SalesCarViewModel : ObservableObject
{
    // ── Dependencies ────────────────────────────────────────────────────────
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<SalesCar> _salesCarRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<Car> _carRepository;
    private readonly CompositeKeyRepository _compositeRepo;

    // ── Lookup collections ─────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Customer> _customers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<Car> _cars = [];

    // ── Invoice lists ──────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<SalesCar> _invoices = [];
    [ObservableProperty] private ObservableCollection<SalesCar> _filteredInvoices = [];

    // ── Invoice form ───────────────────────────────────────────────────────
    [ObservableProperty] private SalesCar _formItem = new();
    [ObservableProperty] private ObservableCollection<SalesCarPayment> _formPayments = [];
    [ObservableProperty] private SalesCarPayment _currentPayment = new();

    // ── State ──────────────────────────────────────────────────────────────
    [ObservableProperty] private SalesCar? _selectedInvoice;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;
    [ObservableProperty] private double _remaining;
    [ObservableProperty] private bool _isCashPaymentMode;
    [ObservableProperty] private int _selectedCashId;
    [ObservableProperty] private double _currentCustomerBalance;
    [ObservableProperty] private double _currentSafeBalance;

    partial void OnSelectedCashIdChanged(int value)
    {
        if (FormPayments != null && FormPayments.Any()) FormPayments[0].CashId = value;
        if (CurrentPayment != null) CurrentPayment.CashId = value;
        CurrentSafeBalance = Cashes.FirstOrDefault(c => c.CashId == value)?.Bal ?? 0;
    }

    // ── Invoice search ─────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

    // ── Customer popup ─────────────────────────────────────────────────────
    [ObservableProperty] private string _customerSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Customer> _filteredCustomersList = [];
    [ObservableProperty] private bool _isCustomerPopupOpen;
    [ObservableProperty] private string? _selectedCustomerDisplay;

    // ── Car popup ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _carSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Car> _filteredCarsList = [];
    [ObservableProperty] private bool _isCarPopupOpen;
    [ObservableProperty] private string? _selectedCarDisplay;

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

    // ── Constructor ────────────────────────────────────────────────────────
    public SalesCarViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<SalesCar> salesCarRepository,
        IRepository<Customer> customerRepository,
        IRepository<Cash> cashRepository,
        IRepository<Car> carRepository,
        CompositeKeyRepository compositeRepo)
    {
        _dbFactory = dbFactory;
        _salesCarRepository = salesCarRepository;
        _customerRepository = customerRepository;
        _cashRepository = cashRepository;
        _carRepository = carRepository;
        _compositeRepo = compositeRepo;
    }

    // ── Initial data load ──────────────────────────────────────────────────
    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var customers = await _customerRepository.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers);
            FilteredCustomersList = new ObservableCollection<Customer>(customers);

            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes.Where(c => c.OmlaId == 0 || c.OmlaId == null));

            var cars = await _carRepository.GetAllAsync();
            var activeCars = cars.ToList();
            Cars = new ObservableCollection<Car>(activeCars);
            FilteredCarsList = new ObservableCollection<Car>(activeCars);

            await LoadInvoicesAsync();
            await AddNewAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في تحميل البيانات: {ex.Message}"; }
    }

    private async Task LoadInvoicesAsync()
    {
        var data = await _salesCarRepository.GetAllAsync();
        Invoices = new ObservableCollection<SalesCar>(data);
        FilterInvoices();
    }

    // ── Invoice filter ─────────────────────────────────────────────────────
    partial void OnSearchTextChanged(string value) => FilterInvoices();

    private void FilterInvoices()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredInvoices = new ObservableCollection<SalesCar>(Invoices);
            return;
        }
        var lower = SearchText.ToLower();
        FilteredInvoices = new ObservableCollection<SalesCar>(
            Invoices.Where(i =>
                i.SalesId.ToString().Contains(lower) ||
                (Customers.FirstOrDefault(c => c.CusId == i.CusId)?.CusName?
                    .ToLower().Contains(lower) == true)));
    }

    [RelayCommand] private void ShowSearchPanel() { IsSearchPanelVisible = true; SearchText = ""; FilterInvoices(); }
    [RelayCommand] private void HideSearchPanel() { IsSearchPanelVisible = false; }

    // ── Selected invoice ───────────────────────────────────────────────────
    partial void OnSelectedInvoiceChanged(SalesCar? value)
    {
        if (value is not null)
        {
            IsSearchPanelVisible = false;
            _isInsertMode = false;
            IsEditing = false; // View mode after selection
            
            FormItem = CloneInvoice(value);
            IsCashPaymentMode = FormItem.IsCash;
            SelectedCustomerDisplay = Customers.FirstOrDefault(c => c.CusId == value.CusId)?.CusName;
            CurrentCustomerBalance = Customers.FirstOrDefault(c => c.CusId == value.CusId)?.Bal ?? 0;
            SelectedCarDisplay = Cars.FirstOrDefault(c => c.CarId == value.CarId)?.ChassisNo;

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

            LoadPaymentsAsync(value.SalesId).ConfigureAwait(false);
        }
    }

    private async Task LoadPaymentsAsync(int salesId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var payments = await db.QueryAsync<SalesCarPayment>(
                "SELECT * FROM Sales_Car_Payments WHERE SalesId = @SalesId",
                new { SalesId = salesId });
            FormPayments = new ObservableCollection<SalesCarPayment>(payments);
            CalculatePayedTotal();
        }
        catch (Exception ex) { StatusMessage = "خطأ في تحميل المدفوعات: " + ex.Message; }
    }

    // ── Add new ────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task AddNewAsync()
    {
        await Task.CompletedTask;

        _isInsertMode = true;
        IsEditing = true;
        SelectedInvoice = null;

        FormItem = new SalesCar
        {
            SalesDate = DateTime.Now,
            IsCash = true,
            CusId = Customers.FirstOrDefault()?.CusId ?? 0,
            AddPc = Environment.MachineName,
            AddDate = DateTime.Now
        };
        IsCashPaymentMode = true;
        SelectedCashId = Cashes.FirstOrDefault()?.CashId ?? 0;

        FormPayments.Clear();
        TotalPayed = 0;
        CurrentPayment = new SalesCarPayment
        {
            SalesId = FormItem.SalesId,
            PayDate = FormItem.SalesDate.AddSeconds(20),
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };

        SelectedCarDisplay = null;
        SelectedCustomerDisplay = null;
        CurrentCustomerBalance = 0;
        CurrentSafeBalance = Cashes.FirstOrDefault(c => c.CashId == SelectedCashId)?.Bal ?? 0;

        // Re-seed popup lists
        FilteredCustomersList = new ObservableCollection<Customer>(Customers);
        FilteredCarsList = new ObservableCollection<Car>(Cars.Where(c => c.IsStock));

        VatTaxPercent = 0;
        WhtTaxPercent = 0;
    }

    // ── Edit selected ──────────────────────────────────────────────────────
    [RelayCommand]
    public void EditSelected()
    {
        if (SelectedInvoice is null) return;
        FormItem = CloneInvoice(SelectedInvoice);
        SelectedCustomerDisplay = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.CusName;
        CurrentCustomerBalance = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.Bal ?? 0;
        SelectedCarDisplay = Cars.FirstOrDefault(c => c.CarId == FormItem.CarId)?.ChassisNo;
        _isInsertMode = false;
        IsEditing = true;
        IsCashPaymentMode = FormItem.IsCash;
        if (FormItem.IsCash && FormPayments.Any())
            SelectedCashId = FormPayments.First().CashId;
        else
            SelectedCashId = Cashes.FirstOrDefault()?.CashId ?? 0;

        // Seed popup lists (include current car even if inactive)
        FilteredCustomersList = new ObservableCollection<Customer>(Customers);
        FilteredCarsList = new ObservableCollection<Car>(
            Cars.Where(c => c.IsStock || c.CarId == FormItem.CarId));
    }

    // ── Cancel ─────────────────────────────────────────────────────────────
    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false;
        IsEditing = false;
        FormItem = new SalesCar();
        FormPayments.Clear();
        TotalPayed = 0;
        CurrentPayment = new SalesCarPayment { PayDate = DateTime.Now };
        SelectedCarDisplay = null;
        SelectedCustomerDisplay = null;
        IsCustomerPopupOpen = false;
        IsCarPopupOpen = false;
        CurrentCustomerBalance = 0;
        CurrentSafeBalance = 0;
        SelectedCashId = 0;
        StatusMessage = null;
        VatTaxPercent = 0;
        WhtTaxPercent = 0;
    }

    // ── Save ───────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;
        if (FormItem.CusId <= 0) { StatusMessage = "⚠️ يجب اختيار العميل."; return; }
        if (FormItem.CarId <= 0) { StatusMessage = "⚠️ يجب اختيار الموتوسيكل."; return; }

        try
        {
            var affectedCashIds = FormPayments
                .Select(p => p.CashId).Where(id => id > 0).Distinct().ToList();
            int? oldCusId = null;

            if (!_isInsertMode)
            {
                using var dbPre = _dbFactory.CreateConnection();
                var oldCashIds = await dbPre.QueryAsync<int>(
                    "SELECT DISTINCT CashID FROM Sales_Car_Payments WHERE SalesID = @SalesId",
                    new { SalesId = FormItem.SalesId });
                foreach (var cid in oldCashIds)
                    if (cid > 0 && !affectedCashIds.Contains(cid)) affectedCashIds.Add(cid);

                oldCusId = await dbPre.QueryFirstOrDefaultAsync<int?>(
                    "SELECT CusId FROM Sales_Car WHERE Sales_ID = @SalesId",
                    new { SalesId = FormItem.SalesId });

                var oldCarId = await dbPre.QueryFirstOrDefaultAsync<int?>(
                    "SELECT CarID FROM Sales_Car WHERE Sales_ID = @SalesId",
                    new { SalesId = FormItem.SalesId });
                
                // If car changed, revert the old car
                if (oldCarId.HasValue && oldCarId.Value != FormItem.CarId)
                {
                    await dbPre.ExecuteAsync(
                        "UPDATE Cars SET IsStock = 1, StatusID = 1, OwnerID = NULL WHERE Car_ID = @OldCarId",
                        new { OldCarId = oldCarId.Value });
                }
            }

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                FormItem.IsCash = IsCashPaymentMode;
                
                if (IsCashPaymentMode)
                {
                    FormPayments.Clear();
                    FormPayments.Add(new SalesCarPayment
                    {
                        SalesId = FormItem.SalesId,
                        PayDate = FormItem.SalesDate.AddSeconds(20),
                        PayMoney = FormItem.Net,
                        CashId = SelectedCashId,
                        Notes = "دفع كاش للفاتورة"
                    });
                }

                if (FormItem.IsTax && string.IsNullOrWhiteSpace(FormItem.TaxNo))
                {
                    var maxTaxNoStr = await db.QueryFirstOrDefaultAsync<string>(
                        "SELECT CAST(MAX(CAST(TaxNo AS INT)) AS VARCHAR) FROM Sales_Car WHERE ISNUMERIC(TaxNo) = 1 AND TaxNo NOT LIKE '%[^0-9]%'",
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

                // Mark car as sold / inactive
                await db.ExecuteAsync(
                    "UPDATE Cars SET IsStock = 0, StatusID = 2, OwnerID = @CusId WHERE Car_ID = @CarId",
                    new { CarId = FormItem.CarId, CusId = FormItem.CusId }, tx);

                if (_isInsertMode)
                {
                    FormItem.SalesId = await _salesCarRepository.GetNextIdAsync();
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                    INSERT INTO Sales_Car
                        (Sales_ID, SalesDate, CusId, CarID, Mileage, Total, Net, Notes,
                         AddDate, AddPc, AddUser, IsTax, VatTax, Tax, TaxNo, IsCash)
                    VALUES
                        (@SalesId, @SalesDate, @CusId, @CarId, @Mileage, @Total, @Net, @Notes,
                         @AddDate, @AddPc, @AddUser, @IsTax, @VatTax, @Tax, @TaxNo, @IsCash)",
                        FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                    UPDATE Sales_Car
                    SET SalesDate = @SalesDate, CusId = @CusId, CarID = @CarId,
                        Mileage   = @Mileage,   Total = @Total,  Net = @Net,  Notes = @Notes,
                        EditDate  = @EditDate,  EditPc = @EditPc, EditUser = @EditUser,
                        IsTax = @IsTax, VatTax = @VatTax, Tax = @Tax, TaxNo = @TaxNo, IsCash = @IsCash
                    WHERE Sales_ID = @SalesId",
                        FormItem, tx);
                    await db.ExecuteAsync(
                        "DELETE FROM Sales_Car_Payments WHERE SalesId = @SalesId",
                        new { SalesId = FormItem.SalesId }, tx);
                }

                int maxPayId = await db.QuerySingleAsync<int>(
                    "SELECT ISNULL(MAX(Pay_ID), 0) FROM Sales_Car_Payments",
                    transaction: tx);

                foreach (var p in FormPayments)
                {
                    p.PayDate = FormItem.SalesDate.AddSeconds(20);
                    p.PayId = ++maxPayId;
                    p.SalesId = FormItem.SalesId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Sales_Car_Payments
                            (Pay_ID, PayDate, PayMoney, CashID, Notes, SalesID)
                        VALUES
                            (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @SalesId)",
                        p, tx);
                }

                tx.Commit();
                StatusMessage = "تم الحفظ بنجاح ✓";
            }
            catch { tx.Rollback(); throw; }

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);
            
            if (IsCashPaymentMode && !affectedCashIds.Contains(SelectedCashId))
                await _compositeRepo.RecalcBalanceForCashAsync(SelectedCashId);

            if (oldCusId.HasValue && oldCusId.Value != FormItem.CusId)
                await _compositeRepo.RecalcBalanceForCustomerAsync(oldCusId.Value);
            await _compositeRepo.RecalcBalanceForCustomerAsync(FormItem.CusId);

            _isInsertMode = false;
            await LoadInvoicesAsync();
            var savedInvoice = Invoices.FirstOrDefault(x => x.SalesId == FormItem.SalesId);
            if (savedInvoice != null)
            {
                SelectedInvoice = savedInvoice;
                IsEditing = true;
            }
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحفظ: {ex.Message}"; }
    }

    // ── Delete ─────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (SelectedInvoice is null) return;
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
                    "DELETE FROM Sales_Car_Payments WHERE SalesId = @SalesId",
                    new { SalesId = SelectedInvoice.SalesId }, tx);
                await db.ExecuteAsync(
                    "DELETE FROM Sales_Car WHERE Sales_ID = @SalesId",
                    new { SalesId = SelectedInvoice.SalesId }, tx);
                
                // Revert car back to inventory
                await db.ExecuteAsync(
                    "UPDATE Cars SET IsStock = 1, StatusID = 1, OwnerID = NULL WHERE Car_ID = @CarId",
                    new { CarId = SelectedInvoice.CarId }, tx);
                    
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);
            await _compositeRepo.RecalcBalanceForCustomerAsync(SelectedInvoice.CusId);

            StatusMessage = "تم حذف الفاتورة بنجاح ✓";
            IsEditing = false;
            FormItem = new SalesCar();
            FormPayments.Clear();
            TotalPayed = 0;
            SelectedInvoice = null;
            SelectedCarDisplay = null;
            SelectedCustomerDisplay = null;
            CurrentCustomerBalance = 0;
            CurrentSafeBalance = 0;
            SelectedCashId = 0;
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحذف: {ex.Message}"; }
    }

    // ── Payments ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void AddPayment()
    {
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;
        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();
        CurrentPayment = new SalesCarPayment
        {
            SalesId = FormItem.SalesId,
            PayDate = FormItem.SalesDate.AddSeconds(20),
            CashId = Cashes.FirstOrDefault()?.CashId ?? 0
        };
    }

    [RelayCommand]
    private void RemovePayment(SalesCarPayment payment)
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

    private void UpdateRemaining()
    {
        Remaining = (FormItem?.Net ?? 0) - TotalPayed;
    }

    public void HandleCashModeChanged()
    {
        if (FormItem == null) return;
        IsCashPaymentMode = FormItem.IsCash;

        if (FormItem.IsCash)
        {
            FormPayments.Clear();
            FormPayments.Add(new SalesCarPayment
            {
                SalesId = FormItem.SalesId,
                PayDate = FormItem.SalesDate.AddSeconds(20),
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

    // ── Customer popup commands ────────────────────────────────────────────

    [RelayCommand]
    private void OpenCustomerSearch()
    {
        IsCarPopupOpen = false;   // close car popup if open
        CustomerSearchText = string.Empty;
        FilteredCustomersList = new ObservableCollection<Customer>(Customers);
        IsCustomerPopupOpen = true;
    }

    [RelayCommand]
    private void CloseCustomerSearch()
    {
        IsCustomerPopupOpen = false;
        CustomerSearchText = string.Empty;
    }

    [RelayCommand]
    private void SelectCustomer(Customer customer)
    {
        if (customer is null) return;
        FormItem.CusId = customer.CusId;
        SelectedCustomerDisplay = customer.CusName;
        CurrentCustomerBalance = customer.Bal ?? 0;
        IsCustomerPopupOpen = false;
        CustomerSearchText = string.Empty;
    }

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            FilteredCustomersList = new ObservableCollection<Customer>(Customers);
        else
        {
            var lower = value.ToLower();
            FilteredCustomersList = new ObservableCollection<Customer>(
                Customers.Where(c =>
                    c.CusName?.ToLower().Contains(lower) == true ||
                    c.CusId.ToString().Contains(lower)));
        }
    }

    // ── Car popup commands ─────────────────────────────────────────────────

    [RelayCommand]
    private void OpenCarSearch()
    {
        IsCustomerPopupOpen = false;   // close customer popup if open
        CarSearchText = string.Empty;
        FilteredCarsList = new ObservableCollection<Car>(
            Cars.Where(c => c.IsStock || c.CarId == FormItem.CarId));
        IsCarPopupOpen = true;
    }

    [RelayCommand]
    private void CloseCarSearch()
    {
        IsCarPopupOpen = false;
        CarSearchText = string.Empty;
    }

    [RelayCommand]
    private void SelectCar(Car car)
    {
        if (car is null) return;
        FormItem.CarId = car.CarId;
        FormItem.Mileage = car.Mileage;  
        SelectedCarDisplay = car.ChassisNo;
        IsCarPopupOpen = false;
        CarSearchText = string.Empty;
        OnPropertyChanged(nameof(FormItem));
    }

    partial void OnCarSearchTextChanged(string value)
    {
        var pool = Cars.Where(c => c.IsStock || c.CarId == FormItem.CarId);
        if (string.IsNullOrWhiteSpace(value))
            FilteredCarsList = new ObservableCollection<Car>(pool);
        else
        {
            var lower = value.ToLower();
            FilteredCarsList = new ObservableCollection<Car>(
                pool.Where(c =>
                    c.ChassisNo?.ToLower().Contains(lower) == true ||
                    c.PlateNo?.ToLower().Contains(lower) == true ||
                    c.MotorNo?.ToLower().Contains(lower) == true));
        }
    }

    // ── Helper ─────────────────────────────────────────────────────────────
    private static SalesCar CloneInvoice(SalesCar s) => new()
    {
        SalesId = s.SalesId,
        SalesDate = s.SalesDate,
        CusId = s.CusId,
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
            FormItem.Tax = 0;
            FormItem.VatTax = 0;
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

        // Notify UI of changes
        OnPropertyChanged(nameof(FormItem));
        OnPropertyChanged(nameof(FormTotal));
        OnPropertyChanged(nameof(FormIsTax));
    }

    [RelayCommand]
    private async Task PrintInvoiceAsync()
    {
        if (FormItem == null || FormItem.SalesId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ الفاتورة أو اختيار فاتورة أولاً لطباعتها.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");
            double previousBalance = await _compositeRepo.GetCustomerOldBalanceAsync(FormItem.CusId, FormItem.SalesDate);

            var paymentsList = new System.Collections.Generic.List<(double Amount, string CashName, string Notes)>();
            foreach (var p in FormPayments)
            {
                var cashName = Cashes.FirstOrDefault(c => c.CashId == p.CashId)?.CashName ?? "";
                paymentsList.Add((p.PayMoney, cashName, p.Notes ?? ""));
            }

            // Load car details for printing
            string carModel = "", carBrand = "", chassisNo = "", motorNo = "", plateNo = "", colorName = "";
            int yearNo = 0, mileage = (int)FormItem.Mileage;
            if (FormItem.CarId > 0)
            {
                var car = await db.QuerySingleOrDefaultAsync<Car>(
                    "SELECT * FROM Cars WHERE Car_ID = @CarId", new { CarId = FormItem.CarId });
                if (car != null)
                {
                    chassisNo = car.ChassisNo ?? "";
                    motorNo = car.MotorNo ?? "";
                    plateNo = car.PlateNo ?? "";
                    yearNo = car.YearNo;
                    mileage = car.Mileage;
                    var mdl = await db.QuerySingleOrDefaultAsync<CarModel>(
                        "SELECT * FROM CarModels WHERE Model_ID = @ModelId", new { ModelId = car.ModelId });
                    if (mdl != null)
                    {
                        carModel = mdl.ModelName ?? "";
                        var brand = await db.QuerySingleOrDefaultAsync<CarBrand>(
                            "SELECT * FROM CarBrands WHERE Brand_ID = @BrandId", new { BrandId = mdl.BrandId });
                        carBrand = brand?.BrandName ?? "";
                    }
                    var clr = await db.QuerySingleOrDefaultAsync<dynamic>(
                        "SELECT ColorName FROM Colors WHERE Color_ID = @ColorId", new { ColorId = car.ColorId });
                    colorName = clr?.ColorName ?? "";
                }
            }

            var model = new MotorBike.Services.SalesCarInvoiceModel
            {
                InvoiceNo = FormItem.SalesId.ToString(),
                IssueDate = FormItem.SalesDate.ToString("yyyy-MM-dd"),
                Time = FormItem.AddDate.ToString("hh:mm tt") ?? "-",
                IsCash = FormItem.IsCash,
                CustomerName = Customers.FirstOrDefault(c => c.CusId == FormItem.CusId)?.CusName ?? "",
                Notes = FormItem.Notes ?? "",
                CarModel = carModel,
                CarBrand = carBrand,
                ChassisNo = chassisNo,
                MotorNo = motorNo,
                PlateNo = plateNo,
                ColorName = colorName,
                YearNo = yearNo,
                Mileage = mileage,
                Total = FormItem.Total,
                IsTax = FormItem.IsTax,
                VatTax = FormItem.VatTax,
                WhtTax = FormItem.Tax,
                NetAmount = FormItem.Net,
                PreviousBalance = previousBalance,
                PaidAmount = TotalPayed,
                RemainingAmount = Remaining,
                Payments = paymentsList
            };

            var document = new MotorBike.Services.SalesCarInvoiceDocument(model, company);
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "فاتورة بيع موتوسيكل رقم " + FormItem.SalesId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}