# Dubbed-Up: Steam, Workshop & Multiplayer Geliştirme ve İş Dağılım Planı

Bu doküman, projede eşzamanlı veya bağımsız çalışan **geliştiriciler ve AI asistanları** için ortak referans kaynağıdır. Çakışmaları önlemek ve işleri net paketler halinde yürütmek amacıyla hazırlanmıştır.

---

## 1. Projenin Mevcut Durumu (Özet)

* **Oyun Türü:** Video sahneleri üzerine oyuncuların kendi sesleriyle dublaj yaptığı, videoyla senkron izlediği ve eğlendiği parti oyunu.
* **Mevcut Tamamlanan Altyapı:**
  - `DubbedUp.Core`: Motor bağımsız saf C# iş mantığı (Oturumlar, Turlar, Oylama, Skor, Şema, TakeStore). 58/58 test başarılı.
  - `DubbedUp.Godot`: Godot 4.x tabanlı UI ekranları (`MainMenu`, `Setup`, `Recording`, `Playback`, `Voting`, `Results`), `GodotVoiceRecorder` (mikrofon), `SynchronizedScenePlayer` (anti-drift senkron oynatıcı).
  - `LocalSessionCoordinator`: Tüm yerel döngüyü tek merkezden yöneten koordinatör.

---

## 2. Yeni Vizyon & Hedefler (Steam & Multiplayer)

1. **Ana Mod: Co-op Dublaj (Oylamasız):** Her oyuncu sahnede bir karakteri seslendirir, ardından ortaya çıkan komik/absürt sahne hep birlikte canlı izlenir. Oylama isteğe bağlı bir parti modudur.
2. **Steam Workshop Sahne Desteği:** Topluluk kendi video sahnelerini (`scene.json` + `video.mp4`) Workshop'a yükleyebilir; oyuncular tek tıkla indirip oynayabilir.
3. **Steam P2P / Multiplayer:** Oyuncular Steam lobisinde toplanır, herkes kendi bilgisayarından ses kaydeder ve bitmiş video herkesin ekranında eşzamanlı oynatılır.

---

## 3. Bağımsız İş Paketleri (Workstreams & Görev Paylaşımı)

İki geliştirici veya AI asistanının **birbirini engellemeden ve kod çakışması yaşamadan** paralel çalışabilmesi için işler şu şekilde modüllere ayrılmıştır:

```text
+-----------------------------------------------------------------------------------------------+
|                                  İŞ PAKETLERİ (WORKSTREAMS)                                    |
+-----------------------------------------------------------------------------------------------+
| İŞ PAKETİ A (Core & Sahne Yönetimi):         | İŞ PAKETİ B (Godot UI & Co-op Modu):          |
| - GameMode (Coop / Competitive) tanımları    | - ScenePickerScreen (Sahne Seçici Arayüzü)     |
| - ScenePackageLoader (Dinamik MP4 / JSON)    | - Co-op Akışı (Kayıt -> Doğrudan Sinema Modu) |
| - Sahne Doğrulama & Dosya Tarama Testleri    | - Playback Ekranı Geliştirmeleri (Replay/Skip) |
| [Sahiplik: src/DubbedUp.Core/]               | [Sahiplik: src/DubbedUp.Godot/UI/]             |
+----------------------------------------------+------------------------------------------------+
| İŞ PAKETİ C (Steam Workshop Altyapısı):      | İŞ PAKETİ D (Steam Multiplayer & Lobi):       |
| - Workshop UGC Dizin Tarayıcısı              | - Steam / ENet Lobi Ekranı & Oda Kurulumu      |
| - Sahne İndirme & Yerel Klasöre Bağlama      | - Ses Dosyalarının Ağ Üzerinden Dağıtımı      |
| - Örnek MP4 Sahne Paketleri (Content/)       | - Senkronize Başlatma (Network Master Clock)   |
| [Sahiplik: src/DubbedUp.Godot/Workshop/]     | [Sahiplik: src/DubbedUp.Godot/Network/]        |
+-----------------------------------------------------------------------------------------------+
```

---

## 4. İş Paketlerinin Detaylı Görev Listesi

### 📦 İş Paketi A: Core & Dinamik Sahne Paketleri
* **Amaç:** Oyunun sabit kodlanmış sahne yerine herhangi bir klasördeki `scene.json` + `video.mp4` dosyalarını yükleyebilmesini sağlamak ve `GameMode` desteği eklemek.
* **Yapılacaklar:**
  1. `DubbedUp.Core.Game.GameMode` enum'ı (`CoopDubbing`, `CompetitiveVoting`).
  2. `ScenePackage` ve `IScenePackageLoader` kontratı (klasörden sahne metaverisi ve video yolu okuma).
  3. `DubbedUp.Core.Tests` altına sahne yükleme testlerinin eklenmesi.
* **Değişecek Dizinler:** `src/DubbedUp.Core/Scenes/`, `tests/DubbedUp.Core.Tests/`.

### 📦 İş Paketi B: Godot UI & Co-op Dublaj Akışı
* **Amaç:** Sahne seçici arayüzü eklemek ve oylamayı atlayarak doğrudan nihai dublajı sinema gibi izleten akışı bağlamak.
* **Yapılacaklar:**
  1. `ScenePickerScreen.tscn` ve `.cs` (küçük resimli sahne seçim ekranı).
  2. `SetupScreen` üzerinde Oyun Modu seçeneği (Co-op / Oylamalı).
  3. Co-op modunda `PlaybackScreen` bittiğinde doğrudan "Tekrar İzle", "Yeni Sahne", "Ana Menü" seçeneklerinin sunulması.
* **Değişecek Dizinler:** `src/DubbedUp.Godot/UI/Screens/`, `src/DubbedUp.Godot/LocalSession/`.

### 📦 İş Paketi C: Steam Workshop UGC Entegrasyonu
* **Amaç:** Kullanıcıların Steam Workshop'tan indirdiği sahneleri otomatik algılayıp oyunda oynanabilir hale getirmek.
* **Yapılacaklar:**
  1. Steam Workshop klasör tarayıcısı (`WorkshopSceneProvider`).
  2. Workshop metaverisi okuyucu ve sahne listesine ekleyici.
  3. Örnek telifsiz MP4 sahne paketi hazırlanması (`Content/OfficialScenes/`).
* **Değişecek Dizinler:** `src/DubbedUp.Godot/Workshop/`, `Content/`.

### 📦 İş Paketi D: Multiplayer & Ağ Üzerinden Dublaj
* **Amaç:** Oyuncuların farklı cihazlardan odaya girip kendi repliklerini söylemesi ve ortak izlemesi.
* **Yapılacaklar:**
  1. Lobi arayüzü (`LobbyScreen`) ve oyuncu hazır durumları.
  2. Kaydedilen `.wav` seslerinin host/diğer oyunculara aktarılması.
  3. Ortak zaman sayacı (senkron video başlatma).
* **Değişecek Dizinler:** `src/DubbedUp.Godot/Network/`.

---

## 5. Koordinasyon & Çakışma Önleme Kuralları

1. **Dal (Branch) İsimlendirmesi:** `feat/<is-paketi>-<aciklama>` (Örn: `feat/scene-packages`, `feat/coop-flow`, `feat/workshop-loader`).
2. **Hotspot Kuralları:** `DubbedUp.sln`, `project.godot` veya paylaşılan temel interfacelerde değişiklik yapılacaksa diğer geliştiriciye önceden haber verilmelidir.
3. **Core Bağımsızlığı:** `DubbedUp.Core` içerisinde kesinlikle Godot veya Steamworks kütüphanesi kullanılmaz.
