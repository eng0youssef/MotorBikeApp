using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using MotorBike.Services;

namespace MotorBike.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    // ════════════════════════════════════════════════════════════
    // المسار الكامل لملف appsettings.json
    // ════════════════════════════════════════════════════════════
    private static string AppSettingsPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    // ════════════════════════════════════════════════════════════
    // Properties
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
    // Constructor
    // ════════════════════════════════════════════════════════════

    public SettingsViewModel()
    {
        LoadConnectionString();
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

            try
            {
                // فك التشفير عشان يعرض النص العادي للمستخدم
                ConnectionString = ConnectionStringEncryptor.Decrypt(raw);
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException || ex is FormatException)
            {
                ConnectionString = "";
                ShowStatus("⚠️ فشل فك تشفير نص الاتصال، قد يكون تم تشفيره على جهاز آخر أو تالف. يرجى إدخال نص اتصال جديد.", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في تحميل الإعدادات: {ex.Message}", false);
        }
    }

    // ════════════════════════════════════════════════════════════
    // Commands
    // ════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            ShowStatus("يرجى إدخال الـ Connection String أولاً.", false);
            return;
        }

        IsTesting = true;
        IsStatusVisible = false;

        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            ShowStatus("✅  تم الاتصال بقاعدة البيانات بنجاح!", true);
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
            ShowStatus("يرجى إدخال الـ Connection String.", false);
            return;
        }

        IsSaving = true;
        IsStatusVisible = false;

        try
        {
            // تشفير قبل الحفظ
            var encrypted = ConnectionStringEncryptor.Encrypt(ConnectionString);

            // قراءة ملف appsettings.json وتحديث القيمة
            var json = await File.ReadAllTextAsync(AppSettingsPath);
            var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
            var node = JsonNode.Parse(json, documentOptions: documentOptions)
                ?? throw new InvalidOperationException("ملف appsettings.json تالف.");

            // إنشاء الـ node إذا مش موجود
            if (node["ConnectionStrings"] is not JsonObject connStrings)
            {
                connStrings = new JsonObject();
                node["ConnectionStrings"] = connStrings;
            }
            connStrings["DefaultConnection"] = encrypted;

            // حفظ مع تنسيق جميل
            var options = new JsonSerializerOptions { WriteIndented = true };
            var newJson = node.ToJsonString(options);
            await File.WriteAllTextAsync(AppSettingsPath, newJson);

            ShowStatus("✅  تم حفظ الإعدادات وتشفير الاتصال بنجاح! يُرجى إعادة تشغيل البرنامج لتطبيق التغييرات.", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"❌  خطأ في الحفظ: {ex.Message}", false);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        LoadConnectionString();
        ShowStatus("تم استعادة القيمة المحفوظة.", true);
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
}
