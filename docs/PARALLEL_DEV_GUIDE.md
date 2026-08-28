# Parallel Development & Team Workstream Guide

Bu doküman, projede **2 kişi (Codex & Antigravity)** paralel olarak çalışırken kodların, ekranların ve mantıksal yapıların çakışmaması (merge conflict yaşamaması) ve iş bittiğinde yerel stüdyo düzenlemelerinin ana MVP'ye pürüzsüzce entegre edilmesi için hazırlanmıştır.

---

## 👥 Görev ve Sorumluluk Dağılımı

| Geliştirici / Araç | Sorumluluk Alanı | Çalışacağı Dizin ve Dosyalar |
| :--- | :--- | :--- |
| **Arkadaşın (Codex)**<br>*(MVP & Core Lead)* | • `DubbedUp.Core` oyun kuralları, domain modelleri<br>• Çok oyunculu (Multiplayer) & Lobi altyapısı<br>• Steamworks SDK & Steam P2P bağlantısı<br>• Resmi telifsiz sahne paketleri & CI/CD<br>• PR #19 ve `main` branch birleştirme | • `src/DubbedUp.Core/` (tamamı)<br>• `src/DubbedUp.Godot/Networking/`<br>• `src/DubbedUp.Godot/UI/Screens/VotingScreen.*`<br>• `src/DubbedUp.Godot/UI/Screens/ResultsScreen.*`<br>• `src/DubbedUp.Godot/Content/OfficialScenes/`<br>• `tests/DubbedUp.Core.Tests/` |
| **Sen & Antigravity**<br>*(Local Studio & Media Lead)* | • Sahne Editörü & Dalga Formu Zaman Çizgisi<br>• Seslendirme & Canlı Mikrofon Kayıt Stüdyosu<br>• Senkronize Önizleme & Oynatma Motoru<br>• AI/FFmpeg Ses Ayrıştırma (Vokal/Müzik)<br>• Dinamik Aspect Ratio & Kesintisiz Video Oynatma | • `src/DubbedUp.Godot/UI/Screens/SceneEditorScreen.*`<br>• `src/DubbedUp.Godot/UI/Screens/SceneCreatorScreen.*`<br>• `src/DubbedUp.Godot/UI/Screens/RecordingScreen.*`<br>• `src/DubbedUp.Godot/UI/Screens/PlaybackScreen.*`<br>• `src/DubbedUp.Godot/UI/Controls/`<br>• `src/DubbedUp.Godot/VideoPlayback/`<br>• `src/DubbedUp.Godot/AudioPlayback/`<br>• `src/DubbedUp.Godot/Microphone/` |

---

## 🔒 Dokunulmazlık ve Çakışmama Kuralları (Boundary Invariants)

1. **Paylaşılan Ortak Sözleşmeler (Shared Contracts) Sabittir:**
   - `DubbedUp.Core` içindeki `OfficialSceneDocument`, `DubProjectDocument`, `VoiceTakeStore`, `TimelineEntry` ve `VoiceSlotDefinition` veri modelleri sabittir.
   - Bu modeller değiştirilmeden önce her iki tarafın onayı gerekir.
2. **Kritik Dosyalara (Hotspots) Dikkat:**
   - `DubbedUp.sln`, `src/DubbedUp.Godot/DubbedUp.Godot.csproj` ve `project.godot` dosyalarında gereksiz paket veya ayar değişikliği yapılmamalıdır.
3. **Sahne ve Medya Ayrımı:**
   - Sen sadece kullanıcı sahneleri (`user://workshop_scenes`) ve stüdyo araçlarıyla test yaparsın.
   - Arkadaşın resmi sahneleri (`Content/OfficialScenes`) ve oyun turlarını entegre eder.

---

## 🌿 Git ve Branch Stratejisi

```text
       [origin/main] (Kararlı ve Korumalı Dal)
             │
             ├───> [Arkadaşın Dalı: origin/issue-8-local-round-integration veya feature/mvp-core]
             │          (Core, Oylama, Lobi, Testler)
             │
             └───> [Senin Dalın: origin/issue-8-local-round-integration veya feature/local-studio-pipeline]
                        (Editör, Kayıt Stüdyosu, Vokal Ayrıştırma, Preview Oynatıcı)
```

### Sen Çalışırken:
1. Kendi feature branch'inde çalış (`issue-8-local-round-integration` veya `feature/local-studio-pipeline`).
2. Küçük ve açıklamalı commit'ler at.
3. Yerel testlerini çalıştır:
   ```powershell
   dotnet test tests/DubbedUp.Core.Tests/DubbedUp.Core.Tests.csproj
   ```
4. İşlerini düzenli aralıklarla GitHub'a pushla (`git push origin <dal-adı>`).

---

## 🚀 Final Entegrasyon Protokolü (İşler Bittiğinde)

Arkadaşın MVP geliştirmesini tamamladığında, senin geliştirdiğin stüdyo motorunu (Edit, Record, Playback) çekip projeyi birleştirmek için şu adımları uygulayacaktır:

### Adım 1: Senin Dalını Fetch Etme
```powershell
git fetch origin
```

### Adım 2: Senin Dalını Kendi Dalına Birleştirme (Merge)
```powershell
# Arkadaşın kendi dalındayken:
git merge origin/issue-8-local-round-integration --no-ff -m "merge: Integrate local studio, recording, and media pipeline"
```

### Adım 3: Dosya Sahipliği Kontrolü
Yukarıdaki tabloya göre dizinler ve ekranlar tamamen ayrıldığı için **%99 hiçbir çakışma (conflict) çıkmayacaktır**.
Eğer `DubbedUp.sln` veya `ScenePickerScreen` gibi ortak bir yerde minör çakışma çıkarsa:
- Edit, Record, Playback, MediaTranscoder ve AudioPlayback kısımlarında **senin kodların** kabul edilir (`Accept Incoming / Ours`).
- Core modellerinde ve Oylama/Lobi kısımlarında **arkadaşının kodları** kabul edilir.

### Adım 4: Derleme ve Test Doğrulaması
```powershell
dotnet build DubbedUp.sln --configuration Debug
dotnet test tests/DubbedUp.Core.Tests/DubbedUp.Core.Tests.csproj
.\run-game.ps1
```

