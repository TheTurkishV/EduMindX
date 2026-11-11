# EduMindX Akıllı Tahta (Smart Board Kiosk & Management System)

EduMindX, eğitim kurumlarındaki dokunmatik akıllı tahtaların güvenliğini sağlamak, ders ve teneffüs saatlerine göre otomatik kilitleyip açmak ve merkezi bir sunucu üzerinden uzaktan yönetilmesini sağlamak amacıyla geliştirilmiş kurumsal bir **Kiosk ve Tahta Yönetim Sistemi** yazılımıdır.

Proje, **.NET 8.0** ve **WPF (Windows Presentation Foundation)** teknolojileri kullanılarak geliştirilmiştir.

---

## 🌟 Öne Çıkan Özellikler

- **Ders & Teneffüs Bazlı Otomatik Kilitleme:** Sunucudan çekilen kriptografik imzalı haftalık ders programına göre ders bitiminde (`LESSON_END`) tahta otomatik kilitlenir; ders dışı açılmalarda serbest süre (`TIMEOUT`, varsayılan 10 dk) bitiminde kapanır.
- **Çok Yöntemli Güvenli Kilit Açma:**
  - 📱 **Dinamik QR Kod:** Öğretmen cep telefonu kamerasıyla okutarak saniyeler içinde kilit açabilir.
  - 🔑 **Tek Kullanımlık Günlük Kod:** İnternet kesintilerinde idareden alınan güne özel 8 haneli tek kullanımlık çevrimdışı kodlarla açılabilir.
  - 🔒 **Ana İdare Parolası:** Kurumun yetkili şifresi ile doğrudan kilit açma.
  - 🌐 **Uzaktan Açma:** İdare web panelinden tek tıkla veya belirli bir saate kadar açık kalacak şekilde uzaktan kilit açma.
- **Sertleştirilmiş Kiosk Güvenliği (Anti-Bypass):**
  - **Windows Gezgini Dondurma (`ShellGuard`):** Kilitliyken `explorer.exe` Native API (`NtSuspendProcess`) ile dondurulur; Başlat menüsü, arama ve UWP kabuk pencereleri sonlandırılır.
  - **Donanımsal Klavye Engelleme (`KeyboardBlocker`):** Win32 Low-Level Hook (`WH_KEYBOARD_LL`) ile Win tuşları, Alt+Tab, Alt+F4, Alt+Esc ve Ctrl+Esc kısayolları donanım seviyesinde yutulur.
  - **Sanal Masaüstü Takibi (`VirtualDesktops`):** `IVirtualDesktopManager` COM arayüzü ile öğrencilerin `Win + Ctrl + D` ile yeni masaüstüne kaçması engellenir; kilit ekranı daima aktif masaüstüne çekilir.
  - **Dokunmatik Hareket & Tablet Modu Kısıtlaması:** 3 ve 4 parmakla görev görünümünü açma hareketleri ve Windows Tablet Modu Kayıt Defteri'nden kilitlenir.
  - **Omuz Gözetleme Koruması (Anti-Shoulder Surfing):** Sayısal tuş takımındaki rakamlar her açılışta ve her denemede rastgele karıştırılır (`Random.Shared.Next`).
  - **Kaba Kuvvet (Brute-Force) Koruması:** 5 hatalı denemeden sonra sistem kademeli kilitlenmeye (1 dk ile 10 dk arası) girer.
  - **Hile Korumalı Saat Mekanizması (`BoardClock`):** Windows saatinin geri/ileri alınmasına karşı donanımsal `Stopwatch` tick sayacı ile zamanı ölçer.
- **Odak Çalmayan Yüzen Çubuk (`UnlockedBar`):** Tahta açıkken ekranın kenarında durur; Win32 `WS_EX_NOACTIVATE` bayrağı sayesinde PowerPoint, PDF veya tahta uygulamalarının odağını bozmaz. Süre bitimine 2 dakika kala sarı/turuncu uyarı moduna geçer.
- **Merkezi Duyuru Sistemi (`AnnouncementWindow`):** İdareden gelen acil durum ve anonsları kilit ekranının da üzerinde tam ekran kart olarak gösterir.
- **Kriptografik Güvenlik:**
  - Ders planları sunucu tarafından **ECDSA-SHA256** ile imzalanır ve doğrulanır.
  - Şifreler ve günlük kodlar **BCrypt** ile özetlenir.
  - Yerel veriler (`config.dat`, `plan.dat`) **Windows DPAPI (LocalMachine)** ile şifrelenir.
- **Arıza Korumalı Otomatik Güncelleme:** Yalnızca tahta **kilitli, ders dışı ve çevrimiçi** iken çalışır. Güncelleme paketi sisteme uygulanmadan önce izole ortamda `--self-test` ile test edilir; hata olursa otomatik geri alma (rollback) yapılır.

---

## 🏗️ Proje Mimarisi

```
EduMindX
│
├── EduMindX.SmartBoard.Core (.NET 8.0 Sınıf Kütüphanesi)
│   ├── Kilit ve Zaman Durum Makinesi (LockEngine, LockPlan, LessonSlot)
│   ├── Kriptografik İmza Doğrulama (Signatures, PlanVerifier, UpdateVerifier)
│   ├── Donanım Sayaçlı Saat (BoardClock)
│   ├── Güvenlik Kapısı ve Kod Takibi (PasswordGate, PinCheck, UsedUnlockCodes)
│   └── Ağ / WAF Uyumluluk Katmanı (WafSafeHeadersHandler, UpdateManifest)
│
└── EduMindX.SmartBoard (.NET 8.0-windows WPF Uygulaması)
    ├── Kullanıcı Arayüzü (LockWindow, UnlockedBar, AnnouncementWindow)
    ├── Windows Kabuk & Güvenlik Korumaları (ShellGuard, KeyboardBlocker, VirtualDesktops)
    ├── Donanım & Dokunmatik Kısıtlamaları (TabletMode, TouchGestures)
    ├── Gerçek Zamanlı Bulut Bağlantısı (DeviceConnection - SignalR Client)
    ├── Şifreli Yerel Depolama (BoardStorage - Windows DPAPI)
    └── Otomatik Güncelleme Motoru (UpdateRunner, UpdateTask)
```

---

## 🚀 Derleme ve Çalıştırma

### Gereksinimler
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 / 11 (Akıllı Tahta donanımı veya dokunmatik ekran önerilir)
- Visual Studio 2022 veya Visual Studio Code

### Derleme Komutu
Projeyi derlemek için terminalden çözüm klasöründe aşağıdaki komutu çalıştırın:

```powershell
dotnet build EduMindX.sln -c Release
```

### CLI Parametreleri
Uygulama arka plan servisleri ve test için aşağıdaki komut satırı parametrelerini destekler:

- `--check <ApiBaseUrl> <ResultJsonPath>`: Sunucu bağlantısını, token geçerliliğini ve plan imzasını test eder.
- `--update`: Arka planda güncelleme motorunu çalıştırır.
- `--self-test <ResultJsonPath>`: Kurulan binary'nin sorunsuz çalıştığını ve yapılandırmayı okuyabildiğini doğrular.
- `--register-update-task`: Windows Görev Zamanlayıcısı'na SYSTEM yetkili otomatik güncelleme görevini kaydeder.

---

## 🔒 Güvenlik Notları
EduMindX, öğrenci seviyesindeki standart atlatma girişimlerine (Alt+Tab, Alt+F4, Görev Çubuğu, Sanal Masaüstü vb.) karşı çok katmanlı savunma içerir. Ancak tam bir Kiosk güvenliği için Windows işletim sisteminde:
1. Öğrenci/Tahta oturumunun **standart (yönetici olmayan)** kullanıcı yapılması,
2. Windows Kayıt Defteri veya Grup İlkesi (GPO) üzerinden `DisableTaskMgr` (Görev Yöneticisi engelleme) politikasının aktif edilmesi önerilir.

---

## 📄 Lisans
Bu proje eğitim kurumları ve akıllı tahta yönetimi amacıyla geliştirilmiş açık kaynaklı bir referans mimaridir.
