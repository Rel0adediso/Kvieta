using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Otium.Core.Models;
using Otium.Core.Services;

namespace Otium.App.Services;

public static class ManagerDeviceEnrollmentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string FilePath => Path.Combine(
        ProtectionServiceManager.ProtectionDataDirectory,
        "manager-device.json");

    public static ManagerDeviceEnrollment? Load()
    {
        try
        {
            ManagerDeviceEnrollment? enrollment = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<ManagerDeviceEnrollment>(File.ReadAllText(FilePath), JsonOptions)
                : null;
            return enrollment is not null && IsValid(enrollment) ? enrollment : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(ManagerDeviceEnrollment enrollment)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        if (!IsValid(enrollment))
        {
            throw new ArgumentException("The manager device enrollment is invalid.", nameof(enrollment));
        }

        Directory.CreateDirectory(ProtectionServiceManager.ProtectionDataDirectory);
        string temporaryPath = $"{FilePath}.tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(enrollment, JsonOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);
            string? userSid = ProtectionServiceManager.LoadEnrollment()?.UserSid;
            if (string.IsNullOrWhiteSpace(userSid))
            {
                ProtectionServiceManager.HardenSensitiveFileAcl(FilePath);
            }
            else
            {
                ProtectionServiceManager.HardenReadOnlyFileAcl(FilePath, userSid);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static void Revoke(DateTimeOffset revokedAtUtc)
    {
        ManagerDeviceEnrollment? enrollment = Load();
        if (enrollment is not null)
        {
            Save(ManagerDeviceTransferService.Revoke(enrollment, revokedAtUtc));
        }
    }

    public static bool CompleteTransfer(
        ManagerDeviceEnrollment replacement,
        ManagerDeviceTransfer transfer,
        DateTimeOffset now)
    {
        ManagerDeviceEnrollment? current = Load();
        ManagerDeviceEnrollment? completed = current is null
            ? null
            : ManagerDeviceTransferService.CompleteTransfer(current, replacement, transfer, now);
        if (completed is null)
        {
            return false;
        }

        Save(completed);
        return true;
    }

    private static bool IsValid(ManagerDeviceEnrollment enrollment)
    {
        return ManagerDeviceEnrollmentService.IsWellFormed(enrollment);
    }
}
