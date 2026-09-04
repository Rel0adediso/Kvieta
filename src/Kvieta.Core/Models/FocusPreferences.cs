namespace Kvieta.Core.Models;

public sealed class FocusPreferences
{
    public int SchemaVersion { get; set; } = 1;
    public int LastDurationMinutes { get; set; } = 25;
}
