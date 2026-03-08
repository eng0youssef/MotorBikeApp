using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class SuppliersViewModel : LookupViewModelBase<Supplier>
{
    private readonly IRepository<City> _cityRepository;

    [ObservableProperty]
    private ObservableCollection<City> _cities = [];

    public SuppliersViewModel(
        IRepository<Supplier> repository,
        IRepository<City> cityRepository) : base(repository)
    {
        _cityRepository = cityRepository;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var citiesData = await _cityRepository.GetAllAsync();
            Cities = new ObservableCollection<City>(citiesData.Where(c => c.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(Supplier entity) => entity.SuppId;
    protected override bool IsNewRecord(Supplier entity) => entity.SuppId == 0;
    protected override void SetEntityId(Supplier entity, int id) => entity.SuppId = id;

    protected override void SetDefaultValues(Supplier entity)
    {
        base.SetDefaultValues(entity);
        entity.OpenDate = DateTime.Now;
        entity.Credit = 0;
        entity.Debit = 0;
        entity.Bal = 0;

        if (Cities.Any())
        {
            entity.CityId = Cities.First().CityId;
        }
    }
}
