using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorBike.Services.Activation;

namespace MotorBike.ViewModels;

public partial class ActivationViewModel : ObservableObject
{
    private readonly ActivationService _activationService;
    private readonly HardwareInfoService _hardwareService;

    [ObservableProperty] private string _hwid = string.Empty;
    [ObservableProperty] private string _cpu = string.Empty;
    [ObservableProperty] private string _os = string.Empty;
    [ObservableProperty] private string _ram = string.Empty;
    [ObservableProperty] private string _storage = string.Empty;
    [ObservableProperty] private string _referenceName = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "برجاء تفعيل النسخة للمتابعة";
    [ObservableProperty] private bool _isSuccess;

    public ActivationViewModel(ActivationService activationService, HardwareInfoService hardwareService)
    {
        _activationService = activationService;
        _hardwareService = hardwareService;
        
        var info = _hardwareService.GetCurrentDeviceInfo();
        Hwid = info.HWID;
        Cpu = info.CPU;
        Os = info.OSVersion;
        Ram = $"{info.RAM_GB} GB";
        Storage = $"{info.Storage_GB} GB";
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(ReferenceName) || ReferenceName.Length < 3)
        {
            StatusMessage = "برجاء إدخال اسم صحيح (3 أحرف على الأقل)";
            return;
        }

        IsBusy = true;
        StatusMessage = "جاري إرسال الطلب...";
        
        var (success, message) = await _activationService.RegisterAsync(ReferenceName);
        
        IsBusy = false;
        StatusMessage = message;
    }

    [RelayCommand]
    private async Task CheckActivationAsync()
    {
        IsBusy = true;
        StatusMessage = "جاري التحقق من التفعيل...";

        var (status, message) = await _activationService.CheckServerAsync();

        IsBusy = false;
        StatusMessage = message;

        if (status == ActivationService.ServerCheckStatus.Success)
        {
            IsSuccess = true;
            await Task.Delay(2000);
            RequestClose?.Invoke(true);
        }
    }

    [RelayCommand]
    private void CopyHwid()
    {
        Clipboard.SetText(Hwid);
        StatusMessage = "تم نسخ كود الجهاز بنجاح";
    }

    public event Action<bool>? RequestClose;
}
