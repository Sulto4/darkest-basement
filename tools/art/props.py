"""Recuzita de pe hol și din camere (sprite-uri, PPU 32, pivot jos-centru).

Rulează:  python3 tools/art/props.py
Scrie în Assets/_Game/Resources/Sprites/Props/ și planșa docs/art-review/props.png.

Fiecare sprite stă „pe podea”: ultimul rând de pixeli opaci e la baza pânzei și desenul e centrat,
deci pivotul jos-centru pune obiectul exact pe sol. Excepții: web_0 (colț de perete/tavan, pivot sus-stânga),
chain_0 (se repetă pe verticală), blood_decal_* (pete văzute de sus, pivot centru), dust_mote (particulă).
"""
import sys
import math
import os
import random

from PIL import Image

sys.dont_write_bytecode = True  # nu lăsăm __pycache__ în tools/art
from envkit import (OUTLINE, PROPS_DIR, REVIEW_DIR, BOTTOM_RIGHT, TOP_LEFT, TOP, Canvas, Sheet, dilate, edge,
                    mix, on_checker, pts_ellipse, pts_line, pts_poly, pts_rect, shade_bevel, shade_sphere,
                    tile_noise, upscale)
from environment import (BLOOD, BONE, BS, CV, FLESH, GOLD, METAL, POOP, SHROOM, WOOD, bone, chain, mushroom,
                         skull, splat)
import palette as P

FIRE = [P.BLOOD[1], P.BLOOD[0], P.STATUS["mark"], P.STATUS["stun"], P.SKIN[0]]   # de la margine spre miez
ROCK = [mix(CV[2], BS[2], 0.5), mix(CV[3], BS[3], 0.45), mix(CV[4], BS[4], 0.4), mix(CV[5], BS[5], 0.35),
        mix(CV[6], BS[6], 0.3)]
TEARS = P.TEAR  # highlight, bază, umbră


def finish(cv, outline=True):
    if outline:
        cv.outline(OUTLINE)
    return cv


# ---------------------------------------------------------------- torța (4 cadre)

def torch(frame):
    """Torță pe un suport de perete: mâner de lemn, cupă de fier, flacără care pâlpâie (4 cadre)."""
    cv = Canvas(16, 32)
    # mânerul (lemn), puțin conic
    for y in range(19, 31):
        cv.put(7, y, WOOD[3] if y < 27 else WOOD[2])
        cv.put(8, y, WOOD[2] if y < 27 else WOOD[1])
    cv.put(7, 31, WOOD[2])
    cv.put(8, 31, WOOD[1])
    # suportul de fier: cupă + brațe + inel
    cup = pts_poly([(3, 15), (12, 15), (11, 19), (4, 19)])
    shade_bevel(cv, cup, METAL[1:5])
    cv.fill([(3, 15), (12, 15)], METAL[4])
    for x in range(3, 13):
        cv.put(x, 15, METAL[4] if x < 9 else METAL[3])
    ring = pts_rect(6, 23, 9, 24)
    shade_bevel(cv, ring, METAL[1:5])
    cv.put(5, 23, METAL[2])
    cv.put(10, 23, METAL[2])
    # jarul din cupă
    cv.fill([(5, 14), (6, 14), (7, 14), (8, 14), (9, 14), (10, 14)], FIRE[1])
    cv.put(7, 14, FIRE[2])
    cv.put(9, 14, FIRE[2])
    # flacăra: straturi de picătură întoarsă, vârful se leagănă de la un cadru la altul
    sway = [0, 1, 0, -1][frame]
    tall = [12, 13, 11, 13][frame]
    width = [4.4, 4.0, 4.8, 4.1][frame]
    base_y = 14
    layers = []
    for li, (wk, hk) in enumerate([(1.0, 1.0), (0.72, 0.78), (0.48, 0.56), (0.24, 0.32)]):
        pts = set()
        h = tall * hk
        for y in range(int(base_y - h), base_y + 1):
            t = (base_y - y) / max(h, 1)          # 0 jos, 1 sus
            half = width * wk * math.sin(math.pi * min(1.0, 0.35 + t * 0.75)) * (1 - t * 0.55)
            cx = 7.5 + sway * t * t * 2.0
            for x in range(0, 16):
                if abs(x - cx) <= half:
                    pts.add((x, y))
        layers.append(pts)
    cols = [FIRE[1], FIRE[2], FIRE[3], FIRE[4]]
    for pts, c in zip(layers, cols):
        cv.fill(pts, c)
    # limbi de foc și scântei care se schimbă la fiecare cadru
    rng = random.Random(40 + frame)
    tip = min(layers[0], key=lambda p: p[1])
    cv.put(tip[0] + rng.choice([-1, 0, 1]), tip[1] - 1, FIRE[0])
    for _ in range(2):
        cv.put(rng.randint(4, 11), rng.randint(0, 3), FIRE[2] if rng.random() < 0.5 else FIRE[3])
    return finish(cv)


# ---------------------------------------------------------------- caca

def poop(state):
    """Grămada de caca din Isaac: 3 etaje + vârf răsucit. state 0 = întreagă, 1 = ciobită, 2 = aproape distrusă."""
    cv = Canvas(24, 24)
    tiers = [(11.5, 18.5, 10.5, 4.6), (11.5, 13.2, 7.6, 3.8), (12, 8.6, 5.0, 3.1)]
    if state == 1:
        tiers = tiers[:2]
    if state == 2:
        tiers = [(11.5, 19.5, 10.0, 3.6)]
    ramp = [POOP[0], POOP[1], POOP[2], POOP[3], mix(POOP[3], "#e8dcc4", 0.45)]
    for i, (cx, cy, rx, ry) in enumerate(tiers):
        pts = pts_ellipse(cx, cy, rx, ry)
        shade_sphere(cv, pts, ramp[0:4], cx, cy, rx, ry + 2, dither=0.3)
        # linia dintre etaje (umbra etajului de deasupra)
        if i > 0:
            for (x, y) in edge(pts, [(0, 1)]):
                cv.put(x, y, POOP[0])
        # luciu
        cv.put(int(cx - rx * 0.5), int(cy - ry * 0.45), ramp[4])
        cv.put(int(cx - rx * 0.5) + 1, int(cy - ry * 0.45), ramp[3])
    if state == 0:
        # vârful răsucit
        for p, c in [((13, 5), POOP[2]), ((14, 4), POOP[2]), ((14, 5), POOP[1]), ((12, 5), POOP[3]), ((15, 3), POOP[1])]:
            cv.put(*p, c)
    if state == 1:
        # ruptura de sus: interior mai închis, zimțat
        rng = random.Random(7)
        for x in range(6, 18):
            y = 10 + rng.choice([0, 0, 1])
            cv.put(x, y, POOP[0])
            cv.put(x, y + 1, POOP[1] if rng.random() < 0.6 else POOP[0])
        for p in [(8, 8), (15, 9), (11, 9)]:
            cv.put(*p, POOP[2])
            cv.put(p[0] + 1, p[1], POOP[1])
    if state == 2:
        # bucăți împrăștiate
        for (x, y, r) in [(4, 15, 1.5), (18, 14, 2), (12, 15, 1.4)]:
            pts = pts_ellipse(x, y, r, r * 0.8)
            shade_sphere(cv, pts, ramp[0:4], x, y, r, r)
        rng = random.Random(9)
        for x in range(3, 21):
            if rng.random() < 0.4:
                cv.put(x, 17, POOP[0])
    return finish(cv)


# ---------------------------------------------------------------- pietre

def rock_shape(rng, cx, cy, rx, ry, n=9, jitter=0.18):
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.2, 0.2)
        r = 1 + rng.uniform(-jitter, jitter)
        pts.append((cx + math.cos(a) * rx * r, cy + math.sin(a) * ry * r))
    return pts_poly(pts, 64, 64)


def rock(seed, w=24, h=24, rx=9.5, ry=7.5, ramp=ROCK, tinted=False):
    """Bolovan: siluetă neregulată, volum sferic pe 4 tonuri, o fațetă de sus luminată, crăpătură."""
    rng = random.Random(seed)
    cv = Canvas(w, h)
    cx, cy = w / 2 - 0.5, h - 1 - ry
    shape = set(rock_shape(rng, cx, cy, rx, ry))
    shape = set(p for p in shape if p[1] <= h - 2)
    shade_sphere(cv, shape, ramp[0:4], cx - 1, cy - 1, rx, ry + 2, dither=0.15)
    # fațeta de sus: o „pantă” plată, deschisă, cu muchia de jos tăiată drept (piatra pare cioplită)
    fy = int(cy - ry * 0.25) + rng.choice([0, 1])
    facet = [p for p in shape if p[1] < fy and p[0] < cx + rx * 0.45 - (fy - p[1]) * 0.3]
    cv.fill(facet, ramp[3])
    cv.fill([p for p in facet if (p[0], p[1] + 1) not in facet and (p[0], p[1] + 1) in shape], ramp[2])
    for (x, y) in edge(shape, TOP_LEFT):
        cv.put(x, y, ramp[4])
    # crăpătură scurtă și ciupituri
    ax, ay = int(cx + rng.uniform(1, 3)), int(cy - 1)
    for k in range(rng.randint(3, 5)):
        p = (ax + (k // 2), ay + k)
        if p in shape:
            cv.put(*p, ramp[0])
            if (p[0] + 1, p[1]) in shape:
                cv.put(p[0] + 1, p[1], ramp[3])
    for _ in range(3):
        p = rng.choice(sorted(shape))
        if p not in facet:
            cv.put(*p, ramp[1])
    # baza în umbră, așezată pe sol
    for (x, y) in edge(shape, [(0, 1)]):
        cv.put(x, y, ramp[0])
    for (x, y) in edge(shape, [(1, 0)]):
        if y > cy:
            cv.put(x, y, ramp[0])
    if tinted:
        # piatra „marcată” din Isaac: o dungă deschisă, albăstruie, în formă de X
        for k in range(-4, 5):
            for p in [(int(cx) + k, int(cy) + k // 2), (int(cx) + k, int(cy) - k // 2)]:
                if p in shape:
                    cv.put(*p, TEARS[1] if abs(k) > 1 else TEARS[0])
                    q = (p[0], p[1] + 1)
                    if q in shape:
                        cv.put(*q, TEARS[2])
    return finish(cv)


def bones(kind):
    cv = Canvas(24, 16)
    if kind == 0:
        bone(cv, 3, 13, 20, 9)
        bone(cv, 4, 9, 19, 14)
        skull(cv, 6, 3)
    else:
        bone(cv, 2, 12, 11, 14)
        bone(cv, 13, 10, 21, 13)
        # coaste: un arc de linii curbe
        rib = Canvas(24, 16)
        for k in range(4):
            x0 = 8 + k * 3
            rib.fill([(x0, 8), (x0 + 1, 7), (x0 + 2, 7), (x0 + 3, 8), (x0 + 3, 9)], BONE[2])
            rib.put(x0 + 3, 10, BONE[1])
        rib.fill(pts_line(7, 9, 20, 9), BONE[1])
        rib.outline(OUTLINE)
        cv.blit(rib, 0, 0)
        # dinte căzut
        cv.put(4, 14, BONE[3])
        cv.put(5, 14, BONE[2])
    return cv


# ---------------------------------------------------------------- pânza de păianjen

def web_corner():
    """Pânză în colțul stânga-sus (se oglindește în cod pentru dreapta). Fire de 1 px, neregulate, fără contur."""
    rng = random.Random(5)
    cv = Canvas(32, 32)
    col = BONE[1]
    col2 = mix(BONE[1], "#5e4232", 0.45)
    rays = [0, 14, 33, 51, 70, 90]
    lens = [30, 22, 28, 25, 29, 31]
    for a, R in zip(rays, lens):
        ang = math.radians(a)
        cv.line(0, 0, round(math.cos(ang) * R), round(math.sin(ang) * R), col)
    for k, c in [(0.22, col), (0.42, col), (0.63, col2), (0.84, col2)]:
        prev = None
        for a, R in zip(rays, lens):
            ang = math.radians(a)
            kk = k + rng.uniform(-0.05, 0.05)
            p = (round(math.cos(ang) * 28 * kk), round(math.sin(ang) * 28 * kk))
            if prev and rng.random() < 0.85:
                mx, my = (prev[0] + p[0]) / 2 + 1, (prev[1] + p[1]) / 2 + 1.5
                cv.fill(pts_line(prev[0], prev[1], round(mx), round(my)), c)
                cv.fill(pts_line(round(mx), round(my), p[0], p[1]), c)
            prev = p
    # un fir rupt care atârnă și un păianjen mic
    cv.fill(pts_line(19, 12, 20, 21), col2)
    cv.fill([(19, 22), (20, 22), (19, 23), (20, 23)], OUTLINE)
    cv.fill([(18, 22), (21, 22), (18, 24), (21, 24)], OUTLINE)
    return cv


def floor_splat(seed, kind):
    rng = random.Random(seed)
    cv = Canvas(32, 16)
    S = 0.55   # turtirea pe verticală (podeaua e văzută din unghi)
    core, drops = set(), set()
    if kind == 0:   # baltă mare
        core |= set(pts_ellipse(15.5, 8, 12, 12 * S))
        for _ in range(7):
            a = rng.uniform(0, 2 * math.pi)
            core |= set(pts_ellipse(15.5 + math.cos(a) * 9, 8 + math.sin(a) * 9 * S, 4, 4 * S))
    elif kind == 1:  # stropitură cu picături în jur
        core |= set(pts_ellipse(15.5, 8, 6, 6 * S))
        for _ in range(6):
            a = rng.uniform(0, 2 * math.pi)
            core |= set(pts_ellipse(15.5 + math.cos(a) * 5, 8 + math.sin(a) * 5 * S, 2.5, 2.5 * S))
        for _ in range(14):
            a = rng.uniform(0, 2 * math.pi)
            d = rng.uniform(8, 14)
            r = rng.choice([0.6, 0.9, 1.2])
            drops |= set(pts_ellipse(15.5 + math.cos(a) * d, 8 + math.sin(a) * d * S, r + 0.3, r * S + 0.3))
    elif kind == 2:  # dâră târâtă
        for k in range(22):
            x = 4 + k
            y = 8 + math.sin(k * 0.35) * 1.5
            r = 2.6 - k * 0.07
            core |= set(pts_ellipse(x, y, r, max(0.8, r * 0.75)))
        for _ in range(6):
            drops |= set(pts_ellipse(rng.uniform(4, 28), rng.uniform(3, 13), 0.8, 0.6))
    elif kind == 3:  # câteva picături
        for (x, y, r) in [(9, 7, 2.2), (17, 10, 1.6), (23, 6, 2.6), (13, 12, 1.0), (27, 11, 1.0)]:
            core |= set(pts_ellipse(x, y, r, r * S + 0.3))
    else:            # pată medie cu o urmă de pas
        core |= set(pts_ellipse(12, 8, 8, 8 * S))
        for _ in range(5):
            a = rng.uniform(0, 2 * math.pi)
            core |= set(pts_ellipse(12 + math.cos(a) * 6, 8 + math.sin(a) * 6 * S, 3, 3 * S))
        foot = set(pts_ellipse(24, 7, 2.2, 1.5)) | set(pts_ellipse(26.5, 7.5, 1.4, 1.2))
        for p in foot:
            drops.add(p)
    allp = set(p for p in core | drops if 0 <= p[0] < 32 and 0 <= p[1] < 16)
    cv.fill(allp, BLOOD[2])
    cv.fill([p for p in edge(allp, [(0, 1), (1, 0)])], BLOOD[1])
    cv.fill([p for p in edge(allp, [(0, -1)]) if rng.random() < 0.6], BLOOD[3])
    # centrul bălții e mai închis (sângele e mai adânc acolo)
    if kind in (0, 4):
        cx = 15.5 if kind == 0 else 12
        cv.fill([p for p in allp if ((p[0] - cx) / 6) ** 2 + ((p[1] - 8) / 2.5) ** 2 < 1], BLOOD[1])
    hl = min(allp, key=lambda p: p[0] + p[1] * 2)
    cv.put(hl[0] + 2, hl[1] + 1, BLOOD[4])
    return cv


# ---------------------------------------------------------------- cufăr

def chest(opened):
    cv = Canvas(32, 24)
    wood = [POOP[0], POOP[1], POOP[2], POOP[3]]
    box = pts_rect(4, 12, 27, 23)
    shade_bevel(cv, box, wood)
    for y in range(13, 23):
        for x in range(5, 27):
            if (x - 5) % 6 == 5:
                cv.put(x, y, POOP[0])
    # benzi de metal auriu și colțuri
    for x in (8, 23):
        for y in range(12, 24):
            cv.put(x, y, GOLD[1])
            cv.put(x + 1, y, GOLD[2] if y > 12 else GOLD[3])
    for (x, y) in [(4, 12), (27, 12), (4, 23), (27, 23)]:
        cv.put(x, y, GOLD[2])
    if not opened:
        lid = set(pts_rect(4, 6, 27, 11)) | set(pts_ellipse(15.5, 7, 11.5, 3.2))
        lid = set(p for p in lid if p[1] >= 3 and 4 <= p[0] <= 27)
        shade_sphere(cv, lid, wood, 13, 4, 14, 8, dither=0.2)
        for x in (8, 23):
            for y in range(3, 12):
                if (x, y) in lid:
                    cv.put(x, y, GOLD[2])
                    cv.put(x + 1, y, GOLD[1])
        cv.line(4, 11, 27, 11, POOP[0])
        # încuietoare
        lock = pts_rect(14, 9, 17, 14)
        shade_bevel(cv, lock, GOLD)
        cv.put(15, 12, OUTLINE)
        cv.put(16, 12, OUTLINE)
    else:
        # capacul dat pe spate: se vede partea interioară, închisă
        lid = pts_poly([(5, 2), (26, 2), (27, 8), (4, 8)], 64, 64)
        cv.fill(lid, POOP[1])
        cv.fill(edge(lid, TOP), POOP[2])
        for x in (8, 23):
            for y in range(2, 9):
                cv.put(x, y, GOLD[1])
        inside = pts_rect(6, 9, 25, 13)
        cv.fill(inside, OUTLINE)
        cv.fill(pts_rect(6, 9, 25, 9), POOP[0])
        # strălucire de comoară
        for p, c in [((12, 11), GOLD[2]), ((13, 11), GOLD[3]), ((18, 12), GOLD[2]), ((21, 11), GOLD[1])]:
            cv.put(*p, c)
        cv.line(4, 12, 27, 12, POOP[3])
    return finish(cv)


# ---------------------------------------------------------------- patul lui Isaac

def bed():
    """Patul din camera lui Isaac: tăblie de lemn, pernă albă, pătură albastră cu model, văzut din lateral-față."""
    cv = Canvas(48, 32)
    wood = [WOOD[1], WOOD[2], WOOD[3], WOOD[4]]
    # tăblia din stânga (înaltă) și cea din dreapta (joasă)
    head = pts_rect(2, 6, 7, 31)
    shade_bevel(cv, head, wood)
    cv.fill(pts_ellipse(4.5, 6, 3, 2.5), wood[2])
    cv.fill(edge(pts_ellipse(4.5, 6, 3, 2.5), TOP_LEFT), wood[3])
    foot = pts_rect(41, 14, 45, 31)
    shade_bevel(cv, foot, wood)
    cv.fill(pts_ellipse(43, 14, 2.5, 2), wood[2])
    # cadrul și picioarele
    frame = pts_rect(7, 23, 41, 26)
    shade_bevel(cv, frame, wood)
    cv.fill(pts_rect(8, 27, 9, 31), wood[0])
    cv.fill(pts_rect(38, 27, 39, 31), wood[0])
    # salteaua
    mat = pts_rect(7, 18, 41, 22)
    cv.fill(mat, "#e8dcc4")
    cv.fill(edge(mat, [(0, 1)]), "#c8b89a")
    # perna
    pil = pts_ellipse(12, 15, 4.6, 2.8)
    shade_sphere(cv, pil, ["#c8b89a", "#e8dcc4", "#fff0e8"], 12, 15, 5, 3.5)
    # pătura albastră, cu buline (Isaac are o pătură de copil)
    blank = pts_poly([(17, 15), (41, 15), (42, 24), (16, 24)], 64, 64)
    shade_sphere(cv, blank, [TEARS[2], TEARS[1], TEARS[0]], 26, 12, 20, 14, dither=0.3)
    for x in range(19, 41, 5):
        for y in range(17, 24, 4):
            cv.put(x + (y // 4) % 2 * 2, y, "#e8dcc4")
    # marginea păturii care atârnă peste cadru
    for x in range(16, 42):
        cv.put(x, 24, TEARS[2])
        if x % 3 == 0:
            cv.put(x, 25, TEARS[2])
    cv.fill(edge(blank, TOP), TEARS[0])
    # o pată de lacrimi pe pernă
    cv.put(10, 14, TEARS[1])
    cv.put(11, 15, TEARS[1])
    return finish(cv)


# ---------------------------------------------------------------- piedestal

def pedestal():
    """Piedestalul de item din Isaac: piatră cu blat lat; deasupra, o coloană de lumină caldă, în trepte."""
    cv = Canvas(32, 40)
    stone = ROCK[1:5]
    top = pts_rect(6, 24, 25, 27)
    col = pts_rect(9, 28, 22, 35)
    base = pts_rect(7, 36, 24, 39)
    for pts in (col, base, top):
        shade_bevel(cv, pts, stone)
    cv.fill(edge(top, TOP), stone[3])
    cv.line(9, 28, 22, 28, stone[0])          # umbra blatului pe coloană
    for x in range(11, 21, 3):
        cv.put(x, 31, stone[0])
        cv.put(x + 1, 32, stone[3])
    finish(cv)
    # lumina: o coloană care urcă din blat și se stinge în trepte (3 niveluri de alfa, fără gradient neted)
    glow = Canvas(32, 40)
    warm = mix(P.STATUS["stun"], "#fff0e8", 0.55)
    for y in range(1, 24):
        t = (24 - y) / 23.0                  # 0 la blat, 1 sus
        a_in = 200 if t < 0.35 else (130 if t < 0.65 else 60)
        half_out = 8 + (1 if t > 0.5 else 0)
        half_in = 4
        for x in range(32):
            d = abs(x - 15.5)
            if d <= half_in:
                glow.put(x, y, warm[:3] + (a_in,))
            elif d <= half_out:
                glow.put(x, y, warm[:3] + (max(40, a_in // 2),))
    # baza luminii pe blat, mai puternică
    for x in range(8, 24):
        glow.put(x, 23, warm[:3] + (230,))
    for (x, y) in [(5, 10), (26, 6), (9, 18), (22, 14), (14, 3), (18, 8)]:
        glow.put(x, y, "#fff0e8")
    out = Canvas(32, 40)
    out.blit(glow, 0, 0)
    out.blit(cv, 0, 0)
    return out


def trapdoor():
    """Trapa spre etajul următor: gaură neagră cu ramă de scânduri, văzută de sus în unghi."""
    cv = Canvas(32, 24)
    outer = set(pts_ellipse(15.5, 12, 15, 10.5))
    hole = set(pts_ellipse(15.5, 12.5, 11, 7))
    rim = outer - hole
    rng = random.Random(3)
    for (x, y) in rim:
        ang = math.atan2(y - 12, x - 15.5)
        k = int((ang + math.pi) / (2 * math.pi) * 10)
        c = WOOD[3] if k % 2 else WOOD[2]
        cv.put(x, y, c)
    for (x, y) in rim:
        nb = (x + 1, y)
        a1 = int((math.atan2(y - 12, x - 15.5) + math.pi) / (2 * math.pi) * 10)
        a2 = int((math.atan2(nb[1] - 12, nb[0] - 15.5) + math.pi) / (2 * math.pi) * 10)
        if a1 != a2:
            cv.put(x, y, WOOD[0])
    cv.fill(edge(outer, TOP_LEFT), WOOD[4])
    cv.fill(edge(outer, [(0, 1)]), WOOD[1])
    cv.fill(hole, OUTLINE)
    # peretele îndepărtat al gropii (sus) se vede luminat puțin
    for (x, y) in hole:
        if (x, y - 1) not in hole:
            cv.put(x, y, BS[2])
            if (x, y + 1) in hole:
                cv.put(x, y + 1, BS[1])
    for (x, y) in rim:
        if rng.random() < 0.06:
            cv.put(x, y, WOOD[1])
    # cuie
    for (x, y) in [(3, 10), (28, 10), (15, 2), (15, 21), (7, 18), (24, 18)]:
        cv.put(x, y, METAL[4])
    return finish(cv)


# ---------------------------------------------------------------- ciuperci (caves)

def mushroom_prop(kind):
    if kind == 0:
        cv = Canvas(16, 16)
        mushroom(cv, 8, 11, 6)
    else:
        cv = Canvas(24, 16)
        mushroom(cv, 9, 11, 5)
        mushroom(cv, 16, 12, 3.5)
        mushroom(cv, 4, 13, 2.5)
    return cv


# ---------------------------------------------------------------- lanț (depths)

def chain_prop():
    """Lanț vertical care se repetă perfect la 24 px (2 perechi de zale)."""
    cv = Canvas(10, 36)
    chain(cv, 5, -6, 48)
    return crop_canvas(cv, 0, 6, 10, 24)


def crop_canvas(cv, x, y, w, h):
    out = Canvas(w, h)
    out.a = cv.a[y:y + h, x:x + w].copy()
    return out


# ---------------------------------------------------------------- țepi

def spikes(up):
    cv = Canvas(32, 20)
    plate = pts_poly([(2, 15), (29, 15), (31, 19), (0, 19)], 64, 64)
    shade_bevel(cv, plate, [METAL[0], METAL[1], METAL[2], METAL[3]])
    holes = [(5, 17), (11, 17), (17, 17), (23, 17), (28, 17), (8, 16), (14, 16), (20, 16), (25, 16)]
    for (x, y) in holes:
        cv.put(x, y, OUTLINE)
        cv.put(x + 1, y, METAL[0])
    finish(cv)
    if up:
        sp = Canvas(32, 20)
        # întâi rândul din spate, apoi cel din față (se suprapun corect)
        order = sorted(range(len(holes)), key=lambda i: holes[i][1])
        for i in order:
            x, y = holes[i]
            h = 13 if y == 16 else 15
            tip_y = y - h + 2
            tri = pts_poly([(x - 2, y), (x + 2, y), (x, tip_y)], 64, 64)
            layer = Canvas(32, 20)
            layer.fill(tri, METAL[3])
            layer.fill([p for p in tri if p[0] < x], METAL[4])
            layer.fill([p for p in tri if p[0] > x], METAL[2])
            layer.put(x, tip_y, METAL[5])
            if i in (2, 6):
                # vârfuri însângerate
                layer.put(x, tip_y + 1, BLOOD[3])
                layer.put(x, tip_y + 2, BLOOD[2])
                layer.put(x + 1, tip_y + 3, BLOOD[2])
            layer.outline(OUTLINE)
            sp.blit(layer, 0, 0)
        cv.blit(sp, 0, 0)
    else:
        for (x, y) in holes:
            cv.put(x, y - 1, METAL[4])
            cv.put(x, y, METAL[2])
    return cv


def dust_mote():
    """Fir de praf (particulă): cruce de 3×3, miezul opac, brațele semitransparente."""
    cv = Canvas(3, 3)
    c = mix("#e8dcc4", "#c8b89a", 0.3)
    cv.put(1, 1, c[:3] + (230,))
    for p in [(0, 1), (2, 1), (1, 0), (1, 2)]:
        cv.put(*p, c[:3] + (110,))
    return cv


# ================================================================ generare

def generate():
    out = {}
    for f in range(4):
        out["torch_%d" % f] = torch(f)
    for s in range(3):
        out["poop_%d" % s] = poop(s)
    out["rock_0"] = rock(11, rx=9.5, ry=7.5)
    out["rock_1"] = rock(23, rx=10.5, ry=6.5)
    out["rock_2"] = rock(37, rx=7, ry=6.5)
    out["tinted_rock"] = rock(11, rx=9.5, ry=7.5, tinted=True,
                              ramp=[mix(r, P.TEAR[2], 0.18) for r in ROCK])
    out["bones_0"] = bones(0)
    out["bones_1"] = bones(1)
    out["web_0"] = web_corner()
    for k in range(5):
        out["blood_decal_%d" % k] = floor_splat(70 + k, k)
    out["chest_closed"] = chest(False)
    out["chest_open"] = chest(True)
    out["bed"] = bed()
    out["pedestal"] = pedestal()
    out["trapdoor"] = trapdoor()
    out["mushroom_0"] = mushroom_prop(0)
    out["mushroom_1"] = mushroom_prop(1)
    out["chain_0"] = chain_prop()
    out["spikes_0"] = spikes(True)
    out["spikes_1"] = spikes(False)
    out["dust_mote"] = dust_mote()
    return out


def save_all(props):
    for name, cv in props.items():
        cv.save(os.path.join(PROPS_DIR, name + ".png"))


def in_context(props):
    """Recuzita pusă pe podeaua/peretele din basement, ×3, ca să judecăm cât se citește în joc."""
    floor = Image.open(os.path.join(os.path.dirname(PROPS_DIR), "..", "Textures", "Env", "basement", "floor.png"))
    wall = Image.open(os.path.join(os.path.dirname(PROPS_DIR), "..", "Textures", "Env", "basement", "wall.png"))
    W, H = 256, 96
    bg = Image.new("RGBA", (W, H))
    for x in range(0, W, 64):
        bg.paste(wall, (x, 0))
        bg.paste(floor, (x, 48))
    # torțe pe perete, obiecte pe podea (pivot jos-centru)
    def put(name, cx, by):
        im = props[name].image()
        bg.alpha_composite(im, (cx - im.width // 2, by - im.height))
    put("torch_0", 14, 40)
    put("torch_2", 120, 40)
    put("blood_decal_0", 60, 82)
    put("blood_decal_1", 190, 90)
    put("poop_0", 40, 78)
    put("rock_0", 84, 74)
    put("tinted_rock", 108, 80)
    put("bones_0", 140, 80)
    put("chest_closed", 172, 74)
    put("spikes_0", 210, 86)
    put("mushroom_1", 236, 70)
    put("web_0", 16, 16)
    bg.alpha_composite(props["web_0"].image(), (0, 0))
    return bg


def review(props):
    sheet = Sheet("Darkest Basement - props (x4, pivot bottom-center)")
    k = 4
    row = lambda names: [(on_checker(props[n].image(), k), n) for n in names]
    sheet.row(row(["torch_0", "torch_1", "torch_2", "torch_3", "poop_0", "poop_1", "poop_2"]), heading="hallway")
    sheet.row(row(["rock_0", "rock_1", "rock_2", "tinted_rock", "bones_0", "bones_1", "web_0"]))
    sheet.row(row(["blood_decal_0", "blood_decal_1", "blood_decal_2", "blood_decal_3", "blood_decal_4"]),
              heading="floor decals (seen from above)")
    sheet.row(row(["chest_closed", "chest_open", "bed", "pedestal", "trapdoor"]), heading="rooms")
    sheet.row(row(["mushroom_0", "mushroom_1", "chain_0", "spikes_0", "spikes_1"]) +
              [(on_checker(props["dust_mote"].image(), 8), "dust_mote x8")], heading="floor-specific + fx")
    sheet.row([(upscale(in_context(props), 3), "in context on basement wall/floor (x3)")], heading="readability")
    im = sheet.render()
    im.save(os.path.join(REVIEW_DIR, "props.png"))
    return im


if __name__ == "__main__":
    props = generate()
    save_all(props)
    review(props)
    print("ok", len(props), "props")
