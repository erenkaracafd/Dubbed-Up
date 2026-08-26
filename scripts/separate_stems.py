import os
import sys
import subprocess
import shutil
from pathlib import Path

def separate_media_stems(input_file: str, output_dir: str):
    """
    Separates input audio/video file into vocals.wav and background.wav using Demucs.
    """
    input_path = Path(input_file).resolve()
    out_dir = Path(output_dir).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    if not input_path.exists():
        print(f"Error: Input file does not exist: {input_path}", file=sys.stderr)
        sys.exit(1)

    print(f"[StemSeparation] Processing input: {input_path}")
    print(f"[StemSeparation] Output directory: {out_dir}")

    # Run Demucs 2-stem separation (vocals vs no_vocals)
    cmd = [
        sys.executable, "-m", "demucs",
        "--two-stems=vocals",
        "-n", "htdemucs",
        "--out", str(out_dir / "_demucs_temp"),
        str(input_path)
    ]

    print(f"[StemSeparation] Running: {' '.join(cmd)}")
    result = subprocess.run(cmd, capture_output=True, text=True)

    if result.returncode != 0:
        print(f"[StemSeparation] Demucs output/error:\n{result.stderr}\n{result.stdout}", file=sys.stderr)

    # Locate generated files inside _demucs_temp/htdemucs/<filename>/
    track_name = input_path.stem
    temp_dir = out_dir / "_demucs_temp" / "htdemucs" / track_name

    vocals_src = temp_dir / "vocals.wav"
    no_vocals_src = temp_dir / "no_vocals.wav"

    final_vocals = out_dir / "vocals.wav"
    final_bg = out_dir / "background.wav"

    if vocals_src.exists():
        shutil.copy2(vocals_src, final_vocals)
        print(f"[StemSeparation] Created: {final_vocals}")
    
    if no_vocals_src.exists():
        shutil.copy2(no_vocals_src, final_bg)
        print(f"[StemSeparation] Created: {final_bg}")

    # Clean up temp folder
    try:
        shutil.rmtree(out_dir / "_demucs_temp", ignore_errors=True)
    except Exception:
        pass

    if final_vocals.exists():
        print("[StemSeparation] Success! Stems separated.")
    else:
        print("[StemSeparation] Warning: Could not locate separated stems.", file=sys.stderr)

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python separate_stems.py <input_file> <output_dir>")
        sys.exit(1)

    separate_media_stems(sys.argv[1], sys.argv[2])
