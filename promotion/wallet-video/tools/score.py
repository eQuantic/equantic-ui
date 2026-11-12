"""score.py <out.mid>: the agency cut's score, 133.33 BPM (a beat is 0.45 s, a bar 1.8 s), A minor.

The cut sits on this grid: the hook is bars 0-1, each device holds two bars (2-3, 4-5, 6-7, 8-9) and its
travel is the last two beats of them, the pull-back lands on bar 10 with the hero frame, the logo on bar 12.
One track per instrument, in the session's order: drums, bass, arp, pad, sub.
"""
import struct
import sys

PPQ = 480
BEAT = PPQ
BAR = 4 * BEAT
TOTAL_BARS = 16

# General MIDI drum notes.
KICK, SNARE, CLAP, HAT, OPEN_HAT, TOM_LO, TOM_MID, TOM_HI, CRASH = 36, 38, 39, 42, 46, 45, 47, 50, 49

# A minor: Am | F | C | G, one chord a bar.
CHORDS = [(57, 60, 64), (53, 57, 60), (48, 52, 55), (55, 59, 62)]
ROOTS = [45, 41, 48, 43]  # bass roots, an octave below the chords' own


def chord_of(bar):
    return CHORDS[bar % 4], ROOTS[bar % 4]


drums, bass, arp, pad, sub = [], [], [], [], []


def note(track, pitch, start, length, velocity=100):
    track.append((int(start), int(pitch), int(length), int(velocity)))


# Hook (bars 0-1): hats that grow, a snare roll into the first hit.
for step in range(16, 32):
    note(drums, HAT, step * BEAT / 4 + BAR, BEAT / 8, 30 + (step - 16) * 4)
for step in range(8):
    note(drums, SNARE, BAR + 2 * BEAT + step * BEAT / 4, BEAT / 8, 50 + step * 9)

# The groove (bars 2-9): four on the floor, clap on 2 and 4, offbeat hats, a fill into every travel.
for bar in range(2, 10):
    base = bar * BAR
    for beat in range(4):
        note(drums, KICK, base + beat * BEAT, BEAT / 2, 118 if beat == 0 else 104)
        note(drums, HAT, base + beat * BEAT + BEAT / 2, BEAT / 8, 78)
        note(drums, HAT, base + beat * BEAT + BEAT / 4, BEAT / 8, 44)
        note(drums, HAT, base + beat * BEAT + 3 * BEAT / 4, BEAT / 8, 44)
    note(drums, CLAP, base + BEAT, BEAT / 4, 100)
    note(drums, CLAP, base + 3 * BEAT, BEAT / 4, 104)
    note(drums, OPEN_HAT, base + 3 * BEAT + BEAT / 2, BEAT / 2, 70)
    if bar % 2 == 0:
        note(drums, CRASH, base, 2 * BEAT, 96)
    else:
        # The travel: the last two beats roll up the toms.
        fill = [TOM_HI, TOM_HI, TOM_MID, TOM_MID, TOM_LO, TOM_LO, SNARE, SNARE]
        for step, pitch in enumerate(fill):
            note(drums, pitch, base + 2 * BEAT + step * BEAT / 4, BEAT / 8, 70 + step * 6)

# Bar 9 is the pull-back: its last two beats are a snare roll in 32nds instead of the toms.
for step in range(16):
    note(drums, SNARE, 9 * BAR + 2 * BEAT + step * BEAT / 8, BEAT / 16, 60 + step * 4)

# The hero frame (bars 10-11): the hit, then a half-time groove.
for bar in (10, 11):
    base = bar * BAR
    note(drums, KICK, base, BEAT / 2, 124 if bar == 10 else 104)
    note(drums, KICK, base + 2 * BEAT + BEAT / 2, BEAT / 2, 90)
    note(drums, CLAP, base + 2 * BEAT, BEAT / 4, 104)
    for beat in range(4):
        note(drums, HAT, base + beat * BEAT + BEAT / 2, BEAT / 8, 64)
note(drums, CRASH, 10 * BAR, 4 * BEAT, 124)

# The logo (bar 12): one last hit, and the crash rings.
note(drums, KICK, 12 * BAR, BEAT, 127)
note(drums, CRASH, 12 * BAR, 8 * BEAT, 110)

# Bass: driving eighths through the groove, whole notes on the hero, the root held under the logo.
for bar in range(2, 10):
    _, root = chord_of(bar)
    for eighth in range(8):
        if bar % 2 == 1 and eighth >= 4:
            # Under the travel: an octave jump that lifts into the next device.
            note(bass, root + 12 if eighth % 2 else root, bar * BAR + eighth * BEAT / 2, BEAT / 2 - 30, 96)
        else:
            note(bass, root, bar * BAR + eighth * BEAT / 2, BEAT / 2 - 40, 108 if eighth % 2 == 0 else 84)
for bar in (10, 11):
    _, root = chord_of(bar)
    note(bass, root, bar * BAR, 2 * BEAT, 110)
    note(bass, root, bar * BAR + 2 * BEAT + BEAT / 2, BEAT, 96)
note(bass, 45, 12 * BAR, 6 * BEAT, 112)

# Arp: sixteenths over the chord, up and down, from the first device to the hero.
pattern = [0, 1, 2, 1, 0, 2, 1, 2]
for bar in range(2, 12):
    chord, _ = chord_of(bar)
    tones = [chord[0] + 12, chord[1] + 12, chord[2] + 12]
    for step in range(16):
        velocity = 92 if step % 4 == 0 else 66
        note(arp, tones[pattern[step % 8]], bar * BAR + step * BEAT / 4, BEAT / 4 - 20, velocity)

# Pad: the chords all the way, softer under the hook, held over the logo.
for bar in range(0, 12):
    chord, _ = chord_of(bar)
    for pitch in chord:
        note(pad, pitch, bar * BAR, BAR - 10, 64 if bar < 2 else 80)
for pitch in (57, 60, 64, 69):
    note(pad, pitch, 12 * BAR, 4 * BAR - 10, 84)

# Sub: a sine boom on every hit.
for at in (2 * BAR, 10 * BAR, 12 * BAR):
    note(sub, 33, at, 3 * BEAT, 120)


def variable_length(value):
    out = [value & 0x7F]
    value >>= 7
    while value:
        out.insert(0, (value & 0x7F) | 0x80)
        value >>= 7
    return bytes(out)


def track_chunk(notes, channel, name, tempo=False):
    events = []
    for start, pitch, length, velocity in notes:
        events.append((start, 1, bytes([0x90 | channel, pitch, velocity])))
        events.append((start + length, 0, bytes([0x80 | channel, pitch, 0])))
    events.sort(key=lambda event: (event[0], event[1]))
    data = bytearray()
    data += variable_length(0) + b"\xff\x03" + variable_length(len(name)) + name.encode()
    if tempo:
        data += variable_length(0) + b"\xff\x51\x03" + (450000).to_bytes(3, "big")
        data += variable_length(0) + b"\xff\x58\x04\x04\x02\x18\x08"
    now = 0
    for at, _, message in events:
        data += variable_length(at - now) + message
        now = at
    data += variable_length(0) + b"\xff\x2f\x00"
    return b"MTrk" + struct.pack(">I", len(data)) + bytes(data)


tracks = [(drums, 9, "Drums"), (bass, 0, "Bass"), (arp, 1, "Arp"), (pad, 2, "Pad"), (sub, 3, "Sub")]
out = b"MThd" + struct.pack(">IHHH", 6, 1, len(tracks), PPQ)
for index, (notes, channel, name) in enumerate(tracks):
    out += track_chunk(notes, channel, name, tempo=index == 0)
open(sys.argv[1], "wb").write(out)
print(sys.argv[1], {name: len(notes) for notes, _, name in tracks})
