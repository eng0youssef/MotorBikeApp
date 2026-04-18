using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MotorBike.Helpers;

/// <summary>
/// AES-256-CBC encryption service for activation data.
/// Uses the same keys as the mobile app and server for compatibility.
/// </summary>
public static class CryptoHelper
{
    // Secure AES-256 Key (32 bytes) - Must match server/mobile app
    private static readonly byte[] Key =
    {
        0xA3, 0x70, 0x49, 0xD6, 0xB8, 0xE1, 0x4D, 0x30,
        0x6C, 0x08, 0x69, 0x09, 0x6E, 0x20, 0x19, 0x72,
        0xA9, 0xCB, 0x88, 0xCE, 0x18, 0x9D, 0x34, 0x45,
        0x61, 0xF8, 0x49, 0xA6, 0x40, 0x50, 0x9F, 0x77
    };

    // Secure AES IV (16 bytes) - Must match server/mobile app
    private static readonly byte[] IV =
    {
        0xC7, 0x6C, 0xC1, 0x9E, 0xD8, 0x10, 0x49, 0x01,
        0xAF, 0xF0, 0xDA, 0xF6, 0xBC, 0xD2, 0x97, 0x86
    };

    /// <summary>
    /// Encrypts plaintext using AES-256-CBC.
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decrypts ciphertext using AES-256-CBC.
    /// </summary>
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);

            return sr.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }
}
