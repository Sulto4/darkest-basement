"""Generatorul eroilor: isaac, cain, lilith, azazel + familiarul Incubus.

Rulare (din rădăcina repo-ului):  python3 tools/art/heroes.py
Scrie:
  Assets/_Game/Resources/Sprites/Heroes/<id>/{back_idle_0..1, back_walk_0..3, side_idle_0..1,
      side_attack_0..1, side_hurt_0, side_dead_0, portrait}.png
  Assets/_Game/Resources/Sprites/Familiars/incubus/{idle_0..1, attack_0}.png
  docs/art-review/heroes.png, docs/art-review/heroes-in-context.png

Metoda: fiecare cadru e o stivă de straturi (aripi / păr lung → picioare → trunchi → cap →
braț → efecte). Fiecare strat e desenat procedural, umbrit cu lumină din stânga-sus, apoi
primește conturul lui de 1 px. Când straturile se suprapun, conturul capului rămâne vizibil
peste trunchi, ca în sprite-urile din Isaac. Detaliile fine (ochi, guri, lacrimi) sunt
pixeli puși de mână prin pk.stamp().
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pixkit as pk                                   # noqa: E402
from palette import (OUTLINE, SKIN, TEAR, BLOOD, BASEMENT, AZAZEL, LILITH, CAIN,  # noqa: E402
                     UI, STATUS)

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT_HEROES = os.path.join(ROOT, "Assets", "_Game", "Resources", "Sprites", "Heroes")
OUT_FAM = os.path.join(ROOT, "Assets", "_Game", "Resources", "Sprites", "Familiars", "incubus")
REVIEW = os.path.join(ROOT, "docs", "art-review")

W = H = 48
O = OUTLINE

# ───────────────────────────── rampe de culoare ─────────────────────────────
# Fiecare rampă: [highlight, bază, umbră, umbră adâncă]. Culorile în plus sunt nuanțe ale paletei.

SKIN_R = [SKIN[0], SKIN[1], SKIN[2], SKIN[3]]
SKIN_LINE = pk.tint(SKIN[3], 0.55)                   # linii interioare pe piele (gură, urechi)
LIL_SKIN_R = [pk.tint(LILITH["skin"][0], 1.45), LILITH["skin"][0], LILITH["skin"][1],
              pk.tint(LILITH["skin"][1], 0.84)]
LIL_LINE = pk.tint(LILITH["skin"][1], 0.55)
# Azazel: baza e o nuanță puțin mai deschisă decât negrul din paletă, altfel conturul dispare.
AZ_SKIN_R = [pk.tint(AZAZEL["black"][0], 1.22), pk.tint(AZAZEL["black"][0], 1.1),
             AZAZEL["black"][0], AZAZEL["black"][1]]
AZ_LINE = AZAZEL["black"][1]
AZ_RIM = BLOOD[2]                                    # lumină roșiatică din spate (strălucirea iadului)
AZ_HORN_R = [pk.tint(AZAZEL["horn"], 1.3), AZAZEL["horn"], pk.tint(AZAZEL["horn"], 0.72),
             pk.tint(AZAZEL["horn"], 0.5)]
AZ_WING_R = [pk.tint(AZAZEL["horn"], 1.1), pk.mix(AZAZEL["horn"], AZAZEL["black"][0], 0.45),
             pk.mix(AZAZEL["horn"], AZAZEL["black"][0], 0.8), AZAZEL["black"][1]]
AZ_BONE = pk.tint(AZAZEL["horn"], 1.2)
INC_R = [pk.tint(LILITH["incubus"], 1.25), pk.tint(LILITH["incubus"], 1.1), LILITH["incubus"],
         pk.tint(LILITH["incubus"], 0.6)]

DIAPER_R = [pk.tint(UI["parchment"][0], 1.4), UI["parchment"][0], UI["parchment"][1],
            pk.tint(UI["parchment"][1], 0.8)]
CAIN_HAIR_R = ["#8a5a2b", CAIN["hair"][0], CAIN["hair"][1], pk.tint(CAIN["hair"][1], 0.75)]
CAIN_SHIRT_R = [pk.tint(CAIN["clothes"][0], 1.18), CAIN["clothes"][0], CAIN["clothes"][1],
                pk.tint(CAIN["clothes"][1], 0.72)]
CAIN_PANTS_R = ["#6b4423", "#4a2e17", pk.tint("#4a2e17", 0.8), pk.tint("#4a2e17", 0.62)]
LIL_HAIR_R = [pk.tint(LILITH["hair"][0], 1.25), LILITH["hair"][0], LILITH["hair"][1],
              pk.tint(LILITH["hair"][1], 0.7)]
LIL_DRESS_R = [pk.tint(LILITH["dress"], 1.22), LILITH["dress"], pk.tint(LILITH["dress"], 0.74),
               pk.tint(LILITH["dress"], 0.52)]
TEAR_R = [TEAR[0], TEAR[1], TEAR[2], pk.tint(TEAR[2], 0.7)]
BLOOD_R = [pk.tint(BLOOD[0], 1.3), BLOOD[0], BLOOD[1], BLOOD[2]]

EYE = pk.mix(OUTLINE, TEAR[2], 0.18)                 # ochi albastru foarte închis
GLINT = TEAR[0]
RED_EYE = AZAZEL["eye"]
RED_HOT = pk.tint(AZAZEL["eye"], 1.55)
INC_EYE = LILITH["incubus_eye"]
FANG = SKIN[0]

# Unde stă eroul pe pânza de 48×48: picioarele pe rândul 46, conturul lor pe 47, centrul pe x=24.
HEAD_BOX = (11, 11, 36, 32)                          # capul: 26×22 px (fără contur)
SPHERE_CUTS = (0.965, 0.2, -0.2)                     # praguri highlight / bază / umbră
SPHERE_LIGHT = (-0.42, -0.58, 0.7)


class Pose:
    """Parametrii unui cadru. Valorile implicite = poza de repaus."""

    def __init__(self, **kw):
        self.head_dx = 0          # deplasarea capului față de corp (aplecare)
        self.head_dy = 0
        self.body_dy = 0          # bob-ul întregului corp (mers)
        self.head_box = None      # cutia capului (None = HEAD_BOX)
        self.expr = "idle"        # idle / charge / shoot / hurt / dead
        self.arm = "down"         # down / back / punch / summon / point / recoil / claw
        self.legs = "stand"       # stand / brace / liftL / liftR / pass
        self.torso_squash = 0     # câte rânduri coboară umerii și capul (respirație)
        self.wing = 0             # faza aripilor (0 jos, 1 sus, 2 larg)
        self.hair = 0             # faza părului lung (Lilith)
        self.t = 0                # indicele cadrului, pentru detalii animate (picături)
        self.fx = []              # efecte (vezi FX)
        self.dx = 0               # deplasarea întregului personaj
        self.__dict__.update(kw)


def layer():
    return pk.Canvas(W, H)


def finish(c):
    """Conturul stratului (1 px, vecini pe laturi)."""
    pk.outline(c)
    return c


# ───────────────────────────── ochi și guri (pixeli puși de mână) ─────────────────────────────
# K = ochi, W = sclipire, B/b = lacrimă închisă/deschisă, L = linie pe piele, o = contur,
# R/H/r = roșu / roșu încins / roșu închis, m/t = gură / limbă, F = colț

def eye_key(line=SKIN_LINE):
    return {"K": EYE, "W": GLINT, "B": TEAR[2], "b": TEAR[1], "L": line, "o": O,
            "R": RED_EYE, "r": BLOOD[1], "H": RED_HOT, "m": BLOOD[2], "t": BLOOD[0],
            "F": FANG, "x": None}


# Ochiul apropiat (profil): oval mare, sclipire sus-stânga, lacrimi strânse jos.
EYE_NEAR = [".KKKK.",
            "KWWKKK",
            "KWWKKK",
            "KKKKKK",
            "KKKKWK",
            "KbbbbK",
            ".BBBB."]
# Ochiul îndepărtat e strivit de perspectivă.
EYE_FAR = [".KK",
           "KKK",
           "WKK",
           "KKK",
           "KKK",
           "bbK",
           ".B."]
EYE_FRONT = [".KKKK.",
             "KWWKKK",
             "KWWKKK",
             "KKKKKK",
             "KKKKWK",
             "KbbbbK",
             ".BBBB."]
EYE_CLENCH = ["KK...",                                # ochi strânși de durere: „>”
              "..KKK",
              "KK..."]
EYE_X = ["o...o",
         ".o.o.",
         "..o..",
         ".o.o.",
         "o...o"]
EYE_X_SMALL = ["o.o",
               ".o.",
               "o.o"]
MOUTH_SAD = [".LLL.",
             "L...L"]
MOUTH_WAIL = [".oo.",
              "omto",
              "omto",
              ".oo."]


def face_side(box):
    """Unde stau trăsăturile în profil, calculat de la marginea din dreapta-jos a capului."""
    x0, y0, x1, y1 = box
    return {"nx": x1 - 12, "fx": x1 - 4, "ey": y1 - 13, "mx": x1 - 9, "my": y1 - 3}


def tear_streak(c, x, y, n, t):
    """Lacrimă care curge pe obraz: dâră de 1 px + picătura de la capăt (se mută între cadre)."""
    for i in range(n):
        c.set(x, y + i, TEAR[1])
    c.set(x, y, TEAR[0])
    c.set(x, y + n + (t % 2), TEAR[2])


def shine_arc(c, mask, box, r0, r1, color, umax=0.3, vmax=-0.15):
    """Arcul de luciu pe păr: pixelii dintre razele relative r0..r1, doar în partea de sus-stânga."""
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2
    rx, ry = (x1 - x0 + 1) / 2, (y1 - y0 + 1) / 2
    for x, y in mask:
        u, v = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
        r = math.sqrt(u * u + v * v)
        if r0 <= r <= r1 and u < umax and v < vmax:
            c.set(x, y, color)


# ───────────────────────────── capete, vedere laterală ─────────────────────────────

def head_mask(box):
    x0, y0, x1, y1 = box
    m = pk.ellipse(x0, y0, x1, y1)
    m |= pk.ellipse(x0 + 4, y0 + 5, x1, y1)          # obrajii, puțin mai plini spre față
    return m


def shade_head(c, m, ramp, box):
    pk.shade_sphere(c, m, ramp, cuts=SPHERE_CUTS, light=SPHERE_LIGHT, box=box)


def head_side(hero, box, pose):
    """Capul din profil, cu fața spre dreapta. Întoarce un strat cu contur."""
    c = layer()
    m = head_mask(box)
    ramp = {"isaac": SKIN_R, "cain": SKIN_R, "lilith": LIL_SKIN_R, "azazel": AZ_SKIN_R}[hero]
    if hero == "azazel":
        horns_side(c, box)
    shade_head(c, m, ramp, box)
    {"isaac": face_isaac_side, "cain": face_cain_side, "lilith": face_lilith_side,
     "azazel": face_azazel_side}[hero](c, box, m, pose)
    pk.outline(c)
    return c


def face_isaac_side(c, box, m, pose):
    f = face_side(box)
    key = eye_key()
    e = pose.expr
    if e in ("idle", "charge", "shoot"):
        pk.stamp(c, f["nx"], f["ey"], EYE_NEAR, key)
        pk.stamp(c, f["fx"], f["ey"], EYE_FAR, key)
        if e == "idle":
            tear_streak(c, f["nx"] + 1, f["ey"] + 7, 2, pose.t)
            tear_streak(c, f["fx"] + 1, f["ey"] + 7, 1, pose.t + 1)
            pk.stamp(c, f["mx"], f["my"], MOUTH_SAD, key)
        elif e == "charge":
            # lacrima se umflă sub ochi, gata să plece; gura strânsă de efort
            pk.stamp(c, f["nx"] + 1, f["ey"] + 5, [".bbb.", "bWbbb", "bbbbB", ".BBB."], key)
            pk.stamp(c, f["mx"] + 1, f["my"], ["LL", "LL"], key)
        else:
            tear_streak(c, f["nx"] + 1, f["ey"] + 7, 3, 0)
            pk.stamp(c, f["mx"] + 1, f["my"] - 1, [".oo.", "oKKo", ".oo."], key)
    elif e == "hurt":
        pk.stamp(c, f["nx"], f["ey"] + 2, EYE_CLENCH, key)
        pk.stamp(c, f["fx"], f["ey"] + 2, ["..K", "KK.", "..K"], key)
        pk.stamp(c, f["mx"], f["my"] - 3, MOUTH_WAIL, key)
    elif e == "dead":
        pk.stamp(c, f["nx"], f["ey"] + 1, EYE_X, key)
        pk.stamp(c, f["fx"], f["ey"] + 2, EYE_X_SMALL, key)
        pk.stamp(c, f["mx"], f["my"] - 1, ["LLL.", ".tt.", ".tt."], key)


def face_cain_side(c, box, m, pose):
    x0, y0, x1, y1 = box
    f = face_side(box)
    key = eye_key()
    e = pose.expr
    hair_cain_side(c, box, m)
    if e in ("idle", "charge", "shoot"):
        pk.stamp(c, f["nx"], f["ey"], EYE_NEAR, key)
        lid = {"idle": 1, "charge": 3, "shoot": 0}[e]   # pleoapa: privire „cool” / încruntată
        for r in range(lid):
            c.fill([(f["nx"] + i, f["ey"] + r) for i in range(6)], SKIN_R[2] if r < lid - 1 else SKIN_LINE)
        if e == "idle":
            tear_streak(c, f["nx"] + 1, f["ey"] + 7, 1, pose.t)
            pk.stamp(c, f["mx"], f["my"] - 1, ["...L", "LLL."], key)        # zâmbet strâmb
        elif e == "charge":
            pk.stamp(c, f["mx"], f["my"] - 1, ["oooo", "oFFo"], key)          # dinți strânși
        else:
            pk.stamp(c, f["mx"], f["my"] - 2, [".oo.", "omto", ".oo."], key)  # strigă
    elif e == "hurt":
        pk.stamp(c, f["nx"], f["ey"] + 2, EYE_CLENCH, key)
        pk.stamp(c, f["mx"], f["my"] - 3, MOUTH_WAIL, key)
    elif e == "dead":
        pk.stamp(c, f["nx"], f["ey"] + 1, EYE_X, key)
        pk.stamp(c, f["mx"], f["my"] - 1, ["LLL.", ".tt.", ".tt."], key)
    patch_side(c, box, f)


def face_lilith_side(c, box, m, pose):
    x0, y0, x1, y1 = box
    f = face_side(box)
    key = eye_key(LIL_LINE)
    e = pose.expr
    hair_lilith_side_front(c, box, m, pose)
    if e == "idle":
        pk.stamp(c, f["mx"] + 1, f["my"], ["LL"], key)
        tear_streak(c, f["nx"] + 2, f["ey"] + 8, 2, pose.t)
    elif e == "charge":
        pk.stamp(c, f["mx"] + 1, f["my"], ["LL"], key)
    elif e == "shoot":
        pk.stamp(c, f["mx"] + 1, f["my"] - 1, [".oo.", "oKKo", ".oo."], key)
    elif e == "hurt":
        pk.stamp(c, f["mx"], f["my"], ["oooo", ".mt."], key)
    elif e == "dead":
        pk.stamp(c, f["mx"], f["my"] - 1, ["LLL.", ".tt.", ".tt."], key)


def hair_cain_side(c, box, m):
    """Părul lui Cain: calotă castanie pe creștet și ceafă, cu țepi spre spate și breton zimțat."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0 + 1, y1 - y0 + 1
    front_y = face_side(box)["ey"] - 2                  # fruntea se termină chiar deasupra ochiului
    back_y = y0 + h * 0.66                              # la ceafă părul coboară mai jos
    hair = set()
    for x, y in m:
        f = (x - x0) / w                               # 0 = ceafa (stânga), 1 = fața (dreapta)
        if y < back_y + (front_y - back_y) * f:
            hair.add((x, y))
    for (bx, tip) in [(x0 + 6, (x0 + 0, y0 - 3)), (x0 + 11, (x0 + 6, y0 - 5)),
                      (x0 + 16, (x0 + 13, y0 - 4)), (x0 + 21, (x0 + 20, y0 - 2))]:
        hair |= pk.polygon([(bx - 4, y0 + 3), tip, (bx + 3, y0 + 2)])
    fy = front_y - 2                                    # bretonul: trei șuvițe peste frunte
    for k, fxp in enumerate((x1 - 10, x1 - 7, x1 - 4)):
        hair |= pk.polygon([(fxp - 2, fy - 2), (fxp + 2.5, fy - 2), (fxp + 1.5, fy + 2 + (k == 1))])
    hair |= {p for p in pk.ellipse(x0 + 2, y0 + 8, x0 + 6, y0 + 14)}   # perciunul din spatele urechii
    pk.shade_sphere(c, hair, CAIN_HAIR_R, cuts=(0.93, 0.35, -0.1), light=SPHERE_LIGHT,
                    box=(x0, y0 - 3, x1, y1))
    for sx, sy in [(x0 + 5, y0 + 6), (x0 + 10, y0 + 4), (x0 + 15, y0 + 3)]:
        c.fill(pk.line(sx, sy, sx - 3, sy + 4), CAIN_HAIR_R[2])


def patch_side(c, box, f):
    """Peticul pe ochiul stâng (cel îndepărtat, în profil) + bareta subțire spre ceafă."""
    x0, y0, x1, y1 = box
    P, HL = CAIN["patch"], BASEMENT[2]
    c.fill(pk.polyline([(f["fx"] - 1, f["ey"] + 1), (f["fx"] - 3, f["ey"] - 2), (x0 + 4, y0 + 5)]), P)
    pk.stamp(c, f["fx"] - 1, f["ey"], [".PPPP",
                                       "PHHPP",
                                       "PHPPP",
                                       "PPPPP",
                                       "PPPPP",
                                       ".PPP."], {"P": P, "H": HL})


def hair_lilith_side_front(c, box, m, pose):
    """Părul negru-mov al lui Lilith pe cap: acoperă creștetul, ceafa și ochii (breton lung)."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0 + 1, y1 - y0 + 1
    f = face_side(box)
    by = f["ey"] + 6                                     # unde se termină bretonul (sub ochi)
    hair = set()
    for x, y in m:
        if (x - x0) / w < 0.5 or y < by:
            hair.add((x, y))
    for k in range(4):                                   # vârfurile bretonului, zimțate
        tx = x1 - 13 + k * 4
        hair |= pk.polygon([(tx - 1.5, by - 1), (tx + 2.5, by - 1), (tx + 0.5, by + 2 + (k % 2))])
    hair |= pk.ellipse(x0 - 1, y0 - 1, x1 - 1, y0 + h // 2)
    hb = (x0 - 1, y0 - 1, x1, y1)
    pk.shade_sphere(c, hair, LIL_HAIR_R, cuts=(0.95, 0.3, -0.15), light=SPHERE_LIGHT, box=hb)
    shine_arc(c, hair, hb, 0.6, 0.74, LIL_HAIR_R[0], umax=0.35)
    for sx in (x1 - 12, x1 - 8, x1 - 4):                 # șuvițe verticale pe breton
        c.fill(pk.line(sx, by - 5, sx + 1, by), LIL_HAIR_R[2])


def horns_side(c, box):
    """Coarnele lui Azazel: mici, curbate spre spate. Se desenează înaintea capului (sub el)."""
    x0, y0, x1, y1 = box
    for base_x, tip, far in [(x0 + 13, (x0 + 8, y0 - 6), True), (x0 + 19, (x0 + 15, y0 - 7), False)]:
        poly = pk.polygon([(base_x - 3, y0 + 4), (base_x - 2.5, y0 - 1), tip,
                           (base_x + 0.5, y0 - 2), (base_x + 3, y0 + 4)])
        ramp = AZ_HORN_R if not far else [AZ_HORN_R[1], AZ_HORN_R[2], AZ_HORN_R[3], AZ_HORN_R[3]]
        pk.shade_flat(c, poly, ramp, shadow=(1, 0), deep=None, light_rim=(-1, 0))
        c.set(base_x - 2, y0 + 0, ramp[2])               # inelele cornului
        c.set(base_x - 1, y0 - 2, ramp[2])


def face_azazel_side(c, box, m, pose):
    x0, y0, x1, y1 = box
    f = face_side(box)
    nx, fx, ey = f["nx"], f["fx"], f["ey"] - 1
    e = pose.expr
    key = eye_key(AZ_LINE)
    # marginea roșiatică jos (lumina iadului) ajută silueta pe fundal închis
    rim = pk.edge_band(m, 1, 1) & pk.edge_band(m, 1, 0)
    c.fill({p for p in rim if p[1] > (y0 + y1) / 2 and p[0] < x1 - 6}, AZ_RIM)
    if e != "dead":                                       # sprâncene încruntate: privirea de demon
        c.fill(pk.line(nx - 1, ey - 2, nx + 5, ey), AZ_LINE)
        c.fill(pk.line(fx - 1, ey, fx + 2, ey - 2), AZ_LINE)
    if e in ("idle", "charge", "shoot"):
        hot = e != "idle"
        pk.stamp(c, nx, ey, [".RRRR.", "RHHRRR", "RHHRRR", "RRRRRr", "rRRRrr", ".rrrr."] if not hot else
                 [".HHHH.", "HHHHHR", "HHHHRR", "RHHRRr", "rRRRrr", ".rrrr."], key)
        pk.stamp(c, fx, ey, [".R.", "RHR", "RHR", "RRR", "rRr", ".r."], key)
    elif e == "hurt":
        pk.stamp(c, nx, ey + 2, ["RR...", "..RRR", "RR..."], key)
        pk.stamp(c, fx, ey + 2, ["..R", "RR.", "..R"], key)
    elif e == "dead":
        pk.stamp(c, nx, ey + 1, EYE_X, eye_key())
        pk.stamp(c, fx, ey + 2, EYE_X_SMALL, eye_key())
    mx, my = x1 - 11, y1 - 4
    if e == "idle":                                       # rânjet cu colți
        pk.stamp(c, mx, my, ["Loooooo", "..F..F."], {"o": O, "F": FANG, "L": AZ_LINE})
    elif e == "charge":                                   # gura strânsă, roșul scapă pe la colț
        pk.stamp(c, mx, my, ["ooooooH", ".F...FH"], {"o": O, "F": FANG, "H": RED_HOT})
    elif e == "hurt":
        pk.stamp(c, mx + 1, my - 2, [".oooo.", "omttmo", "omttmo", ".oooo."], key)
    elif e == "dead":
        pk.stamp(c, mx, my - 1, ["ooooo.", ".F.tt.", "...tt."], {"o": O, "F": FANG, "t": BLOOD[0]})
    # „shoot”: gura deschisă e desenată de efectul beam, peste cap


# ───────────────────────────── capete, vedere din spate ─────────────────────────────

def ears_back(box, ramp):
    """Urechile văzute din spate: strat separat, sub cap, ca să le treacă conturul capului."""
    c = layer()
    x0, y0, x1, y1 = box
    for ex in (x0 - 2, x1 - 2):
        m = pk.ellipse(ex, y0 + 10, ex + 4, y0 + 15)
        pk.shade_flat(c, m, ramp, shadow=(1, 1), deep=None)
    return finish(c)


def head_back(hero, box, pose):
    c = layer()
    x0, y0, x1, y1 = box
    w, h = x1 - x0 + 1, y1 - y0 + 1
    cx = (x0 + x1 + 1) / 2
    m = pk.ellipse(x0, y0, x1, y1)
    if hero == "azazel":
        horns_back(c, box)
    ramp = {"isaac": SKIN_R, "cain": SKIN_R, "lilith": LIL_SKIN_R, "azazel": AZ_SKIN_R}[hero]
    shade_head(c, m, ramp, box)
    if hero == "isaac":
        # ceafa: două cute fine deasupra gâtului
        c.fill([(x, y1 - 2) for x in range(round(cx) - 4, round(cx) + 3)], SKIN_R[2])
    if hero == "cain":
        # părul coboară până aproape de gât; rămâne doar o fâșie de piele la ceafă, în mijloc
        nape = {(x, y) for x, y in m if y >= y1 - 2 and abs(x + 0.5 - cx) < 5}
        hair = {(x, y) for x, y in m} - nape
        for k in range(3):                               # trei vârfuri scurte peste ceafă
            bx = round(cx) - 5 + k * 4
            hair |= pk.polygon([(bx - 1, y1 - 3), (bx + 2.5, y1 - 3), (bx + 1, y1 + 0.5)])
        for (bx, tip) in [(x0 + 7, (x0 + 3, y0 - 3)), (x0 + 12, (x0 + 11, y0 - 5)),
                          (x0 + 18, (x0 + 20, y0 - 3))]:
            hair |= pk.polygon([(bx - 3, y0 + 3), tip, (bx + 3, y0 + 2)])
        hair = {p for p in hair if p in m or p[1] < y0 + 4}
        hb = (x0, y0 - 3, x1, y1)
        pk.shade_sphere(c, hair, CAIN_HAIR_R, cuts=(0.93, 0.15, -0.3), light=SPHERE_LIGHT, box=hb)
        shine_arc(c, hair, hb, 0.62, 0.72, CAIN_HAIR_R[0], umax=0.2)
        for sx in range(x0 + 4, x1 - 2, 4):              # șuvițe
            c.fill(pk.line(sx, y0 + 9, sx - 1, y0 + 15), CAIN_HAIR_R[2])
        # vârtejul de pe creștet: semnul clar că vedem ceafa
        pk.stamp(c, round(cx) - 2, y0 + 3, [".dd.", "d..d", "..dd"], {"d": CAIN_HAIR_R[3]})
        # bareta peticului: bandă de 2 px, un arc care coboară spre mijloc (cercul capului văzut de sus)
        strap = [(x, round(y0 + 8 + 2 * (1 - ((x + 0.5 - cx) / (w / 2)) ** 2))) for x in range(x0, x1 + 1)]
        c.fill([p for p in strap if p in m], O)
    pk.outline(c)
    return c


def horns_back(c, box):
    x0, y0, x1, y1 = box
    cx = (x0 + x1 + 1) / 2
    for side in (-1, 1):
        bx = cx + side * 6
        tip = (cx + side * 9, y0 - 6)
        poly = pk.polygon([(bx - 3, y0 + 4), tip, (bx + 3, y0 + 4)])
        pk.shade_flat(c, poly, AZ_HORN_R, shadow=(1, 0), deep=None, light_rim=(-1, 0))


# ───────────────────────────── corp, vedere laterală ─────────────────────────────

def mat(hero, part):
    """Rampa de material pentru o parte a corpului."""
    if hero == "isaac":
        return SKIN_R
    if hero == "cain":
        return {"torso": CAIN_SHIRT_R, "arm": CAIN_SHIRT_R, "hand": SKIN_R, "leg": CAIN_PANTS_R}[part]
    if hero == "lilith":
        return {"torso": LIL_DRESS_R, "arm": LIL_SKIN_R, "hand": LIL_SKIN_R, "leg": LIL_SKIN_R}[part]
    return AZ_SKIN_R


def legs_side(hero, pose, oy):
    """Picioarele din profil (picior din spate mai în umbră). oy = deplasare (Azazel plutește)."""
    far, near = layer(), layer()
    ramp = mat(hero, "leg")
    dim = [ramp[2], ramp[2], ramp[3], ramp[3]]
    if hero == "azazel":                                  # picioare atârnate, vârfurile în jos
        fl = pk.rect(20, 40 + oy, 22, 44 + oy) | {(21, 45 + oy)}
        nl = pk.rect(24, 40 + oy, 26, 44 + oy) | {(25, 45 + oy)}
    else:
        fl = pk.rect(20, 42, 22, 46) | pk.rect(21, 45, 23, 46)
        nl = pk.rect(24, 42, 26, 46) | pk.rect(25, 45, 27, 46)
        if pose.legs == "brace":                          # picioare depărtate la lovitură
            fl, nl = pk.shift(fl, -2, 0), pk.shift(nl, 1, 0)
    pk.shade_flat(far, fl, dim, shadow=(1, 1), deep=None)
    pk.shade_flat(near, nl, ramp, shadow=(1, 1), deep=None)
    if hero == "cain":                                    # pantofi închiși
        near.fill({p for p in nl if p[1] >= 45}, CAIN["patch"])
        far.fill({p for p in fl if p[1] >= 45}, CAIN["patch"])
    return finish(far), finish(near)


def torso_side(hero, pose, oy):
    c = layer()
    sq = pose.torso_squash
    if hero == "lilith":
        top = pk.ellipse(19, 31 + oy + sq, 28, 40 + oy)
        skirt = pk.polygon([(19.5, 36 + oy), (28.5, 36 + oy), (31.5, 44 + oy), (16.5, 44 + oy)])
        body = top | skirt
        pk.shade_flat(c, body, LIL_DRESS_R, shadow=(2, 1), deep=(1, 1))
        for fx in (21, 25):                               # pliuri pe fustă
            c.fill(pk.line(fx, 40 + oy, fx - 1, 43 + oy), LIL_DRESS_R[2])
        c.fill([(x, 44 + oy) for x in range(17, 32) if (x, 44 + oy) in body], LIL_DRESS_R[3])
        return finish(c)
    body = pk.ellipse(18, 31 + oy + sq, 29, 43 + oy, fat=0.15)
    pk.shade_flat(c, body, mat(hero, "torso"), shadow=(2, 1), deep=(1, 1))
    if hero == "isaac":
        dia = {p for p in body if p[1] >= 40 + oy}        # scutecul
        pk.shade_flat(c, dia, DIAPER_R, shadow=(2, 1), deep=(1, 1))
        c.fill([p for p in dia if p[1] == 40 + oy], DIAPER_R[2])
        c.set(20, 41 + oy, DIAPER_R[0])
    if hero == "cain":
        sh = {p for p in body if p[1] >= 41 + oy}         # pantaloni scurți
        pk.shade_flat(c, sh, CAIN_PANTS_R, shadow=(2, 1), deep=(1, 1))
    if hero == "azazel":
        c.fill(pk.ellipse(22, 36 + oy, 26, 41 + oy) & body, AZ_SKIN_R[0])   # burtică mai deschisă
        rim = pk.edge_band(body, 1, 1) & pk.edge_band(body, 1, 0)
        c.fill({p for p in rim if p[1] > 36 + oy}, AZ_RIM)
    return finish(c)


def arm_side(hero, pose, oy, hx=0):
    """Brațul din față. Pozițiile sunt alese de mână, ca la un animator, nu calculate."""
    c = layer()
    ramp, hand = mat(hero, "arm"), mat(hero, "hand")
    a = pose.arm
    if a == "down":                                       # ciot scurt, mâna pe burtă
        m = pk.ellipse(25, 36 + oy, 28, 41 + oy)
        hm = {p for p in m if p[1] >= 39 + oy}
    elif a == "back":                                     # pumnul tras înapoi
        m = pk.ellipse(14, 34 + oy, 22, 38 + oy)
        hm = pk.ellipse(11, 33 + oy, 16, 38 + oy)
        m |= hm
    elif a == "punch":                                    # brațul întins, pumn mare
        m = pk.ellipse(23, 34 + oy, 36, 38 + oy)
        hm = pk.ellipse(33, 32 + oy, 40, 39 + oy)
        m |= hm
    elif a == "summon":                                   # brațul jos-înainte, palma deschisă (Lilith cheamă)
        m = pk.polygon([(22, 35 + oy), (26, 34 + oy), (33, 40 + oy), (31, 43 + oy), (23, 39 + oy)])
        hm = pk.ellipse(30, 39 + oy, 35, 44 + oy)
        m |= hm
    elif a == "point":                                    # arată spre țintă cu degetul
        m = pk.ellipse(23, 34 + oy, 34, 37 + oy)
        hm = pk.ellipse(31, 33 + oy, 35, 37 + oy) | pk.rect(35, 34 + oy, 38, 35 + oy)
        m |= hm
    elif a == "recoil":                                   # brațul aruncat înapoi de lovitură
        m = pk.ellipse(14, 35 + oy, 22, 39 + oy)
        hm = pk.ellipse(12, 34 + oy, 16, 39 + oy)
        m |= hm
    elif a == "claw":                                     # Azazel: gheara înainte
        m = pk.ellipse(23, 34 + oy, 32, 38 + oy)
        hm = pk.ellipse(29, 33 + oy, 34, 38 + oy)
        m |= hm
    else:
        raise ValueError(a)
    m, hm = pk.shift(m, hx, 0), pk.shift(hm, hx, 0)
    pk.shade_flat(c, m, ramp, shadow=(1, 1), deep=(1, 1), light_rim=(-1, -1))
    if hand is not ramp:
        pk.shade_flat(c, hm, hand, shadow=(1, 1), deep=(1, 1), light_rim=(-1, -1))
    if a == "punch":                                      # încheieturile degetelor
        for yy in (34, 36):
            c.set(39 + hx, yy + oy, hand[3])
    if hero == "azazel":                                  # gheare albe
        tips = {"claw": [(34, 37), (33, 39)]}.get(a, [])
        for tx, ty in tips:
            c.set(tx + hx, ty + oy, FANG)
    return finish(c)


# Aripa de liliac în profil, ca listă de puncte relative la umăr: încheietura (cu gheara),
# vârful, apoi marginea zimțată de membrană înapoi spre corp. Trei faze de bătaie.
WING_SIDE = {
    0: [(0, 0), (-3, -6), (-7, -10), (-15, -8), (-12, -5), (-14, -1), (-10, -1), (-10, 4), (-6, 2), (-3, 4)],
    1: [(0, 0), (-2, -7), (-5, -14), (-14, -16), (-11, -11), (-15, -8), (-10, -6), (-11, -1), (-6, -2), (-3, 2)],
    2: [(0, 0), (-4, -6), (-9, -9), (-19, -6), (-15, -3), (-17, 1), (-12, 1), (-11, 5), (-7, 3), (-3, 4)],
}


def wing_poly(c, pts, ramp, bone):
    m = pk.polygon(pts)
    pk.shade_flat(c, m, ramp, shadow=(1, 1), deep=None)
    r = [(round(x), round(y)) for x, y in pts]
    c.fill(pk.polyline([r[0], r[2], r[3]]), bone)        # brațul aripii
    for k in (5, 7):                                      # degetele, până în zimți
        c.fill(pk.line(*r[2], *r[k]), ramp[3])
    c.fill([(r[2][0], r[2][1] - 1)], AZ_HORN_R[0])        # gheruța de la încheietură
    return m


def wings_side(pose, oy, hx=0):
    """Aripa lui Azazel, în spatele corpului, cu marginea zimțată. pose.wing alege faza."""
    c = layer()
    sx, sy = 20 + hx, 33 + oy
    pts = [(sx + dx, sy + dy) for dx, dy in WING_SIDE[pose.wing]]
    far = [(x + 4, y - 2) for x, y in pts]                # aripa din spate, puțin decalată
    wing_poly(c, far, [AZ_WING_R[2], AZ_WING_R[2], AZ_WING_R[3], AZ_WING_R[3]], AZ_WING_R[1])
    finish(c)
    near = layer()
    wing_poly(near, pts, AZ_WING_R, AZ_BONE)
    c.paste(finish(near))
    return c


def hair_lilith_back_side(pose, oy, box):
    """Părul lung al lui Lilith, văzut din profil: cade pe spate până aproape de pământ."""
    c = layer()
    x0, y0, x1, y1 = box
    s = [0, 1, -1][pose.hair % 3]
    # silueta: o „pelerină” de păr care coboară până aproape de pământ și se evazează în spate
    pts = [(x0 + 1, y0 + 8), (x0 + 13, y0 + 6), (x0 + 15, y1 + 4), (x0 + 14 + s, 45 + oy),
           (x0 + 11 + s, 42 + oy), (x0 + 8 + s, 46 + oy), (x0 + 5 + s, 42 + oy), (x0 + 2 + s, 46 + oy),
           (x0 - 1 + s, 42 + oy), (x0 - 3 + 2 * s, 45 + oy), (x0 - 2, y1 + 1), (x0 - 1, y1 - 3)]
    m = pk.polygon(pts)
    pk.shade_flat(c, m, LIL_HAIR_R, shadow=(2, 1), deep=(1, 1), light_rim=(-1, 0))
    for sx in (x0 + 1, x0 + 5, x0 + 9):
        c.fill(pk.line(sx, y1 - 1, sx + s, 41 + oy), LIL_HAIR_R[2])
    return finish(c)


# ───────────────────────────── efecte ─────────────────────────────

def fx_tear(x0, y0, size=6):
    """Lacrima-proiectil a lui Isaac: sferă albastră cu sclipire, contur închis."""
    c = layer()
    m = pk.ellipse(x0, y0, x0 + size - 1, y0 + size - 1)
    pk.shade_sphere(c, m, TEAR_R, cuts=(0.9, 0.2, -0.3), light=SPHERE_LIGHT)
    c.set(x0 + 1, y0 + 1, "#ffffff")
    return finish(c)


def fx_drops(pts, ramp):
    """Picături de 2×2 (lacrimi sau sânge) cu un pixel de luciu."""
    c = layer()
    for x, y in pts:
        c.fill([(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)], ramp[1])
        c.set(x, y, ramp[0])
    return finish(c)


def fx_punch(x, y, r=4):
    """Steluța de impact la încheieturile pumnului lui Cain (galben „stun”, miez alb)."""
    c = layer()
    k = r * 0.32
    star = pk.polygon([(x, y - r), (x + k, y - k), (x + r, y), (x + k, y + k),
                       (x, y + r), (x - k, y + k), (x - r, y), (x - k, y - k)])
    c.fill(star, STATUS["stun"])
    c.fill(pk.ellipse(x - 1, y - 1, x + 1, y + 1), "#fff0e8")
    return finish(c)


def fx_point(x, y):
    """Scânteia de la vârful degetului lui Lilith (comanda pentru Incubus)."""
    c = layer()
    glow = pk.tint(LILITH["dress"], 1.6)
    c.fill([(x, y - 2), (x, y + 2), (x - 2, y), (x + 2, y), (x, y - 1), (x, y + 1), (x - 1, y),
            (x + 1, y)], glow)
    c.set(x, y, "#fff0e8")
    c.fill([(x + 3, y - 3), (x + 4, y + 2)], glow)
    return c


def fx_beam(mx, my):
    """Gura deschisă a lui Azazel și începutul razei Brimstone (restul îl desenează FX-ul din joc)."""
    c = layer()
    maw = pk.ellipse(mx - 2, my - 4, mx + 5, my + 4)
    c.fill(maw, BLOOD[2])
    for (dy0, dy1, col) in [(-4, 4, BLOOD[1]), (-3, 3, BLOOD[0]), (-1, 1, RED_HOT), (0, 0, "#fff0e8")]:
        c.fill(pk.rect(mx + 4, my + dy0, W - 1, my + dy1), col)
    bulb = pk.ellipse(mx + 2, my - 5, mx + 9, my + 5)    # „bulbul” razei la gură
    c.fill(bulb - pk.rect(0, my - 3, W, my + 3), BLOOD[0])
    c.fill(pk.ellipse(mx + 3, my - 3, mx + 8, my + 3), RED_HOT)
    c.fill(pk.ellipse(mx + 4, my - 1, mx + 7, my + 1), "#fff0e8")
    finish(c)
    pk.stamp(c, mx - 1, my - 4, [".F.F", ".F.F"], {"F": FANG})   # colții peste gură
    pk.stamp(c, mx, my + 3, ["F.F"], {"F": FANG})
    return c


def fx_charge(mx, my):
    """Glob roșu care se adună în gura lui Azazel, cu particule atrase spre el."""
    c = layer()
    orb = layer()
    orb.fill(pk.ellipse(mx, my - 2, mx + 4, my + 2), BLOOD[0])
    orb.fill(pk.ellipse(mx + 1, my - 1, mx + 3, my + 1), RED_HOT)
    orb.set(mx + 2, my, "#fff0e8")
    c.paste(finish(orb))
    for (x, y) in [(mx + 8, my - 6), (mx + 10, my + 2), (mx + 6, my + 6), (mx + 12, my - 2)]:
        c.fill([(x, y), (x + 1, y)], BLOOD[0])
        c.set(x + 2, y, BLOOD[2])
    return c


FX = {"tear": fx_tear, "punch": fx_punch, "point": fx_point, "beam": fx_beam,
      "charge": fx_charge,
      "blood": lambda pts: fx_drops(pts, BLOOD_R),
      "tears": lambda pts: fx_drops(pts, TEAR_R)}


# ───────────────────────────── cadre laterale ─────────────────────────────

FLOAT = {"azazel": 3}


def ground_shadow(c, cx=24, w=14):
    """Umbra de pe sol a eroului care plutește (semi-transparentă, fără anti-aliasing)."""
    c.fill(pk.ellipse(cx - w // 2, 45, cx + w // 2 - 1, 47), pk.with_alpha(OUTLINE, 150))


def side_frame(hero, pose):
    c = layer()
    fl = FLOAT.get(hero, 0)
    oy = pose.body_dy - fl
    hx, hy = pose.head_dx, pose.head_dy
    sq = pose.torso_squash
    if fl:
        ground_shadow(c)
    b = pose.head_box or HEAD_BOX
    box = (b[0] + hx, b[1] + hy + oy + sq, b[2] + hx, b[3] + hy + oy + sq)

    # pose.dx apleacă tot corpul de deasupra picioarelor; picioarele rămân pe loc (pivotul nu fuge)
    lean = lambda layer_: pk.shifted(layer_, pose.dx, 0) if pose.dx else layer_
    far_leg, near_leg = legs_side(hero, pose, oy)
    stack = []
    if hero == "azazel":
        stack.append(lean(wings_side(pose, oy + sq, hx // 2)))
    if hero == "lilith":
        stack.append(lean(hair_lilith_back_side(pose, oy, box)))
    stack += [far_leg, lean(torso_side(hero, pose, oy)), near_leg]
    arm = lean(arm_side(hero, pose, oy + sq, hx // 2))
    head = lean(head_side(hero, box, pose))
    if pose.arm in ("back", "recoil"):                    # brațul din spatele capului
        stack += [arm, head]
    else:
        stack += [head, arm]
    for f in pose.fx:
        stack.append(FX[f[0]](*f[1:]))
    for s in stack:
        c.paste(s)
    return c


def side_poses(hero):
    """Pozele laterale ale unui erou: idle ×2 (respirație), attack ×2 (anticipare, lovitură), hurt."""
    poses = {
        "side_idle_0": Pose(t=0, wing=1, hair=0),
        "side_idle_1": Pose(t=1, torso_squash=1, wing=0, hair=1),
    }
    if hero == "isaac":
        poses["side_attack_0"] = Pose(expr="charge", head_dx=-2, head_dy=1, torso_squash=1, dx=-1)
        poses["side_attack_1"] = Pose(expr="shoot", head_dx=2, head_dy=-1, dx=1,
                                      fx=[("tear", 40, 18, 6), ("tears", [(36, 23), (38, 27)])])
    elif hero == "cain":
        poses["side_attack_0"] = Pose(expr="charge", head_dx=-2, torso_squash=1, arm="back", dx=-1)
        poses["side_attack_1"] = Pose(expr="shoot", head_dx=2, arm="punch", legs="brace",
                                      fx=[("punch", 42, 35)])
    elif hero == "lilith":
        poses["side_attack_0"] = Pose(expr="charge", head_dx=-1, torso_squash=1, arm="summon", hair=2,
                                      fx=[("point", 36, 44)])
        poses["side_attack_1"] = Pose(expr="shoot", head_dx=1, arm="point", hair=1,
                                      fx=[("point", 41, 34)])
    elif hero == "azazel":
        poses["side_attack_0"] = Pose(expr="charge", head_dx=-2, head_dy=-1, torso_squash=1, wing=1,
                                      dx=-1, fx=[("charge", 27, 24)])
        poses["side_attack_1"] = Pose(expr="shoot", head_dx=2, wing=2, arm="claw", dx=1,
                                      fx=[("beam", 33, 24)])
    poses["side_hurt_0"] = Pose(expr="hurt", head_box=(9, 15, 36, 32), head_dx=-2, arm="recoil",
                                dx=-1, wing=2, hair=2,
                                fx=[("blood", [(39, 15), (42, 20), (36, 11)]),
                                    ("tears", [(10, 11), (5, 15)])])
    return poses


# ───────────────────────────── cadrul „mort” ─────────────────────────────

def puddle(c, ramp=None):
    """Balta de sânge: o pată plată, organică, cu margine închisă și câteva sclipiri."""
    ramp = ramp or BLOOD_R
    m = pk.ellipse(2, 41, 45, 46) | pk.ellipse(6, 39, 20, 44) | pk.ellipse(30, 40, 43, 45)
    c.fill(m, ramp[2])
    c.fill(pk.edge_band(m, 0, 1) | pk.edge_band(m, 1, 1), ramp[3])
    c.fill(pk.edge_band(m, 0, -1) & pk.edge_band(m, -1, -1), ramp[1])
    for gx, gy in ((8, 40), (9, 40), (33, 41), (40, 43)):
        c.set(gx, gy, ramp[0])
    return m


def side_dead(hero):
    """Cadavrul: capul turtit într-o baltă de sânge, ochi X, limba scoasă, un picior moale în spate."""
    c = layer()
    pud = layer()
    if hero == "azazel":                                  # Azazel lasă o baltă de smoală și sânge
        puddle(pud, [BLOOD[1], AZ_SKIN_R[1], AZ_SKIN_R[2], AZ_SKIN_R[3]])
        pud.fill(pk.ellipse(30, 42, 44, 46), BLOOD[1])
    else:
        puddle(pud)
    c.paste(finish(pud))
    if hero == "lilith":                                  # rochia mov, mototolită în spate
        dl = layer()
        dm = pk.polygon([(2, 45), (4, 38), (10, 35), (16, 37), (17, 45)])
        pk.shade_flat(dl, dm, LIL_DRESS_R, shadow=(2, 1), deep=(1, 1), light_rim=(-1, -1))
        c.paste(finish(dl))
    leg = layer()
    lm = pk.polygon([(4, 41), (8, 36), (11, 38), (8, 43)])
    pk.shade_flat(leg, lm, mat(hero, "leg"), shadow=(1, 1), deep=None)
    if hero == "cain":
        leg.fill({p for p in lm if p[1] <= 38}, CAIN["patch"])
    c.paste(finish(leg))
    if hero == "lilith":                                  # părul împrăștiat în baltă
        hl = layer()
        hm = pk.polygon([(10, 45), (12, 35), (24, 30), (28, 45)]) | pk.polygon([(34, 44), (40, 37), (45, 45)])
        pk.shade_flat(hl, hm, LIL_HAIR_R, shadow=(2, 1), deep=(1, 1), light_rim=(-1, -1))
        for sx in (15, 20):
            hl.fill(pk.line(sx, 35, sx - 3, 44), LIL_HAIR_R[2])
        c.paste(finish(hl))
    if hero == "azazel":                                  # aripa ruptă, căzută
        w = layer()
        wing_poly(w, [(30, 40), (34, 36), (38, 31), (46, 33), (43, 36), (45, 39), (40, 39), (39, 42)],
                  AZ_WING_R, AZ_BONE)
        c.paste(finish(w))
    head = head_side(hero, (11, 29, 36, 45), Pose(expr="dead"))
    c.paste(head)
    drops = layer()                                       # stropi în jur
    for x, y in [(3, 36), (44, 36), (40, 31), (6, 33)]:
        drops.fill([(x, y), (x + 1, y), (x, y + 1)], BLOOD[0])
    c.paste(finish(drops))
    trickle = [(29, 41), (29, 42), (30, 43)]              # sânge din gură
    c.fill(trickle, BLOOD[0])
    return c


# ───────────────────────────── cadre din spate ─────────────────────────────

def back_frame(hero, pose):
    c = layer()
    fl = FLOAT.get(hero, 0)
    oy = pose.body_dy - fl
    sq = pose.torso_squash
    if fl:
        ground_shadow(c)
    box = (HEAD_BOX[0] + pose.head_dx, HEAD_BOX[1] + oy + pose.head_dy + sq,
           HEAD_BOX[2] + pose.head_dx, HEAD_BOX[3] + oy + pose.head_dy + sq)
    stack = legs_back(hero, pose, oy)
    stack.append(torso_back(hero, pose, oy))
    stack += arms_back(hero, pose, oy)
    if hero == "azazel":
        stack.append(wings_back(pose, oy))
    if hero in ("isaac", "cain"):
        stack.append(ears_back(box, SKIN_R))
    stack.append(head_back(hero, box, pose))
    if hero == "lilith":
        stack.append(hair_lilith_back(pose, oy, box))
    for s in stack:
        c.paste(s)
    return c


def legs_back(hero, pose, oy):
    """Picioarele din spate. Piciorul ridicat (care împinge) arată talpa, mai închisă."""
    out = []
    ramp = mat(hero, "leg")
    for side, x in (("L", 19), ("R", 25)):
        c = layer()
        lift = 2 if pose.legs == "lift" + side else 0
        if hero == "azazel":
            m = pk.rect(x + 1, 40 + oy, x + 3, 44 + oy) | {(x + 2, 45 + oy)}
            m = pk.shift(m, 0, -lift // 2)
        else:
            m = pk.rect(x, 42 + oy - lift, x + 3, 46 - lift)
        pk.shade_flat(c, m, ramp, shadow=(1, 1), deep=None)
        if lift and hero != "azazel":
            y_end = 46 - lift
            c.fill([(xx, y_end) for xx in range(x, x + 4)], ramp[3])
            c.fill([(xx, y_end - 1) for xx in range(x, x + 4)], ramp[2])
        if hero == "cain":
            c.fill({p for p in m if p[1] >= 45 - lift}, CAIN["patch"])
        out.append(finish(c))
    return out


def torso_back(hero, pose, oy):
    c = layer()
    sq = pose.torso_squash
    if hero == "lilith":
        body = pk.ellipse(18, 31 + oy + sq, 29, 40 + oy) | \
            pk.polygon([(18.5, 36 + oy), (29.5, 36 + oy), (32.5, 44 + oy), (15.5, 44 + oy)])
        pk.shade_flat(c, body, LIL_DRESS_R, shadow=(2, 1), deep=(1, 1))
        c.fill([p for p in body if p[1] == 44 + oy], LIL_DRESS_R[3])
        for fx in (19, 28):
            c.fill(pk.line(fx, 40 + oy, fx + (1 if fx < 24 else -1) * -1, 43 + oy), LIL_DRESS_R[2])
        return finish(c)
    body = pk.ellipse(17, 31 + oy + sq, 30, 43 + oy, fat=0.2)
    pk.shade_flat(c, body, mat(hero, "torso"), shadow=(2, 1), deep=(1, 1))
    if hero == "isaac":
        dia = {p for p in body if p[1] >= 40 + oy}
        pk.shade_flat(c, dia, DIAPER_R, shadow=(2, 1), deep=(1, 1))
        c.fill([p for p in dia if p[1] == 40 + oy], DIAPER_R[2])
        c.fill([(23, y) for y in range(36 + oy, 39 + oy)], SKIN_R[2])     # șira spinării
    if hero == "cain":
        sh = {p for p in body if p[1] >= 41 + oy}
        pk.shade_flat(c, sh, CAIN_PANTS_R, shadow=(2, 1), deep=(1, 1))
        c.fill([(23, y) for y in range(35 + oy, 40 + oy)], CAIN_SHIRT_R[2])
    return finish(c)


def arms_back(hero, pose, oy):
    """Brațele de pe lângă corp; la mers se balansează invers față de picioare."""
    out = []
    ramp, hand = mat(hero, "arm"), mat(hero, "hand")
    swing = {"liftL": (1, -1), "liftR": (-1, 1)}.get(pose.legs, (0, 0))
    for (x0, x1), s in (((13, 17), swing[0]), ((30, 34), swing[1])):
        c = layer()
        y0 = 34 + oy + pose.torso_squash + s
        m = pk.ellipse(x0, y0, x1, y0 + 6)
        pk.shade_flat(c, m, ramp, shadow=(1, 1), deep=(1, 1))
        if hand is not ramp:
            pk.shade_flat(c, {p for p in m if p[1] >= y0 + 4}, hand, shadow=(1, 1), deep=None)
        out.append(finish(c))
    return out


def wings_back(pose, oy):
    """Aripile văzute din spate: pornesc din omoplați și se deschid larg, peste umeri."""
    c = layer()
    lift = {0: 2, 1: -3, 2: 0}[pose.wing]
    for side in (-1, 1):
        def P(dx, dy):
            return (24 + side * dx, 35 + oy + dy + (lift if abs(dx) > 6 else lift // 2))
        pts = [P(3, 0), P(7, -6), P(12, -11), P(22, -8), P(19, -4), P(21, 0), P(15, -1),
               P(15, 4), P(10, 2), P(6, 5)]
        w = layer()
        wing_poly(w, pts, AZ_WING_R, AZ_BONE)
        c.paste(finish(w))
    return c


def hair_lilith_back(pose, oy, box):
    """Părul lung din spate: acoperă toată ceafa și cade până la talie, cu vârfuri zimțate.
    E mai îngust jos decât capul, ca să se vadă umerii, brațele și fusta."""
    c = layer()
    x0, y0, x1, y1 = box
    s = [0, 1, -1][pose.hair % 3]
    m = pk.ellipse(x0 - 1, y0 - 1, x1 + 1, y1 + 1)
    # coada cade drept din cea mai lată parte a capului, se îngustează puțin și se termină în
    # vârfuri zimțate deasupra fustei: o siluetă de „piatră de mormânt” pe care n-o are nimeni altcineva
    tail = pk.polygon([(x0, (y0 + y1) / 2), (x1 + 1, (y0 + y1) / 2), (x1 - 1 + s, 37 + oy),
                       (x1 - 3 + s, 40 + oy), (x1 - 6 + s, 38 + oy), (x1 - 9 + s, 41 + oy),
                       (24 + s, 39 + oy), (x0 + 9 + s, 41 + oy), (x0 + 6 + s, 38 + oy),
                       (x0 + 3 + s, 40 + oy), (x0 + 1 + s, 37 + oy)])
    m |= tail
    hb = (x0 - 1, y0 - 1, x1 + 1, y1 + 1)
    pk.shade_sphere(c, m, LIL_HAIR_R, cuts=(0.95, 0.3, -0.2), light=SPHERE_LIGHT, box=hb)
    c.fill({p for p in tail if p[1] > y1 + 1} & pk.edge_band(m, 2, 0), LIL_HAIR_R[2])
    shine_arc(c, m, hb, 0.6, 0.74, LIL_HAIR_R[0], umax=0.4)
    for sx in (x0 + 5, x0 + 10, x1 - 9, x1 - 4):         # șuvițe
        c.fill(pk.line(sx, y0 + 14, sx + s, 37 + oy), LIL_HAIR_R[2])
    return finish(c)


def back_poses():
    return {
        "back_idle_0": Pose(t=0, wing=0),
        "back_idle_1": Pose(t=1, torso_squash=1, hair=1, wing=2),
        "back_walk_0": Pose(legs="liftL", head_dx=-1, wing=0, hair=1),
        "back_walk_1": Pose(legs="pass", body_dy=-1, wing=1, hair=0),
        "back_walk_2": Pose(legs="liftR", head_dx=1, wing=0, hair=2),
        "back_walk_3": Pose(legs="pass", body_dy=-1, wing=1, hair=0),
    }


# ───────────────────────────── portrete (față) ─────────────────────────────

def portrait(hero):
    """Fața văzută din față, 24×24, pentru bara de ture și rama cărților."""
    c = pk.Canvas(24, 24)
    box = {"azazel": (2, 5, 21, 23), "cain": (2, 4, 21, 22), "lilith": (2, 2, 21, 20),
           "isaac": (1, 2, 22, 21)}[hero]
    x0, y0, x1, y1 = box
    cx = (x0 + x1 + 1) // 2
    m = pk.ellipse(x0, y0, x1, y1)
    ramp = {"isaac": SKIN_R, "cain": SKIN_R, "lilith": LIL_SKIN_R, "azazel": AZ_SKIN_R}[hero]
    if hero == "azazel":
        for side in (-1, 1):
            poly = pk.polygon([(cx + side * 5 - 2, y0 + 3), (cx + side * 8, y0 - 5), (cx + side * 5 + 2.5, y0 + 3)])
            pk.shade_flat(c, poly, AZ_HORN_R, shadow=(1, 0), deep=None, light_rim=(-1, 0))
    if hero == "lilith":
        back = pk.polygon([(x0 - 1, y0 + 6), (x1 + 1, y0 + 6), (x1 + 2, 23), (x0 - 2, 23)])
        pk.shade_flat(c, back, LIL_HAIR_R, shadow=(1, 1), deep=None)
    pk.shade_sphere(c, m, ramp, cuts=SPHERE_CUTS, light=SPHERE_LIGHT, box=box)
    key = eye_key()
    ey = y1 - 12
    if hero == "isaac":
        pk.stamp(c, cx - 8, ey, EYE_FRONT, key)
        pk.stamp(c, cx + 2, ey, EYE_FRONT, key, flip=True)
        pk.stamp(c, cx - 2, ey + 9, MOUTH_SAD, key)
        tear_streak(c, cx - 6, ey + 7, 2, 0)
        tear_streak(c, cx + 5, ey + 7, 3, 1)
    elif hero == "cain":
        hair = {(x, y) for x, y in m if y < y0 + 6}
        for k in range(5):
            bx = x0 + 2 + k * 4
            hair |= pk.polygon([(bx - 1, y0 + 5), (bx + 3, y0 + 5), (bx + 2, y0 + 8)])
        hair |= pk.polygon([(x0 + 3, y0 + 2), (x0 + 5, y0 - 3), (x0 + 9, y0 + 1)])
        hair |= pk.polygon([(x0 + 9, y0 + 1), (x0 + 12, y0 - 4), (x0 + 14, y0 + 1)])
        hair |= pk.polygon([(x0 + 14, y0 + 1), (x0 + 18, y0 - 2), (x0 + 18, y0 + 3)])
        hair = {p for p in hair if p[1] >= 0}
        pk.shade_sphere(c, hair, CAIN_HAIR_R, cuts=(0.9, 0.3, -0.1), light=SPHERE_LIGHT, box=(x0, y0 - 3, x1, y1))
        pk.stamp(c, cx - 8, ey, EYE_FRONT, key)
        c.fill([(cx - 8 + i, ey) for i in range(6)], SKIN_LINE)
        # peticul pe ochiul stâng al lui Cain = în dreapta imaginii; bareta trece pe frunte
        c.fill(pk.line(cx + 2, ey, x1, y0 + 6), CAIN["patch"])
        c.fill(pk.line(cx - 1, ey - 1, x0 + 1, y0 + 6), CAIN["patch"])
        pk.stamp(c, cx + 1, ey - 1, [".PPPPP.", "PHHPPPP", "PHPPPPP", "PPPPPPP", "PPPPPPP", ".PPPPP."],
                 {"P": CAIN["patch"], "H": BASEMENT[2]})
        pk.stamp(c, cx - 3, ey + 9, ["....L", ".LLL."], key)
        tear_streak(c, cx - 6, ey + 7, 1, 0)
    elif hero == "lilith":
        hair = {(x, y) for x, y in m if y < y0 + 10}
        for k in range(6):
            bx = x0 + 1 + k * 3.4
            hair |= pk.polygon([(bx - 0.5, y0 + 9), (bx + 3, y0 + 9), (bx + 1.5, y0 + 12 + (k % 2))])
        pk.shade_sphere(c, hair, LIL_HAIR_R, cuts=(0.92, 0.3, -0.1), light=SPHERE_LIGHT, box=box)
        shine_arc(c, hair, box, 0.62, 0.76, LIL_HAIR_R[0], umax=0.45)
        for sx in (x0 + 5, cx, x1 - 5):
            c.fill(pk.line(sx, y0 + 5, sx, y0 + 10), LIL_HAIR_R[2])
        pk.stamp(c, cx - 1, y1 - 3, ["LL"], eye_key(LIL_LINE))
        tear_streak(c, cx - 5, y0 + 13, 2, 0)
        tear_streak(c, cx + 4, y0 + 13, 1, 1)
    elif hero == "azazel":
        c.fill(pk.line(cx - 8, ey - 2, cx - 3, ey), AZ_LINE)
        c.fill(pk.line(cx + 2, ey, cx + 7, ey - 2), AZ_LINE)
        eye = [".RRRR.", "RHHRRR", "RHHRRR", "rRRRRr", ".rrrr."]
        pk.stamp(c, cx - 8, ey, eye, key)
        pk.stamp(c, cx + 2, ey, eye, key, flip=True)
        pk.stamp(c, cx - 4, ey + 7, ["oooooooo", ".F....F."], {"o": O, "F": FANG})
        rim = pk.edge_band(m, 0, 1) & pk.edge_band(m, 1, 1)
        c.fill({p for p in rim if p[1] > y1 - 5}, AZ_RIM)
    pk.outline(c)
    return c


# ───────────────────────────── Incubus (24×24) ─────────────────────────────

def incubus(frame):
    """Familiarul lui Lilith: demon mic, negru-mov, cu coarne, aripioare și ochi roșii. Privește spre dreapta.
    frame 0/1 = plutire (aripi sus/jos), 2 = atac (gura deschisă, scuipă o lacrimă de sânge)."""
    c = pk.Canvas(24, 24)
    bob = (0, 1, 0)[frame]
    w = pk.Canvas(24, 24)                                  # aripa din spate
    wy = 11 + bob
    up = [(9, wy), (6, wy - 5), (2, wy - 8), (1, wy - 4), (-1, wy - 2), (3, wy - 1), (2, wy + 2), (6, wy + 1)]
    down = [(9, wy), (5, wy - 1), (0, wy + 1), (2, wy + 3), (0, wy + 6), (4, wy + 5), (5, wy + 7), (8, wy + 3)]
    wpts = down if frame == 1 else up
    wm = pk.polygon(wpts)
    pk.shade_flat(w, wm, INC_R, shadow=(1, 1), deep=None)
    w.fill(pk.line(9, wy, *wpts[2]), INC_R[3])
    pk.outline(w)
    c.paste(w)
    b = pk.Canvas(24, 24)                                  # corpul mic + codița cu vârf
    bm = pk.ellipse(9, 14 + bob, 15, 20 + bob)
    pk.shade_flat(b, bm, INC_R, shadow=(1, 1), deep=None)
    b.fill(pk.polyline([(9, 19 + bob), (6, 21 + bob), (4, 20 + bob)]), INC_R[2])
    b.fill([(3, 19 + bob), (3, 20 + bob), (4, 19 + bob), (2, 19 + bob)], INC_R[2])
    pk.outline(b)
    c.paste(b)
    hd = pk.Canvas(24, 24)                                 # capul
    hb = (6, 3 + bob, 19, 15 + bob)
    for bx, tip in ((10, (7, -1 + bob)), (15, (13, -1 + bob))):
        poly = pk.polygon([(bx - 2, 5 + bob), tip, (bx + 1.5, 5 + bob)])
        pk.shade_flat(hd, poly, AZ_HORN_R, shadow=(1, 0), deep=None)
    hm = pk.ellipse(*hb)
    pk.shade_sphere(hd, hm, INC_R, cuts=SPHERE_CUTS, light=SPHERE_LIGHT, box=hb)
    key = {"R": INC_EYE, "H": pk.tint(INC_EYE, 1.5), "r": BLOOD[1], "o": O, "F": FANG,
           "m": BLOOD[2], "t": BLOOD[0]}
    ey = 7 + bob
    hd.fill(pk.line(11, ey - 1, 14, ey), INC_R[3])
    if frame == 2:
        pk.stamp(hd, 12, ey, ["HHR", "HHR", "rRr"], key)
        pk.stamp(hd, 17, ey, ["HR", "HR", "rr"], key)
        pk.stamp(hd, 14, ey + 4, ["ooo", "omt", "ooo"], key)
    else:
        pk.stamp(hd, 12, ey, [".R.", "RHR", "rRr"], key)
        pk.stamp(hd, 17, ey, ["R.", "HR", "rr"], key)
        pk.stamp(hd, 13, ey + 5, ["oooo", ".F.F"], key)
    pk.outline(hd)
    c.paste(hd)
    if frame == 2:                                         # lacrima de sânge care pleacă
        t = pk.Canvas(24, 24)
        tm = pk.ellipse(19, 10, 23, 14)
        pk.shade_sphere(t, tm, [pk.tint(BLOOD[0], 1.4), BLOOD[0], BLOOD[1], BLOOD[2]],
                        cuts=(0.85, 0.2, -0.3), light=SPHERE_LIGHT)
        t.set(20, 11, "#ffffff")
        pk.outline(t)
        c.paste(t)
    return c


# ───────────────────────────── generarea tuturor fișierelor ─────────────────────────────

HEROES = ["isaac", "cain", "lilith", "azazel"]
ORDER = ["back_idle_0", "back_idle_1", "back_walk_0", "back_walk_1", "back_walk_2", "back_walk_3",
         "side_idle_0", "side_idle_1", "side_attack_0", "side_attack_1", "side_hurt_0", "side_dead_0",
         "portrait"]


def build_hero(hero):
    frames = {}
    for name, pose in back_poses().items():
        frames[name] = back_frame(hero, pose)
    for name, pose in side_poses(hero).items():
        frames[name] = side_frame(hero, pose)
    frames["side_dead_0"] = side_dead(hero)
    frames["portrait"] = portrait(hero)
    return {k: frames[k] for k in ORDER}


def build_all():
    heroes = {h: build_hero(h) for h in HEROES}
    fam = {"idle_0": incubus(0), "idle_1": incubus(1), "attack_0": incubus(2)}
    return heroes, fam


def write_all(heroes, fam):
    for h, frames in heroes.items():
        for name, cv in frames.items():
            pk.save_png(cv, os.path.join(OUT_HEROES, h, name + ".png"))
    for name, cv in fam.items():
        pk.save_png(cv, os.path.join(OUT_FAM, name + ".png"))


def review_sheet(heroes, fam):
    sections = []
    for h, frames in heroes.items():
        sections.append((h, [(n.replace("side_", "s_").replace("back_", "b_"), cv) for n, cv in frames.items()]))
    combo = pk.Canvas(72, 48)
    combo.paste(heroes["lilith"]["side_attack_1"], 0, 0)
    combo.paste(fam["attack_0"], 40, 6)
    sections.append(("incubus (24x24) + Lilith attack with familiar",
                     [("idle_0", fam["idle_0"]), ("idle_1", fam["idle_1"]), ("attack_0", fam["attack_0"]),
                      ("lilith+incubus", combo)]))
    pk.contact_sheet(sections, os.path.join(REVIEW, "heroes.png"), scale=6, cell_bg="#33282a",
                     title="Darkest Basement - heroes (x6)")


# ───────────────────────────── macheta în context ─────────────────────────────

def brick_wall(c, x0, y0, x1, y1, ramp, bw=10, bh=5):
    """Perete de cărămidă: rânduri decalate, rost închis, muchia de sus a cărămizii puțin luminată."""
    for y in range(y0, y1 + 1):
        row = (y - y0) // bh
        for x in range(x0, x1 + 1):
            off = (bw // 2) if row % 2 else 0
            if (y - y0) % bh == bh - 1 or (x + off) % bw == bw - 1:
                col = ramp[2]
            elif (y - y0) % bh == 0:
                col = ramp[0]
            else:
                col = ramp[1]
            c.set(x, y, col)


def brick_floor(c, y0, y1, x0=0, x1=None):
    """Podea din cărămidă cu perspectivă simplă: rândurile cresc spre cameră."""
    x1 = c.w - 1 if x1 is None else x1
    y, row, bh = y0, 0, 3
    while y <= y1:
        depth = (y - y0) / max(1, y1 - y0)
        bw = 8 + int(depth * 8)
        ramp = [BASEMENT[1], BASEMENT[2], BASEMENT[4]] if depth > 0.3 else [BASEMENT[2], BASEMENT[3], BASEMENT[4]]
        for yy in range(y, min(y + bh, y1 + 1)):
            for x in range(x0, x1 + 1):
                off = (bw // 2) if row % 2 else 0
                mortar = yy == y + bh - 1 or (x + off) % bw == bw - 1
                col = ramp[2] if mortar else (ramp[0] if yy == y else ramp[1])
                c.set(x, yy, col)
        y += bh
        row += 1
        bh = 3 + int(depth * 3)


def gaper():
    """Silueta unui Gaper (privește spre stânga), doar pentru scară și compoziție."""
    c = layer()
    sil = [pk.tint(BASEMENT[3], 1.12), BASEMENT[3], BASEMENT[4], BASEMENT[4]]
    body = layer()
    bm = pk.ellipse(19, 28, 31, 44, fat=0.2) | pk.rect(20, 42, 23, 46) | pk.rect(27, 42, 30, 46)
    pk.shade_flat(body, bm, sil, shadow=(2, 1), deep=None)
    arm = pk.polygon([(20, 31), (8, 31), (7, 35), (20, 35)])
    pk.shade_flat(body, arm, sil, shadow=(1, 1), deep=None)
    pk.outline(body)
    c.paste(body)
    head = layer()
    hm = pk.ellipse(11, 7, 33, 30)
    pk.shade_sphere(head, hm, sil, cuts=(0.97, 0.2, -0.2), light=SPHERE_LIGHT)
    pk.stamp(head, 13, 15, [".mm...mm.", "mmm..mmm.", ".m.....m."], {"m": BLOOD[2]})
    pk.stamp(head, 13, 21, [".oooo.", "ommmmo", "omttmo", ".oooo."], {"m": BLOOD[2], "t": BLOOD[1], "o": O})
    head.fill([(14, 25), (14, 26), (15, 27)], BLOOD[1])
    pk.outline(head)
    c.paste(head)
    return c


def context_mock(heroes, fam):
    """(a) formația 2×2 văzută din spate pe hol; (b) linia de luptă laterală, rangurile 4-3-2-1 vs Gaper."""
    from PIL import Image, ImageDraw
    scale = 4
    wall_r = [pk.tint(BASEMENT[4], 1.12), pk.tint(BASEMENT[4], 0.85), pk.tint(BASEMENT[4], 0.55)]

    # (a) din spate: rândul din față (rang 1-2) e mai departe de cameră, deci mai sus;
    #     rândul din spate (rang 3-4) e mai jos și decalat, ca să nu se acopere capetele.
    A = pk.Canvas(200, 96)
    brick_wall(A, 0, 0, 199, 41, wall_r)
    brick_floor(A, 42, 95)
    A.fill(pk.rect(0, 42, 199, 43), pk.tint(BASEMENT[4], 0.55))      # plinta: umbra de la baza peretelui
    front = [("azazel", 58, 20), ("cain", 96, 22)]
    back = [("isaac", 34, 44), ("lilith", 118, 46)]
    for h, x, y in front:
        A.paste(heroes[h]["back_walk_1"], x, y)
    for h, x, y in back:
        A.paste(heroes[h]["back_walk_0" if h == "isaac" else "back_walk_2"], x, y)
    A.paste(fam["idle_0"], 160, 38)

    # (b) lateral: Lilith (4), Isaac (3), Cain (2), Azazel (1) | Gaper
    B = pk.Canvas(300, 66)
    brick_wall(B, 0, 0, 299, 51, wall_r, bw=12, bh=6)
    brick_floor(B, 52, 65)
    B.fill(pk.rect(0, 52, 299, 52), pk.tint(BASEMENT[4], 0.55))
    xs = [4, 52, 98, 144]
    for h, x in zip(["lilith", "isaac", "cain", "azazel"], xs):
        B.paste(heroes[h]["side_idle_0"], x, 9)
    B.paste(fam["idle_0"], 38, 4)
    B.paste(gaper(), 232, 9)

    imgs = [A.to_image(scale), B.to_image(scale)]
    pad = 24
    wpx = max(i.width for i in imgs) + pad * 2
    hpx = sum(i.height for i in imgs) + pad * 4 + 64
    out = Image.new("RGB", (wpx, hpx), pk.rgba("#141011")[:3])
    d = ImageDraw.Draw(out)
    f = pk._font(22)
    y = pad
    labels = ["(a) explorare: formatia 2x2 din spate (rang 1-2 in fata, 3-4 in spate) - x4",
              "(b) lupta: rangurile 4-3-2-1 (Lilith+Incubus, Isaac, Cain, Azazel) vs Gaper - x4"]
    for lab, im in zip(labels, imgs):
        d.text((pad, y), lab, fill=pk.rgba("#e8dcc4"), font=f)
        y += 32
        out.paste(im, (pad, y), im)
        y += im.height + pad
    out.save(os.path.join(REVIEW, "heroes-in-context.png"), optimize=False)


def main():
    heroes, fam = build_all()
    write_all(heroes, fam)
    review_sheet(heroes, fam)
    context_mock(heroes, fam)
    n = sum(len(v) for v in heroes.values()) + len(fam)
    print("scrise %d sprite-uri + 2 planșe în %s" % (n, os.path.relpath(REVIEW, ROOT)))


if __name__ == "__main__":
    main()
