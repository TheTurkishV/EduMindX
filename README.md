# EduMindX — Akıllı Tahta Kitleme Programı 

> [!IMPORTANT]
> **YASAL UYARI VE EĞİTİM BEYANI (DISCLAIMER):**  
> Bu depo, yalnızca **yazılım mimarisi, siber güvenlik, tersine mühendislik ve Windows Kiosk savunma sistemlerini incelemek** amacıyla hazırlanmış **eğitim ve araştırma odaklı** bir açık kaynak referans çalışmasıdır.  
> Herhangi bir ticari amaç taşımamakta olup; okul sistemlerinde kullanılan kiosk kilit yazılımlarının Windows işletim sistemi seviyesindeki çalışma mantığını, düşük seviyeli Win32 API kullanımlarını ve olası tasarım sınırlarını akademik/eğitsel olarak analiz etmek isteyen geliştiricilere rehberlik etmesi hedeflenmiştir.

---

## 🎯 Bu Projede Neler İncelenebilir? (Eğitim Konuları)

Bu kaynak kod tabanı, Windows üzerinde çalışan ileri düzey masaüstü ve güvenlik yazılımlarının tasarımına dair zengin bir öğrenme materyali sunmaktadır:

### 1. Windows Kabuk (Shell) Manipülasyonu ve Süreç İzolasyonu
* **`ntdll!NtSuspendProcess` & `NtResumeProcess` Kullanımı ([ShellGuard.cs](EduMindX.SmartBoard/EduMindX.SmartBoard/ShellGuard.cs)):**  
  Windows Gezgini'nin (`explorer.exe`) geçici olarak dondurulması, görev çubuğu ve masaüstü etkileşimlerinin kesilmesi ve kilit açıldığında ardışık suspend sayaçlarının güvenle eritilerek sistemin uyandırılması.
* **UWP Kabuk Host Süreçlerinin Yönetimi:**  
  Başlat menüsü (`StartMenuExperienceHost`) ve Windows Arama (`SearchApp`) gibi arka plan bileşenlerinin yaşam döngüsü.

### 2. Düşük Seviyeli Donanım Kancaları (Low-Level Hooks)
* **`WH_KEYBOARD_LL` Klavye Kancası ([KeyboardBlocker.cs](EduMindX.SmartBoard/EduMindX.SmartBoard/KeyboardBlocker.cs)):**  
  Windows tuşları (LWin/RWin), Alt+Tab, Alt+F4 ve Alt+Esc gibi kritik işletim sistemi kısayollarının kullanıcı modunda yakalanıp engellenmesi (`return 1`).

### 3. Windows 10/11 Sanal Masaüstü COM Entegrasyonu
* **`IVirtualDesktopManager` Arayüzü ([VirtualDesktops.cs](EduMindX.SmartBoard/EduMindX.SmartBoard/VirtualDesktops.cs)):**  
  Kullanıcıların `Win + Ctrl + D` ile yeni bir sanal masaüstü oluşturarak tam ekran kilit pencerelerinden kaçma girişimlerinin tespit edilmesi ve pencerenin aktif masaüstüne dinamik taşınması (`MoveWindowToDesktop`).

### 4. Dokunmatik Ekran ve Tablet Modu Sınırlandırmaları
* **Registry Seviyesinde Donanım Yönetimi ([TouchGestures.cs](EduMindX.SmartBoard/EduMindX.SmartBoard/TouchGestures.cs) & [TabletMode.cs](EduMindX.SmartBoard/EduMindX.SmartBoard/TabletMode.cs)):**  
  Çoklu parmak (3 ve 4 parmak) dokunmatik hareketlerin Windows Kayıt Defteri üzerinden kapatılması ve otomatik tablet moduna geçişin önlenmesi.

### 5. Hile Korumalı Saat Mekanizması (Anti-Tamper Clock)
* **Monotonic Tick ile Bağımsız Zaman Ölçümü ([BoardClock.cs](EduMindX.SmartBoard.Core/EduMindX.SmartBoard.Core/BoardClock.cs)):**  
  İşletim sistemi saatinin (BIOS/Windows Time) kullanıcı tarafından geri/ileri alınmasına karşı, işlemcinin yüksek hassasiyetli sayaçları (`Stopwatch.GetTimestamp`) kullanılarak sunucu UTC zamanının güvenilir şekilde yürütülmesi.

### 6. Omuz Gözetleme ve Parmak İzi Analizi Koruması (Anti-Shoulder Surfing)
* **Dinamik Karıştırılan Tuş Takımı ([LockWindow.cs](EduMindX.SmartBoard/EduMindX.SmartBoard/LockWindow.cs)):**  
  Fiziksel dokunmatik ekranlarda parmak izi lekesinden veya arkadan bakan öğrencilerin el hareketlerinden şifre tahminini imkansız kılmak için rakamların (0-9) her açılışta rastgele sıralanması (`Random.Shared.Next`).

### 7. Odak Çalmayan Yüzen Arayüz (Non-Activating Floating Bar)
* **`WS_EX_NOACTIVATE` & `WS_EX_TOOLWINDOW` ([UnlockedBar.cs](EduMindX.SmartBoard/EduMindX.SmartBoard/UnlockedBar.cs)):**  
  Tahta açıkken ekranda duran, dokunulduğunda veya sürüklendiğinde arka planda çalışan sunum/video/ders yazılımlarının odağını (focus) bozmayan modern bir WPF arayüz tasarımı.

### 8. Kriptografi ve Güvenli Veri Depolama
* **ECDSA-SHA256 Dijital İmza Doğrulaması ([Signatures.cs](EduMindX.SmartBoard.Core/EduMindX.SmartBoard.Core/Signatures.cs) & [PlanVerifier.cs](EduMindX.SmartBoard.Core/EduMindX.SmartBoard.Core/PlanVerifier.cs)):**  
  Ders planlarının ve güncelleme manifestolarının asimetrik kriptografi ile doğrulanması.
* **Windows DPAPI Makine Koruması ([BoardStorage.cs](EduMindX.SmartBoard/EduMindX.SmartBoard/BoardStorage.cs)):**  
  Hassas yapılandırma verilerinin `ProtectedData.Protect` ile işletim sistemi anahtarıyla şifrelenmesi ve atomik dosya takası (`.tmp` -> `File.Move`).

---

## 📂 Proje Yapısı

```
EduMindX
│
├── EduMindX.SmartBoard.Core (.NET 8.0 Bağımsız Çekirdek Kütüphane)
│   ├── Kilit ve Zaman Durum Makinesi (LockEngine, LockPlan, LessonSlot)
│   ├── Kriptografik İmza Doğrulama (Signatures, PlanVerifier, UpdateVerifier)
│   ├── Hile Korumalı Saat Mekanizması (BoardClock)
│   ├── Parola ve Güvenlik Kapısı (PasswordGate, PinCheck, UsedUnlockCodes)
│   └── WAF Güvenli Ağ Katmanı (WafSafeHeadersHandler, UpdateManifest)
│
└── EduMindX.SmartBoard (.NET 8.0-windows WPF Kiosk Uygulaması)
    ├── Kullanıcı Arayüzü (LockWindow, UnlockedBar, AnnouncementWindow)
    ├── Windows Kabuk & Güvenlik Korumaları (ShellGuard, KeyboardBlocker, VirtualDesktops)
    ├── Donanım & Dokunmatik Kısıtlamaları (TabletMode, TouchGestures)
    ├── Gerçek Zamanlı Bulut Bağlantısı (DeviceConnection - SignalR Client)
    ├── Şifreli Yerel Depolama (BoardStorage - Windows DPAPI)
    └── Otomatik Güncelleme Motoru (UpdateRunner, UpdateTask)
```

---

## 🔍 İncelenen Güvenlik Zafiyetleri ve Mimari Limitler

Eğitim araştırmamız kapsamında bu tür kullanıcı modu (User-mode) kiosk sistemlerinde karşılaşılabilecek temel güvenlik sınırları ve zafiyetler şunlardır:

1. **`Ctrl + Alt + Del` (SAS) Engellenemezliği:**  
   Windows mimarisinde kullanıcı modu kancaları (`WH_KEYBOARD_LL`) SAS tuş kombinasyonunu yakalayamaz. Windows Grup İlkesi (GPO) ile `DisableTaskMgr` aktif edilmediği takdirde Görev Yöneticisi açılarak süreç sonlandırılabilir.
2. **DPAPI `LocalMachine` Kapsamı:**  
   `DataProtectionScope.LocalMachine` kullanıldığında, o makinedeki yetkisiz herhangi bir kullanıcı dahi şifrelenmiş `config.dat` dosyasını çözebilir ve hash'lenmiş parolayı çevrimdışı (offline brute-force) saldırıya tabi tutabilir.
3. **Tek Süreç Mimarisi:**  
   İzleyici (Watchdog) bir servis bulunmadığında, ana uygulamanın kapatılması veya çökmesi durumunda tahta korumasız açık masaüstüne döner.

> Detaylı güvenlik analizi ve savunma mekanizmaları için depodaki [EduMindX_Inceleme_Raporu.md](EduMindX_Inceleme_Raporu.md) dokümanını inceleyebilirsiniz.

---

## 🛠️ Yerel İnceleme ve Derleme

### Gereksinimler
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 veya Windows 11
- Visual Studio 2022 / VS Code

### Projeyi Derleme
```powershell
dotnet build EduMindX.sln -c Release
```

---

## ⚖️ Lisans ve Sorumluluk
Bu kaynak kodlar yalnızca eğitim, akademik araştırma ve güvenlik geliştirme amacıyla incelenmek üzere sunulmuştur. Kodların amacı dışında veya yetkisiz sistemlerde kullanılmasından kaynaklanabilecek sorumluluklar tamamen uygulayan kişiye aittir.
