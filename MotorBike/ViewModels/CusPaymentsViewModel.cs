using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CusPaymentsViewModel : LookupViewModelBase<CusPayment>
{
    private readonly IRepository<Customer> _customerRepo;
    private readonly IRepository<Cash> _cashRepo;

    [ObservableProperty] private ObservableCollection<Customer> _customers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];

    public ObservableCollection<KeyValuePair<byte, string>> PayTypes { get; } =
    [
        new(0, "سداد لعميل"),
        new(1, "تحصيل من عميل"),
        new(2, "رد مبلغ"),
        new(3, "خصم/تسوية")
    ];

    public CusPaymentsViewModel(
        IRepository<CusPayment> repository,
        IRepository<Customer> customerRepo,
        IRepository<Cash> cashRepo) : base(repository)
    {
        _customerRepo = customerRepo;
        _cashRepo = cashRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var customers = await _customerRepo.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers.Where(x => x.Active));

            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(CusPayment entity) => entity.PayId;
    protected override bool IsNewRecord(CusPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(CusPayment entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(CusPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;
        entity.PayType = 1; // Default: تحصيل من عميل

        if (Customers.Any()) entity.CusId = Customers.First().CusId;
        if (CashList.Any()) entity.CashId = CashList.First().CashId;
    }

}
