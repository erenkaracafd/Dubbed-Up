import os
import sys
import subprocess
import shutil
from pathlib import Path

PYTHON_EXE = r"C:\Users\SÜLEYMAN\AppData\Local\Programs\Python\Python312\python.exe"

SCENES = [
    "speed_mama_homeless",
    "museum_mixup",
    "space_drift",
    "cooking_disaster"
]

REPO_ROOT = Path(__file__).resolve().parent.parent

def separate_scene(scene_id: str):
    print(f"\n=======================================================")
    print(f"[*] Processing Scene: {scene_id}")
    print(f"=======================================================")
    
    scene_dir = REPO_ROOT / "scenes" / scene_id
    godot_content_dir = REPO_ROOT / "src" / "DubbedUp.Godot" / "Content" / "OfficialScenes" / scene_id
    
    media_dir = scene_dir / "media"
    godot_media_dir = godot_content_dir / "media"
    
    media_dir.mkdir(parents=True, exist_ok=True)
    godot_media_dir.mkdir(parents=True, exist_ok=True)
    
    # Check for source audio or video
    source_audio = media_dir / "audio.wav"
    if not source_audio.exists():
        # Extract audio.wav from video file
        for ext in [".ogv", ".mp4", ".webm", ".mkv"]:
            for v_name in [f"{scene_id}{ext}", f"speed_homeless{ext}", f"video{ext}", f"scene{ext}"]:
                v_path = media_dir / v_name
                if v_path.exists():
                    print(f"[*] Extracting audio from video: {v_path}")
                    subprocess.run(["ffmpeg", "-y", "-i", str(v_path), "-vn", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2", str(source_audio)], check=True)
                    break
            if source_audio.exists():
                break

    if not source_audio.exists():
        print(f"[!] No source audio found for {scene_id}, skipping.")
        return

    print(f"[*] Running Demucs AI Vocal/Background Stem Separation on: {source_audio}")
    out_temp = REPO_ROOT / "temp_demucs_out"
    out_temp.mkdir(parents=True, exist_ok=True)
    
    cmd = [
        PYTHON_EXE, "-m", "demucs.separate",
        "-n", "htdemucs",
        "--two-stems", "vocals",
        "-o", str(out_temp),
        str(source_audio)
    ]
    subprocess.run(cmd, check=True)
    
    # Demucs puts results in out_temp / htdemucs / <track_name> / vocals.wav and no_vocals.wav
    track_name = source_audio.stem
    demucs_res = out_temp / "htdemucs" / track_name
    
    vocals_src = demucs_res / "vocals.wav"
    bg_src = demucs_res / "no_vocals.wav"
    
    vocals_dst = media_dir / "vocals.wav"
    bg_dst = media_dir / "background.wav"
    
    if vocals_src.exists():
        # Convert to standard 16-bit PCM WAV 44100Hz
        subprocess.run(["ffmpeg", "-y", "-i", str(vocals_src), "-acodec", "pcm_s16le", "-ar", "44100", str(vocals_dst)], check=True)
        shutil.copy2(vocals_dst, godot_media_dir / "vocals.wav")
        print(f"[+] Saved vocals stem: {vocals_dst}")
        
    if bg_src.exists():
        subprocess.run(["ffmpeg", "-y", "-i", str(bg_src), "-acodec", "pcm_s16le", "-ar", "44100", str(bg_dst)], check=True)
        shutil.copy2(bg_dst, godot_media_dir / "background.wav")
        print(f"[+] Saved background stem: {bg_dst}")

    # Also sync source audio.wav to godot_media_dir
    if source_audio.exists():
        shutil.copy2(source_audio, godot_media_dir / "audio.wav")

if __name__ == "__main__":
    for s in SCENES:
        separate_scene(s)
    print("\n[✔] All AI stem separations complete!")
