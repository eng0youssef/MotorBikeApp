using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class SuppPaymentsViewModel : LookupViewModelBase<SuppPayment>
{
    private readonly IRepository<Supplier> _supplierRepo;
    private readonly IRepository<Cash> _cashRepo;

    [ObservableProperty] private ObservableCollection<Supplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];

    public ObservableCollection<KeyValuePair<byte, string>> PayTypes { get; } =
    [
        new(0, "سداد لمورد"),
        new(1, "تحصيل من مورد"),
        new(2, "رد مبلغ"),
        new(3, "خصم/تسوية")
    ];

    public SuppPaymentsViewModel(
        IRepository<SuppPayment> repository,
        IRepository<Supplier> supplierRepo,
        IRepository<Cash> cashRepo) : base(repository)
    {
        _supplierRepo = supplierRepo;
        _cashRepo = cashRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var suppliers = await _supplierRepo.GetAllAsync();
            Suppliers = new ObservableCollection<Supplier>(suppliers.Where(x => x.Active));

            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(SuppPayment entity) => entity.PayId;
    protected override bool IsNewRecord(SuppPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(SuppPayment entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(SuppPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;
        entity.PayType = 0; // Default: سداد لمورد

        if (Suppliers.Any()) entity.SuppId = Suppliers.First().SuppId;
        if (CashList.Any()) entity.CashId = CashList.First().CashId;
    }

}
