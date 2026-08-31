using System.Security.Cryptography;
using System.Text;
using Otium.Core.Models;

namespace Otium.Core.Services;

public sealed class RecoveryChallengeService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RecoveryChallenge> _activeChallenges = [];
    private readonly TimeSpan _lifetime;

    public RecoveryChallengeService(TimeSpan? lifetime = null)
    {
        _lifetime = lifetime ?? TimeSpan.FromMinutes(2);
        if (_lifetime <= TimeSpan.Zero || _lifetime > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }
    }

    public RecoveryChallenge Issue(
        string deviceId,
        DateTimeOffset now,
        string purpose = "generic",
        string payloadHashBase64 = "")
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device ID is required.", nameof(deviceId));
        }

        RecoveryChallenge challenge = new(
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            deviceId.Trim(),
            now.ToUniversalTime().Add(_lifetime),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            purpose,
            payloadHashBase64);
        lock (_gate)
        {
            RemoveExpired(now);
            _activeChallenges[challenge.ChallengeId] = challenge;
        }

        return challenge;
    }

    public bool TryConsume(
        RecoveryChallengeResponse response,
        string expectedDeviceId,
        ReadOnlySpan<byte> verificationKey,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (string.IsNullOrWhiteSpace(expectedDeviceId))
        {
            return false;
        }

        lock (_gate)
        {
            RemoveExpired(now);
            if (string.IsNullOrWhiteSpace(response.ChallengeId) ||
                !_activeChallenges.TryGetValue(response.ChallengeId, out RecoveryChallenge? challenge) ||
                !string.Equals(challenge.DeviceId, expectedDeviceId.Trim(), StringComparison.Ordinal) ||
                !string.Equals(challenge.NonceBase64, response.NonceBase64, StringComparison.Ordinal) ||
                !string.Equals(response.DeviceId, challenge.DeviceId, StringComparison.Ordinal) ||
                verificationKey.IsEmpty ||
                !IsValidSignature(challenge, response, verificationKey))
            {
                return false;
            }

            _activeChallenges.Remove(challenge.ChallengeId);
            return true;
        }
    }

    private static bool IsValidSignature(
        RecoveryChallenge challenge,
        RecoveryChallengeResponse response,
        ReadOnlySpan<byte> verificationKey)
    {
        try
        {
            byte[] signature = Convert.FromBase64String(response.SignatureBase64);
            byte[] expected = HMACSHA256.HashData(
                verificationKey,
                Encoding.UTF8.GetBytes(CreateSignedContent(challenge)));
            return CryptographicOperations.FixedTimeEquals(signature, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string CreateSignedContent(RecoveryChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return string.Join(
            '.',
            challenge.ChallengeId,
            challenge.DeviceId,
            challenge.NonceBase64,
            challenge.Purpose,
            challenge.PayloadHashBase64);
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        DateTimeOffset utcNow = now.ToUniversalTime();
        foreach (string challengeId in _activeChallenges
                     .Where(item => item.Value.ExpiresAtUtc <= utcNow)
                     .Select(item => item.Key)
                     .ToList())
        {
            _activeChallenges.Remove(challengeId);
        }
    }
}
