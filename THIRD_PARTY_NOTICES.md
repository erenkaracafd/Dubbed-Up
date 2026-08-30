# Third-party notices

The following development/runtime dependencies are referenced by project files. Restore their packages only from their official distribution channels.

| Component | Purpose | License |
|---|---|---|
| Godot Engine / Godot.NET.Sdk 4.7.x | Game engine and C# SDK | MIT |
| Steamworks.NET 2024.8.0 | Managed C# wrapper and native runtime bindings for the Valve Steamworks SDK | MIT |
| Microsoft.NET.Test.Sdk | .NET test host | MIT |
| xUnit.net | Unit testing | Apache-2.0 |
| xunit.runner.visualstudio | Visual Studio/.NET test adapter | Apache-2.0 |
| coverlet.collector | Test coverage collection | MIT |

No third-party media is included. Official scene media requires separate provenance and commercial-rights approval under `docs/CONTENT_POLICY.md`.

Steamworks.NET requires the Steam client and Valve's platform-specific `steam_api` native runtime for Steam-enabled builds. The game must continue to start and retain its ENet local/IP fallback when Steam is unavailable.

