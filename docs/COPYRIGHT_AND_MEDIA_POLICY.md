# Copyright & Zero-Media Server Protection Policy

Bu doküman, **Dubbed Up** oyununun telif hakkı (Copyright / DMCA / Fikir ve Sanat Eserleri Kanunu) ihlallerine ve yasal sorumluluklara karşı **%100 korunmasını** sağlayan bağlayıcı mimari ve sunucu kurallarını tanımlar.

---

## 🚫 1. Sıfır Medya Sunucu Politikası (Zero-Media Server Policy)

> [!CAUTION]
> **Kritik Yasal Kural:**
> Dubbed Up oyununa ait hiçbir merkezi sunucu, aracı sunucu (relay server) veya bulut veritabanı **asla video, film, dizi, müzik veya üçüncü taraf telifli medya dosyalarını barındırmaz, depolamaz, işlemez veya dağıtmaz.**

### Sunucuya Neler Girebilir? (İzin Verilen Hafif Veriler)
Sunucular ve ağ katmanı yalnızca telifsiz, saf metinsel **Edit / Metaveri** dosyalarını iletir:
- ✅ **`scene.json` Metaverisi:** Sahne başlığı, karakter isimleri, replik metinleri (altyazılar), zaman kutucuklarının başlangıç/bitiş saniyeleri (`StartMilliseconds`, `EndMilliseconds`).
- ✅ **Kullanıcı Ses Kayıtları (`Voice Takes`):** Oyuncuların kendi mikrofonlarıyla seslendirdikleri anlık sesler (yalnızca odadaki oyuncular arasında P2P veya geçici RAM tamponuyla iletilir, sunucuda kalıcı saklanmaz).
- ✅ **Oyun Durumu:** Lobi oyuncu listesi, hazır durumu, karakter seçim kilitleri.

### Sunucuya Neler Asla Giremez? (Kesinlikle Yasak Olanlar)
- ❌ **Video Dosyaları:** `.mp4`, `.webm`, `.ogv`, `.mov`, `.mkv` vb.
- ❌ **Orijinal Film / Dizi Müzikleri ve Ses Parçaları:** `audio.wav`, `background.wav`, `vocals.wav` vb.

---

## 📁 2. Medya Nasıl Dağıtılır? (İstemci Tabanlı & Workshop Modeli)

Telif risklerini tamamen sıfırlamak için tüm video dosyaları **yalnızca oyuncuların kendi yerel bilgisayarlarında** bulunur:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                          İSTEMCİ TABANLI DAĞITIM                        │
├───────────────────────────────────┬─────────────────────────────────────┤
│ 1. Steam Workshop UGC             │ 2. Yerel Sahne İçe Aktarma (Local)  │
├───────────────────────────────────┼─────────────────────────────────────┤
│ • Kullanıcılar sahneleri doğrudan │ • Oyuncular kendi videolarını yerel │
│   Steam Workshop üzerinden paylaşır│   olarak içe aktarır (`user://...`) │
│ • Telif sorumluluğu Steam UGC     │ • Video yalnızca kullanıcının kendi │
│   kullanıcı sözleşmesine aittir.  │   sabit diskinde kalır.             │
└───────────────────────────────────┴─────────────────────────────────────┘
```

---

## 🔒 3. Çok Oyunculu Odada Sahne Eşleşmesi (Scene Matching Protocol)

Bir lobide oyun başlamadan önce video transferi **yapılmaz**, bunun yerine sahnenin her iki oyuncuda da var olduğu doğrulanır:

1. **Host Sahne Bildirimi:**
   - Host odaya bir sahne seçtiğinde ağa yalnızca `SceneId` ve `WorkshopItemId` (varsa `scene.json` edit verisi) gönderir.
2. **İstemci Yerel Kontrolü:**
   - Odaya bağlı diğer oyuncular kendi yerel `workshop_scenes` veya Steam Workshop klasörlerinde bu `SceneId`'ye sahip videonun olup olmadığını kontrol eder.
3. **Sahnesi Olmayan Oyuncu:**
   - Eğer istemcide video yoksa, oyun istemciye **"Bu sahne Steam Workshop'ta mevcut, indirmek için tıklayın"** diyerek Steam Workshop aboneliği açar.
   - Video asla host'un bilgisayarından veya oyun sunucusundan doğrudan aktarılmaz (böylece korsan video yayma/dağıtma suçu tamamen engellenir).

---

## 🏛️ 4. Resmi Oyun İçi İçerikler (Official Built-in Content)

Oyunla birlikte resmi olarak gelen (`Content/OfficialScenes`) sahneler için kurallar:
- **%100 Hakları Alınmış / Orijinal İçerik:** Yalnızca Dubbed Up için özel üretilmiş animasyonlar veya ticari lisansı yazılı olarak kanıtlanmış telifsiz sahneler (`provenance.json` dosyasıyla) resmi içeriğe dahil edilebilir.
- **Test Sahneleri Ayrımı:** Geliştirme aşamasında test amacıyla kullanılan internet videoları resmi paketlere dahil edilemez; sadece yerel geliştirici ortamında kalır.

---

## ⚖️ 5. Yasal Sorumluluk Reddi ve DMCA Güvenli Liman (Safe Harbor)

1. **Kullanıcı Tarafından Oluşturulan İçerik (UGC):**
   - Kullanıcıların kendi yükledikleri sahneler "Kullanıcı İçeriği" kapsamındadır. Oyun geliştiricileri kullanıcıların yerel bilgisayarlarında oynattıkları videolardan sorumlu tutulamaz.
2. **Kaldırma Bildirimleri (Notice & Takedown):**
   - Steam Workshop üzerinde paylaşılan herhangi bir telifli içerik Steam DMCA prosedürlerine tabidir ve telif sahibinin bildirimiyle Steam tarafından kaldırılır.
