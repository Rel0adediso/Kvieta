namespace Kvieta.Core.Models;

public sealed record RecoveryChallenge(
    string ChallengeId,
    string DeviceId,
    DateTimeOffset ExpiresAtUtc,
    string NonceBase64,
    string Purpose = "generic",
    string PayloadHashBase64 = "");

public sealed record RecoveryChallengeResponse(
    string ChallengeId,
    string DeviceId,
    string NonceBase64,
    string SignatureBase64);
