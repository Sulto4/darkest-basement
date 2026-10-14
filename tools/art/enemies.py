"""Generatorul inamicilor comuni (16 tipuri), toți cu fața spre STÂNGA (stau în dreapta ecranului).

Rulare (din rădăcina repo-ului):  python3 tools/art/enemies.py
Scrie:
  Assets/_Game/Resources/Sprites/Enemies/<id>/{idle_0..1, attack_0..1, hurt_0, dead_0, portrait}.png
      (+ host: closed_0, open_0, open_1; globin: regen_0)
  docs/art-review/enemies.png              planșa ×6
  docs/art-review/enemies-in-context.png   macheta: eroii vs. inamicii fiecărui etaj (+ boss-ul etajului)

Aceeași metodă ca în heroes.py: fiecare parte a corpului e un strat (pânză separată), umbrit ca o
sferă cu lumina din stânga-sus și cu conturul lui de 1 px; straturile se suprapun, deci liniile
închise dintre cap și corp rămân. Ochii, gurile și sângele sunt pixeli puși de mână (pk.stamp).

Pânze: 32×32 pentru fly / spider / pooter, 48×48 pentru restul. Picioarele (sau baza) stau pe
penultimul rând, conturul lor pe ultimul, centrul pe x = W/2 (pivot jos-centru în Unity).
Cine zboară (fly, pooter) plutește câțiva pixeli mai sus și are umbra pe sol, semi-transparentă.

Cadrul „hurt” = poza de durere, turtită (mai lată, mai scundă) și „aprinsă” (culorile trase spre
alb, conturul rămâne închis). Cadrul „dead” = baltă de sânge cu bucăți din inamic.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pixkit as pk                                   # noqa: E402
from palette import (OUTLINE, SKIN, TEAR, BLOOD, POOP, BASEMENT, CAVES, DEPTHS,  # noqa: E402
                     DEPTHS_FLESH, UI, STATUS, AZAZEL, CAVES_SHROOM)

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Game", "Resources", "Sprites", "Enemies")
REVIEW = os.path.join(ROOT, "docs", "art-review")

O = OUTLINE
CUTS = (0.965, 0.2, -0.2)                            # aceleași praguri ca la capetele eroilor
LIGHT = (-0.42, -0.58, 0.7)                          # lumina din stânga-sus, ca în heroes.py

# ───────────────────────────── rampe de culoare ─────────────────────────────
# [highlight, bază, umbră, umbră adâncă]. Toate sunt nuanțe ale paletei din ART.md.

mx, tn = pk.mix, pk.tint

SKIN_R = list(SKIN)
SKIN_LINE = tn(SKIN[3], 0.55)
BLOOD_R = [tn(BLOOD[0], 1.3), BLOOD[0], BLOOD[1], BLOOD[2]]
GLOSS = tn(BLOOD[0], 1.65)                           # luciul de pe cheaguri
HOLE = mx(O, BLOOD[2], 0.35)                         # orbitele goale: negru cu o tentă de sânge
WHITE = "#fff0e8"                                    # „albul” jocului (highlight-ul pielii)
EYE_DARK = mx(O, TEAR[2], 0.18)                      # ochi albastru foarte închis (ca la Isaac)

# Gaper: piele palidă, cenușie, de cadavru (pielea lui Isaac trasă spre gri)
GAPER_R = [mx(SKIN[0], CAVES[0], 0.15), mx(SKIN[1], CAVES[0], 0.33), mx(SKIN[2], CAVES[1], 0.4),
           mx(SKIN[3], CAVES[2], 0.45)]
GAPER_LINE = mx(GAPER_R[3], O, 0.45)
# Horf: palid-gălbui, bolnăvicios (pielea trasă spre verdele de „poison”)
HORF_R = [mx(SKIN[0], STATUS["poison"], 0.12), mx(SKIN[1], STATUS["poison"], 0.24),
          mx(SKIN[2], STATUS["poison"], 0.3), mx(SKIN[3], CAVES[2], 0.4)]
HORF_LINE = mx(HORF_R[3], O, 0.45)
VEIN = mx(BLOOD[1], DEPTHS[0], 0.35)
# Muscă: aproape neagră, cu reflexe roșiatice (Attack Fly); aripi palide, translucide
FLY_R = [mx(DEPTHS[0], WHITE, 0.25), DEPTHS[0], DEPTHS[2], DEPTHS[3]]
WING_R = [mx(TEAR[0], WHITE, 0.5), mx(TEAR[0], CAVES[0], 0.35), mx(TEAR[1], CAVES[1], 0.55),
          CAVES[2]]
CLOT_R = BLOOD_R


# ───────────────────────────── unelte comune ─────────────────────────────

class Pose:
    """Parametrii unui cadru. Valorile implicite = repaus."""

    def __init__(self, **kw):
        self.t = 0            # indicele cadrului (picături care curg, aripi)
        self.ph = "idle"      # idle / wind / strike / hurt / dead (+ extra: closed / open / regen)
        self.bob = 0          # deplasarea pe verticală a corpului (negativ = sus)
        self.sq = 0           # turtire în px: mai scund cu sq, mai lat cu sq de fiecare parte
        self.lean = 0         # aplecare orizontală a capului / trunchiului (negativ = spre stânga)
        self.__dict__.update(kw)


def L(w=48, h=48):
    return pk.Canvas(w, h)


def fin(c):
    pk.outline(c)
    return c


def sphere(c, m, ramp, box=None, cuts=CUTS, light=LIGHT):
    pk.shade_sphere(c, m, ramp, cuts=cuts, light=light, box=box)


def stack(w, h, layers):
    c = L(w, h)
    for s in layers:
        if s is not None:
            c.paste(s)
    return c


def sqbox(box, sq, ground=None):
    """Cutia turtită: mai joasă cu sq (baza rămâne pe loc), mai lată cu sq în fiecare parte."""
    x0, y0, x1, y1 = box
    g = y1 if ground is None else ground
    k = (g - y0) and (g - y0 - sq) / (g - y0)
    ny0 = g - (g - y0) * k
    ny1 = g - (g - y1) * k
    return (x0 - sq, round(ny0), x1 + sq, round(ny1))


def mbox(box, dx=0, dy=0):
    x0, y0, x1, y1 = box
    return (x0 + dx, y0 + dy, x1 + dx, y1 + dy)


def head_mask_left(box):
    """Cap cu fața spre stânga: obrajii puțin mai plini spre față (jos-stânga)."""
    x0, y0, x1, y1 = box
    return pk.ellipse(x0, y0, x1, y1) | pk.ellipse(x0, y0 + 5, x1 - 4, y1)


def face_left(box):
    """Unde stau trăsăturile pe un cap 3/4 cu fața spre stânga (oglinda lui heroes.face_side)."""
    x0, y0, x1, y1 = box
    return {"nx": x0 + 7, "fx": x0 + 2, "ey": y1 - 13, "mx": x0 + 5, "my": y1 - 4}


def ground_shadow(c, cx, w, y1=None, a=150):
    """Umbra de pe sol a celor care plutesc (semi-transparentă, fără anti-aliasing)."""
    y1 = c.h - 1 if y1 is None else y1
    c.fill(pk.ellipse(cx - w // 2, y1 - 2, cx + (w + 1) // 2 - 1, y1), pk.with_alpha(O, a))


def drip(c, x, y, n, t, ramp=BLOOD_R, drop=True):
    """Dâră de sânge de 1 px care curge în jos; picătura de la capăt coboară de la un cadru la altul."""
    for i in range(n):
        c.set(x, y + i, ramp[1] if i < n - 1 else ramp[2])
    c.set(x, y, ramp[0] if n > 2 else ramp[1])
    if drop:
        c.set(x, y + n + (t % 2), ramp[2])


def fx_drops(w, h, pts, ramp=BLOOD_R):
    """Picături de 2×2 cu un pixel de luciu și contur (stropi care zboară)."""
    c = L(w, h)
    for x, y in pts:
        c.fill([(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)], ramp[1])
        c.set(x, y, ramp[0])
    return fin(c)


def fx_specks(c, pts, color=BLOOD[0]):
    """Stropi de 1 px fără contur (pulverizare fină)."""
    c.fill(pts, color)


def fx_shot(w, h, x, y, size=6, ramp=BLOOD_R):
    """Proiectil-lacrimă de sânge: sferă cu sclipire albă, contur închis (pereche cu lacrima lui Isaac)."""
    c = L(w, h)
    m = pk.ellipse(x, y, x + size - 1, y + size - 1)
    sphere(c, m, ramp, cuts=(0.9, 0.2, -0.3))
    c.set(x + 1, y + 1, "#ffffff")
    return fin(c)


def puddle(c, cx, y1, w, h=4, ramp=BLOOD_R, lobes=()):
    """Baltă de sânge plată: elipsă + lobi, margine închisă jos, luciu sus."""
    m = pk.ellipse(cx - w // 2, y1 - h + 1, cx + (w + 1) // 2 - 1, y1)
    for (lx, lw, lh) in lobes:
        m |= pk.ellipse(lx - lw // 2, y1 - lh + 1, lx + lw // 2, y1 - 1)
    c.fill(m, ramp[2])
    c.fill(pk.edge_band(m, 0, 1) | pk.edge_band(m, 1, 1), ramp[3])
    c.fill(pk.edge_band(m, 0, -1) & pk.edge_band(m, -1, -1), ramp[1])
    return m


def gib(w, h, x, y, rw, rh, ramp, bone=None):
    """O bucată de carne (gib): bulgăre umbrit, cu contur; opțional un os care iese."""
    c = L(w, h)
    m = pk.ellipse(x, y, x + rw - 1, y + rh - 1)
    if bone:
        c.fill(pk.line(x + rw - 2, y + 1, x + rw + 1, y - 1), bone)
    sphere(c, m, ramp)
    return fin(c)


def flash(c, t=0.5, color=WHITE):
    """Cadrul de lovitură: toate culorile trase spre alb, conturul rămâne (se citește forma)."""
    out = L(c.w, c.h)
    o = pk.norm(O)
    for y in range(c.h):
        for x in range(c.w):
            col = c.get(x, y)
            if col:
                if col == o or len(col) == 9:
                    out.set(x, y, col)
                else:
                    out.set(x, y, mx(col, color, t))
    return out


def recenter(c, cx=None, ground=None):
    """Mută conținutul ca centrul siluetei să fie pe cx (pivot jos-centru), fără să-l schimbe."""
    m = c.mask()
    if not m:
        return c
    x0, y0, x1, y1 = pk.bbox(m)
    cx = c.w // 2 if cx is None else cx
    dx = cx - (x0 + x1 + 1) // 2
    dy = 0 if ground is None else ground - y1
    return pk.shifted(c, dx, dy) if (dx or dy) else c


def disc(cx, cy, r):
    """Disc plin cu centru real (cx, cy): pixelii cu centrul la distanță ≤ r."""
    pts = set()
    for y in range(int(math.floor(cy - r)) - 1, int(math.ceil(cy + r)) + 1):
        for x in range(int(math.floor(cx - r)) - 1, int(math.ceil(cx + r)) + 1):
            if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                pts.add((x, y))
    return pts


def tube(pts, step=0.5):
    """Mască de „tub” (tentacul, tulpină): discuri de-a lungul unei linii frânte; pts = [(x, y, r), ...]."""
    m = set()
    for (x0, y0, r0), (x1, y1, r1) in zip(pts, pts[1:]):
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) / step))
        for i in range(n + 1):
            u = i / n
            m |= disc(x0 + (x1 - x0) * u, y0 + (y1 - y0) * u, r0 + (r1 - r0) * u)
    return m


def wing_pair(w, h, cx, cy, up):
    """Două aripi de insectă, translucide-palide, în spatele corpului: ridicate (up) sau lăsate pe spate."""
    out = L(w, h)
    for k, (ox, oy) in enumerate([(3, 0), (0, 1)]):
        if up:
            wm = pk.ellipse(cx - 1 + ox, cy - 11 + oy, cx + 5 + ox, cy - 2 + oy)
            wm |= pk.ellipse(cx + 1 + ox, cy - 12 + oy, cx + 6 + ox, cy - 6 + oy)
        else:
            wm = pk.ellipse(cx + 2 + ox, cy - 6 + oy, cx + 11 + ox, cy - 1 + oy)
        r = WING_R if k == 1 else [WING_R[1], WING_R[2], WING_R[2], WING_R[3]]
        layer = L(w, h)
        pk.shade_flat(layer, wm, r, shadow=(1, 1), deep=None, light_rim=(-1, -1))
        x0, y0, x1, y1 = pk.bbox(wm)
        layer.fill(pk.line(x0 + 1, y1 - 1, x1 - 1, y0 + 1) if up else pk.line(x0 + 1, y1 - 1, x1 - 1, y0 + 2), r[2])
        out.paste(fin(layer))
    return out


# ───────────────────────────── stampile (ochi, guri) ─────────────────────────────
# Toate privesc spre stânga. K = negru (contur), k = orbită (negru-roșiatic), W = sclipire,
# r/R/H = sânge închis / sânge / sânge deschis, L = linie pe piele, F = dinte, E = ochi închis.

def key(line=SKIN_LINE):
    return {"K": O, "o": O, "k": HOLE, "W": WHITE, "w": TEAR[0], "r": BLOOD[2], "R": BLOOD[1],
            "H": BLOOD[0], "h": GLOSS, "L": line, "F": WHITE, "f": UI["parchment"][1],
            "E": EYE_DARK, "x": None}


HOLLOW_NEAR = [".KKKK.",              # orbita goală a lui Gaper, ochiul apropiat
               "KkkkkK",
               "KkkkkK",
               "KkkkkK",
               "KkkkkK",
               ".KRRK."]
HOLLOW_FAR = ["KK.",                  # cel îndepărtat, strivit de perspectivă
              "kkK",
              "kkK",
              "kkK",
              "kkK",
              "RK."]
GAPE = [".KKKK.",                     # gura căscată
        "KkkkkK",
        "KkrrkK",
        "KkrRkK",
        "KkrRkK",
        "KkkkkK",
        ".KKKK."]
GAPE_WIDE = [".KKKKK.",
             "KkkkkkK",
             "KkrrrkK",
             "KkrRRkK",
             "KkrRRkK",
             "KkrRRkK",
             "KkkrkkK",
             ".KKKKK."]
GAPE_SMALL = [".KKK.",
              "KkkkK",
              "KkrkK",
              "KkkkK",
              ".KKK."]
EYE_X = ["K...K", ".K.K.", "..K..", ".K.K.", "K...K"]
EYE_X_SMALL = ["K.K", ".K.", "K.K"]


# ───────────────────────────── GAPER ─────────────────────────────
# Omulețul palid cu orbitele goale și gura căscată, care vine spre tine cu mâinile întinse.

def gaper_head(box, pose, ramp=GAPER_R, line=GAPER_LINE):
    c = L()
    x0, y0, x1, y1 = box
    m = head_mask_left(box)
    sphere(c, m, ramp, box)
    f = face_left(box)
    k = key(line)
    nx, fx, ey, mxp, my = f["nx"], f["fx"], f["ey"] - 3, f["mx"] - 1, f["my"]
    t, ph = pose.t, pose.ph
    # sprâncenele și pomeții: umbra din jurul orbitelor (fața scobită)
    c.fill([(nx + i, ey - 1) for i in range(1, 5)], ramp[2])
    c.fill([(fx + i, ey - 1) for i in range(0, 2)], ramp[2])
    if ph == "hurt":
        pk.stamp(c, nx, ey + 1, ["KK...", "..KKK", "KK..."], k)          # orbite strânse
        pk.stamp(c, fx, ey + 1, ["..K", "KK.", "..K"], k)
        pk.stamp(c, mxp - 1, my - 5, GAPE_WIDE, k)
    elif ph == "dead":
        pk.stamp(c, nx, ey, EYE_X, k)
        pk.stamp(c, fx, ey + 1, EYE_X_SMALL, k)
        pk.stamp(c, mxp, my - 2, ["KKKK.", ".HH..", ".HH.."], k)
    else:
        pk.stamp(c, nx, ey, HOLLOW_NEAR, k)
        pk.stamp(c, fx, ey, HOLLOW_FAR, k)
        if ph == "strike":
            pk.stamp(c, mxp - 1, my - 5, GAPE_WIDE, k)
        else:
            pk.stamp(c, mxp, my - 4 + (1 if ph == "wind" else 0), GAPE, k)
        # lacrimi de sânge din orbite (curg între cadre)
        drip(c, nx + 2, ey + 6, 3 + (t % 2), t)
        drip(c, fx, ey + 6, 2, t + 1)
    # sânge pe bărbie, din colțul gurii
    if ph in ("idle", "wind", "strike"):
        drip(c, mxp + 4, my + 2, 2, t, drop=False)
    return fin(c)


def gaper_body(pose, ramp=GAPER_R, arms="reach"):
    """Trunchiul mic + picioarele-butuc. Întoarce (picior îndepărtat, trunchi, picior apropiat)."""
    far, body, near = L(), L(), L()
    sq, bob, lean = pose.sq, pose.bob, pose.lean
    dim = [ramp[2], ramp[2], ramp[3], ramp[3]]
    fl = pk.rect(26, 42, 28, 46) | pk.rect(25, 45, 27, 46)
    nl = pk.rect(21, 42, 23, 46) | pk.rect(20, 45, 22, 46)
    if pose.ph == "strike":                              # pas mare înainte
        nl = pk.shift(nl, -2, 0)
    pk.shade_flat(far, fl, dim, shadow=(1, 1), deep=None)
    pk.shade_flat(near, nl, ramp, shadow=(1, 1), deep=None)
    tb = sqbox((18 + lean, 30 + bob, 30 + lean, 43), sq, ground=43)
    tm = pk.ellipse(*tb, fat=0.15)
    pk.shade_flat(body, tm, ramp, shadow=(2, 1), deep=(1, 1), light_rim=(-1, -1))
    cx = (tb[0] + tb[2]) // 2
    body.set(cx - 1, tb[3] - 3, ramp[3])                 # buricul
    for yy in (tb[1] + 3, tb[1] + 5):                    # coaste
        body.fill([(cx - 3 + i, yy) for i in range(3)], ramp[2])
    return fin(far), fin(body), fin(near)


def gaper_arm(pose, ramp=GAPER_R, which="near"):
    """Brațele întinse înainte, ca la zombi (pentru Grab). Mâna are trei degete-cârlig."""
    c = L()
    bob, lean = pose.bob, pose.lean
    ph = pose.ph
    oy = bob + pose.sq
    if ph == "wind":                                     # brațele trase înapoi, gata să apuce
        sh = (24 + lean, 33 + oy)
        hand = (34 + lean, 27 + oy)
    elif ph == "strike":                                 # întinse departe, apucă
        sh = (20 + lean, 33 + oy)
        hand = (4 + lean, 31 + oy)
    elif ph == "hurt":                                   # aruncate înapoi
        sh = (24 + lean, 33 + oy)
        hand = (33 + lean, 28 + oy)
    else:
        sh = (21 + lean, 33 + oy)
        hand = (9 + lean, 33 + oy + (pose.t % 2))
    if which == "far":
        sh = (sh[0] + 4, sh[1] - 1)
        hand = (hand[0] + 3, hand[1] - 2)
    m = set()
    for i in range(0, 3):                                # brațul: bandă de 3 px
        m |= set(pk.line(sh[0], sh[1] + i - 1, hand[0], hand[1] + i - 1))
    hm = pk.ellipse(hand[0] - 2, hand[1] - 2, hand[0] + 2, hand[1] + 2)
    m |= hm
    r = ramp if which == "near" else [ramp[2], ramp[2], ramp[3], ramp[3]]
    pk.shade_flat(c, m, r, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    if ph not in ("wind", "hurt"):                       # degetele-cârlig spre stânga
        for dy in (-2, 0, 2):
            c.set(hand[0] - 3, hand[1] + dy, r[2])
    return fin(c)


def draw_gaper(pose):
    head_box = (12, 6, 35, 30)
    hb = mbox(head_box, pose.lean, pose.bob + pose.sq)
    if pose.ph == "wind":
        hb = mbox(hb, 2, 1)
    elif pose.ph == "strike":
        hb = mbox(hb, -3, 1)
    elif pose.ph == "hurt":
        hb = mbox(hb, 3, 2)
    far, body, near = gaper_body(pose)
    layers = [gaper_arm(pose, which="far"), far, body, near, gaper_head(hb, pose),
              gaper_arm(pose, which="near")]
    c = stack(48, 48, layers)
    if pose.ph == "strike":
        c.paste(fx_drops(48, 48, [(5, 24), (2, 28), (9, 21)]))
    return c


def dead_gaper(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 42, 6, lobes=[(10, 12, 5), (38, 10, 4)])
    c.paste(fin(pud))
    for (x, y, w_, h_) in [(33, 39, 7, 5), (6, 41, 5, 4), (38, 42, 5, 4)]:
        c.paste(gib(48, 48, x, y, w_, h_, GAPER_R, bone=UI["parchment"][0]))
    hb = (10, 25, 31, 45)
    c.paste(gaper_head(hb, Pose(ph="dead")))
    sp = L()
    sp.fill([(3, 36), (4, 36), (44, 35), (41, 31), (42, 31)], BLOOD[0])
    c.paste(fin(sp))
    return c


# ───────────────────────────── FLY ─────────────────────────────
# Musca de atac: bilă aproape neagră, ochi compuși roșii, aripi palide care bat între cadre.

def fly_canvas(pose, cx=16, cy=15, big=1.0, shadow=True):
    w = h = 32
    c = L(w, h)
    if shadow:
        ground_shadow(c, 16, 10)
    t, ph = pose.t, pose.ph
    cy += pose.bob
    # aripile (în spatele corpului): sus / jos după cadru
    up = (t % 2 == 0) and ph != "strike"
    c.paste(wing_pair(w, h, cx, cy, up))
    # picioarele care atârnă
    legs = L(w, h)
    for lx, ln in ((cx - 2, 3), (cx + 1, 4), (cx + 4, 3)):
        legs.fill(pk.line(lx, cy + 4, lx - 1 + (t % 2), cy + 4 + ln), FLY_R[3])
    c.paste(legs)
    # corpul: abdomen rotund + cap mic în față
    body = L(w, h)
    bm = pk.ellipse(cx - 4, cy - 4, cx + 5, cy + 5)
    sphere(body, bm, FLY_R, cuts=(0.9, 0.25, -0.15))
    for yy in (cy + 1, cy + 3):                          # inelele abdomenului
        body.fill([(x, yy) for x in range(cx + 1, cx + 5) if (x, yy) in bm], FLY_R[3])
    rim = pk.edge_band(bm, 1, 1) & pk.edge_band(bm, 1, 0)
    body.fill({p for p in rim if p[1] > cy}, BLOOD[2])     # marginea roșiatică (Attack Fly)
    fin(body)
    head = L(w, h)
    hm = pk.ellipse(cx - 8, cy - 4, cx - 1, cy + 3)
    sphere(head, hm, FLY_R, cuts=(0.9, 0.25, -0.15))
    # ochii compuși: mari, roșii, cu sclipire
    ek = {"R": BLOOD[0], "r": BLOOD[1], "H": tn(BLOOD[0], 1.5), "W": WHITE, "K": O}
    if ph == "hurt":
        pk.stamp(head, cx - 8, cy - 2, ["RR.", "..R", "RR."], ek)
    else:
        pk.stamp(head, cx - 7, cy - 3, [".RR.", "RWHR", "RHRr", ".rr."], ek)
        head.set(cx - 8, cy, BLOOD[1])
    # trompa / gura
    if ph in ("strike", "wind"):
        head.fill([(cx - 9, cy + 2), (cx - 10, cy + 3)], O)
    fin(head)
    c.paste(body)
    c.paste(head)
    return c


def draw_fly(pose):
    if pose.ph == "strike":
        c = fly_canvas(pose, cx=13, cy=17)
        lines = L(32, 32)                                # liniile de viteză: se aruncă spre stânga
        for (x0, y0, n) in [(22, 13, 5), (24, 17, 6), (22, 21, 4)]:
            lines.fill([(x0 + i, y0) for i in range(n)], mx(WING_R[1], O, 0.3))
        c.paste(lines)
        c.paste(fx_drops(32, 32, [(2, 13)]))
        return c
    if pose.ph == "wind":
        return fly_canvas(pose, cx=18, cy=12)
    return fly_canvas(pose)


def dead_fly(pose):
    c = L(32, 32)
    pud = L(32, 32)
    puddle(pud, 16, 30, 18, 3, lobes=[(9, 6, 3)])
    c.paste(fin(pud))
    w = L(32, 32)                                        # o aripă ruptă, căzută
    pk.shade_flat(w, pk.ellipse(18, 24, 25, 27), WING_R, shadow=(1, 1), deep=None)
    c.paste(fin(w))
    b = L(32, 32)                                        # corpul turtit, cu picioarele în sus
    bm = pk.ellipse(10, 24, 19, 29)
    sphere(b, bm, FLY_R, cuts=(0.9, 0.25, -0.15))
    for lx in (12, 15, 17):
        b.fill(pk.line(lx, 23, lx + 1, 21), FLY_R[3])
    fin(b)
    pk.stamp(b, 11, 25, ["K.K", ".K.", "K.K"], {"K": BLOOD[0]})
    c.paste(b)
    return c


# ───────────────────────────── CLOTTY ─────────────────────────────
# Cheagul: o grămadă de sânge închegat din mai mulți „bulgări”, fiecare cu luciul lui,
# cu ochi mici și negri. Bulgării se umbresc separat, din spate spre față, ca să apară cutele.

CLOT_LUMPS = [(24, 27, 5), (17, 31, 5.5), (31, 31, 5.5), (12, 38, 5.5), (36, 38, 5.5),
              (24, 35, 8), (18, 41, 6.5), (30, 41, 6.5)]


def clotty_lumps(sq=0, puff=0):
    out = []
    for cx_, cy_, r in CLOT_LUMPS:
        k = 1 + puff * 0.06
        x = 24 + (cx_ - 24) * (1 + sq * 0.1) * k
        y = 46 - (46 - cy_) * (1 - sq * 0.11) * k
        out.append((x, y, r * k * (1 + sq * 0.03)))
    return out


def draw_clotty(pose):
    c = L()
    ph, t = pose.ph, pose.t
    puff = 2 if ph == "wind" else 0
    sq = pose.sq + (3 if ph == "strike" else 0)
    lumps = clotty_lumps(sq, puff)
    body = L()
    whole = set()
    for (x, y, r) in sorted(lumps, key=lambda l: l[1]):     # din spate (sus) spre față (jos)
        m = {p for p in disc(x, y, r) if p[1] <= 46}
        whole |= m
        sphere(body, m, CLOT_R, box=(x - r, y - r, x + r, y + r), cuts=(0.9, 0.25, -0.3))
        body.set(int(x - r * 0.45), int(y - r * 0.55), GLOSS)        # luciul ud al fiecărui bulgăre
    x0, y0, x1, y1 = pk.bbox(whole)
    # pete de sânge închegat (mai închise), în model fix
    for (bx, by) in [(x0 + 15, y0 + 6), (x1 - 6, y0 + 13), (x0 + 4, y1 - 5), (x1 - 12, y1 - 3)]:
        body.fill({p for p in pk.ellipse(bx, by, bx + 2, by + 1) if p in whole}, mx(BLOOD[2], DEPTHS_FLESH, 0.5))
    body.fill([(x0 + 10, y0 + 2), (x0 + 11, y0 + 2)], WHITE)
    # fața, pe bulgărele din față
    k = key()
    ex, ey = x0 + 7, y0 + 11 - puff
    if ph == "hurt":
        pk.stamp(body, ex, ey, ["KK..KK.", "..K...K", "KK..KK."], k)
        pk.stamp(body, ex + 2, ey + 4, [".KK.", "KrrK", ".KK."], k)
    elif ph == "strike":
        pk.stamp(body, ex, ey, ["KKK.KK", "KKK.KK"], k)
        pk.stamp(body, ex + 1, ey + 3, [".KKK.", "KkrkK", "KkRkK", ".KKK."], k)
    else:
        pk.stamp(body, ex, ey, ["KK..KK", "KW..KW", "KK..KK"], k)
        if ph == "wind":
            pk.stamp(body, ex + 1, ey + 5, ["KKKK"], k)
        else:
            pk.stamp(body, ex + 1, ey + 4, ["K..K", ".KK."], k)
    fin(body)
    # sângele care se scurge pe margini (peste contur, ca o prelingere)
    drip(body, x0 + 1, y1 - 7, 3 + t % 2, t, drop=False)
    drip(body, x1 - 2, y1 - 5, 2 + (t + 1) % 2, t, drop=False)
    pud = L()
    puddle(pud, 24, 46, 38 + sq * 2, 3, lobes=[(9 - sq, 8, 3)])
    c.paste(fin(pud))
    c.paste(body)
    if ph == "strike":                                   # Clot Burst: stropi în toate părțile
        c.paste(fx_drops(48, 48, [(4, 22), (10, 13), (37, 12), (43, 21), (24, 8), (1, 31)]))
        fx_specks(c, [(8, 18), (16, 10), (31, 9), (41, 16), (4, 27), (20, 5)])
    return c


def dead_clotty(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 44, 7, lobes=[(8, 12, 5), (40, 12, 6), (24, 18, 8)])
    c.paste(fin(pud))
    for (x, y, w_, h_) in [(13, 37, 8, 6), (27, 36, 7, 6), (35, 41, 5, 4), (6, 42, 4, 3)]:
        c.paste(gib(48, 48, x, y, w_, h_, CLOT_R))
    k = key()
    pk.stamp(c, 15, 39, ["K.K", ".K.", "K.K"], k)
    pk.stamp(c, 29, 38, ["K.K", ".K.", "K.K"], k)
    c.fill([(20, 44), (21, 44), (31, 45)], GLOSS)
    return c


# ───────────────────────────── HORF ─────────────────────────────
# Capul fără corp care tremură și scuipă sânge: palid-gălbui, încruntat, vene pe frunte.

def horf_head(box, pose):
    c = L()
    x0, y0, x1, y1 = box
    ph, t = pose.ph, pose.t
    m = head_mask_left(box)
    if ph == "wind":                                      # obrajii umflați
        m |= pk.ellipse(x0 - 1, y1 - 10, x0 + 9, y1)
    sphere(c, m, HORF_R, box)
    k = key(HORF_LINE)
    f = face_left(box)
    nx, fx, ey, mxp, my = f["nx"], f["fx"], f["ey"], f["mx"], f["my"]
    # venele de pe frunte și tâmplă
    c.fill(pk.polyline([(x0 + 13, y0 + 3), (x0 + 12, y0 + 5), (x0 + 14, y0 + 7)]), VEIN)
    c.fill(pk.polyline([(x1 - 4, y0 + 7), (x1 - 6, y0 + 9), (x1 - 5, y0 + 11)]), VEIN)
    if ph == "hurt":
        pk.stamp(c, nx, ey + 1, ["KK...", "..KKK", "KK..."], k)
        pk.stamp(c, fx, ey + 1, ["..K", "KK.", "..K"], k)
        pk.stamp(c, mxp, my - 4, [".KKK.", "KkrkK", "KkRkK", ".KKK."], k)
        return fin(c)
    # sprâncene groase, încruntate (în V spre nas)
    c.fill(pk.line(nx, ey - 3, nx + 5, ey - 1), HORF_LINE)
    c.fill(pk.line(fx - 1, ey - 1, fx + 2, ey - 2), HORF_LINE)
    # ochi mici, injectați, cu cearcăne
    eye = ["WWWE.", "WREEL", ".LLL."] if ph != "wind" else ["WWWWE", "WRWEE", "WWWWL", ".LLL."]
    pk.stamp(c, nx + 1, ey, eye, {**k, "R": BLOOD[0]})
    pk.stamp(c, fx, ey, ["WE", "EE", "L."], k)
    if ph == "strike":
        pk.stamp(c, mxp - 1, my - 6, GAPE_WIDE, k)
    elif ph == "wind":
        c.fill(pk.line(mxp, my - 2, mxp + 4, my - 2), HORF_LINE)          # buze strânse, pline
        c.fill([(mxp + 1, my - 1), (mxp + 2, my - 1), (mxp + 3, my - 1)], HORF_R[2])
    else:
        pk.stamp(c, mxp, my - 3, [".KKK.", "KkrkK", "KkRkK", ".KKK."], k)   # gura deschisă, horcăie
        drip(c, mxp + 2, my + 1, 1 + (t % 2), t, drop=False)
    # cicatricea gâtului retezat, jos
    return fin(c)


def horf_stump(box, pose):
    c = L()
    x0, y0, x1, y1 = box
    cx = (x0 + x1) // 2 + 2
    m = pk.ellipse(cx - 6, y1 - 3, cx + 5, y1 + 3)
    pk.shade_flat(c, m, HORF_R, shadow=(1, 1), deep=None)
    c.fill(pk.ellipse(cx - 4, y1 + 0, cx + 3, y1 + 3), BLOOD[1])
    c.fill(pk.ellipse(cx - 2, y1 + 1, cx + 1, y1 + 2), BLOOD[2])
    drip(c, cx - 1, y1 + 4, 1 + pose.t % 2, pose.t, drop=False)
    return fin(c)


def draw_horf(pose):
    c = L()
    ph, t = pose.ph, pose.t
    shake = {0: 0, 1: 1}[t % 2] if ph == "idle" else 0
    hover = -4 + pose.bob
    box = (12 + shake, 14 + hover, 35 + shake, 37 + hover)
    if ph == "wind":
        box = mbox(box, 2, -1)
    elif ph == "strike":
        box = mbox(box, 2, 0)
    elif ph == "hurt":
        box = mbox(box, 3, 1)
    box = sqbox(box, pose.sq)
    ground_shadow(c, 24, 18)
    c.paste(horf_stump(box, pose))
    c.paste(horf_head(box, pose))
    if ph == "strike":
        c.paste(fx_shot(48, 48, 1, 26, 7))
        c.paste(fx_drops(48, 48, [(9, 33), (11, 23)]))
    return c


def dead_horf(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 40, 6, lobes=[(12, 12, 5)])
    c.paste(fin(pud))
    box = (11, 24, 33, 45)
    h = L()
    m = head_mask_left(box)
    sphere(h, m, HORF_R, box)
    f = face_left(box)
    k = key(HORF_LINE)
    pk.stamp(h, f["nx"], f["ey"], EYE_X, k)
    pk.stamp(h, f["fx"], f["ey"] + 1, EYE_X_SMALL, k)
    pk.stamp(h, f["mx"], f["my"] - 3, [".KKK.", "KkrkK", ".KHK."], k)
    c.paste(fin(h))
    c.fill([(f["mx"] + 2, f["my"] + 1), (f["mx"] + 2, f["my"] + 2)], BLOOD[0])
    return c


# ───────────────────────────── POOTER ─────────────────────────────
# Musca-grăsană: corp rotund, palid-vânăt, ochi mari cu pleoape grele, buze roșii țuguiate.

POOTER_R = [mx(SKIN[0], DEPTHS[0], 0.2), mx(SKIN[1], DEPTHS[0], 0.45), mx(SKIN[2], DEPTHS[1], 0.55),
            mx(SKIN[3], DEPTHS[2], 0.6)]
POOTER_LINE = mx(POOTER_R[3], O, 0.4)


def draw_pooter(pose):
    c = L(32, 32)
    ph, t, sq = pose.ph, pose.t, pose.sq
    ground_shadow(c, 16, 12)
    cx, cy = 17, 13 + pose.bob
    if ph == "wind":
        cx, cy = 19, 12
    elif ph == "strike":
        cx = 16
    elif ph == "hurt":
        cx, cy = 18, 14
    c.paste(wing_pair(32, 32, cx + 1, cy - 3, up=(t % 2 == 0) and ph not in ("strike", "hurt")))
    legs = L(32, 32)
    for lx, ln in ((cx - 3, 3), (cx + 1, 4), (cx + 4, 3)):
        legs.fill(pk.line(lx, cy + 6, lx - 1 + (t % 2), cy + 6 + ln), POOTER_R[3])
    c.paste(legs)
    body = L(32, 32)
    bb = (cx - 7 - sq, cy - 6 + sq, cx + 7 + sq, cy + 7)
    bm = pk.ellipse(*bb)
    if ph == "wind":                                      # obrajii umflați: ține scuipatul în gură
        bm |= pk.ellipse(cx - 9, cy, cx - 1, cy + 7)
    sphere(body, bm, POOTER_R, bb)
    for yy in (cy + 2, cy + 4):                           # dungile de muscă pe abdomen
        body.fill([(x, yy) for x in range(cx + 3, cx + 8) if (x, yy) in bm and (x + 1, yy) in bm],
                  POOTER_R[3])
    k = {**key(POOTER_LINE), "l": POOTER_R[2]}
    if ph == "hurt":
        pk.stamp(body, cx - 5, cy - 2, ["KK...", "..KKK", "KK..."], k)
        pk.stamp(body, cx - 8, cy + 3, [".KK.", "KrrK", ".KK."], k)
    else:
        wide = ph in ("wind", "strike")
        pk.stamp(body, cx - 5, cy - 5, [".KKKK.",
                                         "KllllK" if not wide else "KWWWWK",
                                         "KEEWWK",
                                         "KEEWWK",
                                         "KWWWWK",
                                         ".KKKK."], k)
        pk.stamp(body, cx - 8, cy - 4, ["KK", "EK", "EK", "KK"], k)
        if ph == "wind":
            pk.stamp(body, cx - 9, cy + 3, [".RR", "RHR", ".RR"], {"R": BLOOD[1], "H": BLOOD[0]})
        elif ph == "strike":
            pk.stamp(body, cx - 9, cy + 1, [".RRR.", "RkkkR", "RkrkR", ".RRR."], {**k, "R": BLOOD[1]})
        else:
            pk.stamp(body, cx - 8, cy + 2, [".HR.", "RkkR", ".RR."], {**k, "R": BLOOD[1]})
            drip(body, cx - 6, cy + 5, 1 + t % 2, t, drop=False)
    c.paste(fin(body))
    if ph == "strike":
        c.paste(fx_shot(32, 32, 0, cy - 1, 5))
        c.paste(fx_drops(32, 32, [(4, cy + 6)]))
    return c


def dead_pooter(pose):
    c = L(32, 32)
    pud = L(32, 32)
    puddle(pud, 16, 30, 24, 4, lobes=[(6, 6, 3)])
    c.paste(fin(pud))
    w = L(32, 32)
    pk.shade_flat(w, pk.ellipse(19, 21, 27, 25), WING_R, shadow=(1, 1), deep=None)
    c.paste(fin(w))
    b = L(32, 32)
    bb = (6, 21, 21, 30)
    sphere(b, pk.ellipse(*bb), POOTER_R, bb)
    k = key(POOTER_LINE)
    pk.stamp(b, 10, 23, EYE_X, k)
    pk.stamp(b, 7, 24, EYE_X_SMALL, k)
    pk.stamp(b, 8, 28, ["HH"], k)
    c.paste(fin(b))
    return c


# ───────────────────────────── MULLIGAN ─────────────────────────────
# Sacul umflat, palid-gălbui, plin de găuri din care ies muște; fața mică și tristă, sus.

MULL_R = [mx(SKIN[0], UI["parchment"][0], 0.5), mx(SKIN[1], UI["parchment"][1], 0.45),
          mx(SKIN[2], POOP[1], 0.25), mx(SKIN[3], POOP[2], 0.35)]
MULL_LINE = mx(MULL_R[3], O, 0.4)
MULL_VEIN = mx(TEAR[2], MULL_R[2], 0.5)
BRUISE = mx(DEPTHS[0], MULL_R[2], 0.45)


def mini_fly(w, h, x, y, t):
    """Muscă mică (iese din Mulligan sau zboară în jurul lui): corp de 3×2, ochi roșu, aripă."""
    c = L(w, h)
    c.fill([(x, y), (x + 1, y), (x + 2, y), (x + 1, y + 1), (x + 2, y + 1)], FLY_R[1])
    c.set(x, y, BLOOD[0])
    c.fill([(x + 1, y - 1), (x + 2, y - 2)] if t % 2 == 0 else [(x + 2, y - 1), (x + 3, y - 1)], WING_R[0])
    return fin(c)


def draw_mulligan(pose):
    ph, t, sq = pose.ph, pose.t, pose.sq
    lean = {"wind": 2, "strike": -3, "hurt": 2}.get(ph, 0)
    puff = 2 if ph == "wind" else 0
    far, near, body, arm = L(), L(), L(), L()
    fl = pk.rect(27, 41, 30, 46) | pk.rect(26, 45, 29, 46)
    nl = pk.rect(18, 41, 21, 46) | pk.rect(17, 45, 20, 46)
    if ph == "strike":
        nl = pk.shift(nl, -2, 0)
    pk.shade_flat(far, fl, [MULL_R[2], MULL_R[2], MULL_R[3], MULL_R[3]], shadow=(1, 1), deep=None)
    pk.shade_flat(near, nl, MULL_R, shadow=(1, 1), deep=None)
    bb = sqbox((10 + lean - puff, 7 - puff * 2 + pose.bob, 37 + lean + puff, 44), sq, ground=44)
    bm = pk.ellipse(*bb, fat=0.08) | pk.ellipse(bb[0] + 1, bb[1] + 12, bb[2], bb[3] + 1)
    sphere(body, bm, MULL_R, bb)
    x0, y0, x1, y1 = bb
    # vene albăstrui și vânătăi: pielea întinsă la maximum
    body.fill(pk.polyline([(x1 - 6, y0 + 8), (x1 - 8, y0 + 12), (x1 - 7, y0 + 16), (x1 - 9, y0 + 19)]), MULL_VEIN)
    body.fill(pk.polyline([(x0 + 13, y1 - 8), (x0 + 15, y1 - 5), (x0 + 14, y1 - 2)]), MULL_VEIN)
    for (bx, by) in [(x1 - 9, y1 - 12), (x0 + 4, y0 + 22)]:
        pk.dither_fill(body, pk.ellipse(bx, by, bx + 4, by + 3) & bm, BRUISE, 0.5)
    # găurile din care ies muștele
    holes = [(x0 + 15, y0 + 16), (x0 + 20, y0 + 7), (x0 + 9, y0 + 26), (x0 + 19, y0 + 27)]
    for i, (hx, hy) in enumerate(holes):
        pk.stamp(body, hx, hy, [".bb.", "bkkb", ".bb."], {"b": MULL_R[3], "k": HOLE})
    # o muscă își scoate capul dintr-o gaură (alternează între cadre)
    hx, hy = holes[t % 2]
    body.fill([(hx + 1, hy + 1), (hx + 2, hy + 1)], FLY_R[1])
    body.set(hx + 1, hy + 1, BLOOD[0])
    # fața: mică, tristă, sus în stânga
    k = key(MULL_LINE)
    fx, fy = x0 + 4, y0 + 8
    if ph == "hurt":
        pk.stamp(body, fx, fy, ["KK..KK.", "..K...K", "KK..KK."], k)
        pk.stamp(body, fx + 1, fy + 5, [".KK.", "KrrK", ".KK."], k)
    else:
        pk.stamp(body, fx, fy - 2, ["LL..LL", "......", "KK..KK", "KW..KW", "KK..KK"], k)
        mouth = {"wind": ["LLL"], "strike": [".KK.", "KrrK", ".KK."]}.get(ph, [".K.", "KrK", ".K."])
        pk.stamp(body, fx + 1, fy + 5, mouth, k)
        if ph == "idle":
            drip(body, fx + 1, fy + 3, 2 + t % 2, t, ramp=[TEAR[0], TEAR[1], TEAR[2]], drop=False)
    fin(body)
    # brațul-ciot din față
    am = pk.ellipse(x0 + 2, y0 + 19 + (t % 2), x0 + 7, y0 + 25 + (t % 2))
    if ph == "strike":
        am = pk.ellipse(x0 - 3, y0 + 18, x0 + 4, y0 + 23)
    pk.shade_flat(arm, am, MULL_R, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    c = stack(48, 48, [far, body, near, fin(arm)])
    # muștele care roiesc în jur
    flies = [(4, 10), (40, 6)] if t % 2 == 0 else [(6, 7), (41, 10)]
    if ph == "strike":
        flies = [(1, 12), (8, 4), (42, 8)]
    for i, (fx_, fy_) in enumerate(flies):
        c.paste(mini_fly(48, 48, fx_, fy_, t + i))
    return c


def dead_mulligan(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 44, 6, lobes=[(8, 12, 5)])
    c.paste(fin(pud))
    sk = L()                                              # pielea goală, dezumflată
    m = pk.polygon([(7, 46), (10, 38), (16, 35), (22, 38), (27, 33), (34, 36), (40, 41), (42, 46)])
    pk.shade_flat(sk, m, MULL_R, shadow=(2, 1), deep=(1, 1), light_rim=(-1, -1))
    pk.stamp(sk, 17, 40, [".bb.", "bkkb", ".bb."], {"b": MULL_R[3], "k": HOLE})
    pk.stamp(sk, 29, 38, [".bb.", "bkkb", ".bb."], {"b": MULL_R[3], "k": HOLE})
    pk.stamp(sk, 10, 41, EYE_X_SMALL, key())
    c.paste(fin(sk))
    for (fx_, fy_) in [(20, 30), (31, 27), (36, 32)]:    # muștele scapă din sac
        c.paste(mini_fly(48, 48, fx_, fy_, fx_))
    return c


# ───────────────────────────── HOPPER ─────────────────────────────
# Broscoiul-om: un cap-corp ghemuit, ochi bulbucați pe creștet, picioare din spate gata de salt.

HOP_R = [mx(SKIN[0], TEAR[0], 0.25), mx(SKIN[1], CAVES[0], 0.42), mx(SKIN[2], CAVES[1], 0.5),
         mx(SKIN[3], CAVES[2], 0.55)]
HOP_LINE = mx(HOP_R[3], O, 0.45)


def draw_hopper(pose):
    ph, t, sq = pose.ph, pose.t, pose.sq
    air = 11 if ph == "strike" else 0
    crouch = 3 if ph == "wind" else 0
    c = L()
    if air:
        ground_shadow(c, 24, 18)
    oy = -air + crouch + pose.bob + sq
    dim = [HOP_R[2], HOP_R[2], HOP_R[3], HOP_R[3]]
    farleg, nearleg, body, eyes, arm = L(), L(), L(), L(), L()
    if air:                                               # în aer: picioarele întinse în jos-spate
        fl = pk.ellipse(30, 30 + oy, 36, 38 + oy) | pk.polygon([(31, 36 + oy), (35, 36 + oy), (40, 44 + oy), (36, 45 + oy)])
        nl = pk.ellipse(26, 31 + oy, 33, 40 + oy) | pk.polygon([(27, 37 + oy), (32, 37 + oy), (36, 46 + oy), (31, 46 + oy)])
    else:                                                 # ghemuit: coapsa strânsă, laba lată pe sol
        fl = pk.ellipse(29, 31 + oy, 38, 41 + oy) | pk.rect(28, 44, 37, 46)
        nl = pk.ellipse(25, 33 + oy, 35, 43 + oy) | pk.rect(19, 45, 33, 46) | pk.rect(30, 42 + oy, 33, 46)
    pk.shade_flat(farleg, fl, dim, shadow=(1, 1), deep=None)
    pk.shade_flat(nearleg, nl, HOP_R, shadow=(2, 1), deep=(1, 1), light_rim=(-1, -1))
    bb = sqbox((10, 16 + oy, 34, 42 + oy), sq, ground=42 + oy)
    x0, y0, x1, y1 = bb
    bm = pk.ellipse(*bb) | pk.ellipse(x0, y0 + 8, x1 - 2, y1)
    sphere(body, bm, HOP_R, bb)
    # pete de cadavru pe spate
    pk.dither_fill(body, pk.ellipse(x1 - 9, y0 + 6, x1 - 3, y0 + 12) & bm, HOP_R[2], 0.5)
    k = key(HOP_LINE)
    # gura largă de broască, cu colțurile în jos
    mxp, my = x0 + 1, y0 + 17
    if ph == "hurt":
        pk.stamp(body, mxp + 2, my - 2, [".KKKK.", "KrRRrK", ".KKKK."], k)
    elif ph == "strike":
        pk.stamp(body, mxp, my - 2, ["KKKKKKKKK.", "KkrRRRrkK.", ".KkkkkkK..", "..KKKKK..."], k)
    else:
        pk.stamp(body, mxp, my, ["K.........", ".KKKKKKKK.", "........KK"], k)
        drip(body, mxp + 3, my + 2, 1 + t % 2, t, drop=False)
    fin(body)
    # ochii bulbucați: două cupole pe creștet (silueta lui Hopper)
    if ph != "hurt":
        for (ex, ey, w_, near_) in [(x0 + 3, y0 - 3, 8, True), (x0 + 11, y0 - 4, 6, False)]:
            em = pk.ellipse(ex, ey, ex + w_ - 1, ey + 6)
            pk.shade_flat(eyes, em, HOP_R if near_ else dim, shadow=(1, 1), deep=None, light_rim=(-1, -1))
            ix, iy = ex + 1, ey + 2
            if near_:
                pk.stamp(eyes, ix, iy, ["WWWWW", "EEWWW", "EEWWW"], k)
            else:
                pk.stamp(eyes, ix, iy, ["WWW", "EWW", "EWW"], k)
        fin(eyes)
    else:
        pk.stamp(body, x0 + 4, y0 + 3, ["KK...", "..KKK", "KK..."], k)
    # brațul din față, sprijinit pe sol (sau ridicat în salt)
    if air:
        am = pk.polygon([(14, 30 + oy), (18, 30 + oy), (10, 22 + oy), (7, 23 + oy)])
        hm = pk.ellipse(5, 19 + oy, 10, 24 + oy)
    else:
        am = pk.polygon([(13, 33 + oy), (17, 33 + oy), (14, 44), (11, 44)])
        hm = pk.ellipse(8, 43, 14, 46)
    pk.shade_flat(arm, am | hm, HOP_R, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    c.paste(stack(48, 48, [fin(farleg), fin(nearleg), body, eyes, fin(arm)]))
    if ph == "wind":
        dust(c, [(8, 45), (38, 45)])
    return c


def dust(c, pts):
    """Praf ridicat de pe sol (pufuri gri-maronii fără contur)."""
    col = mx(BASEMENT[0], WHITE, 0.25)
    for x, y in pts:
        c.fill([(x, y), (x + 1, y), (x - 1, y + 1), (x + 2, y + 1), (x, y - 1)], col)


def dead_hopper(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 44, 6, lobes=[(10, 10, 4)])
    c.paste(fin(pud))
    lg = L()
    for m in (pk.polygon([(28, 44), (38, 36), (42, 38), (32, 46)]), pk.polygon([(6, 46), (9, 40), (13, 41), (11, 46)])):
        pk.shade_flat(lg, m, HOP_R, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    c.paste(fin(lg))
    b = L()
    bb = (9, 29, 37, 46)
    sphere(b, pk.ellipse(*bb), HOP_R, bb)
    k = key(HOP_LINE)
    pk.stamp(b, 13, 33, EYE_X, k)
    pk.stamp(b, 21, 32, EYE_X_SMALL, k)
    pk.stamp(b, 11, 40, ["KKKKKK", ".HHH..", "..H..."], k)
    c.paste(fin(b))
    return c


# ───────────────────────────── MAW ─────────────────────────────
# Capul-gură care plutește: aproape toată fața e o gură căscată, cu dinți; ochii dați peste cap.

MAW_R = [mx(SKIN[0], DEPTHS[0], 0.3), mx(SKIN[1], DEPTHS[0], 0.5), mx(SKIN[2], DEPTHS[1], 0.58),
         mx(SKIN[3], DEPTHS[2], 0.62)]
MAW_LINE = mx(MAW_R[3], O, 0.4)


def maw_head(box, pose):
    c = L()
    x0, y0, x1, y1 = box
    ph, t = pose.ph, pose.t
    m = pk.ellipse(x0, y0, x1, y1) | pk.ellipse(x0, y0 + 10, x1 - 3, y1)
    sphere(c, m, MAW_R, box)
    k = key(MAW_LINE)
    # sprâncene grele + ochii dați peste cap (albi, injectați)
    c.fill(pk.line(x0 + 6, y0 + 5, x0 + 12, y0 + 7), MAW_LINE)
    c.fill(pk.line(x0 + 1, y0 + 7, x0 + 3, y0 + 6), MAW_LINE)
    if ph == "hurt":
        pk.stamp(c, x0 + 7, y0 + 8, ["KK...", "..KKK", "KK..."], k)
    elif ph != "dead":
        pk.stamp(c, x0 + 7, y0 + 8, [".KKK.", "KWWWK", "KWRWK", ".KKK."], {**k, "R": BLOOD[0]})
        pk.stamp(c, x0 + 2, y0 + 9, ["KK", "WK", "KK"], k)
    # gura: ocupă jumătatea de jos a feței
    if ph == "wind":
        mb = (x0 + 2, y0 + 17, x0 + 13, y0 + 22)
    elif ph == "strike" or ph == "hurt":
        mb = (x0, y0 + 13, x0 + 15, y0 + 30)
    else:
        mb = (x0 + 1, y0 + 14 + (t % 2), x0 + 14, y0 + 28)
    mm = pk.ellipse(*mb)
    c.fill(mm, HOLE)
    c.fill(pk.ellipse(mb[0] + 3, mb[1] + 3, mb[2] - 1, mb[3] - 1) & mm, BLOOD[2])     # gâtlejul
    c.fill(pk.ellipse(mb[0] + 2, mb[3] - 4, mb[2] - 2, mb[3]) & mm, BLOOD[1])        # limba
    c.fill({p for p in pk.ellipse(mb[0] + 3, mb[3] - 4, mb[2] - 4, mb[3] - 2) if p in mm}, BLOOD[0])
    c.fill(pk.inner_edge(mm), O)
    if ph != "wind":                                       # dinții: sus și jos, câte un pixel din doi
        top = sorted({(x, y) for x, y in mm if (x, y - 1) not in mm and (x + y) % 2 == 0 and mb[0] + 1 < x < mb[2] - 1})
        for x, y in top:
            c.set(x, y + 1, WHITE)
        bot = sorted({(x, y) for x, y in mm if (x, y + 1) not in mm and x % 2 == 1 and mb[0] + 2 < x < mb[2] - 2})
        for x, y in bot:
            c.set(x, y - 1, UI["parchment"][0])
    # bale de sânge din buza de jos
    if ph in ("idle", "strike"):
        drip(c, mb[0] + 5, mb[3] + 1, 2 + t % 2, t)
    return fin(c)


def maw_stump(box, pose):
    """Gâtul retezat: carne zdrențuită și sânge care picură (ce atârnă sub capul plutitor)."""
    c = L()
    x0, y0, x1, y1 = box
    cx = (x0 + x1) // 2 + 3
    m = pk.ellipse(cx - 5, y1 - 4, cx + 5, y1 + 2)
    pk.shade_flat(c, m, MAW_R, shadow=(1, 1), deep=None)
    c.fill(pk.ellipse(cx - 3, y1 - 1, cx + 3, y1 + 2), BLOOD[1])
    for i, (sx, n) in enumerate([(cx - 3, 3), (cx, 4), (cx + 2, 2)]):        # zdrențe de carne
        c.fill([(sx, y1 + 2 + j) for j in range(n + (pose.t + i) % 2)], BLOOD[1])
    c.set(cx - 1, y1, BLOOD[2])
    return fin(c)


def draw_maw(pose):
    c = L()
    ph, t = pose.ph, pose.t
    hover = -8 + pose.bob + (t % 2 if ph == "idle" else 0)
    box = (13, 6 + hover + 6, 35, 37 + hover)
    box = {"wind": mbox(box, 2, -1), "strike": mbox(box, -1, 1), "hurt": mbox(box, 3, 1)}.get(ph, box)
    box = sqbox(box, pose.sq)
    ground_shadow(c, 24, 16)
    c.paste(maw_stump(box, pose))
    c.paste(maw_head(box, pose))
    if ph == "strike":
        c.paste(fx_shot(48, 48, 0, 25, 6))
        c.paste(gib(48, 48, 6, 33, 4, 3, BLOOD_R))
        c.paste(fx_drops(48, 48, [(8, 22), (3, 34)]))
    return c


def dead_maw(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 42, 6, lobes=[(12, 12, 5), (36, 10, 4)])
    c.paste(fin(pud))
    box = (10, 21, 33, 46)
    h = maw_head(box, Pose(ph="dead"))
    c.paste(h)
    k = key(MAW_LINE)
    pk.stamp(c, 17, 28, EYE_X_SMALL, k)
    pk.stamp(c, 12, 29, ["K.", ".K"], k)
    return c


# ───────────────────────────── GLOBIN ─────────────────────────────
# Grămada de carne vie: un gaper jupuit, roșu-vișiniu, cu mușchii la vedere și pumni grei.

MEAT_R = [mx(BLOOD[0], SKIN[1], 0.45), mx(BLOOD[0], DEPTHS_FLESH, 0.45), DEPTHS_FLESH,
          mx(DEPTHS_FLESH, O, 0.45)]
SINEW = mx(MEAT_R[0], WHITE, 0.35)


def globin_body(pose):
    ph, t, sq = pose.ph, pose.t, pose.sq
    lean = {"wind": 2, "strike": -3, "hurt": 3}.get(ph, 0)
    far, near, torso = L(), L(), L()
    fl = pk.rect(27, 40, 31, 46) | pk.rect(25, 44, 30, 46)
    nl = pk.rect(18, 40, 22, 46) | pk.rect(16, 44, 21, 46)
    pk.shade_flat(far, fl, [MEAT_R[2], MEAT_R[2], MEAT_R[3], MEAT_R[3]], shadow=(1, 1), deep=None)
    pk.shade_flat(near, nl, MEAT_R, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    tb = sqbox((15 + lean, 19 + pose.bob, 38 + lean, 43), sq, ground=43)
    tm = pk.ellipse(*tb, fat=0.15) | pk.ellipse(tb[0] + 8, tb[1] - 3, tb[2], tb[1] + 12)   # gheb
    sphere(torso, tm, MEAT_R, (tb[0], tb[1] - 3, tb[2], tb[3]))
    x0, y0, x1, y1 = tb
    # fibrele mușchilor: arcuri deschise sus, închise jos
    for i, sx in enumerate((x0 + 9, x0 + 14, x0 + 19)):
        torso.fill([p for p in pk.line(sx, y0 + 2, sx - 2, y1 - 4) if p in tm], MEAT_R[3])
        torso.fill([p for p in pk.line(sx - 1, y0 + 3, sx - 3, y0 + 9) if p in tm], SINEW)
    torso.fill([p for p in pk.line(x0 + 4, y1 - 6, x0 + 12, y1 - 4) if p in tm], MEAT_R[3])
    drip(torso, x0 + 6, y1 - 3, 2 + t % 2, t, ramp=MEAT_R, drop=False)
    return fin(far), fin(torso), fin(near), lean


def globin_head(box, pose):
    c = L()
    x0, y0, x1, y1 = box
    ph, t = pose.ph, pose.t
    m = head_mask_left(box)
    sphere(c, m, MEAT_R, box)
    k = key(MEAT_R[3])
    # fibre pe creștet
    for sx in (x0 + 9, x0 + 13):
        c.fill([p for p in pk.line(sx, y0 + 1, sx + 2, y0 + 6) if p in m], MEAT_R[3])
    nx, fx, ey = x0 + 6, x0 + 1, y1 - 13
    # sprânceana încruntată
    c.fill(pk.line(nx - 1, ey - 2, nx + 5, ey), MEAT_R[3])
    if ph == "hurt":
        pk.stamp(c, nx, ey + 1, ["KK...", "..KKK", "KK..."], k)
        pk.stamp(c, x0 + 3, y1 - 6, [".KKKK.", "KkFFkK", ".KKKK."], k)
        return fin(c)
    pk.stamp(c, nx, ey, ["KKKKK", "KkkkK", "KkkkK", ".KRK."], k)
    pk.stamp(c, fx, ey + 1, ["KK", "kK", "kK", "R."], k)
    if ph == "strike":
        pk.stamp(c, x0 + 2, y1 - 7, ["KKKKKKK", "KFkFkFK", "KkkrkkK", "KFkFkFK", ".KKKKK."], k)
    else:
        pk.stamp(c, x0 + 2, y1 - 6, ["KKKKKK.", "KFkFkK.", "KkFkFK.", ".KKKK.."], k)
    drip(c, nx + 2, ey + 4, 2 + t % 2, t)
    return fin(c)


def globin_arm(pose, lean, which):
    c = L()
    ph = pose.ph
    oy = pose.bob + pose.sq
    if ph == "wind":
        sh, hand = (22 + lean, 24 + oy), (20 + lean, 5)
    elif ph == "strike":
        sh, hand = (19 + lean, 25 + oy), (9, 42)
    elif ph == "hurt":
        sh, hand = (24 + lean, 27 + oy), (36, 22)
    else:
        sh, hand = (19 + lean, 25 + oy), (11, 39 + pose.t % 2)
    if which == "far":
        sh, hand = (sh[0] + 7, sh[1] - 1), (hand[0] + 6, hand[1] - (1 if ph != "wind" else 0))
    m = set()
    for i in range(-1, 3):
        m |= set(pk.line(sh[0] + i, sh[1], hand[0] + i, hand[1]))
    hm = pk.ellipse(hand[0] - 3, hand[1] - 3, hand[0] + 3, hand[1] + 3)
    r = MEAT_R if which == "near" else [MEAT_R[2], MEAT_R[2], MEAT_R[3], MEAT_R[3]]
    pk.shade_flat(c, m, r, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    sphere(c, hm, r, None)
    c.set(hand[0] - 2, hand[1] - 1, r[3])                 # încheieturile pumnului
    c.set(hand[0] - 2, hand[1] + 1, r[3])
    return fin(c)


def draw_globin(pose):
    far, torso, near, lean = globin_body(pose)
    ph = pose.ph
    hb = (10 + lean, 9 + pose.bob + pose.sq, 30 + lean, 29 + pose.bob + pose.sq)
    if ph == "strike":
        hb = mbox(hb, -2, 4)
    c = stack(48, 48, [globin_arm(pose, lean, "far"), far, torso, near, globin_head(hb, pose),
                       globin_arm(pose, lean, "near")])
    if ph == "strike":                                     # Slam: stropi și praf la impact
        c.paste(fx_drops(48, 48, [(2, 36), (5, 31), (16, 36)], MEAT_R))
        dust(c, [(3, 45), (17, 45)])
    return c


def globin_goo(c, cx, y1, w, h, mound, t, eyes=True, dead=False):
    """Balta de carne topită; `mound` = cât de mult s-a ridicat la loc (regenerare)."""
    pud = L()
    m = pk.ellipse(cx - w // 2, y1 - h + 1, cx + w // 2, y1) | pk.ellipse(cx - w // 2 - 4, y1 - 3, cx - w // 2 + 6, y1)
    pud.fill(m, MEAT_R[2])
    pud.fill(pk.edge_band(m, 0, 1) | pk.edge_band(m, 1, 1), MEAT_R[3])
    pud.fill(pk.edge_band(m, 0, -1) & pk.edge_band(m, -1, -1), MEAT_R[1])
    c.paste(fin(pud))
    if mound:
        mm = L()
        mb = (cx - 9, y1 - mound - 4, cx + 8, y1 - 1)
        mmask = pk.ellipse(*mb)
        sphere(mm, mmask, MEAT_R, mb)
        for bx, by in [(cx - 6, y1 - mound), (cx + 4, y1 - mound + 1)]:            # bule care se sparg
            mm.set(bx, by, SINEW)
        if eyes:
            k = key(MEAT_R[3])
            ex, ey = cx - 6, y1 - mound + 1
            pk.stamp(mm, ex, ey, ["KkK.KkK"] if not dead else ["K.K.K.K"], k)
        c.paste(fin(mm))
    for i, (bx, by) in enumerate([(cx - 14, y1 - 6 - t % 2), (cx + 12, y1 - 5)]):      # bule în aer
        b = L()
        b.fill(pk.ellipse(bx, by, bx + 1, by + 1), MEAT_R[1])
        c.paste(fin(b))


def regen_globin(pose):
    c = L()
    globin_goo(c, 24, 46, 34, 6, 7, 0)
    for (x, y) in [(12, 35), (35, 33)]:                    # bucăți care se târăsc înapoi
        c.paste(gib(48, 48, x, y, 4, 3, MEAT_R))
    return c


def dead_globin(pose):
    c = L()
    globin_goo(c, 24, 46, 40, 7, 4, 0, eyes=True, dead=True)
    for (x, y, w_, h_) in [(8, 39, 6, 4), (33, 38, 7, 5), (26, 34, 5, 4)]:
        c.paste(gib(48, 48, x, y, w_, h_, MEAT_R, bone=UI["parchment"][0]))
    return c


# ───────────────────────────── CHARGER ─────────────────────────────
# Viermele care se aruncă înainte: o larvă palidă din inele, cu capul mic și gura însângerată.

MAGGOT_R = [mx(UI["parchment"][0], WHITE, 0.4), mx(SKIN[1], UI["parchment"][0], 0.55),
            mx(SKIN[2], UI["parchment"][1], 0.45), mx(SKIN[3], POOP[2], 0.35)]
MAGGOT_HEAD = [mx(MAGGOT_R[1], BLOOD[0], 0.25), mx(MAGGOT_R[2], BLOOD[1], 0.3),
               mx(MAGGOT_R[3], BLOOD[1], 0.35), mx(MAGGOT_R[3], BLOOD[2], 0.55)]


def charger_segments(ph, t):
    """(x, r, ridicare) pentru inele, de la cap (stânga) la coadă."""
    if ph == "strike":
        xs, rs, dy = [6, 14, 21, 28, 34, 39], [5.5, 6, 6, 5.5, 4.5, 3.5], [0] * 6
    elif ph == "wind":
        xs, rs, dy = [15, 20, 25, 30, 34, 37], [5.5, 6, 6.5, 6, 4.5, 3.5], [0, -4, -7, -5, -1, 0]
    elif ph == "hurt":
        xs, rs, dy = [14, 20, 26, 31, 35, 39], [5.5, 6.5, 6.5, 6, 4.5, 3.5], [0, 0, 0, 0, 0, 0]
    else:
        wave = [[0, -1, -2, -1, 0, 0], [0, 0, -1, -2, -1, 0]][t % 2]
        xs, rs, dy = [10, 17, 23, 29, 34, 38], [5.5, 6, 6.5, 6, 4.5, 3.5], wave
    return list(zip(xs, rs, dy))


def draw_charger(pose):
    c = L()
    ph, t = pose.ph, pose.t
    segs = charger_segments(ph, t)
    # dâra lucioasă de mucus pe sol
    trail = L()
    trail.fill([(x, 46) for x in range(segs[0][0], 44)], mx(MAGGOT_R[2], WHITE, 0.2))
    c.paste(trail)
    for i in range(len(segs) - 1, -1, -1):                # de la coadă spre cap
        x, r, dy = segs[i]
        s = L()
        hb = r * (0.92 if i else 1.0)
        box = (round(x - r), round(46 - 2 * hb + 1 + dy - pose.sq * 0), round(x + r) - 1, 46 + dy)
        if pose.sq and i:
            box = (box[0] - 1, box[1] + pose.sq, box[2] + 1, box[3])
        m = pk.ellipse(*box)
        ramp = MAGGOT_HEAD if i == 0 else MAGGOT_R
        sphere(s, m, ramp, box)
        if i:                                              # pliurile inelului
            s.fill([(box[0] + 1, y) for y in range(box[1] + 3, box[3] - 1)], ramp[2])
        else:
            face_charger(s, box, ph, t)
        fin(s)
        c.paste(s)
    if ph == "strike":                                     # liniile de viteză în spate
        lines = L()
        for (x0, y0, n) in [(42, 36, 5), (40, 40, 7), (43, 44, 4)]:
            lines.fill([(x0 + i, y0) for i in range(n) if x0 + i < 48], mx(MAGGOT_R[2], O, 0.3))
        c.paste(lines)
        dust(c, [(44, 45)])
    return c


def face_charger(s, box, ph, t):
    x0, y0, x1, y1 = box
    k = key(MAGGOT_R[3])
    if ph == "hurt":
        pk.stamp(s, x0 + 3, y0 + 3, ["K.K", ".K.", "K.K"], k)
        return
    pk.stamp(s, x0 + 3, y0 + 3, ["KK", "KW"], k)                         # ochiul mic, negru
    if ph == "strike":
        pk.stamp(s, x0, y1 - 5, ["KKK.", "kkrK", "krRK", "KKK."], k)    # gura căscată
    else:
        pk.stamp(s, x0 + 1, y1 - 4, ["KK.", "kRK", "KK."], k)
    # mandibulele, două cârlige mici
    s.set(x0 - 1 if ph == "strike" else x0, y1 - 1, UI["parchment"][1])


def dead_charger(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 44, 5, lobes=[(10, 12, 4)])
    c.paste(fin(pud))
    for (x, r, dy, ramp) in [(36, 4.5, 0, MAGGOT_R), (29, 5, 1, MAGGOT_R), (17, 5.5, 0, MAGGOT_R)]:
        s = L()
        box = (round(x - r), round(46 - 2 * r * 0.8 + 1 + dy), round(x + r) - 1, 46)
        m = pk.ellipse(*box)
        sphere(s, m, ramp, box)
        s.fill([(box[0] + 1, y) for y in range(box[1] + 2, box[3])], ramp[2])
        c.paste(fin(s))
    h = L()
    box = (5, 38, 15, 46)
    sphere(h, pk.ellipse(*box), MAGGOT_HEAD, box)
    pk.stamp(h, 7, 40, EYE_X_SMALL, key())
    c.paste(fin(h))
    c.fill([(23, 40), (24, 41), (16, 46)], BLOOD[0])
    return c


# ───────────────────────────── SPIDER ─────────────────────────────
# Păianjenul mic: corp maroniu-negru, ochi roșii, opt picioare subțiri ca firul de contur.

SPIDER_R = [mx(POOP[3], WHITE, 0.3), mx(POOP[3], DEPTHS[1], 0.25), mx(BASEMENT[4], DEPTHS[2], 0.5),
            mx(DEPTHS[3], O, 0.4)]


def spider_legs(ph, t, dx=0, dy=0, rear=0):
    """Picioarele apropiate (4): (prindere, genunchi, laba). Cele îndepărtate sunt aceleași, decalate."""
    j = 1 if t % 2 else 0
    legs = [((10, 19), (5, 13 - j), (1, 30)), ((12, 20), (9, 12 + j), (7, 30)),
            ((15, 20), (19, 12 - j), (21, 30)), ((16, 20), (24, 13 + j), (28, 30))]
    if ph == "wind":                                       # se ridică pe spate, picioarele din față sus
        legs[0] = ((10, 17), (6, 8), (2, 5))
        legs[1] = ((12, 18), (8, 7), (6, 3))
    if ph == "strike":
        legs[0] = ((10, 19), (3, 15), (0, 24))
    out = []
    for (a, k_, f) in legs:
        out.append([(a[0] + dx, a[1] + dy - rear), (k_[0] + dx, k_[1] + dy - rear), (f[0] + (dx if f[1] < 30 else dx // 2), f[1] if f[1] == 30 else f[1] + dy - rear)])
    return out


def draw_spider(pose):
    c = L(32, 32)
    ph, t = pose.ph, pose.t
    dx = {"strike": -3, "wind": 2, "hurt": 2}.get(ph, 0)
    rear = 3 if ph == "wind" else 0
    dy = pose.sq
    leg_col = O
    knee_col = SPIDER_R[2]
    farl = L(32, 32)
    for pts in spider_legs(ph, t + 1, dx + 2, dy - 1, rear):
        farl.fill(pk.polyline(pts), mx(O, SPIDER_R[2], 0.35))
    c.paste(farl)
    body = L(32, 32)
    ab = (15 + dx, 12 + dy - rear // 2, 27 + dx, 22 + dy)
    am = pk.ellipse(*ab)
    sphere(body, am, SPIDER_R, ab)
    # semnul roșu de pe spate (clepsidra) + perișori
    pk.stamp(body, ab[0] + 6, ab[1] + 3, ["RR.", ".R.", ".RR"], {"R": BLOOD[1]})
    fin(body)
    head = L(32, 32)
    hb = (7 + dx, 15 + dy - rear, 16 + dx, 22 + dy - rear)
    hm = pk.ellipse(*hb)
    sphere(head, hm, SPIDER_R, hb)
    ek = {"R": BLOOD[0], "W": WHITE, "r": BLOOD[1], "K": O}
    if ph == "hurt":
        pk.stamp(head, hb[0] + 1, hb[1] + 2, ["RR.", "..R", "RR."], ek)
    else:
        pk.stamp(head, hb[0] + 1, hb[1] + 2, ["WR.R", "RR.r"], ek)                 # 2 ochi mari + 2 mici
    fin(head)
    # colții (chelicerele): albi, deschiși la mușcătură
    fang = [(hb[0] + 1, hb[3] + 1), (hb[0] + 3, hb[3] + 1)]
    if ph == "strike":
        fang = [(hb[0], hb[3] + 1), (hb[0] - 1, hb[3] + 2), (hb[0] + 3, hb[3] + 1), (hb[0] + 3, hb[3] + 2)]
    c.paste(body)
    c.paste(head)
    c.fill(fang, UI["parchment"][0])
    nearl = L(32, 32)
    for pts in spider_legs(ph, t, dx, dy, rear):
        nearl.fill(pk.polyline(pts), leg_col)
        nearl.set(pts[1][0], pts[1][1], knee_col)
    c.paste(nearl)
    if ph == "strike":                                     # venin verde care picură din colți
        c.fill([(hb[0] - 1, hb[3] + 3), (hb[0] - 1, hb[3] + 4)], STATUS["poison"])
        c.set(hb[0] - 1, hb[3] + 6, mx(STATUS["poison"], O, 0.3))
        lines = L(32, 32)
        for (x0, y0, n) in [(27, 14, 4), (28, 18, 4)]:
            lines.fill([(x0 + i, y0) for i in range(n)], mx(SPIDER_R[1], WHITE, 0.2))
        c.paste(lines)
    return c


def dead_spider(pose):
    c = L(32, 32)
    pud = L(32, 32)
    puddle(pud, 16, 30, 22, 3, ramp=[mx(STATUS["poison"], WHITE, 0.2), STATUS["poison"],
                                       mx(STATUS["poison"], O, 0.4), mx(STATUS["poison"], O, 0.65)])
    c.paste(fin(pud))
    b = L(32, 32)
    ab = (9, 21, 22, 29)
    sphere(b, pk.ellipse(*ab), SPIDER_R, ab)
    c.paste(fin(b))
    # pe spate, cu picioarele strânse în sus
    for (x0, x1) in [(10, 8), (13, 12), (17, 18), (20, 23)]:
        c.fill(pk.polyline([(x0, 22), (x1, 17), (x1 + (1 if x1 > 15 else -1), 15)]), O)
    pk.stamp(c, 11, 24, ["R.R", ".R.", "R.R"], {"R": BLOOD[0]})
    return c


# ───────────────────────────── KNIGHT ─────────────────────────────
# Cavalerul: un cap închis într-un coif de oțel cu vizieră și țeapă, creierul la vedere pe spate.

STEEL_R = [mx(CAVES[0], WHITE, 0.5), mx(CAVES[0], WHITE, 0.08), CAVES[1], CAVES[2]]
STEEL_DARK = CAVES[3]
BRAIN_R = [mx(SKIN[0], BLOOD[0], 0.15), mx(SKIN[1], BLOOD[0], 0.32), mx(SKIN[2], BLOOD[1], 0.42),
           mx(SKIN[3], BLOOD[2], 0.5)]
KLEG_R = [mx(SKIN[1], BLOOD[0], 0.15), mx(SKIN[2], BLOOD[0], 0.25), mx(SKIN[3], BLOOD[1], 0.4),
          mx(SKIN[3], BLOOD[2], 0.6)]


def draw_knight(pose):
    ph, t, sq = pose.ph, pose.t, pose.sq
    lean = {"wind": 1, "strike": -4, "hurt": 3}.get(ph, 0)
    low = {"wind": 2, "strike": 2}.get(ph, 0)
    far, near = L(), L()
    fl = pk.rect(26, 39, 29, 46) | pk.rect(25, 45, 28, 46)
    nl = pk.rect(19, 39, 22, 46) | pk.rect(18, 45, 21, 46)
    if ph == "strike":
        fl, nl = pk.shift(fl, 2, 0), pk.shift(nl, -2, 0)
    pk.shade_flat(far, fl, [KLEG_R[2], KLEG_R[2], KLEG_R[3], KLEG_R[3]], shadow=(1, 1), deep=None)
    pk.shade_flat(near, nl, KLEG_R, shadow=(1, 1), deep=None)
    box = sqbox((11 + lean, 7 + low + pose.bob, 37 + lean, 40), sq, ground=40)
    x0, y0, x1, y1 = box
    # creierul: iese prin spatele coifului (strat sub coif)
    brain = L()
    bmk = pk.ellipse(x1 - 11, y0 + 1, x1 + 1, y0 + 14)
    sphere(brain, bmk, BRAIN_R, None)
    for pts in ([(x1 - 8, y0 + 4), (x1 - 6, y0 + 3), (x1 - 4, y0 + 5), (x1 - 2, y0 + 4)],
                [(x1 - 9, y0 + 8), (x1 - 7, y0 + 7), (x1 - 5, y0 + 9), (x1 - 2, y0 + 8)],
                [(x1 - 7, y0 + 11), (x1 - 4, y0 + 12)]):
        brain.fill([p for p in pk.polyline(pts) if p in bmk], BRAIN_R[3])
    fin(brain)
    helm = L()
    hm = pk.ellipse(x0, y0, x1, y1 + 6)
    hm = {p for p in hm if p[1] <= y1} - pk.ellipse(x1 - 9, y0 + 2, x1 + 3, y0 + 13)
    sphere(helm, hm, STEEL_R, (x0, y0, x1, y1 + 6))
    # creasta de pe creștet + niturile
    helm.fill([p for p in pk.line(x0 + 7, y0 + 1, x1 - 12, y0 + 1) if p in hm], STEEL_R[0])
    helm.fill([(x, y + 1) for x, y in pk.line(x0 + 7, y0 + 1, x1 - 12, y0 + 1) if (x, y + 1) in hm], STEEL_R[2])
    for rx in range(x0 + 3, x1 - 1, 4):
        if (rx, y1 - 2) in hm:
            helm.set(rx, y1 - 2, STEEL_R[0])
            helm.set(rx + 1, y1 - 1, STEEL_DARK)
    helm.fill([(x, y1) for x in range(x0, x1 + 1) if (x, y1) in hm], STEEL_DARK)
    fin(helm)
    # viziera: o placă bombată în față, cu fanta pentru ochi și găurile de respirat
    vis = L()
    vm = pk.polygon([(x0 - 1, y0 + 9), (x0 + 14, y0 + 7), (x0 + 16, y0 + 25), (x0, y0 + 26)]) & pk.ellipse(x0 - 2, y0 - 1, x1 - 2, y1 + 4)
    pk.shade_flat(vis, vm, STEEL_R, shadow=(2, 1), deep=(1, 1), light_rim=(-1, -1))
    sy = y0 + 13
    vis.fill([(x, sy) for x in range(x0 + 1, x0 + 15) if (x, sy) in vm], O)
    vis.fill([(x, sy + 1) for x in range(x0 + 1, x0 + 15) if (x, sy + 1) in vm], O)
    if ph == "hurt":
        vis.fill([(x0 + 4, sy), (x0 + 10, sy + 1)], WHITE)
    else:
        hot = ph in ("wind", "strike")
        vis.fill([(x0 + 8, sy), (x0 + 9, sy), (x0 + 9, sy + 1)], BLOOD[0] if not hot else tn(BLOOD[0], 1.5))
        vis.fill([(x0 + 2, sy), (x0 + 2, sy + 1)], BLOOD[1] if not hot else BLOOD[0])
    for hy in (sy + 5, sy + 8):
        for hx in range(x0 + 3, x0 + 13, 3):
            if (hx, hy) in vm:
                vis.set(hx, hy, STEEL_DARK)
    fin(vis)
    # țeapa (lancea) care iese din vizieră
    spike = L()
    ln = {"strike": 13, "wind": 6}.get(ph, 9)
    sm = pk.polygon([(x0 + 1, sy + 3), (x0 + 1 - ln, sy + 5.5), (x0 + 1, sy + 8)])
    pk.shade_flat(spike, sm, STEEL_R, shadow=(0, 1), deep=None, light_rim=(0, -1))
    fin(spike)
    c = stack(48, 48, [far, brain, helm, near, spike, vis])
    if ph == "wind":                                       # Shield Up: sclipire albastră de „block”
        g = STATUS["block"]
        star = [(x0 + 2, y0 - 2), (x0 + 2, y0 - 1), (x0 + 2, y0 + 1), (x0 + 2, y0 + 2), (x0, y0), (x0 + 1, y0),
                (x0 + 3, y0), (x0 + 4, y0)]
        c.fill(star, g)
        c.set(x0 + 2, y0, WHITE)
        c.fill(pk.line(x0 - 3, y0 + 18, x0 - 3, y0 + 26), mx(g, WHITE, 0.3))
        c.fill(pk.line(x0 - 5, y0 + 20, x0 - 5, y0 + 24), g)
    if ph == "strike":
        lines = L()
        for (lx, ly, n) in [(x1 + 2, y0 + 8, 5), (x1 + 1, y0 + 15, 6), (x1 + 2, y0 + 22, 4)]:
            lines.fill([(lx + i, ly) for i in range(n) if lx + i < 48], mx(STEEL_R[2], O, 0.2))
        c.paste(lines)
    return c


def dead_knight(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 44, 6, lobes=[(36, 12, 5)])
    c.paste(fin(pud))
    br = L()                                              # creierul căzut din coif
    bmk = pk.ellipse(30, 37, 42, 46)
    sphere(br, bmk, BRAIN_R, None)
    br.fill([p for p in pk.polyline([(32, 40), (34, 39), (36, 41), (39, 40)]) if p in bmk], BRAIN_R[3])
    c.paste(fin(br))
    h = L()                                               # coiful răsturnat pe spate, gol
    box = (5, 27, 31, 46)
    hm = pk.ellipse(5, 27, 31, 52)
    hm = {p for p in hm if p[1] <= 46}
    sphere(h, hm, STEEL_R, (5, 27, 31, 52))
    h.fill([(x, 30) for x in range(10, 26) if (x, 30) in hm], O)
    h.fill([(x, 31) for x in range(10, 26) if (x, 31) in hm], O)
    c.paste(fin(h))
    sp = L()
    sp.fill(pk.polygon([(8, 34), (1, 30), (9, 37)]), STEEL_R[1])
    c.paste(fin(sp))
    return c


# ───────────────────────────── BONY ─────────────────────────────
# Scheletul cu craniu mare (proporții Isaac), coaste subțiri; aruncă cu oase.

BONE_R = [mx(UI["parchment"][0], WHITE, 0.55), UI["parchment"][0], UI["parchment"][1],
          mx(UI["parchment"][1], POOP[2], 0.45)]
BONE_LINE = mx(BONE_R[3], O, 0.35)


def bone_stick(w, h, x0, y0, x1, y1):
    """Un os de aruncat: tijă de 2 px cu câte un nod dublu la capete."""
    c = L(w, h)
    m = set()
    for p in pk.line(x0, y0, x1, y1):
        m |= {p, (p[0] + 1, p[1])}
    for (x, y) in ((x0, y0), (x1, y1)):
        m |= pk.ellipse(x - 1, y - 1, x + 1, y) | pk.ellipse(x, y, x + 2, y + 1)
    pk.shade_flat(c, m, BONE_R, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    return fin(c)


def bony_skull(box, ph, t):
    c = L()
    x0, y0, x1, y1 = box
    m = pk.ellipse(x0, y0, x1, y1) | pk.ellipse(x0 + 1, y0 + 6, x1 - 4, y1)
    sphere(c, m, BONE_R, box)
    k = key(BONE_LINE)
    nx, fx, ey = x0 + 6, x0 + 1, y1 - 12
    # crăpătura de pe creștet
    c.fill(pk.polyline([(x1 - 7, y0 + 1), (x1 - 8, y0 + 4), (x1 - 6, y0 + 6), (x1 - 7, y0 + 8)]), BONE_LINE)
    if ph == "hurt":
        pk.stamp(c, nx, ey + 1, ["KK...", "..KKK", "KK..."], k)
        pk.stamp(c, fx, ey + 1, ["..K", "KK.", "..K"], k)
    elif ph == "dead":
        pk.stamp(c, nx, ey, [".KKKK.", "KkkkkK", "KkkkkK", "KkkkkK", ".KKKK."], k)
        pk.stamp(c, fx, ey + 1, ["KK", "kK", "kK", "K."], k)
    else:
        glow = tn(BLOOD[0], 1.5) if ph in ("wind", "strike") else BLOOD[0]
        pk.stamp(c, nx, ey, [".KKKK.", "KkkkkK", "KkkkkK", "KkkkkK", "KkkkkK", ".KKKK."], k)
        pk.stamp(c, fx, ey, ["KK", "kK", "kK", "kK", "KK"], k)
        c.set(nx + 2, ey + 2, glow)                          # luminița din orbită
        c.set(fx, ey + 2, BLOOD[1])
    pk.stamp(c, x0 + 4, ey + 6, [".K", "KK"], k)              # nasul
    # dinții de sus
    for i in range(0, 10):
        xx = x0 + 2 + i
        if (xx, y1 - 1) in m:
            c.set(xx, y1 - 1, O if i % 2 else BONE_R[0])
    return fin(c)


def bony_jaw(box, ph):
    c = L()
    x0, y0, x1, y1 = box
    drop = {"strike": 2, "hurt": 3, "wind": 1}.get(ph, 0)
    jm = pk.polygon([(x0 + 1, y1), (x0 + 13, y1), (x0 + 14, y1 + 4 + drop), (x0 + 2, y1 + 4 + drop)])
    pk.shade_flat(c, jm, BONE_R, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    for i in range(0, 10):
        c.set(x0 + 3 + i, y1 + (1 if drop else 0), O if i % 2 else BONE_R[0])
    return fin(c)


def draw_bony(pose):
    ph, t, sq = pose.ph, pose.t, pose.sq
    lean = {"wind": 2, "strike": -2, "hurt": 3}.get(ph, 0)
    oy = pose.bob + sq
    # picioarele (oase subțiri, genunchi noduroși)
    legs = L()
    for (hx, kx, fx_, col) in [(26, 27, 28, BONE_R[2]), (22, 21, 20, BONE_R[1])]:
        legs.fill(pk.polyline([(hx, 41), (kx, 44), (fx_, 46)]) + pk.polyline([(hx + 1, 41), (kx + 1, 44), (fx_ + 1, 46)]), col)
        legs.fill([(fx_ - 1, 46), (fx_ - 2, 46)], col)
        legs.set(kx, 44, BONE_R[0])
    fin(legs)
    torso = L()
    cx = 24 + lean
    rib = set()
    for i, yy in enumerate(range(28 + oy, 39 + oy, 2)):
        half = [4, 5, 5, 5, 4, 3][i]
        rib |= {(x, yy) for x in range(cx - half, cx + half + 1)}
    spine = {(cx + 1, y) for y in range(26 + oy, 41)} | {(cx + 2, y) for y in range(26 + oy, 41)}
    pelvis = pk.ellipse(cx - 4, 39, cx + 4, 42)
    torso.fill(rib, BONE_R[1])
    torso.fill({p for p in rib if p[0] > cx + 2}, BONE_R[2])
    torso.fill(spine, BONE_R[2])
    pk.shade_flat(torso, pelvis, BONE_R, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    fin(torso)
    # brațul din spate
    farm = L()
    farm.fill(pk.polyline([(cx + 3, 29 + oy), (cx + 6, 33 + oy), (cx + 5, 37 + oy)]), BONE_R[2])
    fin(farm)
    # brațul din față, cu osul în mână
    arm = L()
    if ph == "wind":
        sh, el, hand = (cx - 2, 29 + oy), (cx + 3, 24 + oy), (cx + 6, 18 + oy)
        bone = (hand[0] - 4, hand[1] - 3, hand[0] + 5, hand[1] + 2)
    elif ph == "strike":
        sh, el, hand = (cx - 2, 29 + oy), (cx - 6, 28 + oy), (cx - 11, 26 + oy)
        bone = None
    elif ph == "hurt":
        sh, el, hand = (cx - 1, 29 + oy), (cx + 4, 27 + oy), (cx + 9, 25 + oy)
        bone = None
    else:
        sh, el, hand = (cx - 2, 29 + oy), (cx - 6, 33 + oy), (cx - 9, 32 + oy + t % 2)
        bone = (hand[0], hand[1] - 6, hand[0], hand[1] + 5)
    arm.fill(pk.polyline([sh, el, hand]), BONE_R[1])
    arm.fill([(hand[0], hand[1]), (hand[0] - 1, hand[1])], BONE_R[0])
    fin(arm)
    sb = (12 + lean, 4 + oy, 34 + lean, 26 + oy)
    if ph == "strike":
        sb = mbox(sb, -1, 1)
    layers = [legs, farm, torso, bony_jaw(sb, ph), bony_skull(sb, ph, t)]
    if bone:
        layers.append(bone_stick(48, 48, *bone))
    layers.append(arm)
    c = stack(48, 48, layers)
    if ph == "strike":                                     # Bone Throw: osul se rotește în zbor
        c.paste(bone_stick(48, 48, 2, 18, 8, 13))
        arc = L()
        arc.fill([(10, 11), (12, 10), (14, 10), (9, 20), (11, 21)], mx(BONE_R[2], O, 0.2))
        c.paste(arc)
    return c


def dead_bony(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 40, 4, ramp=[BONE_R[1], mx(BASEMENT[2], BLOOD[2], 0.3), mx(BASEMENT[3], BLOOD[2], 0.3),
                                      BASEMENT[4]])
    c.paste(fin(pud))
    for b in [(6, 44, 16, 41), (28, 43, 40, 45), (20, 40, 31, 38), (35, 39, 42, 34), (12, 38, 15, 33)]:
        c.paste(bone_stick(48, 48, *b))
    c.paste(bony_skull((14, 24, 35, 45), "dead", 0))
    return c


# ───────────────────────────── LEAPER ─────────────────────────────
# Săritorul: cocoșat, cu capul împins în față, brațe lungi până la pământ și picioare pliate ca un arc.

LEAP_R = [mx(SKIN[1], DEPTHS[0], 0.28), mx(SKIN[2], DEPTHS[0], 0.45), mx(SKIN[3], DEPTHS[1], 0.55),
          mx(DEPTHS[2], O, 0.15)]
LEAP_LINE = mx(LEAP_R[3], O, 0.3)


def draw_leaper(pose):
    ph, t, sq = pose.ph, pose.t, pose.sq
    air = 12 if ph == "strike" else 0
    crouch = 3 if ph == "wind" else 0
    lean = {"strike": -3, "hurt": 3}.get(ph, 0)
    c = L()
    if air:
        ground_shadow(c, 24, 20)
    oy = -air + crouch + pose.bob + sq
    dim = [LEAP_R[2], LEAP_R[2], LEAP_R[3], LEAP_R[3]]
    farleg, nearleg, torso, farm, arm = L(), L(), L(), L(), L()
    if air:                                               # picioarele întinse înapoi
        fl = pk.ellipse(30, 26 + oy, 39, 34 + oy) | pk.polygon([(36, 30 + oy), (39, 32 + oy), (45, 40 + oy), (42, 41 + oy)])
        nl = pk.ellipse(27, 28 + oy, 37, 36 + oy) | pk.polygon([(33, 33 + oy), (36, 34 + oy), (42, 43 + oy), (38, 44 + oy)])
    else:                                                 # pliate: coapsa sus, gamba jos, laba pe sol
        fl = pk.ellipse(30, 27 + oy, 41, 37 + oy) | pk.polygon([(37, 33 + oy), (41, 34 + oy), (39, 46), (35, 46)]) | pk.rect(32, 45, 39, 46)
        nl = pk.ellipse(26, 29 + oy, 38, 39 + oy) | pk.polygon([(33, 35 + oy), (37, 36 + oy), (34, 46), (30, 46)]) | pk.rect(26, 45, 34, 46)
    pk.shade_flat(farleg, fl, dim, shadow=(1, 1), deep=None)
    pk.shade_flat(nearleg, nl, LEAP_R, shadow=(2, 1), deep=(1, 1), light_rim=(-1, -1))
    # spinarea cocoșată
    tb = (16 + lean, 14 + oy, 38 + lean, 36 + oy)
    tm = pk.ellipse(*tb)
    sphere(torso, tm, LEAP_R, tb)
    for i, (sx, sy) in enumerate([(tb[0] + 13, tb[1] + 2), (tb[0] + 16, tb[1] + 4), (tb[0] + 18, tb[1] + 7)]):
        torso.set(sx, sy, LEAP_R[3])                        # vertebrele care ies prin piele
        torso.set(sx - 1, sy - 1, LEAP_R[0])
    # brațele lungi, până la pământ
    if air:
        a_sh, a_hand = (18 + lean, 26 + oy), (6, 22 + oy)
    elif ph == "wind":
        a_sh, a_hand = (19, 26 + oy), (27, 40)
    elif ph == "hurt":
        a_sh, a_hand = (21 + lean, 26 + oy), (34, 23 + oy)
    else:
        a_sh, a_hand = (19 + lean, 26 + oy), (10, 44)
    for lay, (sh, hand), r in ((farm, ((a_sh[0] + 6, a_sh[1] - 1), (a_hand[0] + 6, a_hand[1] - (2 if air else 0))), dim),
                               (arm, (a_sh, a_hand), LEAP_R)):
        m = set()
        for i in range(0, 3):
            m |= set(pk.line(sh[0] + i, sh[1], hand[0] + i, hand[1]))
        hm = pk.ellipse(hand[0] - 2, hand[1] - 2, hand[0] + 3, hand[1] + 2)
        pk.shade_flat(lay, m | hm, r, shadow=(1, 1), deep=None, light_rim=(-1, -1))
        fin(lay)
    head = leaper_head((5 + lean, 17 + oy, 25 + lean, 36 + oy), ph, t)
    return stack(48, 48, [c, farleg, farm, fin(torso), fin(nearleg), head, arm])


def leaper_head(box, ph, t):
    c = L()
    x0, y0, x1, y1 = box
    m = head_mask_left(box)
    sphere(c, m, LEAP_R, box)
    k = key(LEAP_LINE)
    nx, fx, ey = x0 + 6, x0 + 1, y0 + 5
    if ph == "hurt":
        pk.stamp(c, nx, ey + 1, ["KK...", "..KKK", "KK..."], k)
        pk.stamp(c, x0 + 3, y1 - 7, [".KKKK.", "KrRRrK", ".KKKK."], k)
        return fin(c)
    # ochi holbați, nebuni: albi, cu pupila mică spre stânga
    pk.stamp(c, nx, ey, [".KKKK.", "KWWWWK", "KEWWWK", "KWWWWK", ".KKKK."], k)
    pk.stamp(c, fx, ey + 1, ["KK", "EK", "WK", "KK"], k)
    c.fill([(nx + 4, ey + 3), (nx + 4, ey + 2)], BLOOD[0] if t % 2 == 0 else WHITE)     # vasele de sânge
    # rânjetul cu dinți
    if ph == "strike":
        pk.stamp(c, x0 + 1, y1 - 8, ["KKKKKKKKK", "KFKFKFKFK", "KkrRRRrkK", "KFKFKFKFK", ".KKKKKKK."], k)
    else:
        pk.stamp(c, x0 + 1, y1 - 7, ["KKKKKKKKK.", "KFKFKFKFK.", ".KKKKKKK.."], k)
        drip(c, x0 + 3, y1 - 4, 1 + t % 2, t, drop=False)
    return fin(c)


def dead_leaper(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 46, 6, lobes=[(10, 12, 5)])
    c.paste(fin(pud))
    lim = L()
    for m in (pk.polygon([(30, 44), (44, 38), (45, 41), (33, 46)]), pk.polygon([(2, 44), (12, 41), (13, 44), (4, 46)])):
        pk.shade_flat(lim, m, LEAP_R, shadow=(1, 1), deep=None, light_rim=(-1, -1))
    c.paste(fin(lim))
    t_ = L()
    tb = (16, 34, 38, 46)
    sphere(t_, pk.ellipse(*tb), LEAP_R, tb)
    c.paste(fin(t_))
    h = L()
    hb = (6, 31, 25, 46)
    sphere(h, head_mask_left(hb), LEAP_R, hb)
    k = key(LEAP_LINE)
    pk.stamp(h, 12, 35, EYE_X, k)
    pk.stamp(h, 7, 36, EYE_X_SMALL, k)
    pk.stamp(h, 7, 42, ["KKKKKKK", "KFKFKFK", ".KHKKK."], k)
    c.paste(fin(h))
    return c


# ───────────────────────────── HOST ─────────────────────────────
# Craniul-capac: stă închis (invulnerabil), apoi își ridică țeasta și arată fața de carne vie.

FLESH_R = [mx(BLOOD[0], SKIN[1], 0.4), BLOOD[0], BLOOD[1], BLOOD[2]]


def host_skull(lift, ph, t, cracked=False):
    c = L()
    y1 = 46 - lift
    box = (9, y1 - 30, 38, y1 + 8)
    m = {p for p in pk.ellipse(*box) if p[1] <= y1}
    sphere(c, m, BONE_R, box)
    k = key(BONE_LINE)
    x0 = box[0]
    # orbitele (goale) + nasul + dinții de sus pe marginea capacului
    ey = y1 - 14
    pk.stamp(c, x0 + 7, ey, [".KKKK.", "KkkkkK", "KkkkkK", "KkkkkK", "KkkkkK", ".KKKK."], k)
    pk.stamp(c, x0 + 1, ey + 1, ["KK", "kK", "kK", "kK", "K."], k)
    pk.stamp(c, x0 + 4, ey + 7, [".K", "KK"], k)
    for i in range(0, 14):
        xx = x0 + 2 + i
        if (xx, y1) in m:
            c.set(xx, y1, O if i % 2 else BONE_R[0])
            c.set(xx, y1 - 1, BONE_R[2] if i % 2 else BONE_R[1])
    c.fill(pk.polyline([(x0 + 18, y1 - 28), (x0 + 17, y1 - 24), (x0 + 20, y1 - 21), (x0 + 19, y1 - 18)]), BONE_LINE)
    if cracked:
        c.fill(pk.polyline([(x0 + 12, y1 - 29), (x0 + 13, y1 - 22), (x0 + 10, y1 - 17), (x0 + 12, y1 - 12)]), O)
    if ph in ("closed", "idle") and lift == 0:
        c.set(x0 + 9, ey + 3, BLOOD[1] if t % 2 == 0 else BLOOD[2])   # o luminiță roșie în orbită
    return fin(c)


def host_face(lift, ph, t):
    """Fața de carne de sub craniu (se vede doar când capacul e ridicat)."""
    c = L()
    box = (11, 46 - max(lift, 2) - 3, 36, 46 + 6)
    m = {p for p in pk.ellipse(*box) if p[1] <= 46}
    sphere(c, m, FLESH_R, box)
    k = key(BLOOD[2])
    if lift >= 6:
        ey = 46 - lift + 2
        if ph == "hurt":
            pk.stamp(c, 15, ey, ["KK...", "..KKK", "KK..."], k)
            pk.stamp(c, 15, ey + 4, [".KKK.", "KkrkK", ".KKK."], k)
        else:
            pk.stamp(c, 16, ey, ["KKK", "KWK"], k)
            pk.stamp(c, 12, ey, ["KK", "WK"], k)
            if lift >= 8:
                op = ph in ("strike", "open1")
                pk.stamp(c, 13, ey + 4, [".KKKK.", "KkrrkK", "KkrRkK", ".KKKK."] if op else [".KKK.", "KkrkK", ".KKK."], k)
    return fin(c)


def draw_host_state(lift, ph, t, shots=False):
    c = L()
    jitter = 1 if (ph == "idle" and t % 2) else 0
    c.paste(host_face(lift, ph, t))
    sk = host_skull(lift, ph, t)
    c.paste(pk.shifted(sk, jitter, 0) if jitter else sk)
    if lift == 0:                                          # sânge care se prelinge de sub capac
        drip(c, 13, 44, 1 + t % 2, t, drop=False)
    if shots:                                              # Triple Shot: trei proiectile în evantai
        for (x, y) in [(0, 25), (3, 33), (0, 41)]:
            c.paste(fx_shot(48, 48, x, y, 5))
    return c


def draw_host(pose):
    ph, t = pose.ph, pose.t
    if ph == "idle":
        return draw_host_state(0, "idle", t)
    if ph == "wind":
        return draw_host_state(7, "open0", t)
    if ph == "strike":
        return draw_host_state(10, "strike", t, shots=True)
    if ph == "hurt":
        return draw_host_state(8, "hurt", t)
    raise ValueError(ph)


def dead_host(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 44, 7, lobes=[(10, 12, 5), (38, 12, 6)])
    c.paste(fin(pud))
    for (x, y, w_, h_) in [(30, 39, 7, 5), (7, 41, 5, 4)]:
        c.paste(gib(48, 48, x, y, w_, h_, FLESH_R))
    sk = host_skull(0, "dead", 0, cracked=True)            # craniul crăpat, înclinat pe o parte
    sk = squash_canvas(sk, 0.78)
    c.paste(pk.shifted(sk, -3, 0))
    return c


def squash_canvas(c, k):
    """Turtire pe verticală prin eșantionare (baza rămâne pe loc). Pentru resturi, nu pentru corpuri vii."""
    out = L(c.w, c.h)
    for y in range(c.h):
        sy = c.h - 1 - int((c.h - 1 - y) / k)
        if 0 <= sy < c.h:
            for x in range(c.w):
                col = c.get(x, sy)
                if col:
                    out.set(x, y, col)
    return out


# ───────────────────────────── NERVE ENDING ─────────────────────────────
# Terminația nervoasă: o tulpină de carne care crește din podea și se leagănă; nu atacă, doar blochează.

NERVE_R = [mx(SKIN[0], BLOOD[0], 0.3), mx(SKIN[1], BLOOD[0], 0.48), mx(SKIN[2], BLOOD[1], 0.55),
           mx(DEPTHS_FLESH, O, 0.2)]


def shade_tube(c, mask, ramp, cuts=(0.75, 0.25, -0.15)):
    """Umbrire de cilindru vertical: pe fiecare rând, stânga e luminată, dreapta în umbră."""
    rows = {}
    for x, y in mask:
        rows.setdefault(y, []).append(x)
    lx, lz = LIGHT[0], LIGHT[2]
    n = math.hypot(lx, lz)
    lx, lz = lx / n, lz / n
    for y, xs in rows.items():
        a, b = min(xs), max(xs)
        cx_, hw = (a + b + 1) / 2, max(1.0, (b - a + 1) / 2)
        for x in xs:
            u = max(-1.0, min(1.0, (x + 0.5 - cx_) / hw))
            i = u * lx + math.sqrt(max(0.0, 1 - u * u)) * lz
            col = ramp[0] if i > cuts[0] else ramp[1] if i > cuts[1] else ramp[2] if i > cuts[2] else ramp[3]
            c.set(x, y, col)


def nerve_path(ph, t):
    """Punctele tulpinii (x, y, rază), de la bază spre vârf."""
    if ph == "wind":                                       # se strânge înapoi, ca un arc
        return [(24, 45, 3.5), (25, 38, 3), (29, 30, 2.6), (31, 22, 2.4), (28, 16, 2.6)]
    if ph == "strike":                                     # biciuie spre stânga
        return [(24, 45, 3.5), (23, 38, 3), (19, 31, 2.6), (13, 27, 2.4), (7, 27, 2.6)]
    if ph == "hurt":
        return [(24, 45, 3.6), (26, 39, 3.2), (28, 33, 3), (30, 28, 2.8), (31, 23, 2.9)]
    s = [0, 1][t % 2]
    return [(24, 45, 3.5), (23, 38, 3), (23 + s, 30, 2.6), (25 + s, 22, 2.4), (24 + 2 * s, 13, 2.6)]


def draw_nerve_ending(pose):
    ph, t = pose.ph, pose.t
    c = L()
    roots = L()
    rm = pk.ellipse(15, 41, 33, 47) & pk.rect(0, 0, 47, 46)
    rm |= tube([(17, 44, 1.5), (11, 45, 1.2), (6, 46, 0.8)]) | tube([(31, 44, 1.5), (37, 45, 1.2), (42, 46, 0.8)])
    rm = {p for p in rm if p[1] <= 46}
    sphere(roots, rm, NERVE_R, (15, 39, 33, 50))
    c.paste(fin(roots))
    stalk = L()
    path = nerve_path(ph, t)
    sm = tube(path)
    # nodurile de-a lungul tulpinii (umflături)
    for i in (1, 2, 3):
        x, y, r = path[i]
        sm |= disc(x, y, r + 1.2)
    tip = path[-1]
    bulb = disc(tip[0], tip[1] - 2, 4.2) | disc(tip[0] - 2, tip[1] - 4, 2.6) | disc(tip[0] + 2, tip[1] - 5, 2.4)
    sm |= bulb
    sm = {p for p in sm if p[1] <= 44}
    shade_tube(stalk, sm, NERVE_R)
    # nervura întunecată care urcă pe tulpină + luciul de pe noduri
    for (a, b) in zip(path, path[1:]):
        stalk.fill([p for p in pk.line(int(a[0] + 1), int(a[1]), int(b[0] + 1), int(b[1])) if p in sm], NERVE_R[3])
    for i in (1, 2, 3):
        x, y, r = path[i]
        stalk.set(int(x - r), int(y - 1), NERVE_R[0])
    bx, by = tip[0] - 3, tip[1] - 6
    stalk.fill([(bx, by), (bx + 1, by), (bx, by + 1)], WHITE)
    if ph == "hurt":
        pass
    fin(stalk)
    c.paste(stalk)
    drip(c, int(tip[0]) + 2, int(tip[1]) + 2, 2 + t % 2, t)
    if ph == "strike":                                     # arcul loviturii
        arc = L()
        arc.fill([(9, 17), (12, 14), (16, 13), (20, 14), (5, 21)], mx(NERVE_R[1], WHITE, 0.3))
        c.paste(arc)
    if ph == "wind":                                       # Taunt: semnul „taunt” (zvâcnire)
        c.fill([(14, 12), (14, 13), (13, 15), (16, 9), (17, 8)], STATUS["taunt"])
    return c


def dead_nerve_ending(pose):
    c = L()
    pud = L()
    puddle(pud, 24, 46, 44, 5, lobes=[(10, 12, 4)])
    c.paste(fin(pud))
    roots = L()
    rm = {p for p in pk.ellipse(17, 40, 31, 47) if p[1] <= 46}
    sphere(roots, rm, NERVE_R, (17, 39, 31, 50))
    stub = disc(24, 39, 2.8)
    sphere(roots, stub, NERVE_R, None)
    roots.fill(pk.ellipse(22, 37, 26, 39), BLOOD[1])
    c.paste(fin(roots))
    st = L()                                              # tulpina căzută la stânga
    sm = tube([(18, 44, 2.4), (11, 43, 2.2), (5, 44, 2.2)]) | disc(4, 42, 3.4)
    sm = {p for p in sm if p[1] <= 46}
    sphere(st, sm, NERVE_R, None)
    c.paste(fin(st))
    return c


# ───────────────────────────── registrul ─────────────────────────────

STD_POSES = {
    "idle_0": dict(t=0),
    "idle_1": dict(t=1, sq=1),
    "attack_0": dict(t=0, ph="wind"),
    "attack_1": dict(t=1, ph="strike"),
    "hurt_0": dict(t=0, ph="hurt", sq=3),
}

ENEMIES = {
    # id: (latură pânză, funcția de desen, funcția „dead”), în ordinea etajelor din GDD
    "fly": (32, draw_fly, dead_fly),
    "gaper": (48, draw_gaper, dead_gaper),
    "pooter": (32, draw_pooter, dead_pooter),
    "horf": (48, draw_horf, dead_horf),
    "clotty": (48, draw_clotty, dead_clotty),
    "mulligan": (48, draw_mulligan, dead_mulligan),
    "hopper": (48, draw_hopper, dead_hopper),
    "maw": (48, draw_maw, dead_maw),
    "globin": (48, draw_globin, dead_globin),
    "charger": (48, draw_charger, dead_charger),
    "spider": (32, draw_spider, dead_spider),
    "knight": (48, draw_knight, dead_knight),
    "bony": (48, draw_bony, dead_bony),
    "leaper": (48, draw_leaper, dead_leaper),
    "host": (48, draw_host, dead_host),
    "nerve_ending": (48, draw_nerve_ending, dead_nerve_ending),
}

# portretul = o fereastră de 24×24 din idle_0 (x, y = colțul stânga-sus)
PORTRAIT_WIN = {"fly": (3, 2), "gaper": (11, 6), "pooter": (4, 2), "horf": (11, 8), "clotty": (12, 22),
                "mulligan": (8, 6), "hopper": (9, 12), "maw": (11, 5), "globin": (9, 8), "charger": (2, 24),
                "spider": (4, 6), "knight": (8, 8), "bony": (11, 4), "leaper": (4, 16), "host": (8, 16),
                "nerve_ending": (12, 4)}


def portrait_from(c, x, y):
    out = L(24, 24)
    out.paste(c, -x, -y)
    return out


def build_enemy(eid):
    size, draw, dead = ENEMIES[eid]
    frames = {}
    for name, kw in STD_POSES.items():
        cv = draw(Pose(**kw))
        if name == "hurt_0":
            cv = flash(cv)
        frames[name] = cv
    frames["dead_0"] = dead(Pose(ph="dead"))
    if eid == "host":                                      # capacul închis / ridicat
        frames["closed_0"] = draw_host_state(0, "closed", 0)
        frames["open_0"] = draw_host_state(8, "open0", 0)
        frames["open_1"] = draw_host_state(10, "open1", 1)
    if eid == "globin":
        frames["regen_0"] = regen_globin(Pose(ph="regen"))
    px, py = PORTRAIT_WIN[eid]
    frames["portrait"] = portrait_from(frames["idle_0"], px, py)
    return frames


def build_all(only=None):
    ids = [e for e in ENEMIES if only is None or e in only]
    return {e: build_enemy(e) for e in ids}


def write_all(enemies):
    for eid, frames in enemies.items():
        for name, cv in frames.items():
            pk.save_png(cv, os.path.join(OUT, eid, name + ".png"))


def review_sheet(enemies, path=None):
    sections = [(eid, list(frames.items())) for eid, frames in enemies.items()]
    pk.contact_sheet(sections, path or os.path.join(REVIEW, "enemies.png"), scale=6, cell_bg="#33282a",
                     title="Darkest Basement - enemies (x6), all facing left")


def main(argv):
    only = argv[1:] or None
    enemies = build_all(only)
    if only:
        review_sheet(enemies, os.environ.get("PREVIEW", "/tmp/enemies_preview.png"))
        return
    write_all(enemies)
    review_sheet(enemies)
    n = sum(len(v) for v in enemies.values())
    print("scrise %d sprite-uri în %s" % (n, os.path.relpath(OUT, ROOT)))


if __name__ == "__main__":
    main(sys.argv)
