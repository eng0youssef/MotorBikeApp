using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CarsViewModel : LookupViewModelBase<Car>
{
    private readonly IRepository<CarModel> _carModelRepository;
    private readonly IRepository<Color> _colorRepository;

    [ObservableProperty]
    private ObservableCollection<CarModel> _carModels = [];

    [ObservableProperty]
    private ObservableCollection<Color> _colors = [];

    public CarsViewModel(
        IRepository<Car> repository,
        IRepository<CarModel> carModelRepository,
        IRepository<Color> colorRepository) : base(repository)
    {
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(Car entity) => entity.CarId;
    protected override bool IsNewRecord(Car entity) => entity.CarId == 0;
    protected override void SetEntityId(Car entity, int id) => entity.CarId = id;

    protected override void SetDefaultValues(Car entity)
    {
        base.SetDefaultValues(entity);
        entity.YearNo = (short)DateTime.Now.Year;
        entity.Mileage = 0;

        if (CarModels.Any())
            entity.ModelId = CarModels.First().ModelId;

        if (Colors.Any())
            entity.ColorId = Colors.First().ColorId;
    }
}
