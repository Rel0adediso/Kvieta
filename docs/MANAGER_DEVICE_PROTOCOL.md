# Otium manager-device protocol v1

The protocol is local-network only. The desktop app serves an embedded companion page from a fixed local origin and an unguessable 192-bit route. Pairing and recovery routes expire after two minutes; transfer routes expire after five. Final authorization requests are one-shot and the server never redirects. QR codes contain the explicit RFC1918 IPv4 `http://` page address, so no APK or custom URL scheme is required.

The page is delivered over local HTTP because phones cannot trust an ad-hoc desktop TLS certificate. Its Content Security Policy blocks external resources, and the six-digit comparison code must be checked on both screens to confirm that the intended session was opened. It does not prevent an active local-network man-in-the-middle from replacing HTTP content. The browser identity is stored only in that browser profile. Clearing site data, using private browsing, changing the computer's LAN address, or opening another browser profile loses access to that identity and requires administrator-assisted re-enrollment. Local HTTP cannot provide the hardware-backed, non-exportable key guarantees of a native phone application.

## Common encoding

Signed content is UTF-8. Every variable field is encoded with standard padded Base64 of its UTF-8 bytes, then fields are joined with `.`. Timestamps inside signed content are Unix seconds in invariant decimal form. ECDSA uses P-256, SHA-256 and an ASN.1 DER `(r,s)` signature.

The six-digit comparison code is `UInt32BigEndian(SHA256(signedContent)[0..4]) % 1_000_000`, zero-padded to six digits.

Recovery test vector:

```text
otium-manager-recovery-v1.QUJD.ZGV2aWNlLTE=.MTcwMDAwMDEyMw==.QVFJREJBPT0=.cGluLXJlc2V0.WVdKalpBPT0=
```

Comparison code: `660957`.

## Enrollment

The companion page creates a P-256 key with the browser's cryptographic random source, stores it in local browser storage for the fixed Otium origin, and posts `ManagerDeviceEnrollmentRequest`. `ProofSignatureBase64` signs:

```text
otium-manager-enrollment-v1
  .B64(DeviceId)
  .B64(DeviceName)
  .B64(PublicKeyPem)
  .B64(EnrolledAtUtc Unix seconds)
```

Enrollment requires the current administrator PIN, Windows administrator confirmation, a matching six-digit code, and an enrollment timestamp within ten minutes. An active enrollment cannot be overwritten.

## PIN recovery

The challenge signature covers:

```text
otium-manager-recovery-v1
  .B64(ChallengeId)
  .B64(DeviceId)
  .B64(ExpiresAtUtc Unix seconds)
  .B64(NonceBase64)
  .B64(Purpose)
  .B64(PayloadHashBase64)
```

For PIN reset, `Purpose` is `pin-reset`. `PayloadHashBase64` binds the approval to the exact salted administrator credential. Guardian independently verifies the enrolled key, purpose, credential binding, expiry, device identity and persistent replay state before changing protected policy.

## Device transfer

Both the current and replacement device sign:

```text
otium-manager-transfer-v1
  .B64(CurrentDeviceId)
  .B64(NewDeviceId)
  .B64(NewDevicePublicKeyHashBase64)
  .B64(ExpiresAtUtc Unix seconds)
  .B64(NonceBase64)
```

The public-key hash is Base64(SHA256(UTF8(PublicKeyPem))). The new phone signs first; the same QR is then opened on the currently enrolled phone for the second signature. Transfers expire within ten minutes. A successful atomic replacement makes the same transfer unusable again because the stored current device changes.
