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

public partial class BuyCarViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<BuyCar> _buyCarRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<Car> _carRepository;

    [ObservableProperty] private ObservableCollection<Cash> _cashes = [];
    [ObservableProperty] private ObservableCollection<Car> _cars = [];

    [ObservableProperty] private ObservableCollection<BuyCar> _invoices = [];
    [ObservableProperty] private ObservableCollection<BuyCar> _filteredInvoices = [];

    [ObservableProperty] private BuyCar _formItem = new();
    [ObservableProperty] private ObservableCollection<BuyCarPayment> _formPayments = [];
    [ObservableProperty] private BuyCarPayment _currentPayment = new();

    [ObservableProperty] private BuyCar? _selectedInvoice;
    [ObservableProperty] private bool _isEditing;
    private bool _isInsertMode;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _totalPayed;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchPanelVisible;

    public BuyCarViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<BuyCar> buyCarRepository,
        IRepository<Cash> cashRepository,
        IRepository<Car> carRepository)
    {
        _dbFactory = dbFactory;
        _buyCarRepository = buyCarRepository;
        _cashRepository = cashRepository;
        _carRepository = carRepository;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes);

            var cars = await _carRepository.GetAllAsync();
            Cars = new ObservableCollection<Car>(cars);

            await LoadInvoicesAsync();
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

    partial void OnSearchTextChanged(string value) => FilterInvoices();

    private void FilterInvoices()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            FilteredInvoices = new ObservableCollection<BuyCar>(Invoices);
        else
        {
            var lower = SearchText.ToLower();
            FilteredInvoices = new ObservableCollection<BuyCar>(
                Invoices.Where(i => i.BuyId.ToString().Contains(lower) ||
                    (i.OwnerName?.ToLower().Contains(lower) == true)));
        }
    }

    [RelayCommand] private void ShowSearchPanel() { IsSearchPanelVisible = true; SearchText = ""; FilterInvoices(); }
    [RelayCommand] private void HideSearchPanel() { IsSearchPanelVisible = false; }

    partial void OnSelectedInvoiceChanged(BuyCar? value)
    {
        if (value is not null && !IsEditing)
        {
            IsSearchPanelVisible = false;
            FormItem = CloneInvoice(value);
            LoadPaymentsAsync(value.BuyId).ConfigureAwait(false);
        }
    }

    private async Task LoadPaymentsAsync(int buyId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var payments = await db.QueryAsync<BuyCarPayment>("SELECT * FROM Buy_Car_Payments WHERE BuyId = @BuyId", new { BuyId = buyId });
            FormPayments = new ObservableCollection<BuyCarPayment>(payments);
            CalculatePayedTotal();
        }
        catch (Exception ex) { StatusMessage = "خطأ في تحميل المدفوعات: " + ex.Message; }
    }

    [RelayCommand]
    public async Task AddNewAsync()
    {
        var item = new BuyCar
        {
            BuyDate = DateTime.Now,
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
        CurrentPayment = new BuyCarPayment { BuyId = item.BuyId, PayDate = DateTime.Now, CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
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
        FormItem = new BuyCar();
        FormPayments.Clear();
        TotalPayed = 0;
        CurrentPayment = new BuyCarPayment();
        StatusMessage = null;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (FormItem is null) return;
        if (string.IsNullOrWhiteSpace(FormItem.OwnerName))
        { StatusMessage = "⚠️ يجب إدخال اسم المالك."; return; }

        try
        {
            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                if (_isInsertMode)
                {
                    FormItem.BuyId = await _buyCarRepository.GetNextIdAsync();
                    FormItem.AddPc ??= Environment.MachineName;
                    FormItem.AddDate = DateTime.Now;
                    FormItem.AddUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                        INSERT INTO Buy_Car (Buy_ID, BuyDate, OwnerName, OwnerTel, OwnerKawmy, OwnerAdress, CarID, Mileage, Total, Notes, AddDate, AddPc, AddUser) 
                        VALUES (@BuyId, @BuyDate, @OwnerName, @OwnerTel, @OwnerKawmy, @OwnerAdress, @CarId, @Mileage, @Total, @Notes, @AddDate, @AddPc, @AddUser)", FormItem, tx);
                }
                else
                {
                    FormItem.EditPc = Environment.MachineName;
                    FormItem.EditDate = DateTime.Now;
                    FormItem.EditUser = AppSession.CurrentUserId ?? 1;
                    await db.ExecuteAsync(@"
                        UPDATE Buy_Car SET BuyDate=@BuyDate, OwnerName=@OwnerName, OwnerTel=@OwnerTel, OwnerKawmy=@OwnerKawmy, 
                        OwnerAdress=@OwnerAdress, CarID=@CarId, Mileage=@Mileage, Total=@Total, Notes=@Notes,
                        EditDate=@EditDate, EditPc=@EditPc, EditUser=@EditUser
                        WHERE Buy_ID = @BuyId", FormItem, tx);
                    await db.ExecuteAsync("DELETE FROM Buy_Car_Payments WHERE BuyId = @BuyId", new { BuyId = FormItem.BuyId }, tx);
                }

                int maxPayId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(Pay_ID), 0) FROM Buy_Car_Payments", transaction: tx);
                foreach (var p in FormPayments)
                {
                    p.PayId = ++maxPayId;
                    p.BuyId = FormItem.BuyId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Buy_Car_Payments (Pay_ID, PayDate, PayMoney, CashID, Notes, BuyID) 
                        VALUES (@PayId, @PayDate, @PayMoney, @CashId, @Notes, @BuyId)", p, tx);
                }

                tx.Commit();
                StatusMessage = "تم الحفظ بنجاح ✓";
            }
            catch { tx.Rollback(); throw; }

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
            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync("DELETE FROM Buy_Car_Payments WHERE BuyId = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx);
                await db.ExecuteAsync("DELETE FROM Buy_Car WHERE Buy_ID = @BuyId", new { BuyId = SelectedInvoice.BuyId }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }

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

    // --- Payments ---
    [RelayCommand]
    private void AddPayment()
    {
        if (CurrentPayment.PayMoney <= 0 || CurrentPayment.CashId <= 0) return;
        FormPayments.Add(CurrentPayment);
        CalculatePayedTotal();
        CurrentPayment = new BuyCarPayment { BuyId = FormItem.BuyId, PayDate = DateTime.Now, CashId = Cashes.FirstOrDefault()?.CashId ?? 0 };
    }

    [RelayCommand]
    private void RemovePayment(BuyCarPayment payment)
    {
        if (payment != null && FormPayments.Contains(payment)) { FormPayments.Remove(payment); CalculatePayedTotal(); }
    }

    private void CalculatePayedTotal() => TotalPayed = FormPayments.Sum(p => p.PayMoney);

    private BuyCar CloneInvoice(BuyCar s) => new()
    {
        BuyId = s.BuyId, BuyDate = s.BuyDate, OwnerName = s.OwnerName, OwnerTel = s.OwnerTel,
        OwnerKawmy = s.OwnerKawmy, OwnerAdress = s.OwnerAdress, CarId = s.CarId, Mileage = s.Mileage,
        Total = s.Total, Notes = s.Notes, AddUser = s.AddUser, AddDate = s.AddDate, AddPc = s.AddPc
    };
}
