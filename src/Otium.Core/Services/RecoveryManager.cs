using Otium.Core.Models;

namespace Otium.Core.Services;

public sealed class RecoveryManager(JsonSettingsStore settingsStore, SecurityAuditLog auditLog)
{
    public async Task<bool> TryConsumeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        bool consumed = false;
        await settingsStore.UpdateAsync(settings =>
        {
            consumed = RecoveryCodeService.TryConsume(settings, code);
            return settings;
        }, cancellationToken);
        await auditLog.AppendAsync("recovery.code", consumed ? "accepted" : "rejected", cancellationToken);
        return consumed;
    }

    public async Task<bool> TryResetPinAsync(
        string code,
        AdminCredential newCredential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newCredential);
        if (!newCredential.IsConfigured)
        {
            return false;
        }

        bool reset = false;
        await settingsStore.UpdateAsync(settings =>
        {
            if (!RecoveryCodeService.TryConsume(settings, code))
            {
                return settings;
            }

            settings.AdminPin = newCredential;
            reset = true;
            return settings;
        }, cancellationToken);
        await auditLog.AppendAsync("recovery.pin-reset", reset ? "accepted" : "rejected", cancellationToken);
        return reset;
    }
}
