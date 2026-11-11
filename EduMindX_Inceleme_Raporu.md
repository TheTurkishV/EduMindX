# EduMindX Akıllı Tahta — Mimari, Güvenlik ve Kod Analiz Raporu

**Çalışma Türü:** Akademik İnceleme, Tersine Mühendislik ve Güvenlik Araştırması  
**Hedef Sistem:** EduMindX Akıllı Tahta (v1.5.1, .NET 8.0 WPF)  
**Tarih:** 6 Kasım 2026  

---

> [!NOTE]
> **Araştırma Amacı:** Bu rapor, eğitim kurumlarında kullanılan akıllı tahta kiosk ve kilit sistemlerinin yazılım mimarisini, Windows işletim sistemi düzeyindeki güvenlik mekanizmalarını, API telemetri trafiğini ve olası tasarım açıklarını incelemek üzere hazırlanmış eğitsel bir teknik rapordur.

---

## 1. Sistemin Genel Amacı ve İşleyiş Mantığı

EduMindX, okullarda bulunan dokunmatik akıllı tahtaların ders saatleri dışında kontrolsüz kullanımını önlemek üzere tasarlanmış bir **kiosk kilit ve yönetim yazılımıdır**.

Sistemin temel çalışma mantığı şunlara dayanır:
* **Haftalık Ders Planı Takibi:** Tahta, sunucudan indirilen haftalık ders programını izler. Ders bittiğinde (`LESSON_END`) otomatik kilitlenir; ders dışı zamanlarda açıldığında serbest süre (`TIMEOUT`, varsayılan 10 dk) bitiminde kapanır.
* **Farklı Kilit Açma Kanalları:**
  * **QR Kod:** Öğretmenin mobil cihazla tahtadaki dinamik QR kodu okutmasıyla açılır.
  * **Çevrimdışı Tek Kullanımlık Kod:** İnternet yokken idareden alınan 8 haneli günlük kodla açılır.
  * **Kurum Şifresi:** İdareye ait ana şifre ile doğrudan açılır.
  * **Uzaktan Yönetim:** Web paneli üzerinden SignalR aracılığıyla uzaktan açılır veya kilitlenir.
* **Ders Esnasında Yüzen Durum Çubuğu (`UnlockedBar`):** Tahta açıkken ekranın kenarında durur, dersin bitmesine kalan süreyi gösterir ve son 2 dakikada sarı/turuncu uyarı verir.

---

## 2. Modüler Mimari Yapı

Yazılım iki ana katmandan oluşmaktadır:

```
EduMindX Çözümü
│
├── EduMindX.SmartBoard.Core (.NET 8.0 Sınıf Kütüphanesi)
│   ├── Kilit Durum Makinesi (LockEngine, LockPlan, LessonSlot)
│   ├── Hile Korumalı Saat (BoardClock)
│   ├── Kriptografik İmzalar (Signatures, PlanVerifier, UpdateVerifier)
│   ├── Güvenlik Kapısı (PasswordGate, UsedUnlockCodes, PinCheck)
│   └── Ağ Başlık Yönetimi (WafSafeHeadersHandler)
│
└── EduMindX.SmartBoard (.NET 8.0-windows WPF Uygulaması)
    ├── Görsel Arayüz (LockWindow, UnlockedBar, AnnouncementWindow)
    ├── Kabuk Koruması (ShellGuard, KeyboardBlocker, VirtualDesktops)
    ├── Donanım & Dokunmatik Kısıtlamaları (TabletMode, TouchGestures)
    ├── Gerçek Zamanlı Bulut Bağlantısı (DeviceConnection - SignalR)
    ├── Şifreli Yerel Depolama (BoardStorage - Windows DPAPI)
    └── Güvenli Güncelleme Motoru (UpdateRunner, UpdateTask)
```

---

## 3. Kod Düzeyinde Temel Mekanizmalar (Nasıl Çalışır?)

### 3.1. Hile Korumalı Saat Mekanizması (`BoardClock.cs`)
Öğrencilerin Windows saatini geri veya ileri alarak kilidi atlatmasını engellemek için geliştirilmiştir:
* Sunucuya bağlanıldığında sunucu UTC saati alınır ve işlemcinin donanımsal sayacı kaydedilir (`Stopwatch.GetTimestamp()`).
* Tahta zamanı hesaplarken işletim sistemi saatine değil, donanımsal sayaç üzerinden geçen süreye bakar:
  $$\text{Zaman} = \text{Sunucu UTC} + \text{Stopwatch Donanım Geçen Süre}$$
* Bu sayede Windows Denetim Masası'ndan saat değiştirilse dahi tahtanın iç zaman algısı bozulmaz.

### 3.2. Windows Gezgini Dondurma (`ShellGuard.cs`)
* Kilit ekranı aktifken `ntdll.dll!NtSuspendProcess` API'si çağrılarak `explorer.exe`'nin tüm iş parçacıkları askıya alınır.
* Görev çubuğu, başlat menüsü ve bildirim alanı tıklamalara yanıt vermez.
* Başlat ve arama UWP süreçleri (`StartMenuExperienceHost`, `SearchApp`) kapatılır.
* Kilit açıldığında `NtResumeProcess` ile kabuk tekrar uyandırılır.

### 3.3. Düşük Seviyeli Klavye Kancası (`KeyboardBlocker.cs`)
* Win32 `SetWindowsHookEx(WH_KEYBOARD_LL = 13, ...)` ile klavye kancası kurulur.
* `VK_LWIN`, `VK_RWIN`, `VK_APPS`, `Alt+Tab`, `Alt+F4`, `Alt+Esc` ve `Ctrl+Esc` tuş vuruşları donanım seviyesinde yakalanarak yutulur.

### 3.4. Sanal Masaüstü Kaçış Koruması (`VirtualDesktops.cs`)
* Windows 10/11'de `Win + Ctrl + D` ile yeni bir sanal masaüstü açılarak kilidin arkasına geçilmesini engellemek için `IVirtualDesktopManager` COM arabirimi kullanılır.
* Kilit penceresinin aktif masaüstünde olup olmadığı her saniye denetlenir; kullanıcı başka masaüstüne geçerse pencere zorla o masaüstüne taşınır (`MoveWindowToDesktop`).

### 3.5. Omuz Gözetleme ve Parmak İzi Analizi Koruması (`LockWindow.cs`)
* Kilit ekranındaki 0-9 sayısal tuş takımı, her açılışta ve her hatalı denemede rastgele karıştırılır (`Random.Shared.Next`).
* Böylece öğretmenin arkasındaki öğrenciler el hareketlerinden veya ekrandaki parmak izi lekelerinden şifreyi tahmin edemez.
* 5 hatalı denemeden sonra sistem 1 ile 10 dakika arasında kademeli geçici kilitlenmeye (`PasswordGate`) girer.

### 3.6. Odak Çalmayan Yüzen Çubuk (`UnlockedBar.cs`)
* Win32 pencere stiline `WS_EX_NOACTIVATE (0x08000000)` ve `WS_EX_TOOLWINDOW (0x0080)` bayrakları atanır.
* Bu sayede öğretmen tahtada ders anlatırken, slayt veya video oynatırken yüzen çubuğa dokunsa bile ders yazılımının odağı bozulmaz.

---

## 4. API Ağ Trafiği ve Veri İletişimi Şeması

EduMindX, merkezi yönetim sunucusu ile SignalR WebSocket protokolü üzerinden haberleşir:

### 4.1. Tahtanın Sunucuya Gönderdiği Veriler (Giden Trafik)
1. **Kalp Atışı (Heartbeat - Her 30 sn):**
   ```json
   { "AgentVersion": "1.5.1", "Locked": true }
   ```
2. **Olay Raporu (Events - Anlık veya Toplu):**
   ```json
   [
     {
       "EventType": "UNLOCK",
       "Reason": "QR",
       "InLesson": true,
       "OccurredAtUtc": "2026-11-11T05:00:00Z",
       "CommandGuid": null
     }
   ]
   ```
3. **Komut Onayı (Ack):** Uzaktan gelen kilitleme veya duyuru komutunun işlendiğini sunucuya bildirir (`Status: "DONE" | "FAILED"`).
4. **Gizlilik Teyidi:** Sistem; ekran görüntüsü, kamera, mikrofon, açılan dosya veya gezilen web siteleri gibi kişisel verileri **kesinlikle toplamaz ve iletmez**.

### 4.2. Sunucunun Tahtaya Gönderdiği Veriler (Gelen Trafik)
1. **Ders ve Kilit Planı (`SignedPlan`):** ECDSA-SHA256 imzalı haftalık ders programı, idare şifre hash'i ve tek kullanımlık günlük kod hash listesi.
2. **Uzaktan Komutlar (`BoardCommand`):** `Lock`, `Unlock`, `ShowAnnouncement`, `RefreshSchedule`, `SetServiceState`, `Reboot`, `Shutdown`.

---

## 5. Güvenlik Zafiyetleri ve Mimari Sınırlar Analizi

Araştırma kapsamında yazılımın mimarisinde tespit edilen zayıflıklar ve aşılma yolları şunlardır:

| Zafiyet Alanı | Açıklama | Risk | Savunma / Çözüm |
| :--- | :--- | :---: | :--- |
| **`Ctrl + Alt + Del` (SAS)** | Kullanıcı modu kancaları SAS menüsünü engelleyemez. GPO yoksa Görev Yöneticisi açılıp süreç sonlandırılabilir. | **Yüksek** | Kayıt Defteri'nden `DisableTaskMgr = 1` yapılmalı. |
| **DPAPI `LocalMachine` Kapsamı** | `config.dat` makinedeki herhangi bir standart kullanıcı tarafından çözülebilir; şifre hash'i offline kırılabilir. | **Orta-Yüksek** | Veriler yalnızca `SYSTEM` hesabının okuyabileceği ACL izinleriyle saklanmalı. |
| **Şifresiz Kod Listesi** | `used-codes.json` düz metindir. Dosya silinirse kullanılmış kodlar tekrar geçerli hale gelebilir. | **Orta** | Dosya DPAPI ile şifrelenmeli (`WriteProtected`). |
| **DLL Enjeksiyonu / Bellek Müdahalesi** | .NET uygulaması korumasızdır; yerel yönetici yetkisindeki harici bir araç bellekten kilidi devre dışı bırakabilir. | **Orta** | Windows AppLocker ile yetkisiz `.exe` ve `.dll` çalıştırması engellenmeli. |
| **İnternetsiz İlk Açılış** | Bilgisayar internetsiz açılırsa `BoardClock` senkronize olamaz ve BIOS saatine güvenir. | **Düşük-Orta** | Son bilinen güvenli zaman diske şifreli kaydedilip geriye gidişler engellenmeli. |
| **Tek Süreç Mimarisi** | İzleyici (Watchdog) servis yoktur. Süreç sonlandırılırsa tahta tamamen açık kalır. | **Orta** | Süreci takip eden hafif bir arka plan Windows Servisi eklenmeli. |

---

## 6. Sonuç ve Değerlendirme

EduMindX, akıllı tahta ortamında öğrencilerin klavye ve fare ile yapabileceği yaygın atlatma denemelerine (Alt+Tab, Görev Çubuğu, Sanal Masaüstü, Omuz Gözetleme) karşı **başarılı ve yaratıcı Windows API savunmaları** geliştirmiştir.

Bununla birlikte, tam kiosk güvenliğinin yalnızca uygulama koduna bırakılamayacağı; **Windows Grup İlkeleri (GPO), kısıtlı kullanıcı hesapları ve AppLocker** gibi işletim sistemi güvenlik katmanlarıyla desteklenmesinin zorunlu olduğu açık bir gerçektir.
