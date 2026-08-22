# Official content and licensing policy

Only media created for Dubbed Up or clearly licensed for commercial use may enter `Content/`. Movie, television, anime, streaming, YouTube, and other third-party clips are prohibited unless explicit commercial rights are documented.

Every scene directory must contain `provenance.json` with at least:

```json
{
  "schemaVersion": 1,
  "sceneId": "stable-scene-id",
  "title": "Scene title",
  "sourceMedia": ["video.ogv"],
  "creator": "Creator/legal entity",
  "licenseSpdx": "License identifier or LicenseRef-DubbedUp-Commission",
  "licenseEvidence": "Relative path or durable URL to the rights evidence",
  "commercialUseApproved": true,
  "approvedBy": "Human reviewer",
  "approvedOn": "YYYY-MM-DD"
}
```

An asset is not approved merely because it is easy to download, attributed, used temporarily, or described as royalty-free. Missing/unclear terms block inclusion. Preserve source files, metadata, dub projects, session state, and player recordings as separate data categories.

Dependency licensing follows the same evidence-first approach. MIT, BSD-2-Clause, BSD-3-Clause, Apache-2.0, and ISC are preferred. LGPL needs explicit review. GPL, AGPL, SSPL, non-commercial, restrictive source-available, and unclear licenses need explicit human approval and must not be introduced silently.

