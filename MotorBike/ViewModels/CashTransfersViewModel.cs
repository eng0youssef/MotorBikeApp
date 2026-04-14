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

    // ── أرصدة الخزائن ──
    [ObservableProperty] private double _fromCashBalance;
    [ObservableProperty] private double _toCashBalance;
    [ObservableProperty] private bool _isDifferentCurrency;
    [ObservableProperty] private string _fromCurrencyName = "";
    [ObservableProperty] private string _toCurrencyName = "";

    // ── FromRate و ToRate: موجودين في FormItem ──
    public double FromRate
    {
        get => FormItem?.FromRate ?? 1;
        set
        {
            if (FormItem != null && FormItem.FromRate != value)
            {
                FormItem.FromRate = value;
                OnPropertyChanged(nameof(FromRate));
                UpdateExchangeRate();
            }
        }
    }

    public double ToRate
    {
        get => FormItem?.ToRate ?? 1;
        set
        {
            if (FormItem != null && FormItem.ToRate != value)
            {
                FormItem.ToRate = value;
                OnPropertyChanged(nameof(ToRate));
                UpdateExchangeRate();
            }
        }
    }

    // ── ExchangeRate: يتم حسابه ضمنياً ──
    public double ExchangeRate
    {
        get => FormItem?.ExchangeRate ?? 1;
    }

    private void UpdateExchangeRate()
    {
        if (FormItem == null) return;
        double f = FormItem.FromRate > 0 ? FormItem.FromRate : 1;
        double t = FormItem.ToRate > 0 ? FormItem.ToRate : 1;
        FormItem.ExchangeRate = f / t;
        OnPropertyChanged(nameof(ExchangeRate));
        RecalcToAmount();
    }

    // ── ToAmount: المبلغ المحول — موجود في FormItem.PayMoneyTo ──
    public double ToAmount => FormItem?.PayMoneyTo ?? 0;

    // ── SelectedCashId: من خزينة ──
    public int SelectedCashId
    {
        get => FormItem?.CashId ?? 0;
        set
        {
            if (FormItem != null && FormItem.CashId != value)
            {
                FormItem.CashId = value;
                OnPropertyChanged(nameof(SelectedCashId));
                RefreshFromCash();
                RecalcExchangeAndToAmount();
            }
        }
    }

    // ── SelectedCashTo: إلى خزينة ──
    public int SelectedCashTo
    {
        get => FormItem?.CashTo ?? 0;
        set
        {
            if (FormItem != null && FormItem.CashTo != value)
            {
                FormItem.CashTo = value;
                OnPropertyChanged(nameof(SelectedCashTo));
                RefreshToCash();
                RecalcExchangeAndToAmount();
            }
        }
    }

    // ── لحفظ القيم القديمة عند التعديل ──
    private int? _oldCashId;    // الخزينة المصدر القديمة
    private int? _oldCashToId;  // الخزينة الوجهة القديمة
    
    private List<MotorBike.Models.Omla> _omlas = new();

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
            using var db = _dbFactory.CreateConnection();
            _omlas = (await db.QueryAsync<MotorBike.Models.Omla>("SELECT * FROM Omla")).ToList();

            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active));

            if (FormItem != null)
            {
                RefreshFromCash();
                RefreshToCash();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(CashTransfer entity) => entity.PayId;
    protected override bool IsNewRecord(CashTransfer entity) => entity.PayId == 0;
    protected override void SetEntityId(CashTransfer entity, int id) => entity.PayId = id;

    protected override void OnFormItemChangedHook(CashTransfer value)
    {
        if (value != null)
        {
            RefreshFromCash();
            RefreshToCash();
            // عند التعديل: نحدد هل عملتين مختلفتين ونعيد حساب ToAmount
            RefreshDifferentCurrencyFlag();
            OnPropertyChanged(nameof(SelectedCashId));
            OnPropertyChanged(nameof(SelectedCashTo));
            OnPropertyChanged(nameof(ExchangeRate));
            OnPropertyChanged(nameof(FromRate));
            OnPropertyChanged(nameof(ToRate));
            OnPropertyChanged(nameof(ToAmount));
            OnPropertyChanged(nameof(PayMoney));
        }
    }

    // PayMoney wrapper - live-updates ToAmount
    public double PayMoney
    {
        get => FormItem?.PayMoney ?? 0;
        set
        {
            if (FormItem != null && FormItem.PayMoney != value)
            {
                FormItem.PayMoney = value;
                OnPropertyChanged(nameof(PayMoney));
                RecalcToAmount();
            }
        }
    }

    private void RefreshFromCash()
    {
        var cash = CashList.FirstOrDefault(c => c.CashId == (FormItem?.CashId ?? 0));
        FromCashBalance = cash?.Bal ?? 0;
        
        if (cash == null || cash.OmlaId == 0 || cash.OmlaId == null)
        {
            FromCurrencyName = "ج.م";
        }
        else
        {
            var omla = _omlas.FirstOrDefault(o => o.OmlaId == cash.OmlaId);
            FromCurrencyName = omla?.OmlaName ?? $"عملة {cash.OmlaId}";
        }
    }

    private void RefreshToCash()
    {
        var cash = CashList.FirstOrDefault(c => c.CashId == (FormItem?.CashTo ?? 0));
        ToCashBalance = cash?.Bal ?? 0;
        
        if (cash == null || cash.OmlaId == 0 || cash.OmlaId == null)
        {
            ToCurrencyName = "ج.م";
        }
        else
        {
            var omla = _omlas.FirstOrDefault(o => o.OmlaId == cash.OmlaId);
            ToCurrencyName = omla?.OmlaName ?? $"عملة {cash.OmlaId}";
        }
    }

    private void RefreshDifferentCurrencyFlag()
    {
        var fromCash = CashList.FirstOrDefault(c => c.CashId == (FormItem?.CashId ?? 0));
        var toCash   = CashList.FirstOrDefault(c => c.CashId == (FormItem?.CashTo   ?? 0));
        IsDifferentCurrency = fromCash?.OmlaId != toCash?.OmlaId;
    }

    private void RecalcExchangeAndToAmount()
    {
        var fromCash = CashList.FirstOrDefault(c => c.CashId == (FormItem?.CashId ?? 0));
        var toCash   = CashList.FirstOrDefault(c => c.CashId == (FormItem?.CashTo   ?? 0));

        // نظهر حقول الصرف بس لو فيه عملة مختلفة عن المحلي أو العملتين مختلفتين
        IsDifferentCurrency = fromCash?.OmlaId != toCash?.OmlaId;

        double fRate = (double)(fromCash?.OmlaRate > 0 ? fromCash.OmlaRate : 1);
        double tRate = (double)(toCash?.OmlaRate   > 0 ? toCash.OmlaRate   : 1);

        // فقط لو السجل جديد (ليس نتيجة تعديل) نضبط السعر المقترح
        if (FormItem != null && FormItem.PayId == 0)
        {
            FormItem.FromRate = fRate;
            FormItem.ToRate = tRate;
            OnPropertyChanged(nameof(FromRate));
            OnPropertyChanged(nameof(ToRate));
            UpdateExchangeRate();
        }
        else
        {
            OnPropertyChanged(nameof(FromRate));
            OnPropertyChanged(nameof(ToRate));
            OnPropertyChanged(nameof(ExchangeRate));
            RecalcToAmount();
        }
    }

    private void RecalcToAmount()
    {
        if (FormItem == null) return;
        FormItem.PayMoneyTo = Math.Round(FormItem.PayMoney * (FormItem.ExchangeRate > 0 ? FormItem.ExchangeRate : 1), 2);
        OnPropertyChanged(nameof(ToAmount));
    }

    protected override void SetDefaultValues(CashTransfer entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;
        entity.CashId  = 0;
        entity.CashTo  = 0;
        entity.PayMoney = 0;
        entity.Notes = string.Empty;
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

        await LoadRelatedDataAsync(); // تحديث الأرصدة في الـ UI
        OnFormItemChangedHook(FormItem);
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

        await LoadRelatedDataAsync();
        OnFormItemChangedHook(FormItem);
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

            // ── Currency info ───────────────────────────────
            string currencyName = string.IsNullOrEmpty(FromCurrencyName) ? "جنية مصري" : FromCurrencyName;
            string toCurrencyName = string.IsNullOrEmpty(ToCurrencyName) ? "جنية مصري" : ToCurrencyName;
            double exchangeRate = FormItem.ExchangeRate > 0 ? FormItem.ExchangeRate : 1.0;

            double amountInLocal = Math.Round(FormItem.PayMoneyTo > 0 ? FormItem.PayMoneyTo : FormItem.PayMoney * exchangeRate, 2);

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
                ToCurrencyName = toCurrencyName,
                ExchangeRate = exchangeRate,
                FromRate = FormItem.FromRate > 0 ? FormItem.FromRate : 1.0,
                ToRate = FormItem.ToRate > 0 ? FormItem.ToRate : 1.0,
                AmountInLocalCurrency = amountInLocal,
                // ─────────────────────────────────────────────────────────────────

                FromCashName = CashList.FirstOrDefault(c => c.CashId == FormItem.CashId)?.CashName ?? "",
                ToCashName = CashList.FirstOrDefault(c => c.CashId == FormItem.CashTo)?.CashName ?? "",
                Amount = FormItem.PayMoney,
                Notes = FormItem.Notes ?? "",

                FromCashPreviousBalance = fromCashOld,
                FromCashBalanceAfter = fromCashOld - FormItem.PayMoney,
                ToCashPreviousBalance = toCashOld,
                ToCashBalanceAfter = toCashOld + amountInLocal
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
