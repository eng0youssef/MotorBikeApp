using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public class ColorsViewModel : LookupViewModelBase<Color>
{
    public ColorsViewModel(IRepository<Color> repository) : base(repository) { }

    protected override object GetEntityId(Color entity) => entity.ColorId;
    protected override bool IsNewRecord(Color entity) => entity.ColorId == 0;
    protected override void SetEntityId(Color entity, int id) => entity.ColorId = id;
}
