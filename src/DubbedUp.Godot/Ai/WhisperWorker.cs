using System.Collections.Concurrent;
using System.Diagnostics;
using DubbedUp.Godot.VideoPlayback;
using Godot;

namespace DubbedUp.Godot.Ai;

/// <summary>
/// Keeps one Whisper Python process alive (<c>whisper_transcribe.py --serve</c>) so the ~5s
/// torch/whisper start-up is paid once instead of on every auto-detect.
/// The worker exits on its own when its stdin closes, i.e. when the game quits.
/// </summary>
public static class WhisperWorker
{
    private const string ReadyMarker = "---WHISPER_READY---";
    private const string StartMarker = "---WHISPER_JSON_START---";
    private const string EndMarker = "---WHISPER_JSON_END---";
    private const string ErrorMarker = "---WHISPER_ERROR---";

    private static readonly object Gate = new();
    private static Process? _process;
    private static BlockingCollection<string>? _lines;
    private static bool _ready;

    /// <summary>Starts the worker in the background if it isn't running yet.</summary>
    public static void WarmUp(string scriptPath)
    {
        Task.Run(() =>
        {
            lock (Gate)
            {
                EnsureStarted(scriptPath, TimeSpan.FromSeconds(60));
            }
        });
    }

    /// <summary>
    /// Transcribes one file and returns the raw JSON segment array, or null if the worker
    /// is unavailable or fails (callers fall back to a one-shot process).
    /// </summary>
    public static string? Transcribe(string scriptPath, string wavFilePath, TimeSpan timeout)
    {
        lock (Gate)
        {
            if (!EnsureStarted(scriptPath, TimeSpan.FromSeconds(60)) || _process is null || _lines is null)
            {
                return null;
            }

            try
            {
                _process.StandardInput.WriteLine(wavFilePath);
                _process.StandardInput.Flush();

                var json = new System.Text.StringBuilder();
                var collecting = false;
                var deadline = DateTime.UtcNow + timeout;

                while (DateTime.UtcNow < deadline)
                {
                    if (!_lines.TryTake(out var line, TimeSpan.FromMilliseconds(250)))
                    {
                        if (_process.HasExited) break;
                        continue;
                    }

                    if (line.StartsWith(ErrorMarker, StringComparison.Ordinal))
                    {
                        GD.PrintErr($"[WhisperWorker] {line}");
                        return null;
                    }

                    if (line.StartsWith(StartMarker, StringComparison.Ordinal))
                    {
                        collecting = true;
                        json.Clear();
                        continue;
                    }

                    if (line.StartsWith(EndMarker, StringComparison.Ordinal))
                    {
                        return json.ToString().Trim();
                    }

                    if (collecting) json.AppendLine(line);
                }

                GD.PrintErr("[WhisperWorker] Transcription timed out; restarting worker next time.");
                Stop();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[WhisperWorker] Transcription failed: {ex.Message}");
                Stop();
            }

            return null;
        }
    }

    private static bool EnsureStarted(string scriptPath, TimeSpan readyTimeout)
    {
        if (_process is not null && !_process.HasExited && _ready) return true;
        Stop();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ExternalToolLocator.ResolveWhisperPython(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new System.Text.UTF8Encoding(false),
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["DUBBEDUP_WHISPER_MODEL"] = ExternalToolLocator.ResolveWhisperModel();
            ExternalToolLocator.AddFfmpegToPath(psi);
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("--serve");

            var process = Process.Start(psi);
            if (process is null) return false;

            var lines = new BlockingCollection<string>();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) lines.Add(e.Data);
            };
            process.ErrorDataReceived += (_, _) => { }; // drain stderr so the pipe never fills
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var deadline = DateTime.UtcNow + readyTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (lines.TryTake(out var line, TimeSpan.FromMilliseconds(250)))
                {
                    if (line.StartsWith(ReadyMarker, StringComparison.Ordinal))
                    {
                        _process = process;
                        _lines = lines;
                        _ready = true;
                        GD.Print("[WhisperWorker] Whisper worker ready.");
                        return true;
                    }
                }
                else if (process.HasExited)
                {
                    break;
                }
            }

            GD.PrintErr("[WhisperWorker] Whisper worker did not start; using one-shot mode.");
            try { process.Kill(entireProcessTree: true); } catch { }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[WhisperWorker] Could not start Whisper worker: {ex.Message}");
        }

        return false;
    }

    public static void Stop()
    {
        _ready = false;
        _lines = null;
        if (_process is null) return;
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already gone.
        }
        _process.Dispose();
        _process = null;
    }
}
