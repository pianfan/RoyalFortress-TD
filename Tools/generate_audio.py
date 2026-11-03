"""Generate the project's original PCM sound effects; Python standard library only."""
from array import array
from pathlib import Path
import math
import random
import sys
import wave

RATE = 24000
DEST = Path(__file__).resolve().parents[1] / "Assets/Resources/FortressAudio/Sfx"
TAU = math.tau


def save(name, samples, gain=.75):
    peak = max(abs(x) for x in samples) or 1
    scale = min(gain / peak, 4)
    data = array('h', (round(max(-1, min(1, x * scale)) * 32767) for x in samples))
    if sys.byteorder != 'little':
        data.byteswap()
    DEST.mkdir(parents=True, exist_ok=True)
    with wave.open(str(DEST / (name + '.wav')), 'wb') as f:
        f.setparams((1, 2, RATE, len(data), 'NONE', 'not compressed'))
        f.writeframes(data.tobytes())


def effect(name, length, start=440, end=220, noise=0, tonal=1, decay=6, seed=1):
    rng = random.Random(seed)
    phase = low = 0
    samples = []
    for i in range(round(RATE * length)):
        t = i / RATE
        u = t / length
        phase += TAU * (start * (end / start) ** u) / RATE
        raw = rng.uniform(-1, 1)
        low += .18 * (raw - low)
        env = min(1, t / .008) * min(1, (length-t) / .025) * math.exp(-decay*u)
        sound = tonal * (math.sin(phase) + .25 * math.sin(phase * 2.01)) + noise * (raw * .45 + low)
        samples.append(sound * env)
    save(name, samples)


def chime(name, notes, beat=.13, length=1.3, minor=False):
    samples = [0.] * round(RATE * length)
    for n, midi in enumerate(notes):
        begin = round(n * beat * RATE)
        freq = 440 * 2 ** ((midi-69) / 12)
        for i in range(begin, len(samples)):
            t = (i-begin) / RATE
            env = min(1, t/.008) * math.exp(-t*5) * min(1, (len(samples)-i)/(RATE*.08))
            samples[i] += env * (math.sin(TAU*freq*t) + .18*math.sin(TAU*freq*2*t)) / len(notes)**.5
    save(name, samples)


def wind(name, seed, gritty=False):
    # Periodic grains and periodic envelopes make the WAV seam continuous.
    rng = random.Random(seed)
    length = 8
    n = RATE * length
    raw = [rng.uniform(-1,1) for _ in range(n)]
    low = 0
    for i in range(n-8000, n):
        low += .035 * (raw[i]-low)
    samples = []
    for i, value in enumerate(raw):
        t = i/RATE
        low += .035 * (value-low)
        swell = .65 + .18*math.sin(TAU*t/length) + .12*math.cos(TAU*3*t/length)
        sound = (low*2 + value*(.20 if gritty else .04)) * swell
        if not gritty:
            sound += .015*math.sin(TAU*672*t)*(.5+.5*math.sin(TAU*2*t/length))**12
        samples.append(sound)
    # Overlap the final 80 ms with the beginning, then start playback after that overlap.
    edge = int(RATE * .08)
    for i in range(edge):
        a = i/edge
        samples[n-edge+i] = samples[n-edge+i]*(1-a) + samples[i]*a
    save(name, samples[edge:], .45)


if __name__ == '__main__':
    effect('ui-click', .12, 760, 580, .12, decay=7)
    chime('card-pick', [69,76], .045, .25)
    effect('invalid', .22, 180, 100, .15, decay=4)
    chime('build', [60,67,76], .055, .65)
    effect('arrow', .15, 1100, 260, 1.2, .25, 5, 9)
    effect('mage', .34, 360, 1050, .1, 1, 3, 8)
    effect('stone', .26, 130, 48, 1.4, .6, 7, 2)
    effect('arrow-hit', .13, 220, 100, 1.5, .2, 9, 3)
    effect('magic-hit', .30, 880, 220, .5, 1, 6, 4)
    effect('stone-hit', .50, 90, 30, 2, .8, 6, 5)
    effect('enemy-death', .32, 180, 65, 1.1, .5, 7, 7)
    effect('leak', .6, 210, 130, .15, 1, 3)
    chime('wave', [55,62,67], .11, 1.05)
    chime('wave-clear', [67,71,74], .10, .95)
    chime('victory', [60,64,67,72,79], .18, 2.6)
    chime('defeat', [60,58,55,48], .25, 2.4)
    chime('boss', [42,49,54,43], .16, 1.8)
    effect('frost', .7, 1300, 650, .2, 1, 4)
    effect('pulse', .85, 220, 880, .6, .8, 3)
    chime('seal', [55,62,67,74,79], .12, 2.1)
    wind('snow-wind', 18)
    wind('sand-wind', 27, True)
    print('Generated', len(list(DEST.glob('*.wav'))), 'sound effects in', DEST)
