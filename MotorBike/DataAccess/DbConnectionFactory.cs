using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MotorBike.Services;

namespace MotorBike.DataAccess;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = "Server=.\\SQLEXPRESS;Database=MotorBike_DB;Trusted_Connection=True;TrustServerCertificate=True;";
        }

        // فك التشفير تلقائياً إذا كانت القيمة مشفرة
        try
        {
            _connectionString = ConnectionStringEncryptor.Decrypt(raw);
        }
        catch
        {
            // في حالة كان التشفير تالف (تم اللعب في الملف يدوياً)، 
            // هنرجع لنص افتراضي عشان البرنامج يكمل واللوجن يكتشف إنه مش شغال فيفتح شاشة الإصلاح.
            _connectionString = "Server=.\\SQLEXPRESS;Database=MotorBike_DB;Trusted_Connection=True;TrustServerCertificate=True;";
        }
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
