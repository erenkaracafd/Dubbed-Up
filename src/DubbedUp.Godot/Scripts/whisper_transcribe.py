import sys
import os
import json

# Force UTF-8 on Windows console & pipes so Turkish letters (ç, ğ, ı, ö, ş, ü, vb.) are never corrupted
if sys.platform.startswith('win'):
    try:
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
        sys.stderr.reconfigure(encoding='utf-8', errors='replace')
    except Exception:
        pass

def load_model():
    import whisper
    # Keep the default lightweight, while allowing deployments to select a larger model.
    model_name = os.environ.get('DUBBEDUP_WHISPER_MODEL', 'tiny')
    return whisper.load_model(model_name)


def transcribe(model, audio_path):
    # Greedy decoding without temperature fallback or cross-segment conditioning:
    # faster on CPU and avoids repetition loops on short clips.
    result = model.transcribe(
        audio_path,
        fp16=False,
        temperature=0.0,
        condition_on_previous_text=False,
    )

    segments = []
    for s in result.get('segments', []):
        text = s.get('text', '').strip()
        if text and len(text) >= 2:
            start_ms = int(round(s.get('start', 0.0) * 1000.0))
            end_ms = int(round(s.get('end', 0.0) * 1000.0))
            if end_ms - start_ms < 600:
                end_ms = start_ms + 600

            segments.append({
                'startMs': start_ms,
                'endMs': end_ms,
                'text': text
            })

    print('---WHISPER_JSON_START---')
    print(json.dumps(segments, ensure_ascii=False))
    print('---WHISPER_JSON_END---', flush=True)


def serve():
    # Long-lived worker: importing torch/whisper takes ~5s, so the game starts this once,
    # then sends one audio path per line on stdin. Exits when stdin closes (game quit).
    model = load_model()
    print('---WHISPER_READY---', flush=True)
    for line in sys.stdin:
        audio_path = line.strip()
        if not audio_path:
            continue
        try:
            if not os.path.exists(audio_path):
                raise FileNotFoundError(audio_path)
            transcribe(model, audio_path)
        except Exception as e:
            print(f'---WHISPER_ERROR--- {e}', flush=True)


def main():
    if len(sys.argv) < 2:
        print('Usage: python whisper_transcribe.py <audio_path> | --serve', file=sys.stderr)
        sys.exit(1)

    if sys.argv[1] == '--serve':
        try:
            serve()
        except Exception as e:
            print(f'Whisper error: {e}', file=sys.stderr)
            sys.exit(2)
        return

    audio_path = sys.argv[1]
    if not os.path.exists(audio_path):
        print(f'Error: audio file {audio_path} not found', file=sys.stderr)
        sys.exit(1)

    try:
        transcribe(load_model(), audio_path)
    except Exception as e:
        print(f'Whisper error: {e}', file=sys.stderr)
        sys.exit(2)

if __name__ == '__main__':
    main()
