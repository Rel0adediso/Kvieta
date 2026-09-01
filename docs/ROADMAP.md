# Kvieta — Ürün ve Release Yol Haritası

**Son güncelleme:** 31 Ağustos 2026

**Mevcut yayın:** **Kvieta Alpha 2** GitHub prerelease

**Aktif hedef:** Alpha 2 saha geri bildirimi ve final `v1.0.0` Windows doğrulama matrisi

**Yayın:** [Kvieta Alpha 2](https://github.com/Rel0adediso/Kvieta/releases/tag/alpha-2)

Bu belge Kvieta'nın bağlayıcı geliştirme sırasını, bilinen eksiklerini, release
kriterlerini ve v1 sonrasındaki ürün yönünü tanımlar. Tamamlanan çalışmalar kısa
bir tarihçe olarak belgenin sonunda tutulur; günlük geliştirme önceliği için
öncelikle **Aktif çalışma planı** bölümü esas alınır.

## Ürün ilkeleri

- Kvieta hesap veya bulut zorunluluğu olmadan yerel çalışır.
- Kullanım verisi, planlar, kurallar ve tanılama kayıtları varsayılan olarak cihazda kalır.
- **Sadece takip** hiçbir kısıtlama uygulamadan kullanım farkındalığı sağlar.
- **Kendim için** kullanıcıyı cezalandırmadan kendi kararına sadık kalmasına yardım eder.
- **Yönettiğim biri için** standart Windows kullanıcısının yaygın kaçış yollarına direnç gösterir.
- Güvenlik iddiaları açık, test edilebilir ve belgelenmiş sınırlar içinde tutulur.
- Arayüz krem/haki ve zeytin/grafit kimliğini, sakin ürün dilini ve kompakt bilgi hiyerarşisini korur.
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

## Kvieta Alpha 2 yayın planı

İkinci community prerelease kullanıcıya **Kvieta Alpha 2** adıyla sunulur. Git tag
`alpha-2`, paket etiketi `Alpha-2` olur; `v1.0.0-alpha.2` kullanıcıya görünen yayın
adı olarak kullanılmaz. Windows Installer ürün sürümü yükseltme/onarım uyumluluğu
için `1.0.0` kalır.

### Alpha 2 kapsamı

- İsteğe bağlı güvenilir telefon eşleştirme, QR aktarımı ve telefonla PIN kurtarma.
- Yönetici çıkışı, Control Center/session tekilleştirme ve Guardian geçiş düzeltmeleri.
- Eski Alpha kurulumunu güncelleme, onarma, yeniden yapılandırma ve kaldırma akışları.
- Guardian başlatma/onarım, protected policy aktarımı, dosya kilidi ve recovery düzeltmeleri.
- Kurulum PIN akışı, açıklamalar, hata durumları ve Kvieta teması iyileştirmeleri.
- Uygulama kuralları, oturum ekranı ve güç menüsü regresyon düzeltmeleri.

### Alpha 2 yayın kapısı

- [x] Çalışma ağacında geçici çıktı, sır veya yanlışlıkla eklenen makineye özel dosya bulunmadığı doğrulandı.
- [x] NuGet audit, format, dokümantasyon, Debug/Release build ve smoke testleri temiz geçti.
- [x] Public-build bypass kontrolü ve companion web bundle üretimi geçti.
- [x] `Alpha-2` community Setup EXE/MSI metadata, manifest ve SHA-256 kontrollerini geçti.
- [x] Release commit'inden temiz community paketi yeniden üretildi; manifest commit'i `alpha-2` tag'iyle eşleşiyor.
- [x] GitHub Release **Kvieta Alpha 2** başlığıyla prerelease olarak yayımlandı.
- [x] README indirme bağlantıları ve SHA-256 değeri yayımlanan son paketle güncellendi.

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

#### 1. Balanced oturum yüzeyi ve masaüstü kaçışları

**Durum:** Devam ediyor; ilk pencere recovery sertleştirmesi uygulandı, gerçek Windows matrisi açık.

Balanced kişisel modda gösterilen oturum yüzeyi yalnız `Topmost` ve `Maximized`
pencere davranışına dayanıyor. Pencere küçültülebiliyor, arkaya gönderilebiliyor
veya masaüstü geçişleriyle aşılabiliyor.

26 Ağustos 2026'da tamamlanan ilk sertleştirme:

- Zorunlu tam ekran yüzey minimize edildiğinde anında maximize durumuna dönüyor.
- Pencere odağı kaybedildiğinde recovery işlemi UI kuyruğundan tekrar değerlendiriliyor.
- Recovery kararı ayrı ve test edilebilir bir policy'ye taşındı.
- Aktif oturum widget'ı, Sadece takip, Kontrol Merkezi, modal doğrulama ve yüzey geçişleri recovery dışında tutuldu.
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
- Flexible modun kullanıcı kontrollü davranışı yanlışlıkla sertleştirilmiyor.

#### 2. Çoklu monitör, DPI ve ekran yaşam döngüsü

**Durum:** Devam ediyor; temel iki fiziksel ekran testi geçti, genişletilmiş topoloji ve DPI matrisi alpha.1 sonrasına ertelendi.

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

#### 3. Installer üretim hattı

**Alpha.1 güncellemesi:** Development bypass içermeyen Release community-alpha
hattı hazır. Alpha yayın kapısı için minimum gerçek kurulum testi; final V1 için
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

- Alpha.1 community paketinde temiz kurulum, açılış, Guardian, repair ve kaldırma akışını doğrulama.
- Build süresini release raporuna ekleme; EXE/MSI boyutları artık manifestte kayıtlı.
- Community paketinin kullanıcı tarafı SHA-256 doğrulama adımlarını yayın metnine bağlama.
- Temiz kurulum, upgrade, repair, uninstall, rollback ve downgrade engelini tekrar çalıştırma.
- MSI hatasında kullanıcı verisi ile korunan policy alanlarının bozulmadığını doğrulama.

Kabul kriteri:

- Temiz checkout'tan tek belgelenmiş komutla aynı sürüm paketi üretilebiliyor.
- Paketleme takılmadan tamamlanıyor ve hatada açık tanılama veriyor.
- Dosya, manifest, boyut, SHA-256 ve sürüm bilgileri birbiriyle eşleşiyor.

#### 4. Açıklamalı kurulum sihirbazı

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

- İlk kurulum sihirbazına haftanın günlerini ve izin verilen saat aralıklarını
  seçmeye yarayan plan oluşturma/düzenleme adımı ekleme; özet ekranında seçilen
  planı kurulumdan önce açıkça gösterme.
- Temiz kurulum ve 1.0.0 → 1.0.1 upgrade akışını gerçek kurucu üzerinden doğrulama.
- Korumalı/Gözetimli seçimde MSI sonrası Guardian enrollment ve oturum açılışını doğrulama.
- Kurulum iptali/hatasında ayarların değişmediğini ve tanılama logunun kaldığını doğrulama.
- Upgrade, repair ve uninstall öncesinde kullanıcı verisine ne olacağını açıklama.

Kabul kriteri:

- İlk kullanıcı Guardian, kullanım biçimi ve veri sonuçlarını anlayarak seçim yapabiliyor.
- Sessiz kurulum ve kurumsal `msiexec` özellikleri korunuyor.

#### 5. Public paket güveni

**Alpha.1 güncellemesi:** İmzasız Release community paketi Debug/test paketinden
teknik olarak ayrıldı. Son release commit'inde bütünlük doğrulamasının yeniden
çalıştırılması ve SmartScreen/SHA-256 yayın metni bekliyor.

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
- İleride ücretsiz veya uygun bir güvenilir imzalama yolu oluşursa Authenticode'u ek sertleştirme olarak yeniden değerlendirme.

Kabul kriteri:

- Public paket development/test unlock geçidi içermez.
- Manifest, SHA-256, kaynak commit'i ve paket metadata'sı birbiriyle eşleşir.
- Guardian değiştirilmiş veya installer dışı istemciyi reddeder; community build için belgelenen kimlik modeli gerçek Windows testinden geçer.
- Kullanıcı imzasız dağıtımın SmartScreen ve yayıncı kimliği sonuçlarını kurmadan önce görebilir.

#### 6. Gerçek Windows yaşam döngüsü matrisi

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

#### Açık kaynak ve proje yönetişimi

**Durum:** Temel yönetişim tamamlandı; özel güvenlik kanalı daha sonra eklenecek.

- MIT lisansı `LICENSE` dosyasıyla eklendi.
- README, contribution ve destek belgelerinde lisans, destek ve güvenlik yolları belirtildi.
- İki dilli issue şablonları ve güvenlik/kalite kontrol listeli pull request şablonu eklendi.
- Katkıların test, güvenlik, gizlilik ve iki dilli belge eşliği gereksinimleri netleştirildi.
- Özel güvenlik bildirim kanalı bulunana kadar hassas ayrıntı içermeyen ilk temas yolu belgelendi.

#### Dokümantasyon tutarlılığı

**Durum:** Ana kullanıcı belgeleri eşitlendi; desteklenen Windows sürüm matrisi final testini bekliyor.

- Türkçe ve İngilizce README üç kullanım biçimi, alpha durumu ve tamamlanan Application Identity davranışıyla eşitlendi.
- İki dilli kurulum, ilk kullanım, recovery, update ve uninstall rehberleri eklendi.
- Alpha sınırları, destek yolu, MIT lisansı ve güvenlik bildirim sınırı belgelendi.
- Eski iki-mod/RC iddialarını ve eksik iki dilli rehberleri yakalayan dokümantasyon kalite kapısı CI'a eklendi.
- Desteklenen Windows sürümleri, gerçek cihaz matrisi tamamlandığında kanıtla belirtilecek.
- Release notes, tag ve GitHub Release metinleri her yayın öncesinde ayrıca eşitlenecek.

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
- Build, format, smoke, birim ve zorunlu entegrasyon testleri geçer.
- Balanced ve Protected kaçış matrisi geçer.
- Çoklu monitör/DPI ve Windows yaşam döngüsü matrisi tamamlanır.
- Installer kurulum, upgrade, repair, uninstall ve rollback testlerini geçer.
- Public community paketinin manifest, SHA-256, commit ve Guardian istemci kimliği doğrulaması geçer.
- Public build development/test unlock geçidi içermez.
- Lisans, güvenlik, kurulum, kullanım ve recovery belgeleri hazırdır.
- GitHub Release doğru notları, SHA-256, manifest, installer ve imzasız dağıtım uyarısını içerir.
- `v1.0.0` etiketi test edilen release commit'ine atanır.

## Önerilen uygulama sırası

1. Alpha 2 release notes, iki dilli durum metinleri ve yayın adını kesinleştir.
2. Çalışma ağacında gizli, geçici veya makineye özel dosya olmadığını doğrula.
3. Build, smoke, format, NuGet audit, dokümantasyon ve public bypass kapılarını
   çalıştır; `Alpha-2` etiketli community paket hattını sınama amacıyla doğrula.
4. Değişiklikleri tek release commit'inde birleştir.
5. Community Setup EXE'yi temiz commit'ten yeniden üret; manifest, SHA-256 ve
   kaynak commit eşleşmesini doğrula.
6. `alpha-2` annotated tag'ini oluştur ve **Kvieta Alpha 2** GitHub prerelease'ini
   paket varlıkları, imzasız yayın uyarısı ve bilinen sınırlarla yayınla.
7. README indirme bağlantısı ile SHA-256 değerini yayımlanan paketle eşitle.
8. Alpha 2 geri bildirimlerinden sonra kalan Windows yaşam döngüsü, DPI,
   kaçış-yolu ve installer matrisini tamamlayıp final `v1.0.0` kapısına devam et.

## v1 sonrası plan

Sürüm numaraları yön gösterir; kullanıcı geri bildirimi ve v1 stabilizasyonuna göre
yeniden sıralanabilir. Yerel çalışma ve hesap zorunluluğu olmaması ilkesi korunur.

### v1.0.x — Stabilizasyon ve uyumluluk

- Public sürümden gelen crash, installer ve Guardian regresyonlarını düzeltme.
- Desteklenen Windows sürümleri için uyumluluk tablosunu genişletme.
- ARM64 teknik fizibilitesi ve paketleme değerlendirmesi.
- Yüksek kontrast, ekran okuyucu, klavye navigasyonu ve büyük metin iyileştirmeleri.
- Installer ve uygulama açılış süresini ölçme ve iyileştirme.
- Migration ve rollback senaryolarını her patch release'te doğrulama.

### v1.1 — Daha yararlı yerel farkındalık

- Yeterli yerel veri olduğunda tek tık uygulama kuralı önerileri.
- Güvenilir canlı öneri yenileme ve öneri nedenini açıklama.
- Haftalık eğilimleri suçlayıcı olmayan özetlere dönüştürme.
- Kullanıcı onaylı azaltma hedeflerini düzenleme ve duraklatma.
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
