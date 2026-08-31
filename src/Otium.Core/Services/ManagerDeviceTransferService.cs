using System.Security.Cryptography;
using System.Text;
using Otium.Core.Models;

namespace Otium.Core.Services;

public static class ManagerDeviceTransferService
{
    public static ManagerDeviceEnrollment? CompleteTransfer(
        ManagerDeviceEnrollment current,
        ManagerDeviceEnrollment replacement,
        ManagerDeviceTransfer transfer,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(transfer);
        if (!VerifyNewDeviceProposal(current, replacement, transfer, now) ||
            !VerifySignature(current.PublicKeyPem, transfer.CurrentDeviceSignatureBase64, transfer) ||
            !VerifySignature(replacement.PublicKeyPem, transfer.NewDeviceSignatureBase64, transfer))
        {
            return null;
        }

        return replacement with { RevokedAtUtc = null };
    }

    public static bool VerifyNewDeviceProposal(
        ManagerDeviceEnrollment current,
        ManagerDeviceEnrollment replacement,
        ManagerDeviceTransfer transfer,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(transfer);
        DateTimeOffset utcNow = now.ToUniversalTime();
        return current.IsActive &&
            ManagerDeviceEnrollmentService.IsValid(replacement) &&
            Math.Abs((utcNow - replacement.EnrolledAtUtc.ToUniversalTime()).TotalMinutes) <= 10 &&
            !string.Equals(current.DeviceId, replacement.DeviceId, StringComparison.Ordinal) &&
            string.Equals(transfer.CurrentDeviceId, current.DeviceId, StringComparison.Ordinal) &&
            string.Equals(transfer.NewDeviceId, replacement.DeviceId, StringComparison.Ordinal) &&
            PublicKeyHashMatches(replacement.PublicKeyPem, transfer.NewDevicePublicKeyHashBase64) &&
            transfer.ExpiresAtUtc > utcNow &&
            transfer.ExpiresAtUtc <= utcNow.AddMinutes(10) &&
            HasValidNonce(transfer.NonceBase64) &&
            VerifySignature(replacement.PublicKeyPem, transfer.NewDeviceSignatureBase64, transfer);
    }

    public static ManagerDeviceEnrollment Revoke(
        ManagerDeviceEnrollment enrollment,
        DateTimeOffset revokedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        return enrollment with { RevokedAtUtc = revokedAtUtc.ToUniversalTime() };
    }

    public static string CreateSignedContent(ManagerDeviceTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        return string.Join(
            '.',
            "otium-manager-transfer-v1",
            EncodeField(transfer.CurrentDeviceId),
            EncodeField(transfer.NewDeviceId),
            EncodeField(transfer.NewDevicePublicKeyHashBase64),
            EncodeField(transfer.ExpiresAtUtc.ToUnixTimeSeconds().ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            EncodeField(transfer.NonceBase64));
    }

    private static string EncodeField(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static bool HasValidNonce(string nonceBase64)
    {
        try
        {
            return Convert.FromBase64String(nonceBase64).Length >= 16;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string CreatePublicKeyHash(string publicKeyPem) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(publicKeyPem)));

    private static bool PublicKeyHashMatches(string publicKeyPem, string suppliedHashBase64)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(CreatePublicKeyHash(publicKeyPem)),
                Convert.FromBase64String(suppliedHashBase64));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool VerifySignature(string publicKeyPem, string signatureBase64, ManagerDeviceTransfer transfer)
    {
        try
        {
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportFromPem(publicKeyPem);
            return verifier.KeySize == 256 && verifier.VerifyData(
                Encoding.UTF8.GetBytes(CreateSignedContent(transfer)),
                Convert.FromBase64String(signatureBase64),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
