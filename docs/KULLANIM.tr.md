# Kvieta kullanım ve kurtarma rehberi

> Mevcut durum: **Kvieta Alpha 3**. Community paketleri doğrulama amaçlı imzasız önizlemelerdir; final public sürüm değildir.

## Kurulum ve güncelleme

1. Yalnız bu deponun GitHub Releases sayfasından veya kendi kaynak checkout'unuzdan ürettiğiniz paketi kullanın.
2. `Kvieta-Setup-<sürüm>.exe` dosyasını açın ve Türkçe ya da English seçin.
3. Temiz kurulumda sihirbaz kullanım biçimi, koruma seviyesi, cihaz adı, günlük süre, Windows başlangıcı ve masaüstü kısayolunu sorar.
4. Kvieta zaten kuruluysa kurucu doğrudan **Güncelle/Onar** ekranına gider. Daha yeni paket yükseltme yapar; aynı sürüm onarım sunar; eski paket downgrade'i engeller.
5. Yönetici izni yalnız Windows Installer işlemi gerektiğinde istenir. Kurulum başarısız olursa yeni ilk kullanım ayarları kaydedilmez.

Kvieta varsayılan olarak `C:\Program Files\Kvieta` altına kurulur. Kullanıcı ayarları ve geçmiş `%LOCALAPPDATA%\Kvieta`, korunan policy ile Guardian durumu `%ProgramData%\Kvieta` altında tutulur. Güncelleme ve onarım bu alanları korur.

## İlk kullanım biçimleri

- **Farkındalık:** Kısıtlama olmadan, yapılandırılan uygulamaların kullanımını cihazda ölçer.
- **Kişisel:** Plan ve limitleri kişinin kendi düzeni için uygular. Esnek kullanıcı kontrollüdür; Dengeli aktif zaman penceresinde oturum yüzeyini korur.
- **Aile:** Bir aile üyesinin ayrı bir Windows yöneticisince yönetilen standart hesabı içindir. Yönetici PIN'i ve Guardian ile kuralları korur.

Kişisel kullanımda **Hızlı odak**, Bugün ekranından veya tray menüsünden 25, 50 ya da 90 dakikalık oturum başlatır. Bugün ekranında özel süre seçilebilir ve yalnız ayrı bir yerel tercih dosyasında tutulan son odak süresi tekrarlanabilir. Odak hedefi günlük limiti veya izin verilen planı uzatmaz.

Bugün ekranı mevcut kullanım ve kalan süreyi; en çok kullanılan üç uygulama, düne göre değişim ve aktif ya da sıradaki plan penceresiyle birleştirir. Veri yoksa veya ilk günse uydurma karşılaştırma gösterilmez.

Ölçülen bir uygulamadaki **Kural oluştur** eylemi dosya seçici açmadan günlük limit, yalnız plan içinde kullanım, odakta engelleme, sınırsız kullanım veya kalıcı engel seçeneklerini sunar. Kullanım geçmişi tam dosya yolu yerine yalnız çalıştırılabilir dosya adını sakladığı için mevcut kuralı olmayan uygulama ilk kural oluşturulurken açık olmalıdır. Değişiklik **Kaydet** sonrasında ve Kişisel/Aile onay kurallarına uygun biçimde uygulanır.

Sürenin bitmesine 15, 5 ve 1 dakika kala oturum yüzeyi sakin bir toparlanma kartı sunar: işi kaydettiğini onaylama, kontrollü mola verme, kullanım biçimi izin veriyorsa ek süre isteme veya yarını planlamak için Kontrol Merkezi'ni açma.

**Ritim Serisi** her gün tek bir anlamlı sonucu ödüllendirir: Farkındalıkta günlük özeti inceleme, Kişisel · Esnek kullanımda bir odak oturumu tamamlama veya planlı Kişisel/Aile kullanımında günlük dengeyi koruma. Dinlenme günleri seriyi bozmaz ya da ilerletmez. Her yedi başarılı gün en fazla iki tane tutulabilen bir Ritim Koruyucu kazandırır; 3, 7, 14, 30, 50 ve 100 günlük kilometre taşları gösterilir. Yönetici onaylı geçici izin ve kurtarma günleri koruyucu tüketmez, seriyi bozmaz. Haftalık özet odak süresini ve en çok artan/azalan uygulama eğilimlerini içerir. İsteğe bağlı 1200×630 paylaşım kartı cihazda üretilir ve uygulama adı içermez. Öneri uygulanabilir, ertesi güne bırakılabilir veya bu cihazda kalıcı gizlenebilir.

Kurulum sırasında beş düzenlenebilir niyet şablonu yararlı bir başlangıç sunar: Kullanımımı gör, Odaklan, Oyun düzeni, Akşam bırak ve Aile düzeni. Şablon uygun kullanım biçimini seçer ve gerektiğinde kurulum tamamlanmadan değiştirilebilen haftalık planı doldurur.

Aile modunda yönetici, oturum aktifken veya moladayken mevcut PIN korumalı eylemle ek süre verebilir; günlük sürenin tamamen dolmasını beklemek gerekmez.

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
