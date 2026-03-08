using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CarModelsViewModel : LookupViewModelBase<CarModel>
{
    private readonly IRepository<CarBrand> _brandsRepository;

    [ObservableProperty]
    private ObservableCollection<CarBrand> _brands = [];

    public CarModelsViewModel(IRepository<CarModel> repository, IRepository<CarBrand> brandsRepository)
        : base(repository)
    {
        _brandsRepository = brandsRepository;
    }

    protected override object GetEntityId(CarModel entity) => entity.ModelId;
    protected override bool IsNewRecord(CarModel entity) => entity.ModelId == 0;
    protected override void SetEntityId(CarModel entity, int id) => entity.ModelId = id;

    [RelayCommand]
    public async Task LoadBrandsAsync()
    {
        var brands = await _brandsRepository.GetAllAsync();
        Brands = new ObservableCollection<CarBrand>(brands);
    }
}
