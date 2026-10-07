# Bloops sounds, synthesized from scratch (OP-owned). Run: python make_sounds.py
import math, wave, struct, os
SR = 44100

def tone(f0, dur, f1=None, vol=0.5, decay=6.0, harm=(1, 0.3, 0.1)):
    f1 = f1 or f0
    out, ph = [], 0.0
    n = int(SR * dur)
    for i in range(n):
        t = i / SR
        f = f0 + (f1 - f0) * (i / n)
        ph += 2 * math.pi * f / SR
        env = min(1, t * 200) * math.exp(-decay * t)
        out.append(vol * env * sum(a * math.sin(ph * (k + 1)) for k, a in enumerate(harm)))
    return out

def gap(dur): return [0.0] * int(SR * dur)

def save(name, s):
    with wave.open(os.path.join(os.path.dirname(__file__), name), "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x)) * 32767)) for x in s))

# done: soft bell, two notes up (C6 -> G6)
save("done.wav", [a + b for a, b in zip(tone(1047, 0.12, decay=18), gap(0.04) + tone(1568, 0.08, decay=18))])
# needs you: boop-boop
save("needs_you.wav", tone(660, 0.11, decay=12, harm=(1, .5)) + gap(0.05) + tone(880, 0.16, decay=12, harm=(1, .5)))
# error: sad bwomp, pitch drops
save("error.wav", tone(330, 0.38, 180, decay=5, harm=(1, .6, .3)))
# spawn: bubbly pop, quick pitch up
save("spawn.wav", tone(300, 0.12, 1200, vol=.6, decay=17, harm=(1,)))
# poof: spawn pop, an octave lower
save("poof.wav", tone(150, 0.18, 600, vol=.6, decay=12.5, harm=(1,)))
