"""Builds the mod icon: Hornet mid-recoil in Moss Grotto, flanked by Lace and Garmond."""
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
from collections import deque
import os, math

SRC = "/Users/faaris/.claude/image-cache/89f522c6-db46-47d8-9319-87d03757ea11"
S = 256

def load(n): return Image.open(os.path.join(SRC, f"{n}.png")).convert("RGBA")

def cut_background(img, tol=232):
    """
    Removes the flat backing by flooding inwards from the border.

    A blanket "delete every near-white pixel" is the obvious version and it quietly
    destroys the subject: Hornet's mask is white, so erasing white by colour punched a
    hole straight through her head and left her reading as a dark blob. Flooding from
    the edges only ever reaches background that is connected to the border, so enclosed
    white - her mask, Lace's face - survives untouched.
    """
    img = img.copy()
    px = img.load()
    w, h = img.size

    def is_bg(x, y):
        r, g, b, a = px[x, y]
        return a > 0 and r >= tol and g >= tol and b >= tol

    seen = bytearray(w * h)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if is_bg(x, y) and not seen[y * w + x]:
                seen[y * w + x] = 1; q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if is_bg(x, y) and not seen[y * w + x]:
                seen[y * w + x] = 1; q.append((x, y))

    while q:
        x, y = q.popleft()
        r, g, b, _ = px[x, y]
        px[x, y] = (r, g, b, 0)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx] and is_bg(nx, ny):
                seen[ny * w + nx] = 1; q.append((nx, ny))

    return img.crop(img.getbbox() or (0, 0, w, h))

def crop_alpha(img):
    """For art that already ships transparent - just tighten the frame."""
    return img.crop(img.getbbox() or (0, 0, *img.size))

def fit_h(img, th):
    w, h = img.size
    return img.resize((max(1, round(w * th / h)), th), Image.LANCZOS)

def rim(img, colour=(255, 255, 255), width=2, alpha=170):
    a = img.split()[3]
    ring = Image.new("RGBA", img.size, colour + (0,))
    ring.putalpha(a.filter(ImageFilter.MaxFilter(width * 2 + 1)).point(lambda v: int(v * alpha / 255)))
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.alpha_composite(ring.filter(ImageFilter.GaussianBlur(1.0)))
    out.alpha_composite(img)
    return out

def shadow(img, blur=7, alpha=140):
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sh.putalpha(img.split()[3].point(lambda v: min(alpha, v)))
    return sh.filter(ImageFilter.GaussianBlur(blur))

# --- background: sharp and clearly legible. No blur, only a light dim so the
#     figures still read on top of it. ---
bg = load(3)
w, h = bg.size
side = min(w, h)
bg = bg.crop(((w - side)//2, (h - side)//2, (w + side)//2, (h + side)//2)).resize((S, S), Image.LANCZOS)
bg = ImageEnhance.Brightness(bg).enhance(0.88)
bg = ImageEnhance.Color(bg).enhance(1.15)
bg = ImageEnhance.Contrast(bg).enhance(1.08)

canvas = Image.new("RGBA", (S, S))
canvas.paste(bg, (0, 0))

# Gentle corner falloff only - not enough to hide the grotto.
vig = Image.new("L", (S, S), 0)
ImageDraw.Draw(vig).ellipse((-S*0.18, -S*0.18, S*1.18, S*1.18), fill=235)
canvas = Image.composite(canvas, Image.new("RGBA", (S, S), (10, 26, 16, 255)),
                         vig.filter(ImageFilter.GaussianBlur(34)))

HX, HY = 132, 122
flare = Image.new("RGBA", (S, S), (0, 0, 0, 0))
fd = ImageDraw.Draw(flare)
for rad, al in ((70, 34), (50, 54), (32, 84), (18, 118)):
    fd.ellipse((HX-rad, HY-rad, HX+rad, HY+rad), fill=(226, 46, 52, al))
canvas = Image.alpha_composite(canvas, flare.filter(ImageFilter.GaussianBlur(16)))

# --- flankers: already transparent, so used as they come ---
lace = fit_h(crop_alpha(load(4)), 92)
canvas.alpha_composite(shadow(lace), (10, 131)); canvas.alpha_composite(lace, (10, 128))

g = rim(fit_h(crop_alpha(load(5)), 92).transpose(Image.FLIP_LEFT_RIGHT), width=1, alpha=130)
gx = S - g.width - 10
canvas.alpha_composite(shadow(g), (gx, 133)); canvas.alpha_composite(g, (gx, 130))

# --- Hornet: background flooded away, white mask intact ---
hn = rim(fit_h(cut_background(load(2)), 132), width=2, alpha=185)
canvas.alpha_composite(hn, (round(HX - hn.width * 0.63), HY - 52))

d = ImageDraw.Draw(canvas)
for ang, r0, r1 in ((208, 84, 106), (250, 80, 100), (292, 80, 100),
                    (334, 84, 106), (168, 86, 106), (12, 86, 106)):
    a = math.radians(ang)
    d.line((HX + r0*math.cos(a), HY + r0*math.sin(a), HX + r1*math.cos(a), HY + r1*math.sin(a)),
           fill=(255, 116, 104, 245), width=6)

canvas.convert("RGB").save("package/icon.png", "PNG")
print("wrote package/icon.png")
