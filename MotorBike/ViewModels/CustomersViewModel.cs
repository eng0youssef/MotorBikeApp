using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CustomersViewModel : LookupViewModelBase<Customer>
{
    private readonly IRepository<City> _cityRepository;
    private readonly IRepository<Car> _carRepository;
    private readonly IRepository<CarModel> _carModelRepository;
    private readonly IRepository<Color> _colorRepository;
    private readonly IRepository<CarBrand> _carBrandRepository;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty]
    private ObservableCollection<City> _cities = [];

    // ── Motorcycle section ────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<CarBrand> _carBrands = [];
    [ObservableProperty] private ObservableCollection<CarModel> _carModels = [];
    [ObservableProperty] private ObservableCollection<Color> _colors = [];
    [ObservableProperty] private ObservableCollection<Car> _customerCars = [];

    // Inline car fields for adding a new motorcycle
    [ObservableProperty] private string _newCarBrandName = string.Empty;
    [ObservableProperty] private string _newCarModelName = string.Empty;
    [ObservableProperty] private string _newCarColorName = string.Empty;
    [ObservableProperty] private bool _isNewCarBrandEnabled = true;
    [ObservableProperty] private short _newCarYearNo = (short)DateTime.Now.Year;
    [ObservableProperty] private int _newCarMileage;

    partial void OnNewCarModelNameChanged(string value)
    {
        var model = CarModels.FirstOrDefault(m => string.Equals(m.ModelName, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (model != null)
        {
            var brand = CarBrands.FirstOrDefault(b => b.BrandId == model.BrandId);
            if (brand != null)
            {
                NewCarBrandName = brand.BrandName;
            }
            IsNewCarBrandEnabled = false;
        }
        else
        {
            IsNewCarBrandEnabled = true;
        }
    }
    [ObservableProperty] private string? _newCarChassisNo;
    [ObservableProperty] private string? _newCarMotorNo;
    [ObservableProperty] private string? _newCarPlateNo;
    [ObservableProperty] private string? _newCarNotes;
    
    [ObservableProperty] private int _editingCarId = 0;
    [ObservableProperty] private bool _isCarsPopupOpen;

    public CustomersViewModel(
        IRepository<Customer> repository,
        IRepository<City> cityRepository,
        IRepository<Car> carRepository,
        IRepository<CarModel> carModelRepository,
        IRepository<Color> colorRepository,
        IRepository<CarBrand> carBrandRepository,
        IDbConnectionFactory dbFactory,
        CompositeKeyRepository compositeRepo) : base(repository)
    {
        _cityRepository = cityRepository;
        _carRepository = carRepository;
        _carModelRepository = carModelRepository;
        _colorRepository = colorRepository;
        _carBrandRepository = carBrandRepository;
        _dbFactory = dbFactory;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var citiesData = await _cityRepository.GetAllAsync();
            Cities = new ObservableCollection<City>(citiesData.Where(c => c.Active));

            var models = await _carModelRepository.GetAllAsync();
            CarModels = new ObservableCollection<CarModel>(models.Where(m => m.Active));

            var colors = await _colorRepository.GetAllAsync();
            Colors = new ObservableCollection<Color>(colors.Where(c => c.Active));

            var brands = await _carBrandRepository.GetAllAsync();
            CarBrands = new ObservableCollection<CarBrand>(brands.Where(b => b.Active));

            // Set defaults for car
            NewCarModelName = string.Empty;
            NewCarColorName = string.Empty;
            NewCarBrandName = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(Customer entity) => entity.CusId;
    protected override bool IsNewRecord(Customer entity) => entity.CusId == 0;
    protected override void SetEntityId(Customer entity, int id) => entity.CusId = id;

    protected override void SetDefaultValues(Customer entity)
    {
        base.SetDefaultValues(entity);
        entity.OpenDate = DateTime.Now;
        entity.Credit = 0;
        entity.Debit = 0;
        entity.Bal = 0;

        if (Cities.Any())
        {
            entity.CityId = Cities.First().CityId;
        }
    }

    // ── Load/Reload customer cars when form item changes ───────────
    protected override async void OnFormItemChangedHook(Customer value)
    {
        if (value != null && value.CusId > 0)
        {
            await LoadCustomerCarsAsync(value.CusId);
        }
        else
        {
            CustomerCars.Clear();
        }
    }

    private async Task LoadCustomerCarsAsync(int cusId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var cars = await db.QueryAsync<Car>(
                @"SELECT * FROM Cars WHERE OwnerID = @CusId",
                new { CusId = cusId });
            CustomerCars = new ObservableCollection<Car>(cars);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل موتوسيكلات العميل: {ex.Message}";
        }
    }

    // ── Add a new motorcycle to the current customer ──────────────────
    [RelayCommand]
    public async Task AddCarToCustomerAsync()
    {
        if (FormItem == null || IsNewRecord(FormItem))
        {
            StatusMessage = "⚠️ يجب حفظ بيانات العميل أولاً قبل إضافة موتوسيكل.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewCarChassisNo))
        {
            StatusMessage = "⚠️ يجب إدخال رقم الشاسيه.";
            return;
        }

        try
        {
            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Color check
                int currentColorId;
                var selectedColorName = NewCarColorName?.Trim() ?? "بدون";
                var colorObj = Colors.FirstOrDefault(c => string.Equals(c.ColorName, selectedColorName, StringComparison.OrdinalIgnoreCase));
                if (colorObj != null)
                {
                    currentColorId = colorObj.ColorId;
                }
                else
                {
                    currentColorId = await _colorRepository.GetNextIdAsync();
                    var newColor = new Color { ColorId = currentColorId, ColorName = selectedColorName, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                    await db.ExecuteAsync("INSERT INTO Colors (Color_ID, ColorName, Active, AddDate, AddPC, AddUser) VALUES (@ColorId, @ColorName, @Active, @AddDate, @AddPc, @AddUser)", newColor, tx);
                    Colors.Add(newColor);
                }

                // Brand and Model check
                int currentModelId;
                var selectedModelName = NewCarModelName?.Trim() ?? "بدون";
                var modelObj = CarModels.FirstOrDefault(m => string.Equals(m.ModelName, selectedModelName, StringComparison.OrdinalIgnoreCase));
                if (modelObj != null)
                {
                    currentModelId = modelObj.ModelId;
                }
                else
                {
                    var selectedBrandName = NewCarBrandName?.Trim() ?? "بدون";
                    var brandObj = CarBrands.FirstOrDefault(b => string.Equals(b.BrandName, selectedBrandName, StringComparison.OrdinalIgnoreCase));
                    int currentBrandId;
                    if (brandObj != null)
                    {
                        currentBrandId = brandObj.BrandId;
                    }
                    else
                    {
                        currentBrandId = await _carBrandRepository.GetNextIdAsync();
                        var newBrand = new CarBrand { BrandId = currentBrandId, BrandName = selectedBrandName, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                        await db.ExecuteAsync("INSERT INTO CarBrands (Brand_ID, BrandName, Active, AddDate, AddPC, AddUser) VALUES (@BrandId, @BrandName, @Active, @AddDate, @AddPc, @AddUser)", newBrand, tx);
                        CarBrands.Add(newBrand);
                    }

                    currentModelId = await _carModelRepository.GetNextIdAsync();
                    var newModel = new CarModel { ModelId = currentModelId, ModelName = selectedModelName, BrandId = currentBrandId, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                    await db.ExecuteAsync("INSERT INTO CarModels (Model_ID, ModelName, BrandID, Active, AddDate, AddPC, AddUser) VALUES (@ModelId, @ModelName, @BrandId, @Active, @AddDate, @AddPc, @AddUser)", newModel, tx);
                    CarModels.Add(newModel);
                }

                if (EditingCarId > 0)
                {
                    // Update existing
                    await db.ExecuteAsync(@"
                        UPDATE Cars
                        SET ModelID = @ModelId,
                            YearNo = @YearNo,
                            ChassisNo = @ChassisNo,
                            MotorNo = @MotorNo,
                            PlateNo = @PlateNo,
                            Mileage = @Mileage,
                            ColorID = @ColorId,
                            Notes = @Notes,
                            EditDate = @EditDate,
                            EditPC = @EditPc,
                            EditUser = @EditUser
                        WHERE Car_ID = @CarId",
                        new
                        {
                            CarId = EditingCarId,
                            ModelId = currentModelId,
                            YearNo = NewCarYearNo,
                            ChassisNo = NewCarChassisNo,
                            MotorNo = NewCarMotorNo ?? "",
                            PlateNo = NewCarPlateNo ?? "",
                            Mileage = NewCarMileage,
                            ColorId = currentColorId,
                            Notes = NewCarNotes,
                            EditDate = DateTime.Now,
                            EditPc = Environment.MachineName,
                            EditUser = AppSession.CurrentUserId ?? 1
                        }, tx);
                }
                else
                {
                    // 1. Create the Car row
                    int carId = await _carRepository.GetNextIdAsync();
                    await db.ExecuteAsync(@"
                        INSERT INTO Cars
                            (Car_ID, ModelID, YearNo, ChassisNo, MotorNo, PlateNo,
                             Mileage, ColorID, IsStock, Notes, OwnerID, StatusID,
                             AddDate, AddPC, AddUser)
                        VALUES
                            (@CarId, @ModelId, @YearNo, @ChassisNo, @MotorNo, @PlateNo,
                             @Mileage, @ColorId, 0, @Notes, @OwnerId, 3,
                             @AddDate, @AddPc, @AddUser)",
                        new
                        {
                            CarId = carId,
                            ModelId = currentModelId,
                            YearNo = NewCarYearNo,
                            ChassisNo = NewCarChassisNo,
                            MotorNo = NewCarMotorNo ?? "",
                            PlateNo = NewCarPlateNo ?? "",
                            Mileage = NewCarMileage,
                            ColorId = currentColorId,
                            Notes = NewCarNotes,
                            OwnerId = FormItem.CusId,
                            AddDate = DateTime.Now,
                            AddPc = Environment.MachineName,
                            AddUser = AppSession.CurrentUserId ?? 1
                        }, tx);

                    // 2. Create the Cus_Cars link row
                    await db.ExecuteAsync(@"
                        INSERT INTO Cus_Cars
                            (CusID, CarID, Active, Notes, AddDate, AddPC, AddUser)
                        VALUES
                            (@CusId, @CarId, 1, @Notes, @AddDate, @AddPc, @AddUser)",
                        new
                        {
                            CusId = FormItem.CusId,
                            CarId = carId,
                            Notes = NewCarNotes,
                            AddDate = DateTime.Now,
                            AddPc = Environment.MachineName,
                            AddUser = AppSession.CurrentUserId ?? 1
                        }, tx);
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }

            StatusMessage = EditingCarId > 0 ? "تم تعديل بيانات الموتوسيكل بنجاح ✓" : "تم إضافة الموتوسيكل بنجاح ✓";
            CancelEditCar();
            
            // Reload the customer's cars
            await LoadCustomerCarsAsync(FormItem.CusId);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في إضافة الموتوسيكل: {ex.Message}";
        }
    }

    // ── Remove a motorcycle from the customer ─────────────────────────
    [RelayCommand]
    public async Task RemoveCarFromCustomerAsync(Car? car)
    {
        if (car == null || FormItem == null) return;

        try
        {
            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Remove link
                await db.ExecuteAsync(
                    "DELETE FROM Cus_Cars WHERE CusID = @CusId AND CarID = @CarId",
                    new { CusId = FormItem.CusId, CarId = car.CarId }, tx);

                // Clear owner on the car
                await db.ExecuteAsync(
                    "UPDATE Cars SET OwnerID = NULL, StatusID = 1 WHERE Car_ID = @CarId",
                    new { CarId = car.CarId }, tx);

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }

            await LoadCustomerCarsAsync(FormItem.CusId);
            StatusMessage = "تم إزالة الموتوسيكل بنجاح ✓";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في إزالة الموتوسيكل: {ex.Message}";
        }
    }

    // ── Edit motorcycle ──────────────────────────────────────────
    [RelayCommand]
    public void EditCarForCustomer(Car? car)
    {
        if (car == null) return;
        EditingCarId = car.CarId;
        
        var model = CarModels.FirstOrDefault(m => m.ModelId == car.ModelId);
        NewCarModelName = model?.ModelName ?? string.Empty;
        var color = Colors.FirstOrDefault(c => c.ColorId == car.ColorId);
        NewCarColorName = color?.ColorName ?? string.Empty;

        if (model != null)
        {
            var brand = CarBrands.FirstOrDefault(b => b.BrandId == model.BrandId);
            NewCarBrandName = brand?.BrandName ?? string.Empty;
            IsNewCarBrandEnabled = false;
        }
        else
        {
            NewCarBrandName = string.Empty;
            IsNewCarBrandEnabled = true;
        }
        
        NewCarYearNo = car.YearNo;
        NewCarChassisNo = car.ChassisNo;
        NewCarMotorNo = car.MotorNo;
        NewCarPlateNo = car.PlateNo;
        NewCarMileage = car.Mileage;
        NewCarNotes = car.Notes;
    }

    [RelayCommand]
    public void CancelEditCar()
    {
        EditingCarId = 0;
        NewCarChassisNo = null;
        NewCarMotorNo = null;
        NewCarPlateNo = null;
        NewCarNotes = null;
        NewCarMileage = 0;
        NewCarYearNo = (short)DateTime.Now.Year;
        NewCarModelName = string.Empty;
        NewCarColorName = string.Empty;
        NewCarBrandName = string.Empty;
        IsNewCarBrandEnabled = true;
    }

    [RelayCommand]
    public void OpenCarsPopup()
    {
        if (FormItem == null || IsNewRecord(FormItem))
        {
            StatusMessage = "⚠️ يجب حفظ بيانات العميل أولاً قبل استعراض موتوسيكلاته.";
            return;
        }
        IsCarsPopupOpen = true;
    }

    [RelayCommand]
    public void CloseCarsPopup()
    {
        IsCarsPopupOpen = false;
        CancelEditCar();
    }
}
