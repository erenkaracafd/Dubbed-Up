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
| FFmpeg 9.0.1 (Gyan full build; external workstation tool, not redistributed) | Media probing, transcoding, extraction, mixing, and thumbnail generation | GPL-3.0-or-later build; FFmpeg components have their respective upstream licenses |
| OpenAI Whisper 20250625 and tiktoken 0.14.0 | Local speech-to-text and tokenizer | MIT |
| PyTorch 2.14.0 | Whisper tensor runtime | Apache-2.0 plus bundled BSD/MIT/BSL/LLVM-Exception components |
| NumPy 2.5.2 | Whisper numerical runtime | BSD-3-Clause plus bundled 0BSD/MIT/Zlib/CC0 components |
| Numba 0.67.0 | Whisper JIT acceleration | BSD-2-Clause |
| llvmlite 0.49.0 | Numba LLVM bindings | BSD-2-Clause and Apache-2.0 WITH LLVM-exception |
| requests 2.34.2 | Whisper model download HTTP client | Apache-2.0 |
| tqdm 4.70.0 | Whisper progress reporting | MPL-2.0 and MIT |
| certifi 2026.7.22 | TLS certificate bundle | MPL-2.0 |
| regex 2026.9.3 | Whisper text processing | Apache-2.0 and CNRI-Python |
| typing_extensions 4.16.0 | Python compatibility types | PSF-2.0 |
| charset-normalizer 3.5.1, more-itertools 11.1.0, urllib3 2.7.0, filelock 3.32.5, setuptools 84.0.0 | Whisper/PyTorch support libraries | MIT |
| idna 3.19, SymPy 1.14.0, NetworkX 3.6.1, Jinja2 3.1.6, MarkupSafe 3.0.3, fsspec 2026.7.0, mpmath 1.3.0, colorama 0.4.6 | Whisper/PyTorch support libraries | BSD-family licenses |
| Microsoft Visual C++ 2015-2022 Redistributable (x64) | Native runtime required by PyTorch on Windows | Microsoft Software License Terms |

No third-party media is included. Official scene media requires separate provenance and commercial-rights approval under `docs/CONTENT_POLICY.md`.

Steamworks.NET requires the Steam client and Valve's platform-specific `steam_api` native runtime for Steam-enabled builds. The game must continue to start and retain its ENet local/IP fallback when Steam is unavailable.

FFmpeg, Python packages, model weights, and the Visual C++ runtime are installed
locally by `scripts/setup-media-tools.ps1`; they are not stored in this repository
or bundled in official scene media. Redistribution of a release package containing
these components requires a separate license and notices review.

