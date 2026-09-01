using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Kvieta.Core.Models;

namespace Kvieta.Core.Services;

public static class ManagerDeviceVerificationCode
{
    public static string ForRecoveryChallenge(RecoveryChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return FromContent(ManagerDeviceAuthorizationService.CreateSignedContent(challenge));
    }

    public static string FromContent(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        uint value = BinaryPrimitives.ReadUInt32BigEndian(hash);
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
