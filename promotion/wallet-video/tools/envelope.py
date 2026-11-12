"""envelope.py <file.wav> [window seconds]: the loudness of a WAV over time, one line a window, as dBFS RMS
and a bar, so a mix can be checked against the cut without listening to it. Reads PCM 16/24/32 and
WAVE_FORMAT_EXTENSIBLE, which the standard library's wave module refuses."""
import math
import struct
import sys

path = sys.argv[1]
window = float(sys.argv[2]) if len(sys.argv) > 2 else 0.45
data = open(path, "rb").read()
assert data[:4] == b"RIFF" and data[8:12] == b"WAVE"
at, channels, rate, bits, samples = 12, 0, 0, 0, b""
while at < len(data):
    chunk, size = data[at:at + 4], struct.unpack("<I", data[at + 4:at + 8])[0]
    body = data[at + 8:at + 8 + size]
    if chunk == b"fmt ":
        _, channels, rate, _, _, bits = struct.unpack("<HHIIHH", body[:16])
    elif chunk == b"data":
        samples = body
    at += 8 + size + (size & 1)
width = bits // 8
frame = width * channels
count = len(samples) // frame
scale = float(1 << (bits - 1))


def sample(index):
    raw = samples[index * width:(index + 1) * width]
    if width == 3:
        return int.from_bytes(raw, "little", signed=True) / scale
    return int.from_bytes(raw, "little", signed=True) / scale


step = int(window * rate)
for start in range(0, count, step):
    total, peak = 0.0, 0.0
    end = min(count, start + step)
    for index in range(start, end, 4):  # every fourth frame is enough for a level
        value = sample(index * channels)
        total += value * value
        peak = max(peak, abs(value))
    measured = max(1, (end - start) // 4)
    rms = math.sqrt(total / measured)
    db = 20 * math.log10(rms) if rms > 0 else -120
    print(f"{start / rate:6.2f}s {db:7.1f} dB {'#' * max(0, int((db + 60) / 1.5))}")
