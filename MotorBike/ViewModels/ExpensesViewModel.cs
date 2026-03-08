using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ExpensesViewModel : LookupViewModelBase<Expense>
{
    private readonly IRepository<ExpGroup> _groupRepo;

    [ObservableProperty]
    private ObservableCollection<ExpGroup> _groups = [];

    public ExpensesViewModel(IRepository<Expense> repository, IRepository<ExpGroup> groupRepo) : base(repository) 
    { 
        _groupRepo = groupRepo;
    }

    public async Task LoadLookupsAsync()
    {
        var groups = await _groupRepo.GetAllAsync();
        Groups = new ObservableCollection<ExpGroup>(groups.Where(g => g.Active));
    }

    protected override object GetEntityId(Expense entity) => entity.ExpId;
    protected override bool IsNewRecord(Expense entity) => entity.ExpId == 0;
    protected override void SetEntityId(Expense entity, int id) => entity.ExpId = id;
}
