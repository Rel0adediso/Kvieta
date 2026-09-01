using System.Security.Cryptography;
using System.Text;
using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public static class ManagerDeviceEnrollmentService
{
    public static bool VerifyRequest(ManagerDeviceEnrollmentRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ManagerDeviceEnrollment enrollment = request.Enrollment;
        if (!IsValid(enrollment) ||
            Math.Abs((now.ToUniversalTime() - enrollment.EnrolledAtUtc.ToUniversalTime()).TotalMinutes) > 10)
        {
            return false;
        }

        try
        {
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportFromPem(enrollment.PublicKeyPem);
            return verifier.KeySize == 256 && verifier.VerifyData(
                Encoding.UTF8.GetBytes(CreateProofContent(enrollment)),
                Convert.FromBase64String(request.ProofSignatureBase64),
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

    public static bool IsValid(ManagerDeviceEnrollment enrollment)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        return enrollment.IsActive && IsWellFormed(enrollment);
    }

    public static bool IsWellFormed(ManagerDeviceEnrollment enrollment)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        if (string.IsNullOrWhiteSpace(enrollment.DeviceId) || enrollment.DeviceId.Length > 128 ||
            string.IsNullOrWhiteSpace(enrollment.DeviceName) || enrollment.DeviceName.Length > 100 ||
            string.IsNullOrWhiteSpace(enrollment.PublicKeyPem) || enrollment.PublicKeyPem.Length > 4096)
        {
            return false;
        }

        try
        {
            using ECDsa key = ECDsa.Create();
            key.ImportFromPem(enrollment.PublicKeyPem);
            return key.KeySize == 256;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static string CreateProofContent(ManagerDeviceEnrollment enrollment)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        return string.Join(
            '.',
            "kvieta-manager-enrollment-v1",
            EncodeField(enrollment.DeviceId),
            EncodeField(enrollment.DeviceName),
            EncodeField(enrollment.PublicKeyPem),
            EncodeField(enrollment.EnrolledAtUtc.ToUnixTimeSeconds().ToString(
                System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static string EncodeField(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
