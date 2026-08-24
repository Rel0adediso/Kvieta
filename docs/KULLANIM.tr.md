# Otium kullanım ve kurtarma rehberi

## Kurulum

Public sürüm için yalnızca yayınlanan, Authenticode imzası doğrulanan MSI paketini kullanın. MSI Otium'u `C:\Program Files\Otium` altına ve Guardian servisini otomatik başlangıçla kurar. Kullanıcı verileri `%LOCALAPPDATA%\Otium`, korunan policy ve Guardian durumu `%ProgramData%\Otium` altında tutulur.

## İlk kullanım

1. **Kişisel** modu kendi düzeniniz için, **Korumalı** modu ayrı bir Windows yöneticisinin yönettiği hesap için seçin.
2. Haftalık planda izin verilen saatleri ve günlük limiti ayarlayın.
3. Uygulamalar ekranında **Uygulama ekle** ile bir program seçip `Kalıcı kapalı`, `Süreli` veya `Kaldır` davranışını belirleyin.
4. Korumalı modda yönetici PIN'ini ve tek kullanımlık kurtarma kodlarını güvenli bir yerde saklayın.
5. **Kaydet** düğmesiyle değişiklikleri uygulayın.

## Davranışlar

- **Kalıcı kapalı:** Program açıldığında Otium programı ve başlattığı tanınan alt işlemleri kapatır.
- **Süreli:** Programın günlük kullanımı sayılır ve tanımlanan süre dolduğunda kapatılır.
- **Kaldır:** Uygulama kuralını siler; program artık bu kuralla yönetilmez.

## Kurtarma Merkezi

- **Saati onayla:** Yanlış saat uyarısını Windows yönetici onayıyla temizler; sistem saatini değiştirmez.
- **Ayarları geri yükle:** Son doğrulanmış ayar kopyasını geri getirir; kullanım geçmişini silmez.
- **Kurulumu onar:** Uygulama ve Guardian kurulumunu yeniden doğrular; planı, PIN'i ve geçmişi silmez.
- **Tanılama raporu:** Özel içerik içermeyen bir JSON raporu dışa aktarır.

Protected modda Guardian eksik veya bozuksa uygulama korumasız devam etmez. Onarımı yönetici olarak onaylayın. Sorun sürerse tanılama raporunu alın ve installer repair çalıştırın.

## Kaldırma ve mod değiştirme

Bir uygulama kuralı, uygulama satırındaki davranış menüsünden `Kaldır` seçilerek silinir. Guardian doğrudan uygulama kuralı değildir; Korumalı moddan çıkış yönetici PIN'i ister. Programın MSI ile kaldırılması Windows Installer ve yönetici onayı üzerinden yapılır.
