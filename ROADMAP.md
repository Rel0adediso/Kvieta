# Otium — Ürün ve Release Yol Haritası

**Son güncelleme:** 26 Ağustos 2026

**Mevcut sürüm:** `v1.0.0-alpha`

**Aktif hedef:** Güvenilir, imzalı ve tekrar üretilebilir `v1.0.0` Windows sürümü

Bu belge Otium'un bağlayıcı geliştirme sırasını, bilinen eksiklerini, release
kriterlerini ve v1 sonrasındaki ürün yönünü tanımlar. Tamamlanan çalışmalar kısa
bir tarihçe olarak belgenin sonunda tutulur; günlük geliştirme önceliği için
öncelikle **Aktif çalışma planı** bölümü esas alınır.

## Ürün ilkeleri

- Otium hesap veya bulut zorunluluğu olmadan yerel çalışır.
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

**Durum:** Devam ediyor; ikincil ekran kalkanları uygulandı, fiziksel donanım testi bekliyor.

26 Ağustos 2026'da ikincil ekranları görev çubuğu dahil kaplayan, monitör
takma/çıkarma ve çözünürlük değişiminde kendini yenileyen ekran kalkanı altyapısı
eklendi. Geliştirme bilgisayarında bağlanabilir ikinci ekran bulunmadığı için bu
davranış henüz gerçek donanımda doğrulanmadı. Final `v1.0.0` öncesinde genişletilmiş
masaüstü kullanan en az iki fiziksel ekranla test edilmesi zorunludur; bu madde test
kanıtı olmadan tamamlandı sayılmaz.

Bekleyen fiziksel test:

- Birincil ekranda normal Otium kontrollerinin kalması.
- İkincil ekranların masaüstü ve görev çubuğunu göstermeyen Otium kalkanıyla kaplanması.
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

**Durum:** Devam ediyor; MSI gömülü tek dosyalık test kurucusu hazır, gerçek kurulum yaşam döngüsü testi bekliyor.

26 Ağustos 2026'da `smartcab` sorunu Windows'un yerel CAB araçlarının Türkçe
karakter içeren kullanıcı geçici yolunu güvenilir işleyememesine kadar indirildi.
Build kapsamındaki `TEMP`/`TMP`, WiX girdi, ara ve çıktı yolları ASCII staging
dizinine alınarak paketleme yaklaşık dokuz saniyede tamamlandı. Dağıtıma kapalı,
Debug geçitli imzasız MSI için ayrı `build-test-installer.ps1` komutu eklendi;
üretilen paket ve SHA-256 çıktısı release artifact'lerinden ayrıldı.
Ardından son kullanıcı için MSI'yı içinde taşıyan self-contained
`Otium-Setup-<version>.exe` üretimi eklendi; bağımsız MSI sessiz ve yönetilen
dağıtımlar için korunmaya devam ediyor.

Paket kalite kapısı MSI veritabanından ürün/sürüm/upgrade kimliğini, gömülü CAB'ı,
Guardian servis kaydını ve uygulama içi kaldırma girişini doğrular. Setup dosya
metadata'sı ile gömülü MSI ayrıca sınanır. Şema v2 release manifesti kaynak commit'i,
paket türünü, dosya boyutlarını ve iki artifact'in SHA-256 değerini birbirine bağlar.

Yapılacaklar:

- İmzasız geliştirme MSI'sında temiz kurulum, açılış, Guardian ve kaldırma akışını doğrulama.
- Build süresini release raporuna ekleme; EXE/MSI boyutları artık manifestte kayıtlı.
- Windows SDK signing tools içinden `signtool.exe` kurma.
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
WPF kurucu tamamlandı. Kurucu Otium'u ve yerel veri sınırını açıklar; mevcut ayarı
algılar; korumasız kullanıcıya mevcut ayarları koruma veya yeniden yapılandırma
seçeneği sunar; Guardian politikasının kurucu üzerinden gevşetilmesini engeller.
Yeni yapılandırmada üç kullanım biçimi, kişisel koruma seviyesi, cihaz adı, günlük
süre, yerel ölçüm, Windows başlangıcı, masaüstü kısayolu ve gerektiğinde yönetici
PIN'i alınır. Özet onayından sonra yönetici izni yalnız MSI işlemi için istenir,
ayarlar yalnız başarılı kurulumdan sonra atomik olarak kaydedilir ve Otium seçilen
modun doğru başlangıç yüzeyiyle açılır.

Yapılacaklar:

- Temiz kurulum ve 1.0.0 → 1.0.1 upgrade akışını gerçek kurucu üzerinden doğrulama.
- Korumalı/Gözetimli seçimde MSI sonrası Guardian enrollment ve oturum açılışını doğrulama.
- Kurulum iptali/hatasında ayarların değişmediğini ve tanılama logunun kaldığını doğrulama.
- Upgrade, repair ve uninstall öncesinde kullanıcı verisine ne olacağını açıklama.

Kabul kriteri:

- İlk kullanıcı Guardian, kullanım biçimi ve veri sonuçlarını anlayarak seçim yapabiliyor.
- Sessiz kurulum ve kurumsal `msiexec` özellikleri korunuyor.

#### 5. Kod imzalama ve public paket

**Durum:** Engelli; bu bilgisayarda `signtool.exe` ve code-signing sertifikası yok.

Yapılacaklar:

- Güvenilir code-signing sertifikası edinme ve private key saklama yöntemini belirleme.
- Windows SDK signing tools kurma.
- EXE, MSI, verifier ve updater'ı aynı yayıncı kimliğiyle imzalama.
- Güvenilir zaman damgası ve sertifika zinciri doğrulamasını çalıştırma.
- Guardian'ın Program Files konumu ile installer tarafından pinlenen imzayı doğrulamasını test etme.
- İmza bilgisini veya private key'i Git deposuna koymama.

Kabul kriteri:

- Bütün dağıtım dosyaları geçerli ve aynı yayıncı kimliğiyle imzalıdır.
- Değiştirilmiş, imzasız veya yanlış yayıncıya ait paketler reddedilir.

#### 6. Gerçek Windows yaşam döngüsü matrisi

**Durum:** Kısmen tamamlandı; final matris açık.

Zorunlu senaryolar:

- Standart kullanıcı ve ayrı yönetici hesabı.
- Guardian service stop, kill, crash ve recovery.
- Otium süreç kill/crash ve korunan oturumun geri gelmesi.
- Reboot, `Win+L`, uyku, hibernation ve güç kesintisi sonrası açılış.
- Explorer restart, kullanıcı değiştirme ve Remote Desktop.
- Tek/çoklu monitör, ekran takma/çıkarma, DPI ve çözünürlük değişimi.
- Bozuk JSON/yedek, disk dolu ve yazma izni kaybı.
- Temiz kurulum, upgrade, repair, uninstall, rollback ve downgrade denemesi.
- Türkçe/İngilizce, açık/koyu/system tema ve Reduce Motion.

Her test için ön koşul, adımlar, beklenen sonuç, gerçek sonuç, build SHA'sı ve kanıt
saklanır. Manuel testler yalnız “denendi” olarak değil, tekrar edilebilir test vakası
olarak belgelenir.

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

**Mevcut eksik:** Depoda `LICENSE` dosyası yok.

- Ürün hedeflerine uygun lisansı seçip `LICENSE` ekleme.
- README ve contribution belgelerinde lisans, destek ve güvenlik yollarını belirtme.
- Özel güvenlik bildirimi için kanal belirleme.
- İki dilli, gizli bilgi paylaşımını önleyen hata bildirimi ve özellik önerisi şablonları eklendi; pull request şablonu bekliyor.
- Katkıların test, güvenlik ve gizlilik gereksinimlerini netleştirme.

#### Dokümantasyon tutarlılığı

**Mevcut eksik:** README bazı yerlerde iki kullanım biçiminden söz ediyor; ürün artık üç
biçime sahip. Application Identity bazı metinlerde planlanmış, bazı metinlerde
tamamlanmış görünüyor.

- Türkçe ve İngilizce README'leri güncel ürünle eşitleme.
- Kurulum, ilk kullanım, recovery, update ve uninstall rehberlerini final sihirbazla eşleştirme.
- Güvenlik sınırlarını test edilmiş davranışlardan ayırarak yazma.
- Bilinen sorunları ve desteklenen Windows sürümlerini belirtme.
- Release notes, tag ve GitHub Release metinlerini tutarlı tutma.

#### Gözlemlenebilirlik ve desteklenebilirlik

- Build SHA'sı, paket türü ve uygulama/Guardian/installer sürümünü birlikte gösterme.
- Gizlilik güvenli tanılama paketine ilgili yaşam döngüsü olaylarını ekleme.
- PIN, recovery code, pencere başlığı, belge, site veya yazılan içerik kaydetmeme.
- Log saklama süresi ve maksimum boyut sınırı belirleme.
- Kullanıcıya tanılama raporunu kolayca dışa aktarma yolu sunma.

## `v1.0.0` çıkış tanımı

Final sürüm ancak aşağıdaki koşulların tamamı sağlandığında yayınlanır:

- P0 release engellerinin tamamı kapalı ve kanıtlıdır.
- Build, format, smoke, birim ve zorunlu entegrasyon testleri geçer.
- Balanced ve Protected kaçış matrisi geçer.
- Çoklu monitör/DPI ve Windows yaşam döngüsü matrisi tamamlanır.
- Installer kurulum, upgrade, repair, uninstall ve rollback testlerini geçer.
- EXE, MSI, verifier ve updater geçerli Authenticode imzasına sahiptir.
- Public build development/test unlock geçidi içermez.
- Lisans, güvenlik, kurulum, kullanım ve recovery belgeleri hazırdır.
- GitHub Release doğru notları, SHA-256, manifest ve imzalı MSI'yı içerir.
- `v1.0.0` etiketi test edilen release commit'ine atanır.

## Önerilen uygulama sırası

1. Balanced oturum yüzeyi kaçışlarını düzelt ve regresyon testlerini yaz.
2. Çoklu monitör/DPI yüzey yöneticisini geliştir.
3. WiX `smartcab` sorununu çöz ve imzasız MSI'yı doğrula.
4. Açıklamalı ve iki dilli installer sihirbazını tamamla.
5. CI kalite kapıları ve ayrıştırılmış test altyapısını kur.
6. Gerçek Windows yaşam döngüsü matrisini çalıştır ve hataları düzelt.
7. Lisans, README, güvenlik ve kullanıcı belgelerini tamamla.
8. Signing tools ve code-signing sertifikasını hazırla.
9. İmzalı yeni release candidate üret ve temiz makinede doğrula.
10. Bütün kanıtlar tamamlandıktan sonra `v1.0.0` yayınla.

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

### v1.3 — Yerel ağ üzerinden yardımcı kontrol

- Aynı yerel ağdaki telefondan durum görüntüleme ve izin isteği.
- Varsayılan kapalı eşleştirme, kısa ömürlü kod ve cihaz iptali.
- Kimlik doğrulama, replay koruması ve açık ağ uyarıları.
- Bulut hesabı zorunluluğu olmadan yerel kullanım.
- Özelliğin Protected güvenlik sınırını zayıflatmamasını doğrulama.

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

### v1.0.0-alpha — Kullanım biçimleri ve alpha hazırlığı

- Üç kullanıcı odaklı kullanım biçimi.
- Flexible, Balanced ve Guardian destekli Guarded kişisel koruma.
- Flexible manuel odak kronometresi.
- Tek oturum yüzeyi, Control Center geçiş korumaları ve startup düzeltmeleri.
- Final release engellerinin açıkça belgelenmesi.

## Roadmap bakım kuralları

- Her aktif madde `Açık`, `Devam ediyor`, `Engelli` veya `Tamamlandı` durumuna sahip olur.
- Bir madde yalnız kod yazıldığında değil, test ve belge kanıtı tamamlandığında kapanır.
- Yeni release engelleri önce bu belgeye eklenir; final tanımı sessizce gevşetilmez.
- Tamamlanan ayrıntılar release notes'a taşınır; roadmap aktif işleri görünür tutar.
- Her release candidate sonrasında doğrulanmış durum ve test tarihi güncellenir.
