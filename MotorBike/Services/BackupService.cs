using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MotorBike.Services;

/// <summary>
/// خدمة الباك أب التلقائي لقاعدة البيانات.
/// يتم استدعاؤها تلقائياً عند إغلاق البرنامج.
/// 
/// هيكل الفولدرات:
///   D:\MotoBackUp\
///     └── 2026-04-28\
///           └── MotorBike_DB_20260428_1715_PCNAME.bak
/// </summary>
public class BackupService
{
    private readonly string _connectionString;

    // المسار الجذر للباك أب على البارتشن D
    private const string BackupRoot = @"D:\MotoBackUp";

    public BackupService(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(raw))
            raw = @"Server=.\SQLEXPRESS;Database=MotorBike_DB;Trusted_Connection=True;TrustServerCertificate=True;";

        try
        {
            _connectionString = ConnectionStringEncryptor.Decrypt(raw);
        }
        catch
        {
            _connectionString = @"Server=.\SQLEXPRESS;Database=MotorBike_DB;Trusted_Connection=True;TrustServerCertificate=True;";
        }
    }

    /// <summary>
    /// ينفذ الباك أب الفعلي لقاعدة البيانات.
    /// يُستدعى عند الإغلاق.
    /// </summary>
    public async Task RunBackupAsync()
    {
        try
        {
            // ← استخرج اسم الداتابيز من connection string
            string dbName = ExtractDatabaseName(_connectionString);

            // ← اسم الجهاز (Computer Name)
            string machineName = Dns.GetHostName();

            // ← التاريخ والوقت الحالي
            DateTime now = DateTime.Now;
            string dateFolder  = now.ToString("yyyy-MM-dd");           // 2026-04-28
            string dateTimePart = now.ToString("yyyyMMdd_HHmm");       // 20260428_1715

            // ← اسم ملف الباك أب:  MotorBike_DB_20260428_1715_MYPC.bak
            string fileName = $"{dbName}_{dateTimePart}_{machineName}.bak";

            // ← مسار فولدر اليوم:  D:\MotoBackUp\2026-04-28
            string dailyFolder = Path.Combine(BackupRoot, dateFolder);

            // ← أنشئ الفولدرات لو مش موجودة
            Directory.CreateDirectory(dailyFolder);   // يعمل MotoBackUp وفولدر اليوم لو مش موجودين

            // ← المسار الكامل لملف الباك أب
            string fullPath = Path.Combine(dailyFolder, fileName);

            // ← نفذ أمر BACKUP DATABASE عبر T-SQL
            await ExecuteBackupCommandAsync(dbName, fullPath);
        }
        catch (Exception ex)
        {
            // لو فيه مشكلة، اكتب الخطأ في لوج بسيط بجانب المشروع
            // ما نوقفش البرنامج ولا نزعج المستخدم
            LogError(ex);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Private Helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private async Task ExecuteBackupCommandAsync(string dbName, string backupFilePath)
    {
        // SQL Server بيحتاج المسار يكون بصلاحياته هو، مش صلاحيات الـ app
        // لذلك هنستخدم BACKUP DATABASE مع TO DISK مباشرة
        string sql = $@"
            BACKUP DATABASE [{dbName}]
            TO DISK = N'{backupFilePath}'
            WITH FORMAT,
                 MEDIANAME = N'MotoBackUp',
                 NAME = N'{dbName} - Auto Backup',
                 STATS = 10,
                 COMPRESSION;";

        // نفتح كونكشن جديد على master أو على الداتابيز نفسها
        // BACKUP DATABASE يشتغل من أي داتابيز
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            // نزود الـ timeout لأن الباك أب ممكن ياخد وقت
            ConnectTimeout = 30
        };

        using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(sql, conn)
        {
            // CommandTimeout: 10 دقايق عشان الداتابيز الكبيرة
            CommandTimeout = 600
        };

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// بيستخرج اسم الداتابيز من الـ connection string.
    /// </summary>
    private static string ExtractDatabaseName(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.InitialCatalog)
                ? "MotorBike_DB"
                : builder.InitialCatalog;
        }
        catch
        {
            return "MotorBike_DB";
        }
    }

    /// <summary>
    /// يكتب الأخطاء في ملف لوج بجانب البرنامج بدون ما يوقفه.
    /// </summary>
    private static void LogError(Exception ex)
    {
        try
        {
            string logPath = Path.Combine(BackupRoot, "backup_errors.log");
            Directory.CreateDirectory(BackupRoot); // تأكد إن المجلد موجود
            File.AppendAllText(logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // لو حتى اللوج فشل، مش هنعمل حاجة - البرنامج لازم يقفل
        }
    }
}
