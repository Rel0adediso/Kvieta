using System.Security.Cryptography;
using Otium.Core.Models;

namespace Otium.Core.Services;

public static class RecoveryCodeService
{
    private const int DefaultCodeCount = 8;
    private const int Iterations = 180_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static IReadOnlyList<string> Generate(ControlSettings settings, int count = DefaultCodeCount)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (count is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        List<string> plainCodes = [];
        List<RecoveryCodeRecord> records = [];
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        for (int index = 0; index < count; index++)
        {
            string id = CreateToken(6);
            string secret = CreateToken(18);
            string normalized = id + secret;
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Hash(normalized, salt, Iterations, HashSize);
            records.Add(new RecoveryCodeRecord
            {
                Id = id,
                Iterations = Iterations,
                SaltBase64 = Convert.ToBase64String(salt),
                HashBase64 = Convert.ToBase64String(hash),
                CreatedAtUtc = createdAt
            });
            plainCodes.Add($"{id}-{secret[..6]}-{secret[6..12]}-{secret[12..]}");
        }

        settings.RecoveryCodes = records;
        return plainCodes;
    }

    public static bool TryConsume(ControlSettings settings, string code, DateTimeOffset? usedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string normalized = Normalize(code);
        if (normalized.Length != 24)
        {
            return false;
        }

        string id = normalized[..6];
        RecoveryCodeRecord? record = settings.RecoveryCodes.FirstOrDefault(item =>
            item.UsedAtUtc is null && string.Equals(item.Id, id, StringComparison.Ordinal));
        if (record is null)
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(record.SaltBase64);
            byte[] expected = Convert.FromBase64String(record.HashBase64);
            byte[] actual = Hash(normalized, salt, record.Iterations, expected.Length);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                return false;
            }

            record.UsedAtUtc = usedAtUtc ?? DateTimeOffset.UtcNow;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Normalize(string? code) => string.IsNullOrWhiteSpace(code)
        ? string.Empty
        : new string(code.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static byte[] Hash(string code, byte[] salt, int iterations, int length) =>
        Rfc2898DeriveBytes.Pbkdf2(code, salt, iterations, HashAlgorithmName.SHA256, length);

    private static string CreateToken(int length)
    {
        char[] result = new char[length];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(result);
    }
}
