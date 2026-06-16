using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;
using Dapper;
using System.Data;

namespace MotorBike.ViewModels;

public partial class InspectionsViewModel : LookupViewModelBase<Inspection>
{
    private readonly IRepository<CarModel> _modelRepository;
    private readonly IRepository<Color> _colorRepository;
    private readonly IRepository<Cash> _cashRepository;
    private readonly IRepository<CarBrand> _carBrandRepository;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty]
    private ObservableCollection<InspectionSub> _formSubItems = [];

    [ObservableProperty]
    private InspectionSub? _selectedSubItem;

    [ObservableProperty]
    private ObservableCollection<CarModel> _models = [];

    [ObservableProperty]
    private ObservableCollection<Color> _colors = [];

    [ObservableProperty]
    private ObservableCollection<CarBrand> _brands = [];

    [ObservableProperty] private string _carModelName = string.Empty;
    [ObservableProperty] private string _carBrandName = string.Empty;
    [ObservableProperty] private string _carColorName = string.Empty;

    partial void OnCarModelNameChanged(string value)
    {
        var model = Models.FirstOrDefault(m => string.Equals(m.ModelName, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (model != null)
        {
            var brand = Brands.FirstOrDefault(b => b.BrandId == model.BrandId);
            if (brand != null) CarBrandName = brand.BrandName;
        }
    }

    [ObservableProperty]
    private ObservableCollection<CarModel> _filteredCarModels = [];

    partial void OnCarBrandNameChanged(string value)
    {
        UpdateFilteredModels();
    }

    private void UpdateFilteredModels()
    {
        if (string.IsNullOrWhiteSpace(CarBrandName))
        {
            FilteredCarModels = new ObservableCollection<CarModel>(Models);
            return;
        }

        var brand = Brands.FirstOrDefault(b => string.Equals(b.BrandName, CarBrandName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (brand != null)
        {
            FilteredCarModels = new ObservableCollection<CarModel>(Models.Where(m => m.BrandId == brand.BrandId));
        }
        else
        {
            FilteredCarModels = new ObservableCollection<CarModel>(Models);
        }
    }

    [ObservableProperty]
    private ObservableCollection<Cash> _cashes = [];

    [ObservableProperty]
    private ObservableCollection<Inspection> _filteredItems = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchPanelVisible;

    public InspectionsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Inspection> repository,
        IRepository<CarBrand> carBrandRepository,
        IRepository<CarModel> modelRepository,
        IRepository<Color> colorRepository,
        IRepository<Cash> cashRepository,
        CompositeKeyRepository compositeRepo) : base(repository)
    {
        _dbFactory = dbFactory;
        _carBrandRepository = carBrandRepository;
        _modelRepository = modelRepository;
        _colorRepository = colorRepository;
        _cashRepository = cashRepository;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try 
        {
            var brands = await _carBrandRepository.GetAllAsync();
            Brands = new ObservableCollection<CarBrand>(brands.Where(b => b.Active));

            var models = await _modelRepository.GetAllAsync();
            Models = new ObservableCollection<CarModel>(models);
            FilteredCarModels = new ObservableCollection<CarModel>(models);

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

    [RelayCommand]
    private void ShowSearchPanel()
    {
        IsSearchPanelVisible = true;
        SearchText = string.Empty;
        FilterItems();
    }

    [RelayCommand]
    private void HideSearchPanel()
    {
        IsSearchPanelVisible = false;
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Items))
        {
            FilterItems();
        }
        else if (e.PropertyName == nameof(SelectedItem))
        {
            if (SelectedItem != null)
            {
                IsSearchPanelVisible = false;
            }
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
        _formSubItems.Clear();

        CarBrandName = string.Empty;
        CarModelName = string.Empty;
        CarColorName = string.Empty;
    }

    [RelayCommand]
    public void AddSubItem()
    {
        FormSubItems.Add(new InspectionSub { Status = true });
    }

    [RelayCommand]
    public void RemoveSubItem(InspectionSub subItem)
    {
        if (subItem != null)
        {
            FormSubItems.Remove(subItem);
        }
    }

    private async Task LoadSubItemsAsync(int inspId)
    {
        try
        {
            using var db = _dbFactory.CreateConnection();
            var subItems = await db.QueryAsync<InspectionSub>(
                "SELECT * FROM Inspection_Sub WHERE InspId = @InspId", 
                new { InspId = inspId });
            FormSubItems = new ObservableCollection<InspectionSub>(subItems);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل التفاصيل: {ex.Message}";
        }
    }

    protected override void OnFormItemChangedHook(Inspection value)
    {
        base.OnFormItemChangedHook(value);
        if (value != null)
        {
            var model = Models.FirstOrDefault(m => m.ModelId == value.ModelId);
            CarModelName = model?.ModelName ?? string.Empty;

            var color = Colors.FirstOrDefault(c => c.ColorId == value.ColorId);
            CarColorName = color?.ColorName ?? string.Empty;

            if (model != null)
            {
                var brand = Brands.FirstOrDefault(b => b.BrandId == model.BrandId);
                CarBrandName = brand?.BrandName ?? string.Empty;
            }
            else
            {
                CarBrandName = string.Empty;
            }

            if (value.InspId > 0)
            {
                _ = LoadSubItemsAsync(value.InspId);
            }
            else
            {
                FormSubItems.Clear();
            }
        }
        else
        {
            FormSubItems.Clear();
        }
    }

    public override async Task SaveAsync()
    {
        if (FormItem is null) return;

        var requiredAbility = IsInsertMode ? AppAbility.Add : AppAbility.Edit;
        if (!AppSession.HasPermission(ScreenId.Inspections, requiredAbility))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية لإجراء هذه العملية.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        try
        {
            bool isInsert = IsInsertMode;
            if (isInsert)
            {
                await SetNewIdAsync(FormItem);
            }

            int? oldCashId = null;
            if (!isInsert)
            {
                using var dbPre = _dbFactory.CreateConnection();
                oldCashId = await dbPre.QueryFirstOrDefaultAsync<int?>(
                    "SELECT CashId FROM Inspection WHERE Insp_ID = @InspId",
                    new { InspId = FormItem.InspId });
            }

            SetAuditFields(FormItem, isInsert);

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var selectedColorName = CarColorName?.Trim() ?? "بدون";
                var colorObj = Colors.FirstOrDefault(c => string.Equals(c.ColorName, selectedColorName, StringComparison.OrdinalIgnoreCase));
                if (colorObj != null)
                {
                    FormItem.ColorId = colorObj.ColorId;
                }
                else
                {
                    int newColorId = await _colorRepository.GetNextIdAsync();
                    var newColor = new Color { ColorId = newColorId, ColorName = selectedColorName, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                    await db.ExecuteAsync("INSERT INTO Colors (Color_ID, ColorName, Active, AddDate, AddPC, AddUser) VALUES (@ColorId, @ColorName, @Active, @AddDate, @AddPc, @AddUser)", newColor, tx);
                    Colors.Add(newColor);
                    FormItem.ColorId = newColorId;
                }

                var selectedBrandName = CarBrandName?.Trim() ?? "بدون";
                var brandObj = Brands.FirstOrDefault(b => string.Equals(b.BrandName, selectedBrandName, StringComparison.OrdinalIgnoreCase));
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
                    Brands.Add(newBrand);
                }

                var selectedModelName = CarModelName?.Trim() ?? "بدون";
                var modelObj = Models.FirstOrDefault(m => string.Equals(m.ModelName, selectedModelName, StringComparison.OrdinalIgnoreCase) && m.BrandId == currentBrandId);
                if (modelObj != null)
                {
                    FormItem.ModelId = modelObj.ModelId;
                }
                else
                {
                    int newModelId = await _modelRepository.GetNextIdAsync();
                    var newModel = new CarModel { ModelId = newModelId, ModelName = selectedModelName, BrandId = currentBrandId, Active = true, AddDate = DateTime.Now, AddPc = Environment.MachineName, AddUser = AppSession.CurrentUserId ?? 1 };
                    await db.ExecuteAsync("INSERT INTO CarModels (Model_ID, ModelName, BrandID, Active, AddDate, AddPC, AddUser) VALUES (@ModelId, @ModelName, @BrandId, @Active, @AddDate, @AddPc, @AddUser)", newModel, tx);
                    Models.Add(newModel);
                    FormItem.ModelId = newModelId;
                }

                if (isInsert)
                {
                    await db.ExecuteAsync(@"
                        INSERT INTO Inspection (Insp_ID, InspDate, Seller, Buyer, ModelId, YearNo, ChassisNo, MotorNo, PlateNo, Mileage, CC, ColorId, Notes, Total, CashId, AddUser, AddDate, AddPc)
                        VALUES (@InspId, @InspDate, @Seller, @Buyer, @ModelId, @YearNo, @ChassisNo, @MotorNo, @PlateNo, @Mileage, @CC, @ColorId, @Notes, @Total, @CashId, @AddUser, @AddDate, @AddPc)",
                        FormItem, tx);
                }
                else
                {
                    await db.ExecuteAsync(@"
                        UPDATE Inspection SET 
                        InspDate=@InspDate, Seller=@Seller, Buyer=@Buyer, ModelId=@ModelId, YearNo=@YearNo, 
                        ChassisNo=@ChassisNo, MotorNo=@MotorNo, PlateNo=@PlateNo, Mileage=@Mileage, CC=@CC,
                        ColorId=@ColorId, Notes=@Notes, Total=@Total, CashId=@CashId, 
                        EditUser=@EditUser, EditDate=@EditDate, EditPc=@EditPc
                        WHERE Insp_ID = @InspId",
                        FormItem, tx);

                    // Delete existing sub-items to replace them
                    await db.ExecuteAsync("DELETE FROM Inspection_Sub WHERE InspId = @InspId", 
                        new { InspId = FormItem.InspId }, tx);
                }

                // Insert sub-items
                int nextSubId = await db.QuerySingleAsync<int>("SELECT ISNULL(MAX(ID), 0) FROM Inspection_Sub", transaction: tx);
                foreach (var item in FormSubItems)
                {
                    item.Id = ++nextSubId;
                    item.InspId = FormItem.InspId;
                    await db.ExecuteAsync(@"
                        INSERT INTO Inspection_Sub (ID, InspId, ItemName, Status, Note)
                        VALUES (@Id, @InspId, @ItemName, @Status, @Note)",
                        item, tx);
                }

                tx.Commit();
                StatusMessage = "تم حفظ الكشف بنجاح ✓";
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            // Recalculate cash balances
            if (oldCashId.HasValue && oldCashId.Value != FormItem.CashId)
            {
                await _compositeRepo.RecalcBalanceForCashAsync(oldCashId.Value);
            }
            await _compositeRepo.RecalcBalanceForCashAsync(FormItem.CashId);

            // Reload data
            var data = await _repository.GetAllAsync();
            Items = new ObservableCollection<Inspection>(data);
            
            // Re-select the saved item
            var savedItem = Items.FirstOrDefault(i => i.InspId == FormItem.InspId);
            if (savedItem != null)
            {
                SelectedItem = savedItem;
                IsEditing = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحفظ: {ex.Message}";
        }
    }

    public override async Task DeleteAsync()
    {
        if (SelectedItem is null) return;

        if (!AppSession.HasPermission(ScreenId.Inspections, AppAbility.Delete))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية للحذف.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        var res = System.Windows.MessageBox.Show("هل أنت متأكد من الحذف نهائياً؟", "تأكيد الحذف", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (res != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var affectedCashId = SelectedItem.CashId;
                await db.ExecuteAsync("DELETE FROM Inspection_Sub WHERE InspId = @InspId", 
                    new { InspId = SelectedItem.InspId }, tx);
                await db.ExecuteAsync("DELETE FROM Inspection WHERE Insp_ID = @InspId", 
                    new { InspId = SelectedItem.InspId }, tx);

                tx.Commit();
                await _compositeRepo.RecalcBalanceForCashAsync(affectedCashId);
                StatusMessage = "تم حذف الكشف بنجاح ✓";
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            IsEditing = false;
            FormItem = new Inspection();
            FormSubItems.Clear();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحذف: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PrintInspectionAsync()
    {
        if (FormItem == null || FormItem.InspId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ الكشف أولاً لطباعته.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

            var model = new MotorBike.Services.InspectionPrintModel
            {
                InspNo = FormItem.InspId.ToString(),
                IssueDate = FormItem.InspDate.ToString("yyyy-MM-dd"),
                Seller = FormItem.Seller ?? "",
                Buyer = FormItem.Buyer ?? "",
                Notes = FormItem.Notes ?? "",
                CarModel = Models.FirstOrDefault(m => m.ModelId == FormItem.ModelId)?.ModelName ?? "",
                CarBrand = CarBrandName,
                ChassisNo = FormItem.ChassisNo ?? "",
                MotorNo = FormItem.MotorNo ?? "",
                PlateNo = FormItem.PlateNo ?? "",
                ColorName = Colors.FirstOrDefault(c => c.ColorId == FormItem.ColorId)?.ColorName ?? "",
                YearNo = FormItem.YearNo,
                Mileage = FormItem.Mileage,
                CC = FormItem.CC ?? 0,
                CashName = Cashes.FirstOrDefault(c => c.CashId == FormItem.CashId)?.CashName ?? "",
                Total = FormItem.Total
            };

            foreach (var item in FormSubItems)
            {
                model.Items.Add(new MotorBike.Services.InspectionSubModel
                {
                    ItemName = item.ItemName ?? "",
                    Status = item.Status,
                    Note = item.Note ?? ""
                });
            }

            var document = new MotorBike.Services.InspectionDocument(model, company);
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "كشف فني رقم " + FormItem.InspId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
