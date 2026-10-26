"""Generates the LSFD dispatch voice fragments with Piper TTS and gives them a radio effect.

Usage:
    pip install piper-tts        (and ffmpeg on your PATH)
    python generate_voices.py --voice path/to/en-us-ryan-high.onnx --csv dispatch_fragments.csv \
        --out ../../Plugins/LSFD/Audio/Dispatch --raw raw/Dispatch

Writes every row of the CSV to <out>/<file> with the radio effect, and the clean TTS output to <raw>/<file>
(keep those out of Plugins/, they are only there to re-run the filter without generating again).
Only rows whose output file doesn't exist yet are generated, unless --force is given.
"""
import argparse, csv, os, subprocess, wave
from piper import PiperVoice, SynthesisConfig

# Portable radio: telephone band, a bit of compression and grit, then normalized.
TRIM = ("silenceremove=start_periods=1:start_threshold=-45dB,areverse,"
        "silenceremove=start_periods=1:start_threshold=-45dB,areverse,apad=pad_dur=0.04,")
RADIO_FILTER = TRIM + ("highpass=f=300,lowpass=f=3400,acompressor=threshold=0.1:ratio=6:attack=5:release=50,"
                "acrusher=bits=10:mix=0.25,volume=1.6,alimiter=limit=0.95")

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--voice", required=True)
    p.add_argument("--csv", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--raw", default="raw/Dispatch", help="where the clean, unfiltered TTS output goes")
    p.add_argument("--speed", type=float, default=0.92, help="< 1 speaks faster, like a dispatcher")
    p.add_argument("--force", action="store_true")
    a = p.parse_args()

    voice = PiperVoice.load(a.voice)
    config = SynthesisConfig(length_scale=a.speed)
    raw_root = a.raw

    with open(a.csv, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    for row in rows:
        out_file = os.path.join(a.out, row["file"])
        raw_file = os.path.join(raw_root, row["file"])
        if os.path.exists(out_file) and not a.force:
            continue
        os.makedirs(os.path.dirname(out_file), exist_ok=True)
        os.makedirs(os.path.dirname(raw_file), exist_ok=True)

        with wave.open(raw_file, "wb") as wf:
            voice.synthesize_wav(row["text"], wf, syn_config=config)

        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", raw_file, "-af", RADIO_FILTER,
                        "-ar", "44100", "-ac", "1", "-sample_fmt", "s16", out_file], check=True)
        print(f"{row['file']:45} {row['text']}")

if __name__ == "__main__":
    main()
