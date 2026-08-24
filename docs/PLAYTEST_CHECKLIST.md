# Dubbed-Up Playtest Checklist & Feedback Guide

This checklist guides developers and QA testers during local and online playtests of Dubbed-Up.

---

## 1. Pre-Session Setup

- [ ] **Microphone Check:**
  - Verify default input device in OS settings.
  - Test input volume level; ensure audio is clear and not clipping.
- [ ] **Scenes & Media:**
  - Ensure sample scenes exist in `scenes/` (or click "📁 Open Custom Scenes Folder" in Scene Picker to drop in MP4 clips).
  - Verify video playback starts smoothly without stutter.
- [ ] **Player Roster:**
  - 2 to 8 players ready for local or multiplayer game.

---

## 2. Playtest Scenarios

### Scenario A: Local Co-op Dubbing (Primary Mode)
1. Launch game -> **Play Local Game**.
2. Pick a scene (e.g. *Museum Mix-up*).
3. Select **Co-op Dubbing** mode.
4. Record all voice slots with friends.
5. Watch the synchronized playback.
6. Verify:
   - [ ] Prompts are readable and easy to understand.
   - [ ] Audio takes start and stop on time with video action.
   - [ ] Zero audio drift over playback duration.
   - [ ] Results screen displays celebration and allows instant replay or new scene selection.

### Scenario B: Competitive Voting (Party Scoring Mode)
1. Pick a scene -> Select **Competitive Voting** mode.
2. Voice all character slots.
3. Watch the dub -> Transition to Voting screen.
4. Verify:
   - [ ] Each player casts a vote (self-voting is prevented).
   - [ ] Tallies, winner announcement, and scoreboard standings update correctly.
   - [ ] "Play Next Round" advances round index and keeps scores.

### Scenario C: Online Multiplayer Lobby (ENet / Steam P2P)
1. Host launches **Online Multiplayer Lobby** -> Clicks "Host Lobby".
2. Guest enters Host IP (e.g. `127.0.0.1` or LAN IP) and joins.
3. Verify:
   - [ ] Player list updates dynamically when players join/leave.
   - [ ] Ready state toggles work.
   - [ ] Host starts match -> all connected players transition into Recording.
   - [ ] Audio recorded by client A is broadcast to client B via `BroadcastAudioTake`.
   - [ ] Master playback is synchronized across all clients.

### Scenario D: Custom & Workshop Scene Packs
1. In Scene Picker, click "📁 Open Custom Scenes Folder".
2. Drop a custom folder containing `scene.json` and a video file (`.mp4` / `.ogv` / `.webm`).
3. Click "🔄 Refresh List".
4. Verify:
   - [ ] New scene appears in the list with duration and character count.
   - [ ] Scene can be loaded and dubbed without errors.

---

## 3. Fun & Clarity Feedback Questions

1. **Pacing:** Did the recording phase feel fast and engaging, or did players wait too long?
2. **Prompts:** Were the prompt texts funny and helpful for improvisation?
3. **Synchronization:** Did the voices line up with character mouth movements and actions?
4. **Replayability:** Did players want to immediately replay the scene or try a new one?
