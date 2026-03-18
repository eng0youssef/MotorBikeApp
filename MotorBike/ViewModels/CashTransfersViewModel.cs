using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class CashTransfersViewModel : LookupViewModelBase<CashTransfer>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<Cash> _cashRepo;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];

    // ── لحفظ القيم القديمة عند التعديل ──
    private int? _oldCashId;    // الخزينة المصدر القديمة
    private int? _oldCashToId;  // الخزينة الوجهة القديمة

    public CashTransfersViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<CashTransfer> repository,
        IRepository<Cash> cashRepo,
        CompositeKeyRepository compositeRepo) : base(repository)
    {
        _dbFactory = dbFactory;
        _cashRepo = cashRepo;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(CashTransfer entity) => entity.PayId;
    protected override bool IsNewRecord(CashTransfer entity) => entity.PayId == 0;
    protected override void SetEntityId(CashTransfer entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(CashTransfer entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;

        if (CashList.Count >= 2)
        {
            entity.CashId = CashList[0].CashId;
            entity.CashTo = CashList[1].CashId;
        }
        else if (CashList.Any())
        {
            entity.CashId = CashList[0].CashId;
            entity.CashTo = CashList[0].CashId;
        }
    }

    // ── Balance Recalculation Hooks ─────────────────────────────────

    protected override async Task BeforeSaveAsync(bool isInsert)
    {
        if (!isInsert && FormItem != null)
        {
            // جلب القيم القديمة من قاعدة البيانات قبل التعديل
            using var db = _dbFactory.CreateConnection();
            var old = await db.QueryFirstOrDefaultAsync<CashTransfer>(
                "SELECT CashID, CashTo FROM Cash_Transfer WHERE Pay_ID = @PayId",
                new { FormItem.PayId });

            if (old != null)
            {
                _oldCashId = old.CashId;
                _oldCashToId = old.CashTo;
            }
        }
        else
        {
            _oldCashId = null;
            _oldCashToId = null;
        }
    }

    protected override async Task AfterSaveAsync(bool wasInsert)
    {
        if (FormItem == null) return;

        // تجميع كل الخزائن المتأثرة (القديمة والجديدة) بدون تكرار
        var affectedCashIds = new HashSet<int>();

        // الخزينة المصدر القديمة لو اتغيرت
        if (_oldCashId.HasValue && _oldCashId.Value > 0)
            affectedCashIds.Add(_oldCashId.Value);

        // الخزينة الوجهة القديمة لو اتغيرت
        if (_oldCashToId.HasValue && _oldCashToId.Value > 0)
            affectedCashIds.Add(_oldCashToId.Value);

        // الخزينة المصدر الحالية
        if (FormItem.CashId > 0)
            affectedCashIds.Add(FormItem.CashId);

        // الخزينة الوجهة الحالية
        if (FormItem.CashTo > 0)
            affectedCashIds.Add(FormItem.CashTo);

        // إعادة حساب رصيد كل الخزائن المتأثرة
        foreach (var cashId in affectedCashIds)
            await _compositeRepo.RecalcBalanceForCashAsync(cashId);
    }

    protected override Task BeforeDeleteAsync()
    {
        // حفظ بيانات السجل المحدد قبل الحذف
        if (SelectedItem != null)
        {
            _oldCashId = SelectedItem.CashId;
            _oldCashToId = SelectedItem.CashTo;
        }
        return Task.CompletedTask;
    }

    protected override async Task AfterDeleteAsync()
    {
        // إعادة حساب رصيد الخزينة المصدر بعد الحذف
        if (_oldCashId.HasValue && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);

        // إعادة حساب رصيد الخزينة الوجهة بعد الحذف
        if (_oldCashToId.HasValue && _oldCashToId.Value > 0 && _oldCashToId.Value != _oldCashId)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashToId.Value);
    }

}
