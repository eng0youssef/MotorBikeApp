using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class InspectionsViewModel : LookupViewModelBase<Inspection>
{
    private readonly IRepository<CarModel> _modelRepository;
    private readonly IRepository<Color> _colorRepository;
    private readonly IRepository<Cash> _cashRepository;

    [ObservableProperty]
    private ObservableCollection<CarModel> _models = [];

    [ObservableProperty]
    private ObservableCollection<Color> _colors = [];

    [ObservableProperty]
    private ObservableCollection<Cash> _cashes = [];

    [ObservableProperty]
    private ObservableCollection<Inspection> _filteredItems = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    public InspectionsViewModel(
        IRepository<Inspection> repository,
        IRepository<CarModel> modelRepository,
        IRepository<Color> colorRepository,
        IRepository<Cash> cashRepository) : base(repository)
    {
        _modelRepository = modelRepository;
        _colorRepository = colorRepository;
        _cashRepository = cashRepository;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try 
        {
            var models = await _modelRepository.GetAllAsync();
            Models = new ObservableCollection<CarModel>(models);

            var colors = await _colorRepository.GetAllAsync();
            Colors = new ObservableCollection<Color>(colors);

            var cashes = await _cashRepository.GetAllAsync();
            Cashes = new ObservableCollection<Cash>(cashes);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterItems();
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Items))
        {
            FilterItems();
        }
    }

    private void FilterItems()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredItems = new ObservableCollection<Inspection>(Items);
        }
        else
        {
            var lowerSearch = SearchText.ToLower();
            var filtered = Items.Where(i => 
                (i.ChassisNo != null && i.ChassisNo.ToLower().Contains(lowerSearch)) ||
                (i.MotorNo != null && i.MotorNo.ToLower().Contains(lowerSearch)) ||
                (i.PlateNo != null && i.PlateNo.ToLower().Contains(lowerSearch)) ||
                i.InspId.ToString().Contains(lowerSearch)
            );
            FilteredItems = new ObservableCollection<Inspection>(filtered);
        }
    }

    protected override object GetEntityId(Inspection entity) => entity.InspId;
    protected override bool IsNewRecord(Inspection entity) => entity.InspId == 0;
    protected override void SetEntityId(Inspection entity, int id) => entity.InspId = id;
    
    protected override void SetDefaultValues(Inspection entity)
    {
        base.SetDefaultValues(entity);
        entity.InspDate = DateTime.Now;
        if (Models.Any()) entity.ModelId = Models.First().ModelId;
        if (Colors.Any()) entity.ColorId = Colors.First().ColorId;
        if (Cashes.Any()) entity.CashId = Cashes.First().CashId;
    }
}
