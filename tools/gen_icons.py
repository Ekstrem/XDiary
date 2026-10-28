"""Генерирует PNG-иконки PWA для XDiary (без внешних зависимостей)."""
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "icons")
os.makedirs(OUT, exist_ok=True)

BG = (18, 16, 26, 255)       # #12101a
HEART = (224, 86, 122, 255)  # #e0567a
PULSE = (255, 255, 255, 255)


def draw_heart(d, cx, cy, w, color):
    """Сердце: два круга + треугольник. w — половина ширины сердца."""
    r = w * 0.58
    y1 = cy - r * 0.45
    d.ellipse([cx - w * 0.48 - r, y1 - r, cx - w * 0.48 + r, y1 + r], fill=color)
    d.ellipse([cx + w * 0.48 - r, y1 - r, cx + w * 0.48 + r, y1 + r], fill=color)
    d.polygon(
        [(cx - w * 0.98, y1 + r * 0.35), (cx + w * 0.98, y1 + r * 0.35), (cx, cy + w * 0.92)],
        fill=color,
    )


def draw_pulse(d, cx, cy, w, color, width):
    pts = [
        (cx - w / 2, cy),
        (cx - w / 4, cy),
        (cx - w / 5, cy - w * 0.07),
        (cx - w / 12, cy + w * 0.07),
        (cx, cy),
        (cx + w / 12, cy - w * 0.09),
        (cx + w / 5, cy),
        (cx + w / 2, cy),
    ]
    d.line(pts, fill=color, width=width, joint="curve")


def make_icon(size, maskable=False):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if maskable:
        d.rectangle([0, 0, size, size], fill=BG)
        pad = int(size * 0.10)
    else:
        pad = int(size * 0.05)
        d.rounded_rectangle([pad, pad, size - pad, size - pad], radius=int(size * 0.20), fill=BG)

    inner = size - 2 * pad
    cx = size / 2
    heart_w = inner * 0.30
    heart_cy = size / 2 - inner * 0.04
    draw_heart(d, cx, heart_cy, heart_w, HEART)

    pulse_y = size / 2 + inner * 0.34
    draw_pulse(d, cx, pulse_y, inner * 0.66, PULSE, max(2, int(size * 0.016)))
    return img


make_icon(192).save(os.path.join(OUT, "icon-192.png"))
make_icon(512).save(os.path.join(OUT, "icon-512.png"))
make_icon(512, maskable=True).save(os.path.join(OUT, "icon-maskable-512.png"))
make_icon(180).save(os.path.join(OUT, "icon-180.png"))
print("OK ->", OUT)
