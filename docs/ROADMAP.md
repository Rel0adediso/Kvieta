# Kvieta — Ürün ve Release Yol Haritası

**Son güncelleme:** 4 Eylül 2026

**Mevcut yayın:** **Kvieta Alpha 2.1** community prerelease

**Aktif hedef:** Alpha 2.1 saha geri bildirimi ve final `v1.0.0` Windows doğrulama matrisi

**Yayın:** [Kvieta Alpha 2.1](https://github.com/Rel0adediso/kvieta-app/releases/tag/kvieta-alpha-2.1)

Bu belge Kvieta'nın bağlayıcı geliştirme sırasını, bilinen eksiklerini, release
kriterlerini ve v1 sonrasındaki ürün yönünü tanımlar. Tamamlanan çalışmalar kısa
bir tarihçe olarak belgenin sonunda tutulur; günlük geliştirme önceliği için
öncelikle **Aktif çalışma planı** bölümü esas alınır.

## Kvieta Alpha 2.1 — yayımlandı; saha testinde

Alpha 2.1; Alpha 2 üzerine kurulumda haftalık planlama, okunabilir kullanım
görselleştirmeleri, kontrollü kaldırma, yönetilen cihaz akışları ve arayüz
iyileştirmelerini ekler. Paket etiketi `Alpha-2.1`, GitHub etiketi
`kvieta-alpha-2.1` olur; numerik MSI sürümü uyumluluk için `1.0.0` kalır.

- [x] Kurulum akışına biçime göre haftalık planlama eklendi.
- [x] Yedi günlük kullanım grafiği ve uygulama kartları iyileştirildi.
- [x] Kontrollü kaldırma, isteğe bağlı veri temizliği ve sonuç ekranı eklendi.
- [x] Yönetilen cihaz, Guardian ve yerel companion akışları sertleştirildi.
- [x] Temiz kaynak commit'inden Alpha 2.1 community paketi üretildi ve doğrulandı.
- [x] GitHub kalite hattı Alpha 2.1 release commit'inde başarıyla çalıştı.
- [x] `kvieta-alpha-2.1` prerelease'i ve doğrulama dosyaları yayımlandı.
- [ ] Alpha 2.1 gerçek cihaz yükseltme ve kullanım testi tamamlanmalı.

## Ürün ilkeleri

- Kvieta hesap veya bulut zorunluluğu olmadan yerel çalışır.
- Kullanım verisi, planlar, kurallar ve tanılama kayıtları varsayılan olarak cihazda kalır.
- **Farkındalık** hiçbir kısıtlama uygulamadan kullanımı anlamayı sağlar.
- **Kişisel** kullanım, kişiyi cezalandırmadan kendi kararına sadık kalmasına yardım eder.
- **Aile** kullanımı, ayrı bir yöneticinin yönettiği standart Windows
  hesabında yaygın kaçış yollarına direnç gösterir.
- Güvenlik iddiaları açık, test edilebilir ve belgelenmiş sınırlar içinde tutulur.
- Arayüz krem/haki ve zeytin/grafit kimliğini, sakin ürün dilini ve kompakt bilgi hiyerarşisini korur.
- Günlük deneyim **Gör → Seç → Yap → Sürdür → Değerlendir → Uyarla** döngüsünü izler.
- Devamlılık mekanikleri kullanıcıyı suçlamaz, dinlenmeyi cezalandırmaz ve hiçbir güvenlik kuralını gevşetmez.
- Public paketler test geçidi, belgelenmemiş veri toplama veya sessiz güvenlik gevşetmesi içermez.

## Doğrulanmış mevcut durum

26 Ağustos 2026 tarihinde yeni geliştirme bilgisayarında aşağıdaki kontroller yeniden yapıldı:

- `main`, `origin/main` ile eşleşiyor ve çalışma ağacı temizdi.
- Debug ve Release derlemeleri `0` uyarı ve `0` hatayla tamamlandı.
- Debug ve Release çekirdek smoke testleri geçti.
- Smoke test paketinde 190 davranış ve regresyon doğrulaması bulunuyor.
- `dotnet format --verify-no-changes` başarılı; 81 kaynak dosyada değişiklik gerekmedi.
- NuGet'in güncel verisine göre bilinen zafiyet içeren paket bulunmadı.
- Self-contained `win-x64` Release EXE üretildi; mevcut çıktı yaklaşık 69,6 MB.
- WiX `smartcab` gecikmesinin Türkçe karakter içeren kullanıcı `TEMP` yolundan
  kaynaklandığı doğrulandı; ASCII staging ile imzasız geliştirme MSI'sı üretildi.

Bu sonuçlar çekirdek ve kaynak build sağlığını doğrular. Gerçek Windows yaşam
döngüsü, Guardian, installer ve ekran davranışlarının tamamının doğrulandığı
anlamına gelmez.

3 Eylül 2026 tarihinde Alpha 2.1 release commit'i `4928649` için ek olarak:

- Debug/Release build ve smoke testleri geçti.
- Dokümantasyon ve public-build bypass kontrolleri geçti.
- Community Setup EXE/MSI metadata'sı ile release manifesti kaynak commit'ine karşı doğrulandı.
- `kvieta-alpha-2.1` etiketiyle imzasız community prerelease ve doğrulama dosyaları yayımlandı.

Bu release kanıtı paket hattını kapatır; ayrı cihazdaki gerçek yükseltme,
Guardian ve uzun kullanım doğrulaması açık kalır.

## Kvieta Alpha 1 yayın arşivi

İlk Kvieta markalı community preview kullanıcıya **Kvieta Alpha 1** adıyla sunuldu.
Git tag `alpha-1`, paket etiketi `Alpha-1` olarak kullanıldı; `v1.0.0-alpha.1`
kullanıcıya görünen ürün adı yapılmadı. Windows Installer ürün sürümü
yükseltme/onarım uyumluluğu için `1.0.0` kaldı.

### Alpha 1 kapsamı

- İsteğe bağlı güvenilir telefon eşleştirme, QR aktarımı ve telefonla PIN kurtarma.
- Yönetici çıkışı, Control Center/session tekilleştirme ve Guardian geçiş düzeltmeleri.
- Eski Alpha kurulumunu güncelleme, onarma, yeniden yapılandırma ve kaldırma akışları.
- Guardian başlatma/onarım, protected policy aktarımı, dosya kilidi ve recovery düzeltmeleri.
- Kurulum PIN akışı, açıklamalar, hata durumları ve Kvieta teması iyileştirmeleri.
- Uygulama kuralları, oturum ekranı ve güç menüsü regresyon düzeltmeleri.

### Alpha 1 yayın kapısı

- [x] Çalışma ağacında geçici çıktı, sır veya yanlışlıkla eklenen makineye özel dosya bulunmadığı doğrulandı.
- [x] NuGet audit, format, dokümantasyon, Debug/Release build ve smoke testleri temiz geçti.
- [x] Public-build bypass kontrolü ve companion web bundle üretimi geçti.
- [x] Yerel `Alpha-1` community Setup EXE/MSI metadata, manifest ve SHA-256 kontrollerini geçti.
- [x] Release commit'inden temiz community paketi yeniden üretildi; manifest commit'i `alpha-1` tag'iyle eşleştirildi.
- [x] GitHub Release **Kvieta Alpha 1** başlığıyla prerelease olarak yayımlandı.
- [x] README indirme bağlantıları ve SHA-256 değeri yayımlanan paketle güncellendi.

## `v1.0.0-alpha.1` yayın arşivi

`alpha.1`, final `v1.0.0` değil; gerçek cihaz kullanımından geri bildirim
toplamak için hazırlanan ilk GitHub prerelease paketidir. Bu hedef için özellik
kapsamı dondurulmuştur. Yayını kullanılamaz veya güvensiz hale getiren bir hata
bulunmadıkça yeni büyük özellik eklenmez.

### Alpha.1 için tamamlananlar

- Release konfigürasyonunda, development/test bypass içermeyen imzasız community
  paket hattı hazırlandı.
- Self-contained Setup EXE, bağımsız MSI, iki SHA-256 dosyası ve şema v2
  manifest birlikte üretilebiliyor.
- Paket metadata'sı, gömülü MSI, kaynak commit'i, dosya boyutu ve SHA-256
  eşleşmesi otomatik doğrulanıyor.
- Public assembly'de development unlock yolu bulunmadığı ikili çıktıdan
  doğrulanıyor.
- Community paket job'unu içeren GitHub Actions koşusunda (`3d15959`) hem
  **Build and test** hem de **Package community alpha installer** işleri geçti.
- Aynı commit'ten üretilen yerel community adayında paket metadata'sı, gömülü
  MSI, self-contained publish, manifest ve SHA-256 kontrolleri geçti.
- Nihai `4dd94ca` release commit'inde aynı iki GitHub Actions işi yeniden geçti;
  bu commit'e bağlı paket `v1.0.0-alpha.1` prerelease olarak yayımlandı.
- English ve Türkçe README; Alpha.1 indirme bağlantısı, doğrulama hash'i,
  imzasız paket uyarısı ve saha testi durumuyla güncellendi.
- Temel iki fiziksel ekran kullanımında oturum yüzeyi ve ekran kalkanları
  başarıyla denendi.
- Süre dolduğunda Windows oturumunu tekrar tekrar kapatan eylem kaldırıldı;
  eski ayarlar güvenli biçimde Windows kilidine taşınıyor.
- Korumalı mod seçimi Kaydet'e basılmadan Guardian veya policy değişikliği
  uygulamıyor.

### Alpha.1 yayın kapısı

- [x] Yerel NuGet audit, format, dokümantasyon, Debug/Release build, iki smoke-test
  yapılandırması ve public-build bypass kontrolü aynı temiz commit'te geçti.
- [x] Community Setup EXE/MSI metadata'sı, gömülü MSI, Guardian servis kaydı,
  manifest, dosya boyutu ve SHA-256 eşleşmesi otomatik doğrulandı.
- [x] Son release commit'i (`4dd94ca`) için GitHub Actions kalite hattının tamamı
  geçmeli.
- [x] Release notes son güvenlik, optimizasyon, mod kaydetme ve süre dolma
  değişiklikleriyle güncellenmeli.
- [x] Eski `v1.0.0-rc.1`, herhangi bir GitHub Release'e bağlı olmayan legacy
  etiket olarak korunmalı; ilk gerçek test yayını Alpha.1 olarak belgelenmeli.
- [x] Temiz release commit'inden son community paketi yeniden üretilmeli; manifest
  commit'i release tag'iyle birebir eşmeli.
- [x] `v1.0.0-alpha.1` annotated tag'i oluşturulup GitHub Release, **Pre-release**
  olarak yayınlanmalı.
- [x] Setup EXE, MSI, SHA-256 dosyaları ve `release-manifest.json` GitHub Release'e
  eklenmeli; SmartScreen ve imzasız yayıncı uyarısı açıkça yazılmalı.

### Alpha.1 yayın sonrası saha testi

Bu kontroller Alpha.1 paketinin gerçek kullanım amacıyla yayımlanmasından sonra,
ayrı bir Windows cihazında yapılır. Engelleyici, veri kaybı veya güvenlik riski
bulunursa mevcut prerelease kaldırılır ve düzeltilmiş bir alpha paketi hazırlanır.

- [ ] Temiz kurulumda Türkçe ve English kurucu, uygulama açılışı ve temel ayar
  kaydı doğrulanmalı.
- [ ] Protected seçiminde Guardian kurulumu, enrollment ve korunan oturum açılışı
  gerçek paketle doğrulanmalı.
- [ ] Süre dolmasında Windows kilidi/engel ekranı ve yönetici geri dönüşü çıkış
  veya kilit döngüsü oluşturmamalı.
- [ ] Uygulama içinden kaldırma ve temel repair akışı gerçek paketle denenmeli.
- [ ] Bir veya iki günlük gerçek kullanım gözlemi tamamlanmalı; sonuçlar roadmap'e
  ve gerekirse issue/release notes'a işlenmeli.

### Alpha.1 sonrasına ertelenenler

Aşağıdaki maddeler alpha kullanım testini başlatmaya engel değildir; final
`v1.0.0` öncesindeki P0/P1 planında açık kalır:

- Eksiksiz `Alt+Tab`, `Win+D`, sanal masaüstü, görev çubuğu ve Explorer restart matrisi.
- Remote Desktop, kullanıcı değiştirme, hibernation ve güç kesintisi senaryoları.
- Bütün DPI, dikey ekran, negatif koordinat ve monitör takma/çıkarma kombinasyonları.
- WPF UI otomasyonu, testlerin konu bazlı projelere ayrılması ve coverage kapıları.
- Authenticode imzalama, otomatik güncelleme, AppLocker/WDAC ve canlı uygulama önerileri.
- Installer upgrade, rollback, downgrade ve hata enjeksiyonunun tam Windows matrisi.

## Aktif çalışma planı

### P0 — `v1.0.0` release engelleri

Bu bölümdeki bütün maddeler kapanmadan final `v1.0.0` etiketi oluşturulmaz.

#### 1. Ürün dili ve kavram bütünlüğü

**Durum:** Tamamlandı; uygulama, kurulum, belgeler ve geriye uyumlu JSON
sözleşmesi yeni sözlüğe taşındı. Setup ve App ortak ürün sözlüğünü kullanıyor.

Kullanıcının Kvieta'yı neden kullandığı ile korumanın teknik seviyesi ayrı
kavramlar olarak sunulur. Güncel arayüzdeki “Yönettiğim biri için”,
“Gözetimli/Guarded” ve eski `Cafe` adları V1 öncesinde kaldırılır.

Bağlayıcı kullanıcı dili:

- **Farkındalık / Insights:** kurulum kartı “Kullanımımı görmek istiyorum” olur.
- **Kişisel / Personal:** kurulum kartı “Kendi düzenimi kurmak istiyorum” olur.
- **Aile / Family:** kurulum kartı “Bir aile üyesi için kuruyorum” olur.
- Kişisel koruma seviyeleri **Esnek / Flexible**, **Dengeli / Balanced** ve
  **Korumalı / Protected** olur.
- Aile kullanımında ayrı seviye seçilmez; yönetici PIN'i ve Guardian koruması
  bu kullanım amacının zorunlu davranışıdır.

Kod ve kaynak temizliği:

- [x] `ControlMode` kavramı `UsageMode` olarak; değerleri `Insights`, `Personal`
  ve `Family` olarak yeniden adlandırılmalı.
- [x] `PersonalProtectionLevel.Guarded`, `PersonalProtectionLevel.Protected` olarak
  yeniden adlandırılmalı.
- [x] `CafeWindow`, `SessionSurfaceWindow`; `CafeViewModel`, `SessionViewModel`
  olarak güvenli semantic refactor ile taşınmalı.
- [x] `SessionWidgetWindow`, Kontrol Merkezi ve Oturum Yüzeyi adları birbirinden
  açıkça ayrılmalı.
- [x] Setup ve App metinleri merkezi Türkçe/English kaynaklardan gelmeli; aynı
  kavramın kod içinde yinelenen çevirileri kaldırılmalı.
- [x] Eski JSON, protected policy ve Guardian enrollment verileri için enum sırası,
  şema migration'ı ve IPC uyumluluğu korunmalı. Kullanıcıya görünmeyen
  `sync-guarded` gibi mevcut wire değerleri uyumluluk gerekçesi olmadan değiştirilmemeli.

Kabul kriteri:

- Güncel arayüz ve kullanıcı belgelerinde eski ürün terimleri bulunmuyor.
- Türkçe ve English adlar aynı amacı ve aynı güvenlik sonucunu anlatıyor.
- Alpha 2.1 ayarları, protected policy'si ve Guardian kaydı veri kaybı olmadan açılıyor.
- Yeniden adlandırma sonrasında Debug/Release build ve ilgili regresyon testleri geçiyor.
- Dokümantasyon kapısı tarihsel release notes dışında eski terimlerin geri dönmesini engelliyor.

#### 2. Bugün deneyimi ve hızlı eylemler

**Durum:** Devam ediyor; ortak ürün sözlüğü tamamlandı. Kişisel mod için
policy değiştirmeyen `25/50/90` dakika hızlı odak ve tray kısayolları eklendi;
özel süre, son oturumu tekrarlama ve günlük hiyerarşinin kalan parçaları bekliyor.

Kvieta'nın ana yüzü ayar listesi değil, kullanıcının birkaç saniyede
“Bugün neredeyim ve şimdi ne yapabilirim?” sorusunu yanıtlayan günlük merkez olur.

Yapılacaklar:

- Bugün toplam kullanımı, kalan süre, en çok kullanılan uygulamalar,
  önceki döneme göre değişim, sıradaki plan ve aktif oturumu tek hiyerarşide gösterme.
- [x] `25`, `50`, `90` dakika odak hedeflerini Bugün ve tray yüzeyinden başlatma.
- [ ] Özel süre ve son oturumu tekrarlama seçeneklerini hızlı odağa ekleme.
- “Kullanımımı gör”, “Odaklan”, “Oyun süremi düzenle”, “Akşam
  bilgisayarı bırak” ve “Aile düzeni kur” niyetlerine uygun başlangıç şablonları sunma.
- Uygulama kullanım kartından ayrı ayar ekranında dosya aratmadan günlük
  limit, plan içi izin, odakta engelleme, sınırsız veya kalıcı engel kuralı oluşturma.
- Mevcut `15/5/1` dakika uyarılarını işi kaydetme, kontrollü mola, ek süre
  isteme ve yarının planını düzenleme eylemleriyle nazik bir süre bitişine bağlama.
- [x] Aile modunda mevcut PIN doğrulamalı ek süre akışını süre dolmadan,
  aktif veya moladaki oturumdan da erişilebilir yapma.

Kullanım amacına göre ana deneyim:

- **Farkındalık:** analiz, yerel ölçüm ve isteğe bağlı küçük hedef; kısıtlama yok.
- **Kişisel · Esnek:** hızlı ve tamamen kullanıcı kontrollü odak oturumları.
- **Kişisel · Dengeli/Korumalı:** plan, limit, bekletilen gevşetme ve günlük denge.
- **Aile:** yönetici korumalı plan, ek süre isteği, geçici izin ve açık uyarılar.

Kabul kriteri:

- Yeni kullanıcı iki dakika içinde amacını seçip ilk anlamlı eylemini başlatabiliyor.
- İlk odak oturumu en fazla iki ana etkileşimle başlatılabiliyor.
- Bugün ekranı her kullanım amacında yalnız ilgili bilgi ve eylemleri gösteriyor.
- Hızlı eylemler kaydedilmeden policy veya Guardian davranışını değiştirmiyor.

#### 3. Ritim Serisi ve haftalık değerlendirme

**Durum:** Açık; `RhythmAnalyzer`, günlük ledger, haftalık karşılaştırma ve
küçük azaltma hedefleri yeniden kullanılacak.

Ritim Serisi kullanıcıyı Kvieta'yı açtığı için değil, kendi seçtiği
anlamlı davranışı tamamladığı için ödüllendirir. V1'de aynı anda tek aktif
günlük ritim hedefi bulunur.

Hedef türleri:

- **Farkındalık Serisi:** günlük özeti inceleme veya isteğe bağlı azaltma hedefi.
- **Odak Serisi:** seçilen günlük odak süresini ya da oturum sayısını tamamlama.
- **Denge Serisi:** günlük limit, plan veya bırakma saati hedefine uyma.
- **Dengeli Günler:** Aile kullanımında ceza veya pazarlık aracı olmayan olumlu ilerleme.

Seri kuralları:

- Mevcut seri ve en iyi seri ayrı tutulur; `3/7/14/30/50/100` gün kilometre
  taşları Kvieta'nın filiz/yaprak/çiçek diliyle sakin biçimde kutlanır.
- Planlı dinlenme günü seriyi bozmaz ve sayıyı artırmaz. Bilgisayarın hiç
  kullanılmadığı gün denge hedefinde başarılı; odak hedefinde dinlenme sayılır.
- Her yedi başarılı gün bir **Ritim Koruyucu** kazandırır; en fazla iki
  tane tutulur. Korunan gün seriyi ilerletmez, yalnız kırılmasını önler.
- Yönetici onaylı ek süre, planlı izin, Kvieta/Guardian arızası veya geçerli
  veri recovery işlemi kullanıcıyı haksız yere cezalandırmaz.
- Seri bozulduğunda en iyi sonuç korunur; suçlayıcı metin, panik geri sayımı,
  XP, sanal para, liderlik tablosu veya ücretli koruyucu kullanılmaz.
- Seri verisi cihazda kalır, güvenilir saat korumasına uyar ve doğrulanmış
  günlük kayıtlardan deterministik olarak yeniden hesaplanabilir.

Haftalık Ritim Özeti:

- Toplam kullanım, önceki haftaya göre değişim, odak süresi, planla uyumlu
  günler, en çok artan/azalan uygulama ve kazanılan ritim günlerini birleştirme.
- Yalnız bir açıklanabilir yerel öneri sunma; kullanıcının uygulamasına,
  gizlemesine veya daha sonra hatırlatmasına izin verme.
- İsteğe bağlı, uygulama adları gizlenebilen ve cihazda üretilen temel
  haftalık paylaşım kartı sağlama; hiçbir içeriği otomatik paylaşmama.

Kabul kriteri:

- Her kullanım amacında hedefin neden ilerlediği veya ilerlemediği açıklanabiliyor.
- Seri, dinlenme ve recovery kuralları gece yarısı, saat geri alma ve eşzamanlı yazmada test ediliyor.
- Seri kazanmak veya kaybetmek hiçbir policy, PIN, Guardian ya da ek süre kuralını değiştirmiyor.
- Türkçe/English metinler teşvik edici fakat suçlamayan aynı anlamı taşıyor.

#### 4. Dengeli oturum yüzeyi ve masaüstü kaçışları

**Durum:** Devam ediyor; ilk pencere recovery sertleştirmesi uygulandı, gerçek Windows matrisi açık.

Dengeli kişisel kullanımda gösterilen oturum yüzeyi yalnız `Topmost` ve `Maximized`
pencere davranışına dayanıyor. Pencere küçültülebiliyor, arkaya gönderilebiliyor
veya masaüstü geçişleriyle aşılabiliyor.

26 Ağustos 2026'da tamamlanan ilk sertleştirme:

- Zorunlu tam ekran yüzey minimize edildiğinde anında maximize durumuna dönüyor.
- Pencere odağı kaybedildiğinde recovery işlemi UI kuyruğundan tekrar değerlendiriliyor.
- Recovery kararı ayrı ve test edilebilir bir policy'ye taşındı.
- Aktif oturum widget'ı, Farkındalık, Kontrol Merkezi, modal doğrulama ve yüzey geçişleri recovery dışında tutuldu.
- Debug/Release build ve yeni policy regresyon testleri geçti.

Kalan doğrulama:

- Görev çubuğu, `Alt+Tab`, `Win+D`, sanal masaüstü ve Explorer restart gerçek Windows üzerinde test edilmeli.
- Bulunan davranış farkları düzeltildikten sonra UI otomasyonuna bağlanmalı.

Yapılacaklar:

- Minimize girişimini algılayıp güvenli pencere durumunu geri yükleme.
- Deactivation, görev çubuğu, `Alt+Tab`, `Win+D` ve sanal masaüstü geçişlerini test etme.
- Modal PIN/recovery pencereleri açıkken oturum yüzeyinin klavye odağını çalmamasını koruma.
- Kontrol Merkezi açılırken tek oturum yüzeyi ve tek yönetim penceresi garantisini koruma.
- Explorer yeniden başlatılması ve süreç yeniden oluşturulması senaryolarını test etme.
- Davranışı mümkün olan ölçüde otomatik Windows UI regresyon testine bağlama.

Kabul kriteri:

- Kullanıcı izin verilen akış dışında masaüstüne ulaşamıyor.
- Koruma döngüsü normal kullanım veya yönetici doğrulama pencerelerini kilitlemiyor.
- Esnek seviyenin kullanıcı kontrollü davranışı yanlışlıkla sertleştirilmiyor.

#### 5. Çoklu monitör, DPI ve ekran yaşam döngüsü

**Durum:** Devam ediyor; temel iki fiziksel ekran testi geçti, genişletilmiş topoloji ve DPI matrisi final V1 için açık.

26 Ağustos 2026'da ikincil ekranları görev çubuğu dahil kaplayan, monitör
takma/çıkarma ve çözünürlük değişiminde kendini yenileyen ekran kalkanı altyapısı
eklendi. 27 Ağustos'ta iki fiziksel ekranla yapılan temel kullanım testi sorun
göstermedi. Final `v1.0.0` öncesinde farklı DPI, yön, negatif koordinat ve
takma/çıkarma kombinasyonları ayrıca kanıtlanacaktır.

Bekleyen fiziksel test:

- Birincil ekranda normal Kvieta kontrollerinin kalması.
- İkincil ekranların masaüstü ve görev çubuğunu göstermeyen Kvieta kalkanıyla kaplanması.
- Monitör çalışma sırasında takıldığında kalkanın otomatik oluşması.
- Monitör çıkarıldığında yardımcı pencerenin temizlenmesi ve yeniden bağlandığında geri gelmesi.
- Farklı DPI, çözünürlük, ekran yönü ve negatif koordinat yerleşimlerinin doğrulanması.

Yapılacaklar:

- Her bağlı monitörü kapsayan koordineli oturum yüzeyleri oluşturma.
- Monitör takma/çıkarma, ana ekran ve ekran yönü değişimini izleme.
- Yüzde 100–200 ölçeklerde düzeni doğrulama.
- Farklı çözünürlük ve negatif ekran koordinatlarında yerleşimi doğrulama.
- Oturum kilidi, kullanıcı değiştirme ve Remote Desktop dönüşlerinde yüzeyleri yeniden kurma.
- Ekran değişiklikleri sırasında yinelenen pencere veya korumasız boşluk oluşmasını engelleme.

Kabul kriteri:

- Aktif kısıtlama gerektiğinde bütün ekranlar kapsanıyor.
- Ekran topolojisi değiştiğinde koruma güvenli biçimde yeniden kuruluyor.
- DPI değişimi kırpılmış metin, erişilemeyen düğme veya görünmeyen modal pencere üretmiyor.

#### 6. Installer üretim hattı

**Alpha 2.1 güncellemesi:** Development bypass içermeyen Release community hattı
çalıştırıldı ve yayın paketi doğrulandı. Final V1 için gerçek kurulum ve
tam installer yaşam döngüsü matrisi bekliyor.

**Durum:** Devam ediyor; MSI gömülü tek dosyalık test kurucusu hazır, gerçek kurulum yaşam döngüsü testi bekliyor.

26 Ağustos 2026'da `smartcab` sorunu Windows'un yerel CAB araçlarının Türkçe
karakter içeren kullanıcı geçici yolunu güvenilir işleyememesine kadar indirildi.
Build kapsamındaki `TEMP`/`TMP`, WiX girdi, ara ve çıktı yolları ASCII staging
dizinine alınarak paketleme yaklaşık dokuz saniyede tamamlandı. Dağıtıma kapalı,
Debug geçitli imzasız MSI için ayrı `build-test-installer.ps1` komutu eklendi;
üretilen paket ve SHA-256 çıktısı release artifact'lerinden ayrıldı.
Ardından son kullanıcı için MSI'yı içinde taşıyan self-contained
`Kvieta-Setup-<version>.exe` üretimi eklendi; bağımsız MSI sessiz ve yönetilen
dağıtımlar için korunmaya devam ediyor.

27 Ağustos 2026'da Debug test paketinden teknik olarak ayrı, development bypass
içermeyen Release konfigürasyonlu `community` paket türü eklendi. Bu hat imzasız
dağıtımı manifestte açıkça belirtir ve SmartScreen sonucunu saklamaz.

Paket kalite kapısı MSI veritabanından ürün/sürüm/upgrade kimliğini, gömülü CAB'ı,
Guardian servis kaydını ve uygulama içi kaldırma girişini doğrular. Setup dosya
metadata'sı ile gömülü MSI ayrıca sınanır. Şema v2 release manifesti kaynak commit'i,
paket türünü, dosya boyutlarını ve iki artifact'in SHA-256 değerini birbirine bağlar.

Yapılacaklar:

- Alpha 2.1 community paketinde temiz kurulum, açılış, Guardian, repair ve kaldırma akışını doğrulama.
- Build süresini release raporuna ekleme; EXE/MSI boyutları artık manifestte kayıtlı.
- Community paketinin kullanıcı tarafı SHA-256 doğrulama adımlarını yayın metnine bağlama.
- Temiz kurulum, upgrade, repair, uninstall, rollback ve downgrade engelini tekrar çalıştırma.
- MSI hatasında kullanıcı verisi ile korunan policy alanlarının bozulmadığını doğrulama.

Kabul kriteri:

- Temiz checkout'tan tek belgelenmiş komutla aynı sürüm paketi üretilebiliyor.
- Paketleme takılmadan tamamlanıyor ve hatada açık tanılama veriyor.
- Dosya, manifest, boyut, SHA-256 ve sürüm bilgileri birbiriyle eşleşiyor.

#### 7. Açıklamalı kurulum sihirbazı

**Durum:** Devam ediyor; tam sihirbaz uygulandı, gerçek temiz kurulum/upgrade doğrulaması bekliyor.

26 Ağustos 2026'da Windows açık/koyu uygulama temasını canlı izleyen, ilk adımda
Türkçe/English seçtiren ve bütün sonraki metinleri seçilen dilde gösteren markalı
WPF kurucu tamamlandı. Kurucu Kvieta'yı ve yerel veri sınırını açıklar; mevcut ayarı
algılar; korumasız kullanıcıya mevcut ayarları koruma veya yeniden yapılandırma
seçeneği sunar; Guardian politikasının kurucu üzerinden gevşetilmesini engeller.
Yeni yapılandırmada üç kullanım biçimi, kişisel koruma seviyesi, cihaz adı, günlük
süre, yerel ölçüm, Windows başlangıcı, masaüstü kısayolu ve gerektiğinde yönetici
PIN'i alınır. Özet onayından sonra yönetici izni yalnız MSI işlemi için istenir,
ayarlar yalnız başarılı kurulumdan sonra atomik olarak kaydedilir ve Kvieta seçilen
modun doğru başlangıç yüzeyiyle açılır.

Kurucu artık Windows Installer tarafından kaydedilmiş mevcut sürümü başlangıçta
algılar. Kurulu cihazda dil ve tanıtım adımlarını tekrar göstermez; doğrudan sürüm
karşılaştırmalı Güncelle/Onar ekranını açar, kullanıcı verilerini korur ve eski
paketin daha yeni kurulumu düşürmesini engeller.

Yapılacaklar:

- Alpha 2.1'de eklenen haftalık planın özet, kaydetme ve ilk açılış sonucunu
  gerçek temiz kurulumda doğrulama.
- Kullanıcıya teknik mod adı sormak yerine Farkındalık, Kişisel ve Aile
  amaçlarını eylem odaklı kartlarla seçtirme; amaca uygun başlangıç şablonu ve
  ilk ritim hedefini kurulum tamamlanmadan özetleme.
- Temiz kurulum ve Alpha 2.1 → V1 upgrade akışını gerçek kurucu üzerinden doğrulama.
- Kişisel · Korumalı ve Aile seçimlerinde MSI sonrası Guardian enrollment ve
  doğru oturum yüzeyi açılışını doğrulama.
- Kurulum iptali/hatasında ayarların değişmediğini ve tanılama logunun kaldığını doğrulama.
- Upgrade, repair ve uninstall öncesinde kullanıcı verisine ne olacağını açıklama.

Kabul kriteri:

- İlk kullanıcı Guardian, kullanım biçimi ve veri sonuçlarını anlayarak seçim yapabiliyor.
- Sessiz kurulum ve kurumsal `msiexec` özellikleri korunuyor.

#### 8. Public paket güveni

**Alpha 2.1 güncellemesi:** İmzasız Release community paketi Debug/test paketinden
teknik olarak ayrıldı; release commit'i, manifest, SHA-256 ve paket metadata'sı
yayın öncesinde doğrulandı. Gerçek kurulu paket kimliği ve V1 geçiş testi açık.

**Durum:** Kod modeli uygulandı; gerçek kurulu community paket testi bekliyor. Ticari sertifika satın alınmayacak.

Proje kişisel ve ticari olmayan bir açık kaynak çalışma olarak yayımlanacak. Bu
nedenle V1 için ücretli code-signing sertifikası zorunlu tutulmayacak. Bu karar,
Development test geçitlerinin public pakete taşınmasına veya bütünlük kontrolünün
kaldırılmasına izin vermez. Windows SmartScreen uyarısı ve doğrulanmış yayıncı
kimliğinin bulunmaması kullanıcıya açıkça anlatılacaktır.

Yapılacaklar:

- Release community paketinin test/development paketinden teknik ayrımını koruma.
- Community MSI metadata'sına paket türü ve kurulu EXE SHA-256 kimliğini yazma; Guardian'ın yalnız installer-managed, exact path, sürüm ve hash eşleşen istemciyi kabul etmesini sağlama.
- Son release commit'inde EXE, MSI, manifest, SHA-256 ve kaynak commit'inin aynı build'e ait olduğunu yeniden doğrulama.
- Guardian'ın Development bypass kullanmadan yalnız installer'ın kurduğu beklenen istemciyi kabul edeceği bütünlük modelini tamamlama.
- Değiştirilmiş veya farklı kaynaktan gelen istemcinin Guardian tarafından reddedildiğini test etme.
- SmartScreen uyarısını ve SHA-256 doğrulamasını Türkçe/İngilizce belgeleme.
- `SECURITY.md` ve Türkçe eşini gerçek unsigned-community Guardian kimlik
  modeliyle eşleme; eski yalnız Authenticode imzalayanına dayalı iddiayı kaldırma.
- GitHub private vulnerability reporting kanalını açma ve iki dilli ilk temas
  yolunu hassas ayrıntıları public issue'ya taşımayacak biçimde güncelleme.
- İleride ücretsiz veya uygun bir güvenilir imzalama yolu oluşursa Authenticode'u ek sertleştirme olarak yeniden değerlendirme.

Kabul kriteri:

- Public paket development/test unlock geçidi içermez.
- Manifest, SHA-256, kaynak commit'i ve paket metadata'sı birbiriyle eşleşir.
- Guardian değiştirilmiş veya installer dışı istemciyi reddeder; community build için belgelenen kimlik modeli gerçek Windows testinden geçer.
- Kullanıcı imzasız dağıtımın SmartScreen ve yayıncı kimliği sonuçlarını kurmadan önce görebilir.

#### 9. Gerçek Windows yaşam döngüsü matrisi

**Durum:** Kısmen tamamlandı; final matris açık.

`Win+L`, oturum açma, uyku ve uyanma event'leri sıralı ve test edilebilir bir
yaşam döngüsü policy'sine bağlandı. Uyku öncesi aktif sayaç atomik kaydedilerek
duraklatılır; yalnız kilit ekranına girilmemiş normal uyanmada devam eder. Kilit
sonrası kullanıcı kontrollü Mola korunur ve yüzey/kalkan topolojisi yenilenir.
Olay sonuçları içerik toplamadan güvenlik audit kaydına eklenir.

Gerçek `Win+L` ve uyku/uyanma testi kullanıcı isteğiyle sonraki doğrulama turuna
ertelendi; final v1 matrisi tamamlanmadan bu madde kapatılmayacak.

Zorunlu senaryolar:

- Standart kullanıcı ve ayrı yönetici hesabı.
- Guardian service stop, kill, crash ve recovery.
- Kvieta süreç kill/crash ve korunan oturumun geri gelmesi.
- Reboot, `Win+L`, uyku, hibernation ve güç kesintisi sonrası açılış.
- Explorer restart, kullanıcı değiştirme ve Remote Desktop.
- Tek/çoklu monitör, ekran takma/çıkarma, DPI ve çözünürlük değişimi.
- Bozuk JSON/yedek, disk dolu ve yazma izni kaybı.
- Temiz kurulum, upgrade, repair, uninstall, rollback ve downgrade denemesi.
- Türkçe/İngilizce, açık/koyu/system tema ve Reduce Motion.

Her test için ön koşul, adımlar, beklenen sonuç, gerçek sonuç, build SHA'sı ve kanıt
saklanır. Manuel testler yalnız “denendi” olarak değil, tekrar edilebilir test vakası
olarak belgelenir.

Tekrar kullanılabilir koşu tablosu `docs/V1-TEST-MATRIX.md` altında hazırlandı;
gerçek cihaz sonuçları bu matrise işlenecek.

### P1 — Release kalitesi ve proje altyapısı

P0 işleriyle paralel ilerleyebilir; final public release öncesinde tamamlanması hedeflenir.

#### CI ve otomatik kalite kapıları

**Durum:** GitHub Actions kalite hattı gerçek Windows runner üzerinde başarıyla doğrulandı ve genişletiliyor.

Her `main` push'u, pull request ve elle başlatılan koşu Windows üzerinde restore ve
NuGet audit, format doğrulaması, Debug/Release build, iki yapı türünde smoke test,
public single-file publish doğrulaması, test installer üretimi, MSI metadata ve
release manifesti doğrulaması çalıştırır. Başarılı test paketi
yedi gün saklanan CI artifact'i olarak yüklenir; aynı branch'teki eski koşular iptal
edilerek gereksiz kaynak tüketimi önlenir.

- İlk runner koşusunda bulunan saat dilimi bağımlılığı giderildi; takip eden koşu tamamen geçti.
- Public assembly'de development unlock metodu, sembolü veya kullanıcı metni bulunmadığını ikili çıktı üzerinden doğrulama tamamlandı; bu kontrol CI kalite kapısına bağlandı.
- NuGet zafiyet taraması ve haftalık NuGet/GitHub Actions Dependabot bildirimi eklendi.
- Self-contained public publish çıktısının tek EXE, PE kimliği, boyut ve sürüm doğrulaması eklendi.
- İmzalama sırlarını yalnız korumalı release ortamında kullanma.
- Başarısız kalite kapısıyla release oluşturulmasını engelleme.

#### Test mimarisini güçlendirme

**Mevcut eksik:** 190 doğrulama tek, büyük console smoke-test dosyasında bulunuyor.

- Core testlerini konu bazlı birim testlerine ayırma.
- Session, schedule, persistence, migration, recovery, clock ve policy testlerini bağımsızlaştırma.
- Guardian IPC ve installer için entegrasyon test katmanı oluşturma.
- WPF yaşam döngüsü ve temel kullanıcı yolculuklarına UI otomasyonu ekleme.
- Satır/branch coverage ölçümü ve kritik güvenlik yolu kapsamı ekleme.
- Flaky Windows testleri için etiket, tekrar stratejisi ve tanılama çıktısı belirleme.

#### Erişilebilirlik ve sunum tabanı

**Durum:** V1 kapsamına alındı; temel klavye, büyük metin ve yardımcı teknoloji doğrulaması bekliyor.

- Ana kullanıcı yolculuklarının fare olmadan tamamlanabilmesini sağlama.
- Mantıklı odak sırası, görünür odak durumu ve modal pencere odağını doğrulama.
- Buton, alan, grafik ve durum göstergelerine anlamlı erişilebilir adlar ekleme.
- Windows yüksek kontrast, yüzde 200 DPI/büyük metin ve Reduce Motion davranışını test etme.
- Türkçe ve English metinlerde kırpılma, taşma veya erişilemeyen eylem bırakmama.

#### Performans ve uzun kullanım dayanıklılığı

**Durum:** V1 kapsamına alındı; ölçüm bütçesi ve uzun koşu kanıtı bekliyor.

- Soğuk/sıcak uygulama açılışı, Kontrol Merkezi ve Oturum Yüzeyi açılış
  sürelerini aynı donanım ve build bilgisiyle kaydetme.
- Normal takip, odak ve Guardian kullanımında bellek, handle, CPU ve disk yazımını
  ölçme; sürekli büyüme veya agresif polling bırakmama.
- Gece yarısı, hafta değişimi, kilit, uyku, uygulama/Guardian restart ve
  birkaç günlük gerçek kullanımda çift sayım veya restart döngüsü oluşmadığını doğrulama.
- V1 için ölçülmüş kabul bütçelerini test makinesi bilgisiyle belgeleyip
  belirgin regresyonları release engeli olarak ele alma.

#### Açık kaynak ve proje yönetişimi

**Durum:** Temel yönetişim tamamlandı; özel güvenlik kanalı V1 öncesinde eklenecek.

- MIT lisansı `LICENSE` dosyasıyla eklendi.
- README, contribution ve destek belgelerinde lisans, destek ve güvenlik yolları belirtildi.
- İki dilli issue şablonları ve güvenlik/kalite kontrol listeli pull request şablonu eklendi.
- Katkıların test, güvenlik, gizlilik ve iki dilli belge eşliği gereksinimleri netleştirildi.
- GitHub private vulnerability reporting etkinleştirilecek; iki dilli güvenlik
  belgeleri public issue'ya hassas ayrıntı yazmadan kullanılacak ilk temas yolunu gösterecek.

#### Dokümantasyon tutarlılığı

**Durum:** Ana kullanıcı belgeleri eşitlendi; desteklenen Windows sürüm matrisi final testini bekliyor.

- Türkçe ve İngilizce README üç kullanım biçimi, alpha durumu ve tamamlanan Application Identity davranışıyla eşitlendi.
- İki dilli kurulum, ilk kullanım, recovery, update ve uninstall rehberleri eklendi.
- Alpha sınırları, destek yolu, MIT lisansı ve güvenlik bildirim sınırı belgelendi.
- Eski iki-mod/RC iddialarını ve eksik iki dilli rehberleri yakalayan dokümantasyon kalite kapısı CI'a eklendi.
- Desteklenen Windows sürümleri, gerçek cihaz matrisi tamamlandığında kanıtla belirtilecek.
- Release notes, tag ve GitHub Release metinleri her yayın öncesinde ayrıca eşitlenecek.
- Farkındalık/Kişisel/Aile ile Esnek/Dengeli/Korumalı sözlüğü güncel
  kullanıcı belgelerinde zorunlu tutulacak; tarihsel release notes kapsam dışı kalacak.
- Yayın sonrası checklist gerçekliğini ROADMAP ile uzlaştıran zorunlu docs-only
  kapanış adımı release sürecine eklenecek.

#### Gözlemlenebilirlik ve desteklenebilirlik

- Uygulama sürümü, paket türü, kaynak commit'i ve kirli çalışma ağacı durumu
  assembly metadata'sına gömülüyor; Ayarlar ve tanılama raporunda gösteriliyor.
- Gizlilik güvenli tanılama paketine ilgili yaşam döngüsü olaylarını ekleme.
- PIN, recovery code, pencere başlığı, belge, site veya yazılan içerik kaydetmeme.
- Güvenlik audit kaydı 30 gün, 500 olay ve 256 KB ile sınırlandı; bozuk veya
  geçersiz olaylar tanılama dışa aktarımında güvenli biçimde atlanıyor.
- Kullanıcıya tanılama raporunu kolayca dışa aktarma yolu sunma.

## `v1.0.0` çıkış tanımı

Final sürüm ancak aşağıdaki koşulların tamamı sağlandığında yayınlanır:

- P0 release engellerinin tamamı kapalı ve kanıtlıdır.
- Farkındalık, Kişisel ve Aile amaçları ile Esnek, Dengeli ve Korumalı
  seviyeler arayüz, kod, migration ve belgelerde tutarlıdır.
- Bugün ekranı, hızlı odak eylemleri, başlangıç şablonları, uygulama kartından
  kural oluşturma, Ritim Serisi ve Haftalık Ritim Özeti kabul kriterlerini geçer.
- Ritim Serisi dinlenme, koruyucu, recovery ve güvenilir saat senaryolarında
  doğru çalışır; güvenlik politikasını veya yönetici yetkisini etkilemez.
- Build, format, smoke, birim ve zorunlu entegrasyon testleri geçer.
- Dengeli, Kişisel · Korumalı ve Aile kaçış matrisi geçer.
- Çoklu monitör/DPI ve Windows yaşam döngüsü matrisi tamamlanır.
- Installer kurulum, upgrade, repair, uninstall ve rollback testlerini geçer.
- Alpha 2.1 → V1 ayar, geçmiş, recovery ve protected policy migration'ı veri kaybı olmadan geçer.
- Temel klavye, ekran okuyucu, yüksek kontrast, büyük metin ve Reduce Motion matrisi geçer.
- Belgelenmiş performans bütçesi ve birkaç günlük uzun kullanım koşusu engelleyici regresyon göstermez.
- Public community paketinin manifest, SHA-256, commit ve Guardian istemci kimliği doğrulaması geçer.
- Public build development/test unlock geçidi içermez.
- Lisans, güvenlik, kurulum, kullanım ve recovery belgeleri ile private güvenlik bildirim yolu hazırdır.
- GitHub Release doğru notları, SHA-256, manifest, installer ve imzasız dağıtım uyarısını içerir.
- `v1.0.0` etiketi test edilen release commit'ine atanır.

## Önerilen uygulama sırası

1. Alpha 2.1'i ayrı Windows cihazında yükseltme, kullanım ve Guardian saha testinden geçir; bulguları kaydet.
2. Ürün sözlüğünü Farkındalık/Kişisel/Aile ve Esnek/Dengeli/Korumalı
   olarak uygula; `CafeWindow`/`CafeViewModel` dahil eski teknik adları migration güvencesiyle temizle.
3. Bugün deneyimi, hızlı odak, başlangıç şablonları ve uygulama kartından
   kural oluşturma akışlarını tamamla.
4. Tek aktif hedefli Ritim Serisi, dinlenme günü, Ritim Koruyucu, kilometre
   taşları ve Haftalık Ritim Özeti'ni yerel-first olarak tamamla.
5. Kullanıcı özelliklerini Türkçe/English, klavye, yüksek kontrast, Reduce Motion
   ve yüzde 100–200 DPI matrisinde doğrula.
6. Dengeli/Korumalı/Aile kaçış, Guardian, Windows yaşam döngüsü, çoklu
   monitör ve installer/migration matrisini gerçek Windows üzerinde tamamla.
7. Açılış/kaynak bütçesini ve birkaç günlük uzun kullanım koşusunu kaydet;
   engelleyici performans, sayaç veya restart regresyonlarını kapat.
8. Private güvenlik bildirimini aç; SECURITY, README, kullanım rehberleri,
   release notes ve ROADMAP'i gerçek davranışla iki dilde eşitle.
9. Özellik kapsamını dondurup `v1.0.0-rc.1` paketini temiz commit'ten üret;
   format, audit, build, test, manifest, SHA-256 ve public bypass kapılarını çalıştır.
10. V1 test matrisini gerçek RC paketiyle doldur; yalnız blocker ve regresyon
    düzeltmeleri için gerekirse `rc.2` üret.
11. Final paketi test edilen commit'ten üretip tag, manifest, hash ve Guardian
    kimliğini yeniden doğrula; `v1.0.0` release'ini yayınla.
12. Yayın sonrası ROADMAP checklist'ini gerçek release kanıtıyla kapatan ayrı
    dokümantasyon uzlaştırmasını tamamla.

## v1 sonrası plan

Sürüm numaraları yön gösterir; kullanıcı geri bildirimi ve v1 stabilizasyonuna göre
yeniden sıralanabilir. Yerel çalışma ve hesap zorunluluğu olmaması ilkesi korunur.

### v1.0.x — Stabilizasyon ve uyumluluk

- Public sürümden gelen crash, installer ve Guardian regresyonlarını düzeltme.
- Desteklenen Windows sürümleri için uyumluluk tablosunu genişletme.
- ARM64 teknik fizibilitesi ve paketleme değerlendirmesi.
- V1 erişilebilirlik ve performans tabanını yeni ekranlar ve Windows sürümleri için genişletme.
- Migration ve rollback senaryolarını her patch release'te yeniden doğrulama.

### v1.1 — Daha yararlı yerel farkındalık

- Birden fazla isteğe bağlı hedef ve daha ayrıntılı hedef geçmişi.
- Haftalık görevler ve gelişmiş, açıklanabilir yerel öneriler.
- Ritim geçmişi takvimi, ek filiz/yaprak/çiçek görselleri ve paylaşım kartı çeşitleri.
- Kullanıcı kontrollü bildirim zamanı ve hedef duraklatma davranışı.
- Bütün analizleri cihazda tutma.

### v1.2 — Tarayıcı ve site kuralları

- İsteğe bağlı tarayıcı eklentisi için tehdit, gizlilik ve izin modelini tasarlama.
- Site kategorisi, süre limiti ve engelleme davranışını açıklama.
- Tam URL veya sayfa içeriği saklamayan minimum veri modelini araştırma.
- Eklenti yokken masaüstü uygulamasının normal çalışmasını koruma.
- Chrome, Edge ve Firefox desteğini ayrı değerlendirme.

### v1.3 — İnternet üzerinden güvenilir telefon doğrulaması

**Ön koşul:** Ürünün isim ve marka değişikliği tamamlanmadan alan adı, site adresi
ve kalıcı servis kimlikleri oluşturulmaz.

- İlk sürümü özel alan adı satın almadan ücretsiz Cloudflare Pages/Workers ve
  SQLite tabanlı Durable Objects kotasıyla yayınlama.
- Telefonun aynı yerel ağda bulunmasını gerektirmeyen, masaüstü uygulamasının
  dışarı doğru açtığı HTTPS/WebSocket bağlantısıyla çalışan relay mimarisi kurma.
- Hesap açmayı zorunlu tutmadan QR ile kısa ömürlü, tek kullanımlık eşleştirme ve
  güvenilir telefon iptali sağlama.
- PIN'i, yeni PIN'i, recovery kodunu, planları veya kullanım verisini sunucuya
  göndermeme; telefonun yalnız tek kullanımlık doğrulama isteğini cihaz anahtarıyla
  imzalamasını ve PIN'in bilgisayarda belirlenmesini sağlama.
- Eşleştirmede iki ekranda karşılaştırma kodu; isteklerde süre sonu, nonce,
  replay engeli, origin kontrolü ve hız sınırı uygulama.
- Telefon tarayıcı verileri silindiğinde güvenin kaybolduğunu açıkça gösterme;
  kurtarma kodlarını çevrimdışı yedek yol olarak koruma.
- Ücretsiz kotayı korumak için relay bağlantısını yalnız eşleştirme/doğrulama
  sırasında açma; kalıcı heartbeat kullanmama ve kota aşımını izleme.
- Relay erişilemediğinde uygulamanın normal yerel çalışmasını sürdürme; telefonla
  doğrulamanın geçici olarak kullanılamadığını anlaşılır biçimde bildirme.
- Sunucuda yalnız gerekli kısa ömürlü oturum verisini tutma; saklama, silme,
  tanılama ve gizlilik sınırlarını iki dilde belgeleme.
- Özelliğin Protected güvenlik sınırını zayıflatmadığını gerçek telefon, farklı
  Wi-Fi/mobil veri, bağlantı kesilmesi ve tekrar oynatma senaryolarıyla doğrulama.

### v1.x — İsteğe bağlı gelişmiş Windows koruması

- Uygun Windows sürümlerinde AppLocker/WDAC entegrasyonu.
- Varsayılan yerine açıkça seçilen gelişmiş seviye sunma.
- Yanlış kuralda güvenli recovery ve yönetici geri dönüş yolu.
- Kurumsal politikaları değiştirmeden önce salt okunur uyumluluk analizi.

### v2 araştırma alanları

Bu maddeler taahhüt değil, ürün ve güvenlik kararı gerektiren araştırma alanlarıdır:

- İsteğe bağlı, uçtan uca şifreli çoklu cihaz eşitleme.
- Aile veya küçük ekip için uzaktan yönetim.
- İnternet üzerinden izin isteği ve durum görüntüleme.
- Cihazlar arası ortak plan ve kural şablonları.
- Yerel veriyi buluta taşımadan çalışan kişiselleştirilmiş öneriler.

Bulut veya uzaktan yönetim için hesap zorunluluğu, veri minimizasyonu, şifreleme,
silme, recovery, kötüye kullanım ve çocuk güvenliği modeli ayrıca onaylanmadan
uygulama geliştirmesi başlatılmaz.

## Tamamlanan kilometre taşları

### v0.15.0 — İlk ürün temeli

- WPF uygulama, yerel ayarlar, plan, günlük limit, uygulama kuralları ve temel oturum akışı.

### v0.15.1 — Veri sağlamlığı

- Süreçler arası kilit, atomik JSON yazma, doğrulama ve last-known-good kurtarma.
- Ayar ve kullanım verisi migration sistemi.
- Eşzamanlı sayaç ve geçmiş birleştirme regresyonları.

### v0.16.0 — Ritim, farkındalık ve gizlilik

- Kural sayacı ile ön plan farkındalık sayacının ayrılması.
- Açık rızaya bağlı yerel uygulama takibi.
- Saklama süresi, geçmiş silme ve JSON/CSV dışa aktarma.
- Başlangıç ritmi, haftalık karşılaştırma ve azaltma hedefleri.

### v0.16.1 — Hareket ve erişilebilirlik

- Kısa geçişler, mikro animasyonlar ve Reduce Motion desteği.

### v0.17.0 — Installer, update ve rollback temeli

- Self-contained uygulama ve WiX MSI yapısı.
- Program Files, kısayollar ve Guardian servis yaşam döngüsü.
- Manifest, SHA-256, imza doğrulama, update, rollback ve downgrade engeli.

### v0.18.0 — Recovery ve güvenlik sertleştirmesi

- Public/development build ayrımı.
- Tek kullanımlık recovery kodları ve Windows yönetici doğrulaması.
- Guardian IPC nonce/HMAC/replay koruması ve kalıcı throttling.
- Monotonic zaman doğrulaması ve Application Identity 2.0.

### v0.19.0 — Tanılama ve Guardian güvenilirliği

- Guardian sağlık ve sürüm uyumluluğu kontrolleri.
- Gizlilik güvenli tanılama dışa aktarma.
- Installer repair, policy recovery ve Guardian kill/crash iyileştirmeleri.

### v1.0.0-alpha — Kullanım biçimleri ve alpha temeli

- Üç kullanıcı odaklı kullanım biçimi.
- Flexible, Balanced ve Guardian destekli Guarded kişisel koruma.
- Flexible manuel odak kronometresi.
- Tek oturum yüzeyi, Control Center geçiş korumaları ve startup düzeltmeleri.
- Final release engellerinin açıkça belgelenmesi.

### v1.0.0-alpha.1 — İlk GitHub prerelease (yayımlandı; saha testinde)

- Development bypass içermeyen imzasız Release community paketi.
- Tek dosyalı Setup EXE, MSI, SHA-256 ve commit bağlı release manifesti.
- Korumalı modun yalnız Kaydet sonrası uygulanması ve süre dolma çıkış
  döngüsünün kaldırılması.
- Yayın sonrası minimum kurulum/Guardian kapısı ve bir veya iki günlük gerçek
  cihaz kullanım testi.
- Tam Windows, DPI, installer ve UI otomasyon matrislerinin final V1 planında açık tutulması.

## Roadmap bakım kuralları

- Her aktif madde `Açık`, `Devam ediyor`, `Engelli` veya `Tamamlandı` durumuna sahip olur.
- Bir madde yalnız kod yazıldığında değil, test ve belge kanıtı tamamlandığında kapanır.
- Yeni release engelleri önce bu belgeye eklenir; final tanımı sessizce gevşetilmez.
- Tamamlanan ayrıntılar release notes'a taşınır; roadmap aktif işleri görünür tutar.
- Her release candidate sonrasında doğrulanmış durum ve test tarihi güncellenir.
