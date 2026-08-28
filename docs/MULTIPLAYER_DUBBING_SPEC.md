# Multiplayer Synchronized Dubbing & Steam Networking Specification

Bu doküman, **Çok Oyunculu (Multiplayer) Karakter Seçimi, Seslendirme ve Senkronize İzleme** sisteminin mimarisini ve adım adım geliştirme planını tanımlar. Oylama sistemi bu aşamada ertelenmiş olup, öncelik **ortak eşzamanlı dublaj deneyimi** ve **Steam çoklu oyuncu altyapısına** verilmiştir.

---

## 🎯 Temel Vizyon & Akış

```text
[Steam / ENet Lobisi] ───> [Sahne Seçimi] ───> [Canlı Karakter Seçimi & Kilitleme] ───> [Kişiselleştirilmiş Kayıt] ───> [Senkron Ortak İzleme]
 (Arkadaşını Davet Et)     (Host Sahne Açar)    (Karakterler Kilitlenir / Paylaşılır)     (Herkes Kendi Rolünü Söyler)    (Birlikte Canlı Sinema)
```

---

## 1. 🎭 Karakter Seçimi ve Kilitleme Mekanizması (Character Claim & Multi-Select)

### Kurallar:
1. **Canlı Karakter Görünümü:** Host bir sahne seçtiğinde (`scene.json`), sahnedeki tüm karakterler (`suspect`, `speed`, vb.) lobideki tüm bağlı oyuncuların ekranında listelenir.
2. **Karakter Kilitleme (Claim & Lock):**
   - Bir oyuncu bir karaktere tıkladığında o karakter o oyuncuya atanır ve diğer oyuncular için görsel olarak **Kilitli (Locked)** duruma geçer.
   - Seçilen karakterin üzerinde o oyuncunun Steam adı ve avatarı belirir.
3. **Çoklu Karakter Seçimi (Multi-Character Assignment):**
   - Eğer sahnede oyuncu sayısından fazla karakter varsa (Örn: 4 karakter, 2 oyuncu), bir oyuncu **birden fazla karakteri** seçebilir.
   - Bir oyuncu seçtiği karakteri bırakabilir (Unclaim) veya kalan boş karakterleri diğer oyuncu alabilir.
4. **Boşta Kalan Karakter Kontrolü:**
   - Sahne başlamadan önce tüm karakterlerin en az bir oyuncuya atanmış olması doğrulanır (veya boşta kalan karakterler otomatik olarak host'a atanır).

### Veri Modeli ve Ağ Mesajı:
```csharp
public sealed class CharacterClaimState
{
    public string CharacterId { get; set; } = "";
    public string CharacterName { get; set; } = "";
    public long ClaimedByPeerId { get; set; } = 0;
    public string ClaimedByPlayerName { get; set; } = "";
    public bool IsLocked => ClaimedByPeerId > 0;
}
```

---

## 2. 🎙️ Kişiselleştirilmiş Çok Oyunculu Kayıt (Distributed Recording)

### Kurallar:
1. **Yalnızca Kendi Repliklerini Kaydetme:**
   - Kayıt stüdyosuna (`RecordingScreen`) girildiğinde, her oyuncu **sadece kendi seçtiği karaktere ait replik slotlarını** görür ve seslendirir.
   - Örneğin 1. Oyuncu `Suspect` rolünü seçtiyse, yalnızca `Suspect` replikleri için mikrofona konuşur.
2. **Anlık Ses Dağıtımı (Take Broadcast):**
   - Bir oyuncu replik kaydını bitirdiği anda (`StopLiveRecording`), kaydedilen 16-bit PCM WAV sesi sıkıştırılarak arka planda host'a ve lobideki tüm oyunculara aktarılır (`Rpc(nameof(ReceiveAudioTake), slotId, peerId, audioBytes)`).
   - Diğer oyuncuların ekranında o replik için **"✅ Kaydedildi"** rozeti anında yeşile döner.
3. **Herkes Hazır Olduğunda Başlatma:**
   - Tüm oyuncular kendi karakterlerinin repliklerini tamamladığında, sistem otomatik olarak veya host'un onayıyla **İzleme Ekranına (`PlaybackScreen`)** geçer.

---

## 3. 🎬 Senkronize Ortak İzleme (Synchronized Playback)

1. **Tam Senkron Başlangıç:**
   - Host başlatma komutunu gönderdiğinde (`Rpc(nameof(StartSynchronizedPlayback), timestamp)`), tüm oyuncuların ekranındaki video oynatıcı ve ses mikseri aynı anda başlar.
2. **Kusursuz Ses Birleştirme:**
   - 1. Oyuncunun aldığı sesler + 2. Oyuncunun aldığı sesler + Orijinal sahnenin arka plan müziği (`background.wav`) birleşerek tam bir film gibi çalar.
   - Oylama yapılmaz; dublaj bitiminde "Tekrar İzle" veya "Yeni Sahne Seç" seçenekleri sunulur.

---

## 4. 🌐 Steamworks & Ağ Altyapısı (Multiplayer Architecture)

```text
┌────────────────────────────────────────────────────────┐
│                   DUAL-LAYER NETWORK                   │
├───────────────────────────┬────────────────────────────┤
│ 1. Steamworks P2P (Ana)   │ 2. ENet Local / IP (Yedek) │
├───────────────────────────┼────────────────────────────┤
│ • Steam Lobi Oluşturma    │ • Yerel Ağ / LAN Bağlantısı│
│ • Arkadaş Listesi & Davet │ • IP / Port ile Bağlantı   │
│ • Steam Relay (NAT Bypass)│ • Offline Geliştirme/Test  │
└───────────────────────────┴────────────────────────────┘
```

### Steam Entegrasyon Katmanı:
- **`SteamManager.cs`**: Steamworks API başlatma (`SteamAPI.Init()`), Steam ID ve profil adı alma.
- **`SteamLobbyManager.cs`**: Steam lobi davetleri (`SteamMatchmaking.CreateLobby`), lobiye katılma (`OnLobbyEntered`), arkadaş davet arayüzü (`SteamFriends.ActivateGameOverlayInviteDialog`).
- **`NetworkLobbyManager.cs`**: ENet ve Steam Socket üzerinden mesajları ileten ortak köprü.

---

## 5. 👥 Görev Dağılımı ve İş Paylaşımı

| Geliştirici | Sorumluluk Alanı | Yapılacak İşler |
| :--- | :--- | :--- |
| **Arkadaşın (Codex)** | • Steamworks SDK & Lobi Altyapısı<br>• Ağ Mesajlaşması & Paket Senkronizasyonu | 1. Steamworks C# wrapper (Facepunch veya Steamworks.NET) entegrasyonu.<br>2. Steam lobi davet & katılma mekanizması.<br>3. `NetworkLobbyManager` ses paketi ve lobi durumu RPC'leri.<br>4. Oylama ekranlarını pasife alıp doğrudan Playback akışına bağlama. |
| **Sen (Antigravity)** | • Karakter Seçim / Kilitleme Arayüzü<br>• Dağıtık Kayıt Stüdyosu & Ses Karışımı | 1. `LobbyScreen` ve `SetupScreen` üzerinde canlı Karakter Seçim & Kilitleme paneli.<br>2. Çoklu karakter seçebilme butonları.<br>3. `RecordingScreen` içinde sadece seçili karaktere ait replikleri filtreleme.<br>4. Gelen uzak sesleri (`RemoteTakes`) yerel `VoiceTakeStore`'a yazıp `PlaybackScreen`'de kusursuz çaldırma. |

---

## 📌 Geliştirme Sırası (Aşama Aşama):

1. **Aşama 1 (Karakter Seçimi & Kilitleme):** Sahnede karakterlerin listelenmesi, tıklayan oyuncuya kilitlenmesi ve çoklu seçime izin verilmesi.
2. **Aşama 2 (Filtreli Kayıt):** Oyuncunun kayıt ekranında sadece kendi seçtiği rolleri görmesi ve kaydetmesi.
3. **Aşama 3 (Seslerin Ağdan İletimi):** Kaydedilen repliklerin diğer oyuncuların bilgisayarına indirilmesi.
4. **Aşama 4 (Steamworks Entegrasyonu):** Steam arkadaş daveti ve lobi katılımı.
5. **Aşama 5 (Ortak İzleme):** Tüm oyuncuların seslerinin video eşliğinde birlikte canlı sinema gibi izlenmesi.
