using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CashViewModel : LookupViewModelBase<Cash>
{
    private readonly IRepository<Omla> _omlaRepository;

    [ObservableProperty]
    private ObservableCollection<Omla> _currencies = [];

    public CashViewModel(IRepository<Cash> repository, IRepository<Omla> omlaRepository)
        : base(repository)
    {
        _omlaRepository = omlaRepository;
    }

    protected override object GetEntityId(Cash entity) => entity.CashId;
    protected override bool IsNewRecord(Cash entity) => entity.CashId == 0;
    protected override void SetEntityId(Cash entity, int id) => entity.CashId = id;

    protected override void SetDefaultValues(Cash entity)
    {
        base.SetDefaultValues(entity);
        entity.OpenDate = DateTime.Today;
        entity.OmlaRate = 1m;
    }

    [RelayCommand]
    public async Task LoadCurrenciesAsync()
    {
        var currencies = await _omlaRepository.GetAllAsync();
        Currencies = new ObservableCollection<Omla>(currencies);
    }
}
