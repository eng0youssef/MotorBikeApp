using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

public partial class ScreenPermission : ObservableObject
{
    public int FrmId { get; set; }
    public string ScreenName { get; set; } = string.Empty;

    [ObservableProperty] private bool _canView;
    [ObservableProperty] private bool _canAdd;
    [ObservableProperty] private bool _canEdit;
    [ObservableProperty] private bool _canDelete;

    public string GetAbilitiesString()
    {
        var abilities = new System.Collections.Generic.List<string>();
        if (CanView) abilities.Add(AppAbility.View);
        if (CanAdd) abilities.Add(AppAbility.Add);
        if (CanEdit) abilities.Add(AppAbility.Edit);
        if (CanDelete) abilities.Add(AppAbility.Delete);
        return string.Join(",", abilities);
    }

    public void LoadAbilitiesString(string abilitiesStr)
    {
        if (string.IsNullOrWhiteSpace(abilitiesStr)) return;
        var parts = abilitiesStr.Split(',').Select(p => p.Trim().ToLowerInvariant()).ToHashSet();
        
        CanView = parts.Contains(AppAbility.View.ToLowerInvariant());
        CanAdd = parts.Contains(AppAbility.Add.ToLowerInvariant());
        CanEdit = parts.Contains(AppAbility.Edit.ToLowerInvariant());
        CanDelete = parts.Contains(AppAbility.Delete.ToLowerInvariant());
    }
}

public partial class UserPermissionsViewModel : ObservableObject
{
    private readonly IDbConnectionFactory _db;
    public int UserId { get; }
    public string UserName { get; }

    public ObservableCollection<ScreenPermission> Permissions { get; } = new();

    public UserPermissionsViewModel(IDbConnectionFactory db, int userId, string userName)
    {
        _db = db;
        UserId = userId;
        UserName = userName;
    }

    public Action? RequestClose { get; set; }

    [RelayCommand]
    internal async Task LoadAsync()
    {
        try
        {
            using var connection = _db.CreateConnection();
            var existingSubs = await connection.QueryAsync<UserSub>("SELECT * FROM User_Sub WHERE UserID = @userId", new { userId = UserId });

            Permissions.Clear();

            // Populate from enum
            foreach (ScreenId screen in Enum.GetValues(typeof(ScreenId)))
            {
                var p = new ScreenPermission
                {
                    FrmId = (int)screen,
                    ScreenName = GetArabicName(screen)
                };

                var sub = existingSubs.FirstOrDefault(x => x.FrmId == p.FrmId);
                if (sub != null)
                {
                    p.LoadAbilitiesString(sub.Ability ?? "");
                }

                Permissions.Add(p);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطأ في تحميل الصلاحيات: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            using var connection = _db.CreateConnection();
            connection.Open();
            
            // Delete old
            await connection.ExecuteAsync("DELETE FROM User_Sub WHERE UserID = @userId", new { userId = UserId });

            // Insert new
            var sql = "INSERT INTO User_Sub (IDSub, UserID, FrmID, Ability) VALUES (@IDSub, @UserID, @FrmID, @Ability)";
            
            // Generate next IDSub if it is not auto incremented? Wait, IDSub is probably an identity or needs MAX(IDSub).
            // Let's check if it's identity. Usually SQL Server has it as identity. 
            // If it's not identity, we need to generate it. Let's assume it's NOT identity because the schema says: [IDSub] [int] NOT NULL.
            // If it fails, we will generate it. I'll generate it just in case.
            
            var maxId = await connection.ExecuteScalarAsync<int?>("SELECT MAX(IDSub) FROM User_Sub") ?? 0;

            foreach (var p in Permissions)
            {
                var abilities = p.GetAbilitiesString();
                if (!string.IsNullOrEmpty(abilities))
                {
                    maxId++;
                    await connection.ExecuteAsync(sql, new { IDSub = maxId, UserID = UserId, FrmID = p.FrmId, Ability = abilities });
                }
            }

            MessageBox.Show("تم حفظ الصلاحيات بنجاح.", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطأ في حفظ الصلاحيات: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var p in Permissions)
        {
            p.CanView = true;
            p.CanAdd = true;
            p.CanEdit = true;
            p.CanDelete = true;
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var p in Permissions)
        {
            p.CanView = false;
            p.CanAdd = false;
            p.CanEdit = false;
            p.CanDelete = false;
        }
    }

    private string GetArabicName(ScreenId screen)
    {
        return screen switch
        {
            ScreenId.Company => "بيانات الشركة",
            ScreenId.Users => "المستخدمين",
            ScreenId.Cities => "المدن",
            ScreenId.Customers => "العملاء",
            ScreenId.Suppliers => "الموردين",
            ScreenId.Units => "وحدات الأصناف",
            ScreenId.ItemCategories => "مجموعات الأصناف",
            ScreenId.Items => "الأصناف",
            ScreenId.Stores => "المخازن",
            ScreenId.OpenStock => "أرصدة أول المدة",
            ScreenId.CarBrands => "ماركات الموتوسيكلات",
            ScreenId.CarModels => "موديلات الماركة",
            ScreenId.Colors => "الألوان",
            ScreenId.Cash => "الخزائن",
            ScreenId.Omla => "العملات",
            ScreenId.ExpGroups => "مجموعات المصروفات",
            ScreenId.Expenses => "المصروفات",
            ScreenId.Buys => "فاتورة المشتريات",
            ScreenId.Sales => "فاتورة المبيعات",
            ScreenId.BuyCar => "شراء موتوسيكل",
            ScreenId.SalesCar => "بيع موتوسيكل",
            ScreenId.ReBuy => "مرتجع المشتريات",
            ScreenId.ReSales => "مرتجع المبيعات",
            ScreenId.CusPayments => "مقبوضات العملاء",
            ScreenId.SuppPayments => "مدفوعات الموردين",
            ScreenId.ExpPayments => "سندات المصروفات",
            ScreenId.CashTransfers => "تحويلات الخزائن",
            ScreenId.Cars => "الموتوسيكلات",
            ScreenId.Inspections => "الكشف الفني",
            ScreenId.ImportSuppliers => "موردي الاستيراد",
            ScreenId.ImportExpenses => "مصروفات الاستيراد",
            ScreenId.ImportInvoice => "فاتورة استيراد",
            ScreenId.ImportPayments => "مدفوعات الاستيراد",
            ScreenId.StoreReports => "تقارير المخازن",
            ScreenId.SalesReports => "تقارير المبيعات",
            ScreenId.PurchasesReports => "تقارير المشتريات",
            ScreenId.ProfitsReports => "تقارير الأرباح",
            ScreenId.CarsReports => "تقارير الموتوسيكلات",
            ScreenId.ImportReports => "تقارير الاستيراد",
            ScreenId.CashReports => "تقارير الخزائن",
            ScreenId.Settings => "الإعدادات",
            _ => screen.ToString()
        };
    }
}
