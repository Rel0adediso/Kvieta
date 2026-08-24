using System.Security.Cryptography;
using KardesKilidi.Core.Models;

namespace KardesKilidi.Core.Services;

public static class AdminPinService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DefaultIterations = 210_000;

    public static bool IsValidFormat(string pin)
    {
        return pin.Length is >= 4 and <= 8 && pin.All(char.IsAsciiDigit);
    }

    public static AdminCredential Create(string pin)
    {
        if (!IsValidFormat(pin))
        {
            throw new ArgumentException("PIN 4-8 rakamdan oluşmalıdır.", nameof(pin));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            pin,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return new AdminCredential
        {
            Iterations = DefaultIterations,
            SaltBase64 = Convert.ToBase64String(salt),
            HashBase64 = Convert.ToBase64String(hash)
        };
    }

    public static bool Verify(string pin, AdminCredential? credential)
    {
        if (!IsValidFormat(pin) || credential is null || !credential.IsConfigured)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(credential.SaltBase64);
            byte[] expected = Convert.FromBase64String(credential.HashBase64);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                pin,
                salt,
                credential.Iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
