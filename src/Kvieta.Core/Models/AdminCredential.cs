namespace Kvieta.Core.Models;

public sealed class AdminCredential
{
    public const int PublicMarkerVersion = 2;
    public int Version { get; set; } = 1;
    public int Iterations { get; set; } = 210_000;
    public string SaltBase64 { get; set; } = string.Empty;
    public string HashBase64 { get; set; } = string.Empty;

    public bool IsPublicMarker => Version == PublicMarkerVersion;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SaltBase64)
        && !string.IsNullOrWhiteSpace(HashBase64)
        && Iterations >= 100_000;
}
