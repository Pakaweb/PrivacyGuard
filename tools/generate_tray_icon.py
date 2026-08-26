#!/usr/bin/env python3
"""Write a small shield ICO that stays readable on light and dark taskbars."""
from pathlib import Path
import struct
import zlib

SIZE = 32
GREEN = (16, 163, 94, 255)
WHITE = (255, 255, 255, 255)
DARK = (20, 28, 24, 255)


def in_shield(x, y):
    nx = (x + 0.5) / SIZE
    ny = (y + 0.5) / SIZE
    if ny < 0.12 or ny > 0.90:
        return False
    half = 0.34 - (ny - 0.12) * 0.08
    if ny > 0.62:
        half = 0.34 - (ny - 0.62) * 0.85
    return abs(nx - 0.5) <= half


def in_inner(x, y):
    nx = (x + 0.5) / SIZE
    ny = (y + 0.5) / SIZE
    if ny < 0.20 or ny > 0.82:
        return False
    half = 0.24 - (ny - 0.20) * 0.06
    if ny > 0.60:
        half = 0.24 - (ny - 0.60) * 0.75
    return abs(nx - 0.5) <= half


def png_chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)


def write_png(path: Path):
    rows = []
    for y in range(SIZE):
        row = bytearray([0])
        for x in range(SIZE):
            if in_inner(x, y):
                pixel = GREEN
            elif in_shield(x, y):
                pixel = WHITE if (x + y) % 2 == 0 else DARK
            else:
                pixel = (0, 0, 0, 0)
            row.extend(pixel)
        rows.append(bytes(row))
    raw = b"".join(rows)
    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", ihdr) + png_chunk(b"IDAT", zlib.compress(raw, 9)) + png_chunk(b"IEND", b"")
    path.write_bytes(png)
    return png


def write_ico(ico_path: Path, png: bytes):
    # PNG-in-ICO (Vista+), one 32x32 image.
    header = struct.pack("<HHH", 0, 1, 1)
    entry = struct.pack("<BBBBHHII", SIZE, SIZE, 0, 0, 1, 32, len(png), 22)
    ico_path.write_bytes(header + entry + png)


def main():
    root = Path(__file__).resolve().parents[1] / "src" / "PrivacyGuard" / "Assets"
    root.mkdir(parents=True, exist_ok=True)
    png = write_png(root / "tray.png")
    write_ico(root / "tray.ico", png)
    print(f"Wrote {root / 'tray.ico'}")


if __name__ == "__main__":
    main()
