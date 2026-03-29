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
    private readonly IDbConnectionFactory _dbFactory;

    [ObservableProperty]
    private ObservableCollection<InspectionSub> _formSubItems = [];

    [ObservableProperty]
    private InspectionSub? _selectedSubItem;

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

    [ObservableProperty]
    private bool _isSearchPanelVisible;

    public InspectionsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<Inspection> repository,
        IRepository<CarModel> modelRepository,
        IRepository<Color> colorRepository,
        IRepository<Cash> cashRepository) : base(repository)
    {
        _dbFactory = dbFactory;
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
        if (Models.Any()) entity.ModelId = Models.First().ModelId;
        if (Colors.Any()) entity.ColorId = Colors.First().ColorId;
        if (Cashes.Any()) entity.CashId = Cashes.First().CashId;

        _formSubItems.Clear();
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
        if (value != null && value.InspId > 0)
        {
            _ = LoadSubItemsAsync(value.InspId);
        }
        else
        {
            FormSubItems.Clear();
        }
    }

    public override async Task SaveAsync()
    {
        if (FormItem is null) return;

        try
        {
            bool isInsert = IsInsertMode;
            if (isInsert)
            {
                await SetNewIdAsync(FormItem);
            }

            SetAuditFields(FormItem, isInsert);

            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                if (isInsert)
                {
                    await db.ExecuteAsync(@"
                        INSERT INTO Inspection (Insp_ID, InspDate, Seller, Buyer, ModelId, YearNo, ChassisNo, MotorNo, PlateNo, Mileage, ColorId, Notes, Total, CashId, AddUser, AddDate, AddPc)
                        VALUES (@InspId, @InspDate, @Seller, @Buyer, @ModelId, @YearNo, @ChassisNo, @MotorNo, @PlateNo, @Mileage, @ColorId, @Notes, @Total, @CashId, @AddUser, @AddDate, @AddPc)",
                        FormItem, tx);
                }
                else
                {
                    await db.ExecuteAsync(@"
                        UPDATE Inspection SET 
                        InspDate=@InspDate, Seller=@Seller, Buyer=@Buyer, ModelId=@ModelId, YearNo=@YearNo, 
                        ChassisNo=@ChassisNo, MotorNo=@MotorNo, PlateNo=@PlateNo, Mileage=@Mileage, 
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

            // Reload data
            await LoadDataAsync();
            
            // Re-select the saved item
            var savedItem = Items.FirstOrDefault(i => i.InspId == FormItem.InspId);
            if (savedItem != null)
            {
                SelectedItem = savedItem;
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

        try
        {
            using var db = _dbFactory.CreateConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                await db.ExecuteAsync("DELETE FROM Inspection_Sub WHERE InspId = @InspId", 
                    new { InspId = SelectedItem.InspId }, tx);
                await db.ExecuteAsync("DELETE FROM Inspection WHERE Insp_ID = @InspId", 
                    new { InspId = SelectedItem.InspId }, tx);

                tx.Commit();
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
}
