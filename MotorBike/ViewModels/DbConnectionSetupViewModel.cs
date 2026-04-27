using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using MotorBike.Services;

namespace MotorBike.ViewModels;

public partial class DbConnectionSetupViewModel : ObservableObject
{
    // ════════════════════════════════════════════════════════════
    // Constants
    // ════════════════════════════════════════════════════════════
    private const string DefaultConnectionString =
        "Server=.\\SQLEXPRESS;Database=MotorBike_DB;Trusted_Connection=True;TrustServerCertificate=True;";

    private static string AppSettingsPath =>
        Path.Combine(
            Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
            ?? AppContext.BaseDirectory,
            "appsettings.json");

    // ════════════════════════════════════════════════════════════
    // Properties – Connection
    // ════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _connectionString = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusSuccess;

    [ObservableProperty]
    private bool _isStatusVisible;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private bool _isSaving;

    // ════════════════════════════════════════════════════════════
    // Properties – Network
    // ════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _localIpAddress = string.Empty;

    [ObservableProperty]
    private string _serverIpAddress = string.Empty;

    [ObservableProperty]
    private string _instanceName = "SQLEXPRESS";

    [ObservableProperty]
    private string _sqlPort = string.Empty;   // فاضي = الافتراضي 1433

    [ObservableProperty]
    private bool _useSqlServerAuth;

    [ObservableProperty]
    private string _sqlServerUserName = string.Empty;

    [ObservableProperty]
    private string _sqlServerPassword = string.Empty;

    [ObservableProperty]
    private string _networkStatus = string.Empty;

    [ObservableProperty]
    private bool _isNetworkSuccess;

    [ObservableProperty]
    private bool _isNetworkStatusVisible;

    /// <summary>يصبح true بعد نجاح اختبار الاتصال بالسيرفر الخارجي – يفتح زرار الحفظ</summary>
    [ObservableProperty]
    private bool _canSaveRemote;

    // ════════════════════════════════════════════════════════════
    // Callback – يُستدعى بعد الحفظ الناجح لإعادة تشغيل الـ Login
    // ════════════════════════════════════════════════════════════
    public Action? OnSavedAndReady { get; set; }

    // ════════════════════════════════════════════════════════════
    // Constructor
    // ════════════════════════════════════════════════════════════

    public DbConnectionSetupViewModel()
    {
        LoadConnectionString();
        LoadLocalIp();
    }

    // ════════════════════════════════════════════════════════════
    // Load
    // ════════════════════════════════════════════════════════════

    private void LoadConnectionString()
    {
        try
        {
            var json = File.ReadAllText(AppSettingsPath);
            var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
            var node = JsonNode.Parse(json, documentOptions: documentOptions);
            var raw = node?["ConnectionStrings"]?["DefaultConnection"]?.GetValue<string>() ?? string.Empty;
            ConnectionString = string.IsNullOrWhiteSpace(raw)
                ? DefaultConnectionString
                : ConnectionStringEncryptor.Decrypt(raw);
        }
        catch
        {
            ConnectionString = DefaultConnectionString;
        }
    }

    private void LoadLocalIp()
    {
        try
        {
            // ── أفضل طريقة: UDP socket trick لمعرفة الـ IP المستخدم فعلياً على الشبكة
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var localEndPoint = socket.LocalEndPoint as IPEndPoint;
            var detected = localEndPoint?.Address.ToString();

            // تحقق إنه Private IP (LAN)
            if (!string.IsNullOrEmpty(detected) && IsPrivateIp(detected))
            {
                LocalIpAddress = detected;
                return;
            }

            // ── Fallback: ابحث في كل الـ adapters عن Private IP
            var privateIp = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork &&
                            IsPrivateIp(a.Address.ToString()))
                .Select(a => a.Address.ToString())
                .FirstOrDefault();

            LocalIpAddress = privateIp ?? detected ?? "غير متاح";
        }
        catch
        {
            LocalIpAddress = "غير متاح";
        }
    }

    /// <summary>يتحقق إن الـ IP من نطاق الشبكة الداخلية (Private)</summary>
    private static bool IsPrivateIp(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr)) return false;
        var bytes = addr.GetAddressBytes();
        return bytes[0] == 10 ||                                         // 10.x.x.x
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // 172.16-31.x.x
               (bytes[0] == 192 && bytes[1] == 168);                     // 192.168.x.x
    }

    // ════════════════════════════════════════════════════════════
    // Commands – Connection
    // ════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            ShowStatus("يرجى إدخال نص الاتصال أولاً.", false);
            return;
        }

        IsTesting = true;
        IsStatusVisible = false;

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            ShowStatus("✅  تم الاتصال بقاعدة البيانات بنجاح! يمكنك حفظ الإعدادات الآن.", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"❌  فشل الاتصال: {ex.Message}", false);
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            ShowStatus("يرجى إدخال نص الاتصال.", false);
            return;
        }

        IsSaving = true;
        IsStatusVisible = false;

        try
        {
            // اختبار الاتصال قبل الحفظ
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            // تشفير وحفظ
            var encrypted = ConnectionStringEncryptor.Encrypt(ConnectionString);
            var json = await File.ReadAllTextAsync(AppSettingsPath);
            var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
            var node = JsonNode.Parse(json, documentOptions: documentOptions)
                ?? throw new InvalidOperationException("ملف الإعدادات تالف.");

            if (node["ConnectionStrings"] is not JsonObject connStrings)
            {
                connStrings = new JsonObject();
                node["ConnectionStrings"] = connStrings;
            }
            connStrings["DefaultConnection"] = encrypted;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var newJson = node.ToJsonString(options);
            await File.WriteAllTextAsync(AppSettingsPath, newJson);

            ShowStatus("✅  تم حفظ الإعدادات بنجاح! جاري العودة لتسجيل الدخول...", true);

            await Task.Delay(1200);
            OnSavedAndReady?.Invoke();
        }
        catch (Exception ex)
        {
            ShowStatus($"❌  {ex.Message}", false);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        ConnectionString = DefaultConnectionString;
        ShowStatus("✅  تم استعادة الإعدادات الافتراضية. لا تنسَ اختبار الاتصال قبل الحفظ.", true);
    }

    // ════════════════════════════════════════════════════════════
    // Commands – Network / Server
    // ════════════════════════════════════════════════════════════

    [RelayCommand]
    private void UseThisAsServer()
    {
        // يضبط الـ connection string ليوجه لهذا الجهاز نفسه
        ConnectionString = DefaultConnectionString;
        ShowNetworkStatus(
            $"✅  يمكن للأجهزة الأخرى الاتصال باستخدام IP الجهاز الحالي: {LocalIpAddress}\n" +
            $"مثال: Server={LocalIpAddress}\\SQLEXPRESS;Database=MotorBike_DB;Trusted_Connection=True;TrustServerCertificate=True;",
            true);
    }

    [RelayCommand]
    private async Task TestServerConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerIpAddress))
        {
            ShowNetworkStatus("يرجى إدخال IP السيرفر أولاً.", false);
            return;
        }

        CanSaveRemote = false;
        IsTesting = true;
        ShowNetworkStatus("⏳  جاري اختبار الاتصال بالسيرفر...", true);

        try
        {
            // ── خطوة 1: Ping (تحذيري فقط – لا يوقف العملية) ──────────
            bool pingOk = false;
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ServerIpAddress, 2000);
                pingOk = reply.Status == IPStatus.Success;
            }
            catch { /* Ping محجوب أو مش متاح – نكمل */ }

            if (!pingOk)
                ShowNetworkStatus("⚠️  الـ Ping محجوب أو غير متاح – جاري تجربة الاتصال بـ SQL مباشرة...", true);

            // ── خطوة 2: بناء Connection String ─────────────────────────
            var serverPart = BuildServerPart();
            var authPart = UseSqlServerAuth && !string.IsNullOrWhiteSpace(SqlServerUserName)
                ? $"User Id={SqlServerUserName};Password={SqlServerPassword};"
                : "Trusted_Connection=True;";
            var cs = $"Server={serverPart};Database=MotorBike_DB;{authPart}TrustServerCertificate=True;Connect Timeout=8;";

            // ── خطوة 3: SqlConnection فعلي ──────────────────────────────
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            // نجح الاتصال ✅
            ConnectionString = cs;
            CanSaveRemote = true;
            ShowNetworkStatus(
                $"✅  تم الاتصال بقاعدة البيانات على ({ServerIpAddress}) بنجاح!\n" +
                $"اضغط \"حفظ الاتصال بالسيرفر\" لتثبيت الإعداد على هذا الجهاز.",
                true);
        }
        catch (Exception ex)
        {
            CanSaveRemote = false;
            string hint;
            if (ex.Message.Contains("login", StringComparison.OrdinalIgnoreCase))
                hint = "\n💡 فعّل مصادقة SQL Server (Mixed Mode) أو تحقق من اسم المستخدم وكلمة المرور.";
            else if (ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                     ex.Message.Contains("connect", StringComparison.OrdinalIgnoreCase) ||
                     ex.Message.Contains("server", StringComparison.OrdinalIgnoreCase))
                hint = "\n💡 تأكد من تفعيل TCP/IP في SQL Server Configuration Manager وفتح Port 1433 في Firewall.";
            else if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                hint = "\n💡 انتهت مهلة الاتصال – تأكد من صحة الـ IP وأن SQL Server شغّال.";
            else
                hint = string.Empty;
            ShowNetworkStatus($"❌  {ex.Message}{hint}", false);
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task SaveRemoteConnectionAsync()
    {
        IsSaving = true;
        try
        {
            var encrypted = ConnectionStringEncryptor.Encrypt(ConnectionString);
            var json = await File.ReadAllTextAsync(AppSettingsPath);
            var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
            var node = JsonNode.Parse(json, documentOptions: documentOptions)
                ?? throw new InvalidOperationException("ملف الإعدادات تالف.");

            if (node["ConnectionStrings"] is not JsonObject connStrings)
            {
                connStrings = new JsonObject();
                node["ConnectionStrings"] = connStrings;
            }
            connStrings["DefaultConnection"] = encrypted;

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(AppSettingsPath, node.ToJsonString(options));

            ShowNetworkStatus("✅  تم حفظ الإعدادات بنجاح! جاري العودة لتسجيل الدخول...", true);
            await Task.Delay(1200);
            OnSavedAndReady?.Invoke();
        }
        catch (Exception ex)
        {
            ShowNetworkStatus($"❌  فشل الحفظ: {ex.Message}", false);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ════════════════════════════════════════════════════════════
    // Helpers – Network
    // ════════════════════════════════════════════════════════════

    private string BuildServerPart()
    {
        // مثال: 192.168.1.10\SQLEXPRESS  أو  192.168.1.10,1433
        var ip = ServerIpAddress.Trim();
        var instance = InstanceName.Trim();
        var port = SqlPort.Trim();

        if (!string.IsNullOrWhiteSpace(port))
            return $"{ip},{port}";

        if (!string.IsNullOrWhiteSpace(instance))
            return $"{ip}\\{instance}";

        return ip;
    }

    // ════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════

    private void ShowStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
        IsStatusVisible = true;
    }

    private void ShowNetworkStatus(string message, bool success)
    {
        NetworkStatus = message;
        IsNetworkSuccess = success;
        IsNetworkStatusVisible = true;
    }
}
