namespace Otium.Core.Models;

public sealed record ManagerDeviceEnrollment(
    string DeviceId,
    string DeviceName,
    string PublicKeyPem,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? RevokedAtUtc = null)
{
    public bool IsActive => RevokedAtUtc is null;
}
