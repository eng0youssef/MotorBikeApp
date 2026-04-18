using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using MotorBike.Helpers;

namespace MotorBike.Services.Activation;

public class ActivationService
{
    private readonly HttpClient _httpClient;
    private readonly HardwareInfoService _hardwareService;
    private const string ServerUrl = "https://activationapp.runasp.net";
    private const string SecretKey = "Mazaya@Activation#Secret!Key$2024";
    private const int GracePeriodDays = 30;

    private readonly string _localStatePath;

    public ActivationService(HardwareInfoService hardwareService)
    {
        _httpClient = new HttpClient();
        _hardwareService = hardwareService;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "MotorBike");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        _localStatePath = Path.Combine(folder, ".actv");
    }

    /// <summary>
    /// Checks if the app is activated locally (cached status within 30 days).
    /// </summary>
    public bool IsActivatedLocally()
    {
        if (!File.Exists(_localStatePath)) return false;

        try
        {
            string encrypted = File.ReadAllText(_localStatePath);
            string decrypted = CryptoHelper.Decrypt(encrypted);
            
            if (string.IsNullOrEmpty(decrypted)) return false;

            var parts = decrypted.Split('|');
            if (parts.Length < 3) return false;

            string status = parts[0];
            string hwid = parts[1];
            DateTime lastCheck = DateTime.Parse(parts[2]);

            // Validate HWID matches current machine
            var currentHwid = _hardwareService.GetCurrentDeviceInfo().HWID;
            if (status == "ACTIVE" && hwid == currentHwid && (DateTime.Now - lastCheck).TotalDays < GracePeriodDays)
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    public enum ServerCheckStatus { Success, NotActivated, NetworkError }

    /// <summary>
    /// Calls the online server to check current activation status.
    /// </summary>
    public async Task<(ServerCheckStatus Status, string Message)> CheckServerAsync()
    {
        try
        {
            var info = _hardwareService.GetCurrentDeviceInfo();
            string encryptedId = CryptoHelper.Encrypt(info.HWID + SecretKey);

            var request = new
            {
                androidId = encryptedId,
                version = "Desktop-1.0.0"
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.PostAsJsonAsync($"{ServerUrl}/api/CustomerData/check-activation", request, options, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ActivationResult>();
                if (result != null && result.IsActivated)
                {
                    SaveActivationLocally(info.HWID);
                    return (ServerCheckStatus.Success, "تم التفعيل بنجاح");
                }
                return (ServerCheckStatus.NotActivated, result?.Message ?? "الجهاز غير مفعل");
            }
            return (ServerCheckStatus.NetworkError, "فشل الاتصال بسيرفر التفعيل");
        }
        catch (Exception ex)
        {
            return (ServerCheckStatus.NetworkError, $"خطأ اتصال: {ex.Message}");
        }
    }

    /// <summary>
    /// Submits a registration request for this device.
    /// </summary>
    public async Task<(bool Success, string Message)> RegisterAsync(string referenceName)
    {
        try
        {
            var info = _hardwareService.GetCurrentDeviceInfo();
            
            var request = new
            {
                encryptedDeviceId = CryptoHelper.Encrypt(info.HWID + SecretKey),
                manufacturer = CryptoHelper.Encrypt(info.Manufacturer),
                model = CryptoHelper.Encrypt(info.Model),
                ram = CryptoHelper.Encrypt(info.RAM_GB.ToString()),
                storage = CryptoHelper.Encrypt(info.Storage_GB.ToString()),
                referenceName = CryptoHelper.Encrypt(referenceName)
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var response = await _httpClient.PostAsJsonAsync($"{ServerUrl}/api/DeviceRegistration/submit", request, options);
            
            if (response.IsSuccessStatusCode)
            {
                return (true, "تم إرسال طلب التفعيل بنجاح. يرجى انتظار الموافقة.");
            }
            
            string error = await response.Content.ReadAsStringAsync();
            return (false, $"فشل الطلب: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            return (false, $"خطأ اتصال: {ex.Message}");
        }
    }

    private void SaveActivationLocally(string hwid)
    {
        string data = $"ACTIVE|{hwid}|{DateTime.Now:O}";
        string encrypted = CryptoHelper.Encrypt(data);

        if (File.Exists(_localStatePath))
        {
            File.SetAttributes(_localStatePath, FileAttributes.Normal);
        }

        File.WriteAllText(_localStatePath, encrypted);
        File.SetAttributes(_localStatePath, FileAttributes.Hidden);
    }

    private class ActivationResult
    {
        public bool IsActivated { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
