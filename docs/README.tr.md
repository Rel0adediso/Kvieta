<div align="center">

[English](../README.md) · **Türkçe**

# Otium

### Her şeyin bir zamanı var.

Yerel, sakin ve hesap gerektirmeyen Windows ekran süresi yönetimi.

![Windows](https://img.shields.io/badge/Windows-yerel-87946B?style=flat-square&labelColor=3F4437)
![.NET](https://img.shields.io/badge/.NET-10-87946B?style=flat-square&labelColor=3F4437)
![WPF](https://img.shields.io/badge/UI-WPF-C9B98E?style=flat-square&labelColor=3F4437)
![Privacy](https://img.shields.io/badge/veri-bu%20cihazda-87946B?style=flat-square&labelColor=3F4437)
![Status](https://img.shields.io/badge/sıradaki-Otium_Alpha_2-C9B98E?style=flat-square&labelColor=3F4437)
![AI assisted](https://img.shields.io/badge/geliştirme-AI%20destekli-87946B?style=flat-square&labelColor=3F4437)

</div>

> Otium, bilgisayar kullanımını cezalandırmak yerine zamanı görünür ve yönetilebilir hale getirir. Bulut hesabı istemez; planlar, kurallar ve kullanım geçmişi cihazda kalır.

## Otium Alpha 2

**Otium Alpha 2 sıradaki community prerelease olarak hazırlanıyor.** Bu yayın;
güvenilir telefonla kurtarma akışı, çok daha sağlam Guardian ve yönetici çıkışı
yaşam döngüsü, daha güvenli onarım/güncelleme davranışı ile çok sayıda kurucu ve
oturum ekranı düzeltmesi getiriyor. Kullanıcıya görünen yayın adı **Otium Alpha
2** olacak; dosya ve etiketlerde güvenli olması için `alpha-2` / `Alpha-2`
kullanılacak, yayın `v1.0.0-alpha.2` adıyla sunulmayacak.

Alpha 2 paketi ve checksum'ı eşleşen temiz release commit'inden yayımlanana kadar
Alpha.1 indirilebilir en güncel paket olarak kalır.

## Alpha.1'i indir

[**Windows x64 için Otium Setup'ı indir**](https://github.com/Rel0adediso/Otium/releases/download/v1.0.0-alpha.1/Otium-Setup-1.0.0-alpha.1.exe)

`v1.0.0-alpha.1`, gerçek cihaz testleri için hazırlanan ilk community
prerelease'tir. Kurucu self-contained çalışır, Türkçe ve English destekler ve
.NET SDK gerektirmez. Paket bilerek imzalanmamıştır; bu nedenle Windows
SmartScreen **Bilinmeyen yayıncı** uyarısı gösterebilir.

Setup EXE'yi çalıştırmadan önce doğrulayın:

```text
SHA-256: 8736c995fc3e7add6020c30fdc2fbf2d10d1aaaf1786cd64d9b10acfb3d76260
```

Bağımsız MSI, checksum dosyaları, release manifesti, ayrıntılı notlar ve bilinen
sınırlar
[v1.0.0-alpha.1 yayın sayfasında](https://github.com/Rel0adediso/Otium/releases/tag/v1.0.0-alpha.1) bulunur.

## Neden Otium?

Otium aynı yerel temeli üç kullanım biçiminde sunar:

| Biçim | Kime uygun? | Ne yapar? |
|---|---|---|
| **Sadece takip** | Bilgisayar alışkanlıklarını anlamak isteyenlere | Yapılandırılan uygulamaların kullanımını yalnızca cihazda ölçer; kısıtlama uygulamaz. |
| **Kendim için** | Kendi düzenini kurmak isteyenlere | Plan, limit, mola ve isteğe bağlı Balanced oturum yüzeyi sunar; yönetici PIN'i gerektirmez. |
| **Yönettiğim biri için** | Ayrı bir yöneticinin yönettiği standart Windows hesabına | Kuralları yönetici PIN'i ve Guardian ile korur; yaygın sonlandırma girişimlerinden sonra korunan oturumu geri getirir. |

## Öne çıkanlar

### Zaman ve plan

- Haftanın günlerine göre saat aralığı ve günlük kullanım limiti
- Win+L sonrasında otomatik devam yerine kontrollü **Mola** durumu
- Ana haftalık planı bozmayan tarihli geçici izinler
- Yönetici onayıyla yalnız bugüne ek süre
- Film veya uzun içerik sırasında mouse hareketine bağlı olmayan sayaç

### Ritim ve geçmiş

- Son yedi gün için günlük ve haftalık kullanım görünümü
- Haftalık toplam, günlük ortalama ve en çok kullanılan uygulama
- Mola, limit dolması, ek süre ve kural değişikliği hareketleri
- 90 günlük, yalnızca cihazda saklanan geçmiş
- Kişisel gelişim, geri kazanılan zaman ve isteğe bağlı aktif uygulama farkındalığı

### Uygulama kuralları

- Engelli, süreli ve serbest uygulamalar
- Süreli uygulamalarda günlük sayaç ve limitte sonlandırma
- Sınırsız uygulamalarda farkındalık amaçlı kullanım kaydı
- Publisher, original filename, isteğe bağlı hash, package family ve launcher/child-process tanıma

### Güvenlik ve veri sağlamlığı

- PBKDF2 ile tuzlanmış yönetici PIN doğrulaması
- Korumalı kurallar için servis tarafından yönetilen otoriter kopya
- Başarısız PIN denemelerinde artan bekleme
- Beş dakikadan büyük saat geri alma girişimini algılama
- Süreçler arası veri dosyası kilidi
- Doğrulanmış atomik JSON kaydı ve son sağlam `.bak` kopyası
- Bozuk ana dosyayı otomatik kurtarma
- Eşzamanlı sayaç ve geçmiş olaylarını kayıpsız birleştirme

## Tasarım dili

Otium, sıcak krem ve haki tonlarını zeytin-grafit koyu temayla birleştiren kompakt bir Windows arayüzü kullanır.

- Sistem / Açık / Koyu tema
- Canlı Türkçe / English değişimi
- İnce özel pencere çubuğu
- Açılır-kapanır ve optik olarak hizalanmış navigasyon
- Kompakt yedi günlük plan
- Tema uyumlu özel tray menüsü
- Bilgi yoğun ama sakin ekranlar; gereksiz büyük dashboard kartları yok

## Proje yapısı

| Parça | Sorumluluk |
|---|---|
| `Otium.Core` | Plan, oturum, policy, model ve dayanıklı yerel veri katmanı |
| `Otium.App` | WPF Kontrol Merkezi, oturum yüzü, tray ve Windows entegrasyonları |
| `Otium.Core.SmokeTests` | Çekirdek davranış, güvenlik regresyonu ve gerçek süreç testleri |
| Guardian | Korumalı modda oturum sürecini ve otoriter policy alanını gözeten Windows servisi |

Solution, proje, assembly ve namespace adları tutarlı biçimde **Otium** adını kullanır.

## Güncel durum

**Otium Alpha 2 — release candidate hazırlığı**

- Release community paketi development/test bypass içermiyor.
- Yerel kalite kapıları ve eşleşen GitHub Actions build/package işleri geçiyor.
- Yayındaki Alpha.1 Setup EXE, MSI, checksum ve release manifesti `4dd94ca` kaynak commit'ine bağlı.
- Alpha 2; kurulum, güvenilir telefon, Guardian, yönetici çıkışı, oturum, onarım ve güç eylemleri için gerçek kullanım testlerini geçti; temiz commit paket kapısı kaldı.
- Ayar migration'ı, recovery, Guardian iletişimi, uygulama kuralları ve oturum davranışı otomatik regresyon testlerine sahip.
- Temel korumalı yüzey davranışı iki fiziksel monitör testini geçti.
- Temiz kurulum, Guardian enrollment, süre dolması, repair ve uninstall ayrı bir Windows cihazında doğrulanıyor.
- Geniş Windows yaşam döngüsü, kaçış yolları, DPI ve installer matrisi final `v1.0.0` öncesinde açık.

Kalan v1.0 doğrulamaları ve sonraki hedefler için [güncel yol haritasına](ROADMAP.md), sürümler arasındaki farklar için [sürüm notlarına](RELEASE_NOTES.md) bakın.

## Kaynaktan çalıştırma

Gereksinim: Windows ve .NET 10 SDK.

```powershell
dotnet run --project src/Otium.App/Otium.App.csproj
```

Korumalı oturum yüzünü doğrudan açmak için:

```powershell
dotnet run --project src/Otium.App/Otium.App.csproj -- --session
```

## Kontroller

```powershell
dotnet build Otium.slnx -c Release
dotnet run --project tests/Otium.Core.SmokeTests/Otium.Core.SmokeTests.csproj -c Release
```

## Güvenlik sınırı

Otium'un korumalı modu esas olarak ayrı bir yönetici hesabı tarafından yönetilen **standart Windows kullanıcısı** için tasarlanır. Fiziksel erişimi ve Windows yönetici yetkisi bulunan bir kişiye karşı hiçbir masaüstü uygulaması mutlak kaldırılamazlık garanti edemez.

Development paketlerindeki test geçitleri public sürümde derlenmez. Ayrıntılar için [güvenlik sınırları belgesine](SECURITY.tr.md) bakın.

Kurulum, ilk kullanım, güncelleme, kurtarma ve kaldırma adımları için [Türkçe kullanım rehberine](KULLANIM.tr.md) bakın. Yardım veya hata bildirimi için [Destek](../.github/SUPPORT.md) belgesini kullanın.

## Geliştirme yaklaşımı

**AI-assisted development · Human-directed product.**

Ürün fikri, yönü, UX kararları ve gerçek kullanım testleri **Rel0adediso** tarafından yürütülür. Mimari, uygulama ve test geliştirme süreci **OpenAI Codex ile iş birliği içinde** ilerler.

Bu nedenle projeyi “%100 AI made” diye tanımlamak yerine, insan tarafından yönlendirilen ve AI destekli geliştirilen bir ürün olarak açıkça belgeliyoruz.

## Lisans

Otium, [MIT Lisansı](../LICENSE) altında yayımlanan açık kaynak bir yazılımdır. Güvenlik sınırları ve platform kısıtları geçerliliğini korur; lisans, Protected modun hiçbir koşulda aşılamayacağı anlamına gelmez.

---

<div align="center">

**Otium** · *All in good time.*

</div>
