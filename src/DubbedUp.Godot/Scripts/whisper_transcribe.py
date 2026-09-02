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

def main():
    if len(sys.argv) < 2:
        print('Usage: python whisper_transcribe.py <audio_path>', file=sys.stderr)
        sys.exit(1)

    audio_path = sys.argv[1]
    if not os.path.exists(audio_path):
        print(f'Error: audio file {audio_path} not found', file=sys.stderr)
        sys.exit(1)

    try:
        import whisper
        # Keep the default lightweight, while allowing deployments to select a larger model.
        model_name = os.environ.get('DUBBEDUP_WHISPER_MODEL', 'tiny')
        model = whisper.load_model(model_name)
        result = model.transcribe(audio_path, fp16=False)

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
        print('---WHISPER_JSON_END---')
    except Exception as e:
        print(f'Whisper error: {e}', file=sys.stderr)
        sys.exit(2)

if __name__ == '__main__':
    main()
