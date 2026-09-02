# Kvieta kullanım ve kurtarma rehberi

> Mevcut durum: **Kvieta Alpha 2**. Community paketleri doğrulama amaçlı imzasız önizlemelerdir; final public sürüm değildir.

## Kurulum ve güncelleme

1. Yalnız bu deponun GitHub Releases sayfasından veya kendi kaynak checkout'unuzdan ürettiğiniz paketi kullanın.
2. `Kvieta-Setup-<sürüm>.exe` dosyasını açın ve Türkçe ya da English seçin.
3. Temiz kurulumda sihirbaz kullanım biçimi, koruma seviyesi, cihaz adı, günlük süre, Windows başlangıcı ve masaüstü kısayolunu sorar.
4. Kvieta zaten kuruluysa kurucu doğrudan **Güncelle/Onar** ekranına gider. Daha yeni paket yükseltme yapar; aynı sürüm onarım sunar; eski paket downgrade'i engeller.
5. Yönetici izni yalnız Windows Installer işlemi gerektiğinde istenir. Kurulum başarısız olursa yeni ilk kullanım ayarları kaydedilmez.

Kvieta varsayılan olarak `C:\Program Files\Kvieta` altına kurulur. Kullanıcı ayarları ve geçmiş `%LOCALAPPDATA%\Kvieta`, korunan policy ile Guardian durumu `%ProgramData%\Kvieta` altında tutulur. Güncelleme ve onarım bu alanları korur.

## İlk kullanım biçimleri

- **Sadece takip:** Kısıtlama olmadan, yapılandırılan uygulamaların kullanımını cihazda ölçer.
- **Kendim için:** Plan ve limitleri kişinin kendi düzeni için uygular. Flexible kullanıcı kontrollüdür; Balanced aktif zaman penceresinde oturum yüzeyini korur.
- **Yönettiğim biri için:** Ayrı bir Windows yöneticisinin yönettiği standart hesap içindir. Yönetici PIN'i ve Guardian ile kuralları korur.

Haftalık planı, günlük limiti ve uygulama kurallarını gözden geçirip **Kaydet** düğmesine basın. Korumalı kullanımda yönetici PIN'i ile tek kullanımlık kurtarma kodlarını güvenli ve ayrı bir yerde saklayın.

## Uygulama davranışları

- **Kalıcı kapalı:** Tanınan uygulamayı ve ilişkili alt süreçleri çalışırken sonlandırır.
- **Süreli:** Günlük kullanımı sayar ve tanımlanan süre dolduğunda uygulamayı kapatır.
- **Serbest:** Engellemez; yerel ölçüm açıksa farkındalık için kullanım süresini gösterebilir.
- **Kaldır:** Yalnız uygulama kuralını siler; programı Windows'tan kaldırmaz.

## Sağlık ve Kurtarma Merkezi

Ayarlar altındaki sistem sağlığı bölümü uygulama, installer, Guardian ve yerel veri durumunu ayrı ayrı gösterir.

- **Saati onayla:** Yanlış saat uyarısını Windows yönetici onayıyla temizler; sistem saatini değiştirmez.
- **Ayarları geri yükle:** Son doğrulanmış ayar kopyasını geri getirir; kullanım geçmişini silmez.
- **Kurulumu onar:** Uygulama ve Guardian kurulumunu yeniden doğrular; planı, PIN'i ve geçmişi silmez.
- **Tanılama raporu:** PIN, kurtarma kodu, pencere başlığı ve içerik toplamayan bir JSON raporu dışa aktarır.

Guardian eksik veya bozuksa korunan kullanım sessizce korumasız devam etmez. Onarımı yönetici olarak onaylayın; sorun sürerse tanılama raporuyla birlikte hata bildirimi oluşturun.

## Kaldırma

Kvieta'yı kaldırmak için uygulamada **Ayarlar > Uygulamayı kaldır** yolunu kullanın veya Windows **Yüklü uygulamalar** listesinden Kvieta'yı seçin. Windows Installer yönetici onayı isteyebilir.

Kaldırma, yeniden kurulum veya yükseltme sırasında yerel ayarların ve geçmişin korunması amaçlanır. Verileri de silmek isterseniz önce gerekli dışa aktarımı alın; alpha sürümünde veri temizliğini ayrıca ve dikkatle yapın.

## Bilinen sınırlar

- Protected kullanım, ayrı yönetici hesabıyla yönetilen standart Windows kullanıcısı için tasarlanır; Windows yöneticisine veya fiziksel disk erişimine karşı mutlak koruma değildir.
- Çoklu monitör, farklı DPI, uyku/hibernation ve tam installer yaşam döngüsü final V1 matrisi henüz tamamlanmadı.
- Alpha test installer'ı imzasız olduğu için Windows SmartScreen uyarı gösterebilir.

Güvenlik sınırları için [SECURITY.tr.md](SECURITY.tr.md), yardım ve bildirim yolu için [Destek](../.github/SUPPORT.md) belgesine bakın.
