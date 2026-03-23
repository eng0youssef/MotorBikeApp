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

public partial class SalesCarViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<SalesCar> _salesCarRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<Car> _carRepository;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty] private ObservableCollection<Customer> _customers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<Car> _cars = [];

    [ObservableProperty] private ObservableCollection<SalesCar> _invoices = [];
    [ObservableProperty] private ObservableCollection<SalesCar> _filteredInvoices = [];

    [ObservableProperty] private SalesCar _formItem = new();
    [ObservableProperty] private ObservableCollection<SalesCarPayment> _formPayments = [];
    [ObservableProperty] private SalesCarPayment _currentPayment = new();

    [ObservableProperty] private SalesCar? _selectedInvoice;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

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

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var customers = await _customerRepository.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers);

            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes);

            var cars = await _carRepository.GetAllAsync();
            Cars = new ObservableCollection<Car>(cars);

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
        var data = await _salesCarRepository.GetAllAsync();
        Invoices = new ObservableCollection<SalesCar>(data);
        FilterInvoices();
    }

    partial void OnSearchTextChanged(string value) => FilterInvoices();

    private void FilterInvoices()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            FilteredInvoices = new ObservableCollection<SalesCar>(Invoices);
        else
        {
            var lower = SearchText.ToLower();
            FilteredInvoices = new ObservableCollection<SalesCar>(
                Invoices.Where(i => i.SalesId.ToString().Contains(lower) ||
                    (Customers.FirstOrDefault(c => c.CusId == i.CusId)?.CusName?.ToLower().Contains(lower) == true)));
        }
    }

    [RelayCommand] private void ShowSearchPanel() { IsSearchPanelVisible = true; SearchText = ""; FilterInvoices(); }
    [RelayCommand] private void HideSearchPanel() { IsSearchPanelVisible = false; }

    partial void OnSelectedInvoiceChanged(SalesCar? value)
    {
        if (value is not null && !IsEditing)
        {
            IsSearchPanelVisible = false;
            FormItem = CloneInvoice(value);
            LoadPaymentsAsync(value.SalesId).ConfigureAwait(false);
        }
    }

    private async Task LoadPaymentsAsync(int salesId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var payments = await db.QueryAsync<SalesCarPayment>("SELECT * FROM Sales_Car_Payments WHERE SalesId = @SalesId", new { SalesId = salesId });
            FormPayments = new ObservableCollection<SalesCarPayment>(payments);
            CalculatePayedTotal();
        }
        catch (Exception ex) { StatusMessage = "خطأ في تحميل المدفوعات: " + ex.Message; }
    }

    [RelayCommand]
    public async Task AddNewAsync()
    {
        var item = new SalesCar
        {
            SalesDate = DateTime.Now,
            CusId = Customers.FirstOrDefault()?.CusId ?? 0,
            AddPc = Environment.MachineName,
            AddDate = DateTime.Now
        };
        await Task.CompletedTask;

        _isInsertMode = true;
        IsEditing = true;
        SelectedInvoice = null;
        FormItem = item;
        FormPayments.Clear();
        TotalPayed = 0;
        CurrentPayment = new SalesCarPayment { SalesId = item.SalesId, PayDate = DateTime.Now, CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
    }

    [RelayCommand]
    public void EditSelected()
    {
        if (SelectedInvoice is null) return;
        FormItem = CloneInvoice(SelectedInvoice);
        _isInsertMode = false;
        IsEditing = true;
    }

    [RelayCommand]
    public void CancelEdit()
    {
        _isInsertMode = false;
        IsEditing = false;
        FormItem = new SalesCar();
        FormPayments.Clear();
        TotalPayed = 0;
        CurrentPayment = new SalesCarPayment { PayDate = DateTime.Now };
        StatusMessage = null;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;
        if (FormItem.CusId <= 0)
        { StatusMessage = "⚠️ يجب اختيار العميل."; return; }

        try
        {
            var affectedCashIds = FormPayments.Select(p => p.CashId).Where(id => id > 0).Distinct().ToList();
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
            }

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                if (_isInsertMode)
                {
                    FormItem.SalesId = await _salesCarRepository.GetNextIdAsync();
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                        INSERT INTO Sales_Car (Sales_ID, SalesDate, CusId, CarID, Mileage, Total, Notes, AddDate, AddPc, AddUser) 
                        VALUES (@SalesId, @SalesDate, @CusId, @CarId, @Mileage, @Total, @Notes, @AddDate, @AddPc, @AddUser)", FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                        UPDATE Sales_Car SET SalesDate=@SalesDate, CusId=@CusId, CarID=@CarId, Mileage=@Mileage, 
                        Total=@Total, Notes=@Notes, EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser
                        WHERE Sales_ID = @SalesId", FormItem, tx);
                    await db.ExecuteAsync("DELETE FROM Sales_Car_Payments WHERE SalesId = @SalesId", new { SalesId = FormItem.SalesId }, tx);
                }

                int maxPayId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(Pay_ID), 0) FROM Sales_Car_Payments", transaction: tx);
                foreach (var p in FormPayments)
                {
                    // Ensure valid dates to prevent SqlDateTime overflow (1/1/1753)
                    if (p.PayDate < new DateTime(1753, 1, 1)) p.PayDate = DateTime.Now;

                    p.PayId = ++maxPayId;
                    p.SalesId = FormItem.SalesId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Sales_Car_Payments (Pay_ID, PayDate, PayMoney, CashID, Notes, SalesID) 
                        VALUES (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @SalesId)", p, tx);
                }

                tx.Commit();
                StatusMessage = "تم الحفظ بنجاح ✓";
            }
            catch { tx.Rollback(); throw; }

            foreach (var cashId in affectedCashIds)
                await _compositeRepo.RecalcBalanceForCashAsync(cashId);

            if (oldCusId.HasValue && oldCusId.Value != FormItem.CusId)
                await _compositeRepo.RecalcBalanceForCustomerAsync(oldCusId.Value);
            await _compositeRepo.RecalcBalanceForCustomerAsync(FormItem.CusId);

            _isInsertMode = false;
            IsEditing = false;
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحفظ: {ex.Message}"; }
    }

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
                await db.ExecuteAsync("DELETE FROM Sales_Car_Payments WHERE SalesId = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
                await db.ExecuteAsync("DELETE FROM Sales_Car WHERE Sales_ID = @SalesId", new { SalesId = SelectedInvoice.SalesId }, tx);
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
            await LoadInvoicesAsync();
        }
        catch (Exception ex) { StatusMessage = $"خطأ في الحذف: {ex.Message}"; }
    }

    // --- Payments ---
    [RelayCommand]
    private void AddPayment()
    {
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;
        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();
        CurrentPayment = new SalesCarPayment { SalesId = FormItem.SalesId, PayDate = DateTime.Now, CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
    }

    [RelayCommand]
    private void RemovePayment(SalesCarPayment payment)
    {
        if (payment != null && FormPayments.Contains(payment)) { FormPayments.Remove(payment); CalculatePayedTotal(); }
    }

    private void CalculatePayedTotal() => TotalPayed = FormPayments.Sum(p => p.PayMoney);

    private SalesCar CloneInvoice(SalesCar s) => new()
    {
        SalesId = s.SalesId, SalesDate = s.SalesDate, CusId = s.CusId, CarId = s.CarId,
        Mileage = s.Mileage, Total = s.Total, Notes = s.Notes,
        AddUser = s.AddUser, AddDate = s.AddDate, AddPc = s.AddPc
    };
}
