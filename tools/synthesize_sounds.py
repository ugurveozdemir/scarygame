"""Synthesizes the electronic and mechanical sounds of the story: memo beeps, the intercom, the keypad, the lift,
the breaker, the siren, the transmitter and so on. Field recordings sound better for anything organic, so this
only makes what a machine would make, plus a few textures like paper and broken glass.

Usage: python3 tools/synthesize_sounds.py [output directory]
"""

import math
import random
import struct
import sys
import wave
from pathlib import Path

RATE = 44100

random.seed(14)


def seconds(duration):
    return int(duration * RATE)


def silence(duration):
    return [0.0] * seconds(duration)


def sine(frequency, duration, phase=0.0):
    step = 2 * math.pi * frequency / RATE
    return [math.sin(phase + i * step) for i in range(seconds(duration))]


def sweep(start_hz, end_hz, duration, shape=lambda t: t):
    """A sine whose frequency moves from start_hz to end_hz; shape maps time 0..1 to progress 0..1."""
    samples, phase, count = [], 0.0, seconds(duration)
    for i in range(count):
        frequency = start_hz + (end_hz - start_hz) * shape(i / count)
        phase += 2 * math.pi * frequency / RATE
        samples.append(math.sin(phase))
    return samples


def noise(duration):
    return [random.uniform(-1.0, 1.0) for _ in range(seconds(duration))]


def lowpass(samples, cutoff_hz):
    alpha = 1 - math.exp(-2 * math.pi * cutoff_hz / RATE)
    out, state = [], 0.0
    for sample in samples:
        state += alpha * (sample - state)
        out.append(state)
    return out


def highpass(samples, cutoff_hz):
    low = lowpass(samples, cutoff_hz)
    return [s - l for s, l in zip(samples, low)]


def bandpass(samples, low_hz, high_hz):
    return lowpass(highpass(samples, low_hz), high_hz)


def envelope(samples, attack, decay_per_second):
    """A fast linear attack, then an exponential decay."""
    attack_samples = max(1, seconds(attack))
    out = []
    for i, sample in enumerate(samples):
        if i < attack_samples:
            gain = i / attack_samples
        else:
            gain = math.exp(-decay_per_second * (i - attack_samples) / RATE)
        out.append(sample * gain)
    return out


def fade(samples, fade_in, fade_out):
    count, fade_in_samples, fade_out_samples = len(samples), seconds(fade_in), seconds(fade_out)
    out = []
    for i, sample in enumerate(samples):
        gain = 1.0
        if fade_in_samples and i < fade_in_samples:
            gain *= i / fade_in_samples
        if fade_out_samples and i > count - fade_out_samples:
            gain *= (count - i) / fade_out_samples
        out.append(sample * gain)
    return out


def scale(samples, gain):
    return [s * gain for s in samples]


def mix(*tracks):
    """Mixes (start_seconds, samples) pairs into one track."""
    length = max(seconds(start) + len(samples) for start, samples in tracks)
    out = [0.0] * length
    for start, samples in tracks:
        offset = seconds(start)
        for i, sample in enumerate(samples):
            out[offset + i] += sample
    return out


def add(*tracks):
    return mix(*((0.0, track) for track in tracks))


def bell(frequency, duration, decay=3.0):
    """A struck metal bar: a few inharmonic partials that die away, the higher ones faster."""
    partials = [(1.0, 1.0), (2.76, 0.5), (5.4, 0.25), (8.93, 0.12)]
    return add(*(scale(envelope(sine(frequency * ratio, duration), 0.002, decay * (1 + ratio / 2)), level)
                 for ratio, level in partials))


def clank(frequency, duration=0.4):
    """A short knock on sheet metal."""
    hit = envelope(highpass(noise(0.03), 800), 0.001, 120)
    ring = add(*(scale(envelope(sine(frequency * ratio, duration), 0.001, 18 * ratio), 0.4 / ratio)
                 for ratio in (1.0, 1.73, 2.41, 3.3)))
    return add(hit, ring)


def thump(frequency=60, duration=0.5):
    body = envelope(sweep(frequency * 2, frequency, duration, lambda t: min(1.0, t * 8)), 0.002, 9)
    click = envelope(lowpass(noise(0.02), 2500), 0.0005, 200)
    return add(body, scale(click, 0.6))


def crackle(duration, density, low_hz=2000, high_hz=9000):
    """Random sharp pops, like sparks or crunching glass."""
    out = [0.0] * seconds(duration)
    for _ in range(int(duration * density)):
        pop = envelope(noise(random.uniform(0.002, 0.012)), 0.0003, random.uniform(300, 900))
        start = random.randrange(0, max(1, len(out) - len(pop)))
        gain = random.uniform(0.2, 1.0)
        for i, sample in enumerate(pop):
            out[start + i] += sample * gain
    return bandpass(out, low_hz, high_hz)


def telephone(samples):
    """A small loudspeaker: no lows, no highs, a little crunch."""
    filtered = bandpass(samples, 450, 3200)
    return [math.tanh(s * 2.5) / 2.5 for s in filtered]


def reverb(samples, room=0.8, wet=0.35, tail=1.5):
    """A small Schroeder reverb, enough for a tiled corridor."""
    padded = samples + [0.0] * seconds(tail)
    combs = [int(RATE * t * room) for t in (0.0297, 0.0371, 0.0411, 0.0437)]
    out = [0.0] * len(padded)
    for delay in combs:
        buffer, feedback = [0.0] * delay, 0.78
        for i, sample in enumerate(padded):
            delayed = buffer[i % delay]
            buffer[i % delay] = sample + delayed * feedback
            out[i] += delayed / len(combs)
    for delay in (int(RATE * 0.005), int(RATE * 0.0017)):
        buffer, gain = [0.0] * delay, 0.7
        result = []
        for i, sample in enumerate(out):
            delayed = buffer[i % delay]
            value = -gain * sample + delayed
            buffer[i % delay] = sample + gain * value
            result.append(value)
        out = result
    return [d * (1 - wet) + w * wet for d, w in zip(padded, out)]


def normalize(samples, peak=0.89):
    loudest = max(abs(s) for s in samples) or 1.0
    return [s * peak / loudest for s in samples]


def make_loopable(samples, crossfade):
    """Crossfades the end into the start so the sound loops without a click."""
    overlap = seconds(crossfade)
    body = samples[:len(samples) - overlap]
    for i in range(overlap):
        t = i / overlap
        body[i] = body[i] * t + samples[len(samples) - overlap + i] * (1 - t)
    return body


def write(directory, name, samples):
    path = directory / f"{name}.wav"
    with wave.open(str(path), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples))
    print(path)


# --- The sounds -------------------------------------------------------------------------------------------------

def osd_beep():
    beep = fade(sine(2400, 0.045), 0.003, 0.01)
    return normalize(mix((0.0, beep), (0.075, beep)), 0.5)


def paper():
    rustle = bandpass(noise(0.7), 900, 7000)
    shape = [abs(math.sin(i / seconds(0.7) * math.pi * 3)) ** 2 for i in range(len(rustle))]
    body = [r * s for r, s in zip(rustle, shape)]
    return normalize(add(body, scale(crackle(0.7, 60, 1500, 8000), 0.6)), 0.6)


def rummage():
    tracks = [(0.0, scale(bandpass(noise(1.6), 300, 5000), 0.15))]
    for start in (0.05, 0.3, 0.55, 0.9, 1.2):
        tracks.append((start, scale(clank(random.uniform(500, 900)), random.uniform(0.5, 1.0))))
    tracks.append((0.4, crackle(0.8, 40, 1000, 6000)))
    return normalize(fade(mix(*tracks), 0.02, 0.3), 0.8)


def intercom_page():
    chime = mix((0.0, bell(659, 1.6, 2.0)), (0.55, bell(523, 2.0, 1.6)))
    hum = scale(add(sine(120, 3.0), scale(sine(240, 3.0), 0.5)), 0.04)
    return normalize(reverb(telephone(add(fade(hum, 0.05, 0.3), chime)), wet=0.45, tail=1.2), 0.9)


def intercom_click():
    click = envelope(noise(0.01), 0.0002, 400)
    hiss = fade(scale(bandpass(noise(0.35), 800, 4000), 0.15), 0.01, 0.1)
    return normalize(mix((0.0, click), (0.02, hiss), (0.33, click)), 0.6)


def keypad_beep():
    return normalize(fade(sine(1250, 0.08), 0.002, 0.02), 0.45)


def keypad_accept():
    beep = fade(sine(1500, 0.07), 0.002, 0.02)
    high = fade(sine(2000, 0.12), 0.002, 0.04)
    return normalize(mix((0.0, beep), (0.1, high)), 0.5)


def keypad_error():
    buzz = [1.0 if s > 0 else -1.0 for s in sine(220, 0.45)]
    return normalize(fade(lowpass(buzz, 2500), 0.005, 0.05), 0.4)


def lift_ding():
    return normalize(reverb(add(bell(880, 2.2, 1.4), scale(bell(1320, 1.5, 2.0), 0.3)), wet=0.3), 0.8)


def lift_doors():
    whir = add(scale(sine(140, 1.8), 0.3), scale(bandpass(noise(1.8), 200, 1500), 0.4))
    whir = fade(whir, 0.15, 0.25)
    return normalize(mix((0.0, scale(clank(300, 0.3), 0.4)), (0.05, whir), (1.75, thump(90, 0.4))), 0.75)


def lift_ride():
    hum = add(scale(sine(55, 6.0), 0.5), scale(sine(110, 6.0), 0.25), scale(lowpass(noise(6.0), 180), 1.2))
    rattle = scale(bandpass(crackle(6.0, 30, 300, 2000), 300, 2000), 0.3)
    ride = fade(add(hum, rattle), 1.2, 1.4)
    return normalize(mix((0.0, thump(70, 0.6)), (0.1, ride), (5.6, thump(80, 0.6))), 0.8)


def breaker():
    lever = mix((0.0, scale(clank(420, 0.3), 0.6)), (0.12, thump(55, 0.9)))
    sparks = fade(crackle(1.0, 140, 1500, 9000), 0.0, 0.6)
    power_down = scale(fade(sweep(120, 30, 1.2, lambda t: t ** 0.5), 0.0, 0.8), 0.4)
    return normalize(reverb(mix((0.0, lever), (0.1, sparks), (0.15, power_down)), wet=0.4), 0.9)


def light_burst():
    pop = envelope(highpass(noise(0.08), 1200), 0.0005, 60)
    tinkle = add(*(scale(envelope(sine(random.uniform(3500, 7500), 0.5), 0.001, random.uniform(10, 25)), 0.15)
                   for _ in range(8)))
    return normalize(reverb(mix((0.0, pop), (0.02, tinkle), (0.01, crackle(0.4, 80))), wet=0.35), 0.85)


def remnant_static():
    length = 3.0
    hiss = bandpass(noise(length + 0.3), 500, 6000)
    flutter = [0.55 + 0.45 * math.sin(2 * math.pi * 3 * i / seconds(length)) ** 2 for i in range(len(hiss))]
    body = [h * f for h, f in zip(hiss, flutter)]
    body = add(body, scale(crackle(length + 0.3, 50), 0.8))
    return normalize(make_loopable(body, 0.3), 0.7)


def glass_crunch():
    grains = crackle(0.3, 260, 2500, 11000)
    tinkle = add(*(scale(envelope(sine(random.uniform(4000, 9000), 0.3), 0.001, random.uniform(25, 50)), 0.1)
                   for _ in range(5)))
    return normalize(fade(add(grains, tinkle), 0.002, 0.1), 0.8)


def siren():
    period = 4.0
    wail = sweep(550, 1150, period, lambda t: 0.5 - 0.5 * math.cos(2 * math.pi * t))
    wail = add(wail, scale(sweep(1100, 2300, period, lambda t: 0.5 - 0.5 * math.cos(2 * math.pi * t)), 0.25))
    distant = lowpass(highpass(wail, 300), 3000)
    return normalize(reverb(distant, room=1.3, wet=0.5, tail=0.0), 0.8)


def reset_whine():
    whine = sweep(1800, 9500, 5.0, lambda t: t ** 1.6)
    whine = fade(scale(whine, 0.6), 2.0, 0.05)
    flash = mix((4.95, thump(45, 1.5)), (4.95, scale(fade(noise(1.5), 0.0, 1.4), 0.4)))
    return normalize(add(whine, flash), 0.9)


def transmitter_hum():
    length = 2.0
    hum = add(*(scale(sine(50 * n, length + 0.2), 0.5 / n) for n in (1, 2, 3, 4, 6)))
    buzz = scale(bandpass(noise(length + 0.2), 2000, 5000), 0.05)
    pulse = [0.85 + 0.15 * math.sin(2 * math.pi * 2 * i / seconds(length)) for i in range(len(hum))]
    return normalize(make_loopable([(h + b) * p for h, b, p in zip(hum, buzz, pulse)], 0.2), 0.7)


def transmitter_destroy():
    sparks = fade(crackle(2.5, 120, 1200, 9000), 0.0, 1.5)
    power_down = fade(add(sweep(150, 20, 3.0, lambda t: t ** 0.4), scale(sweep(300, 40, 3.0, lambda t: t ** 0.4), 0.4)),
                      0.0, 1.5)
    return normalize(reverb(mix((0.0, thump(50, 1.0)), (0.0, sparks), (0.05, scale(power_down, 0.6))), wet=0.4), 0.9)


def tape_insert():
    mechanism = mix((0.0, scale(clank(1400, 0.15), 0.5)), (0.35, scale(clank(900, 0.2), 0.7)))
    motor = fade(scale(add(sine(320, 0.9), bandpass(noise(0.9), 500, 3000)), 0.12), 0.1, 0.2)
    return normalize(mix((0.0, mechanism), (0.45, motor)), 0.6)


def tv_static():
    return normalize(make_loopable(bandpass(noise(2.2), 300, 9000), 0.2), 0.5)


def gate_motor():
    motor = add(scale(sine(95, 4.0), 0.4), scale(sine(190, 4.0), 0.2), scale(bandpass(noise(4.0), 150, 1200), 0.4))
    rumble = scale(crackle(4.0, 25, 200, 1500), 0.4)
    return normalize(mix((0.0, thump(70, 0.5)), (0.1, fade(add(motor, rumble), 0.3, 0.6)), (3.9, scale(clank(260, 0.6), 0.8))),
                     0.85)


def magnetic_release():
    drop = scale(fade(sweep(160, 60, 0.6, lambda t: t ** 0.5), 0.0, 0.3), 0.6)
    return normalize(reverb(mix((0.0, thump(60, 0.6)), (0.0, drop), (0.05, scale(clank(500, 0.4), 0.5))), wet=0.35), 0.8)


SOUNDS = {
    "osd_beep": osd_beep,
    "paper": paper,
    "rummage": rummage,
    "intercom_page": intercom_page,
    "intercom_click": intercom_click,
    "keypad_beep": keypad_beep,
    "keypad_accept": keypad_accept,
    "keypad_error": keypad_error,
    "lift_ding": lift_ding,
    "lift_doors": lift_doors,
    "lift_ride": lift_ride,
    "breaker": breaker,
    "light_burst": light_burst,
    "remnant_static": remnant_static,
    "glass_crunch_01": glass_crunch,
    "glass_crunch_02": glass_crunch,
    "glass_crunch_03": glass_crunch,
    "glass_crunch_04": glass_crunch,
    "siren": siren,
    "reset_whine": reset_whine,
    "transmitter_hum": transmitter_hum,
    "transmitter_destroy": transmitter_destroy,
    "tape_insert": tape_insert,
    "tv_static": tv_static,
    "gate_motor": gate_motor,
    "magnetic_release": magnetic_release,
}

if __name__ == "__main__":
    output = Path(sys.argv[1] if len(sys.argv) > 1 else "assets/sounds/story")
    output.mkdir(parents=True, exist_ok=True)
    only = set(sys.argv[2:])
    for name, make in SOUNDS.items():
        if not only or name in only:
            write(output, name, make())
