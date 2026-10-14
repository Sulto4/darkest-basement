"""Generatorul boss-ilor: Monstro, Gurdy și Mom (picior, mână, ochi). Toți privesc spre STÂNGA.

Rulare (din rădăcina repo-ului):  python3 tools/art/bosses.py
Scrie:
  Assets/_Game/Resources/Sprites/Bosses/monstro/{idle_0..1, attack_0..1, hurt_0, dead_0, portrait}.png   (96×96)
  Assets/_Game/Resources/Sprites/Bosses/gurdy/{idle_0..1, attack_0..1, hurt_0, dead_0, portrait}.png     (128×128)
  Assets/_Game/Resources/Sprites/Bosses/mom/{foot_idle_0, foot_stomp_0..1, hurt_0, dead_0} (128×160),
      {hand_0..1} (96×96), {eye_0..1} (64×64), portrait (24×24)
  docs/art-review/bosses.png  planșa ×6

Aceeași tehnică ca la eroi și inamici (enemies.py, de unde luăm uneltele): straturi cu contur
propriu, umbrire de sferă cu lumina din stânga-sus, fețe puse pixel cu pixel. La boși, corpurile
mari sunt făcute din mai mulți „bulgări” umbriți separat, ca să nu arate ca o minge netedă.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pixkit as pk                                   # noqa: E402
import enemies as en                                  # noqa: E402
from enemies import (L, fin, sphere, stack, drip, fx_drops, fx_shot, fx_specks, puddle, gib,  # noqa: E402
                     flash, key, disc, tube, mbox, ground_shadow, dust, shade_tube, Pose,
                     BLOOD_R, GLOSS, HOLE, WHITE, WING_R, FLY_R, mx, tn)
from palette import (OUTLINE, SKIN, TEAR, BLOOD, POOP, BASEMENT, CAVES, DEPTHS,  # noqa: E402
                     DEPTHS_FLESH, UI, STATUS, CAVES_SHROOM)

ROOT = en.ROOT
OUT = os.path.join(ROOT, "Assets", "_Game", "Resources", "Sprites", "Bosses")
REVIEW = en.REVIEW
O = OUTLINE


def big_stamp(c, x, y, rows, k, s=1):
    """Ca pk.stamp, dar fiecare caracter devine un pătrat de s×s (pentru detalii la boși)."""
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch in ". ":
                continue
            col = k[ch]
            for dy in range(s):
                for dx in range(s):
                    if col is None:
                        c.erase([(x + i * s + dx, y + j * s + dy)])
                    else:
                        c.set(x + i * s + dx, y + j * s + dy, col)


def lumps_layer(w, h, lumps, ramp, ground, cuts=(0.93, 0.25, -0.25)):
    """Corp din bulgări: fiecare (cx, cy, r) e umbrit ca o sferă, din spate (sus) spre față (jos).
    Întoarce (strat, masca întreagă)."""
    c = L(w, h)
    whole = set()
    for (x, y, r) in sorted(lumps, key=lambda l: l[1]):
        m = {p for p in disc(x, y, r) if p[1] <= ground}
        whole |= m
        sphere(c, m, ramp, box=(x - r, y - r, x + r, y + r), cuts=cuts)
    return c, whole


def teeth_row(c, x0, x1, y, down=True, w=3, h=4, col=None, gap=1):
    """Un rând de dinți pătrățoși (cu contur), atârnând în jos (down) sau ridicându-se în sus."""
    col = col or UI["parchment"][0]
    x = x0
    while x + w - 1 <= x1:
        for i in range(w):
            for j in range(h):
                yy = y + j if down else y - j
                tip = (j == h - 1)
                if tip and (i == 0 or i == w - 1):
                    continue
                c.set(x + i, yy, col if i < w - 1 else UI["parchment"][1])
        c.set(x, y if down else y, WHITE)
        x += w + gap


# ───────────────────────────── MONSTRO ─────────────────────────────
# Blobul uriaș, bej-rozaliu, cu ochi mici, sprâncene încruntate și o gură enormă cu dinți.
# Atacuri: Big Leap (sare, umbra rămâne jos) și Blood Spray (gura căscată, stropește sânge).

MON_R = [mx(SKIN[0], BLOOD[0], 0.1), mx(SKIN[1], BLOOD[0], 0.24), mx(SKIN[2], BLOOD[1], 0.32),
         mx(SKIN[3], BLOOD[2], 0.45)]
MON_LINE = mx(MON_R[3], O, 0.4)
MON_SPOT = mx(MON_R[2], POOP[2], 0.35)
MW = MH = 96
MG = 94                                                  # rândul solului (conturul pe 95)


def monstro_body(ph, t, sq=0, air=0):
    """Corpul + picioarele. air > 0 = în salt (corp întins pe verticală, picioare atârnate)."""
    far, near = L(MW, MH), L(MW, MH)
    g = MG - air
    if air:
        bb = (20, 2, 78, g - 10)
        fl, nl = pk.ellipse(56, g - 16, 68, g - 2), pk.ellipse(30, g - 15, 44, g)
    else:
        bb = (12 - sq, 20 + sq + (t % 2), 84 + sq, g - 3)
        fl = pk.ellipse(58, g - 8, 72, g)
        nl = pk.ellipse(32, g - 8, 48, g)
    pk.shade_flat(far, fl, [MON_R[2], MON_R[2], MON_R[3], MON_R[3]], shadow=(1, 1), deep=None)
    pk.shade_flat(near, nl, MON_R, shadow=(2, 1), deep=(1, 1), light_rim=(-1, -1))
    nx0, _, _, ny1 = pk.bbox(nl)                          # degetele de la picior
    near.fill([(nx0 + 2 + i * 4, ny1 - 1) for i in range(3)], MON_R[3])
    body = L(MW, MH)
    x0, y0, x1, y1 = bb
    m = pk.ellipse(*bb) | pk.ellipse(x0 - 2, y0 + (y1 - y0) // 3, x1 + 1, y1 + 2)
    m = {p for p in m if p[1] <= g - 2}
    sphere(body, m, MON_R, (x0 - 2, y0, x1 + 1, y1 + 2))
    # pete, negi și vene: pielea bolnavă
    for (sx, sy, sw, sh) in [(x1 - 22, y0 + 8, 9, 5), (x1 - 12, y0 + 26, 6, 8), (x0 + 30, y0 + 4, 5, 3),
                             (x1 - 30, y1 - 14, 8, 5)]:
        pk.dither_fill(body, pk.ellipse(sx, sy, sx + sw, sy + sh) & m, MON_SPOT, 0.5)
    for (wx, wy) in [(x1 - 18, y0 + 18), (x0 + 44, y0 + 12), (x1 - 8, y1 - 26), (x0 + 50, y1 - 12)]:
        if (wx, wy) in m:
            pk.stamp(body, wx, wy, [".d.", "hbd", ".d."], {"d": MON_R[3], "b": MON_R[2], "h": MON_R[0]})
    body.fill([p for p in pk.polyline([(x1 - 4, y0 + 30), (x1 - 9, y0 + 36), (x1 - 6, y0 + 42), (x1 - 10, y0 + 50)]) if p in m], mx(MON_R[3], BLOOD[1], 0.5))
    # burta: cute de grăsime jos
    for k_, yy in enumerate((y1 - 10, y1 - 5)):
        body.fill([p for p in pk.line(x0 + 30 + k_ * 2, yy, x1 - 10, yy - 2) if p in m], MON_R[2])
    return far, body, near, bb, m


def monstro_face(body, bb, m, ph, t, air=0):
    x0, y0, x1, y1 = bb
    k = {**key(MON_LINE), "E": en.EYE_DARK}
    ex, ey = x0 + 24, y0 + 20
    # sprâncenele groase, încruntate
    if ph not in ("hurt", "dead"):
        body.fill(pk.line(ex - 1, ey - 5, ex + 9, ey - 2), MON_LINE)
        body.fill(pk.line(ex - 1, ey - 4, ex + 9, ey - 1), MON_LINE)
        body.fill(pk.line(x0 + 7, ey - 1, x0 + 12, ey - 4), MON_LINE)
    if ph == "hurt":
        big_stamp(body, ex, ey, ["KK...", "..KKK", "KK..."], k, 2)
        big_stamp(body, x0 + 8, ey + 1, ["..K", "KK.", "..K"], k, 2)
    elif ph == "dead":
        big_stamp(body, ex, ey, en.EYE_X, k, 2)
        big_stamp(body, x0 + 8, ey + 2, en.EYE_X_SMALL, k, 2)
    else:
        wide = ph in ("leap", "spray")
        round_eye(body, ex + 6, ey + 6, 5.6 if wide else 5.0, wide)
        far = pk.ellipse(x0 + 9, ey + 2, x0 + 13, ey + 12)
        body.fill(far, O)
        body.fill(pk.ellipse(x0 + 10, ey + 3, x0 + 12, ey + 11), en.EYE_DARK if not wide else WHITE)
        if wide:
            body.fill(pk.ellipse(x0 + 10, ey + 5, x0 + 11, ey + 9), en.EYE_DARK)
        body.set(x0 + 10, ey + 4, WHITE)
    # gura
    mxp = x0 + 4
    if ph in ("spray", "hurt", "leap"):
        mb = (mxp - 2, y0 + 31, mxp + 40, y1 - 8) if ph != "leap" else (mxp + 6, y0 + 36, mxp + 26, y0 + 50)
        mm = pk.ellipse(*mb)
    elif ph == "dead":
        mb = (mxp + 2, y0 + 32, mxp + 34, y0 + 42)
        mm = pk.ellipse(*mb)
    else:                                                # repaus: rânjet cu dinții strânși, colțurile în jos
        mb = (mxp, y0 + 33 + (t % 2), mxp + 38, y0 + 47)
        mm = {p for p in pk.ellipse(mb[0], mb[1] - 6, mb[2], mb[3]) if p[1] >= mb[1]}
        mm |= pk.ellipse(mb[0] - 1, mb[3] - 6, mb[0] + 5, mb[3] + 3) | pk.ellipse(mb[2] - 6, mb[3] - 7, mb[2], mb[3] + 2)
    mm &= m | pk.ellipse(*mb)
    body.fill(mm, HOLE)
    gx0, gy0, gx1, gy1 = pk.bbox(mm)
    body.fill(pk.ellipse(gx0 + 6, gy0 + 4, gx1 - 3, gy1 - 1) & mm, BLOOD[2])             # gâtlejul
    if ph != "idle":
        tongue = pk.ellipse(gx0 + 4, gy1 - 8, gx1 - 8, gy1 + 2) & mm
        body.fill(tongue, BLOOD[1])
        body.fill({p for p in tongue if (p[0] - 1, p[1] - 1) not in tongue}, BLOOD[0])
    body.fill(pk.inner_edge(mm), O)
    if ph == "idle":                                     # dinții de sus și de jos se ating
        teeth_row(body, gx0 + 4, gx1 - 5, gy0 + 1, down=True, w=4, h=5)
        teeth_row(body, gx0 + 6, gx1 - 7, gy1 - 3, down=False, w=4, h=5, gap=2)
    elif ph != "dead":
        teeth_row(body, gx0 + 3, gx1 - 4, gy0 + 1, down=True, w=4, h=5 if ph == "spray" else 4)
        if ph in ("spray", "hurt"):
            teeth_row(body, gx0 + 8, gx1 - 9, gy1 - 1, down=False, w=4, h=3, gap=3)
    # buza de jos, groasă
    lip = {(x, y + 1) for (x, y) in mm if (x, y + 1) not in mm and (x, y + 1) in m}
    lip |= {(x, y + 1) for (x, y) in lip}
    body.fill(lip, MON_R[2])
    body.fill({(x, y) for (x, y) in lip if (x, y - 1) in mm}, MON_R[0])
    if ph in ("idle", "spray"):                          # bale de sânge din colțul gurii
        drip(body, gx0 + 4, gy1 + 2, 4 + t % 2, t)
        drip(body, gx0 + 16, gy1 + 3, 2 + (t + 1) % 2, t + 1)
    return mb


def round_eye(c, cx, cy, r, wide=False):
    """Ochi rotund: contur negru, interior albastru-negru (sau alb cu pupilă, când e speriat/furios)."""
    m = disc(cx, cy, r)
    c.fill(m, O)
    inner = disc(cx, cy, r - 1.2)
    if wide:
        c.fill(inner, WHITE)
        c.fill({p for p in inner if (p[0] + 1, p[1] + 1) not in inner}, mx(WHITE, SKIN[2], 0.6))
        c.fill(disc(cx - 1.5, cy + 0.5, r * 0.45), en.EYE_DARK)
        c.set(int(cx - 2), int(cy - 1), WHITE)
    else:
        c.fill(inner, en.EYE_DARK)
        c.fill(disc(cx - r * 0.35, cy - r * 0.35, r * 0.32), WHITE)
        c.set(int(cx + r * 0.4), int(cy + r * 0.4), TEAR[0])


def draw_monstro(pose):
    ph, t, sq = pose.ph, pose.t, pose.sq
    c = L(MW, MH)
    air = 18 if ph == "leap" else 0
    if air:                                              # umbra rămâne pe sol cât e în aer
        c.fill(pk.ellipse(18, MG - 4, 78, MG + 1), pk.with_alpha(O, 150))
    far, body, near, bb, m = monstro_body(ph, t, sq, air)
    monstro_face(body, bb, m, ph, t, air)
    fin(body)
    fin(far)
    fin(near)
    c.paste(stack(MW, MH, [far, body, near]))
    if ph == "leap":                                     # liniile de viteză sub el
        lines = L(MW, MH)
        for (x, y0_, n) in [(28, 80, 6), (48, 82, 7), (66, 79, 5)]:
            lines.fill([(x, y0_ + i) for i in range(n)], mx(MON_R[2], O, 0.3))
        c.paste(lines)
        dust(c, [(14, MG), (80, MG), (22, MG - 2)])
    if ph == "spray":                                    # Blood Spray: evantai de stropi și lacrimi de sânge
        c.paste(fx_shot(MW, MH, 2, 46, 8))
        c.paste(fx_shot(MW, MH, 6, 66, 7))
        c.paste(fx_shot(MW, MH, 0, 58, 6))
        c.paste(fx_drops(MW, MH, [(12, 40), (4, 36), (10, 74), (2, 80), (16, 52), (8, 30)]))
        fx_specks(c, [(14, 34), (18, 44), (6, 50), (12, 84), (3, 70), (20, 60)])
    return c


def dead_monstro(pose):
    c = L(MW, MH)
    pud = L(MW, MH)
    puddle(pud, 48, MG, 92, 12, lobes=[(14, 22, 8), (80, 20, 9), (48, 40, 10)])
    c.paste(fin(pud))
    body = L(MW, MH)
    bb = (14, 56, 82, MG - 1)                            # turtit într-o movilă
    m = pk.ellipse(*bb) | pk.ellipse(10, 70, 86, MG + 6)
    m = {p for p in m if p[1] <= MG - 1}
    sphere(body, m, MON_R, (10, 56, 86, MG + 6))
    for (sx, sy, sw, sh) in [(60, 60, 9, 5), (30, 62, 5, 3)]:
        pk.dither_fill(body, pk.ellipse(sx, sy, sx + sw, sy + sh) & m, MON_SPOT, 0.5)
    k = key(MON_LINE)
    big_stamp(body, 34, 64, en.EYE_X, k, 2)
    big_stamp(body, 20, 66, en.EYE_X_SMALL, k, 2)
    mm = pk.ellipse(18, 78, 52, 88) & m
    body.fill(mm, HOLE)
    body.fill(pk.inner_edge(mm), O)
    body.fill(pk.ellipse(26, 82, 40, 92) & m, BLOOD[1])                   # limba scoasă
    body.fill(pk.ellipse(27, 83, 33, 86) & m, BLOOD[0])
    teeth_row(body, 22, 46, 79, down=True, w=4, h=3, gap=2)
    c.paste(fin(body))
    for (x, y, w_, h_) in [(4, 82, 9, 6), (80, 80, 10, 7), (60, 52, 7, 5), (8, 64, 6, 4)]:
        c.paste(gib(MW, MH, x, y, w_, h_, MON_R, bone=UI["parchment"][0]))
    tooth = L(MW, MH)                                    # dinți căzuți
    for (x, y) in [(70, 88), (88, 74), (56, 90)]:
        tooth.fill(pk.rect(x, y, x + 2, y + 3), UI["parchment"][0])
    c.paste(fin(tooth))
    fx_specks(c, [(6, 54), (90, 60), (24, 50), (74, 46)])
    return c


def portrait_monstro():
    c = L(24, 24)
    b = L(24, 24)
    bb = (1, 3, 26, 30)
    m = {p for p in pk.ellipse(*bb) if p[0] < 24 and p[1] < 24}
    sphere(b, m, MON_R, bb)
    k = {**key(MON_LINE), "E": en.EYE_DARK}
    b.fill(pk.line(9, 6, 14, 8), MON_LINE)
    pk.stamp(b, 9, 8, [".KK.", "KWEK", "KEEK", ".KK."], k)
    pk.stamp(b, 3, 9, ["KK", "WK", "KK"], k)
    mm = {p for p in pk.ellipse(1, 10, 20, 23) if p[1] >= 14}
    b.fill(mm, HOLE)
    b.fill(pk.ellipse(5, 18, 15, 24) & mm, BLOOD[1])
    b.fill(pk.inner_edge(mm), O)
    for x in range(3, 18, 3):
        b.fill([(x, 15), (x + 1, 15), (x, 16)], UI["parchment"][0])
    drip(b, 4, 21, 2, 0)
    c.paste(fin(b))
    return c


# ───────────────────────────── GURDY ─────────────────────────────
# Muntele de carne: o movilă uriașă, roz-roșiatică, din bulgări cu vene și pustule gălbui;
# în mijloc, o față cu ochi mici-mici și o gură mare. Atacuri: Spawn, Bile Rain, Mouth Beam.

GUR_R = [mx(SKIN[0], BLOOD[0], 0.35), mx(SKIN[1], BLOOD[0], 0.6), mx(SKIN[2], BLOOD[1], 0.66),
         mx(DEPTHS_FLESH, O, 0.25)]
GUR_LINE = mx(GUR_R[3], O, 0.4)
PUS_R = [mx(CAVES_SHROOM, WHITE, 0.45), mx(CAVES_SHROOM, SKIN[1], 0.4), mx(CAVES_SHROOM, GUR_R[2], 0.5),
         mx(GUR_R[3], CAVES_SHROOM, 0.2)]
GW = GH = 128
GG = 126

GUR_LUMPS = [(64, 66, 34), (30, 92, 22), (100, 90, 24), (42, 40, 16), (84, 42, 17), (64, 98, 32),
             (14, 112, 13), (114, 112, 12), (22, 70, 13), (108, 66, 13)]


def gurdy_lumps(sq=0, puff=0, flat=1.0):
    out = []
    for (x, y, r) in GUR_LUMPS:
        k = 1 + puff * 0.04
        nx = 64 + (x - 64) * (1 + sq * 0.03) * k
        ny = GG - (GG - y) * (1 - sq * 0.04) * k * flat
        out.append((nx, ny, r * k * (1 + sq * 0.01)))
    return out


def draw_gurdy(pose):
    ph, t, sq = pose.ph, pose.t, pose.sq
    c = L(GW, GH)
    puff = 3 if ph == "charge" else (1 if t % 2 else 0)
    lumps = gurdy_lumps(sq + (0 if t % 2 == 0 or ph != "idle" else 0), puff)
    body, m = lumps_layer(GW, GH, lumps, GUR_R, GG, cuts=(0.985, 0.3, -0.15))
    x0, y0, x1, y1 = pk.bbox(m)
    # carne pătată: tonul de umbră amestecat (dither) peste bază, ca să nu pară gumă de mestecat
    base = {p for p in m if body.get(*p) == pk.norm(GUR_R[1])}
    pk.dither_fill(body, {p for p in base if (p[0] // 6 + p[1] // 5) % 3 == 0}, GUR_R[2], 0.25)
    # vene întunecate care urcă pe bulgări
    for pts in ([(20, 120), (24, 100), (20, 84), (26, 70)], [(104, 120), (98, 100), (104, 86), (100, 70)],
                [(70, 30), (74, 44), (70, 56)], [(90, 60), (96, 74), (92, 84)], [(36, 52), (32, 62)]):
        body.fill([p for p in pk.polyline(pts) if p in m], mx(GUR_R[3], BLOOD[1], 0.4))
    # pustule gălbui (unele sparte, cu puroi)
    for i, (px_, py_, r) in enumerate([(98, 50, 4), (112, 98, 3.5), (30, 108, 3), (58, 30, 3), (80, 112, 3),
                                       (24, 80, 2.5)]):
        pm = disc(px_, py_ - (puff if i < 2 else 0) * 0.5, r) & m
        sphere(body, pm, PUS_R, None)
        if i % 2:
            body.set(int(px_), int(py_), HOLE)
    # luciu ud pe bulgării de sus
    for (gx, gy) in [(44, 32), (80, 33)]:
        body.fill([(gx, gy), (gx + 1, gy), (gx, gy + 1)] , WHITE)
    face_gurdy(body, m, ph, t)
    fin(body)
    c.paste(body)
    if ph == "beam":                                     # Mouth Beam: raza de sânge spre stânga
        beam = L(GW, GH)
        my = 98
        for (dy0, dy1, col) in [(-9, 9, BLOOD[1]), (-7, 7, BLOOD[0]), (-3, 3, tn(BLOOD[0], 1.5)), (-1, 1, WHITE)]:
            beam.fill(pk.rect(0, my + dy0, 30, my + dy1), col)
        bulb = pk.ellipse(22, my - 13, 44, my + 13)
        beam.fill(bulb, BLOOD[0])
        beam.fill(pk.ellipse(26, my - 9, 42, my + 9), tn(BLOOD[0], 1.5))
        beam.fill(pk.ellipse(30, my - 4, 40, my + 4), WHITE)
        c.paste(fin(beam))
        c.paste(fx_drops(GW, GH, [(10, 80), (20, 116), (4, 112), (16, 72)]))
        rain = L(GW, GH)                                 # Bile Rain: picături care cad de sus
        for (x, y) in [(20, 8), (44, 14), (70, 4), (96, 12), (118, 6), (8, 30)]:
            rain.fill([(x, y), (x, y + 1), (x + 1, y + 1), (x, y + 2), (x + 1, y + 2)], BLOOD[0])
            rain.set(x, y + 1, tn(BLOOD[0], 1.4))
        c.paste(fin(rain))
    if ph == "charge":                                   # Spawn: o muscă iese din pustula spartă
        c.paste(en.mini_fly(GW, GH, 104, 36, 0))
        c.paste(en.mini_fly(GW, GH, 116, 24, 1))
    return c


def face_gurdy(body, m, ph, t):
    k = {**key(GUR_LINE), "E": en.EYE_DARK}
    ex, ey = 50, 66
    if ph == "hurt":
        big_stamp(body, ex, ey, ["KK..", "..KK", "KK.."], k, 2)
        big_stamp(body, 34, ey + 2, ["K.", ".K", "K."], k, 2)
    elif ph == "dead":
        big_stamp(body, ex, ey, en.EYE_X_SMALL, k, 2)
        big_stamp(body, 34, ey + 2, en.EYE_X_SMALL, k, 2)
    else:
        # ochii mici-mici (contrastul cu corpul uriaș e gluma lui Gurdy)
        body.fill(pk.line(ex - 2, ey - 3, ex + 6, ey - 1), GUR_LINE)
        big_stamp(body, ex, ey, [".KK.", "KWEK", "KEEK", ".KK."], k, 2 if ph != "charge" else 2)
        big_stamp(body, 35, ey + 2, ["KK", "WK", "KK"], k, 2)
    if ph == "charge":                                   # gura strânsă, obrajii umflați
        mm = pk.ellipse(30, 88, 66, 96)
        body.fill(mm, O)
        body.fill(pk.ellipse(32, 90, 64, 94), HOLE)
        teeth_row(body, 34, 62, 90, down=True, w=3, h=3, gap=1)
        return
    if ph in ("beam", "hurt"):
        mb = (24, 80, 70, 116)
    elif ph == "dead":
        mb = (30, 90, 66, 104)
    else:
        mb = (28, 84 + (t % 2), 68, 110)
    mm = pk.ellipse(*mb) & (m | pk.ellipse(*mb))
    body.fill(mm, HOLE)
    gx0, gy0, gx1, gy1 = pk.bbox(mm)
    body.fill(pk.ellipse(gx0 + 6, gy0 + 6, gx1 - 4, gy1 - 2) & mm, BLOOD[2])
    tongue = pk.ellipse(gx0 + 4, gy1 - 10, gx1 - 10, gy1 + 2) & mm
    body.fill(tongue, BLOOD[1])
    body.fill({p for p in tongue if (p[0] - 1, p[1] - 1) not in tongue}, BLOOD[0])
    body.fill(pk.inner_edge(mm), O)
    if ph != "dead":
        teeth_row(body, gx0 + 5, gx1 - 6, gy0 + 1, down=True, w=4, h=5)
        teeth_row(body, gx0 + 9, gx1 - 10, gy1 - 1, down=False, w=4, h=4, gap=3)
    lip = {(x, y + 1) for (x, y) in mm if (x, y + 1) not in mm and (x, y + 1) in m}
    lip |= {(x, y + 1) for (x, y) in lip}
    body.fill(lip, GUR_R[2])
    if ph in ("idle", "beam"):
        drip(body, gx0 + 6, gy1 + 3, 5 + t % 2, t)
        drip(body, gx0 + 20, gy1 + 3, 3 + (t + 1) % 2, t + 1)


def dead_gurdy(pose):
    c = L(GW, GH)
    pud = L(GW, GH)
    puddle(pud, 64, GG, 124, 14, lobes=[(16, 26, 10), (110, 26, 12)])
    c.paste(fin(pud))
    lumps = gurdy_lumps(0, 0, flat=0.45)
    lumps = [(x, y, r * 0.95) for (x, y, r) in lumps]
    body, m = lumps_layer(GW, GH, lumps, GUR_R, GG - 2)
    for (px_, py_, r) in [(98, 96, 3.5), (30, 110, 3)]:
        pm = disc(px_, py_, r) & m
        sphere(body, pm, PUS_R, None)
        body.set(int(px_), int(py_), HOLE)
    face_gurdy(body, m, "dead", 0)
    c.paste(fin(body))
    for (x, y, w_, h_) in [(4, 108, 10, 7), (112, 104, 10, 8), (90, 72, 8, 6), (20, 84, 7, 5)]:
        c.paste(gib(GW, GH, x, y, w_, h_, GUR_R, bone=UI["parchment"][0]))
    fx_specks(c, [(10, 90), (120, 84), (40, 70), (84, 64)])
    return c


def portrait_gurdy():
    c = L(24, 24)
    lumps = [(12, 15, 11), (3, 20, 5), (21, 20, 5), (8, 6, 5), (17, 6, 5)]
    body, m = lumps_layer(24, 24, lumps, GUR_R, 23)
    m = {p for p in m if 0 <= p[0] < 24 and 0 <= p[1] < 24}
    k = {**key(GUR_LINE), "E": en.EYE_DARK}
    pk.stamp(body, 12, 9, ["KK", "KK"], k)
    pk.stamp(body, 8, 10, ["K", "K"], k)
    body.set(12, 9, WHITE)
    mm = pk.ellipse(5, 13, 17, 21)
    body.fill(mm, HOLE)
    body.fill(pk.inner_edge(mm), O)
    body.fill(pk.ellipse(8, 17, 14, 21) & mm, BLOOD[1])
    for x in range(7, 16, 2):
        body.set(x, 14, UI["parchment"][0])
    body.set(19, 8, PUS_R[0])
    body.set(20, 9, PUS_R[1])
    c.paste(fin(body))
    return c


# ───────────────────────────── MOM ─────────────────────────────
# Mama: nu o vedem niciodată întreagă. Piciorul uriaș coboară de sus (tivul rochiei albastre,
# pantof roșu cu toc cui), mâna intră din lateral, ochiul se uită prin ușă.

MOM_SKIN = [SKIN[0], mx(mx(SKIN[1], SKIN[2], 0.3), BLOOD[0], 0.05), mx(SKIN[2], BLOOD[1], 0.12),
            mx(SKIN[3], BLOOD[2], 0.2)]
MOM_LINE = mx(MOM_SKIN[3], O, 0.45)
DRESS_R = [mx(TEAR[1], WHITE, 0.25), mx(TEAR[1], TEAR[2], 0.55), tn(TEAR[2], 0.72), tn(TEAR[2], 0.48)]
SHOE_R = [tn(BLOOD[0], 1.45), BLOOD[0], BLOOD[1], BLOOD[2]]
NAIL = BLOOD[0]
FW, FH = 128, 160
FG = 158                                                 # solul pentru picior


def dress_hem(c, y_hem, sway=0):
    """Tivul rochiei: un clopot albastru sus, cu volane (margine ondulată) și pliuri."""
    m = set()
    for x in range(14, 124):
        wave = 3 * math.sin((x + sway) / 7.0) + (2 if (x // 7) % 2 else 0)
        yb = y_hem + int(wave)
        for y in range(0, yb + 1):
            m.add((x, y))
    m |= pk.ellipse(10, -20, 127, y_hem - 2)
    m = {p for p in m if 0 <= p[0] < FW and p[1] >= 0}
    if not m:
        return
    sphere(c, m, DRESS_R, (8, -30, 127, y_hem + 4), cuts=(0.9, 0.3, -0.25))
    for fx_ in range(24, 120, 14):                       # pliurile
        c.fill([p for p in pk.line(fx_ + sway // 3, 0, fx_ + 3, y_hem) if p in m], DRESS_R[3])
        c.fill([p for p in pk.line(fx_ - 2 + sway // 3, 0, fx_ + 1, y_hem - 3) if p in m], DRESS_R[0] if fx_ < 60 else DRESS_R[1])
    # tivul cusut: o linie mai închisă paralelă cu marginea
    edge = {(x, y - 3) for (x, y) in m if (x, y + 1) not in m}
    c.fill(edge & m, DRESS_R[3])
    fin(c)


def shoe(c, g, sq=0, dx=0):
    """Pantoful roșu cu toc cui, vârful spre stânga. g = rândul tălpii (solul sub el)."""
    pts = [(12, g - 3), (18, g), (36, g), (48, g - 6), (58, g - 14), (66, g - 19), (76, g - 20),
           (76, g - 34), (68, g - 37), (52, g - 25), (36, g - 15), (20, g - 10)]
    if sq:
        pts = [(x - sq if x < 40 else x + sq // 2, y) for (x, y) in pts]
    if dx:
        pts = [(x + dx, y) for (x, y) in pts]
    body = pk.polygon(pts)
    heel = pk.polygon([(64 + dx, g - 20), (75 + dx, g - 20), (71 + dx, g + 1), (67 + dx, g + 1)])
    hl = L(FW, FH)
    pk.shade_flat(hl, heel, SHOE_R, shadow=(1, 0), deep=None, light_rim=(-1, 0))
    hl.fill([(67 + dx, g), (68 + dx, g), (69 + dx, g), (70 + dx, g)], O)      # vârful metalic al tocului
    c.paste(fin(hl))
    sl = L(FW, FH)
    sphere(sl, body, SHOE_R, pk.bbox(body), cuts=(0.86, 0.25, -0.3))
    # luciul de lac: două dâre albe pe vârf și pe călcâi
    sl.fill([(x + dx, y) for (x, y) in [(22, g - 9), (23, g - 9), (24, g - 10), (26, g - 11), (27, g - 11)]], WHITE)
    sl.fill([(70 + dx, g - 31), (70 + dx, g - 30), (71 + dx, g - 28)], WHITE)
    # talpa închisă, vizibilă la arcuire
    sl.fill([p for p in pk.polyline([(18 + dx, g), (36 + dx, g), (48 + dx, g - 6), (58 + dx, g - 14), (66 + dx, g - 19)]) if p in body], SHOE_R[3])
    c.paste(fin(sl))


def leg(c, ankle_y, hem_y, dx=0):
    """Piciorul: un cilindru care se subțiază de la tiv la gleznă, cu pulpa bombată în spate."""
    a = ankle_y
    span = a - hem_y
    pts = [(78 + dx, hem_y - 6, 18), (76 + dx, hem_y + span * 0.35, 17), (74 + dx, hem_y + span * 0.62, 14),
           (68 + dx, hem_y + span * 0.85, 10), (64 + dx, a, 8.5)]
    m = tube(pts)
    m |= disc(80 + dx, hem_y + span * 0.45, 15)           # pulpa
    m = {p for p in m if p[1] >= 0}
    lay = L(FW, FH)
    shade_tube(lay, m, MOM_SKIN, cuts=(0.985, 0.3, -0.25))
    # genunchiul (în față) și tendonul lui Ahile (în spate)
    ky = int(hem_y + span * 0.18)
    lay.fill([p for p in pk.line(62 + dx, ky, 66 + dx, ky + 3) if p in m], MOM_SKIN[2])
    lay.fill([p for p in pk.line(72 + dx, a - 14, 70 + dx, a - 2) if p in m], MOM_SKIN[2])
    # laba în pantof: dosul piciorului de la gleznă la decolteul pantofului
    foot = pk.polygon([(56 + dx, a - 4), (73 + dx, a - 2), (74 + dx, a + 12), (54 + dx, a + 24), (38 + dx, a + 32),
                       (36 + dx, a + 28)])
    shade_tube(lay, foot, MOM_SKIN, cuts=(0.985, 0.3, -0.25))
    lay.fill([p for p in pk.line(44 + dx, a + 26, 58 + dx, a + 16) if p in foot], MOM_SKIN[2])   # tendoanele labei
    c.paste(fin(lay))


def mom_foot(lift=0, ph="idle", t=0):
    c = L(FW, FH)
    g = FG - lift
    if lift:                                             # umbra pe sol: mai mare cu cât e mai aproape
        w = 90 - lift // 2
        c.fill(pk.ellipse(44 - w // 2, FG - 5, 44 + w // 2, FG + 1), pk.with_alpha(O, 110 + min(60, 2400 // (lift + 20))))
    hem_y = 26 + (2 if ph == "stomp" else 0)
    leg(c, g - 46, hem_y)
    shoe(c, g, sq=2 if ph == "stomp" else 0)
    hem = L(FW, FH)
    dress_hem(hem, hem_y, sway=t * 5 + (6 if ph == "stomp" else 0))
    c.paste(hem)
    if ph == "stomp":                                    # impactul: praf, crăpături, sânge stropit
        cr = L(FW, FH)
        for pts in ([(10, FG), (4, FG - 2), (0, FG - 1)], [(84, FG), (96, FG - 2), (104, FG), (112, FG - 1)]):
            cr.fill(pk.polyline(pts), O)
        c.paste(cr)
        for (x, y) in [(6, FG - 4), (90, FG - 6), (100, FG - 3), (112, FG - 8), (2, FG - 10)]:
            dust(c, [(x, y)])
        c.paste(fx_drops(FW, FH, [(98, FG - 18), (108, FG - 26), (4, FG - 20), (116, FG - 14)]))
        lines = L(FW, FH)
        for (x, y0_, n) in [(30, 70, 14), (88, 66, 12), (100, 80, 10)]:
            lines.fill([(x, y0_ + i) for i in range(n)], mx(MOM_SKIN[2], O, 0.45))
        c.paste(lines)
    if ph == "hurt":
        c.paste(fx_drops(FW, FH, [(46, 70), (40, 80), (98, 64), (36, 96)]))
    return c


def dead_mom_foot():
    """Piciorul s-a prăbușit: zace pe podea, pantoful a căzut, sângele curge de sub rochie."""
    c = L(FW, FH)
    pud = L(FW, FH)
    puddle(pud, 64, FG, 126, 12, lobes=[(20, 30, 9), (100, 30, 10)])
    c.paste(fin(pud))
    shoe(c, 128, dx=-8)                                  # pantoful a rămas în picioare, gol, în spate
    lay = L(FW, FH)
    m = tube([(126, 132, 15), (96, 136, 14), (66, 142, 10.5), (40, 146, 8)])
    m |= disc(100, 133, 14)
    m |= pk.ellipse(14, 140, 44, 154)                    # laba goală
    m = {p for p in m if p[1] <= FG - 1}
    sphere(lay, m, MOM_SKIN, (10, 116, 140, 160), cuts=(0.95, 0.3, -0.1))
    for x in (16, 19, 22, 25):                           # degetele cu ojă roșie
        lay.fill(pk.ellipse(x - 2, 141, x + 1, 145), MOM_SKIN[1])
        lay.fill([(x - 1, 142), (x, 142)], NAIL)
    c.paste(fin(lay))
    hem = L(FW, FH)                                      # rochia mototolită, intră din dreapta
    hm = pk.ellipse(96, 104, 150, 162) | pk.ellipse(108, 90, 150, 140)
    hm = {p for p in hm if p[0] < FW and p[1] <= FG}
    sphere(hem, hm, DRESS_R, (96, 90, 150, 162))
    for x in (106, 114, 122):
        hem.fill([p for p in pk.line(x, 96, x - 4, 156) if p in hm], DRESS_R[3])
    c.paste(fin(hem))
    drip(c, 104, FG - 30, 6, 0)
    fx_specks(c, [(60, 120), (80, 126), (50, 130), (8, 140)])
    return c


# --- mâna (96×96) ---

def mom_hand(grab=False, t=0):
    c = L(96, 96)
    oy = 8 if grab else 0
    arm = L(96, 96)
    am = tube([(100, 46 + oy, 13), (74, 48 + oy, 12), (54, 50 + oy, 10)])
    shade_tube_h(arm, am, MOM_SKIN)
    arm.fill([p for p in pk.line(60, 56 + oy, 84, 55 + oy) if p in am], MOM_SKIN[2])          # tendoanele
    c.paste(fin(arm))
    cuff = L(96, 96)                                     # manșeta rochiei albastre la marginea din dreapta
    cm = pk.ellipse(78, 28 + oy, 104, 70 + oy)
    cm = {p for p in cm if p[0] < 96}
    sphere(cuff, cm, DRESS_R, (78, 28 + oy, 104, 70 + oy))
    cuff.fill([p for p in pk.line(82, 32 + oy, 80, 66 + oy) if p in cm], DRESS_R[0])
    c.paste(fin(cuff))
    fingers = L(96, 96)
    palm = pk.ellipse(30, 36 + oy, 60, 64 + oy)
    if not grab:                                         # mâna deschisă, degetele răsfirate spre stânga
        fdefs = [[(34, 40, 4.2), (18, 32, 3.8), (6, 28, 3.4)], [(32, 46, 4.4), (14, 42, 4), (2, 42, 3.6)],
                 [(32, 53, 4.4), (14, 54, 4), (3, 57, 3.6)], [(34, 59, 4), (18, 64, 3.6), (8, 70, 3.2)]]
        thumb = [(46, 38, 5), (40, 26, 4.2), (34, 18, 3.6)]
    else:                                                # apucă: degetele îndoite în jos, ca un cârlig
        fdefs = [[(34, 40, 4.2), (20, 38, 3.8), (14, 48, 3.4), (18, 56, 3)],
                 [(32, 46, 4.4), (16, 46, 4), (10, 56, 3.6), (14, 64, 3.2)],
                 [(32, 53, 4.4), (18, 54, 4), (13, 64, 3.6), (18, 71, 3.2)],
                 [(34, 59, 4), (22, 62, 3.6), (19, 70, 3.2), (24, 75, 2.8)]]
        thumb = [(46, 40, 5), (36, 34, 4.2), (28, 38, 3.6)]
    allm = set(palm)
    for f in fdefs + [thumb]:
        allm |= tube([(x, y + oy, r) for (x, y, r) in f])
    shade_tube_h(fingers, allm, MOM_SKIN)
    sphere(fingers, palm, MOM_SKIN, None, cuts=(0.95, 0.3, -0.1))
    # încheieturile degetelor (riduri) și unghiile date cu ojă roșie
    for f in fdefs:
        jx, jy, _ = f[1]
        fingers.fill([(jx, jy + oy - 1), (jx, jy + oy), (jx + 1, jy + oy + 1)], MOM_SKIN[2])
        tx, ty, r = f[-1]
        nm = disc(tx + (0 if not grab else 0), ty + oy, r * 0.7)
        fingers.fill(nm & allm, NAIL)
        fingers.set(int(tx - 1), int(ty + oy - 1), tn(NAIL, 1.5))
    tx, ty, r = thumb[-1]
    fingers.fill(disc(tx, ty + oy, r * 0.7) & allm, NAIL)
    fingers.fill([p for p in pk.line(40, 44 + oy, 54, 46 + oy) if p in palm], MOM_SKIN[2])   # oasele mâinii
    fingers.fill([p for p in pk.line(40, 52 + oy, 54, 52 + oy) if p in palm], MOM_SKIN[2])
    c.paste(fin(fingers))
    if grab:
        lines = L(96, 96)
        for (x, y, n) in [(60, 22, 10), (64, 78, 12), (70, 30, 7)]:
            lines.fill([(x + i, y) for i in range(n)], mx(MOM_SKIN[2], O, 0.4))
        c.paste(lines)
    return c


def shade_tube_h(c, mask, ramp, cuts=(0.8, 0.3, -0.15)):
    """Umbrire de cilindru orizontal: pe fiecare coloană, sus e luminat, jos în umbră."""
    cols = {}
    for x, y in mask:
        cols.setdefault(x, []).append(y)
    ly, lz = en.LIGHT[1], en.LIGHT[2]
    n = math.hypot(ly, lz)
    ly, lz = ly / n, lz / n
    for x, ys in cols.items():
        a, b = min(ys), max(ys)
        cy_, hh = (a + b + 1) / 2, max(1.0, (b - a + 1) / 2)
        for y in ys:
            v = max(-1.0, min(1.0, (y + 0.5 - cy_) / hh))
            i = v * ly + math.sqrt(max(0.0, 1 - v * v)) * lz
            col = ramp[0] if i > cuts[0] else ramp[1] if i > cuts[1] else ramp[2] if i > cuts[2] else ramp[3]
            c.set(x, y, col)


# --- ochiul din ușă (64×64) ---

STONE_R = [BASEMENT[0], BASEMENT[1], BASEMENT[2], BASEMENT[3]]


def door_frame(c, w=64, h=64, inner=(14, 12, 49, 63), thick=9):
    """Arcada de piatră a ușii (ca ușile din Basement): bolțari cu rosturi, lumina din stânga-sus."""
    x0, y0, x1, y1 = inner
    outer = pk.rect(x0 - thick, y0 + 8, x1 + thick, h - 1) | pk.ellipse(x0 - thick, y0 - thick, x1 + thick, y0 + 26)
    hole = pk.rect(x0, y0 + 10, x1, h - 1) | pk.ellipse(x0, y0, x1, y0 + 22)
    frame = {p for p in outer - hole if 0 <= p[0] < w and 0 <= p[1] < h}
    pk.shade_flat(c, frame, STONE_R, shadow=(2, 2), deep=(1, 1), light_rim=(-1, -1))
    # rosturile: raze din centrul arcului (bolțari) și rânduri orizontale pe stâlpi
    cx, cy = (x0 + x1) / 2, y0 + 11
    for ang in range(200, 341, 28):
        a = math.radians(ang)
        pts = pk.line(int(cx + math.cos(a) * 17), int(cy + math.sin(a) * 12),
                      int(cx + math.cos(a) * 30), int(cy + math.sin(a) * 24))
        c.fill([p for p in pts if p in frame], BASEMENT[4])
    for yy in range(y0 + 22, h, 9):
        c.fill([(x, yy) for x in range(0, w) if (x, yy) in frame], BASEMENT[4])
        c.fill([(x, yy + 1) for x in range(0, w) if (x, yy + 1) in frame and (x, yy) in frame], BASEMENT[0])
    return hole


def mom_eye(mad=False, t=0):
    c = L(64, 64)
    hole = door_frame(c)
    dark = L(64, 64)
    dark.fill(hole, DEPTHS[3])
    pk.dither_fill(dark, {p for p in hole if p[1] > 50}, O, 0.5)
    c.paste(dark)
    face = L(64, 64)
    # pielea feței lipite de ușă, care se pierde în întuneric spre margini
    skin = pk.ellipse(10, 14, 53, 64) & hole
    sphere(face, skin, MOM_SKIN, (8, 14, 55, 64), cuts=(0.95, 0.35, -0.05))
    pk.dither_fill(face, {p for p in skin if p[1] > 52 or p[0] < 17 or p[0] > 46}, DEPTHS[3], 0.5)
    pk.dither_fill(face, {p for p in skin if p[1] > 57 or p[0] < 15 or p[0] > 48}, DEPTHS[3], 1.0)
    # ochiul: migdală mare, albul injectat, irisul albastru care privește spre eroi (stânga)
    eb = (16, 26, 47, 46)
    eye = pk.ellipse(*eb) & pk.ellipse(10, 20, 53, 60) & pk.ellipse(10, 12, 53, 46)
    lid_y = 32 if mad else 27
    eye = {p for p in eye if p[1] >= lid_y}
    sclera = [WHITE, mx(WHITE, SKIN[1], 0.4), mx(SKIN[2], BLOOD[0], 0.25), mx(SKIN[3], BLOOD[1], 0.4)]
    sphere(face, eye, sclera, eb, cuts=(0.7, 0.1, -0.35))
    for pts in ([(16, 37), (20, 36), (22, 38)], [(46, 34), (42, 36), (40, 35)], [(45, 41), (41, 40)],
                [(18, 42), (21, 41)] + ([(24, 43)] if mad else [])):
        face.fill([p for p in pk.polyline(pts) if p in eye], BLOOD[1] if not mad else BLOOD[0])
    ix = 26 if not mad else 23
    iris = disc(ix, 37, 7.2) & eye
    face.fill(iris, TEAR[2])
    face.fill({p for p in iris if (p[0] - 1, p[1] - 1) not in iris or (p[0] - 2, p[1] - 2) not in iris}, tn(TEAR[2], 0.7))
    face.fill(disc(ix - 1, 38, 4.6) & iris, TEAR[1])
    face.fill(disc(ix, 37, 3.2) & iris, O)
    face.fill([(ix - 3, 34), (ix - 2, 34), (ix - 3, 35)], WHITE)
    face.fill(pk.inner_edge(eye), O)
    # pleoapa de sus (grea, cu gene) și cuta de dedesubt
    top = {(x, y - 1) for (x, y) in eye if (x, y - 1) not in eye}
    face.fill(top, O)
    face.fill({(x, y - 1) for (x, y) in top}, MOM_SKIN[2])
    for x in range(17, 47, 3):                           # genele
        ys = [y for (xx, y) in top if xx == x]
        if ys:
            face.fill(pk.line(x, min(ys), x - 2, min(ys) - 3), O)
    face.fill([p for p in pk.polyline([(18, 49), (30, 51), (44, 48)]) if p in skin], MOM_SKIN[2])
    face.fill([p for p in pk.polyline([(18, 22), (30, 19), (45, 22)]) if p in skin], MOM_SKIN[2])   # sprânceana
    if mad:
        face.fill([p for p in pk.polyline([(20, 24), (32, 26), (44, 22)]) if p in skin], MOM_LINE)
    c.paste(face)
    if mad:                                              # Mom's Eye: privirea care slăbește (Weak)
        g = STATUS["weak"]
        for (x, y) in [(6, 30), (57, 26), (8, 44), (55, 46), (4, 18)]:
            c.fill([(x, y - 1), (x - 1, y), (x, y), (x + 1, y), (x, y + 1)], g)
            c.set(x, y, WHITE)
    return c


def portrait_mom():
    c = L(24, 24)
    hole = door_frame(c, 24, 24, inner=(5, 4, 18, 23), thick=4)
    c.fill(hole, DEPTHS[3])
    eye = pk.ellipse(5, 9, 18, 17)
    eye = {p for p in eye if p[1] >= 11}
    c.fill(pk.ellipse(4, 6, 19, 23) & hole, MOM_SKIN[2])
    c.fill(pk.ellipse(5, 7, 17, 20) & hole, MOM_SKIN[1])
    c.fill(eye, WHITE)
    c.fill({p for p in eye if p[0] > 14}, mx(WHITE, SKIN[2], 0.5))
    c.fill(disc(10, 13.5, 3.2) & eye, TEAR[2])
    c.fill(disc(10, 13.5, 1.6) & eye, O)
    c.set(9, 12, WHITE)
    c.fill([(x, 10) for x in range(5, 19) if (x, 11) in eye], O)
    c.fill(pk.inner_edge(eye), O)
    c.set(15, 14, BLOOD[1])
    return c


# ───────────────────────────── generarea ─────────────────────────────

def build_monstro():
    f = {
        "idle_0": draw_monstro(Pose(t=0)),
        "idle_1": draw_monstro(Pose(t=1, sq=2)),
        "attack_0": draw_monstro(Pose(t=0, ph="leap")),
        "attack_1": draw_monstro(Pose(t=1, ph="spray", sq=1)),
        "hurt_0": flash(draw_monstro(Pose(ph="hurt", sq=6))),
        "dead_0": dead_monstro(Pose(ph="dead")),
        "portrait": portrait_monstro(),
    }
    return f


def build_gurdy():
    return {
        "idle_0": draw_gurdy(Pose(t=0)),
        "idle_1": draw_gurdy(Pose(t=1, sq=1)),
        "attack_0": draw_gurdy(Pose(t=0, ph="charge")),
        "attack_1": draw_gurdy(Pose(t=1, ph="beam")),
        "hurt_0": flash(draw_gurdy(Pose(ph="hurt", sq=5))),
        "dead_0": dead_gurdy(Pose(ph="dead")),
        "portrait": portrait_gurdy(),
    }


def build_mom():
    return {
        "foot_idle_0": mom_foot(lift=26, ph="idle", t=0),
        "foot_stomp_0": mom_foot(lift=58, ph="raise", t=1),
        "foot_stomp_1": mom_foot(lift=0, ph="stomp", t=2),
        "hurt_0": flash(mom_foot(lift=34, ph="hurt", t=1)),
        "dead_0": dead_mom_foot(),
        "hand_0": mom_hand(False, 0),
        "hand_1": mom_hand(True, 1),
        "eye_0": mom_eye(False, 0),
        "eye_1": mom_eye(True, 1),
        "portrait": portrait_mom(),
    }


def build_all():
    return {"monstro": build_monstro(), "gurdy": build_gurdy(), "mom": build_mom()}


def write_all(bosses):
    for bid, frames in bosses.items():
        for name, cv in frames.items():
            pk.save_png(cv, os.path.join(OUT, bid, name + ".png"))


def review_sheet(bosses, path=None, scale=6):
    mom = bosses["mom"]
    sections = [
        ("monstro (96x96)", list(bosses["monstro"].items())),
        ("gurdy (128x128)", list(bosses["gurdy"].items())),
        ("mom - foot (128x160)", [(k, mom[k]) for k in ("foot_idle_0", "foot_stomp_0", "foot_stomp_1", "hurt_0",
                                                         "dead_0")]),
        ("mom - hand (96x96), eye (64x64), portrait (24x24)",
         [(k, mom[k]) for k in ("hand_0", "hand_1", "eye_0", "eye_1", "portrait")]),
    ]
    pk.contact_sheet(sections, path or os.path.join(REVIEW, "bosses.png"), scale=scale, cell_bg="#33282a",
                     title="Darkest Basement - bosses (x6), all facing left")


def main(argv):
    bosses = build_all()
    if len(argv) > 1 and argv[1] == "preview":
        review_sheet(bosses, os.environ.get("PREVIEW", "/tmp/bosses_preview.png"), int(os.environ.get("SCALE", "3")))
        return
    write_all(bosses)
    review_sheet(bosses)
    n = sum(len(v) for v in bosses.values())
    print("scrise %d sprite-uri în %s" % (n, os.path.relpath(OUT, ROOT)))


if __name__ == "__main__":
    main(sys.argv)
