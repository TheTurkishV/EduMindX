# EduMindX Akıllı Tahta Sistemi — Kapsamlı Mimari ve İşlev İnceleme Raporu

**Rapor Tarihi:** 6 Kasım 2026  
**İncelenen Ürün:** EduMindX Akıllı Tahta (EduMindX SmartBoard)  
**Geliştirici:** EduMindX Project  
**Sürüm:** 1.5.1 (.NET 8.0 Windows / WPF)  
**Hedef Platform:** Windows İşletim Sistemli Etkileşimli Akıllı Tahtalar  

---

## 1. Genel Tanıtım ve Amaç

**EduMindX Akıllı Tahta**, eğitim kurumlarında (okullar, kolejler, dershaneler vb.) bulunan dokunmatik akıllı tahtaların merkezi olarak yönetilmesini, ders saatleri haricinde yetkisiz kullanımın engellenmesini, ders ve teneffüs saatlerine göre otomatik kilitlenip açılmasını sağlayan profesyonel bir **akıllı tahta kilit ve yönetim sistemi (Smart Board Kiosk & Management System)** yazılımıdır.

Sistem, öğrencilerin teneffüslerde veya gözetimsiz zamanlarda tahtayı amacı dışında kullanmasını engellerken, öğretmenlerin ders başlangıcında tahtaya hızlı, modern ve güvenli bir şekilde erişebilmesini (QR kod, şifre, günlük kod veya uzaktan açma ile) sağlar.

---

## 2. Mimari Yapı ve Bileşen Organizasyonu

Çözüm iki ana projeden meydana gelen modüler ve katmanlı bir mimariye sahiptir:

```
EduMindX (Solution)
│
├── EduMindX.SmartBoard.Core (.NET 8.0 - Platform Bağımsız Çekirdek Kütüphane)
│   ├── Kilit ve Zaman Durum Makinesi (LockEngine, LockPlan, LessonSlot)
│   ├── Kriptografik Doğrulama (Signatures, PlanVerifier, UpdateVerifier)
│   ├── Hile Korumalı Saat Mekanizması (BoardClock)
│   ├── Parola ve Güvenlik Kapısı (PasswordGate, PinCheck, UsedUnlockCodes)
│   └── Ağ / WAF Uyumluluk Katmanı (WafSafeHeadersHandler, UpdateManifest)
│
└── EduMindX.SmartBoard (.NET 8.0-windows - WPF İstemci Uygulaması)
    ├── Kullanıcı Arayüzü (LockWindow, UnlockedBar, AnnouncementWindow)
    ├── Windows Kabuk & Güvenlik Korumaları (ShellGuard, KeyboardBlocker, VirtualDesktops)
    ├── Donanım & Dokunmatik Kısıtlamaları (TabletMode, TouchGestures)
    ├── Gerçek Zamanlı Bulut Bağlantısı (DeviceConnection - SignalR Client)
    ├── Yerel Şifreli Depolama (BoardStorage - Windows DPAPI)
    └── Otomatik Güncelleme Motoru (UpdateRunner, UpdateTask)
```

---

## 3. Sistemin Ne İşlev Yaptığı (Fonksiyonel Özellikler)

1. **Haftalık Ders ve Teneffüs Çizelgesine Göre Otomatik Kilitleme:**
   - Sunucudan gelen haftalık ders planını (Pazartesi'den Pazar'a ders başlangıç ve bitiş saatleri) takip eder.
   - Ders bittiği anda tahta otomatik olarak tam ekran kilitlenir (`LESSON_END`).
   - Ders dışı saatlerde açıldığında belirlenen mühlet (varsayılan 10 dakika) bitiminde otomatik kapanır (`TIMEOUT`).
   - Kilitlenme yaklaşırken (varsayılan 2 dakika kala) kullanıcıya ses/renk uyarısı ve geri sayım sayacı gösterir.

2. **Çok Yöntemli Kilit Açma Yetkilendirmesi:**
   - **QR Kod ile Mobil Açma:** Öğretmen cep telefonundan tahtada beliren dinamik QR kodu okutarak EduMindX mobil/web uygulaması üzerinden tahtayı saniyeler içinde açar.
   - **Günün Tek Kullanımlık Çevrimdışı Kodu:** İnternet kesintisi durumunda veya acil hallerde idarenin verebileceği 8 haneli tek kullanımlık günlük kodla tahta açılabilir. Kullanılan kod anında yerel veritabanında harcanmış olarak işaretlenir.
   - **Acil Durum / İdare Şifresi:** Kilit ekranındaki tuş takımından kurumun ana parolası girilerek kilit açılabilir.
   - **Merkezi / Uzaktan Kilit Açma:** İdare paneli üzerinden tek tıkla veya belirli bir saate kadar açık kalacak şekilde uzaktan kilit açılabilir.

3. **Gelişmiş Kiosk Güvenliği ve Kilit Atlama (Bypass) Koruması:**
   - Kilit ekranındayken Windows Görev Çubuğu (Taskbar) ve masaüstü `explorer.exe` seviyesinde dondurulur; başlat menüsü, arama ve bildirim panelleri öldürülür.
   - Klavyeden Win tuşu, Alt+Tab, Alt+F4, Alt+Esc, Ctrl+Esc gibi kısayollar donanımsal kanca (low-level hook) ile engellenir.
   - Sanal masaüstü oluşturarak kilitten kaçma girişimi engellenir.
   - 3 ve 4 parmak dokunmatik hareketler ve Windows Tablet Modu devre dışı bırakılır.
   - Sayısal tuş takımı her gösterimde tuşları rastgele karıştırarak (shuffled keypad) omuz gözetlemesini ve ekrandaki parmak izi analizini imkansız hale getirir.

4. **Yüzen Bildirim ve Yönetim Çubuğu (UnlockedBar):**
   - Tahta açıkken ekranın sol veya sağ kenarında konumlanan kompakt, yarı saydam, odak çalmayan yüzen bir çubuk gösterilir.
   - Kalan ders süresini ve ders bitiş saatini gösterir; süre bitimine az kala sarı/turuncu uyarı moduna geçer.
   - İstenilen yere sürüklenebilir; tek tıkla tahtayı manuel kilitleme imkanı sunar.

5. **Merkezi Duyuru ve Acil Durum Bildirimi (AnnouncementWindow):**
   - Okul idaresi tüm tahtalara veya belirli bir sınıfa anlık mesaj gönderebilir (Örn: Yangın/Deprem tatbikatı, genel duyuru).
   - Duyuru tam ekran kart halinde belirir ve kilit ekranının da üstünde görüntülenir.

6. **Merkezi İzleme, Olay Kaydı ve Telemetri:**
   - Kilit açılma, kilitlenme, başarısız parola denemeleri, ders içi/dışı durumlar sunucuya WebSocket üzerinden anlık iletilir; internet yoksa yerel JSON'da biriktirilip bağlantı gelince toplu gönderilir.

7. **Kriptografik Olarak Doğrulanan Güvenli Güncelleme:**
   - Güncellemeler ECDSA dijital imzası ve SHA-256 hash kontrolleriyle doğrulanır.
   - Güncelleme paketi sisteme uygulanmadan önce izole bir süreçte kendi kendine test edilir (`--self-test`).
   - Tahta sadece **kilitli, ders dışı ve çevrimiçi** iken güncellenir; ders esnasında asla kesinti yaşanmaz.

---

## 4. Neyin Nasıl Yapıldığı (Teknik Derinlemesine İnceleme)

### 4.1. Kilit Motoru ve Durum Yönetimi (`EduMindX.SmartBoard.Core\LockEngine.cs`)
`LockEngine`, tahtanın kilit durumunu yöneten deterministik bir durum makinesidir (State Machine).

- **Kilit Açılma Süresinin Hesaplanması (`ComputeUnlockUntil`):**
  - Tahta açılmak istendiğinde anlık yerel saat (`nowLocal`) alınır.
  - Günün dersleri (`LessonsOn`) taranır. Eğer anlık saat bir dersin içindeyse veya dersin başlamasına `OutsideLessonUnlockMinutes` (varsayılan 10 dk) kadar bir süre kalmışsa, tahta **ilgili dersin bitiş saatine kadar** açılır.
  - Eğer peş peşe blok dersler varsa, bitiş saati blok dersin sonuna kadar uzatılır.
  - Eğer o an bir ders yoksa, tahta `OutsideLessonUnlockMinutes` süresi kadar açılır.
  - Öğretmen veya idare sunucudan özel bir süre talep etmişse (`requestedUntilLocal`), bu süre en fazla 14 saat (`MaxRequestedUnlock`) olmak koşuluyla kabul edilir.
- **Zamanlayıcı Döngüsü (`Tick`):**
  - Her saniye tetiklenen saat sinyali ile `UnlockedUntil` kontrol edilir. Zaman dolduğunda nedenine göre `LessonEnd` (Ders Bitti) veya `Timeout` (Zaman Aşımı) sebebiyle tahta kilitlenir.
- **Kilitlenme Nedenleri (`LockReason`):**
  - `Teacher`: Öğretmen yüzen çubuktaki "Kilitle" butonuna bastı.
  - `Remote`: Sunucudan uzaktan kilitleme emri geldi.
  - `LessonEnd`: Ders saati tamamlandı.
  - `Timeout`: Ders dışı serbest kullanım süresi doldu.
  - `Session`: Windows oturumu değişti/kilitlendi.
  - `ServiceDisabled`: İdare tahtayı hizmet dışına aldı.

---

### 4.2. Hile Korumalı Saat Mekanizması (`EduMindX.SmartBoard.Core\BoardClock.cs`)
Öğrencilerin Windows saatini ileri veya geri alarak kilit sistemini aldatmasını önlemek için özel bir saat mekanizması geliştirilmiştir:

- Sistem sunucuya bağlandığında sunucunun UTC saati alınır (`Sync(serverUtc)`).
- Aynı anda işlemcinin donanımsal yüksek hassasiyetli sayacı kaydedilir (`Stopwatch.GetTimestamp()`).
- Tahta zamanı hesaplarken işletim sisteminin `DateTime.UtcNow` değerine güvenmek yerine:
  $$\text{Hesaplanan UTC} = \text{Eşitlenen Sunucu UTC} + \text{Stopwatch Donanım Geçen Süre}$$
- Böylece kullanıcı Windows Denetim Masası'ndan tarihi veya saati değiştirse dahi, `Stopwatch` donanım frekansına bağlı olduğu için programın iç zaman algısı değişmez.

---

### 4.3. Windows Kabuk ve Süreç İzolasyonu (`EduMindX.SmartBoard\ShellGuard.cs`)
Kilit ekranı aktifken öğrencilerin arkada çalışan programlara, masaüstüne veya görev çubuğuna erişimini kesmek için Windows Native API'leri kullanılır:

- **Kabuğun Dondurulması (`Freeze`):**
  - `explorer.exe` süreci bulunur ve `OpenProcess(PROCESS_SUSPEND_RESUME)` ile açılır.
  - `ntdll.dll` içerisindeki belgelenmemiş (undocumented) `NtSuspendProcess` fonksiyonu çağrılarak Windows Gezgini'nin tüm iş parçacıkları (threads) dondurulur.
  - Görev çubuğu tıklamalara yanıt vermez, masaüstü simgeleri görünmez veya etkileşimsiz hale gelir.
  - Windows Başlat ve Arama arayüzlerini sağlayan UWP süreçleri anında sonlandırılır (`Kill`):
    - `StartMenuExperienceHost`
    - `ShellExperienceHost`
    - `ShellHost`
    - `SearchHost`
    - `SearchApp`
    - `SearchUI`
- **Kabuğun Çözülmesi (`Thaw`):**
  - Kilit açıldığında `NtResumeProcess` ardışık olarak çağrılarak (en fazla 8 kez, iç içe suspend sayılarını eritmek için) `explorer.exe` uyandırılır.
  - Eğer gezgin süreci çökmüş veya kapatılmışsa 20 saniyelik tolerans (`RestartGrace`) sonrasında yeni bir `explorer.exe` süreci başlatılır.
- **İzinsiz Ön Pencere Denetimi (`ReportForeignForeground`):**
  - Kilitliyken `user32.dll!GetForegroundWindow` taranarak kilit ekranı dışında öne geçmeye çalışan harici bir pencere tespit edilirse anında loglanır.

---

### 4.4. Donanımsal Klavye Engelleme (`EduMindX.SmartBoard\KeyboardBlocker.cs`)
Öğrencilerin klavye kısayolları ile kilidi aşmasını önlemek amacıyla Low-Level Windows Klavye Kancası kurulur:

- `SetWindowsHookEx(WH_KEYBOARD_LL = 13, HookCallback, ...)` API'si ile kanca sisteme enjekte edilir.
- Kilitli modda (`Enabled = true`) aşağıdaki tuş kombinasyonları algılandığı anda `return 1` dönülerek Windows'a veya diğer uygulamalara ulaşması engellenir:
  - **Windows Tuşları:** `VK_LWIN` (91), `VK_RWIN` (92)
  - **Uygulama/Menü Tuşu:** `VK_APPS` (93)
  - **Alt + Tab:** `VK_TAB` tuşuna basılırken `LLKHF_ALTDOWN` (0x20) bayrağı varsa
  - **Alt + F4:** `VK_F4` tuşuna basılırken Alt bayrağı varsa
  - **Alt + Esc & Ctrl + Esc:** `VK_ESCAPE` tuşuna basılırken Alt veya Ctrl basılıysa

---

### 4.5. Sanal Masaüstü ve Dokunmatik Kaçış Koruması
- **Sanal Masaüstü Takibi (`EduMindX.SmartBoard\VirtualDesktops.cs`):**
  - Windows 10/11'de `Win + Ctrl + D` ile yeni bir sanal masaüstü açılarak kilit ekranından kaçılmasını engellemek için Windows COM arayüzü `IVirtualDesktopManager` (`a5cd92ff-29be-454c-8d04-d82879fb3f1b`) kullanılır.
  - Kilit penceresinin (`LockWindow`) aktif masaüstünde olup olmadığı `IsWindowOnCurrentVirtualDesktop` ile her saniye sorgulanır.
  - Eğer kullanıcı başka bir sanal masaüstüne geçmişse, kilit penceresi `MoveWindowToDesktop` API'si ile anında kullanıcının geçtiği masaüstüne taşınır.
- **Tablet Modu Engelleme (`EduMindX.SmartBoard\TabletMode.cs`):**
  - Akıllı tahtalarda tablet modu açıldığında pencereler tam ekrana zorlanabilir ve arayüz değişebilir.
  - Registry yolu `HKCU\Software\Microsoft\Windows\CurrentVersion\ImmersiveShell` üzerinden:
    - `TabletMode = 0` (Kapalı tutulur)
    - `SignInMode = 1` (Masaüstü modunda başla)
    - `ConvertibleSlateModePromptPreference = 0` (Tablet moduna geçiş sorma)
- **Çoklu Dokunma Hareketlerini Kapatma (`EduMindX.SmartBoard\TouchGestures.cs`):**
  - Öğrencilerin 3 veya 4 parmakla ekranı kaydırarak Görev Görünümü'nü (Task View) açmasını engellemek için:
    - Registry `HKCU\Control Panel\Desktop\TouchGestureSetting = 0` yapılır.

---

### 4.6. Kilit Ekranı ve Omuz Gözetleme Koruması (`EduMindX.SmartBoard\LockWindow.cs`)
WPF ile geliştirilen kilit ekranı, akıllı tahtanın yüksek çözünürlüklü ekranlarını tam kaplayacak şekilde tasarlanmıştır:

- **Tüm Monitörleri Kaplama (`CoverAllScreens`):**
  - `VirtualScreenLeft`, `VirtualScreenTop`, `VirtualScreenWidth` ve `VirtualScreenHeight` parametreleri kullanılarak sisteme bağlı projeksiyon veya ek monitörler dahil tüm ekran alanı kapatılır.
  - `Topmost = true` ve `ResizeMode = NoResize`, `WindowStyle = None` ayarları ile pencere kenarlıksız en üstte tutulur.
  - Kapatma girişimi engellenir (`e.Cancel = !AllowClose`).
- **Karıştırılmış Sayısal Tuş Takımı (Randomized Shuffled Keypad):**
  - İdare girişi veya PIN ekranı açıldığında `0-9` arasındaki rakamlar `Random.Shared.Next()` ile rastgele bir diziye dizilir (`BuildKeys`).
  - **Neden Yapıldı?** Sınıfta tahtaya şifre giren öğretmenin arkasındaki öğrenciler, öğretmenin el hareketlerinden veya ekrandaki parmak izi lekelerinden şifreyi tahmin edemez. Tuşların yeri her açılışta ve her hatalı denemede değişir.
- **Brute-Force Koruması (`PasswordGate.cs`):**
  - Maksimum deneme hakkı: **5**.
  - 5 hatalı denemeden sonra sistem geçici kilitlenmeye (`LockedOut`) girer.
  - Kilitlenme süresi her seferinde 1 dakika artar (`1 dk, 2 dk, 3 dk ... 10 dk maksimum`).
  - Süre sayacı `Stopwatch.GetElapsedTime` ile çalıştığından, Windows saatini ileri almak bu süreyi sıfırlamaz.

---

### 4.7. Doğrulama ve Kriptografi

1. **Ders Planı İmzası (`EduMindX.SmartBoard.Core\Signatures.cs` & `PlanVerifier.cs`):**
   - Sunucudan indirilen `SignedPlan` paketi bir veri yükü (`Payload`) ve dijital imza (`Signature`) içerir.
   - Doğrulama algoritması: **ECDSA (Elliptic Curve Digital Signature Algorithm)** + **SHA-256**.
   - Cihaz, sunucunun Base64 formatındaki genel anahtarını (`PlanPublicKey`) içe aktarır (`ImportSubjectPublicKeyInfo`) ve imzanın geçerliliğini doğrular.
   - Paketteki `deviceGuid` değeri tahtanın kendi kimliği ile eşleşmiyorsa plan reddedilir (başka bir tahtanın planı yüklenemez).
2. **Parola ve Günlük Kod Kontrolü (`PasswordGate.cs`):**
   - Girilen 8 haneli PIN kodu `BCrypt.Net.BCrypt.Verify(pin, hash)` ile kontrol edilir.
   - Kod, hem tahtanın ana şifresi (`UnlockPasswordHash`) hem de sunucunun o gün için tahsis ettiği tek kullanımlık kodların hash listesi ile karşılaştırılır.
3. **Tek Kullanımlık Kod Takibi (`UsedUnlockCodes.cs`):**
   - Eşleşen bir günlük kod kullanıldığında hash değeri `used-codes.json` içine kaydedilir ve `MarkUsed` ile işaretlenir.
   - Aynı kod o gün içinde bir daha kullanılamaz.
   - 7 günden eski kod kayıtları otomatik temizlenir (`Retention = 7 gün`).
4. **Yerel Verilerin Şifrelenmesi (`EduMindX.SmartBoard\BoardStorage.cs`):**
   - `config.dat` ve `plan.dat` dosyaları düz metin değil, Windows'un donanım destekli **DPAPI (Data Protection API)** mekanizmasıyla şifrelenir (`DataProtectionScope.LocalMachine`).
   - Ekstra entropi olarak `"EduMindX.SmartBoard.v1"` bayt dizisi kullanılır. Dosyalar sadece o makinede çözülebilir; başka bir bilgisayara kopyalansa dahi açılamaz.
   - Elektrik kesintisi durumunda dosya bozulmasını önlemek için yazma işlemleri önce `.tmp` dosyasına yapılır, ardından `File.Move(..., overwrite: true)` ile atomik olarak hedef dosyanın üzerine yazılır.

---

### 4.8. Sunucu İletişimi ve SignalR Protokolü (`EduMindX.SmartBoard\DeviceConnection.cs`)
Uygulama, okulun/merkezin bulut sunucusuna (`https://api.example.com/hubs/device`) ASP.NET Core SignalR WebSocket protokolü üzerinden bağlı kalır:

- **Kesintisiz Yeniden Bağlanma Politikası (`ForeverRetryPolicy`):**
  - Bağlantı koptuğunda: İlk 5 deneme 5 saniye arayla, sonrasında 30 saniye arayla sonsuza kadar tekrar denenir.
- **WAF Uyumlu Başlıklar (`WafSafeHeadersHandler.cs`):**
  - Sunucu önündeki Web Application Firewall (WAF) veya ters vekillerin (Reverse Proxy) WebSocket veya REST isteklerini bot zannedip engellememesi için özel HTTP başlıkları eklenir (`Accept: */*`, `Content-Type: text/plain; charset=utf-8`).
- **Gelen Sunucu Komutları (`BoardCommand`):**
  - `Lock`: Tahtayı uzaktan kilitler.
  - `Unlock`: Tahtayı uzaktan açar (QR onayından veya doğrudan idare panelinden tetiklenir).
  - `RefreshSchedule`: Yeni ders programını sunucudan çeker.
  - `ShowAnnouncement`: Ekranda acil durum duyurusu çıkartır.
  - `SetServiceState`: Tahtanın hizmet dışı bırakılması veya tekrar devreye alınması.
  - `Reboot` / `Shutdown`: Windows'un `shutdown /r /t 10` veya `/s` komutu ile yeniden başlatılması veya kapatılması.
- **Replay Saldırısı Engelleme (`HandledCommands.cs`):**
  - Ağdan mükerrer gelen veya geçmişe ait komutların tekrar çalıştırılmasını önlemek için her komutun benzersiz GUID'si kaydedilir. 1 saatlik saklama süresi vardır. Daha önce işlenmiş komut gelirse işlem tekrarlanmaz, sadece sunucuya başarı teyidi (`Ack`) gönderilir.
- **Giden İstemci İstekleri:**
  - `Heartbeat(Locked, AgentVersion)`: Her 30 saniyede bir tahtanın kilit durumunu ve sürümünü bildirir, güncel sunucu UTC saatini döner.
  - `CreateUnlockChallenge`: Kilit ekranında QR butona basıldığında sunucuda bir oturum açma meydan okuması (challenge) başlatır; gelen URL'yi QR koda dönüştürür.
  - `ReportEvents`: Tahtada gerçekleşen kilit açılma, kapanma veya yetkisiz deneme olaylarını toplu (en fazla 200'lük paketler halinde) sunucuya raporlar.

---

### 4.9. Yüzen Çubuk Kullanıcı Deneyimi (`EduMindX.SmartBoard\UnlockedBar.cs`)
Tahta kilitli değilken öğretmenlerin ders işlemesini engellemeyecek şekilde tasarlanmış yüzen bir araç çubuğudur:

- **Odak Çalmayan Pencere (Non-Activating Window):**
  - Win32 API `SetWindowLong` ile pencere stiline `WS_EX_NOACTIVATE (0x08000000)` ve `WS_EX_TOOLWINDOW (0x0080)` bayrakları eklenir.
  - Bu sayede öğretmen tahtada PowerPoint, PDF, dijital tahta uygulaması veya video izletirken bu çubuğa dokunsa bile ana uygulamanın odağı (focus) kaybolmaz, slayt gösterisi durmaz.
- **Akıllı Kenar Yerleşimi:**
  - Çubuk ekranın sol veya sağ yarısına sürüklendiğinde, detay paneli otomatik olarak ekranın içine doğru açılacak şekilde (`Dock.Left` veya `Dock.Right`) yön değiştirir.
  - Konum `bar-position.json` dosyasına normalize koordinat olarak (0.0 ile 1.0 arası oran) kaydedilir; ekran çözünürlüğü değişse bile orantılı yerini korur.
- **Geri Sayım ve Uyarı:**
  - Dersin bitmesine 2 dakika kala çubuğun rengi maviden turuncuya döner ve kalan saniyeler geri sayım şeklinde gösterilir.

---

### 4.10. Otomatik Güncelleme Mimarisi (`EduMindX.SmartBoard\UpdateRunner.cs` & `UpdateTask.cs`)
Sistem, Windows Görev Zamanlayıcısı (Task Scheduler) ile tam entegre çalışan güvenli bir güncelleme altyapısına sahiptir:

1. **Görev Zamanlayıcı Kaydı (`UpdateTask.cs`):**
   - Windows Görev Zamanlayıcı COM servisi (`Schedule.Service`) kullanılarak `EduMindX Akilli Tahta Guncelleme` adında bir görev oluşturulur.
   - Görev **SYSTEM (`S-1-5-18`)** hesabı altında en yüksek yetkilerle (`HighestAvailable`) çalışacak şekilde kaydedilir.
   - Tetikleyiciler:
     - Bilgisayar açılışında 2 dakika gecikmeli (`BootTrigger`).
     - Her gece saat 03:00'te (`CalendarTrigger`).
     - Gerektiğinde `BoardController` tarafından talep üzerine (`schtasks /Run /TN ...`).
2. **Güvenli Çalışma Şartı (`UpdateCheckSchedule.cs`):**
   - Güncelleme denetimi rastgele bir zamanda değil; yalnızca **tahta kilitliyken**, **ders saati dışındayken** ve **internet bağlantısı aktifken** çalıştırılır. Dersteki bir öğretmenin ekranı asla güncelleme sebebiyle kesintiye uğramaz.
3. **Güncelleme İşlem Akışı (`UpdateRunner.RunAsync`):**
   - `Global\EduMindX.SmartBoard.Update` mutex'i ile aynı anda birden fazla güncelleme çalışması engellenir.
   - `akilli-tahta-surum.json` adresi kontrol edilir.
   - İndirilen manifesto, gömülü ECDSA Genel Anahtarı (`UpdateVerifier.ReleasePublicKey`) ile doğrulanır.
   - Sürüm numarası mevcut sürümden büyükse (`IsNewer`) `paket.zip` indirilir.
   - İndirilen paketin SHA-256 özeti hesaplanır ve manifestodaki değerle doğrulanır.
   - Paket `.update/paket` dizinine açılır.
   - **Kritik Güvenlik Adımı (Self-Test):** Yeni `EduMindX.SmartBoard.exe` doğrudan kurulmaz! Önce arka planda `--self-test` parametresiyle çalıştırılır. Yeni dosyanın sorunsuz açıldığı, yapılandırmayı okuyabildiği ve doğru sürüm numarasını döndürdüğü teyit edilir.
   - **Atomik Takas (Swap) & Rollback:** Eski dosyalar `.old` uzantısıyla yedeklenir, yeniler yerlerine kopyalanır. Bir hata çıkarsa anında geri alınır (rollback).
   - Eski süreçler kapatılır ve `schtasks` üzerinden güncel tahta uygulaması başlatılır.

---

## 5. Özet Tablo: Güvenlik Tehditleri ve EduMindX Savunma Mekanizmaları

| Tehdit / Kaçış Girişimi | EduMindX Karşı Tedbiri | İlgili Kod Modülü |
| :--- | :--- | :--- |
| **Alt+Tab / Alt+F4 ile uygulamayı kapatma** | Düşük seviyeli klavye kancasıyla (WH_KEYBOARD_LL) tuşların yutulması | `KeyboardBlocker.cs` |
| **Görev Çubuğu / Başlat Menüsü kullanımı** | `NtSuspendProcess` ile `explorer.exe`'nin dondurulması; UWP kabuk host'larının sonlandırılması | `ShellGuard.cs` |
| **Yeni Sanal Masaüstü açarak kilitten kaçma** | COM arayüzü ile masaüstü takibi ve kilit ekranının zorla aktif masaüstüne taşınması | `VirtualDesktops.cs` |
| **Çok parmaklı dokunma hareketleri (Gestures)** | Windows Kayıt Defteri'nde (Registry) dokunmatik hareketlerin kapatılması | `TouchGestures.cs` |
| **Sistem saatini değiştirerek kilit açma** | Donanımsal `Stopwatch` sayaç tabanlı senkronize sanal saat (`BoardClock`) | `BoardClock.cs` |
| **Öğretmenin şifresini izleme (Omuz gözetleme)** | Kilit ekranında her seferinde sayısal tuş takımının rastgele karıştırılması | `LockWindow.cs` |
| **Kaba kuvvet (Brute-Force) şifre denemesi** | 5 denemede 1-10 dk kademeli dondurma (`PasswordGate`) | `PasswordGate.cs` |
| **Yetkisiz / Sahte ders planı yükleme** | ECDSA-SHA256 dijital imza ve cihaz GUID doğrulaması | `PlanVerifier.cs`, `Signatures.cs` |
| **Yerel yapılandırma ve şifrelerin çalınması** | Windows DPAPI makine tabanlı şifreleme ile dosya saklama | `BoardStorage.cs` |
| **Zararlı yazılım içeren güncelleme enjeksiyonu** | ECDSA imzalı manifesto, SHA-256 paket özeti ve `--self-test` çalıştırma doğrulaması | `UpdateVerifier.cs`, `UpdateRunner.cs` |

---

## 6. Sonuç

**EduMindX Akıllı Tahta**, MEB ve özel okul ortamlarındaki etkileşimli tahtaların ihtiyaç duyduğu tüm kilit, ders programı takibi, uzaktan yönetim ve güvenlik gereksinimlerini karşılamak üzere titizlikle tasarlanmış, kurumsal seviyede bir kiosk yönetim yazılımıdır.

Yazılımın mimarisi; **katmanlı soyutlama**, **kriptografik imza doğrulamaları**, **Windows çekirdek (Native/NT) seviyesinde kabuk koruması**, **odak çalmayan modern kullanıcı arayüzü** ve **arıza korumalı (self-test & rollback) otomatik güncelleme altyapısı** ile yüksek güvenilirlik ve güvenlik standartlarına sahiptir.

