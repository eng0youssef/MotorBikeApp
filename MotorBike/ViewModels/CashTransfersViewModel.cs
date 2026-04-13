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
    // ── Replace PrintReceiptAsync in CashTransfersViewModel with this version ────
    // Changes: adds CurrencyName, ExchangeRate, AmountInLocalCurrency, AmountInWords
    // Removes: فرع field (never existed in model — just don't show it)

    [RelayCommand]
    private async Task PrintReceiptAsync()
    {
        if (FormItem == null || FormItem.PayId <= 0)
        {
            System.Windows.MessageBox.Show(
                "يجب حفظ إيصال التحويل أولاً لطباعته.",
                "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

            // ── Currency info from DB (Omla table) ───────────────────────────────
            // If your CashTransfer has OmlaId / Rate fields, use them directly.
            // Otherwise fall back to EGP defaults.
            string currencyName = "جنية مصري";
            double exchangeRate = 1.0;

            // Uncomment and adjust if CashTransfer carries OmlaId / Rate:
            // if (FormItem.OmlaId > 0)
            // {
            //     var omla = await db.QueryFirstOrDefaultAsync<dynamic>(
            //         "SELECT OmlaName, Rate FROM Omla WHERE OmlaId = @Id",
            //         new { Id = FormItem.OmlaId });
            //     if (omla != null) { currencyName = omla.OmlaName; exchangeRate = omla.Rate; }
            // }

            double amountInLocal = Math.Round(FormItem.PayMoney * exchangeRate, 2);
            string amountWords = MotorBike.Services.CashTransferReceiptDocument
                                       .ToArabicWords(amountInLocal);

            // ── Supplier balances BEFORE this transfer ────────────────────────────
            double fromCashOld = await _compositeRepo.GetCashOldBalanceAsync(
                                     FormItem.CashId, FormItem.PayDate);
            double toCashOld = await _compositeRepo.GetCashOldBalanceAsync(
                                     FormItem.CashTo, FormItem.PayDate);

            var model = new MotorBike.Services.CashTransferReceiptModel
            {
                ReceiptNo = FormItem.PayId.ToString(),
                IssueDate = FormItem.PayDate.ToString("dd-MM-yyyy hh:mm:ss tt"),

                // ── NEW currency fields ───────────────────────────────────────────
                CurrencyName = currencyName,
                ExchangeRate = exchangeRate,
                AmountInLocalCurrency = amountInLocal,
                AmountInWords = amountWords,
                // ─────────────────────────────────────────────────────────────────

                FromCashName = CashList.FirstOrDefault(c => c.CashId == FormItem.CashId)?.CashName ?? "",
                ToCashName = CashList.FirstOrDefault(c => c.CashId == FormItem.CashTo)?.CashName ?? "",
                Amount = FormItem.PayMoney,
                Notes = FormItem.Notes ?? "",

                FromCashPreviousBalance = fromCashOld,
                FromCashBalanceAfter = fromCashOld - FormItem.PayMoney,
                ToCashPreviousBalance = toCashOld,
                ToCashBalanceAfter = toCashOld + FormItem.PayMoney
            };

            var document = new MotorBike.Services.CashTransferReceiptDocument(model, company);

            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "إيصال تحويل رقم " + FormItem.PayId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "حدث خطأ أثناء الطباعة: " + ex.Message,
                "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
