namespace Kvieta.Core.Models;

public sealed record ManagerDeviceTransferRequest(
    ManagerDeviceEnrollment Replacement,
    ManagerDeviceTransfer Transfer);
