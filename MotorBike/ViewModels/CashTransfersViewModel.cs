using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CashTransfersViewModel : LookupViewModelBase<CashTransfer>
{
    private readonly IRepository<Cash> _cashRepo;

    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];

    public CashTransfersViewModel(
        IRepository<CashTransfer> repository,
        IRepository<Cash> cashRepo) : base(repository)
    {
        _cashRepo = cashRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(CashTransfer entity) => entity.PayId;
    protected override bool IsNewRecord(CashTransfer entity) => entity.PayId == 0;
    protected override void SetEntityId(CashTransfer entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(CashTransfer entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;

        if (CashList.Count >= 2)
        {
            entity.CashId = CashList[0].CashId;
            entity.CashTo = CashList[1].CashId;
        }
        else if (CashList.Any())
        {
            entity.CashId = CashList[0].CashId;
            entity.CashTo = CashList[0].CashId;
        }
    }

}
