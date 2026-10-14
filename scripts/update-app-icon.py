"""Regenerates the application icon in src/UpSTEPer.App/Assets from its geometry below.

Usage: python scripts/update-app-icon.py
The icon is an isometric cube in the app's colours (Themes/Tokens.axaml): a light top, a blue
left face and an orange right face, the selection colour, standing for a coloured face.
Writes upsteper.svg (source for designers and packaging), upsteper.png (256 px, window icon) and
upsteper.ico (16-256 px, Windows executable). Standard library only: the faces are polygons, so
they are rasterised here with supersampling instead of needing an SVG renderer.
"""
import math
import os
import struct
import zlib

ASSETS = os.path.join(os.path.dirname(__file__), "..", "src", "UpSTEPer.App", "Assets")
CANVAS = 1024
PADDING = 72
ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]

TOP = "#DCEEFF"     # AccentHoverColor
LEFT = "#1769AA"    # AccentColor
RIGHT = "#FF8C1A"   # selection colour


def cube_faces():
    """Faces of a unit cube in isometric projection, back to front, scaled to the canvas."""
    def project(x, y, z):
        return ((x - y) * math.cos(math.pi / 6), (x + y) * 0.5 - z)

    faces = [
        ([(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)], TOP),
        ([(0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)], LEFT),
        ([(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)], RIGHT),
    ]
    points = [project(*p) for corners, _ in faces for p in corners]
    min_x, max_x = min(p[0] for p in points), max(p[0] for p in points)
    min_y, max_y = min(p[1] for p in points), max(p[1] for p in points)
    scale = (CANVAS - 2 * PADDING) / max(max_x - min_x, max_y - min_y)
    off_x = (CANVAS - (max_x - min_x) * scale) / 2 - min_x * scale
    off_y = (CANVAS - (max_y - min_y) * scale) / 2 - min_y * scale
    return [([(px * scale + off_x, py * scale + off_y) for px, py in (project(*p) for p in corners)], colour)
            for corners, colour in faces]


def write_svg(faces, path):
    paths = []
    for polygon, colour in faces:
        d = " ".join(("M" if i == 0 else "L") + f"{x:.1f} {y:.1f}" for i, (x, y) in enumerate(polygon)) + " Z"
        # A hairline in the face colour closes the anti-aliasing seams between adjacent faces.
        paths.append(f'  <path d="{d}" fill="{colour}" stroke="{colour}" stroke-width="1" stroke-linejoin="round"/>')
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(f'<svg xmlns="http://www.w3.org/2000/svg" width="{CANVAS}" height="{CANVAS}" viewBox="0 0 {CANVAS} {CANVAS}">\n'
                f"  <title>UpSTEPer</title>\n" + "\n".join(paths) + "\n</svg>\n")


def rasterise(faces, size, samples=8):
    """RGBA rows; each pixel averages samples x samples points, the frontmost face wins per point."""
    rgb = [tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) for _, c in faces]
    scale = size / CANVAS
    polygons = [[(x * scale, y * scale) for x, y in polygon] for polygon, _ in faces]
    width = size * samples
    acc = [[0, 0, 0, 0] for _ in range(size * size)]
    for sy in range(size * samples):
        y = (sy + 0.5) / samples
        row = [-1] * width
        for index, polygon in enumerate(polygons):
            crossings = []
            for (x0, y0), (x1, y1) in zip(polygon, polygon[1:] + polygon[:1]):
                if (y0 <= y < y1) or (y1 <= y < y0):
                    crossings.append(x0 + (y - y0) * (x1 - x0) / (y1 - y0))
            crossings.sort()
            for a, b in zip(crossings[::2], crossings[1::2]):
                first = max(0, math.ceil(a * samples - 0.5))
                last = min(width - 1, math.floor(b * samples - 0.5))
                row[first:last + 1] = [index] * max(0, last - first + 1)
        base = (sy // samples) * size
        for sx, index in enumerate(row):
            if index >= 0:
                pixel = acc[base + sx // samples]
                r, g, b = rgb[index]
                pixel[0] += r
                pixel[1] += g
                pixel[2] += b
                pixel[3] += 1
    total = samples * samples
    rows = []
    for py in range(size):
        line = bytearray()
        for px in range(size):
            r, g, b, n = acc[py * size + px]
            # Colours are averaged over covered samples only (straight alpha), alpha is the coverage.
            line += bytes((r // n, g // n, b // n, 255 * n // total)) if n else b"\0\0\0\0"
        rows.append(bytes(line))
    return rows


def png(rows, size):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    raw = b"".join(b"\0" + row for row in rows)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def ico(images):
    """ICO with PNG-compressed entries (Windows Vista and later)."""
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = len(header) + 16 * len(images)
    entries, data = b"", b""
    for size, image in images:
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(image), offset + len(data))
        data += image
    return header + entries + data


def main():
    os.makedirs(ASSETS, exist_ok=True)
    faces = cube_faces()
    write_svg(faces, os.path.join(ASSETS, "upsteper.svg"))
    images = [(size, png(rasterise(faces, size, 8 if size <= 64 else 4), size)) for size in ICO_SIZES]
    with open(os.path.join(ASSETS, "upsteper.png"), "wb") as f:
        f.write(dict(images)[256])
    with open(os.path.join(ASSETS, "upsteper.ico"), "wb") as f:
        f.write(ico(images))
    print(f"Wrote upsteper.svg, upsteper.png and upsteper.ico ({', '.join(map(str, ICO_SIZES))} px) to {os.path.normpath(ASSETS)}")


if __name__ == "__main__":
    main()
