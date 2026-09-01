using System.Security.Cryptography;
using System.Text;
using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public static class ManagerDeviceRecoveryService
{
    public const string PinResetPurpose = "pin-reset";

    public static RecoveryChallenge CreatePinResetChallenge(
        string deviceId,
        AdminCredential newCredential,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newCredential);
        if (!newCredential.IsConfigured)
        {
            throw new ArgumentException("A configured credential is required.", nameof(newCredential));
        }

        return new RecoveryChallengeService().Issue(
            deviceId,
            now,
            PinResetPurpose,
            CreateCredentialHash(newCredential));
    }

    public static bool MatchesPinReset(RecoveryChallenge challenge, AdminCredential credential)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(credential);
        return credential.IsConfigured &&
            string.Equals(challenge.Purpose, PinResetPurpose, StringComparison.Ordinal) &&
            FixedTimeBase64Equals(challenge.PayloadHashBase64, CreateCredentialHash(credential));
    }

    public static string CreateCredentialHash(AdminCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        string content = string.Join(
            '.',
            "kvieta-admin-credential-v1",
            EncodeField(credential.SaltBase64),
            EncodeField(credential.HashBase64),
            credential.Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    private static bool FixedTimeBase64Equals(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(left),
                Convert.FromBase64String(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncodeField(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
