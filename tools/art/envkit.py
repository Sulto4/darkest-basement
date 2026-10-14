"""envkit — uneltele comune pentru mediu, recuzită și UI (Pillow + numpy).

Totul e determinist: fiecare generator primește un seed fix, așa că rularea din nou
scrie exact aceiași pixeli. Pixelii se pun unul câte unul pe grilă, fără anti-aliasing.
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import palette as P  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
RES = os.path.join(ROOT, "Assets", "_Game", "Resources")
ENV_DIR = os.path.join(RES, "Textures", "Env")
PROPS_DIR = os.path.join(RES, "Sprites", "Props")
UI_DIR = os.path.join(RES, "Sprites", "UI")
FONT_DIR = os.path.join(RES, "Fonts")
REVIEW_DIR = os.path.join(ROOT, "docs", "art-review")

OUTLINE = P.OUTLINE
CLEAR = (0, 0, 0, 0)


# ---------------------------------------------------------------- culori

def C(c, a=None):
    """Normalizează o culoare: '#rrggbb', (r,g,b) sau (r,g,b,a) -> (r,g,b,a)."""
    if c is None:
        return CLEAR
    if isinstance(c, str):
        return P.hex_to_rgba(c, 255 if a is None else a)
    if len(c) == 3:
        return (c[0], c[1], c[2], 255 if a is None else a)
    return tuple(c) if a is None else (c[0], c[1], c[2], a)


def mix(c1, c2, t):
    """Nuanță între două culori din paletă (folosită doar ca să derivăm tonuri apropiate)."""
    a, b = C(c1), C(c2)
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (255,)


def hexc(c):
    c = C(c)
    return "#%02x%02x%02x" % c[:3]


# ---------------------------------------------------------------- pânza

class Canvas:
    """Pânză RGBA. Cu wrap=True coordonatele se înfășoară, ca texturile să se lege fără cusături."""

    def __init__(self, w, h, wrap=False, bg=None):
        self.w, self.h, self.wrap = w, h, wrap
        self.a = np.zeros((h, w, 4), np.uint8)
        if bg is not None:
            self.a[:, :] = C(bg)

    # -- acces
    def _xy(self, x, y):
        x, y = int(x), int(y)
        if self.wrap:
            return x % self.w, y % self.h
        if 0 <= x < self.w and 0 <= y < self.h:
            return x, y
        return None

    def put(self, x, y, c):
        p = self._xy(x, y)
        if p:
            self.a[p[1], p[0]] = C(c)

    def get(self, x, y):
        p = self._xy(x, y)
        if not p:
            return CLEAR
        return tuple(int(v) for v in self.a[p[1], p[0]])

    def opaque(self, x, y):
        return self.get(x, y)[3] > 0

    def fill(self, pts, c):
        c = C(c)
        for x, y in pts:
            self.put(x, y, c)

    def rect(self, x0, y0, x1, y1, c):
        """Dreptunghi plin, coordonate inclusive."""
        self.fill(pts_rect(x0, y0, x1, y1), c)

    def frame(self, x0, y0, x1, y1, c):
        self.fill(pts_rect_border(x0, y0, x1, y1), c)

    def line(self, x0, y0, x1, y1, c):
        self.fill(pts_line(x0, y0, x1, y1), c)

    def clear(self, pts):
        for x, y in pts:
            p = self._xy(x, y)
            if p:
                self.a[p[1], p[0]] = CLEAR

    def blit(self, other, x, y, only_opaque=True):
        """Pune altă pânză peste asta (alfa binar: pixel opac înlocuiește)."""
        src = other.a if isinstance(other, Canvas) else np.array(other.convert("RGBA"))
        h, w = src.shape[:2]
        for yy in range(h):
            for xx in range(w):
                px = src[yy, xx]
                if px[3] == 0 and only_opaque:
                    continue
                self.put(x + xx, y + yy, tuple(int(v) for v in px))

    def mask(self):
        return set((int(x), int(y)) for y, x in zip(*np.nonzero(self.a[:, :, 3])))

    def copy(self):
        c = Canvas(self.w, self.h, self.wrap)
        c.a = self.a.copy()
        return c

    def image(self):
        return Image.fromarray(self.a, "RGBA")

    def save(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.image().save(path)
        return path

    def recolor(self, mapping):
        """Înlocuiește culori exacte (folosit la variante: același desen, altă paletă)."""
        m = {C(k)[:3]: C(v) for k, v in mapping.items()}
        for y in range(self.h):
            for x in range(self.w):
                px = tuple(int(v) for v in self.a[y, x])
                if px[3] and px[:3] in m:
                    self.a[y, x] = m[px[:3]]

    def outline(self, color=OUTLINE, diagonal=False, pts=None):
        """Contur de 1 px pe exteriorul pixelilor opaci (doar pe pixelii transparenți vecini)."""
        src = self.a[:, :, 3] > 0
        add = []
        for y in range(self.h):
            for x in range(self.w):
                if src[y, x]:
                    continue
                nb = [(1, 0), (-1, 0), (0, 1), (0, -1)]
                if diagonal:
                    nb += [(1, 1), (-1, -1), (1, -1), (-1, 1)]
                for dx, dy in nb:
                    xx, yy = x + dx, y + dy
                    if self.wrap:
                        xx %= self.w
                        yy %= self.h
                    if 0 <= xx < self.w and 0 <= yy < self.h and src[yy, xx]:
                        add.append((x, y))
                        break
        for x, y in add:
            if pts is None or (x, y) in pts:
                self.put(x, y, color)


# ---------------------------------------------------------------- forme (seturi de puncte)

def pts_rect(x0, y0, x1, y1):
    return [(x, y) for y in range(int(y0), int(y1) + 1) for x in range(int(x0), int(x1) + 1)]


def pts_rect_border(x0, y0, x1, y1):
    s = []
    for x in range(x0, x1 + 1):
        s += [(x, y0), (x, y1)]
    for y in range(y0 + 1, y1):
        s += [(x0, y), (x1, y)]
    return s


def pts_line(x0, y0, x1, y1):
    """Bresenham: linie fără anti-aliasing."""
    x0, y0, x1, y1 = int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy
    return pts


def pts_polyline(points):
    out = []
    for a, b in zip(points, points[1:]):
        out += pts_line(a[0], a[1], b[0], b[1])
    return out


def pts_ellipse(cx, cy, rx, ry):
    """Elipsă plină; centrul poate fi la jumătate de pixel (cx=7.5) pentru forme simetrice pare."""
    pts = []
    for y in range(int(math.floor(cy - ry)) - 1, int(math.ceil(cy + ry)) + 2):
        for x in range(int(math.floor(cx - rx)) - 1, int(math.ceil(cx + rx)) + 2):
            dx = (x - cx) / max(rx, 0.01)
            dy = (y - cy) / max(ry, 0.01)
            if dx * dx + dy * dy <= 1.0 + 1e-9:
                pts.append((x, y))
    return pts


def pts_poly(points, w=256, h=256, ox=0, oy=0):
    """Poligon plin (rasterizat de Pillow fără anti-aliasing)."""
    m = Image.new("1", (w, h), 0)
    ImageDraw.Draw(m).polygon([(x + ox, y + oy) for x, y in points], fill=1, outline=1)
    a = np.array(m)
    return [(int(x) - ox, int(y) - oy) for y, x in zip(*np.nonzero(a))]


def dilate(pts, r=1, diagonal=True):
    s = set(pts)
    out = set(s)
    for x, y in s:
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if not diagonal and abs(dx) + abs(dy) > r:
                    continue
                out.add((x + dx, y + dy))
    return out


def edge(pts, dirs):
    """Pixelii din set care au un vecin în afara setului pe una din direcții."""
    s = set(pts)
    return [p for p in s if any((p[0] + dx, p[1] + dy) not in s for dx, dy in dirs)]


TOP_LEFT = [(-1, 0), (0, -1)]
BOTTOM_RIGHT = [(1, 0), (0, 1)]
TOP = [(0, -1)]
BOTTOM = [(0, 1)]
LEFT = [(-1, 0)]
RIGHT = [(1, 0)]


# ---------------------------------------------------------------- umbrire (lumina din stânga-sus)

BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def quant(v, n, x=0, y=0, dither=0.0):
    """Valoare 0..1 -> index de ton 0..n-1, cu dither ordonat opțional (doar la granițe)."""
    v = v + (BAYER4[y % 4, x % 4] - 0.5) * dither
    return max(0, min(n - 1, int(v * n)))


def shade_bevel(cv, pts, ramp, hi=True, lo=True, deep=False):
    """Umbrire tip „cărămidă”: bază, muchia stânga-sus luminată, dreapta-jos în umbră.
    ramp = [umbră adâncă, umbră, bază, lumină] (de la închis la deschis)."""
    s = set(pts)
    base = ramp[2] if len(ramp) > 2 else ramp[-1]
    cv.fill(s, base)
    if lo:
        cv.fill(edge(s, BOTTOM_RIGHT), ramp[1])
    if deep:
        br = [(x, y) for x, y in s if (x + 1, y + 1) not in s and ((x + 1, y) not in s or (x, y + 1) not in s)]
        cv.fill(br, ramp[0])
    if hi:
        cv.fill([p for p in edge(s, TOP_LEFT) if p not in edge(s, BOTTOM_RIGHT)], ramp[-1])


def shade_sphere(cv, pts, ramp, cx, cy, rx, ry, dither=0.25, light=(-0.55, -0.65, 0.52), spec=None):
    """Umbrire de volum rotund: normală pe elipsoid, lumină din stânga-sus, cuantizată pe rampă."""
    L = np.array(light, float)
    L /= np.linalg.norm(L)
    for x, y in pts:
        u = (x - cx) / max(rx, 0.5)
        v = (y - cy) / max(ry, 0.5)
        d = u * u + v * v
        z = math.sqrt(max(0.0, 1.0 - min(d, 1.0)))
        n = np.array([u, v, z])
        n /= np.linalg.norm(n) + 1e-9
        i = max(0.0, float(n @ L))
        i = 0.15 + 0.85 * i
        cv.put(x, y, ramp[quant(i, len(ramp), x, y, dither)])
    if spec:
        for p in spec:
            cv.put(p[0], p[1], ramp[-1] if len(p) < 3 else p[2])


def speckle(cv, rng, pts, colors, density):
    """Firimituri de murdărie: câțiva pixeli izolați, puși determinist."""
    pts = list(pts)
    for p in pts:
        if rng.random() < density:
            cv.put(p[0], p[1], rng.choice(colors))


# ---------------------------------------------------------------- zgomot tileable

def tile_noise(w, h, cells, seed, octaves=2, persistence=0.5):
    """Value noise care se înfășoară perfect (grila de valori e periodică)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w))
    amp, total = 1.0, 0.0
    c = cells
    for _ in range(octaves):
        g = rng.random((c, c))
        ys = np.arange(h) * c / h
        xs = np.arange(w) * c / w
        y0 = np.floor(ys).astype(int)
        x0 = np.floor(xs).astype(int)
        fy = ys - y0
        fx = xs - x0
        fy = fy * fy * (3 - 2 * fy)
        fx = fx * fx * (3 - 2 * fx)
        y1 = (y0 + 1) % c
        x1 = (x0 + 1) % c
        a = g[np.ix_(y0, x0)]
        b = g[np.ix_(y0, x1)]
        cc = g[np.ix_(y1, x0)]
        d = g[np.ix_(y1, x1)]
        top = a + (b - a) * fx[None, :]
        bot = cc + (d - cc) * fx[None, :]
        out += amp * (top + (bot - top) * fy[:, None])
        total += amp
        amp *= persistence
        c *= 2
    out /= total
    out = (out - out.min()) / (out.max() - out.min() + 1e-9)
    return out


def tile_voronoi(w, h, n, seed, min_dist=0, jitter_scale=1.0):
    """Celule Voronoi periodice (pietre). Întoarce (index celulă, F2-F1, puncte)."""
    rng = random.Random(seed)
    pts = []
    tries = 0
    while len(pts) < n and tries < 5000:
        tries += 1
        p = (rng.uniform(0, w), rng.uniform(0, h))
        ok = True
        for q in pts:
            dx = min(abs(p[0] - q[0]), w - abs(p[0] - q[0]))
            dy = min(abs(p[1] - q[1]), h - abs(p[1] - q[1]))
            if dx * dx + dy * dy < min_dist * min_dist:
                ok = False
                break
        if ok:
            pts.append(p)
    ys, xs = np.mgrid[0:h, 0:w]
    xs = xs + 0.5
    ys = ys + 0.5
    d = np.full((len(pts), h, w), 1e9)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xs - px)
        dx = np.minimum(dx, w - dx)
        dy = np.abs(ys - py) * jitter_scale
        dy = np.minimum(dy, h * jitter_scale - dy)
        d[i] = np.sqrt(dx * dx + dy * dy)
    order = np.argsort(d, axis=0)
    f1 = np.take_along_axis(d, order[0:1], axis=0)[0]
    f2 = np.take_along_axis(d, order[1:2], axis=0)[0]
    return order[0], f2 - f1, pts


def cells_to_pts(idx):
    """Index Voronoi -> listă de seturi de pixeli pe celulă."""
    out = {}
    h, w = idx.shape
    for y in range(h):
        for x in range(w):
            out.setdefault(int(idx[y, x]), []).append((x, y))
    return out


def wrapped_edge(pts, w, h, dirs):
    """Ca edge(), dar vecinii se caută înfășurat (pentru celule care trec peste marginea texturii)."""
    s = set(pts)
    return [p for p in s if any(((p[0] + dx) % w, (p[1] + dy) % h) not in s for dx, dy in dirs)]


# ---------------------------------------------------------------- scalare, planșe, text

def upscale(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def tile_grid(img, nx=3, ny=3):
    out = Image.new("RGBA", (img.width * nx, img.height * ny))
    for j in range(ny):
        for i in range(nx):
            out.paste(img, (i * img.width, j * img.height))
    return out


_FONT_CACHE = {}

# Fonturile pixel și mărimea la care fiecare pixel cade pe grilă (măsurat în tools/art/fonts.md).
FONT_FILES = {
    "silk": ("Silkscreen/Silkscreen-Regular.ttf", 8),
    "silkb": ("Silkscreen/Silkscreen-Bold.ttf", 8),
    "micro": ("Micro5/Micro5-Regular.ttf", 11),
    "pixelify": ("PixelifySans/PixelifySans-Variable.ttf", 20),
    "gothic": ("JacquardaBastarda9/JacquardaBastarda9-Regular.ttf", 13),
}


def font(key, size=None):
    path, native = FONT_FILES[key]
    size = size or native
    k = (key, size)
    if k not in _FONT_CACHE:
        _FONT_CACHE[k] = ImageFont.truetype(os.path.join(FONT_DIR, path), size)
    return _FONT_CACHE[k]


def text_mask(s, fnt):
    """Text randat fără anti-aliasing într-o mască 1-bit, decupată strâns."""
    tmp = Image.new("L", (max(8, len(s) * fnt.size * 2), fnt.size * 3), 0)
    d = ImageDraw.Draw(tmp)
    d.fontmode = "1"
    d.text((2, fnt.size // 2), s, font=fnt, fill=255)
    bb = tmp.getbbox()
    if not bb:
        return Image.new("L", (1, 1), 0), (0, 0)
    asc_top = fnt.size // 2 + fnt.getbbox("A")[1]
    return tmp.crop((bb[0], asc_top, bb[2], bb[3])), (0, 0)


def draw_text(img, xy, s, fnt, color, shadow=None, outline=None, anchor="lt"):
    """Scrie text pixel-perfect pe o imagine PIL. anchor: lt (stânga-sus) sau mt (centru-sus)."""
    m, _ = text_mask(s, fnt)
    x, y = xy
    if anchor == "mt":
        x -= m.width // 2
    elif anchor == "rt":
        x -= m.width
    if outline is not None:
        for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1)]:
            img.paste(Image.new("RGBA", m.size, C(outline)), (x + dx, y + dy), m)
    if shadow is not None:
        img.paste(Image.new("RGBA", m.size, C(shadow)), (x + 1, y + 1), m)
    img.paste(Image.new("RGBA", m.size, C(color)), (x, y), m)
    return m.size


def text_size(s, fnt):
    m, _ = text_mask(s, fnt)
    return m.size


def wrap_text(s, fnt, max_w):
    words, lines, cur = s.split(), [], ""
    for wd in words:
        t = (cur + " " + wd).strip()
        if text_size(t, fnt)[0] <= max_w or not cur:
            cur = t
        else:
            lines.append(cur)
            cur = wd
    if cur:
        lines.append(cur)
    return lines


class Sheet:
    """Planșă de review: rânduri de imagini cu etichete, pe fundal închis."""

    def __init__(self, title, bg="#16100e", label_color="#e8dcc4", pad=16):
        self.title = title
        self.rows = []
        self.bg = bg
        self.label_color = label_color
        self.pad = pad

    def row(self, items, heading=None):
        """items = [(PIL.Image, etichetă)]; imaginile sunt deja scalate."""
        self.rows.append((heading, items))

    def render(self, max_w=None):
        lab = font("silk")
        LS = 2  # etichetele se scriu ×2 (Silkscreen 8 px)
        pad = self.pad

        def cell_w(im, label):
            lw = (text_size(label, lab)[0] * LS) if label else 0
            return max(im.width, lw)

        widths, heights = [], []
        for heading, items in self.rows:
            w = pad + sum(cell_w(im, l) + pad for im, l in items)
            h = max(im.height for im, _ in items) + 8 * LS + 10
            if heading:
                h += 10 * LS + 8
            widths.append(w)
            heights.append(h + pad)
        W = max(widths + [400]) if max_w is None else max_w
        H = 14 * LS + 2 * pad + sum(heights)
        out = Image.new("RGBA", (W, H), C(self.bg))
        small = Image.new("RGBA", (W // LS + 1, 12), CLEAR)
        draw_text(small, (pad // LS, 2), self.title.upper(), font("silkb"), self.label_color)
        out.alpha_composite(upscale(small, LS), (0, pad // 2))
        y = 14 * LS + pad
        for (heading, items), h in zip(self.rows, heights):
            if heading:
                hs = Image.new("RGBA", (W // LS + 1, 12), CLEAR)
                draw_text(hs, (pad // LS, 1), heading.upper(), font("silkb"), "#e0b04a")
                out.alpha_composite(upscale(hs, LS), (0, y))
                y += 10 * LS + 8
            x = pad
            for im, label in items:
                out.alpha_composite(im, (x, y))
                if label:
                    ls = Image.new("RGBA", (text_size(label, lab)[0] + 4, 10), CLEAR)
                    draw_text(ls, (0, 1), label, lab, self.label_color)
                    out.alpha_composite(upscale(ls, LS), (x, y + im.height + 4))
                x += cell_w(im, label) + pad
            y += h - (10 * LS + 8 if heading else 0)
        return out


def checker(w, h, a="#2a2226", b="#231c20", cell=4):
    """Fundal tablă de șah ca să se vadă transparența pe planșe."""
    im = Image.new("RGBA", (w, h), C(a))
    d = ImageDraw.Draw(im)
    for y in range(0, h, cell):
        for x in range(0, w, cell):
            if (x // cell + y // cell) % 2:
                d.rectangle((x, y, x + cell - 1, y + cell - 1), fill=C(b))
    return im


def on_checker(img, k, cell=None):
    big = upscale(img, k)
    bg = checker(big.width, big.height, cell=cell or 2 * k)
    bg.alpha_composite(big)
    return bg


def seam_score(img):
    """Cât de diferite sunt marginile opuse (0 = tile perfect). Doar ca verificare numerică."""
    a = np.array(img.convert("RGB")).astype(int)
    lr = np.abs(a[:, 0] - a[:, -1]).sum(axis=1).mean()
    tb = np.abs(a[0, :] - a[-1, :]).sum(axis=1).mean()
    inner_lr = np.abs(a[:, 1:] - a[:, :-1]).sum(axis=2).mean()
    inner_tb = np.abs(a[1:, :] - a[:-1, :]).sum(axis=2).mean()
    return lr / (inner_lr + 1e-9), tb / (inner_tb + 1e-9)
