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

## Synthetic sounds

```
python generate_tones.py --out ../../Plugins/LSFD/Audio
```
