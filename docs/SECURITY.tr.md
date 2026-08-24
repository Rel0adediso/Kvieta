# Otium güvenlik sınırları

## Korunan senaryo

Protected mod, ayrı bir Windows yönetici hesabının yönettiği standart kullanıcı hesabındaki basit atlatmaları zorlaştırır. Guardian Windows servisi korunan policy kopyasını tutar, oturum yüzünü yeniden başlatır ve uygulamayla kimlik doğrulamalı IPC üzerinden konuşur.

Guardian servisinin veya korunan oturumun Görev Yöneticisi benzeri yollarla sonlandırılması Windows servis kurtarması ve gözetim döngüsüyle karşılanır. Protected başlangıçta Guardian sağlıklı değilse Otium korumasız devam etmez; onarım ister veya güvenli biçimde kapanır.

## Garanti edilmeyenler

- Windows yönetici yetkisi, fiziksel disk erişimi veya çevrimdışı işletim sistemi müdahalesi olan saldırgana karşı mutlak koruma yoktur.
- Güvenli Mod, başka bir işletim sistemiyle açılış, firmware/boot değişiklikleri ve çekirdek düzeyi araçlar kapsam dışıdır.
- Otium ebeveyn denetimi veya kurumsal EDR/AppLocker/WDAC yerine geçmez.
- İmzasız Development çıktısı yayın paketi değildir. Public Guardian, Program Files altındaki ve installer tarafından sabitlenen geçerli Authenticode imzasına sahip istemciyi bekler.

## Saklanan veriler

Planlar, kurallar ve kullanım geçmişi cihazda kalır. Tanılama raporu PIN, kurtarma kodu, belge içeriği veya pencere başlığı içermez. Güvenlik audit'i işlem türü, sonuç ve zaman gibi sınırlı olayları kaydeder.

## Kurtarma modeli

PIN sıfırlama tek kullanımlık kurtarma kodu ve Windows yönetici onayı gerektirir. Son sağlam ayar kopyası geri yüklenebilir; installer repair uygulama ve Guardian dosyalarını doğrular. Kurtarma araçları günlük süreleri ve uygulama kurallarını gizlice kaldırmaz.

Güvenlik açığı bildirirken hassas veriyi herkese açık issue'ya koymayın; proje sahibi özel bir bildirim kanalı yayımlayana kadar yalnızca yeniden üretim için gereken en az bilgiyi paylaşın.
