using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

    [ObservableProperty]
    private string _brandNameText = string.Empty;

    protected override void OnFormItemChangedHook(CarModel value)
    {
        if (value != null && value.BrandId > 0)
        {
            var brand = Brands.FirstOrDefault(b => b.BrandId == value.BrandId);
            BrandNameText = brand?.BrandName ?? string.Empty;
        }
        else
        {
            BrandNameText = string.Empty;
        }
    }

    protected override void SetDefaultValues(CarModel entity)
    {
        base.SetDefaultValues(entity);
        BrandNameText = string.Empty;
    }

    protected override async Task BeforeSaveAsync(bool isInsert)
    {
        var selectedBrandName = BrandNameText?.Trim() ?? "بدون";
        var brandObj = Brands.FirstOrDefault(b => string.Equals(b.BrandName, selectedBrandName, StringComparison.OrdinalIgnoreCase));
        
        if (brandObj != null)
        {
            FormItem.BrandId = brandObj.BrandId;
        }
        else
        {
            int newBrandId = await _brandsRepository.GetNextIdAsync();
            var newBrand = new CarBrand 
            { 
                BrandId = newBrandId, 
                BrandName = selectedBrandName, 
                Active = true, 
                AddDate = DateTime.Now, 
                AddPc = Environment.MachineName, 
                AddUser = AppSession.CurrentUserId ?? 1 
            };
            await _brandsRepository.InsertAsync(newBrand);
            Brands.Add(newBrand);
            FormItem.BrandId = newBrandId;
        }

        await base.BeforeSaveAsync(isInsert);
    }

    [RelayCommand]
    public async Task LoadBrandsAsync()
    {
        var brands = await _brandsRepository.GetAllAsync();
        Brands = new ObservableCollection<CarBrand>(brands);
    }
}
