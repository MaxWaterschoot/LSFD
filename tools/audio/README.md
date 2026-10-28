# Audio tools

These scripts made the sounds in `Plugins/LSFD/Audio/`. They aren't needed to play or build LSFD, and they aren't part
of the download. Keep them to add new voice fragments or to redo the sounds.

**File names:** in capitals, e.g. `CO_ALARM.wav`. LSFD finds the dispatch fragments by name:

| What | Where it is looked for |
| --- | --- |
| Call titles | `Callouts/` + the title in capitals, with `_` for spaces and dashes. "Head-on Collision" is `HEAD_ON_COLLISION.wav`. |
| Units | `Units/` + the apparatus type, e.g. `ENGINE.wav` |
| Numbers | `Numbers/01.wav` to `10.wav` |
| Everything else | The paths in `dispatch_fragments.csv` |

A call gets its own announcement when you add a row for it to the CSV and generate the file.

| File | |
| --- | --- |
| `dispatch_fragments.csv` | Every dispatch voice fragment: `category`, `key` (the value in code), `file` (under `Plugins/LSFD/Audio/Dispatch/`) and the `text` that is spoken |
| `generate_voices.py` | Generates the voice fragments with [Piper TTS](https://github.com/rhasspy/piper) and adds the radio effect |
| `sfx_prompts.csv` | Every sound effect: `file` (under `Plugins/LSFD/Audio/`), `duration`, `loop` and the English `prompt` |
| `generate_sfx.py` | Generates the sound effects with the ElevenLabs Sound Effects API |
| `generate_tones.py` | Generates the synthetic sounds: dispatch tones, radio clicks/static, alarms and the CPR metronome |

## Voices

The current fragments use the Piper voice **Ryan (high)** (`en-us-ryan-high`, from the `v0.0.2` release of
rhasspy/piper on GitHub). Its dataset license is **CC BY-NC-SA 4.0**: fine for a free mod, but it has to stay
non-commercial, be credited, and the audio shared under the same license.

```
pip install piper-tts numpy     # ffmpeg has to be on your PATH as well
cd tools/audio
python generate_voices.py --voice path/to/en-us-ryan-high.onnx --csv dispatch_fragments.csv --out ../../Plugins/LSFD/Audio/Dispatch
```

Only missing files are generated: add rows to the CSV and run it again, or use `--force` to redo everything
(e.g. with another voice or `--speed`). The clean TTS output goes to `raw/` (not in Git).

### With ElevenLabs

Needs an API key (elevenlabs.io → Developers → API keys) and the voice ID of the voice you pick. No pip packages.

```
export ELEVENLABS_API_KEY=...          # Windows: set ELEVENLABS_API_KEY=...
python generate_voices.py --engine elevenlabs --voice <voice id> --csv dispatch_fragments.csv --out ../../Plugins/LSFD/Audio/Dispatch --force
```

Every fragment gets the same voice settings and seed, so they sound like one dispatcher. Generate them clean (no
effects in ElevenLabs): the script adds the same radio filter as for Piper. `--only Units/ENGINE.wav` redoes one file.
Try a few first with `--only` before spending credits on all of them.

## Sound effects

```
python generate_sfx.py --csv sfx_prompts.csv --out ../../Plugins/LSFD/Audio
```

Rows with `loop` 1 are generated to loop seamlessly. Files that already exist (like the synthetic alarms) are skipped;
add `--force --only Alarms/PASS_ALARM.wav` to replace one. `--takes 3` keeps three versions in `raw/SFX/` to pick the
best by ear. Check the ElevenLabs terms for your plan before releasing the generated sounds.

## Synthetic sounds

```
python generate_tones.py --out ../../Plugins/LSFD/Audio
```
