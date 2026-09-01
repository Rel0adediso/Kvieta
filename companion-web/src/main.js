import { p256 } from "@noble/curves/nist.js";
import { sha256 } from "@noble/hashes/sha2.js";

const config = window.__KVIETA_CONFIG__;
const stateKey = "kvieta-manager-device-v1";
const textEncoder = new TextEncoder();

const $ = (selector) => document.querySelector(selector);
const encodeField = (value) => btoa(String.fromCharCode(...textEncoder.encode(value)));
const toBase64 = (bytes) => btoa(String.fromCharCode(...bytes));
const fromBase64 = (value) => Uint8Array.from(atob(value), (character) => character.charCodeAt(0));
const nowIso = () => new Date().toISOString();
const unixSeconds = (value) => Math.floor(new Date(value).getTime() / 1000).toString();

function randomUuid() {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map((value) => value.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

function loadState() {
  try {
    return JSON.parse(localStorage.getItem(stateKey));
  } catch {
    return null;
  }
}

function saveState(value) {
  localStorage.setItem(stateKey, JSON.stringify(value));
}

function friendlyDeviceName() {
  const userAgent = navigator.userAgent || "";
  const platform = navigator.userAgentData?.platform || navigator.platform || "";
  if (/iPad/i.test(userAgent) || (/Macintosh/i.test(userAgent) && navigator.maxTouchPoints > 1)) {
    return "iPad";
  }
  if (/iPhone|iPod/i.test(userAgent)) return "iPhone";
  if (/Android/i.test(userAgent) || /Linux arm/i.test(platform)) return "Android telefon";
  if (/Windows Phone/i.test(userAgent)) return "Windows telefon";
  if (/Windows/i.test(platform)) return "Windows cihaz";
  if (/Mac/i.test(platform)) return "Apple cihaz";
  return platform || "Telefon tarayıcısı";
}

function getOrCreateIdentity() {
  const existing = loadState();
  if (existing?.privateKeyBase64 && existing?.deviceId) return existing;
  const privateKey = p256.utils.randomSecretKey();
  const identity = {
    deviceId: randomUuid(),
    privateKeyBase64: toBase64(privateKey),
  };
  saveState(identity);
  return identity;
}

function publicKeyPem(privateKey) {
  const raw = p256.getPublicKey(privateKey, false);
  const prefix = Uint8Array.from([
    0x30, 0x59, 0x30, 0x13, 0x06, 0x07, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02,
    0x01, 0x06, 0x08, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x03, 0x01, 0x07, 0x03,
    0x42, 0x00,
  ]);
  const der = new Uint8Array(prefix.length + raw.length);
  der.set(prefix);
  der.set(raw, prefix.length);
  const base64 = toBase64(der);
  return `-----BEGIN PUBLIC KEY-----\n${base64.match(/.{1,64}/g).join("\n")}\n-----END PUBLIC KEY-----`;
}

function integerDer(bytes) {
  let index = 0;
  while (index < bytes.length - 1 && bytes[index] === 0) index++;
  let value = bytes.slice(index);
  if (value[0] & 0x80) value = Uint8Array.from([0, ...value]);
  return Uint8Array.from([0x02, value.length, ...value]);
}

function compactToDer(compact) {
  const r = integerDer(compact.slice(0, 32));
  const s = integerDer(compact.slice(32, 64));
  return Uint8Array.from([0x30, r.length + s.length, ...r, ...s]);
}

function signContent(content, privateKey) {
  const digest = sha256(textEncoder.encode(content));
  const signature = p256.sign(digest, privateKey, { prehash: false, format: "compact" });
  return toBase64(compactToDer(signature));
}

function recoverySignedContent(challenge) {
  return [
    "kvieta-manager-recovery-v1",
    encodeField(challenge.ChallengeId),
    encodeField(challenge.DeviceId),
    encodeField(unixSeconds(challenge.ExpiresAtUtc)),
    encodeField(challenge.NonceBase64),
    encodeField(challenge.Purpose || "generic"),
    encodeField(challenge.PayloadHashBase64 || ""),
  ].join(".");
}

function transferSignedContent(transfer) {
  return [
    "kvieta-manager-transfer-v1",
    encodeField(transfer.CurrentDeviceId),
    encodeField(transfer.NewDeviceId),
    encodeField(transfer.NewDevicePublicKeyHashBase64),
    encodeField(unixSeconds(transfer.ExpiresAtUtc)),
    encodeField(transfer.NonceBase64),
  ].join(".");
}

function verificationCode(content) {
  const digest = sha256(textEncoder.encode(content));
  const value = (((digest[0] << 24) >>> 0) + (digest[1] << 16) + (digest[2] << 8) + digest[3]) >>> 0;
  return String(value % 1_000_000).padStart(6, "0");
}

async function api(method, body) {
  const response = await fetch(config.apiUrl, {
    method,
    cache: "no-store",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) throw new Error(`Kvieta isteği reddetti (${response.status}).`);
  return response.json();
}

function setBusy(busy) {
  $("#approve").disabled = busy;
  $("#reject").disabled = busy;
}

function showResult(message, error = false) {
  $("#result").textContent = message;
  $("#result").className = error ? "result error" : "result success";
}

async function approveEnrollment(endpoint) {
  const identity = getOrCreateIdentity();
  const privateKey = fromBase64(identity.privateKeyBase64);
  const enrolledAtUtc = nowIso();
  const deviceName = friendlyDeviceName();
  const pem = publicKeyPem(privateKey);
  const enrollment = {
    DeviceId: identity.deviceId,
    DeviceName: deviceName.slice(0, 100),
    PublicKeyPem: pem,
    EnrolledAtUtc: enrolledAtUtc,
    RevokedAtUtc: null,
  };
  const proof = [
    "kvieta-manager-enrollment-v1",
    encodeField(enrollment.DeviceId),
    encodeField(enrollment.DeviceName),
    encodeField(enrollment.PublicKeyPem),
    encodeField(unixSeconds(enrolledAtUtc)),
  ].join(".");
  await api("POST", {
    Enrollment: enrollment,
    ProofSignatureBase64: signContent(proof, privateKey),
  });
}

async function approveRecovery(endpoint) {
  const identity = loadState();
  if (!identity?.privateKeyBase64 || !identity?.deviceId) {
    throw new Error("Bu tarayıcıda kayıtlı Kvieta yönetici anahtarı yok.");
  }
  const challenge = endpoint.challenge;
  if (challenge.DeviceId !== identity.deviceId) {
    throw new Error("Bu istek başka bir yönetici cihazına ait.");
  }
  const privateKey = fromBase64(identity.privateKeyBase64);
  const content = recoverySignedContent(challenge);
  await api("POST", {
    ChallengeId: challenge.ChallengeId,
    DeviceId: challenge.DeviceId,
    NonceBase64: challenge.NonceBase64,
    SignatureBase64: signContent(content, privateKey),
  });
}

async function proposeTransfer(endpoint) {
  const existing = loadState();
  if (existing?.deviceId === endpoint.currentDeviceId) {
    throw new Error("Önce yeni telefonda aç. Yeni telefon onayladıktan sonra bu kayıtlı telefonda aynı QR'ı tekrar tara.");
  }
  const identity = getOrCreateIdentity();
  const privateKey = fromBase64(identity.privateKeyBase64);
  const pem = publicKeyPem(privateKey);
  const replacement = {
    DeviceId: identity.deviceId,
    DeviceName: friendlyDeviceName().slice(0, 100),
    PublicKeyPem: pem,
    EnrolledAtUtc: nowIso(),
    RevokedAtUtc: null,
  };
  const transfer = {
    CurrentDeviceId: endpoint.currentDeviceId,
    NewDeviceId: identity.deviceId,
    NewDevicePublicKeyHashBase64: toBase64(sha256(textEncoder.encode(pem))),
    ExpiresAtUtc: endpoint.expiresAtUtc,
    NonceBase64: toBase64(crypto.getRandomValues(new Uint8Array(24))),
    CurrentDeviceSignatureBase64: "",
    NewDeviceSignatureBase64: "",
  };
  transfer.NewDeviceSignatureBase64 = signContent(transferSignedContent(transfer), privateKey);
  await api("POST", { Replacement: replacement, Transfer: transfer });
}

async function approveTransfer(endpoint) {
  const identity = loadState();
  const request = endpoint.request;
  if (!identity?.privateKeyBase64 || identity.deviceId !== request.Transfer.CurrentDeviceId) {
    throw new Error("Bu adım yalnız mevcut kayıtlı yönetici telefonunda onaylanabilir.");
  }
  const signature = signContent(
    transferSignedContent(request.Transfer),
    fromBase64(identity.privateKeyBase64),
  );
  await api("POST", { CurrentDeviceSignatureBase64: signature });
}

async function start() {
  try {
    const endpoint = await api("GET");
    const enrollment = endpoint.service === "kvieta-enrollment";
    const recovery = endpoint.service === "kvieta-recovery";
    const transferNew = endpoint.service === "kvieta-transfer-new";
    const transferCurrent = endpoint.service === "kvieta-transfer-current";
    if (!enrollment && !recovery && !transferNew && !transferCurrent) throw new Error("Geçersiz Kvieta endpoint'i.");
    const code = recovery
      ? verificationCode(recoverySignedContent(endpoint.challenge))
      : endpoint.verificationCode;
    $("#title").textContent = enrollment
      ? "Yönetici telefonunu eşleştir"
      : recovery
        ? "PIN sıfırlamayı onayla"
        : transferNew
          ? "Yeni yönetici telefonu"
          : "Aktarımı tamamla";
    $("#description").textContent = enrollment
      ? "Bu tarayıcı bu bilgisayar için cihaz-yerel bir yönetici anahtarı oluşturacak."
      : recovery
        ? "Bu onay yalnız bilgisayarda seçilen yeni PIN kimliğine bağlıdır."
        : transferNew
          ? "Yeni telefon anahtarını oluştur. Ardından aynı QR'ı eski kayıtlı telefonla tekrar tara."
          : "Yeni telefonu devralan cihaz olarak yetkilendirmek için eski yönetici anahtarıyla imzala.";
    $("#code").textContent = code;
    $("#approve").textContent = enrollment
      ? "Kod eşleşiyor · Eşleştir"
      : transferNew
        ? "Kod eşleşiyor · Yeni telefonu hazırla"
        : "Kod eşleşiyor · Onayla";
    $("#approve").onclick = async () => {
      setBusy(true);
      try {
        if (enrollment) await approveEnrollment(endpoint);
        else if (recovery) await approveRecovery(endpoint);
        else if (transferNew) await proposeTransfer(endpoint);
        else await approveTransfer(endpoint);
        showResult(enrollment
          ? "Telefon başarıyla eşleştirildi."
          : recovery
            ? "PIN sıfırlama onaylandı."
            : transferNew
              ? "Yeni telefon hazır. Şimdi aynı QR'ı eski kayıtlı telefonla tara."
              : "Yönetici cihazı aktarımı tamamlandı.");
      } catch (error) {
        showResult(error.message || "İşlem tamamlanamadı.", true);
        setBusy(false);
      }
    };
    $("#reject").onclick = () => {
      showResult("İstek reddedildi. Bu sayfayı kapatabilirsin.", true);
      setBusy(true);
    };
    $("#app").hidden = false;
    $("#loading").hidden = true;
  } catch (error) {
    $("#loading").textContent = error.message || "Kvieta bilgisayarına bağlanılamadı.";
    $("#loading").className = "loading error";
  }
}

start();
