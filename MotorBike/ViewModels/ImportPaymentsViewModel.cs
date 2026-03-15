using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ImportPaymentsViewModel : LookupViewModelBase<ImportPayment>
{
    private readonly IRepository<ImportSupplier> _supplierRepo;
    private readonly IRepository<Cash> _cashRepo;
    private readonly IRepository<Omla> _omlaRepo;
    private readonly IRepository<ImportInvoice> _invoiceRepo;

    [ObservableProperty] private ObservableCollection<ImportSupplier> _suppliers = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];
    [ObservableProperty] private ObservableCollection<Omla> _omlas = [];
    [ObservableProperty] private ObservableCollection<ImportInvoice> _invoices = [];

    public ImportPaymentsViewModel(
        IRepository<ImportPayment> repository,
        IRepository<ImportSupplier> supplierRepo,
        IRepository<Cash> cashRepo,
        IRepository<Omla> omlaRepo,
        IRepository<ImportInvoice> invoiceRepo) : base(repository)
    {
        _supplierRepo = supplierRepo;
        _cashRepo = cashRepo;
        _omlaRepo = omlaRepo;
        _invoiceRepo = invoiceRepo;

        FormItem.PayDate = DateTime.Now;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var supp = await _supplierRepo.GetAllAsync(); Suppliers = new(supp.Where(x => x.Active));
            var cash = await _cashRepo.GetAllAsync(); CashList = new(cash.Where(x => x.Active));
            var omlas = await _omlaRepo.GetAllAsync(); Omlas = new(omlas.Where(x => x.Active));
            var invs = await _invoiceRepo.GetAllAsync(); Invoices = new(invs);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(ImportPayment entity) => entity.PayId;
    protected override bool IsNewRecord(ImportPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(ImportPayment entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(ImportPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;
        entity.OmlaRate = 1;

        if (Suppliers.Any()) entity.SuppId = Suppliers.First().SuppId;
        if (CashList.Any()) entity.CashId = CashList.First().CashId;
        if (Omlas.Any()) entity.OmlaId = Omlas.First().OmlaId;
    }

}
