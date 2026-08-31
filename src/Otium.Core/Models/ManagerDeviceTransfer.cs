namespace Otium.Core.Models;

public sealed record ManagerDeviceTransfer(
    string CurrentDeviceId,
    string NewDeviceId,
    string NewDevicePublicKeyHashBase64,
    DateTimeOffset ExpiresAtUtc,
    string NonceBase64,
    string CurrentDeviceSignatureBase64,
    string NewDeviceSignatureBase64);
