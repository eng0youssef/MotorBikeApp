using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CarsViewModel : LookupViewModelBase<Car>
{
    private readonly IRepository<Car> _carRepo;
    private readonly IRepository<CarModel> _carModelRepository;
    private readonly IRepository<Color> _colorRepository;

    private List<Car> _allCars = [];

    [ObservableProperty] private ObservableCollection<CarModel> _carModels = [];
    [ObservableProperty] private ObservableCollection<Color> _colors = [];

    // Filtered list bound to the DataGrid
    [ObservableProperty] private ObservableCollection<Car> _filteredCars = [];

    // Search text — auto-filters on change
    [ObservableProperty] private string _searchText = string.Empty;

    // Filter by Active status: 0=All, 1=Active, 2=Inactive
    [ObservableProperty] private int _activeFilter = 0;

    public CarsViewModel(
        IRepository<Car> repository,
        IRepository<CarModel> carModelRepository,
        IRepository<Color> colorRepository) : base(repository)
    {
        _carRepo = repository;
        _carModelRepository = carModelRepository;
        _colorRepository = colorRepository;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var models = await _carModelRepository.GetAllAsync();
            CarModels = new ObservableCollection<CarModel>(models.Where(m => m.Active));

            var colors = await _colorRepository.GetAllAsync();
            Colors = new ObservableCollection<Color>(colors.Where(c => c.Active));

            var cars = await _carRepo.GetAllAsync();
            _allCars = cars.ToList();
            FilterCars();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات: {ex.Message}";
        }
    }

    // Re-filter whenever the search text or filter changes
    partial void OnSearchTextChanged(string value) => FilterCars();
    partial void OnActiveFilterChanged(int value) => FilterCars();

    private void FilterCars()
    {
        IEnumerable<Car> query = _allCars;

        // Apply Active Filter
        if (ActiveFilter == 1) // Active only
            query = query.Where(c => c.Active);
        else if (ActiveFilter == 2) // Inactive only
            query = query.Where(c => !c.Active);

        // Apply Search Text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var lower = SearchText.Trim().ToLower();
            query = query.Where(c =>
                (c.ChassisNo?.ToLower().Contains(lower) == true) ||
                (c.MotorNo?.ToLower().Contains(lower) == true) ||
                (c.PlateNo?.ToLower().Contains(lower) == true) ||
                c.YearNo.ToString().Contains(lower) ||
                CarModels.FirstOrDefault(m => m.ModelId == c.ModelId)
                         ?.ModelName?.ToLower().Contains(lower) == true);
        }

        FilteredCars = new ObservableCollection<Car>(query);
    }

    // ── base-class overrides kept for compatibility ────────────────────────
    protected override object GetEntityId(Car entity) => entity.CarId;
    protected override bool IsNewRecord(Car entity) => entity.CarId == 0;
    protected override void SetEntityId(Car entity, int id) => entity.CarId = id;

    protected override void SetDefaultValues(Car entity)
    {
        base.SetDefaultValues(entity);
        entity.YearNo = (short)DateTime.Now.Year;
        entity.Mileage = 0;
        if (CarModels.Any()) entity.ModelId = CarModels.First().ModelId;
        if (Colors.Any()) entity.ColorId = Colors.First().ColorId;
    }
}