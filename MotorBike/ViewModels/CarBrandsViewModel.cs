using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public class CarBrandsViewModel : LookupViewModelBase<CarBrand>
{
    public CarBrandsViewModel(IRepository<CarBrand> repository) : base(repository) { }

    protected override object GetEntityId(CarBrand entity) => entity.BrandId;
    protected override bool IsNewRecord(CarBrand entity) => entity.BrandId == 0;
    protected override void SetEntityId(CarBrand entity, int id) => entity.BrandId = id;
}
