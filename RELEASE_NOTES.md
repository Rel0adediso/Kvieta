# Otium sürüm notları

## v1.0.0-rc.1 — Kullanım biçimleri ve Guardian RC

Bu sürüm final değildir. İmzalı MSI, açıklamalı kurulum sihirbazı ve gerçek Windows yaşam döngüsü matrisi tamamlandıktan sonra `v1.0.0` yayımlanacaktır.

### Neler değişti?

- Kullanım biçimleri `Sadece takip`, `Kendim için` ve `Yönettiğim biri için` olarak ayrıldı.
- Kişisel kullanım için `Esnek`, `Dengeli` ve `Sıkı · Guardian` seviyeleri eklendi.
- Esnek mod haftalık plandan ayrılarak manuel odak oturumuna dönüştürüldü.
- Esnek oturuma sıfırdan başlayan, molada duran kronometre eklendi.
- Sıkı kişisel moddan çıkış kullanıcı PIN'i yerine gecikmeli politika değişikliğine bağlandı.
- Guardian, gecikme dolduğunda uygulama kapalı olsa bile kişisel gevşetmeyi uygulayacak şekilde güncellendi.
- Oturum ekranı ve Kontrol Merkezi arasında çift pencere oluşturan yarış durumları kapatıldı.
- Sadece takip başlangıcındaki görünmez pencere çökmesi ve eski `Farkındalık` etiketi düzeltildi.
- Kurtarma, Guardian sağlığı, tanılama ve korumalı yönetici çıkışı akışları güçlendirildi.
- Uygulama önerileri kararsız olduğu için mevcut sürümden çıkarılıp v1 sonrasına taşındı.

### Bilinen açık sorunlar

- Dengeli kişisel modda Otium kapatıldıktan sonra açılan oturum yüzeyi küçültülerek veya alta atılarak masaüstüne geçilebiliyor. Bu sorun v1.0 final öncesi release blocker'dır.
- Public paket henüz imzalı değildir ve açıklamalı MSI kurulum sihirbazı tamamlanmamıştır.
- Reboot, Win+L, uyku/hibernation, Explorer restart, çoklu monitör ve standart kullanıcı matrisi tamamlanmamıştır.

## v0.19.0 — Tanılama ve Guardian sağlamlığı

- Guardian sağlık ve sürüm uyumluluğu kontrolleri eklendi.
- Gizlilik korumalı tanılama dışa aktarımı eklendi.
- Guardian kurulum ve korunan politika kurtarma akışları güçlendirildi.
- Test kilidi kaldırma yolu yalnız Development derlemelerine sınırlandı.

## v0.18.0 — Kurtarma ve güvenlik sertleştirmesi

- Kurtarma Merkezi ve açıklamalı onarım araçları eklendi.
- PIN kurtarma, ayar yedeği ve kurulum onarımı akışları tamamlandı.
- Uygulama kuralları publisher, original filename, SHA-256 ve süreç ilişkileriyle güçlendirildi.
- UI açıklamaları, tooltip'ler ve geçici izin düzeni iyileştirildi.

## v0.17.0 — Güvenli installer yaşam döngüsü

- Windows Installer tabanlı kurulum, güncelleme, onarım ve kaldırma altyapısı eklendi.
- Sürüm yükseltme, rollback ve downgrade engeli doğrulandı.
- Release manifesti, SHA-256 ve imzalayan kimliği kontrolleri eklendi.
- Guardian servis kurulum temeli oluşturuldu.
