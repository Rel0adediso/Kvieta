namespace Otium.Core.Models;

public sealed record ManagerDeviceEnrollmentRequest(
    ManagerDeviceEnrollment Enrollment,
    string ProofSignatureBase64);
