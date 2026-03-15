using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ImportExpensesViewModel : LookupViewModelBase<ImportExpense>
{
    public ImportExpensesViewModel(IRepository<ImportExpense> repository) : base(repository)
    {
    }

    protected override object GetEntityId(ImportExpense entity) => entity.ExpId;
    protected override bool IsNewRecord(ImportExpense entity) => entity.ExpId == 0;
    protected override void SetEntityId(ImportExpense entity, int id) => entity.ExpId = id;

    protected override void SetDefaultValues(ImportExpense entity)
    {
        base.SetDefaultValues(entity);
        entity.Active = true;
    }
}
