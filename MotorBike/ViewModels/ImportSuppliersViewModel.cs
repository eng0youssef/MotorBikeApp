using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ImportSuppliersViewModel : LookupViewModelBase<ImportSupplier>
{
    private readonly IRepository<Omla> _omlaRepository;

    [ObservableProperty]
    private ObservableCollection<Omla> _omlas = [];

    public ImportSuppliersViewModel(
        IRepository<ImportSupplier> repository,
        IRepository<Omla> omlaRepository) : base(repository)
    {
        _omlaRepository = omlaRepository;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var omlaData = await _omlaRepository.GetAllAsync();
            Omlas = new ObservableCollection<Omla>(omlaData);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(ImportSupplier entity) => entity.SuppId;
    protected override bool IsNewRecord(ImportSupplier entity) => entity.SuppId == 0;
    protected override void SetEntityId(ImportSupplier entity, int id) => entity.SuppId = id;

    protected override void SetDefaultValues(ImportSupplier entity)
    {
        base.SetDefaultValues(entity);
        entity.OpenDate = DateTime.Now;
        entity.Credit = 0;
        entity.Debit = 0;
        entity.Bal = 0;
        entity.Active = true;
        entity.Country = "";
        if (Omlas.Any())
            entity.OmlaId = Omlas.First().OmlaId;
    }
}
