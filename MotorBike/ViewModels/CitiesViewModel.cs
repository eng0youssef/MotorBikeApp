using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public class CitiesViewModel : LookupViewModelBase<City>
{
    public CitiesViewModel(IRepository<City> repository) : base(repository) { }

    protected override object GetEntityId(City entity) => entity.CityId;
    protected override bool IsNewRecord(City entity) => entity.CityId == 0;
    protected override void SetEntityId(City entity, int id) => entity.CityId = id;
}
