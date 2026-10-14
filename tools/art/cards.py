"""cards.py — ilustrațiile celor 40 de cărți (56×36), în stilul The Binding of Isaac: Rebirth.

Rulează din rădăcina repo-ului:  python3 tools/art/cards.py
Scrie:
  Assets/_Game/Resources/Sprites/Cards/<cardId>.png   56×36, opac, pixel art pe paletă
  docs/art-review/cards.png                           toate cele 40, ×6, cu id + erou + raritate
  docs/art-review/cards-framed.png                    8 cărți în ramele eroilor, ×4 (doar dacă ui.py a scris ramele)

Lista cărților (id, erou, raritate, cost) se citește din Databases.cs, ca să nu existe două surse de adevăr.

Cum e făcută o ilustrație:
  1. fundalul: un „spot” de lumină în nuanțele ramei eroului (benzi cu dither Bayer, colțuri închise),
     ca subiectul să iasă în față;
  2. subiectul, din straturi: fiecare strat primește conturul lui de 1 px, apoi straturile se suprapun
     (așa apare linia închisă dintre piese, ca în Isaac);
  3. efectele fără contur (dâre de mișcare, scântei). Cărțile Rare primesc raze în fundal și o
     strălucire discretă în jurul subiectului: raritatea se vede din compoziție, nu doar din bijuterie.
Totul e determinist: aceeași comandă dă aceiași octeți.
"""
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

import palette as P  # noqa: E402
import pixkit as pk  # noqa: E402
from pixkit import Canvas, mix  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CARDS_DIR = os.path.join(ROOT, "Assets", "_Game", "Resources", "Sprites", "Cards")
UI_DIR = os.path.join(ROOT, "Assets", "_Game", "Resources", "Sprites", "UI")
REVIEW_DIR = os.path.join(ROOT, "docs", "art-review")
DB_PATH = os.path.join(ROOT, "Assets", "_Game", "Scripts", "Model", "Data", "Databases.cs")

W, H = 56, 36
OUT = P.OUTLINE
WHITE = "#ffffff"
INK = P.UI["ink"]

# ───────────────────────────── rampe [lumină, bază, umbră, umbră adâncă] ─────────────────────────────
# Toate pornesc din paleta din ART.md; culorile în plus sunt doar amestecuri ale ei.

SKIN = list(P.SKIN)
TEAR4 = [P.TEAR[0], P.TEAR[1], P.TEAR[2], mix(P.TEAR[2], OUT, 0.45)]
BLOOD4 = [mix("#d33c3c", "#fff0e8", 0.45), "#d33c3c", "#9e1b1b", "#5c0d0d"]
BEAM4 = [mix("#d33c3c", "#fff0e8", 0.75), mix("#d33c3c", "#fff0e8", 0.38), "#d33c3c", "#9e1b1b"]  # miez → margine
DEMON4 = ["#4b3b44", mix("#231c22", "#4b3b44", 0.35), "#231c22", "#151015"]
HORN4 = [mix("#4b3b44", "#fff0e8", 0.3), "#4b3b44", mix("#4b3b44", "#151015", 0.5), "#151015"]
IMP4 = [mix("#2a2030", "#6b3f7d", 0.55), mix("#2a2030", "#6b3f7d", 0.2), "#2a2030", mix("#2a2030", OUT, 0.5)]
BONE4 = ["#fff0e8", "#e8dcc4", "#c8b89a", mix("#c8b89a", "#4a3326", 0.45)]
GOLD4 = [mix("#e0b04a", "#fff0e8", 0.55), "#e0b04a", "#a8782a", mix("#a8782a", "#2a1b13", 0.5)]
STEEL4 = [mix("#7a7f86", "#ffffff", 0.55), "#7a7f86", "#5c6168", "#43474d"]
BOMB4 = [mix("#5c6168", "#7a7f86", 0.5), "#43474d", "#2f3236", mix("#2f3236", OUT, 0.5)]
POOP4 = list(P.POOP)
CARD4 = [mix("#a8743d", "#e8dcc4", 0.4), "#a8743d", "#8a5a2b", "#6b4423"]
GREEN4 = [mix("#5b6b3a", "#c9a227", 0.35), "#5b6b3a", "#43502a", mix("#43502a", OUT, 0.45)]
LEAF4 = [mix("#7cc04a", "#fff0e8", 0.4), "#7cc04a", mix("#7cc04a", "#43502a", 0.55), mix("#43502a", OUT, 0.3)]
POISON4 = [mix("#7cc04a", "#fff0e8", 0.5), "#7cc04a", mix("#7cc04a", OUT, 0.35), mix("#7cc04a", OUT, 0.6)]
PURPLE4 = [mix("#6b3f7d", "#fff0e8", 0.35), "#6b3f7d", "#3a2c45", "#241c2b"]
PTEAR4 = [mix("#6b3f7d", "#d6ecff", 0.65), mix("#6b3f7d", "#d6ecff", 0.32), "#6b3f7d", "#3a2c45"]
PINK4 = [mix("#e9cbbf", "#fff0e8", 0.5), mix("#f2d3c4", "#d33c3c", 0.22), mix("#dcae9c", "#d33c3c", 0.3),
         mix("#b9857a", "#9e1b1b", 0.4)]
MAG = mix("#d33c3c", "#6b3f7d", 0.45)
MAG4 = [mix(MAG, "#fff0e8", 0.45), MAG, mix(MAG, OUT, 0.3), mix(MAG, OUT, 0.55)]
EYE_RED = P.AZAZEL["eye"]
EYE_HI = mix(EYE_RED, "#fff0e8", 0.6)
STUN = P.STATUS["stun"]
MARK = P.STATUS["mark"]

# Fundalurile, de la colț (închis) la spot (deschis), în nuanța ramei eroului.
BG = {
    "azazel": ["#151015", mix("#151015", "#5c0d0d", 0.3), mix("#151015", "#5c0d0d", 0.58),
               mix("#151015", "#5c0d0d", 0.85)],
    "cain": [mix("#2a1b13", OUT, 0.4), "#2a1b13", mix("#2a1b13", "#43502a", 0.55),
             mix("#43502a", "#5b6b3a", 0.3)],
    "isaac": [mix("#4a7fbf", OUT, 0.8), mix("#4a7fbf", OUT, 0.68), mix("#4a7fbf", OUT, 0.55),
              mix("#4a7fbf", OUT, 0.42)],
    "lilith": [mix("#241c2b", OUT, 0.4), "#241c2b", "#3a2c45", mix("#3a2c45", "#6b3f7d", 0.35)],
}
HERO_ORDER = ["azazel", "cain", "isaac", "lilith"]
RARITY_NAMES = {"s": "starter", "c": "common", "u": "uncommon", "r": "rare"}

_L = pk.LIGHT
_LN = math.sqrt(sum(v * v for v in _L))
LX, LY, LZ = (v / _LN for v in _L)


# ───────────────────────────── forme (măști = mulțimi de pixeli) ─────────────────────────────

def E(cx, cy, rx, ry):
    """Elipsă după centru și raze (pot fi fracționare). Testăm centrul pixelului, fără anti-aliasing."""
    pts = set()
    for y in range(math.floor(cy - ry) - 1, math.ceil(cy + ry) + 1):
        for x in range(math.floor(cx - rx) - 1, math.ceil(cx + rx) + 1):
            u, v = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            if u * u + v * v <= 1.0:
                pts.add((x, y))
    return pts


def Erot(cx, cy, rx, ry, ang):
    """Elipsă rotită cu `ang` grade (pentru obiecte așezate pe diagonală)."""
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    r = max(rx, ry)
    pts = set()
    for y in range(math.floor(cy - r) - 1, math.ceil(cy + r) + 1):
        for x in range(math.floor(cx - r) - 1, math.ceil(cx + r) + 1):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            u, v = (dx * ca + dy * sa) / rx, (-dx * sa + dy * ca) / ry
            if u * u + v * v <= 1.0:
                pts.add((x, y))
    return pts


def ring(cx, cy, rx, ry, t):
    return E(cx, cy, rx, ry) - E(cx, cy, rx - t, ry - t)


def R(x0, y0, x1, y1):
    return pk.rect(x0, y0, x1, y1)


def rounded(x0, y0, x1, y1, r):
    """Dreptunghi cu colțuri rotunjite (r în pixeli)."""
    m = R(x0 + r, y0, x1 - r, y1) | R(x0, y0 + r, x1, y1 - r)
    for cx, cy in ((x0 + r, y0 + r), (x1 - r, y0 + r), (x0 + r, y1 - r), (x1 - r, y1 - r)):
        m |= E(cx + 0.5, cy + 0.5, r + 0.5, r + 0.5)
    return m


def poly(points):
    return pk.polygon(points)


def mirror(points, axis=28.0):
    """Oglindește puncte continue față de axa verticală x = axis (implicit mijlocul ilustrației)."""
    return [(2 * axis - x, y) for x, y in points]


def mirror_mask(m, axis=28.0):
    return {(int(round(2 * axis - 1 - x)), y) for x, y in m}


def local(ox, oy, ang, pts):
    """Puncte (u, v) dintr-un cadru rotit: u de-a lungul direcției `ang` (grade), v perpendicular."""
    a = math.radians(ang)
    ca, sa = math.cos(a), math.sin(a)
    return [(ox + u * ca - v * sa, oy + u * sa + v * ca) for u, v in pts]


def arc(cx, cy, rx, ry, a0, a1, n=24):
    """Puncte pe o elipsă între unghiurile a0..a1 (grade; 0 = dreapta, 90 = jos)."""
    return [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
             cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def bez(p0, p1, p2, n=12):
    """Curbă Bézier pătratică (pentru cozi, fitile, dâre curbe)."""
    out = []
    for i in range(n + 1):
        t = i / n
        a, b, c = (1 - t) ** 2, 2 * (1 - t) * t, t * t
        out.append((a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]))
    return out


def star_pts(cx, cy, ro, ri, n, rot=0.0, sy=1.0):
    pts = []
    for i in range(2 * n):
        r = ro if i % 2 == 0 else ri
        a = math.radians(rot - 90 + 180.0 * i / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a) * sy))
    return pts


def heart(cx, cy, w):
    """Inimă cu jumătatea de lățime w: doi lobi + un triunghi spre vârf."""
    r = w / 2.0
    return (E(cx - r, cy, r, r) | E(cx + r, cy, r, r)
            | poly([(cx - w + 0.4, cy + r * 0.3), (cx + w - 0.4, cy + r * 0.3), (cx, cy + w * 1.12)]))


def clip(m):
    return {(x, y) for x, y in m if 0 <= x < W and 0 <= y < H}


def grow(m, n=1, diagonal=True):
    for _ in range(n):
        m = m | pk.outer_edge(m, diagonal)
    return m


# ───────────────────────────── umbrire ─────────────────────────────

def _pick(i, ramp, cuts):
    return ramp[0] if i > cuts[0] else ramp[1] if i > cuts[1] else ramp[2] if i > cuts[2] else ramp[3]


def sphere(c, mask, ramp, cuts=(0.9, 0.42, 0.1), box=None, dither=0.0):
    pk.shade_sphere(c, mask, ramp, cuts=cuts, box=box, dither=dither)
    return mask


def flat(c, mask, ramp, shadow=(2, 2), rim=True):
    pk.shade_flat(c, mask, ramp, shadow=shadow, deep=(1, 1), light_rim=(-1, -1) if rim else None)
    return mask


def tube(c, pts, r0, r1=None, ramp=STEEL4, mode="solid", cuts=(0.85, 0.35, 0.05),
         bands=(0.3, 0.55, 0.8), dither=None):
    """Tub de-a lungul unei linii frânte, cu raza de la r0 la r1 (coarne, fitile, raze laser, cozi).

    mode="solid": umbrit ca un cilindru luminat din stânga-sus.
    mode="glow":  benzi de la miez (ramp[0]) spre margine (ramp[3]) — pentru raze și energie.
    dither=nivel: desenează doar o parte din pixeli (urme, imagini remanente).
    """
    r1 = r0 if r1 is None else r1
    segs = list(zip(pts, pts[1:]))
    lens = [math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in segs]
    total = sum(lens) or 1.0
    rm = max(r0, r1)
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    m = set()
    for y in range(math.floor(min(ys) - rm) - 1, math.ceil(max(ys) + rm) + 1):
        for x in range(math.floor(min(xs) - rm) - 1, math.ceil(max(xs) + rm) + 1):
            px, py = x + 0.5, y + 0.5
            best, acc = None, 0.0
            for (a, b), ln in zip(segs, lens):
                dx, dy = b[0] - a[0], b[1] - a[1]
                t = 0.0 if ln == 0 else max(0.0, min(1.0, ((px - a[0]) * dx + (py - a[1]) * dy) / (ln * ln)))
                qx, qy = a[0] + t * dx, a[1] + t * dy
                d = math.hypot(px - qx, py - qy)
                if best is None or d < best[0]:
                    best = (d, (acc + t * ln) / total, px - qx, py - qy)
                acc += ln
            d, tt, ox, oy = best
            r = r0 + (r1 - r0) * tt
            if d > r:
                continue
            if dither is not None and not pk.dither_on(x, y, dither):
                continue
            if mode == "glow":
                f = d / r
                col = ramp[0] if f < bands[0] else ramp[1] if f < bands[1] else ramp[2] if f < bands[2] else ramp[3]
            else:
                vx, vy = ox / r, oy / r
                z = math.sqrt(max(0.0, 1.0 - vx * vx - vy * vy))
                col = _pick(vx * LX + vy * LY + z * LZ, ramp, cuts)
            c.set(x, y, col)
            m.add((x, y))
    return m


def glow_ball(c, cx, cy, r, ramp, bands=(0.35, 0.6, 0.82)):
    """Glob de energie: miez deschis, margini închise (lumina vine din el, nu de la lampă)."""
    m = E(cx, cy, r, r)
    for x, y in m:
        f = math.hypot(x + 0.5 - cx + r * 0.12, y + 0.5 - cy + r * 0.12) / r
        c.set(x, y, ramp[0] if f < bands[0] else ramp[1] if f < bands[1] else ramp[2] if f < bands[2] else ramp[3])
    return m


def hbeam(c, x0, x1, cy, r, ramp, wobble=0.7, period=8.0, spiral=True):
    """Rază orizontală tip Brimstone: pulsează în grosime și are o spirală în banda din mijloc."""
    m = set()
    for x in range(x0, x1 + 1):
        rr = r + wobble * math.sin((x + 0.5) * 2 * math.pi / period)
        for y in range(math.floor(cy - rr) - 1, math.ceil(cy + rr) + 1):
            d = abs(y + 0.5 - cy) / rr
            if d > 1:
                continue
            k = 0 if d < 0.3 else 1 if d < 0.58 else 2 if d < 0.84 else 3
            if spiral and k == 1 and (x + (y if y + 0.5 < cy else -y)) % 6 < 2:
                k = 2
            c.set(x, y, ramp[k])
            m.add((x, y))
    return m


def band_shade(c, mask, ox, oy, ang, bands):
    """Umbrire în benzi paralele cu o direcție (lamă de cuțit): bands = [(v_max, culoare), ...]."""
    a = math.radians(ang)
    sa, ca = math.sin(a), math.cos(a)
    for x, y in mask:
        v = -(x + 0.5 - ox) * sa + (y + 0.5 - oy) * ca
        for vmax, col in bands:
            if v <= vmax:
                c.set(x, y, col)
                break


# ───────────────────────────── piese refolosite ─────────────────────────────

def glint(c, x, y, n=2, col=WHITE):
    c.set(x, y, col)
    if n >= 2:
        c.set(x + 1, y, col)
        c.set(x, y + 1, col)


def spark(c, x, y, col=STUN, arm=1, core=WHITE):
    """Scânteie în cruce: centru alb, brațe colorate."""
    c.set(x, y, core)
    for k in range(1, arm + 1):
        for dx, dy in pk.N4:
            c.set(x + dx * k, y + dy * k, col)


def tear(c, cx, cy, r, ramp=TEAR4, hi=WHITE):
    """Lacrima din Isaac: sferă cu un luciu alb în stânga-sus."""
    m = sphere(c, E(cx, cy, r, r), ramp, cuts=(0.86, 0.36, 0.04))
    gx, gy = math.floor(cx - r * 0.45), math.floor(cy - r * 0.5)
    glint(c, gx, gy, 2 if r >= 3.5 else 1, hi)
    return m


def red_eye(c, x, y, w=2, h=2, col=EYE_RED, hi=EYE_HI):
    c.fill(R(x, y, x + w - 1, y + h - 1), col)
    c.set(x, y, hi)


def fly(a, cx, cy):
    """Musculița din Basement: corp negru rotund, aripi gri deschis.

    Aripile și corpul sunt straturi separate: cu un singur contur comun, musca arăta ca o inimă.
    """
    wl = a.layer(glow=False)
    for s in (-1, 1):
        wl.fill(Erot(cx + s * 2.0, cy - 2.2, 1.8, 1.1, s * 35), STEEL4[0])
    bl = a.layer(glow=False)
    bl.fill(E(cx, cy, 2.1, 2.1), "#231c22")
    bl.set(math.floor(cx - 1), math.floor(cy - 1), "#4b3b44")


def clover(c, cx, cy, r=2.2, ramp=LEAF4, stem=True):
    """Trifoi cu patru foi (norocul lui Cain)."""
    m = set()
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        m |= E(cx + dx * r * 0.95, cy + dy * r * 0.95, r, r)
    sphere(c, m, ramp, cuts=(0.8, 0.3, 0.0))
    if stem:
        tube(c, [(cx + 0.5, cy + r), (cx + r * 0.9, cy + r * 2.6)], 0.6, 0.6, ramp)
    c.set(int(cx), int(cy), ramp[3])
    return m


def bomb(c, cx, cy, r):
    m = sphere(c, E(cx, cy, r, r), BOMB4, cuts=(0.9, 0.4, 0.02))
    glint(c, math.floor(cx - r * 0.5), math.floor(cy - r * 0.55), 2 if r > 4 else 1, STEEL4[0])
    return m


def cube(c, cx, cy, s, faces, pips, pip_col=INK):
    """Zar izometric: fața de sus, stânga, dreapta + punctele (u, v ∈ [0, 1]) pe fiecare față."""
    T, Rt, C, Lt = (cx, cy - s), (cx + s, cy - s / 2), (cx, cy), (cx - s, cy - s / 2)
    Lb, Rb, B = (cx - s, cy + s / 2), (cx + s, cy + s / 2), (cx, cy + s)
    quads = {"top": (T, Rt, Lt), "left": (Lt, C, Lb), "right": (C, Rt, B)}
    shapes = {"top": [T, Rt, C, Lt], "left": [Lt, C, B, Lb], "right": [C, Rt, Rb, B]}
    masks = {}
    for k, ramp in faces.items():
        m = poly(shapes[k])
        masks[k] = m
        c.fill(m, ramp[1])
        c.fill(pk.edge_band(m, -1, -1) - pk.edge_band(m, 1, 1), ramp[0])
    for k, uvs in pips.items():
        A, Bq, D = quads[k]
        for u, v in uvs:
            x = A[0] + u * (Bq[0] - A[0]) + v * (D[0] - A[0])
            y = A[1] + u * (Bq[1] - A[1]) + v * (D[1] - A[1])
            c.fill(E(x, y, 1.15, 1.0) & masks[k], pip_col)
    return masks


def baby_head(c, cx, cy, r, ramp=SKIN, eye=INK, cry=False):
    """Cap de bebeluș în stil Isaac: sferă mare, ochi mari, rotunzi și umezi, cu un luciu alb."""
    m = sphere(c, E(cx, cy, r, r * 0.92), ramp)
    ew = max(1.6, r * 0.28)
    ey = cy + r * 0.08
    for s in (-1, 1):
        ex = cx + s * r * 0.38
        c.fill(E(ex, ey, ew, ew * 1.15), eye)
        c.set(math.floor(ex - ew * 0.35), math.floor(ey - ew * 0.5), WHITE)
        if cry:
            c.fill(R(math.floor(ex), math.floor(ey + ew * 1.1), math.floor(ex), math.floor(cy + r * 0.8)), TEAR4[1])
    return m


# ───────────────────────────── ilustrația: fundal + straturi ─────────────────────────────

class Art:
    """O ilustrație: fundal în culoarea eroului + straturi cu contur + efecte.

    a.layer()  strat cu contur (și, la Rare, cu strălucire în jur)
    a.fx()     strat fără contur, sub straturile desenate după el (dâre, aure)
    a.top()    strat fără contur, peste tot (scântei)
    a.veil()   „sticlă”: amestecă ce e dedesubt cu o culoare (translucid, dar tot pe paletă);
               cu under=True e o aură în spatele subiectului
    """

    def __init__(self, card):
        self.card = card
        self.hero = card["hero"]
        self.rarity = card["rarity"]
        self.tones = BG[self.hero]
        self.spot = (28, 17)
        self.spot_r = (31, 21)
        self.layers = []
        self.veils = []
        self.post = []
        self.shadows = []
        self.glow_color = GOLD4[1]
        self.rays = self.rarity == "r"

    def layer(self, outline=True, glow=True):
        c = Canvas(W, H)
        self.layers.append([c, outline, glow])
        return c

    def fx(self):
        return self.layer(outline=False, glow=False)

    def top(self):
        c = Canvas(W, H)
        self.post.append(c)
        return c

    def shadow(self, cx, cy, rx, ry):
        self.shadows.append(E(cx, cy, rx, ry))

    def veil(self, mask, color, t, under=False):
        """under=True: aura în spatele subiectului; altfel „sticlă” peste el (scutul din Holy Mantle)."""
        self.veils.append((mask, color, t, under))

    def background(self):
        c = Canvas(W, H)
        sx, sy = self.spot
        rx, ry = self.spot_r
        for y in range(H):
            for x in range(W):
                d = math.hypot((x + 0.5 - sx) / rx, (y + 0.5 - sy) / ry)
                d += (pk.BAYER4[y % 4][x % 4] / 15.0 - 0.5) * 0.14
                lvl = 3 if d < 0.5 else 2 if d < 0.8 else 1 if d < 1.12 else 0
                if self.rays and lvl < 3 and d < 1.3:
                    # razele Rare: pene alternante cu un ton mai deschis (în dither spre margini)
                    ang = math.atan2(y + 0.5 - sy, x + 0.5 - sx)
                    if int((ang + math.pi) / (math.pi / 9)) % 2 == 0 and (d < 0.85 or pk.dither_on(x, y, 0.5)):
                        lvl += 1
                c.set(x, y, self.tones[lvl])
        for m in self.shadows:
            c.fill(clip(m), self.tones[0])
        return c

    def render(self):
        c = self.background()
        for L, ol, _ in self.layers:
            if ol:
                pk.outline(L)
        if self.rarity == "r":
            solid = set()
            for L, _, g in self.layers:
                if g:
                    solid |= L.mask()
            r1 = clip(pk.outer_edge(solid, True))
            r2 = clip(pk.outer_edge(solid | r1, False)) - r1
            for x, y in r1:
                c.set(x, y, mix(c.get(x, y), self.glow_color, 0.5))
            for x, y in r2:
                if pk.dither_on(x, y, 0.5):
                    c.set(x, y, mix(c.get(x, y), self.glow_color, 0.28))
        for under in (True, False):
            if not under:
                for L, _, _ in self.layers:
                    c.paste(L)
            for mask, col, t, u in self.veils:
                if u == under:
                    for x, y in clip(mask):
                        c.set(x, y, mix(c.get(x, y), col, t))
        for L in self.post:
            c.paste(L)
        return c


CARDS = {}


def card(cid):
    def deco(fn):
        CARDS[cid] = fn
        return fn
    return deco


# ═════════════════════════════════════ AZAZEL ═════════════════════════════════════

def azazel_head(a, cx, cy, r, mouth=True, ramp=DEMON4):
    """Capul lui Azazel din profil (privește spre dreapta): coarne mici, ochi roșii, gura deschisă."""
    hl = a.layer()
    for s in (-1, 1):
        x0 = cx + s * r * 0.42 + r * 0.12
        tube(hl, [(x0, cy - r * 0.55), (x0 + s * r * 0.14, cy - r * 1.0), (x0 + s * r * 0.36, cy - r * 1.28)],
             max(1.3, r * 0.24), 0.55, HORN4)
    L = a.layer()
    sphere(L, E(cx, cy, r, r * 0.92), ramp)
    ew = 3 if r >= 8 else 2
    for ex in (cx - r * 0.02, cx + r * 0.5):
        red_eye(L, math.floor(ex), math.floor(cy - r * 0.38), ew, ew)
    if mouth:
        mx, my = cx + r * 0.62, cy + r * 0.3
        mm = E(mx, my, r * 0.34, r * 0.32)
        L.fill(mm, "#5c0d0d")
        L.fill(E(mx + r * 0.05, my + r * 0.04, r * 0.2, r * 0.18), "#151015")
        L.set(math.floor(mx - r * 0.15), math.floor(my - r * 0.3), WHITE)
        L.set(math.floor(mx + r * 0.12), math.floor(my - r * 0.3), WHITE)
    return L


def drops(a, items, ramp=BLOOD4, glow=False):
    L = a.layer(glow=glow)
    for x, y, r in items:
        sphere(L, E(x, y, r, r * 1.1), ramp, cuts=(0.8, 0.3, 0.0))
    return L


@card("brimstone_swipe")
def brimstone_swipe(a):
    a.spot = (30, 20)
    ghost = a.fx()
    # imaginile remanente ale razei: „swipe” = raza mătură de sus în jos
    for ang, lvl in ((-26, 0.25), (-13, 0.5)):
        tube(ghost, local(16, 24, ang, [(0, 0), (46, 0)]), 2.0, 2.8,
             ["#d33c3c", "#9e1b1b", "#9e1b1b", "#5c0d0d"], mode="glow", dither=lvl)
    sw = a.fx()
    tube(sw, arc(16, 24, 41, 41, -28, 2, 20), 0.5, 1.5, [BLOOD4[0], BLOOD4[0], BLOOD4[1], BLOOD4[1]],
         mode="glow", bands=(0.5, 0.5, 0.75))
    azazel_head(a, 10, 21, 8.5)
    beam = a.layer()
    tube(beam, [(15, 24), (60, 27)], 2.8, 3.8, BEAM4, mode="glow")
    drops(a, [(33, 32.5, 1.2), (45, 17, 1.0), (51, 33, 1.0)])


@card("demon_wings")
def demon_wings(a):
    a.spot = (28, 17)
    left = [(26, 21), (15, 9), (4, 3), (1.5, 10), (5, 11.5), (2.5, 18), (8, 18.5), (6.5, 26), (12.5, 23.5),
            (14, 31), (19, 25.5), (25, 28)]
    wrist = (13.5, 8.5)
    tips = [(1.5, 10), (2.5, 18), (6.5, 26), (14, 31)]
    for pts, tp, wr in ((left, tips, wrist), (mirror(left), mirror(tips), mirror([wrist])[0])):
        L = a.layer()
        m = poly(pts)
        L.fill(m, DEMON4[1])
        L.fill(pk.edge_band(m, 0, 2), DEMON4[2])
        L.fill(pk.edge_band(m, 0, 1), DEMON4[3])
        for t in tp:
            tube(L, [wr, t], 0.7, 0.55, HORN4)
        tube(L, [pts[0], wr], 1.3, 1.0, HORN4)
        L.fill(E(wr[0], wr[1] - 1, 1.2, 1.2), HORN4[0])
    head = a.layer()
    sphere(head, E(28, 22, 7.5, 7), DEMON4)
    for s in (-1, 1):
        tube(head, [(28 + s * 4, 16.5), (28 + s * 5.5, 13)], 1.3, 0.5, HORN4)
    for ex in (24, 30):
        red_eye(head, ex, 20, 3, 3)
    head.fill(R(26, 26, 30, 26), "#5c0d0d")
    head.set(26, 26, WHITE)
    head.set(30, 26, WHITE)


@card("blood_beam")
def blood_beam(a):
    a.spot = (28, 15)
    spray = a.layer(glow=False)
    for x, y, r in ((7, 5, 1.0), (16, 3.5, 1.3), (29, 5, 1.0), (40, 3, 1.2), (51, 6, 1.0), (23, 7, 0.8)):
        sphere(spray, E(x, y, r, r), BLOOD4, cuts=(0.8, 0.3, 0.0))
    b = a.layer()
    hbeam(b, -2, 58, 15.5, 5.2, BEAM4)
    d = a.layer(glow=False)
    for x, ln in ((8, 6), (20, 10), (33, 5), (45, 11)):
        tube(d, [(x + 0.5, 20), (x + 0.5, 20 + ln)], 1.0, 0.8, BLOOD4)
        sphere(d, E(x + 0.5, 22 + ln, 1.4, 1.7), BLOOD4, cuts=(0.8, 0.3, 0.0))
    a.shadow(30, 34.5, 22, 1.6)


@card("goat_head")
def goat_head(a):
    a.spot = (28, 15)
    hl = a.layer()
    for s in (-1, 1):
        pts = [(28 + s * 5, 8), (28 + s * 11, 3.5), (28 + s * 18, 4), (28 + s * 22, 9), (28 + s * 21, 15),
               (28 + s * 17, 16.5)]
        tube(hl, pts, 3.1, 0.9, HORN4)
        for k in range(1, 5):   # inelele cornului
            x, y = pts[k]
            hl.set(int(x), int(y), HORN4[3])
    sk = a.layer()
    m = E(28, 13, 9, 8) | poly([(20.5, 15), (35.5, 15), (32.5, 30), (23.5, 30)]) | E(28, 29, 4.5, 3)
    sphere(sk, m, BONE4, box=(18, 5, 38, 33))
    for side in (False, True):
        sock = [(20.5, 12), (26.5, 14.5), (25.5, 18.5), (21, 17.5)]
        sock = mirror(sock) if side else sock
        sk.fill(poly(sock), INK)
    red_eye(sk, 23, 15, 2, 1)
    red_eye(sk, 31, 15, 2, 1)
    for x, y in ((26, 23), (26, 24), (29, 23), (29, 24)):
        sk.set(x, y, INK)
    for x in range(24, 32):
        sk.set(x, 30, INK if x % 2 else BONE4[0])
    sk.fill(pk.polyline([(31, 6), (29, 9), (30, 11)]), BONE4[3])
    a.shadow(28, 34, 11, 1.8)


@card("blood_bag")
def blood_bag(a):
    a.spot = (28, 16)
    tl = a.layer()
    tube(tl, [(28.5, 29), (28.5, 32), (33, 34), (58, 34)], 1.1, 1.1, BLOOD4)
    bag = a.layer()
    m = rounded(19, 6, 37, 28, 3)
    plastic = mix("#fff0e8", "#d33c3c", 0.12)
    bag.fill(m, plastic)
    liquid = {(x, y) for x, y in m if y >= 11}
    sphere(bag, liquid, BLOOD4, cuts=(0.95, 0.4, 0.1), box=(17, 6, 39, 30))
    bag.fill({(x, y) for x, y in liquid if y == 11}, BLOOD4[0])
    bag.fill(R(21, 8, 21, 24), WHITE)
    bag.fill(R(24, 15, 32, 21), BONE4[1])
    bag.fill(R(27, 16, 29, 20) | R(25, 17, 31, 19), BLOOD4[1])
    tab = a.layer()
    tab.fill(R(25, 2, 31, 6), plastic)
    tab.fill(R(27, 3, 29, 4), OUT)
    port = a.layer()
    port.fill(R(27, 28, 30, 30), plastic)
    t = a.top()
    for x, y in ((12, 10), (45, 9), (47, 19)):
        spark(t, x, y, BLOOD4[0], core="#fff0e8")


@card("pentagram")
def pentagram(a):
    a.spot = (28, 18)
    cx, cy, rr = 28, 18, 14.5
    aura = a.fx()
    p = Canvas(W, H)
    tube(p, arc(cx, cy, rr, rr, 0, 360, 64), 1.1, 1.1, BEAM4, mode="glow", bands=(0.3, 0.7, 0.9))
    tips = [(cx + (rr - 1) * math.cos(math.radians(-90 + 72 * k)), cy + (rr - 1) * math.sin(math.radians(-90 + 72 * k)))
            for k in range(5)]
    tube(p, [tips[(2 * k) % 5] for k in range(6)], 0.95, 0.95, BEAM4, mode="glow", bands=(0.35, 0.7, 0.9))
    lines = p.mask()
    halo = grow(lines, 2, False) - grow(lines, 1, False)
    for x, y in halo:
        if pk.dither_on(x, y, 0.5):
            aura.set(x, y, "#9e1b1b")
    a.layer().paste(p)
    t = a.top()
    for x, y in tips:
        spark(t, int(x), int(y), BLOOD4[0], core="#fff0e8")


@card("charged_brimstone")
def charged_brimstone(a):
    a.spot = (35, 18)
    fx = a.fx()
    cx, cy = 36, 18
    for k in range(9):
        ang = math.radians(-100 + k * 25)
        for rr in range(11, 17):
            if rr % 3 != 0:
                fx.set(int(cx + rr * math.cos(ang)), int(cy + rr * math.sin(ang)), BLOOD4[0] if rr < 14 else "#d33c3c")
    azazel_head(a, 10, 19, 9)
    link = a.layer()
    tube(link, [(17, 22), (30, 19)], 1.6, 2.4, BEAM4, mode="glow")
    orb = a.layer()
    glow_ball(orb, cx, cy + 0.5, 8, BEAM4)
    t = a.top()
    for x, y in ((47, 7), (49, 28), (28, 7)):
        spark(t, x, y, BLOOD4[0], core="#fff0e8")


@card("ceremonial_robes")
def ceremonial_robes(a):
    a.spot = (28, 15)
    robe = mix("#6b3f7d", "#9e1b1b", 0.55)
    R4 = [mix(robe, "#fff0e8", 0.3), robe, mix(robe, OUT, 0.35), mix(robe, OUT, 0.62)]
    a.shadow(28, 34.5, 17, 1.8)
    sl = a.layer()
    for s in (-1, 1):
        tube(sl, [(28 + s * 6, 16), (28 + s * 12, 24), (28 + s * 13.5, 31)], 2.6, 3.4, R4)
    body = a.layer()
    m = poly([(21, 13), (35, 13), (43, 35), (13, 35)]) | E(28, 11.5, 8.5, 8.5)
    sphere(body, m, R4, box=(12, 3, 44, 42))
    for x0, x1 in ((23, 19), (33, 37), (25.5, 24), (30.5, 32)):
        body.fill(pk.line(int(x0), 22, int(x1), 33), R4[2])
    body.fill(E(28, 12.5, 5, 5.5), "#151015")
    body.fill(ring(28, 12.5, 6, 6.5, 1.2), GOLD4[1])
    body.fill({(x, y) for x, y in ring(28, 12.5, 6, 6.5, 1.2) if x < 27 and y < 12}, GOLD4[0])
    body.set(26, 12, EYE_RED)
    body.set(30, 12, EYE_RED)
    tube(body, [(28.5, 19), (28.5, 35)], 1.0, 1.0, GOLD4)
    body.fill({(x, y) for x, y in m if y in (32, 33)}, GOLD4[2])
    body.fill({(x, y) for x, y in m if y == 32}, GOLD4[1])
    red_eye(body, 28, 20, 2, 2)


@card("abaddon")
def abaddon(a):
    a.spot = (28, 16)
    a.glow_color = mix(GOLD4[1], EYE_RED, 0.35)
    smoke = a.fx()
    for k, (x0, y0) in enumerate(((14, 33), (40, 32), (21, 35), (34, 35))):
        tube(smoke, bez((x0, y0), (x0 + (6 if k % 2 == 0 else -6), y0 - 4), (x0, y0 - 9)), 1.6, 0.6,
             [DEMON4[1]] * 4, mode="glow", dither=0.5)
    hl = a.layer()
    for s in (-1, 1):
        tube(hl, [(28 + s * 6, 12), (28 + s * 12, 8), (28 + s * 15, 3), (28 + s * 13, -1)], 2.9, 0.6, HORN4)
    head = a.layer()
    m = E(28, 17.5, 9.5, 8.5) | poly([(19.5, 19), (36.5, 19), (32.5, 31), (23.5, 31)])
    sphere(head, m, DEMON4, box=(18, 8, 38, 33))
    for side in (False, True):
        e = [(20.5, 15.5), (26.5, 18), (25.5, 20), (21, 18.5)]
        head.fill(poly(mirror(e) if side else e), EYE_RED)
    head.set(25, 18, EYE_HI)
    head.set(30, 18, EYE_HI)
    head.fill(R(23, 25, 33, 27), "#5c0d0d")
    for x in range(23, 34):
        if x % 2 == 1:
            head.set(x, 25, WHITE)
        else:
            head.set(x, 27, BONE4[1])
    t = a.top()
    for x, y in ((8, 8), (48, 10), (46, 29), (10, 27)):
        spark(t, x, y, GOLD4[1])


@card("maw_of_the_void")
def maw_of_the_void(a):
    a.spot = (28, 18)
    a.glow_color = mix(GOLD4[1], "#9e1b1b", 0.3)
    void = ["#151015", "#151015", "#231c22", "#9e1b1b"]      # Brimstone negru: miez negru, margine roșie
    rl = a.layer(glow=False)
    tube(rl, arc(28, 18, 23, 13.5, 0, 360, 72), 2.4, 2.4, void, mode="glow", bands=(0.4, 0.62, 0.84))
    mouth = a.layer()
    flat(mouth, E(28, 18, 10.5, 7), ["#d33c3c", "#9e1b1b", "#5c0d0d", "#231c22"])
    mouth.fill(E(28, 18.5, 8, 4.3), "#151015")
    mouth.fill(E(28, 21, 3.5, 1.4), BLOOD4[1])
    for x in range(21, 35, 2):
        mouth.fill([(x, 15), (x + 1, 15), (x, 16)], WHITE)
        mouth.fill([(x + 1, 22), (x, 22), (x + 1, 21)], BONE4[1])
    t = a.top()
    for x, y in ((5, 5), (51, 31), (50, 4), (6, 31)):
        spark(t, x, y, GOLD4[1])


# ═════════════════════════════════════ CAIN ═════════════════════════════════════

@card("lucky_punch")
def lucky_punch(a):
    a.spot = (34, 17)
    fx = a.fx()
    for y, x0, x1 in ((7, 1, 9), (4, 3, 8), (29, 1, 10), (32, 4, 9)):
        fx.fill(pk.line(x0, y, x1, y), BONE4[2])
    burst = a.layer()
    burst.fill(poly(star_pts(41, 17, 14, 6.5, 9, rot=10, sy=0.95)), STUN)
    burst.fill(poly(star_pts(41, 17, 8, 4, 9, rot=30)), mix(STUN, WHITE, 0.6))
    sleeve = a.layer()
    flat(sleeve, poly([(-1, 12), (14, 11), (15, 26), (-1, 27)]), GREEN4)
    sleeve.fill(R(12, 12, 13, 25), GREEN4[2])
    back = a.layer()                      # dosul palmei
    sphere(back, rounded(13, 11, 24, 26, 3), SKIN, box=(12, 9, 28, 28))
    for i, y0 in enumerate((10, 14, 18, 22)):    # patru degete îndoite: fiecare cu conturul lui
        f = a.layer()
        sphere(f, rounded(21, y0, 30, y0 + (3 if i == 3 else 4), 2), SKIN, cuts=(0.8, 0.35, 0.05),
               box=(18, y0 - 2, 31, y0 + 5))
    th = a.layer()                        # degetul mare trece prin fața degetelor
    tube(th, [(15, 21.5), (25, 21)], 2.0, 1.8, SKIN)
    cl = a.layer()                        # trifoiul norocos plutește deasupra pumnului
    clover(cl, 18.5, 5.5, 1.7, stem=False)


@card("lucky_foot")
def lucky_foot(a):
    a.spot = (30, 17)
    O, ang = (16, 11), 27
    chain = a.layer(glow=False)
    for i, (x, y) in enumerate(pk.line(12, 8, 8, 6)):
        chain.set(x, y, GOLD4[1] if i % 2 == 0 else GOLD4[2])
    chain.fill(ring(5.5, 4.5, 3.2, 3.2, 1.2), GOLD4[1])
    fur = [BONE4[0], BONE4[1], mix(BONE4[2], "#a8743d", 0.25), mix(BONE4[3], "#6b4423", 0.3)]
    foot = a.layer()
    axis = local(*O, ang, [(1, 0), (8, 0), (15, 0), (22, 0)])
    m = pk.blob([(x, y, r) for (x, y), r in zip(axis, (3.2, 3.8, 4.4, 4.8))])
    m |= {(x, y) for x, y in pk.outer_edge(m) if (x * 3 + y * 2) % 5 == 0}   # smocuri de blană pe contur
    sphere(foot, m, fur, box=(11, 4, 42, 30))
    for u, v in ((5, -1), (10, 1.5), (14, -2), (18, 1.5), (9, -3)):
        (x0, y0), (x1, y1) = local(*O, ang, [(u, v), (u + 2.5, v + 0.5)])
        foot.fill(pk.line(int(x0), int(y0), int(x1), int(y1)), fur[2])
    for v in (-3.7, 0, 3.7):              # degetele labei, fiecare cu conturul lui
        t = a.layer()
        (x, y), (cx_, cy_) = local(*O, ang, [(26.5, v), (30.5, v * 1.1)])
        sphere(t, Erot(x, y, 3.4, 2.1, ang), fur, cuts=(0.8, 0.3, 0.0))
        t.fill([(int(cx_), int(cy_)), (int(cx_) + 1, int(cy_))], INK)
    cap = a.layer()
    flat(cap, poly(local(*O, ang, [(-3.5, -3.6), (1.5, -3.6), (1.5, 3.6), (-3.5, 3.6)])), GOLD4)
    (x0, y0), (x1, y1) = local(*O, ang, [(-1, -3), (-1, 3)])
    cap.fill(pk.line(int(x0), int(y0), int(x1), int(y1)), GOLD4[2])
    t = a.top()
    spark(t, 45, 8, LEAF4[0])
    spark(t, 9, 26, STUN)


@card("bomb_toss")
def bomb_toss(a):
    a.spot = (34, 18)
    fx = a.fx()
    for i, (x, y) in enumerate(bez((1, 35), (10, 16), (26, 18), 40)):
        if (i // 3) % 2 == 0:
            fx.set(int(x), int(y), BONE4[2])
    for y, x0, x1 in ((15, 17, 23), (20, 16, 22), (25, 18, 24)):
        fx.fill(pk.line(x0, y, x1, y), BONE4[1])
    fuse = a.layer()
    tube(fuse, [(39, 12), (41, 8), (44.5, 6.5)], 1.0, 0.9, POOP4)
    b = a.layer()
    bomb(b, 35, 20.5, 9)
    b.fill(R(37, 11, 40, 12), STEEL4[1])
    t = a.top()
    spark(t, 46, 5, STUN, arm=2)
    t.set(48, 3, MARK)
    t.set(44, 3, MARK)
    t.set(48, 7, MARK)


@card("the_poop")
def the_poop(a):
    a.spot = (28, 19)
    a.shadow(28, 33.5, 14, 2)
    st = a.fx()
    for x0 in (19, 37):
        for y in range(2, 12):
            x = x0 + round(1.2 * math.sin(y * 0.9))
            if y % 3 != 0:
                st.set(x, y, LEAF4[2])
    for cx, cy, rx, ry in ((28, 28, 12.5, 4.8), (28, 22.5, 9.5, 4.3), (28.5, 17.5, 6.5, 3.6)):
        L = a.layer()
        sphere(L, E(cx, cy, rx, ry), POOP4, cuts=(0.88, 0.38, 0.05))
    tip = a.layer()
    tube(tip, [(29, 15), (30, 11.5), (32.5, 10)], 2.2, 0.8, POOP4)
    for x, y in ((10, 9), (45, 7), (48, 17)):
        fly(a, x, y)


@card("lucky_penny")
def lucky_penny(a):
    a.spot = (28, 17)
    side = a.layer()
    side.fill(E(30.5, 18, 11, 13), GOLD4[3])
    for y in range(6, 31, 2):
        side.fill({(x, y2) for x, y2 in E(30.5, 18, 11, 13) if y2 == y and x >= 38}, GOLD4[2])
    face = a.layer()
    m = E(28, 18, 11, 13)
    sphere(face, m, GOLD4)
    face.fill(ring(28, 18, 9, 11, 1), GOLD4[2])
    face.fill(pk.line(20, 11, 22, 8), WHITE)
    face.set(19, 13, WHITE)
    cl = a.layer()
    clover(cl, 27.5, 16.5, 2.4)
    t = a.top()
    spark(t, 45, 7, WHITE)
    spark(t, 10, 27, STUN)


@card("moms_knife")
def moms_knife(a):
    a.spot = (30, 16)
    O, ang = (7, 31), -30
    handle = a.layer()
    hm = poly(local(*O, ang, [(-1, -2.6), (13, -2.6), (13, 2.6), (-1, 2.6)])) | E(*local(*O, ang, [(-1, 0)])[0], 2.6, 2.6)
    flat(handle, hm, POOP4)
    for u in (3, 9):
        x, y = local(*O, ang, [(u, 0)])[0]
        handle.set(int(x), int(y), BONE4[0])
    guard = a.layer()
    flat(guard, poly(local(*O, ang, [(13, -3.3), (16, -3.3), (16, 3.6), (13, 3.6)])), STEEL4)
    blade = a.layer()
    bm = poly(local(*O, ang, [(16, -3), (44, -3), (50, -2.2), (53, -1.4), (47, 1.0), (38, 3.2), (16, 4)]))
    steel = [(-1.6, STEEL4[0]), (1.0, STEEL4[1]), (2.6, STEEL4[2]), (99, STEEL4[3])]
    band_shade(blade, bm, O[0], O[1], ang, steel)
    a_ = math.radians(ang)
    bloody = {(x, y) for x, y in bm
              if (x + 0.5 - O[0]) * math.cos(a_) + (y + 0.5 - O[1]) * math.sin(a_) > 34 + 2 * math.sin(y * 1.3)}
    band_shade(blade, bloody, O[0], O[1], ang,
               [(-1.6, BLOOD4[0]), (1.0, BLOOD4[1]), (2.6, BLOOD4[2]), (99, BLOOD4[3])])
    dr = a.layer(glow=False)
    for u, ln in ((37, 5), (43, 3)):
        x, y = local(*O, ang, [(u, 3.0)])[0]
        tube(dr, [(x, y), (x, y + ln)], 0.9, 0.8, BLOOD4)
    drops(a, [(36, 32, 1.2), (44, 30.5, 1.0)])


@card("crickets_head")
def crickets_head(a):
    a.spot = (28, 17)
    cat = [mix("#5c6168", "#7a7f86", 0.3), "#43474d", "#2f3236", mix("#2f3236", OUT, 0.5)]
    a.shadow(28, 34, 12, 1.6)
    ears = a.layer()
    for side in (False, True):
        e = [(17.5, 15), (18.5, 2.5), (24.5, 10)]
        inner = [(19, 12), (19.5, 6), (22.5, 10.5)]
        if side:
            e, inner = mirror(e), mirror(inner)
        flat(ears, poly(e), cat)
        ears.fill(poly(inner), PINK4[2])
    head = a.layer()
    m = E(28, 18.5, 11.5, 9.5) | E(28, 22, 12.5, 6.5)
    sphere(head, m, cat, box=(15, 9, 41, 30))
    for x0 in (21, 32):
        head.fill(pk.line(x0, 16, x0 + 3, 19) + pk.line(x0 + 3, 16, x0, 19), BONE4[2])
    head.fill([(27, 21), (28, 21), (29, 21), (28, 22)], PINK4[1])
    head.fill([(26, 24), (27, 23), (28, 24), (29, 23), (30, 24)], INK)
    neck = a.layer()
    sphere(neck, E(28, 28.5, 7.5, 2.4), BLOOD4, cuts=(0.95, 0.5, 0.2))
    tube(neck, [(24.5, 29), (24.5, 33)], 0.9, 0.8, BLOOD4)
    tube(neck, [(31.5, 29), (31.5, 32)], 0.9, 0.8, BLOOD4)
    wh = a.top()
    for s in (-1, 1):
        for dy0, dy1 in ((22, 20), (24, 25)):
            x0, x1 = (17, 10) if s < 0 else (38, 45)
            wh.fill(pk.line(x0, dy0, x1, dy1), BONE4[1])


def die_faces(base):
    return {"top": [mix(base[0], WHITE, 0.5), base[0], base[1], base[2]],
            "left": [base[0], base[1], base[2], base[3]],
            "right": [base[1], base[2], base[3], base[3]]}


@card("dice_shard")
def dice_shard(a):
    a.spot = (27, 18)
    faces = die_faces(BONE4)
    main = a.layer()
    cube(main, 22, 20, 11, faces, {"top": [(0.5, 0.5)], "left": [(0.27, 0.27), (0.73, 0.73)],
                                   "right": [(0.2, 0.75), (0.5, 0.82)]})
    chip = poly([(26.5, 10.5), (29, 13.5), (27.5, 15.5), (30.5, 17.5), (29.5, 20.5), (35, 19.5), (35, 10)])
    main.erase(chip)
    broken = {p for p in main.mask() if any((p[0] + dx, p[1] + dy) in chip for dx, dy in pk.N8)}
    main.fill(broken, BONE4[3])
    main.fill(pk.polyline([(27, 17), (25, 19), (26, 22), (24, 25)]), BONE4[3])
    shard = a.layer()
    cube(shard, 43, 10.5, 6, faces, {"top": [(0.5, 0.5)]})
    shard.erase(poly([(36, 9), (39.5, 11), (38.5, 13), (41.5, 14.5), (40.5, 18), (36, 18)]))
    frag = a.layer()
    for pts in ([(46, 23), (49, 22), (47, 26)], [(40, 29), (43, 30), (41, 33)], [(50, 29), (52, 28), (52, 31)]):
        frag.fill(poly(pts), BONE4[1])
    t = a.top()
    spark(t, 34, 6, WHITE)
    spark(t, 52, 17, STUN)


@card("midas_touch")
def midas_touch(a):
    a.spot = (25, 17)
    a.glow_color = mix(GOLD4[0], WHITE, 0.3)
    sleeve = a.layer()
    flat(sleeve, poly([(16, 29), (32, 29), (33, 37), (15, 37)]), GREEN4)
    th = a.layer()
    tube(th, [(19, 24), (15, 19.5), (13.5, 15.5)], 1.9, 1.6, GOLD4)
    fingers = a.layer()
    for x0, y0, x1, y1 in ((19.5, 18, 18.5, 7.5), (23, 17, 23, 5), (26.5, 17, 27, 6), (30, 18, 31, 9.5)):
        tube(fingers, [(x0, y0), (x1, y1)], 1.6, 1.45, GOLD4)
    palm = a.layer()
    sphere(palm, rounded(17, 15, 32, 30, 4), GOLD4, cuts=(0.8, 0.35, 0.05))
    palm.fill(pk.line(21, 24, 28, 22), GOLD4[2])
    coins = a.layer()
    for x, y in ((41, 9), (47, 20), (40, 28)):
        sphere(coins, E(x, y, 3, 3.4), GOLD4)
        coins.fill(pk.line(x, y - 1, x, y + 1), GOLD4[2])
    t = a.top()
    for x, y in ((36, 4), (52, 11), (9, 9), (50, 31), (11, 29)):
        spark(t, x, y, GOLD4[0])


@card("dr_fetus")
def dr_fetus(a):
    a.spot = (28, 19)
    fx = a.fx()
    for y, x0, x1 in ((6, 13, 17), (11, 13, 16), (25, 41, 44), (30, 41, 44)):
        fx.fill(pk.line(x0, y, x1, y), BONE4[2])
    minis = a.layer()
    bomb(minis, 8, 9, 3.2)
    bomb(minis, 48.5, 28, 3.2)
    minis.fill([(9, 5), (10, 4)], POOP4[1])
    minis.fill([(49, 24), (50, 23)], POOP4[1])
    fuse = a.layer()
    tube(fuse, [(32, 11), (35, 7), (39, 5.5)], 1.0, 0.9, POOP4)
    b = a.layer()
    bomb(b, 28, 20.5, 11)
    # „fereastra” bombei: lichid închis, ca fătul deschis la culoare să iasă în față
    b.fill(ring(28.5, 21.5, 8.5, 8, 1.2), STEEL4[0])
    fluid = ["#9e1b1b", "#5c0d0d", mix("#5c0d0d", "#151015", 0.45), "#151015"]
    sphere(b, E(28.5, 21.5, 7.3, 6.8), fluid, cuts=(0.95, 0.55, 0.2))
    body = a.layer(glow=False)
    sphere(body, E(30.5, 24, 3.1, 2.4), PINK4)
    tube(body, bez((32, 26), (35, 27.5), (35.5, 24)), 0.6, 0.6, PINK4)
    head = a.layer(glow=False)
    sphere(head, E(26.5, 19.5, 3.9, 3.6), PINK4)
    head.fill([(24, 20), (25, 20)], PINK4[3])
    t = a.top()
    spark(t, 40, 4, STUN, arm=2)
    spark(t, 4, 5, STUN)
    spark(t, 52, 24, STUN)


# ═════════════════════════════════════ ISAAC ═════════════════════════════════════

@card("tear")
def tear_card(a):
    a.spot = (34, 17)
    fx = a.fx()
    for y, x0, x1 in ((12, 12, 25), (17, 4, 24), (22, 9, 25)):
        for x in range(x0, x1 + 1):
            if x % 3 != 0:
                fx.set(x, y, TEAR4[1] if x > (x0 + x1) // 2 else TEAR4[2])
    tr = a.layer(glow=False)
    tear(tr, 9, 26, 1.4)
    tear(tr, 17, 25.5, 2.2)
    t = a.layer()
    tear(t, 35, 17, 9)


@card("yum_heart")
def yum_heart(a):
    a.spot = (28, 16)
    hl = a.layer()
    m = heart(27.5, 12, 12.5)
    bite = E(41, 7, 5.6, 5.6)
    for k in range(5):
        ang = math.radians(100 + k * 30)
        bite |= E(41 + 6.2 * math.cos(ang), 7 + 6.2 * math.sin(ang), 1.2, 1.2)
    m -= bite
    sphere(hl, m, BLOOD4, cuts=(0.88, 0.4, 0.08), box=(14, 4, 41, 28))
    near = {(x, y) for x, y in m if any((x + dx, y + dy) in bite for dx, dy in pk.N8)}
    hl.fill(near, mix(BLOOD4[0], "#fff0e8", 0.35))
    glint(hl, 19, 9)
    hl.set(19, 11, WHITE)
    cr = a.layer(glow=False)
    for x, y in ((47, 13), (50, 9), (46, 4)):
        cr.fill(R(x, y, x + 1, y), BLOOD4[1])
    t = a.top()
    for x, y in ((8, 9), (11, 25), (47, 26)):
        spark(t, x, y, BLOOD4[0], core=WHITE)


@card("the_sad_onion")
def the_sad_onion(a):
    a.spot = (28, 18)
    sp = a.layer()
    tube(sp, [(28, 9), (26, 4), (23.5, 1)], 1.4, 0.6, LEAF4)
    tube(sp, [(28.5, 9), (31, 3), (34, 1)], 1.4, 0.6, LEAF4)
    on = a.layer()
    onion = ["#fff0e8", mix("#e8dcc4", "#f2d3c4", 0.4), "#c8b89a", mix("#c8b89a", "#6b4423", 0.45)]
    m = E(28, 22, 11, 10) | poly([(19, 18), (37, 18), (29, 7), (27, 7)])
    sphere(on, m, onion, box=(16, 8, 40, 33))
    for s in (-1, 1):
        on.fill(pk.polyline([(int(28 + s * 8), 14), (int(28 + s * 10), 22), (int(28 + s * 8), 29)]), onion[3])
    for x in (25, 28, 31):
        on.set(x, 32, POOP4[2])
        on.set(x - 1, 33, POOP4[2])
    for s in (-1, 1):
        ex = 28 + s * 4.5
        on.fill(E(ex, 21, 2.0, 2.4), "#1d2a3d")
        on.set(math.floor(ex - 1), 20, WHITE)
        on.fill(pk.line(int(ex - 2 * s), 17, int(ex + 1 * s), 18), POOP4[3])
        on.fill(R(math.floor(ex), 24, math.floor(ex), 28), TEAR4[1])
    on.fill([(26, 28), (27, 27), (28, 27), (29, 27), (30, 28)], POOP4[3])
    tt = a.layer(glow=False)
    tear(tt, 11, 24, 2.5)
    tear(tt, 45.5, 22, 2.8)


@card("spoon_bender")
def spoon_bender(a):
    a.spot = (26, 18)
    spoon = [WHITE, mix("#7a7f86", "#ffffff", 0.6), "#7a7f86", "#5c6168"]
    wv = a.fx()
    # undele psihice pornesc din îndoitură
    for rr, col in ((6, PURPLE4[0]), (9, mix(PURPLE4[0], PURPLE4[1], 0.5)), (12, PURPLE4[1])):
        for i, (x, y) in enumerate(arc(20, 26, rr, rr * 0.75, 180, 360, 36)):
            if i % 3 != 2:
                wv.set(int(x), int(y), col)
    trail = a.fx()
    for i, (x, y) in enumerate(bez((35, 6), (48, 4), (45, 17), 18)):
        if i % 3 != 2:
            trail.set(int(x), int(y), PTEAR4[0])
    handle = a.layer()
    tube(handle, [(4, 31), (18, 27.5), (20.5, 26), (24, 18), (27.5, 12)], 1.5, 1.4, spoon)
    bowl = a.layer()
    sphere(bowl, Erot(30, 8, 6, 3.9, -59), spoon)
    bowl.fill(Erot(30.5, 8.2, 3.6, 2.0, -59), spoon[2])
    glint(bowl, 28, 5)
    tt = a.layer()
    tear(tt, 45, 22, 4.2, PTEAR4)


@card("book_of_belial")
def book_of_belial(a):
    a.spot = (28, 17)
    pages = a.layer()
    pm = poly([(36, 5), (39.5, 7), (39.5, 34), (36, 32)]) | poly([(18, 32), (36, 32), (39.5, 34), (21, 34)])
    pages.fill(pm, BONE4[1])
    for y in range(8, 33, 2):
        pages.fill({(x, yy) for x, yy in pm if yy == y and x >= 37}, BONE4[2])
    cover = a.layer()
    book = [mix("#5c0d0d", "#9e1b1b", 0.5), "#5c0d0d", mix("#5c0d0d", "#151015", 0.5), "#151015"]
    flat(cover, rounded(16, 4, 36, 31, 1), book)
    cover.fill(R(16, 4, 18, 31), book[2])
    for y in (8, 27):
        cover.fill(R(16, y, 18, y), GOLD4[1])
    for pts in ([(36, 4), (32, 4), (36, 8)], [(36, 31), (32, 31), (36, 27)]):
        cover.fill(poly([(p[0] + 0.5, p[1] + 0.5) for p in pts]), GOLD4[1])
    em = a.layer()
    sphere(em, E(27, 18.5, 5, 4.5), BLOOD4)
    for s in (-1, 1):
        tube(em, [(27 + s * 3, 15), (27 + s * 5, 11.5), (27 + s * 4.5, 9)], 1.3, 0.5, BLOOD4)
    em.fill([(24, 18), (25, 18), (29, 18), (30, 18)], OUT)
    em.fill(R(25, 21, 29, 21), OUT)
    t = a.top()
    for x, y in ((10, 8), (45, 5), (46, 27), (9, 28), (43, 15)):
        spark(t, x, y, BLOOD4[1], core=BLOOD4[0])


@card("20_20")
def twenty_twenty(a):
    a.spot = (28, 15)
    fx = a.fx()
    for y in (26, 30):
        for x in range(4, 20):
            if x % 3 != 0:
                fx.set(x, y, TEAR4[2])
    tt = a.layer(glow=False)
    tear(tt, 24, 28.5, 3.3)
    tear(tt, 36, 28.5, 3.3)
    frame = ["#4b3b44", "#231c22", "#151015", "#151015"]
    tl = a.layer()
    tube(tl, [(11, 12), (3, 10)], 0.9, 0.8, frame)
    tube(tl, [(45, 12), (53, 10)], 0.9, 0.8, frame)
    gl = a.layer()
    lens = [WHITE, mix("#d6ecff", "#7fb2e5", 0.3), mix("#7fb2e5", "#4a7fbf", 0.5), "#4a7fbf"]
    for cx in (19, 37):
        sphere(gl, E(cx, 14, 7.5, 6.5), lens, cuts=(0.8, 0.3, -0.1))
        gl.fill(pk.line(cx - 4, 13, cx - 1, 10) + pk.line(cx - 3, 15, cx + 1, 11), WHITE)
        gl.fill(ring(cx, 14, 7.5, 6.5, 1.4), frame[1])
        gl.fill({(x, y) for x, y in ring(cx, 14, 7.5, 6.5, 1.4) if y < 12 and x < cx}, frame[0])
    tube(gl, [(25.5, 13), (28, 11.5), (30.5, 13)], 0.9, 0.9, frame)


@card("ipecac")
def ipecac(a):
    a.spot = (34, 16)
    burst = a.layer()
    burst.fill(poly(star_pts(35, 15, 13, 8, 10, rot=8, sy=0.92)), POISON4[2])
    burst.fill(poly(star_pts(35, 15, 10, 7, 10, rot=26)), POISON4[1])
    bl = a.layer()
    bm = rounded(4, 18, 14, 33, 2)
    sphere(bl, bm, [mix("#a8743d", "#e0b04a", 0.3), "#8a5a2b", "#6b4423", "#4a2e17"], box=(3, 16, 16, 36))
    bl.fill(R(5, 23, 13, 29), BONE4[1])
    bl.fill(R(7, 25, 11, 27), POISON4[1])
    bl.fill(R(7, 14, 11, 17), "#6b4423")
    cork = a.layer()
    flat(cork, R(6, 11, 12, 14), POOP4)
    t = a.layer()
    sphere(t, E(35, 15, 6.5, 6.5), POISON4, cuts=(0.86, 0.36, 0.04))
    glint(t, 32, 11)
    dr = a.layer(glow=False)
    for x, y, r in ((19, 6, 1.5), (52, 8, 1.3), (50, 28, 1.6), (22, 27, 1.3), (44, 33, 1.0), (31, 2, 1.0)):
        sphere(dr, E(x, y, r, r), POISON4, cuts=(0.8, 0.3, 0.0))
    t2 = a.top()
    for x, y in ((25, 12), (46, 22), (40, 4)):
        t2.fill(pk.inner_edge(E(x + 0.5, y + 0.5, 1.6, 1.6)), POISON4[0])


@card("the_d6")
def the_d6(a):
    a.spot = (28, 18)
    arr = a.layer()
    white = [WHITE, mix("#e8dcc4", WHITE, 0.6), mix("#c8b89a", "#d6ecff", 0.35), mix("#7a7f86", "#d6ecff", 0.3)]
    for a0, a1 in ((-80, -10), (100, 170)):
        pts = arc(28, 18, 22, 15.5, a0, a1, 16)
        tube(arr, pts, 1.0, 1.0, white)
        (x0, y0), (x1, y1) = pts[-2], pts[-1]
        ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
        arr.fill(poly(local(x1, y1, ang, [(3, 0), (-1.5, -3), (-1.5, 3)])), white[1])
    d = a.layer()
    cube(d, 28, 18.5, 12, die_faces(white), {
        "top": [(0.5, 0.5)], "left": [(0.27, 0.27), (0.73, 0.73)],
        "right": [(0.25, 0.25), (0.5, 0.5), (0.75, 0.75)]})


@card("holy_mantle")
def holy_mantle(a):
    a.spot = (28, 15)
    a.glow_color = mix(GOLD4[0], WHITE, 0.4)
    isa = a.layer(glow=False)
    baby_head(isa, 28, 25, 6.5, cry=True)
    halo = a.layer(glow=False)           # aureola stă deasupra capului, în interiorul scutului
    hr = ring(28, 16.5, 6.5, 2.0, 1.2)
    halo.fill(hr, GOLD4[1])
    halo.fill({(x, y) for x, y in hr if y < 16}, GOLD4[0])
    # scutul: o bulă translucidă (amestec cu ce e dedesubt) cu margine și luciu deschise
    dome = E(28, 19.5, 16.5, 15)
    a.veil(dome, "#fff0e8", 0.2)
    rim = pk.inner_edge(dome) | (pk.inner_edge(dome - pk.inner_edge(dome)) & {(x, y) for x, y in dome if y < 16})
    a.veil(rim, WHITE, 0.6)
    t = a.top()
    t.fill(pk.polyline([(15, 15), (17, 11), (21, 8)]), WHITE)
    t.set(14, 18, WHITE)
    for x, y in ((6, 6), (49, 4), (51, 18), (5, 25), (49, 30)):
        spark(t, x, y, GOLD4[0])


@card("polyphemus")
def polyphemus(a):
    a.spot = (28, 17)
    a.glow_color = mix(GOLD4[0], "#d6ecff", 0.4)
    ey = a.layer()
    sphere(ey, E(16, 18, 11.5, 11.5), ["#ffffff", "#fff0e8", "#e8dcc4", "#c8b89a"], cuts=(0.8, 0.3, 0.0))
    for pts in ([(5, 16), (8, 17), (10, 15)], [(9, 27), (11, 24), (13, 25)], [(12, 8), (13, 11), (15, 10)],
                [(7, 22), (10, 22)]):
        ey.fill(pk.polyline([tuple(map(int, p)) for p in pts]), BLOOD4[1])
    ey.fill(E(20.5, 18, 6, 6.5), TEAR4[2])
    ey.fill(ring(20.5, 18, 6, 6.5, 1), TEAR4[3])
    ey.fill(E(21.5, 18, 3, 3.5), "#1b1530")
    glint(ey, 18, 15)
    sp = a.layer(glow=False)             # stropi care se desprind din ochi, în urma lacrimii
    tear(sp, 31, 26.5, 1.4)
    tear(sp, 31.5, 9.5, 1.1)
    tt = a.layer()
    tear(tt, 44, 17.5, 9.5)
    t = a.top()
    for x, y in ((50, 4), (53, 31), (36, 32)):
        spark(t, x, y, GOLD4[0])


# ═════════════════════════════════════ LILITH ═════════════════════════════════════

def incubus(a, cx, cy, r):
    """Incubus (familiarul lui Lilith): demon mic negru, coarne, aripioare, ochi roșii; privește spre dreapta."""
    wl = a.layer()
    for s in (-1, 1):
        # aripă de liliac ridicată: braț spre vârf, apoi două „degete” cu margine festonată
        pts = [(cx + s * r * 0.5, cy - r * 0.1), (cx + s * r * 1.15, cy - r * 1.05), (cx + s * r * 1.8, cy - r * 0.95),
               (cx + s * r * 1.6, cy - r * 0.35), (cx + s * r * 1.75, cy + r * 0.15), (cx + s * r * 1.3, cy - r * 0.05),
               (cx + s * r * 1.1, cy + r * 0.45), (cx + s * r * 0.7, cy + r * 0.35)]
        flat(wl, poly(pts), IMP4)
    tl = a.layer()
    tube(tl, bez((cx - 1, cy + r * 0.9), (cx - r * 0.5, cy + r * 1.5), (cx - r * 1.1, cy + r * 1.25)), 0.8, 0.7, IMP4)
    tip = (cx - r * 1.1, cy + r * 1.25)
    tl.fill(poly([(tip[0] - 1.8, tip[1] - 1.2), (tip[0] + 0.8, tip[1] - 0.6), (tip[0] - 0.8, tip[1] + 1.8)]), IMP4[2])
    hl = a.layer()
    for s in (-1, 1):
        tube(hl, [(cx + s * r * 0.45, cy - r * 0.6), (cx + s * r * 0.6, cy - r * 1.15)], 1.2, 0.5, HORN4)
    L = a.layer()
    sphere(L, E(cx, cy, r, r * 0.92), IMP4)
    red_eye(L, math.floor(cx - r * 0.05), math.floor(cy - r * 0.3), 2, 2, P.LILITH["incubus_eye"])
    red_eye(L, math.floor(cx + r * 0.45), math.floor(cy - r * 0.3), 2, 2, P.LILITH["incubus_eye"])
    L.set(math.floor(cx + r * 0.3), math.floor(cy + r * 0.45), WHITE)
    return L


@card("incubus_shot")
def incubus_shot(a):
    a.spot = (30, 16)
    fx = a.fx()
    for y, x0, x1 in ((13, 26, 34), (17, 24, 35), (21, 26, 34)):
        for x in range(x0, x1 + 1):
            if x % 3 != 1:
                fx.set(x, y, BLOOD4[2])
    incubus(a, 14, 16, 6.5)
    sh = a.layer()
    sphere(sh, E(41, 17, 5, 5), BLOOD4, cuts=(0.86, 0.36, 0.04))
    glint(sh, 38, 14)
    t = a.top()
    spark(t, 22, 17, BLOOD4[0], core="#fff0e8")


@card("cambion_shield")
def cambion_shield(a):
    a.spot = (28, 15)
    sh = a.layer()
    m = poly([(15.5, 3.5), (40.5, 3.5), (40.5, 15), (37.5, 23.5), (28, 33.5), (18.5, 23.5), (15.5, 15)])
    sphere(sh, m, PURPLE4, cuts=(0.85, 0.35, 0.02), box=(12, 0, 44, 36))
    rim = pk.inner_edge(m) | pk.inner_edge(m - pk.inner_edge(m))
    sh.fill(rim, STEEL4[2])
    sh.fill({(x, y) for x, y in rim if x < 28 and y < 20}, STEEL4[0])
    sh.fill(pk.line(19, 13, 23, 7), mix(PURPLE4[0], WHITE, 0.4))
    em = a.layer()
    for s in (-1, 1):
        tube(em, [(28 + s * 2.5, 13), (28 + s * 4, 9.5)], 1.1, 0.5, HORN4)
    sphere(em, E(28, 16.5, 5, 4.5), IMP4)
    red_eye(em, 25, 15, 2, 2, P.LILITH["incubus_eye"])
    red_eye(em, 30, 15, 2, 2, P.LILITH["incubus_eye"])
    t = a.top()
    spark(t, 46, 8, P.STATUS["block"])
    spark(t, 10, 26, P.STATUS["block"])


@card("box_of_friends")
def box_of_friends(a):
    a.spot = (28, 17)
    a.shadow(28, 34, 15, 1.8)
    back = a.layer()
    flat(back, poly([(16, 17), (40, 17), (37, 12), (19, 12)]), CARD4)
    fr = a.layer()
    sphere(fr, E(20.5, 14, 4.2, 4), IMP4)
    for s in (-1, 1):
        fr.fill([(int(20.5 + s * 2.5), 9), (int(20.5 + s * 2.5), 10)], HORN4[1])
    red_eye(fr, 19, 13, 1, 1, P.LILITH["incubus_eye"])
    red_eye(fr, 22, 13, 1, 1, P.LILITH["incubus_eye"])
    bb = a.layer()
    baby_head(bb, 29, 12.5, 5.2)
    fly(a, 37, 13)
    box = a.layer()
    flat(box, R(15, 19, 41, 33), CARD4)
    box.fill(R(16, 19, 40, 19), CARD4[3])
    box.fill(R(25, 20, 31, 22), BONE4[1])
    flaps = a.layer()
    flat(flaps, poly([(15, 19.5), (8, 13), (12, 11.5), (19, 18)]), CARD4)
    flat(flaps, poly([(41, 19.5), (48, 13), (44, 11.5), (37, 18)]), CARD4)


@card("cursed_eye")
def cursed_eye(a):
    a.spot = (28, 16)
    smoke = a.fx()
    for x0, y0 in ((6, 8), (50, 9), (8, 27), (48, 27), (28, 2)):
        tube(smoke, bez((x0, y0), (x0 + 4, y0 - 3), (x0 + 7, y0 + 1)), 1.4, 0.6, [PURPLE4[1]] * 4, mode="glow",
             dither=0.5)
    ey = a.layer()
    m = set()
    for x in range(9, 47):
        u = (x + 0.5 - 28) / 18.5
        if abs(u) >= 1:
            continue
        top, bot = 17 - 10 * math.sqrt(1 - u * u), 17 + 7.5 * math.sqrt(1 - u * u)
        for y in range(H):
            if top <= y + 0.5 <= bot:
                m.add((x, y))
    sclera = [WHITE, mix("#e8dcc4", "#9a8fb0", 0.3), mix("#c8b89a", "#9a8fb0", 0.5), "#9a8fb0"]
    sphere(ey, m, sclera, cuts=(0.85, 0.35, 0.0))
    ey.fill(E(28, 17, 6.5, 6.5) & m, MAG4[1])
    ey.fill(ring(28, 17, 6.5, 6.5, 1) & m, MAG4[3])
    ey.fill(R(27, 12, 28, 21), OUT)
    glint(ey, 25, 14)
    ey.fill({(x, y) for x, y in m if (x, y - 3) not in m}, PURPLE4[2])
    lashes = a.layer()
    for x0, x1 in ((14, 12), (20, 19), (28, 28), (36, 37), (42, 44)):
        u = (x0 + 0.5 - 28) / 18.5
        y0 = int(17 - 10 * math.sqrt(1 - u * u))
        lashes.fill(pk.line(x0, y0 - 1, x1, y0 - 4), PURPLE4[3])
    # lacrimi blestemate: scurte, cu picătură la capăt (lungi, arătau ca niște picioare)
    dr = a.layer(glow=False)
    for x, ln in ((18, 2), (37, 4)):
        u = (x + 0.5 - 28) / 18.5
        y0 = 17 + 7.5 * math.sqrt(1 - u * u)
        tube(dr, [(x + 0.5, y0), (x + 0.5, y0 + ln)], 0.7, 0.9, PTEAR4)
        sphere(dr, E(x + 0.5, y0 + ln + 1.8, 1.6, 1.9), PTEAR4, cuts=(0.8, 0.3, 0.0))


@card("moms_eyeshadow")
def moms_eyeshadow(a):
    a.spot = (28, 15)
    a.shadow(28, 33.5, 17, 1.8)
    case = [mix(MAG, "#fff0e8", 0.5), mix(MAG, "#fff0e8", 0.2), MAG, mix(MAG, OUT, 0.4)]
    # fardul lui Mom: o pudrieră rotundă deschisă — oglinda în capac, culorile în bază
    lid = a.layer()
    flat(lid, E(28, 11.5, 13, 9.5), case)
    mir = E(28, 11.5, 10.3, 7.2)
    lid.fill(mir, mix("#d6ecff", "#7fb2e5", 0.45))
    lid.fill(pk.inner_edge(mir), case[3])
    lid.fill((set(pk.line(20, 15, 25, 7)) | set(pk.line(23, 16, 28, 8)) | set(pk.line(32, 15, 35, 11))) & mir, WHITE)
    hinge = a.layer()
    hinge.fill(R(25, 19, 31, 21), case[2])
    base = a.layer()
    top, side = E(28, 25.5, 16, 6.3), E(28, 27.5, 16, 6.3)
    base.fill(side, case[3])
    base.fill({(x, y) for x, y in side if (x, y - 2) not in side}, case[2])
    flat(base, top, case, shadow=None)
    base.fill(E(28, 25.5, 13.5, 4.6), case[3])
    for cx, ramp in ((20.5, PTEAR4), (28, MAG4), (35.5, TEAR4)):
        sphere(base, E(cx, 25.5, 3.4, 2.1), ramp, cuts=(0.75, 0.25, -0.5))
    t = a.top()
    for x, y in ((48, 6), (7, 9), (51, 24)):
        spark(t, x, y, STUN)
    hm = heart(46.5, 15, 2.2)
    t.fill(hm, MAG4[1])
    t.set(45, 14, MAG4[0])


@card("lil_brimstone")
def lil_brimstone(a):
    a.spot = (30, 17)
    a.shadow(13, 31, 6, 1.4)
    hl = a.layer()
    for s in (-1, 1):
        tube(hl, [(13 + s * 3, 11), (13 + s * 3.8, 7.5)], 1.2, 0.5, HORN4)
    h = a.layer()
    sphere(h, E(13, 16.5, 6.5, 6), DEMON4)
    red_eye(h, 13, 13, 2, 3)
    red_eye(h, 17, 13, 2, 3)
    h.fill(E(17.5, 19.5, 2.0, 1.7), "#5c0d0d")
    b = a.layer()
    tube(b, [(17, 19.5), (60, 19.5)], 2.1, 2.5, BEAM4, mode="glow")
    sp = a.layer(glow=False)
    for x, y in ((50, 13), (53, 25), (46, 27)):
        sphere(sp, E(x, y, 1.1, 1.1), BLOOD4, cuts=(0.8, 0.3, 0.0))


@card("succubus")
def succubus(a):
    a.spot = (28, 16)
    aura = E(28, 17, 17.5, 15.5)
    a.veil(aura, MAG4[0], 0.14, under=True)
    a.veil(aura - E(28, 17, 15, 13), MAG4[0], 0.42, under=True)
    hair4 = [mix("#3a2c45", "#6b3f7d", 0.4), "#3a2c45", "#241c2b", mix("#241c2b", OUT, 0.5)]
    skin4 = ["#fff0e8", P.LILITH["skin"][0], P.LILITH["skin"][1], mix(P.LILITH["skin"][1], "#6b3f7d", 0.35)]
    wl = a.layer()
    for s in (-1, 1):                     # aripile ies mult în lateral, ca să nu le acopere părul
        pts = [(28 + s * 6, 17), (28 + s * 13, 7), (28 + s * 18, 6.5), (28 + s * 16.5, 12), (28 + s * 18, 16),
               (28 + s * 14, 14.5), (28 + s * 13, 19), (28 + s * 8, 20)]
        flat(wl, poly(pts), IMP4)
    tl = a.layer()
    tube(tl, bez((30, 25), (37, 30), (41, 25)), 0.8, 0.7, IMP4)
    sphere(tl, heart(42, 22.5, 1.9), MAG4)
    dress = a.layer()
    sphere(dress, poly([(23.5, 21), (32.5, 21), (34.5, 29), (21.5, 29)]), PURPLE4, box=(20, 18, 36, 31))
    hair = a.layer()
    sphere(hair, E(28, 14, 8, 7.5) | poly([(20, 14), (36, 14), (35, 24), (21, 24)]), hair4, box=(19, 6, 37, 26))
    hn = a.layer()                        # coarne roșii: pe părul închis, cele negre dispăreau
    for s in (-1, 1):
        tube(hn, [(28 + s * 3.5, 8.5), (28 + s * 5.5, 5), (28 + s * 5, 2.5)], 1.3, 0.5, BLOOD4)
    face = a.layer()
    sphere(face, E(28, 16.5, 5.4, 5.1), skin4)
    face.fill(E(28, 12.3, 5.8, 2.7), hair4[1])
    face.fill([(25, 16), (26, 16), (30, 16), (31, 16), (25, 17), (26, 17), (30, 17), (31, 17)], EYE_RED)
    face.fill([(24, 15), (25, 15), (26, 15), (30, 15), (31, 15), (32, 15)], OUT)
    face.set(25, 16, EYE_HI)
    face.set(30, 16, EYE_HI)
    face.fill([(27, 20), (28, 20), (29, 20)], MAG4[1])
    t = a.top()
    for i, (x, y) in enumerate(arc(28, 17, 16.2, 14.2, 0, 360, 56)):
        if i % 2 == 0:
            t.set(int(x), int(y), MAG4[0])
    for x, y in ((6, 6), (49, 4), (51, 29)):
        t.fill(heart(x, y, 2.2), MAG4[1])
        t.set(x - 2, y - 1, MAG4[0])


@card("multidimensional_baby")
def multidimensional_baby(a):
    a.spot = (28, 16)
    ghost = [mix("#d6ecff", "#fff0e8", 0.5), mix("#9a8fb0", "#d6ecff", 0.45), "#9a8fb0", mix("#9a8fb0", "#3a2c45", 0.5)]
    echo = a.fx()
    # copiile din alte dimensiuni: aceeași siluetă, decalată în roșu și în albastru (dither)
    for dx, col in ((-6, MAG4[1]), (6, TEAR4[1])):
        for x, y in E(28 + dx, 16, 8, 7.5):
            if pk.dither_on(x, y, 0.5):
                echo.set(x, y, col)
    for y, x0, x1 in ((28, 3, 8), (31, 5, 9), (28, 33, 38), (31, 35, 39)):
        for x in range(x0, x1 + 1):
            if x % 2 == 0:
                echo.set(x, y, PTEAR4[1])
    b = a.layer()
    baby_head(b, 28, 16, 8, ghost, eye="#241c2b")
    b.fill([(27, 21), (28, 21), (29, 21)], ghost[3])
    b.fill([(28, 8), (29, 7), (30, 7)], ghost[2])          # o șuviță, ca la bebelușii din Isaac
    tt = a.layer(glow=False)
    tear(tt, 13, 29.5, 2.6, PTEAR4)
    tear(tt, 43, 29.5, 2.6, PTEAR4)
    t = a.top()
    t.fill(ring(50.5, 8.5, 3.5, 3.5, 1), MARK)
    t.fill([(50, 3), (50, 4), (50, 13), (50, 14), (45, 8), (46, 8), (55, 8), (54, 8)], MARK)


@card("guardian_angel")
def guardian_angel(a):
    a.spot = (28, 14)
    a.glow_color = mix(GOLD4[0], WHITE, 0.4)
    feather = ["#ffffff", "#fff0e8", "#c8b89a", mix("#c8b89a", "#9a8fb0", 0.5)]
    for side in (False, True):
        w = a.layer()
        rows = [[(26, 18), (12, 9), (5, 10), (11, 15), (24, 22)],
                [(25, 21), (10, 15), (5, 18), (12, 21), (24, 25)],
                [(24, 24), (12, 22), (8, 26), (15, 27), (24, 27)]]
        for pts in rows:
            pts = mirror(pts) if side else pts
            fm = poly(pts)
            flat(w, fm, feather)
    body = a.layer()
    sphere(body, E(28, 27, 5, 4.5), feather)
    h = a.layer()
    baby_head(h, 28, 17, 7)
    h.fill([(23, 20), (33, 20)], PINK4[1])
    halo = a.layer()
    halo.fill(ring(28, 6.5, 6.5, 2.2, 1.2), GOLD4[1])
    halo.fill({(x, y) for x, y in ring(28, 6.5, 6.5, 2.2, 1.2) if y < 6}, GOLD4[0])
    hr = a.layer()
    sphere(hr, heart(46, 26, 3), BLOOD4)
    t = a.top()
    for x, y in ((8, 4), (49, 5), (52, 17), (6, 30)):
        spark(t, x, y, GOLD4[0])


@card("twisted_pair")
def twisted_pair(a):
    a.spot = (28, 16)
    a.glow_color = mix(GOLD4[1], "#9e1b1b", 0.35)
    cord = a.layer()
    for k, col in enumerate((BLOOD4, PURPLE4)):
        pts = [(x, 25 + 2.2 * math.sin((x - 20) * 0.55 + k * math.pi)) for x in range(19, 38)]
        tube(cord, pts, 0.8, 0.8, col)
    twin = ["#c8b89a", "#9a8fb0", mix("#9a8fb0", "#3a2c45", 0.5), "#3a2c45"]
    for cx in (16, 40):
        bd = a.layer()
        sphere(bd, E(cx, 26, 3.6, 3.4), twin)
        hd = a.layer()
        baby_head(hd, cx, 15, 7, twin, eye=OUT)
        hd.set(math.floor(cx - 2.7), 15, EYE_RED)
        hd.set(math.floor(cx + 2.7), 15, EYE_RED)
        for k in range(-2, 3):
            hd.set(cx + k, 19, OUT if k % 2 == 0 else twin[2])
        tube(hd, [(cx - 2.3, 18), (cx - 2.3, 22)], 0.8, 0.7, BLOOD4)
    t = a.top()
    for x, y in ((28, 6), (5, 5), (51, 31), (52, 6)):
        spark(t, x, y, GOLD4[0])


# ───────────────────────────── date + ieșire ─────────────────────────────

def load_cards():
    """Cărțile din Databases.cs: id, nume, erou, raritate, cost (în ordinea din pachete)."""
    src = open(DB_PATH, encoding="utf-8").read()
    rx = re.compile(r'Add\("([a-z0-9_]+)", "([^"]+)", "(azazel|cain|isaac|lilith)", ([scur]), (\d+),')
    return [{"id": m[0], "name": m[1], "hero": m[2], "rarity": m[3], "cost": int(m[4])} for m in rx.findall(src)]


def render_all(cards):
    out = {}
    for cd in cards:
        if cd["id"] not in CARDS:
            raise SystemExit("lipsește ilustrația pentru " + cd["id"])
        a = Art(cd)
        CARDS[cd["id"]](a)
        out[cd["id"]] = a.render()
    return out


def _font(size):
    return ImageFont.load_default(size=size)


RARITY_COL = {"s": "#9a9a9a", "c": P.RARITY["common"], "u": "#6fa8dc", "r": P.RARITY["rare"]}


def review_sheet(cards, arts, path, scale=6, cols=5):
    """Planșa ×6: câte două rânduri pe erou, fiecare celulă cu id, erou și raritate; jos, toate la mărimea din joc."""
    gap, label_h, head_h = 16, 44, 40
    cw, ch = W * scale, H * scale
    width = gap * 2 + cols * cw + (cols - 1) * gap
    rows = []
    for hero in HERO_ORDER:
        hc = [c for c in cards if c["hero"] == hero]
        rows.append((hero, [hc[i:i + cols] for i in range(0, len(hc), cols)]))
    small = 3          # mărimea din joc: 640×360 afișat ×3 la 1080p
    sw, sh = W * small, H * small
    scols = (width - 2 * gap + gap) // (sw + 8)
    srows = math.ceil(len(cards) / scols)
    height = gap + 56 + sum(head_h + len(r) * (ch + label_h + gap) for _, r in rows)
    height += head_h + srows * (sh + 8) + gap * 2 + 28 + H + gap
    img = Image.new("RGB", (width, height), "#141011")
    d = ImageDraw.Draw(img)
    d.text((gap, gap), "Darkest Basement - card illustrations 56x36 (x6)", fill="#e8dcc4", font=_font(30))
    y = gap + 56
    for hero, chunks in rows:
        d.text((gap, y + 6), hero.upper(), fill=BG[hero][3] if hero != "azazel" else EYE_RED, font=_font(26))
        d.rectangle([gap + 120, y + 20, width - gap, y + 21], fill=BG[hero][2])
        y += head_h
        for chunk in chunks:
            x = gap
            for cd in chunk:
                img.paste(arts[cd["id"]].to_image(scale).convert("RGB"), (x, y))
                d.text((x, y + ch + 4), cd["id"], fill="#e8dcc4", font=_font(18))
                d.text((x, y + ch + 24), "%s - %s" % (hero, RARITY_NAMES[cd["rarity"]]),
                       fill=RARITY_COL[cd["rarity"]], font=_font(15))
                x += cw + gap
            y += ch + label_h + gap
    d.text((gap, y + 6), "in-game size (x3 = 1080p), then x1", fill="#e8dcc4", font=_font(22))
    y += head_h
    for i, cd in enumerate(cards):
        img.paste(arts[cd["id"]].to_image(small).convert("RGB"),
                  (gap + (i % scols) * (sw + 8), y + (i // scols) * (sh + 8)))
    y += srows * (sh + 8) + gap
    for i, cd in enumerate(cards):
        img.paste(arts[cd["id"]].to_image(1).convert("RGB"), (gap + i * (W + 4), y))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=False)
    return img


# Cărțile din planșa cu rame + un text de efect provizoriu (din GDD §6), ca macheta să arate ca în mână.
FRAMED = {"brimstone_swipe": "Deal 6 damage.", "abaddon": "Gain 3 Strength. Lose 5 HP.",
          "moms_knife": "Deal 7 damage. Mark 2.", "dr_fetus": "Deal 6 damage to ALL enemies. 25% crit.",
          "the_sad_onion": "Deal 3 damage twice.", "polyphemus": "Deal 18 damage.",
          "box_of_friends": "Deal 3 damage 3 times.", "guardian_angel": "Heal 10. Gain 5 Block."}


def framed_sheet(cards, arts, path, scale=4):
    """Cărțile complete (ilustrație + ramă + nume + cost + bijuterie), ca în mână. Ramele vin din ui.py."""
    frames = {h: os.path.join(UI_DIR, "card_frame_%s.png" % h) for h in HERO_ORDER}
    meta_path = os.path.join(UI_DIR, "ui_slices.json")
    if not all(os.path.exists(p) for p in frames.values()) or not os.path.exists(meta_path):
        print("ramele lipsesc (rulează tools/art/ui.py): sar peste cards-framed.png")
        return None
    meta = json.load(open(meta_path, encoding="utf-8"))["card"]
    ax, ay = meta["artRect"][:2]
    nx, ny, nw, nh = meta["nameRect"]
    tx, ty, tw, th = meta["textRect"]
    gx, gy = meta["gemPos"]
    ox_, oy_ = meta["costOrbPos"]
    try:
        import envkit as ek
        f_name, f_cost = ek.font("micro"), ek.font("silkb")

        def text(im, xy, s, f, col, outline=None):
            ek.draw_text(im, xy, s, f, col, outline=outline, anchor="mt")

        def wrap(s, f, w):
            return ek.wrap_text(s, f, w)
    except Exception:          # fără fonturile pixel, planșa tot se face (cu fontul implicit)
        f_name = f_cost = ImageFont.load_default()

        def text(im, xy, s, f, col, outline=None):
            ImageDraw.Draw(im).text(xy, s, fill=col, font=f, anchor="mt")

        def wrap(s, f, w):
            return [s]
    by_id = {c["id"]: c for c in cards}
    pad = 6
    cw, ch = meta["size"][0] + pad * 2, meta["size"][1] + pad * 2
    cols = 4
    sheet = Image.new("RGBA", (cols * cw + 8, 2 * (ch + 10) + 6), "#141011")
    for i, cid in enumerate(FRAMED):
        cd = by_id[cid]
        im = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        im.alpha_composite(arts[cid].to_image(), (pad + ax, pad + ay))
        im.alpha_composite(Image.open(frames[cd["hero"]]).convert("RGBA"), (pad, pad))
        text(im, (pad + nx + nw // 2, pad + ny + 2), cd["name"], f_name, "#e8dcc4")
        for k, ln in enumerate(wrap(FRAMED[cid], f_name, tw - 4)[:4]):
            text(im, (pad + tx + tw // 2, pad + ty + 3 + k * 7), ln, f_name, INK)
        gem = os.path.join(UI_DIR, "gem_%s.png" % {"s": "common", "c": "common", "u": "uncommon", "r": "rare"}[cd["rarity"]])
        if os.path.exists(gem):
            im.alpha_composite(Image.open(gem).convert("RGBA"), (pad + gx, pad + gy))
        orb = os.path.join(UI_DIR, "cost_orb.png")
        if os.path.exists(orb):
            o = Image.open(orb).convert("RGBA")
            im.alpha_composite(o, (pad + ox_ + 2, pad + oy_ + 2))
            text(im, (pad + ox_ + 2 + o.width // 2, pad + oy_ + 2 + o.height // 2 - 3), str(cd["cost"]), f_cost,
                 "#ffffff", outline=OUT)
        sheet.alpha_composite(im, (4 + (i % cols) * cw, 4 + (i // cols) * (ch + 10)))
    big = sheet.resize((sheet.width * scale, sheet.height * scale), Image.NEAREST).convert("RGB")
    big.save(path, optimize=False)
    return big


def main():
    cards = load_cards()
    if len(cards) != 40:
        print("atenție: Databases.cs are %d cărți (mă așteptam la 40)" % len(cards))
    arts = render_all(cards)
    os.makedirs(CARDS_DIR, exist_ok=True)
    worst = 0
    for cd in cards:
        c = arts[cd["id"]]
        pk.save_png(c, os.path.join(CARDS_DIR, cd["id"] + ".png"))
        worst = max(worst, len(c.colors()))
    review_sheet(cards, arts, os.path.join(REVIEW_DIR, "cards.png"))
    framed_sheet(cards, arts, os.path.join(REVIEW_DIR, "cards-framed.png"))
    print("ok: %d ilustrații în %s (maxim %d culori pe carte)" % (len(cards), os.path.relpath(CARDS_DIR, ROOT), worst))


if __name__ == "__main__":
    main()
