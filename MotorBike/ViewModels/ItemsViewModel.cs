using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ItemsViewModel : LookupViewModelBase<Item>
{
    private readonly IRepository<ItemCategory> _categoryRepo;
    private readonly IRepository<Unit> _unitRepo;

    [ObservableProperty]
    private ObservableCollection<ItemCategory> _categories = [];

    [ObservableProperty]
    private ObservableCollection<Unit> _units = [];

    public ItemsViewModel(IRepository<Item> repository, IRepository<ItemCategory> categoryRepo, IRepository<Unit> unitRepo) : base(repository) 
    { 
        _categoryRepo = categoryRepo;
        _unitRepo = unitRepo;
    }

    public async Task LoadLookupsAsync()
    {
        var cats = await _categoryRepo.GetAllAsync();
        Categories = new ObservableCollection<ItemCategory>(cats.Where(c => c.Active));

        var units = await _unitRepo.GetAllAsync();
        Units = new ObservableCollection<Unit>(units.Where(u => u.Active));
    }

    protected override object GetEntityId(Item entity) => entity.ItemId;
    protected override bool IsNewRecord(Item entity) => entity.ItemId == 0;
    protected override void SetEntityId(Item entity, int id) => entity.ItemId = id;

    protected override void SetDefaultValues(Item entity)
    {
        base.SetDefaultValues(entity);
        entity.IsStock = true;
    }
}
