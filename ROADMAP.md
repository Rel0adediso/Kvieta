# Otium — Güncel Yol Haritası

Bu belge Otium'un v0.15 sonrasındaki güncel ve bağlayıcı geliştirme sırasıdır. Eski handoff belgelerindeki yol haritası maddeleri tarihsel bağlam olarak kalır; yeni planlama için bu dosya esas alınır.

## Temel ürün ilkeleri

- Otium hesap ve bulut zorunluluğu olmadan yerel çalışır.
- Kişisel mod kullanıcıyı suçlamadan kendi kararına sadık kalmasına yardım eder.
- Korumalı mod standart Windows kullanıcısının basit kaçışlarına direnç gösterir.
- Arayüz krem/haki ve zeytin/grafit kimliğini, kompakt bilgi hiyerarşisini ve sakin ürün dilini korur.
- Yeni özellikler kullanıcıyı zorla içeride tutmaz; ölçülebilir fayda, güven ve iyi deneyim yoluyla kalıcı değer üretir.

## Hemen — Proje güvenliği ve sürüm kontrolü

### Git ve sürüm geçmişi

- Projeyi gerçek bir Git deposuna alma.
- Kaynak, test ve dokümantasyon değişikliklerini anlamlı commit'lerle izleme.
- Kararlı paketleri sürüm etiketiyle işaretleme.
- Build çıktıları ile kullanıcı verilerini depoya almama.
- Her büyük değişiklik için güvenli geri dönüş noktası oluşturma.

## v0.15.1 — Veri sağlamlığı

### Crash-safe veri katmanı

- Ayar, kullanım ve geçmiş dosyalarında süreçler arası yazma kilidi.
- Eşzamanlı yazarların birbirinin verisini ezmesini engelleme.
- Geçici dosya + doğrulama + atomik değiştirme akışı.
- Disk dolu, yarım yazma ve bozuk JSON senaryoları.

### Son sağlam kopya ve kurtarma

- Her doğrulanmış kayıttan sonra `last-known-good` kopyası.
- Ana dosya bozuksa otomatik algılama ve kontrollü geri dönüş.
- Kurtarma gerçekleştiğinde kullanıcıya açık bilgi verme.
- Sessizce sınırsız kullanıma geçmeme.

### Veri şeması ve migration sistemi

- Ayarlar, kullanım ve geçmiş için açık şema sürümleri.
- Her sürüm geçişi için test edilebilir migration adımları.
- Eski PIN, özel cihaz adı, plan, izin ve kullanım geçmişini koruma.
- Yeni sürümün desteklemediği veride güvenli hata ve onarım yolu.

## v0.16 — Ritim ve kullanıcı farkındalığı

### Ritim

- İlk 7–14 günlük kişisel başlangıç ritmi.
- Haftalık toplam ve günlük ortalama karşılaştırmaları.
- Planla uyumlu günler.
- Başlangıç ritmine göre geri kazanılan zaman.
- Uygulama kullanımındaki artış ve azalışlar.
- Yoğun kullanım saatleri ve hafta içi/hafta sonu farkları.
- Küçük, kullanıcı tarafından onaylanan azaltma hedefleri.
- Bağışlayıcı ilerleme dili; kırılan streak veya suçlayıcı mesaj yok.

### Kural sayacı ve farkındalık sayacı ayrımı

- Kural motoru için uygulamanın çalıştığı süreyi ölçme.
- Ritim için yalnız gerçekten önde kullanılan uygulamanın süresini ölçme.
- İki veriyi farklı amaçlarla saklama ve UI'da karıştırmama.

### Yerel aktif uygulama takibi

- Yalnız aktif ön plan uygulamasını ölçme.
- Pencere başlığı, belge adı, yazılan metin veya ziyaret edilen siteyi kaydetmeme.
- Özelliği açık rıza ile etkinleştirme ve istenildiğinde kapatma.
- Tüm veriyi yalnız cihazda tutma.

### Gizlilik ve veri sahipliği merkezi

- Hangi verinin neden ölçüldüğünü açıkça gösterme.
- 30/90/180 günlük saklama seçeneği.
- Geçmişi silme.
- JSON/CSV dışa aktarma.
- Buluta veri gönderilmediğini anlaşılır biçimde belirtme.

## v0.16.1 — Otium hareket dili ve animasyonlar

- Sayfa geçişlerinde kısa fade + hafif yönlü hareket.
- Ritim grafiklerinin ilk açılışta sakin biçimde dolması.
- Sayaç ve ilerleme değerlerinde sert sıçrama yerine kontrollü geçiş.
- Başarılı kaydetme, hedef ilerlemesi ve dönüm noktalarında küçük mikro animasyonlar.
- Pending kartında yalnız kullanıcı yönlendirildiğinde kısa vurgu davranışını koruma.
- Sidebar açılıp kapanırken optik merkezi bozmayan akıcı geçiş.
- Tema değişiminde mümkünse yumuşak renk geçişi.
- Animasyonları kısa, sade ve işlevsel tutma; sürekli glow, konfeti ve dikkat dağıtan loop kullanmama.
- Windows `Reduce motion`/erişilebilirlik tercihini izleme ve animasyonları kapatabilme.
- Animasyonların sayaç, servis veya kural motorunu hiçbir şekilde geciktirmemesi.

## v0.17 — Kurulum, güvenli güncelleme ve rollback

### Installer

- Program Files altında sabit ve güvenli kurulum konumu.
- Başlat menüsü ve isteğe bağlı masaüstü kısayolu.
- Guardian servis kurulumu, onarımı ve kaldırılması.
- Güncellemede ayar ve kullanım geçmişini koruma.
- PIN ve Windows yönetici izniyle kontrollü kaldırma.

### Güvenli güncelleme

- Uygulama ve Guardian sürüm uyumu kontrolü.
- Güncelleme paketi bütünlük doğrulaması.
- Başarısız güncellemede çalışan eski sürüme rollback.
- Eski sürüme dönerek korumayı aşmayı engelleme.
- İlk kararlı sürümde kontrollü manuel güncelleme; otomatik güncellemeyi daha sonra değerlendirme.

## v0.18 — Recovery ve güvenlik sertleştirmesi

### Test ve public build ayrımı

- `Ctrl+Alt+Shift+F12` geçidini yalnız Development/Test build'de derleme.
- Public Release paketinde test geçidini tamamen çıkarma.
- Paket türünü sürüm bilgisinde ve tanılama ekranında açıkça gösterme.

### Yönetici kurtarma sistemi

- Tek kullanımlık recovery kodları.
- Windows yönetici doğrulaması.
- Installer repair ve last-known-good kurtarma yolu.
- Kurtarma işlemlerini yerel audit kaydına yazma.

### Guardian ve IPC güvenliği

- İstemci kimliği ve yetkisini doğrulama.
- Mesaj bütünlüğü ve tekrar oynatma saldırısı koruması.
- Artan PIN beklemesini servis tarafında otoriter tutma.
- Yetkisiz servis komutlarını reddetme ve kaydetme.

### Monotonic zaman güvenliği

- Sistem saatine ek olarak monotonic süre kaynağı kullanma.
- Reboot, saat dilimi ve ileri/geri tarih değişikliklerini ayırma.
- Son güvenilir zamanı saklama.
- Yanlış pozitif kilitlenmede yönetici kurtarma yolu bırakma.

### Uygulama kimliği 2.0

- EXE yoluna ek olarak publisher imzası ve original filename.
- Ürün bilgisi ve isteğe bağlı SHA-256 kimliği.
- Launcher ve child-process ilişkileri.
- Portable ve Microsoft Store uygulamaları.
- AppLocker/WDAC entegrasyonunu isteğe bağlı gelişmiş koruma seviyesi olarak değerlendirme.

## v0.19 — Tanılama, platform sağlamlığı ve regresyon

### Koruma sağlık kontrolü

- Guardian servis durumu.
- Uygulama/servis sürüm uyumu.
- ProgramData ve kurulum izinleri.
- Windows başlangıç kaydı ve korunan policy durumu.
- Sorun bulunduğunda güvenli `Onar` akışı.

### Yerel audit ve tanılama günlüğü

- Servis restart, crash ve recovery olayları.
- Yanlış PIN ve saat manipülasyonu olayları.
- Kural değişiklikleri ve engelleme olayları.
- PIN, pencere başlığı, belge veya özel içerik kaydetmeme.
- Sınırlı saklama ve dışa aktarılabilir tanılama raporu.

### Gerçek Windows entegrasyon testleri

- Servis kurma/kaldırma ve Task Manager kill.
- Windows reboot, Win+L, uyku ve hibernation.
- Elektrik kesintisi ve crash recovery.
- Standart/yönetici Windows hesapları ve kullanıcı değiştirme.
- Bozuk veri, disk dolu ve başarısız upgrade.

### WPF UI otomasyon ve görsel regresyon

- Türkçe/English ve açık/koyu/system tema.
- Sidebar açık/kapalı ve seçili 40×40 hizası.
- Plan, Ritim, pending kartı ve tray menüsü.
- Farklı DPI, çözünürlük ve metin uzunlukları.
- Erişilebilirlik ve `Reduce motion` davranışı.

### Çoklu monitör, DPI ve Windows oturum sağlamlığı

- Oturum/engel yüzünü bütün monitörlerde doğru yönetme.
- Monitör takma/çıkarma ve ana ekran değişimi.
- DPI/ölçek ve ekran yönü değişimi.
- Explorer restart, kullanıcı değiştirme ve Remote Desktop senaryoları.

## v1.0 — İlk kararlı açık kaynak sürüm

- Personal ve Protected akışlarının uçtan uca kararlı olması.
- Test backdoor'u içermeyen public paket.
- Güvenli installer, recovery ve upgrade yolu.
- Ritim, geçmiş ve gizlilik kontrolleri.
- Guardian ve uygulama kural motoru için belgelenmiş güvenlik sınırları.
- Kurulum, kullanım, kurtarma ve katkı rehberleri.
- Lisanslı, etiketlenmiş ve tekrar üretilebilir GitHub sürümü.

## v1.0 sonrasına bırakılanlar

- Tarayıcı eklentisi ve site kuralları.
- Aynı Wi-Fi üzerinden telefon kontrolü.
- İnternet üzerinden uzaktan yönetim.
- Bulut senkronizasyonu ve çoklu cihaz paneli.
- Her sistemde zorunlu AppLocker/WDAC yönetimi.
