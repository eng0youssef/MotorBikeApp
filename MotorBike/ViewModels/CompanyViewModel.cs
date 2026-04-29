using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.DataAccess;
using MotorBike.Models;

namespace MotorBike.ViewModels;

/// <summary>
/// ViewModel لشاشة بيانات الشركة — شاشة فردية (سجل واحد فقط) بدون DataGrid.
/// </summary>
public partial class CompanyViewModel : ObservableObject
{
    private readonly IRepository<Company> _repository;

    [ObservableProperty]
    private Company _company = new();

    [ObservableProperty]
    private string? _statusMessage;

    public CompanyViewModel(IRepository<Company> repository)
    {
        _repository = repository;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            var all = await _repository.GetAllAsync();
            var first = all.FirstOrDefault();
            if (first is not null)
            {
                Company = first;
                StatusMessage = "تم تحميل بيانات الشركة";
            }
            else
            {
                Company = new Company { Id = 1 };
                StatusMessage = "لا توجد بيانات — أدخل بيانات الشركة واضغط حفظ";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في التحميل: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (!AppSession.HasPermission(ScreenId.Company, AppAbility.Edit))
        {
            System.Windows.MessageBox.Show("عفواً، ليس لديك صلاحية لإجراء هذه العملية.", "صلاحيات غير كافية", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        try
        {
            var existing = await _repository.GetByIdAsync(Company.Id);
            if (existing is not null)
            {
                await _repository.UpdateAsync(Company);
                StatusMessage = "تم حفظ بيانات الشركة بنجاح ✓";
            }
            else
            {
                await _repository.InsertAsync(Company);
                StatusMessage = "تم إنشاء بيانات الشركة بنجاح ✓";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الحفظ: {ex.Message}";
        }
    }

    [RelayCommand]
    public void SelectLogo()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختر شعار الشركة",
            Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(openFileDialog.FileName);
                
                // Fire partial notification to update the bound Image depending on whether the object triggers change
                var oldCompany = Company;
                oldCompany.Logo = bytes;
                
                // Force UI update for the Company object
                OnPropertyChanged(nameof(Company));
                StatusMessage = "تم اختيار الشعار (تذكر حفظ البيانات)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في قراءة الصورة: {ex.Message}";
            }
        }
    }
}
