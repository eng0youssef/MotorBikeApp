using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ExpPaymentsViewModel : LookupViewModelBase<ExpPayment>
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepository<Expense> _expenseRepo;
    private readonly IRepository<Cash> _cashRepo;
    private readonly CompositeKeyRepository _compositeRepo;

    [ObservableProperty] private ObservableCollection<Expense> _expenses = [];
    [ObservableProperty] private ObservableCollection<Cash> _cashList = [];

    // ── لحفظ القيم القديمة عند التعديل ──
    private int? _oldCashId;

    public ExpPaymentsViewModel(
        IDbConnectionFactory dbFactory,
        IRepository<ExpPayment> repository,
        IRepository<Expense> expenseRepo,
        IRepository<Cash> cashRepo,
        CompositeKeyRepository compositeRepo) : base(repository)
    {
        _dbFactory = dbFactory;
        _expenseRepo = expenseRepo;
        _cashRepo = cashRepo;
        _compositeRepo = compositeRepo;
    }

    [RelayCommand]
    public async Task LoadRelatedDataAsync()
    {
        try
        {
            var expenses = await _expenseRepo.GetAllAsync();
            Expenses = new ObservableCollection<Expense>(expenses.Where(x => x.Active));

            var cash = await _cashRepo.GetAllAsync();
            CashList = new ObservableCollection<Cash>(cash.Where(x => x.Active));
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات المرتبطة: {ex.Message}";
        }
    }

    protected override object GetEntityId(ExpPayment entity) => entity.PayId;
    protected override bool IsNewRecord(ExpPayment entity) => entity.PayId == 0;
    protected override void SetEntityId(ExpPayment entity, int id) => entity.PayId = id;

    protected override void SetDefaultValues(ExpPayment entity)
    {
        base.SetDefaultValues(entity);
        entity.PayDate = DateTime.Now;

        if (Expenses.Any()) entity.ExpId = Expenses.First().ExpId;
        if (CashList.Any()) entity.CashId = CashList.First().CashId;
    }

    // ── Balance Recalculation Hooks ─────────────────────────────────

    protected override async Task BeforeSaveAsync(bool isInsert)
    {
        if (!isInsert && FormItem != null)
        {
            // جلب القيم القديمة من قاعدة البيانات قبل التعديل
            using var db = _dbFactory.CreateConnection();
            var old = await db.QueryFirstOrDefaultAsync<ExpPayment>(
                "SELECT CashID FROM Exp_Payments WHERE Pay_ID = @PayId",
                new { FormItem.PayId });

            if (old != null)
            {
                _oldCashId = old.CashId;
            }
        }
        else
        {
            _oldCashId = null;
        }
    }

    protected override async Task AfterSaveAsync(bool wasInsert)
    {
        if (FormItem == null) return;

        // إعادة حساب رصيد الخزينة القديمة لو اتغيرت
        if (_oldCashId.HasValue && _oldCashId.Value != (FormItem.CashId ?? 0) && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);

        // إعادة حساب رصيد الخزينة الحالية
        if (FormItem.CashId.HasValue && FormItem.CashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(FormItem.CashId.Value);
    }

    protected override Task BeforeDeleteAsync()
    {
        // حفظ بيانات السجل المحدد قبل الحذف
        if (SelectedItem != null)
        {
            _oldCashId = SelectedItem.CashId;
        }
        return Task.CompletedTask;
    }

    protected override async Task AfterDeleteAsync()
    {
        // إعادة حساب رصيد الخزينة بعد الحذف
        if (_oldCashId.HasValue && _oldCashId.Value > 0)
            await _compositeRepo.RecalcBalanceForCashAsync(_oldCashId.Value);
    }

    [RelayCommand]
    private async Task PrintReceiptAsync()
    {
        if (FormItem == null || FormItem.PayId <= 0)
        {
            System.Windows.MessageBox.Show("يجب حفظ الإيصال أولاً لطباعته.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var db = _dbFactory.CreateConnection();
            var company = await db.QueryFirstOrDefaultAsync<Company>("SELECT TOP 1 * FROM Company");

            double previousBalance = await _compositeRepo.GetCashOldBalanceAsync((int)FormItem.CashId, FormItem.PayDate);

            var model = new MotorBike.Services.ExpPaymentReceiptModel
            {
                ReceiptNo = FormItem.PayId.ToString(),
                IssueDate = FormItem.PayDate.ToString("yyyy-MM-dd"),
                ExpenseName = Expenses.FirstOrDefault(e => e.ExpId == FormItem.ExpId)?.ExpName ?? "",
                CashName = CashList.FirstOrDefault(c => c.CashId == FormItem.CashId)?.CashName ?? "",
                Amount = FormItem.PayMoney,
                Notes= FormItem.Notes ?? "",
                PreviousBalance = previousBalance,
                BalanceAfter = previousBalance - FormItem.PayMoney
            };

            var document = new MotorBike.Services.ExpPaymentReceiptDocument(model, company);
            var previewWindow = new MotorBike.Views.PrintPreviewWindow(document, "إيصال دفع مصروف رقم " + FormItem.PayId);
            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("حدث خطأ أثناء الطباعة: " + ex.Message, "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
