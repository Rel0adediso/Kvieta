<div align="center">

[English](README.md) · **Türkçe**

# Otium

### Her şeyin bir zamanı var.

Yerel, sakin ve hesap gerektirmeyen Windows ekran süresi yönetimi.

![Windows](https://img.shields.io/badge/Windows-yerel-87946B?style=flat-square&labelColor=3F4437)
![.NET](https://img.shields.io/badge/.NET-10-87946B?style=flat-square&labelColor=3F4437)
![WPF](https://img.shields.io/badge/UI-WPF-C9B98E?style=flat-square&labelColor=3F4437)
![Privacy](https://img.shields.io/badge/veri-bu%20cihazda-87946B?style=flat-square&labelColor=3F4437)
![Status](https://img.shields.io/badge/sürüm-v0.15.1-C9B98E?style=flat-square&labelColor=3F4437)
![AI assisted](https://img.shields.io/badge/geliştirme-AI%20destekli-87946B?style=flat-square&labelColor=3F4437)

</div>

> Otium, bilgisayar kullanımını cezalandırmak yerine zamanı görünür ve yönetilebilir hale getirir. Bulut hesabı istemez; planlar, kurallar ve kullanım geçmişi cihazda kalır.

## Neden Otium?

Otium aynı çekirdeği iki farklı kullanım biçiminde sunar:

| Kendim için | Yönettiğim biri için |
|---|---|
| Ani kararlarla kuralları gevşetmeye karşı gecikme uygular. | Yönetici PIN'i ve Guardian servisiyle kuralları korur. |
| PIN gerektirmeden kişinin kendi düzenini kurmasına yardım eder. | Standart Windows kullanıcısının basit kaçışlarına direnç gösterir. |
| Bilgisayar kullanılırken sayaç arka planda devam eder. | Zorla kapatılan oturum yüzünü yeniden ayağa kaldırabilir. |

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
- Gelecek sürümlerde kişisel gelişim, geri kazanılan zaman ve aktif uygulama farkındalığı

### Uygulama kuralları

- Engelli, süreli ve serbest uygulamalar
- Süreli uygulamalarda günlük sayaç ve limitte sonlandırma
- Sınırsız uygulamalarda farkındalık amaçlı kullanım kaydı
- Gelecek sürümlerde publisher, original filename, hash ve launcher/child-process tanıma

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

**v0.15.1 prototip**

- Release build: `0` uyarı, `0` hata
- Tek dosyalık, self-contained Windows paketi
- Türkçe ve English kaynakları eşleşiyor
- Veri kurtarma, migration, dosya kilidi ve eşzamanlı yazma smoke testleri mevcut

Geliştirme sırası ve v1.0 hedefleri için [güncel yol haritasına](ROADMAP.md) bakın.

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

Development paketlerindeki test geçitleri public sürüm hedefi değildir. Yol haritasında test ve public build'lerin tamamen ayrılması planlanmıştır.

## Geliştirme yaklaşımı

**AI-assisted development · Human-directed product.**

Ürün fikri, yönü, UX kararları ve gerçek kullanım testleri **Rel0adediso** tarafından yürütülür. Mimari, uygulama ve test geliştirme süreci **OpenAI Codex ile iş birliği içinde** ilerler.

Bu nedenle projeyi “%100 AI made” diye tanımlamak yerine, insan tarafından yönlendirilen ve AI destekli geliştirilen bir ürün olarak açıkça belgeliyoruz.

---

<div align="center">

**Otium** · *All in good time.*

</div>
