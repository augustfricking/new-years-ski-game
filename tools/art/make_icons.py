"""Home-screen icons for the web build (phones: Add to Home Screen opens the game without the browser's bars).

Writes icon-512.png, icon-192.png and icon-180.png (Apple) into the WebGL template, which Unity copies into
every build. The look follows the loading screen: "LAST RUN" in Bangers, tilted, on the night colour.
Run: python tools/art/make_icons.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONT = os.path.join(ROOT, "SkiGame", "Assets", "_Game", "Resources", "Fonts", "Bangers-Regular.ttf")
OUT = os.path.join(ROOT, "SkiGame", "Assets", "WebGLTemplates", "LastRun")

NIGHT = (27, 20, 48)
LAMP = (255, 198, 74)
SNOW = (245, 235, 216)


def icon(size):
    big = 1024   # drawn large, then scaled down
    img = Image.new("RGB", (big, big), NIGHT)
    d = ImageDraw.Draw(img)
    # A snowy slope across the bottom.
    d.polygon([(0, big * 0.78), (big, big * 0.62), (big, big), (0, big)], fill=SNOW)
    text = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    t = ImageDraw.Draw(text)
    font = ImageFont.truetype(FONT, int(big * 0.34))
    for i, (word, y) in enumerate((("LAST", 0.13), ("RUN", 0.42))):
        w = t.textlength(word, font=font)
        x = (big - w) / 2
        t.text((x + big * 0.022, big * y + big * 0.022), word, font=font, fill=(0, 0, 0, 255))
        t.text((x, big * y), word, font=font, fill=LAMP + (255,))
    text = text.rotate(4, resample=Image.BICUBIC, center=(big / 2, big / 2))
    img.paste(text, (0, 0), text)
    return img.resize((size, size), Image.LANCZOS)


for s in (512, 192, 180):
    path = os.path.join(OUT, "icon-%d.png" % s)
    icon(s).save(path, optimize=True)
    print(path)
