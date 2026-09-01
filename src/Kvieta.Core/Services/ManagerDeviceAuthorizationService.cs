using System.Security.Cryptography;
using System.Text;
using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public static class ManagerDeviceAuthorizationService
{
    public static bool VerifyResponse(
        ManagerDeviceEnrollment enrollment,
        RecoveryChallenge challenge,
        RecoveryChallengeResponse response,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(response);
        if (!enrollment.IsActive ||
            !string.Equals(enrollment.DeviceId, challenge.DeviceId, StringComparison.Ordinal) ||
            !string.Equals(challenge.ChallengeId, response.ChallengeId, StringComparison.Ordinal) ||
            !string.Equals(challenge.DeviceId, response.DeviceId, StringComparison.Ordinal) ||
            !string.Equals(challenge.NonceBase64, response.NonceBase64, StringComparison.Ordinal) ||
            challenge.ExpiresAtUtc <= now.ToUniversalTime() ||
            challenge.ExpiresAtUtc > now.ToUniversalTime().AddMinutes(10))
        {
            return false;
        }

        try
        {
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportFromPem(enrollment.PublicKeyPem);
            return verifier.KeySize == 256 && verifier.VerifyData(
                Encoding.UTF8.GetBytes(CreateSignedContent(challenge)),
                Convert.FromBase64String(response.SignatureBase64),
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

    public static string CreateSignedContent(RecoveryChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return string.Join(
            '.',
            "kvieta-manager-recovery-v1",
            EncodeField(challenge.ChallengeId),
            EncodeField(challenge.DeviceId),
            EncodeField(challenge.ExpiresAtUtc.ToUnixTimeSeconds().ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            EncodeField(challenge.NonceBase64),
            EncodeField(challenge.Purpose),
            EncodeField(challenge.PayloadHashBase64));
    }

    private static string EncodeField(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
