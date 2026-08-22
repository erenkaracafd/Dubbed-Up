# Engine-independent project format

Schema version 1 uses UTF-8 JSON with camel-case property names. Unknown properties and unsupported schema versions are rejected so migrations are deliberate.

## Document boundaries

- An official scene document owns immutable source-media references, characters, voice slots, and their timeline placement.
- A dub project document identifies an official scene and maps voice slots to selected `takeId` values.
- Voice recording metadata/files are stored separately by the VoiceTakes subsystem. The project never embeds an audio path or audio bytes.
- Player/session/round state is not persisted in either document.

This boundary lets a project select a different take without copying media and lets future engines or tools read the same data without Godot.

## Official scene example

```json
{
  "schemaVersion": 1,
  "sceneId": "museum-mixup",
  "title": "Museum Mix-up",
  "durationMilliseconds": 12000,
  "sourceMedia": [
    {
      "mediaId": "scene-video",
      "role": "sceneVideo",
      "relativePath": "media/scene.ogv"
    }
  ],
  "characters": [
    { "characterId": "guard", "displayName": "Guard" }
  ],
  "voiceSlots": [
    {
      "voiceSlotId": "guard-line-1",
      "characterId": "guard",
      "prompt": "React to the suspicious statue."
    }
  ],
  "timeline": [
    {
      "timelineEntryId": "entry-1",
      "voiceSlotId": "guard-line-1",
      "startMilliseconds": 1500,
      "endMilliseconds": 4300
    }
  ]
}
```

## Dub project example

```json
{
  "schemaVersion": 1,
  "projectId": "friday-round-1",
  "sceneId": "museum-mixup",
  "selectedTakes": [
    { "voiceSlotId": "guard-line-1", "takeId": "take-guard-1" }
  ]
}
```

IDs use lowercase kebab case. Media paths use forward slashes, are relative to the scene package, and cannot contain traversal segments. Every voice slot has exactly one timeline entry in schema version 1; different characters may overlap in time.
