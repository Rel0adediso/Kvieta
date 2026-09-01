<div align="center">

<img src="../assets/branding/kvieta-mark.svg" alt="Kvieta logosu" width="132" />

# Kvieta

### Her şeyin bir zamanı var.

Windows'ta ekran süresini anlamanın ve yönetmenin sakin, yerel yolu.

[**English**](../README.md)

![Windows](https://img.shields.io/badge/Windows-yerel-87946B?style=flat-square&labelColor=292B26)
![.NET](https://img.shields.io/badge/.NET-10-87946B?style=flat-square&labelColor=292B26)
![WPF](https://img.shields.io/badge/UI-WPF-C9B98E?style=flat-square&labelColor=292B26)
![Privacy](https://img.shields.io/badge/gizlilik-yerel--öncelikli-87946B?style=flat-square&labelColor=292B26)
![Status](https://img.shields.io/badge/durum-Alpha_1_Hotfix_2-C9B98E?style=flat-square&labelColor=292B26)
![License](https://img.shields.io/badge/lisans-MIT-87946B?style=flat-square&labelColor=292B26)

</div>

Kvieta, bilgisayar kullanımını cezaya çevirmeden zamanı görünür ve bilinçli hale getirir. Planlar, kurallar, kullanım geçmişi, kimlik bilgileri ve kurtarma verileri Windows cihazında kalır. Kvieta hesabı gerekmez.

## Kvieta Alpha 1 Hotfix 2'yi indir

[**Windows x64 için Kvieta Setup'ı indir**](https://github.com/Rel0adediso/Kvieta/releases/download/alpha-1-hotfix-2/Kvieta-Setup-Alpha-1-Hotfix-2.exe)

Self-contained kurucu Türkçe ve English destekler; .NET SDK gerektirmez. Bu
community preview bilerek imzasızdır, bu nedenle Windows SmartScreen
**Bilinmeyen yayıncı** uyarısı gösterebilir.

```text
SHA-256: 0c9a974072929e47369efdd951bdc42341a836814aa447f48d8299fbf70e5f72
```

Bağımsız MSI, checksum dosyaları, release manifesti, ayrıntılı notlar ve bilinen
sınırlar [Kvieta Alpha 1 Hotfix 2 yayın sayfasında](https://github.com/Rel0adediso/Kvieta/releases/tag/alpha-1-hotfix-2) bulunur.

> **Önemli:** Hotfix 2, Hotfix 1'deki Guardian PIN düzeltmesini içerir ve yönetici pencerelerini korumalı oturum yüzeyinin üzerinde görünür tutar. Herhangi bir eski Alpha 1 paketinin üzerine kurulabilir; ayarlar ve korunan policy korunur.

## Zamanla nasıl bir ilişki kuracağını seç

| Biçim | Kime göre? | Deneyim |
|---|---|---|
| **Sadece takip** | Alışkanlıklarını anlamak isteyenlere | Yapılandırılan uygulamaların kullanımını yalnızca cihazda kaydeder; hiçbir kısıtlama uygulamaz. |
| **Kendim için** | Kendi düzenini kurmak isteyenlere | Plan, limit, mola, odak oturumu ve isteğe bağlı kişisel koruma ekler. |
| **Yönettiğim biri için** | Ayrı bir yöneticinin yönettiği standart Windows hesabına | Kuralları yönetici PIN'i ve Kvieta Guardian servisiyle korur. |

## Kvieta neler yapar?

| | |
|---|---|
| **Zamanı planlar** | Haftalık planlar, günlük limitler, kontrollü molalar, geçici izinler ve yönetici onaylı ek süre. |
| **Uygulamaları yönetir** | Günlük sayaç ve dayanıklı süreç tanımayla engelli, süreli veya serbest uygulama kuralları. |
| **Ritmi gösterir** | Yedi günlük içgörü, 90 günlük yerel geçmiş, haftalık toplam, günlük ortalama ve aktivite olayları. |
| **Kurtarılabilir kalır** | Çevrimdışı kurtarma kodları; isteğe bağlı güvenilir telefon, QR aktarımı, iptal ve yerel PIN sıfırlama onayı. |
| **Kuralları korur** | Guardian gözetimi, korumalı policy alanı, sağlık kontrolleri, onarım yolları ve doğrulanmış yönetici çıkışı. |
| **Gerçek hayata dayanır** | Atomik kayıt, son sağlam yedek, bozulma kurtarması, saat geri alma algısı ve eşzamanlı yazma koruması. |

## Kvieta Alpha 1 preview ile gelenler

- Uygulama, kurucu, Windows ikonu, tray ve belgelerin tamamında yeni **Kvieta** kimliği.
- Uygulama ve kurucuda net vektör logo; EXE için çok çözünürlüklü Windows ikonu.
- Yerel ağda kısa ömürlü, imzalı isteklerle çalışan isteğe bağlı **güvenilir telefon kurtarması**; PIN ve kurtarma kodları bilgisayardan çıkmaz.
- QR ile güvenilir cihaz eşleştirme ve aktarma, tek aktif cihaz, açık iptal, karşılaştırma kodu, süre sonu ve tekrar oynatma koruması.
- Başlatma, onarım, policy aktarımı, yönetici çıkışı ve yinelenen pencere engelini kapsayan daha sağlam Guardian yaşam döngüsü.
- Paket ve manifest doğrulamalı daha güvenli kurulum, güncelleme, onarım, yeniden yapılandırma, rollback ve kaldırma akışları.
- Oturum davranışı, uygulama kuralları, Windows kilidi geçişleri, güç eylemleri, odak yönetimi ve iki dilli hata durumlarında düzeltmeler.

## Gizlilik tasarımın parçası

Kvieta zorunlu bulut hesabı kullanmaz ve ekran süresi geçmişini bir Kvieta servisine göndermez. İsteğe bağlı telefon yardımcısı bilgisayar tarafından sunulur ve şu anda telefonun bilgisayara yerel ağdan erişebilmesini gerektirir. Onay mesajları imzalı, kısa ömürlü, origin kontrollü ve hız sınırlıdır; yönetici PIN'ini veya kurtarma kodlarını içermez.

## Projenin durumu

**Kvieta Alpha 1 Hotfix 2 güncel community preview'dur.**

- Kaynak kod bugün çalıştırılabilir; Windows paket hattı hazırdır.
- Debug ve Release derlemeleri, smoke testleri, belge kontrolleri ve public-build bypass kontrolleri kalite kapısı olarak çalışır.
- Community kurucular bilerek imzasızdır; Windows SmartScreen **Bilinmeyen yayıncı** uyarısı gösterebilir.
- Yayımlanan Setup EXE, MSI, checksum ve manifest `56c2462` kaynak commit'ine bağlıdır.
- Geniş kurucu, DPI, Guardian, kaçış yolu ve Windows yaşam döngüsü matrisi final `v1.0.0` öncesinde açıktır.

Kalan doğrulamalar için [yol haritasına](ROADMAP.md), ayrıntılı geçmiş için [sürüm notlarına](RELEASE_NOTES.md) bakın.

## Kaynaktan çalıştırma

Gereksinim: Windows ve .NET 10 SDK.

```powershell
dotnet run --project src/Kvieta.App/Kvieta.App.csproj
```

Oturum yüzünü doğrudan açmak için:

```powershell
dotnet run --project src/Kvieta.App/Kvieta.App.csproj -- --session
```

Ana kalite kontrolleri:

```powershell
dotnet build Kvieta.slnx -c Release
dotnet run --project tests/Kvieta.Core.SmokeTests/Kvieta.Core.SmokeTests.csproj -c Release
```

<details>
<summary><strong>Proje yapısı</strong></summary>

| Parça | Sorumluluk |
|---|---|
| `Kvieta.Core` | Plan, oturum, policy, model ve dayanıklı yerel veri katmanı |
| `Kvieta.App` | WPF Kontrol Merkezi, oturum yüzü, tray ve Windows entegrasyonları |
| Guardian servisi | Korumalı oturum ve otoriter policy alanını gözeten Windows servisi |
| `Kvieta.SetupApp` | İki dilli kurulum, güncelleme, onarım, yapılandırma ve kaldırma deneyimi |
| `Kvieta.Core.SmokeTests` | Çekirdek davranış, güvenlik regresyonu ve gerçek süreç testleri |

</details>

## Güvenlik sınırı

Korumalı mod esas olarak ayrı bir yönetici hesabı tarafından yönetilen **standart Windows kullanıcısı** için tasarlanır. Fiziksel erişimi ve Windows yönetici yetkisi bulunan birine karşı hiçbir masaüstü uygulaması mutlak direnç garanti edemez. Development test geçitleri Public build'lere derlenmez. Korumalı moda güvenmeden önce [güvenlik modelini ve sınırlarını](SECURITY.tr.md) okuyun.

## Belgeler

- [Türkçe kullanım rehberi](KULLANIM.tr.md) · [English user guide](USAGE.md)
- [Yol haritası](ROADMAP.md) · [Sürüm notları](RELEASE_NOTES.md)
- [Destek](../.github/SUPPORT.md) · [Katkıda bulunma](../.github/CONTRIBUTING.md)

## Geliştirme yaklaşımı

**İnsan tarafından yönlendirilen ürün · AI destekli geliştirme.** Ürün yönü, UX kararları ve gerçek kullanım testleri [Rel0adediso](https://github.com/Rel0adediso) tarafından yürütülür. Mimari, uygulama ve test geliştirme süreci OpenAI Codex ile iş birliği içinde ilerler.

Kvieta, [MIT Lisansı](../LICENSE) altında yayımlanan açık kaynak bir yazılımdır.

---

<div align="center">

**Kvieta** · *Her şeyin bir zamanı var.*

</div>
