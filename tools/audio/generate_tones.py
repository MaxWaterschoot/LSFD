"""Generates LSFD's synthetic sounds (dispatch tones, radio clicks and static, alarms, CPR metronome).

Usage: python generate_tones.py --out ../../Plugins/LSFD/Audio   (needs numpy)
"""
import argparse, os, wave
import numpy as np

SR = 44100
rng = np.random.default_rng(7)

def tone(freq, dur, vol=0.5, fade=0.008):
    t = np.arange(int(SR * dur)) / SR
    s = np.sin(2 * np.pi * freq * t) * vol
    n = int(SR * fade)
    if n and len(s) > 2 * n:
        ramp = np.linspace(0, 1, n)
        s[:n] *= ramp; s[-n:] *= ramp[::-1]
    return s

def silence(dur): return np.zeros(int(SR * dur))

def noise(dur, vol=0.2, lo=300, hi=3400):
    s = rng.standard_normal(int(SR * dur))
    spec = np.fft.rfft(s); f = np.fft.rfftfreq(len(s), 1 / SR)
    spec[(f < lo) | (f > hi)] = 0
    s = np.fft.irfft(spec, len(s)); s /= np.max(np.abs(s)) + 1e-9
    return s * vol

def env(s, attack=0.005, release=0.05):
    a, r = int(SR * attack), int(SR * release)
    if a: s[:a] *= np.linspace(0, 1, a)
    if r: s[-r:] *= np.linspace(1, 0, r)
    return s

def cat(*parts): return np.concatenate(parts)

def write(root, rel, s):
    path = os.path.join(root, rel); os.makedirs(os.path.dirname(path), exist_ok=True)
    s = np.clip(s, -1, 1)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((s * 32767).astype(np.int16).tobytes())
    print(rel)

def main():
    p = argparse.ArgumentParser(); p.add_argument("--out", required=True); root = p.parse_args().out

    # Dispatch tones
    write(root, "Dispatch/Tones/ATTENTION_TONE.wav", cat(tone(1000, .15), silence(.05), tone(1500, .22)))
    warble = cat(*[tone(f, .125, .45, .004) for f in [850, 1250] * 6])
    write(root, "Dispatch/Tones/ALERT_TONE_P1.wav", warble)
    write(root, "Dispatch/Tones/ALERT_TONE_P2.wav", cat(*[cat(tone(1000, .12), silence(.08)) for _ in range(3)]))
    write(root, "Dispatch/Tones/ALERT_TONE_P3.wav", tone(880, .25, .35))
    write(root, "Dispatch/Tones/FIRE_TONES.wav", cat(tone(853.2, 1.0, .45), tone(960.0, 3.0, .45), silence(.3)))

    # Radio
    click = cat(env(noise(.03, .5, 800, 6000), .001, .02), silence(.02))
    write(root, "Radio/MIC_CLICK_ON.wav", click)
    write(root, "Radio/MIC_CLICK_OFF.wav", cat(silence(.02), env(noise(.18, .35), .002, .12)))
    write(root, "Radio/STATIC_SHORT.wav", env(noise(.3, .12), .02, .1))
    write(root, "Radio/STATIC_LOOP.wav", noise(5.0, .05))

    # Alarms
    pass_alarm = cat(*[cat(tone(3100, .1, .5, .003), tone(3500, .1, .5, .003)) for _ in range(15)])
    write(root, "Alarms/PASS_ALARM.wav", pass_alarm)
    write(root, "Alarms/PAGER_ALERT.wav", cat(*[cat(tone(2000, .08, .45, .003), silence(.06)) for _ in range(4)], silence(.3),
                                             *[cat(tone(2000, .08, .45, .003), silence(.06)) for _ in range(4)]))
    t3 = cat(*[cat(tone(3100, .5, .4), silence(.5)) for _ in range(3)], silence(1.0))       # smoke alarm, temporal-3
    write(root, "Alarms/SMOKE_DETECTOR.wav", t3)
    t4 = cat(*[cat(tone(3100, .1, .4, .003), silence(.1)) for _ in range(4)], silence(5.0))  # CO alarm, temporal-4
    write(root, "Alarms/CO_ALARM.wav", t4)
    write(root, "Alarms/STATION_PA_CHIME.wav", cat(tone(784, .45, .35, .02) * np.exp(-np.linspace(0, 3, int(SR*.45))),
                                                   tone(659, .8, .35, .02) * np.exp(-np.linspace(0, 3, int(SR*.8)))))

    # CPR metronome: 110 per minute, 10 seconds, loopable
    beat = 60 / 110
    clickb = cat(tone(1200, .03, .5, .002), silence(beat - .03))
    write(root, "Effects/AED_METRONOME.wav", np.tile(clickb, 18))

if __name__ == "__main__":
    main()
