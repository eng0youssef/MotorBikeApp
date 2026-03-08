using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CustomersViewModel : LookupViewModelBase<Customer>
{
    private readonly IRepository<City> _cityRepository;
    private readonly IRepository<Omla> _omlaRepository;

    [ObservableProperty]
    private ObservableCollection<City> _cities = [];

    [ObservableProperty]
    private ObservableCollection<Omla> _omlas = [];

    public CustomersViewModel(
        IRepository<Customer> repository,
        IRepository<City> cityRepository,
        IRepository<Omla> omlaRepository) : base(repository)
    {
        _cityRepository = cityRepository;
        _omlaRepository = omlaRepository;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var citiesData = await _cityRepository.GetAllAsync();
            Cities = new ObservableCollection<City>(citiesData.Where(c => c.Active));

            var omlasData = await _omlaRepository.GetAllAsync();
            Omlas = new ObservableCollection<Omla>(omlasData.Where(o => o.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(Customer entity) => entity.CusId;
    protected override bool IsNewRecord(Customer entity) => entity.CusId == 0;
    protected override void SetEntityId(Customer entity, int id) => entity.CusId = id;

    protected override void SetDefaultValues(Customer entity)
    {
        base.SetDefaultValues(entity);
        entity.OpenDate = DateTime.Now;
        entity.Credit = 0;
        entity.Debit = 0;
        entity.Bal = 0;
        entity.OmlaRate = 1;

        if (Cities.Any())
        {
            entity.CityId = Cities.First().CityId;
        }
    }
}
