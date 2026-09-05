# 🌟 Radial Launcher (Pie Command Center)

<div align="center">
  <img src="Resources/app.ico" width="128" height="128" alt="Radial Launcher Logo" />
  <br />
  <h3>Modern, Donanım Hızlandırmalı Dairesel Başlatıcı ve Windows Komuta Merkezi</h3>
  <p>Windows için akıcı animasyonlar, açık pencere yöneticisi, oyun kütüphane entegrasyonu ve sistem aksiyonları sunan yeni nesil dairesel menü.</p>
</div>

---

## 🚀 Öne Çıkan Özellikler

### 1. 🪟 Entegre Açık Pencere Yöneticisi (Window Switcher)
- Bilgisayarda o an açık olan tüm uygulamaları anında tespit eder ve gerçek ikonlarıyla dairesel menüde listeler.
- **Sol Tık:** İlgili pencereye yumuşak geçiş yapar (`SetForegroundWindow`).
- **Orta Tık (Tekerlek):** Farenin orta tuşu ile tıklandığında ilgili pencereyi/uygulamayı anında kapatır (`WM_CLOSE`).

### 2. 📁 Hiyerarşik Alt Menüler (Katmanlar / Sub-menus)
- Öğeler `SUBMENU` türünde tanımlanabilir.
- Alt menüye tıklandığında menü katmanın içine geçer ve merkez buton otomatik olarak **⬅ Geri** butonuna dönüşür.
- Sınırsız derinlikte hiyerarşik menü navigasyonu sunar.

### 3. ⚡ Doğrudan Sistem ve Medya Komutları (Call Function)
- Herhangi bir üçüncü parti programa ihtiyaç duymadan doğrudan işletim sistemi seviyesinde çalışır:
  - **Medya:** Ses Aç (+2%), Ses Kıs (-2%), Sesi Kapat/Aç, Oynat/Durdur, Sonraki Parça, Önceki Parça
  - **Windows & Araçlar:** Masaüstünü Göster (Win+D), Ekran Alıntısı Aracı (Win+Shift+S), Görev Yöneticisi, Bilgisayarı Kilitle, Geri Dönüşüm Kutusunu Boşalt.

### 4. 🎮 Otomatik Steam & Epic Games Algılama
- Bilgisayardaki Steam kütüphanelerini (`libraryfolders.vdf`, `appmanifest_*.acf`) ve Epic Games manifest dosyalarını tek tıkla tarar.
- Yüklü oyunları menüye ekler ve resmi yüksek çözünürlüklü `.ico` dosyalarını otomatik eşleştirir.

### 5. 🌐 Akıllı Web Favicon Algılama
- Eklenen web sitelerinin (YouTube, GitHub, Twitter vb.) favicon'larını Google Favicon API ile otomatik indirir, `%LOCALAPPDATA%` üzerinde önbelleğe alır.

### 6. 🔍 Anlık Arama (Search-as-you-type)
- Menü açıkken klavyeden doğrudan yazmaya başlayarak arama yapabilirsiniz.
- Eşleşen öğeler anında filtrelenir, `Enter` tuşuna basıldığında ilk sonuç çalıştırılır.

### 7. 🎨 Glassmorphism Tasarım & 60 FPS Animasyonlar
- WPF ve DirectX donanım hızlandırmalı modern koyu arayüz.
- Fare ile üzerine gelinen öğenin dinamik olarak büyümesi (Scale Hover Animation) ve merkezde tam adının gösterilmesi.
- 5 farklı hazır tema: Dark, Light, Midnight Blue, Purple Haze, Forest.

---

## 📊 Karşılaştırmalı Rakip Analizi

Radial Launcher, piyasadaki mevcut dairesel çözümlerin zayıf yönlerini gidermek ve modern bir alternatif sunmak amacıyla geliştirilmiştir:

| Kriter | **MouseLauncher** | **Circle Dock** | **MightyPie** | **Radial Launcher (Biz)** |
| :--- | :--- | :--- | :--- | :--- |
| **Geliştirme Mimarisi** | PureBasic / GDI | WinForms .NET 2.0 (2008) | C# WPF (Basit) | **Modern .NET 7 + WPF (DirectX Donanım Hızlandırmalı)** |
| **Kapasite ve Düzen** | Sabit dilimler, taşma sorunu | Spiral dönen karışık liste | Sabit 8 buton kısıtı | **Altın Oranlı 15'li Akıllı Sayfalama + Kategori Filtresi** |
| **Pencere Geçişi (Alt+Tab)**| ❌ Yok | ❌ Yok | ⚠️ Basit liste | **✅ Canlı İkonlar + Sol Tık Odaklanma + Orta Tık Kapatma** |
| **Alt Menü / Katman Desteği**| ❌ Yok | ❌ Harici klasör | ⚠️ Sabit 3 katman | **✅ Dinamik Derinlik + "⬅ Geri" Navigasyon Yığını** |
| **Sistem Komutları (Aksiyonlar)**| ❌ Yok | ❌ Yok | ⚠️ Kısıtlı | **✅ Medya, Ses, Ekran Alıntısı, Masaüstü, Kilit, Görev Yön.** |
| **Oyun Platform Entegrasyonu**| ❌ Yok | ❌ Yok | ❌ Yok | **✅ Steam & Epic Games Otomatik Kütüphane & İkon Tarama** |
| **Web Favicon Çekme** | ❌ Yok | ❌ Yok | ❌ Yok | **✅ Google Favicon Crawler + Yerel Önbellekleme** |
| **Anlık Arama** | ❌ Yok | ❌ Yok | ❌ Yok | **✅ Menü Açıkken Doğrudan Yazarak Filtreleme** |
| **Veritabanı Altyapısı** | `.ini` dosyası | `.xml` (bozulabilir) | Bellek İçi | **✅ ACID SQLite Veritabanı + JSON İçe/Dışa Aktarım** |
| **Yönetim Arayüzü** | Basit ayar ekranı | Karışık ayarlar | Sınırlı ayar menüsü | **✅ Koyu Temalı Sürükle-Bırak Sıralamalı Yönetim Paneli** |

---

## ⌨️ Varsayılan Kısayollar ve Kontroller

| İşlem | Tuş / Fare Hareketi |
| :--- | :--- |
| **Menüyü Aç** | Fare Orta Tuşu (Tekerlek Tıklaması) |
| **Öğeyi Başlat** | Fare Sol Tık |
| **Pencereyi Kapat** | Açık Pencereler modunda Fare Orta Tuş |
| **Alt Menüye Gir** | Submenu öğesine Sol Tık |
| **Geri Dön** | Merkezdeki `⬅` Butonu veya `Esc` / `Backspace` |
| **Sayfa / Kategori Değiştir** | Fare Tekerleği Kaydırma veya Sol/Sağ Ok Tuşları |
| **Arama Yap** | Menü açıkken herhangi bir harfe basmaya başlayın |
| **Favori Yap / Çıkar** | Öğe üzerinde Sağ Tık |
| **Yönetim Paneli** | Merkezdeki butona Sağ Tık veya Sistem Tepsisinden "Settings" |

---

## 🛠️ Kurulum ve Derleme

Gereksinimler:
- Windows 10 / Windows 11
- [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) veya üzeri

Projeyi derlemek ve çalıştırmak için:
```bash
git clone https://github.com/mephisto-mert/radial.git
cd radial
dotnet build
dotnet run
```

---

## 📄 Lisans
Bu proje açık kaynaklıdır ve MIT lisansı altında dağıtılmaktadır.
