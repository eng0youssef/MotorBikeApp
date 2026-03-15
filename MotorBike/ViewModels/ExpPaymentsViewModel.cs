using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ExpPaymentsViewModel : LookupViewModelBase<ExpPayment>
{
    private readonly IRepository<Expense> _expenseRepo;
    private readonly IRepository<Cash> _cashRepo;

    [ObservableProperty] private ObservableCollection<Expense> _expenses = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];

    public ExpPaymentsViewModel(
        IRepository<ExpPayment> repository,
        IRepository<Expense> expenseRepo,
        IRepository<Cash> cashRepo) : base(repository)
    {
        _expenseRepo = expenseRepo;
        _cashRepo = cashRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var expenses = await _expenseRepo.GetAllAsync();
            Expenses = new ObservableCollection<Expense>(expenses.Where(x => x.Active));

            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(ExpPayment entity) => entity.PayId;
    protected override bool IsNewRecord(ExpPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(ExpPayment entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(ExpPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;

        if (Expenses.Any()) entity.ExpId = Expenses.First().ExpId;
        if (CashList.Any()) entity.CashId = CashList.First().CashId;
    }

}
