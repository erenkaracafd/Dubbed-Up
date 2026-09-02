# Dubbed Up

Dubbed Up is a desktop comedy party game where friends record dialogue for characters in a short official scene, watch the synchronized dub, vote, and play another round.

The MVP has one purpose: determine whether that loop is fun. Features such as public matchmaking, user video imports, AI, Workshop support, export, and a backend are intentionally out of scope.

## Technology

- Godot 4.7.x .NET (use the latest 4.7 maintenance release)
- C# and .NET 8
- Desktop-first modular monolith
- Engine-independent domain logic in `DubbedUp.Core`

## Repository map

```text
src/DubbedUp.Core/        Plain C# domain and gameplay logic
src/DubbedUp.Godot/       Godot UI and platform adapters
tests/DubbedUp.Core.Tests Core unit and architecture tests
Content/                  Licensed official scene packages and metadata
docs/                     Product, architecture, workflow, and status docs
```

Start with [AGENTS.md](AGENTS.md) and [docs/DEVELOPMENT_WORKFLOW.md](docs/DEVELOPMENT_WORKFLOW.md). GitHub Issues are authoritative for task ownership.

## Local prerequisites

Install a current .NET 8 SDK and the latest Godot 4.7.x .NET editor. On Windows,
install the local Whisper/FFmpeg media toolchain once (the Visual C++ installer may
request administrator approval):

```powershell
.\scripts\setup-media-tools.ps1
```

This installs FFmpeg through WinGet, creates an ignored `.tools/whisper` Python
environment, installs the locked Whisper runtime, downloads the `tiny` model, and
writes the ignored `.tools/media-tools.json` discovery file. Tool binaries and AI
models are never committed. Advanced setups can override discovery with
`DUBBEDUP_FFMPEG_PATH`, `DUBBEDUP_WHISPER_PYTHON`, and
`DUBBEDUP_WHISPER_MODEL`.

Then run:

```powershell
dotnet restore DubbedUp.sln
dotnet build src/DubbedUp.Core/DubbedUp.Core.csproj --configuration Release --no-restore
dotnet test tests/DubbedUp.Core.Tests/DubbedUp.Core.Tests.csproj --configuration Release --no-restore
pwsh scripts/verify-repository.ps1
```

Open `src/DubbedUp.Godot/project.godot` with the .NET-enabled Godot editor.

