using System;
using System.Management;
using System.Linq;

namespace MotorBike.Services.Activation;

public record WindowsDeviceInfo(
    string HWID,
    string Manufacturer,
    string Model,
    string CPU,
    string OSVersion,
    long RAM_GB,
    long Storage_GB);

public class HardwareInfoService
{
    public WindowsDeviceInfo GetCurrentDeviceInfo()
    {
        return new WindowsDeviceInfo(
            HWID: GetHardwareFingerprint(),
            Manufacturer: GetProperty("Win32_ComputerSystem", "Manufacturer"),
            Model: GetProperty("Win32_ComputerSystem", "Model"),
            CPU: GetProperty("Win32_Processor", "Name"),
            OSVersion: GetProperty("Win32_OperatingSystem", "Caption") + " " + GetProperty("Win32_OperatingSystem", "OSArchitecture"),
            RAM_GB: GetTotalRAM(),
            Storage_GB: GetTotalStorage()
        );
    }

    private string GetHardwareFingerprint()
    {
        try
        {
            // Combine multiple hardware identifiers for absolute uniqueness
            string rawData = 
                GetUUID() + 
                GetProperty("Win32_Processor", "ProcessorId") + 
                GetDiskSerialNumber();

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
            return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 32); // 32-char Hex ID
        }
        catch
        {
            return "FALLBACK-" + GetUUID();
        }
    }

    private string GetDiskSerialNumber()
    {
        try
        {
            // We want the physical drive hosting the OS (usually C:). 
            // Querying Index 0 is the most reliable way to get the boot/primary physical drive.
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index = 0");
            foreach (var obj in searcher.Get())
            {
                var serial = obj["SerialNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(serial)) return serial;
            }
        }
        catch { }

        // Fallback to any fixed drive if Index 0 fails
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType LIKE 'Fixed%'");
            foreach (var obj in searcher.Get())
            {
                var serial = obj["SerialNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(serial)) return serial;
            }
        }
        catch { }

        return "DISK-UNK";
    }

    private string GetUUID()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            foreach (var obj in searcher.Get())
            {
                var uuid = obj["UUID"]?.ToString();
                if (!string.IsNullOrEmpty(uuid) && uuid != "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF" && uuid != "00000000-0000-0000-0000-000000000000")
                    return uuid;
            }
        }
        catch { }

        return GetProperty("Win32_BaseBoard", "SerialNumber") ?? "NO-UUID";
    }

    private string GetProperty(string className, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            foreach (var obj in searcher.Get())
            {
                return obj[propertyName]?.ToString()?.Trim() ?? "Unknown";
            }
        }
        catch { }
        return "Unknown";
    }

    private long GetTotalRAM()
    {
        try
        {
            // Summing Capacity of all physical RAM sticks to show actual hardware size (e.g. 16GB)
            // instead of Win32_ComputerSystem.TotalPhysicalMemory which excludes hardware-reserved RAM.
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            long totalBytes = 0;
            foreach (var obj in searcher.Get())
            {
                totalBytes += Convert.ToInt64(obj["Capacity"]);
            }
            return (long)Math.Ceiling((double)totalBytes / (1024 * 1024 * 1024));
        }
        catch { }
        return 0;
    }

    private long GetTotalStorage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Size FROM Win32_DiskDrive WHERE MediaType LIKE 'Fixed%'");
            long totalBytes = 0;
            foreach (var obj in searcher.Get())
            {
                totalBytes += Convert.ToInt64(obj["Size"]);
            }
            // Round to the nearest common storage size for display (e.g. 238GB -> 256GB)
            // Or just show GiB. User context suggests they want to see the marketed size.
            double gib = (double)totalBytes / (1024 * 1024 * 1024);
            if (gib > 230 && gib < 256) return 256; // Common market sizes
            if (gib > 460 && gib < 512) return 512;
            if (gib > 900 && gib < 1024) return 1024;
            
            return (long)Math.Round(gib);
        }
        catch { }
        return 0;
    }
}
