# Otium

Hesap veya internet bağlantısı gerektirmeden Windows üzerinde çalışan yerel ekran süresi ve oturum yönetimi uygulaması.

Güncel geliştirme sırası ve v1.0 hedefleri için [ROADMAP.md](ROADMAP.md) belgesine bakın.

## Şu anki sürüm: v0.15.0 prototip

Bu sürüm, krem ve haki tonlarında daha ince bir Otium tasarım dili kullanır. Görünüm Windows tercihini otomatik izleyebilir veya Ayarlar ekranından açık/koyu olarak seçilebilir.

Bu prototip şunları içerir:

- Türkçe yönetim paneli
- Windows ile otomatik açık/koyu tema
- Uygulamayı yeniden başlatmadan Türkçe/English dil değiştirme
- İlk kurulumda kişisel veya korumalı kullanım biçimi seçimi
- Kişisel kullanımda PIN'siz yönetim ve çıkış
- Korumalı kullanımda zorunlu yönetici PIN'i
- Ayarlardan PIN kontrollü kullanım biçimi değiştirme
- Kişisel kullanımda gevşeten kural değişikliklerini beklemeye alma
- 15 dakika, 1 saat veya ertesi gün bekleme seçenekleri
- Bekleyen değişikliği uygulanmadan iptal etme
- Bekleyen değişiklikleri saat ve ayrıntılarıyla Bugün sayfasında gösterme
- Üst bildirimden bekleyen değişiklik kartına hızlı geçiş ve dikkat parıltısı
- Kişisel moddan korumalı moda geçişi de beklemeye alarak mod değiştirme açığını kapatma
- Kural bekleme süresini azaltmayı mevcut bekleme süresine tabi tutma
- Yönetici PIN'i satırını yalnızca korumalı kullanımda gösterme
- Kişisel kontrol merkezi küçültülse bile oturum sayacını arka planda sürdürme
- Çalışan oturum ekranını kontrol merkezinden yeniden açma
- `Ctrl+Alt+Shift+F12` ile tüm kontrolleri atlayan, bir saatlik gizli test kilidi kaldırma
- Kişisel kontrol merkezini kapatınca sistem tepsisine gizleme ve oturumu arkada sürdürme
- Sistem temasına uyumlu, ince ve yuvarlatılmış özel sistem tepsisi menüsü
- Korumalı kullanım için yönetici onayıyla kurulabilen Windows gözetmen servisi
- Oturum ekranı zorla kapatıldığında yaklaşık 5-12 saniye içinde otomatik geri getirme
- Servis durumunu Ayarlar'da gösterme, PIN ve Windows yönetici onayıyla kaldırma
- Servisin yalnızca kendi başlattığı kurulu Otium sürecini kabul etmesi
- Koruma kaydını standart kullanıcıya salt okunur ProgramData alanında saklama
- Engelli uygulamaları çalışırken sonlandırma
- Süreli uygulamaların günlük kullanımını ayrı ayrı sayma ve limitte kapatma
- Süre bitişinde seçilen engel ekranı, Windows kilidi veya oturum kapatma eylemini uygulama
- Korumalı kuralların servis tarafından yönetilen salt-okunur ana kopyası
- Yönetim paneli değişikliklerini PIN doğrulamalı yerel servis kanalıyla uygulama
- Elle değiştirilen kullanıcı ayarlarının korumalı oturumu gevşetememesi
- Başarısız servis PIN denemelerinde artan bekleme süresi
- Sistem saati beş dakikadan fazla geri alındığında kullanımı doğru saat dönene kadar durdurma
- Kullanıcı başına tek Otium süreci ve ikinci açılışta mevcut kontrol merkezini öne getirme
- Süre dolunca bekleyen ayarı otomatik uygulama
- Ayarlardan kalıcı Sistem/Açık/Koyu görünüm seçimi
- İnce özel pencere çubuğu ve açılır-kapanır yan menü
- Sağda, üzerine gelince simge gösteren pencere kontrolleri
- Tek birim başlıklı ve tekrarsız günlük limit sütunu
- Tuzlanmış PBKDF2 özetiyle saklanan yönetici PIN'i
- Yönetim paneli açılışında PIN doğrulaması
- PIN ile korunan doğrudan çocuk oturumu modu
- Windows oturumu açılınca korumalı ekranı otomatik başlatma seçeneği
- Doğrudan oturumda kullanım sayacını otomatik başlatma
- Windows kilitliyken ve bilgisayar uykudayken süreyi durdurma
- Windows kilidi açılınca Mola durumunda kalma ve yalnız kullanıcı Devam Et dediğinde sayacı sürdürme
- Kompakt haftalık plan ve hizalı ayar formları
- Günlük kullanım limiti ayarı
- Haftanın günlerine göre saat aralığı ve süre ayarı
- Ana haftalık planı bozmadan belirli bir tarih ve saat için geçici izin tanımlama
- Oturum ekranından yönetici PIN'iyle yalnız bugüne 15, 30 veya 60 dakika ek süre verme
- Kapalı, süreli ve serbest uygulama kuralları
- Yerel JSON ayar kaydı
- Güncel program durumunun önizlemesi
- Tam ekran oturum ve mola deneyimi
- Gerçek zamanlı kullanım sayacı
- Mola verme ve kaldığı yerden devam etme
- Sabah/akşam kullanımında kalan sürenin korunması
- Aktif oturum için küçük kalan süre paneli
- Mola ekranından uyku, yeniden başlatma ve kapatma menüsü
- Son yedi gün için günlük ve haftalık kullanım geçmişi
- Haftalık toplam, günlük ortalama ve en çok kullanılan uygulama özeti
- Yapılandırılmış süreli ve sınırsız uygulamalar için kullanım dökümü
- Mola, limit dolması, ek süre ve kural değişikliği hareket geçmişi
- Geçmiş verilerini 90 gün boyunca yalnızca cihazda saklama

v0.15 kullanım geçmişini, tarihli geçici izinleri, yönetici onaylı ek süre akışını ve gizli test geçidinin bekleyen değişikliği anında uygulamasını içerir. AppLocker/WDAC tabanlı çalıştırma ilkeleri henüz eklenmemiştir.

## Çalıştırma

```powershell
dotnet run --project src/KardesKilidi.App/KardesKilidi.App.csproj
```

PIN oluşturulduktan sonra çocuk oturumunu doğrudan açmak için:

```powershell
dotnet run --project src/KardesKilidi.App/KardesKilidi.App.csproj -- --session
```

## Kontroller

```powershell
dotnet run --project tests/KardesKilidi.Core.SmokeTests/KardesKilidi.Core.SmokeTests.csproj
```
