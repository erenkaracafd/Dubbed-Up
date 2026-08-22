# MVP scope

## Product question

Is dubbing a short scene with friends, watching the result, and voting actually fun?

## Included loop

1. Open the main menu and create a local session.
2. Add local players and select an official licensed scene.
3. Assign its characters/voice slots.
4. Record microphone takes for the required timeline slots.
5. Play the official scene with synchronized player recordings.
6. Collect votes, show scores/results, and start the next round or replay.

The MVP succeeds when this loop is reliable enough for an in-person playtest and produces useful fun/clarity feedback. Visual polish is secondary.

## Explicitly excluded

User video import, Workshop, AI, public matchmaking, dedicated backend, cloud storage, UGC hosting/moderation, runtime FFmpeg, rendered video export, complex content editing, and mobile/web support.

## Acceptance boundary

- Local-only play is sufficient.
- Official scene formats must be supported directly by Godot 4.7.x.
- The result is composed during playback; it does not create a new video file.
- One small commercially usable official test scene is sufficient for MVP validation.

