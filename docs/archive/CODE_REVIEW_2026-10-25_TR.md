# PrintPilotProxy karar raporu

Tarih: 25 Ekim 2026. Kaynak: `154a97a74a9a685232b7f02736a08633bcae4dc2`, sürüm 1.0.2. Uygulama kodu değiştirilmedi. Önerilen karar: IP allow list ve mevcut taşıma mimarisini koruyup HMAC'in neyi kanıtladığını, yerel yönetici yetkisini ve kaynak sınırlarını netleştirmek.

## Nasıl çalışıyor

Windows hizmeti proxy motorunu çalıştırır; WPF panel ve CLI yerel named pipe üzerinden durumu okur/yapılandırır. LAN istemcileri UDP 37421 ile keşfeder, TCP 3128 üzerinden HTTP veya CONNECT isteği yapar. Motor istemci IP ACL'sini, açıksa HMAC'i ve hedef portunu denetler. CONNECT, istemci ile hedef arasında kör TCP tünelidir; TLS içeriğini çözmez. Plain HTTP veya şifresiz SMTP'yi kendiliğinden şifrelemez.

Yapılandırma/korunan upstream parola ProgramData'dadır; yönetim yanıtları kaydedilmiş parolayı döndürmez. Değiştiren komutlar seri çalışır ve correlation ID tekrarında önceki sonuç kullanılabilir. IPC'de 32 eşzamanlı bağlantı ve 1.048.576 karakter mesaj sınırı vardır. Karakter sınırı tam bir MiB ağ baytı sınırı değildir.

Yeni dosya yoksa `JsonConfigurationManager` boş `AllowList` üretir ve bütün istemcileri reddeder. Eski/mevcut dosya kendi modunu korur. “Varsayılan kurulum açık proxy'dir” iddiası bu kaynakta doğru değildir.

## Yapabildikleri ve sınırları

- PrintPilot/Server'ın kısıtlı ağdan dış servislere erişimi için isteğe bağlı taşıma sağlar. Hesap oturumu, lisans, OldOS onayı, posta yetkisi veya cihaz enrollment sahibi değildir.
- Güncel OldOS'un kendi proxy ayarı yoktur; Server'a doğrudan gider. Proxy'yi OldOS üzerinde lisans çözümü gibi tarif etmek yanlıştır.
- Hedef port denetimi vardır; domain/hedef IP allow list yoktur. Model HMAC'i benzersiz cihaz sertifikası değildir.
- Windows service/WPF ürünü olarak uygulanmıştır. Core net8.0'dır; “.NET Standard” veya hazır Linux/systemd ürünü değildir.
- Canlı ağ ayarları `NetworkSettingsPage` içindedir; `ProxySettingsPage` eski bağlantılar için stub'dır.

## Karar verilecek bulgular

### PX-01 P2 Ortak HMAC gerçek uygulama veya tekil cihaz kimliği sağlamıyor

**Kanıt:** [PrintPilotHmacAuthenticator.cs:183](../../src/PrintPilotProxy.Infrastructure/Security/PrintPilotHmacAuthenticator.cs#L183) ve `GetProtocolSecret`, dağıtılan kod parçalarından ortak anahtar türetir. Timestamp ve sınırlı nonce cache replay'i engellemeye çalışır; anahtarı öğrenen yazılım yeni geçerli başlık üretebilir.

**Koşul ve etki:** izin verilen bir IP'deki başka yazılımın yalnız HMAC ile “gerçek PrintPilot”tan ayrıldığı varsayılırsa güven sınırı fazla geniş tarif edilmiş olur. IP ACL'si hâlâ uygulanır; uzaktaki herkesin proxy'ye eriştiği iddia edilmiyor.

**Öneri:** mevcut özelliği protokol uyumluluğu/replay koruması olarak adlandırmak; güçlü kimlik gereken müşteride ayrı sağlanan cihaz anahtarı veya mTLS tasarlamak. Büyük; istemci ve Server tarafı birlikte değişir. Kabul: bir cihaz iptal edilince başka cihazları etkilemeden erişimi kesilebilmeli.

### PX-02 P2 Varsayılan konsol kullanıcısı sistem proxy ayarlarını değiştirebiliyor

**Kanıt:** [IpcSecurityOptions.cs:13](../../src/PrintPilotProxy.Infrastructure/Ipc/IpcSecurityOptions.cs#L13) varsayılan false; [IpcSecurityValidator.cs:94](../../src/PrintPilotProxy.Infrastructure/Ipc/IpcSecurityValidator.cs#L94) aktif konsol kullanıcısına start/stop/configuration/firewall komutlarını açar. Bu açıkça kodlanmış ürün politikasıdır; eksik named-pipe ACL bulgusu değildir.

**Etki:** paylaşılan terminal/kiosk kullanıcıları hizmetin makine düzeyindeki davranışını değiştirebilir. Yönetici kontrolü isteyen kurumlar için varsayılan fazla geniştir.

**Öneri:** kurulumda yönetim politikası seçimi ve kurum profili için `Ipc:RequireAdministrator=true`. Küçük/orta. Kabul: standart konsol kullanıcısı okuyabilmeli ama değiştiren komutlar reddedilmeli; yönetici bakım yolu çalışmalı.

### PX-03 P2 Eksik yapılandırma bölümleri güvenliği gevşetebilir

**Kanıt:** [JsonConfigurationManager.cs:41](../../src/PrintPilotProxy.Infrastructure/Configuration/JsonConfigurationManager.cs#L41) eksik bölümleri model varsayılanlarıyla tamamlar; `ClientAccessSettings.Mode` modelde `AllowAll`, kimlik zorunluluğu false'tur. Yeni dosya oluşturma özel olarak AllowList seçer; mevcut kısmi dosyanın okunması aynı yol değildir.

**Etki ve durum:** onarım/elle düzenleme sonucu clientAccess kaybolursa, parser bunu otomatik reddetmez ve erişim politikası genişleyebilir. Geçerli eski AllowAll tercihinin kendiliğinden değişmesi önerilmiyor. Kaynak akışı doğrulandı; gerçek hizmette yapılandırma değiştirilmedi.

**Öneri:** güvenlik açısından gerekli alanları zorunlu saymak veya eksik durumda kapalı modele dönmek; migrasyon/backupu korumak. Orta. Kabul: boş/kısmi JSON hiçbir istemciyi yanlışlıkla açmamalı; yöneticiye onarım nedeni gösterilmeli.

### PX-04 P2 Maksimum bağlantı sayısı bir kabul sınırı değil

**Kanıt:** [UnobtaniumProxyEngine.cs:465](../../src/PrintPilotProxy.Proxy/UnobtaniumProxyEngine.cs#L465) yalnız uyarı üretir; 477'de bağlantıların reddedilmediği açıkça yazılıdır. Idle timeout ayrıca uygulanır. Tasarım, yarım açık bağlantılarla tüm kullanıcıları kilitlemeyi önlemek için seçilmiş; bunu basit bir toplu reddetme koşuluna çevirmek de sorun üretir.

**Öneri:** arayüzde alanı “uyarı eşiği” diye adlandırmak; gerçek kaynak koruması için kaynak başına adil bütçe, istek hızı ve aktif tünel limitini ölçerek tasarlamak. Orta/büyük. Kabul: tek istemcinin yığılması diğer istemcileri kilitlememeli; bellek/handle sınırı ölçülmeli. Bu çalışmada yük testi yapılmadı.

### PX-05 P2 Hedef port izinleri iç ağ hedeflerini dışlamıyor

**Kanıt:** [EvaluateAccess:520](../../src/PrintPilotProxy.Proxy/UnobtaniumProxyEngine.cs#L520) URI ve portu denetler; domain/IP hedef politikası yoktur. İzinli istemci, izinli port üzerindeki erişilebilir iç hedefe de tünel açabilir.

**Koşul:** Proxy'yi yalnız belirli İnternet posta/bulut servislerine çıkış kapısı olarak kullanmak isteyen kurumlarda yatay erişim riski doğar. Bu ürünün mevcut izinli hedef politikasının sınırıdır; yetkisiz istemcinin ACL'yi aştığı gösterilmedi.

**Öneri:** isteğe bağlı hedef allow list, loopback/link-local/iç ağ istisna politikası ve DNS çözümü ile bağlantı adresinin tutarlılığı. Büyük. Kabul: engelli hedef DNS yönlendirmesiyle veya IP yazarak aşılamamalı; gerekli kurumsal upstream çalışmalı.

### PX-06 P2 .NET 8 destek takvimi

Çalışma zamanı net8.0 tabanlıdır. .NET 8 desteği 10 Kasım 2026'da sona erer. WPF, Windows hizmeti, proxy kütüphanesi ve paketleme doğrulamasıyla birlikte desteklenen LTS hedefe geçiş planlanmalı. [Microsoft destek politikası](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Arayüz önerileri

- **Dashboard:** etkin IP erişim modu, HMAC'in açık/kapalı durumu, yönetici gereksinimi ve hedef politikası aynı özet içinde görünsün. “Güvenli” tek etiketi bu farklı kontrollerin yerine geçmesin.
- **NetworkSettingsPage:** bağlantı uyarı eşiğini hard limit gibi sunmayın; belirli adaptör/IP ile tüm arayüzlerin etkisini açıklayın.
- **Allowed Clients:** boş yeni allow list için “henüz hiçbir bilgisayar izinli değil” durumunu ve kontrollü ilk cihaz eklemeyi görünür yapın.
- **Diagnostics:** DNS, proxy erişimi, tünel, uzak TLS ve posta kabulünü ayrı aşamalar olarak gösterin. İnternet testi başarı sonucu e-posta teslimini kanıtlamaz.

Bunlar WPF kaynaklarına dayalı önerilerdir; gerçek panelde görsel/erişilebilirlik testi yapılmadı.

## Doğrulama ve belgeler

Çözümün dört test projesinde toplam 297 test geçti: Core 113, Proxy 29, Infrastructure 97, App 58. Test komutu ve NuGet taraması başarılı tamamlandı. Gerçek kurulum, firewall, upstream ağ, uzun süreli bağlantı yükü veya SMTP teslimi denenmedi.

`PROJECT_CONTEXT.md`, `docs/SECURITY.md`, `docs/CONFIGURATION.md` düzeltildi; `AGENTS.md` ve `docs/AGENT_CONTEXT.md` eklendi. Özellikle HMAC iddiası, net8.0 hedefi ve advisory bağlantı eşiği açıklandı. Kod davranışı değiştirilmedi.

**Önerilen sıra:** PX-02/03/04 ve PX-06; müşteri gereksinimine göre PX-01/05 protokol projesi. Kararınız: uygula / planla / ertele / kabul edilen risk.
