using System.Security.Cryptography;
using System.Text;

namespace MotorBike.Services;

/// <summary>
/// يشفر ويفك تشفير الـ connection string باستخدام Windows DPAPI على مستوى الجهاز.
/// القيمة المشفرة تبدأ بـ "ENC:" وإذا لم تكن مشفرة تُعامل كنص عادي.
/// </summary>
public static class ConnectionStringEncryptor
{
    private const string EncryptedPrefix = "ENC:";

    /// <summary>
    /// يشفر الـ connection string ويرجع قيمة Base64 مسبوقة بـ "ENC:"
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        // DataProtectionScope.LocalMachine → يعمل بس على نفس الجهاز
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.LocalMachine);
        return EncryptedPrefix + Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// يفك تشفير القيمة إذا كانت تبدأ بـ "ENC:"، وإلا يرجعها كما هي.
    /// </summary>
    public static string Decrypt(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (!value.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
            return value; // قيمة عادية غير مشفرة

        var base64 = value[EncryptedPrefix.Length..];
        var encryptedBytes = Convert.FromBase64String(base64);
        var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// يتحقق إذا كانت القيمة مشفرة (تبدأ بـ ENC:)
    /// </summary>
    public static bool IsEncrypted(string value)
        => !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
}
