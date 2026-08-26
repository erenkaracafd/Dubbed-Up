import os
import sys
import subprocess
import shutil
from pathlib import Path

def convert_to_ogv(input_video: Path, output_ogv: Path):
    """
    Converts any video format (mp4, webm, mov, mkv, etc.) to Godot 4-compatible Ogg Theora (.ogv).
    """
    cmd = [
        "ffmpeg", "-y",
        "-i", str(input_video),
        "-c:v", "libtheora",
        "-q:v", "7",
        "-c:a", "libvorbis",
        "-q:a", "5",
        "-pix_fmt", "yuv420p",
        str(output_ogv)
    ]
    print(f"[MediaProcessor] Converting video to OGV: {' '.join(cmd)}")
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode == 0 and output_ogv.exists():
        print(f"[MediaProcessor] OGV Video created successfully: {output_ogv}")
        return True
    else:
        print(f"[MediaProcessor] OGV conversion failed:\n{res.stderr}", file=sys.stderr)
        return False

def extract_wav(input_media: Path, output_wav: Path):
    """
    Extracts 16-bit 44.1kHz stereo PCM WAV from video/audio using ffmpeg.
    """
    cmd = [
        "ffmpeg", "-y",
        "-i", str(input_media),
        "-vn",
        "-acodec", "pcm_s16le",
        "-ar", "44100",
        "-ac", "2",
        str(output_wav)
    ]
    print(f"[MediaProcessor] Extracting WAV: {' '.join(cmd)}")
    res = subprocess.run(cmd, capture_output=True, text=True)
    return res.returncode == 0 and output_wav.exists()

def separate_media_stems(input_file: str, output_dir: str):
    """
    Processes media file into:
    1. video.ogv (if input is video)
    2. audio.wav (full mix PCM)
    3. vocals.wav (isolated dialogue)
    4. background.wav (isolated ambient / music)
    """
    input_path = Path(input_file).resolve()
    out_dir = Path(output_dir).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    if not input_path.exists():
        print(f"Error: Input file does not exist: {input_path}", file=sys.stderr)
        sys.exit(1)

    print(f"[MediaProcessor] Processing: {input_path} -> {out_dir}")

    is_video = input_path.suffix.lower() in [".mp4", ".webm", ".mov", ".mkv", ".avi", ".ogv", ".flv"]
    
    # 1. Generate Godot-compatible video.ogv
    ogv_target = out_dir / "video.ogv"
    if is_video:
        if input_path.suffix.lower() == ".ogv" and input_path != ogv_target:
            shutil.copy2(input_path, ogv_target)
        elif not ogv_target.exists():
            convert_to_ogv(input_path, ogv_target)

    # 2. Extract full mix audio.wav
    audio_wav = out_dir / "audio.wav"
    if not audio_wav.exists():
        extract_wav(input_path, audio_wav)

    # 3. AI Stem Separation with Demucs
    demucs_input = audio_wav if audio_wav.exists() else input_path
    temp_dir = out_dir / "_demucs_temp"

    cmd = [
        sys.executable, "-m", "demucs",
        "--two-stems=vocals",
        "-n", "htdemucs",
        "--out", str(temp_dir),
        str(demucs_input)
    ]

    print(f"[MediaProcessor] Running Demucs: {' '.join(cmd)}")
    res = subprocess.run(cmd, capture_output=True, text=True)

    track_name = demucs_input.stem
    htdemucs_dir = temp_dir / "htdemucs" / track_name

    vocals_src = htdemucs_dir / "vocals.wav"
    no_vocals_src = htdemucs_dir / "no_vocals.wav"

    final_vocals = out_dir / "vocals.wav"
    final_bg = out_dir / "background.wav"

    if vocals_src.exists():
        shutil.copy2(vocals_src, final_vocals)
        print(f"[MediaProcessor] Generated vocals.wav: {final_vocals}")
    
    if no_vocals_src.exists():
        shutil.copy2(no_vocals_src, final_bg)
        print(f"[MediaProcessor] Generated background.wav: {final_bg}")

    # Fallback if Demucs fails or is unavailable
    if not final_vocals.exists() and audio_wav.exists():
        print("[MediaProcessor] Demucs vocals fallback to audio.wav")
        shutil.copy2(audio_wav, final_vocals)

    if not final_bg.exists() and audio_wav.exists():
        print("[MediaProcessor] Demucs background fallback to audio.wav")
        shutil.copy2(audio_wav, final_bg)

    # Clean up temp
    try:
        shutil.rmtree(temp_dir, ignore_errors=True)
    except Exception:
        pass

    print("[MediaProcessor] Processing complete!")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python separate_stems.py <input_file> <output_dir>")
        sys.exit(1)

    separate_media_stems(sys.argv[1], sys.argv[2])
