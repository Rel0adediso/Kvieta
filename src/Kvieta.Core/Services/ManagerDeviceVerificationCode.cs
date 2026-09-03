using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public static class ManagerDeviceVerificationCode
{
    public static string ForEnrollmentRequest(ManagerDeviceEnrollmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return FromContent(ManagerDeviceEnrollmentService.CreateProofContent(request.Enrollment), 100_000_000, "D8");
    }

    public static string ForRecoveryChallenge(RecoveryChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return FromContent(ManagerDeviceAuthorizationService.CreateSignedContent(challenge));
    }

    public static string FromContent(string content)
        => FromContent(content, 1_000_000, "D6");

    private static string FromContent(string content, uint modulus, string format)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        uint value = BinaryPrimitives.ReadUInt32BigEndian(hash);
        return (value % modulus).ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }
}
