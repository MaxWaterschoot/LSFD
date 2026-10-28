"""Generates LSFD's sound effects with the ElevenLabs Sound Effects API.

Usage (API key needed, no pip packages; ffmpeg on your PATH):
    set ELEVENLABS_API_KEY=...   (macOS/Linux: export ELEVENLABS_API_KEY=...)
    python generate_sfx.py --csv sfx_prompts.csv --out ../../Plugins/LSFD/Audio

Every row of sfx_prompts.csv becomes <out>/<file> as a 44.1 kHz mono 16-bit WAV, the format LSFD plays.
  file      where it goes, e.g. Alarms/LOW_AIR.wav
  duration  seconds (0.5 to 30), empty lets ElevenLabs choose
  loop      1 for sounds LSFD plays looped: they are generated to loop seamlessly and not trimmed
  prompt    what to generate, in English
Only missing files are generated, unless --force is given. --only file1 file2 picks rows.
--takes 3 keeps three versions in raw/SFX/ (…_1.mp3, …_2.mp3) to pick the best; the first one goes to <out>.
"""
import argparse, csv, json, os, subprocess, sys, urllib.error, urllib.request

URL = "https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128"
LEVEL = "loudnorm=I=-16:TP=-1.5:LRA=11"


def generate(key, row, influence, raw_file):
    body = {"text": row["prompt"], "prompt_influence": influence, "model_id": "eleven_text_to_sound_v2",
            "loop": row.get("loop", "").strip() == "1"}
    if row.get("duration", "").strip():
        body["duration_seconds"] = float(row["duration"])
    req = urllib.request.Request(URL, data=json.dumps(body).encode(),
                                 headers={"xi-api-key": key, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=120) as r, open(raw_file, "wb") as f:
            f.write(r.read())
    except urllib.error.HTTPError as e:
        sys.exit(f"ElevenLabs refused {row['file']}: {e.code} {e.read().decode(errors='replace')}")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--csv", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--raw", default="raw/SFX")
    p.add_argument("--influence", type=float, default=0.5, help="0 to 1: how literally the prompt is followed")
    p.add_argument("--takes", type=int, default=1)
    p.add_argument("--only", nargs="*")
    p.add_argument("--force", action="store_true")
    a = p.parse_args()

    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        sys.exit("Set the ELEVENLABS_API_KEY environment variable first.")

    with open(a.csv, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    if a.only:
        rows = [r for r in rows if r["file"] in a.only]

    for row in rows:
        out_file = os.path.join(a.out, row["file"])
        if os.path.exists(out_file) and not a.force:
            continue
        os.makedirs(os.path.dirname(out_file), exist_ok=True)
        base = os.path.splitext(os.path.join(a.raw, row["file"]))[0]
        os.makedirs(os.path.dirname(base), exist_ok=True)

        takes = [f"{base}_{i}.mp3" for i in range(1, a.takes + 1)] if a.takes > 1 else [base + ".mp3"]
        for raw_file in takes:
            generate(key, row, a.influence, raw_file)

        looped = row.get("loop", "").strip() == "1"
        audio_filter = LEVEL if looped else "silenceremove=start_periods=1:start_threshold=-50dB," + LEVEL
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", takes[0], "-af", audio_filter,
                        "-ar", "44100", "-ac", "1", "-sample_fmt", "s16", out_file], check=True)
        print(f"{row['file']:40} {row['prompt'][:70]}")

if __name__ == "__main__":
    main()
