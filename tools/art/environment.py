"""Texturile de mediu pentru hol (3D): wall / floor / ceiling / beam + variante de perete + uși.

Rulează:  python3 tools/art/environment.py
Scrie în Assets/_Game/Resources/Textures/Env/<etaj>/ și planșa docs/art-review/environment.png.

De ce așa:
- Texturile se desenează pe o pânză care se înfășoară (wrap), deci se leagă fără cusături.
- Variantele de perete pornesc din peretele de bază și modifică doar interiorul (margine de 3 px),
  așa că poți pune wall, wall_2, wall, wall_1 unul după altul pe hol și marginile se potrivesc.
- Lumina vine din stânga-sus: muchia de sus/stânga a fiecărei pietre e mai deschisă, cea de jos/dreapta mai închisă.
"""
import sys
import math
import os
import random

import numpy as np
from PIL import Image

sys.dont_write_bytecode = True  # nu lăsăm __pycache__ în tools/art
from envkit import (ENV_DIR, OUTLINE, REVIEW_DIR, BOTTOM_RIGHT, TOP_LEFT, TOP, RIGHT, C, Canvas, Sheet, cells_to_pts,
                    dilate, edge, mix, on_checker, pts_ellipse, pts_line, pts_poly, pts_rect, seam_score,
                    shade_bevel, shade_sphere, tile_grid, tile_noise, tile_voronoi, upscale, wrapped_edge)

FLOORS = ["basement", "caves", "depths"]
T = 64          # pereți, podea, tavan: 64×64 (2×2 unități la PPU 32)
BEAM = 32       # grinda: 32×32
MARGIN = 3      # variantele nu ating marginile

# ---------------------------------------------------------------- rampe (de la închis la deschis)
BS = [OUTLINE, "#2a1b13", "#3a271c", "#4a3326", "#5e4232", "#6e5241", mix("#6e5241", "#a8743d", 0.35)]
WOOD = ["#2a1b13", "#4a2e17", "#6b4423", "#8a5a2b", "#a8743d"]
CV = [OUTLINE, mix("#2f3236", OUTLINE, 0.5), "#2f3236", "#43474d", "#5c6168", "#7a7f86", mix("#7a7f86", "#d6ecff", 0.3)]
WATER = [mix("#3e6f7a", OUTLINE, 0.55), mix("#3e6f7a", OUTLINE, 0.25), "#3e6f7a", mix("#3e6f7a", "#7fb2e5", 0.45), "#d6ecff"]
DP = [OUTLINE, "#1a1316", "#2a1f24", "#3d2b33", "#4a3a40", mix("#4a3a40", "#7a7f86", 0.35)]
FLESH = [mix("#5c0d0d", OUTLINE, 0.4), "#5c0d0d", "#7a2a33", mix("#7a2a33", "#d33c3c", 0.45), mix("#d33c3c", "#fff0e8", 0.35)]
BLOOD = [mix("#5c0d0d", OUTLINE, 0.3), "#5c0d0d", "#9e1b1b", "#d33c3c", mix("#d33c3c", "#fff0e8", 0.45)]
BONE = [mix("#c8b89a", "#4a3326", 0.45), "#c8b89a", "#e8dcc4", "#fff0e8"]
METAL = [mix("#2f3236", OUTLINE, 0.4), "#2f3236", "#43474d", "#5c6168", "#7a7f86", mix("#7a7f86", "#ffffff", 0.35)]
GOLD = [mix("#a8782a", "#2a1b13", 0.5), "#a8782a", "#e0b04a", mix("#e0b04a", "#fff0e8", 0.55)]
SHROOM = [mix("#c9a227", "#2a1b13", 0.6), mix("#c9a227", "#2a1b13", 0.3), "#c9a227", mix("#c9a227", "#fff0e8", 0.5)]
MOSS = [mix("#43502a", OUTLINE, 0.45), "#43502a", "#5b6b3a", mix("#5b6b3a", "#c9a227", 0.35)]
POOP = ["#4a2e17", "#6b4423", "#8a5a2b", "#a8743d"]


def step_map(ramp, delta):
    """Dicționar culoare -> culoarea cu `delta` trepte mai deschisă/închisă pe aceeași rampă."""
    out = {}
    for i, c in enumerate(ramp):
        j = max(0, min(len(ramp) - 1, i + delta))
        out[C(c)[:3]] = C(ramp[j])
    return out


def restep(cv, pts, ramp, delta):
    """Întunecă/luminează pixelii existenți cu câte o treaptă pe rampă (murdărie, pete)."""
    m = step_map(ramp, delta)
    for x, y in pts:
        c = cv.get(x, y)
        if c[:3] in m:
            cv.put(x, y, m[c[:3]])


def partition(rng, total, lo, hi):
    """Împarte `total` în bucăți între lo și hi (lățimi de cărămizi, înălțimi de rânduri)."""
    n = max(1, round(total / ((lo + hi) / 2)))
    parts = [total // n] * n
    for i in range(total - sum(parts)):
        parts[i] += 1
    for _ in range(n * 6):
        i, j = rng.randrange(n), rng.randrange(n)
        k = rng.randint(1, 3)
        if i != j and parts[i] - k >= lo and parts[j] + k <= hi:
            parts[i] -= k
            parts[j] += k
    rng.shuffle(parts)
    return parts


def brick_layout(rng, W, H, rh, bw, min_joint=4):
    """Rânduri de cărămizi care se înfășoară: fiecare rând e un ciclu de lățime W, iar rândurile însumează H."""
    rows = partition(rng, H, *rh)
    out, prev, y = [], [], 0
    for h in rows:
        widths = partition(rng, W, *bw)
        best = None
        for _ in range(40):
            off = rng.randrange(W)
            joints, x = [], off
            for w in widths:
                joints.append(x % W)
                x += w
            score = min([min(abs(j - p), W - abs(j - p)) for j in joints for p in prev] or [99])
            if best is None or score > best[0]:
                best = (score, off, joints)
        x = best[1]
        for w in widths:
            out.append((x, y, w, h))
            x += w
        prev, y = best[2], y + h
    return out


def dark_speck(cv, rng, pts, ramp, base_i, density=0.1, hi_density=0.03):
    """Textură în interiorul unei pietre: pixeli cu o treaptă mai închis/deschis."""
    for x, y in pts:
        r = rng.random()
        if r < density:
            cv.put(x, y, ramp[max(0, base_i - 1)])
        elif r < density + hi_density:
            cv.put(x, y, ramp[min(len(ramp) - 1, base_i + 1)])


def pit(cv, x, y, ramp, i):
    """Gropiță: pixel închis + muchie luminată dreapta-jos (lumina vine din stânga-sus)."""
    cv.put(x, y, ramp[max(0, i - 2)])
    cv.put(x + 1, y + 1, ramp[min(len(ramp) - 1, i + 1)])


def crack(cv, rng, x, y, length, ramp, i, dirs=((1, 0), (1, 1), (0, 1), (1, -1)), hl=True):
    """Crăpătură: drum aleator, linie închisă cu un pixel luminat dedesubt."""
    pts = []
    dx, dy = rng.choice(dirs)
    for _ in range(length):
        pts.append((x, y))
        if rng.random() < 0.35:
            dx, dy = rng.choice(dirs)
        x += dx
        y += dy
    for (px, py) in pts:
        cv.put(px, py, ramp[max(0, i - 2)])
    if hl:
        for (px, py) in pts[::2]:
            if (px + 1, py + 1) not in pts:
                cv.put(px + 1, py + 1, ramp[min(len(ramp) - 1, i + 1)])
    return pts


# ================================================================ BASEMENT

def basement_wall(seed=11):
    rng = random.Random(seed)
    cv = Canvas(T, T, wrap=True, bg=BS[0])
    # mortarul: aproape negru, cu nisip maro închis (dă contrastul „gros” din Isaac)
    for y in range(T):
        for x in range(T):
            if rng.random() < 0.3:
                cv.put(x, y, BS[1])
    for (x, y, w, h) in brick_layout(rng, T, T, (9, 12), (14, 26), min_joint=5):
        bi = rng.choice([4, 4, 4, 3, 5, 4, 3, 5])
        ramp = [BS[bi - 2], BS[bi - 1], BS[bi], BS[min(6, bi + 1)]]
        body = set(pts_rect(x, y, x + w - 2, y + h - 2))
        # colțuri rotunjite și muchii ciupite: cărămizi vechi, făcute de mână
        for cx, cy in [(x, y), (x + w - 2, y), (x, y + h - 2), (x + w - 2, y + h - 2)]:
            body.discard((cx, cy))
            if rng.random() < 0.3:
                body.discard((cx + (1 if cx == x else -1), cy))
        for (px, py) in list(edge(body, TOP_LEFT + BOTTOM_RIGHT)):
            if rng.random() < 0.08:
                body.discard((px, py))
        if rng.random() < 0.5:
            ex = rng.randint(x + 2, x + w - 5)
            ey = rng.choice([y, y + h - 2])
            sgn = 1 if ey == y else -1
            for p in [(ex, ey), (ex + 1, ey), (ex + 2, ey), (ex + 1, ey + sgn)]:
                body.discard(p)
        shade_bevel(cv, body, ramp)
        # teșitura de sus e dublă pe unele cărămizi (mai mult volum)
        if rng.random() < 0.5:
            for (px, py) in edge(body, TOP):
                if (px, py + 1) in body and rng.random() < 0.6:
                    cv.put(px, py + 1, ramp[3] if rng.random() < 0.4 else ramp[2])
        inner = [p for p in body if p not in edge(body, TOP_LEFT + BOTTOM_RIGHT)]
        dark_speck(cv, rng, inner, BS, bi, 0.13, 0.04)
        for _ in range(rng.randint(0, 2)):
            if inner:
                px, py = rng.choice(inner)
                pit(cv, px, py, BS, bi)
        if rng.random() < 0.22 and w > 16:
            crack(cv, rng, x + rng.randint(2, w - 9), y + rng.randint(2, h - 5), rng.randint(4, 7), BS, bi,
                  dirs=((1, 0), (1, 1), (1, -1)))
    # murdărie: pete mari mai închise, ca o pivniță umedă
    n = tile_noise(T, T, 4, seed + 1, octaves=3)
    grime = [(x, y) for y in range(T) for x in range(T) if n[y, x] > 0.68]
    restep(cv, grime, BS, -1)
    return cv


def splat(rng, cx, cy, r, n_blobs=7, sat=10, drips=5, drip_len=(4, 16), bounds=(MARGIN, T - MARGIN - 1)):
    """Formă de stropitură ca în Isaac: un miez din cercuri suprapuse, picături în jur, dâre care curg în jos.
    Întoarce (miez, picături, dâre) ca seturi de pixeli."""
    core = set(pts_ellipse(cx, cy, r, r * 0.85))
    for _ in range(n_blobs):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(r * 0.5, r * 1.1)
        rr = rng.uniform(r * 0.3, r * 0.6)
        core |= set(pts_ellipse(cx + math.cos(a) * d, cy + math.sin(a) * d * 0.85, rr, rr))
    drops = set()
    for _ in range(sat):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(r * 1.4, r * 2.3)
        rr = rng.choice([0.6, 0.8, 1.0, 1.3])
        drops |= set(pts_ellipse(cx + math.cos(a) * d, cy + math.sin(a) * d * 0.85, rr, rr))
    runs = set()
    cxs = sorted(set(x for x, y in core))
    for _ in range(drips):
        x = rng.choice(cxs[1:-1] or cxs)
        y0 = max(y for (xx, y) in core if xx == x)
        ln = rng.randint(*drip_len)
        wide = rng.random() < 0.5
        for y in range(y0, y0 + ln):
            runs.add((x, y))
            if wide and y < y0 + ln - 2:
                runs.add((x + 1, y))
        runs |= set(pts_ellipse(x + (0.5 if wide else 0), y0 + ln, 1.1, 1.2))
    lo, hi = bounds
    clip = lambda S: set(p for p in S if lo <= p[0] <= hi and lo <= p[1] <= hi)
    return clip(core), clip(drops - core), clip(runs - core)


def paint_splat(cv, rng, core, drops, runs, ramp):
    """Umbrire plată, ca sângele din Isaac: bază, margine închisă dreapta-jos, lumină sus-stânga, un luciu."""
    allp = core | drops | runs
    cv.fill(allp, ramp[2])
    cv.fill(edge(allp, BOTTOM_RIGHT), ramp[1])
    cv.fill([p for p in edge(core, TOP_LEFT) if rng.random() < 0.7], ramp[3])
    cv.fill([p for p in runs if (p[0] - 1, p[1]) not in runs], ramp[3])
    cx = sum(x for x, y in core) / max(1, len(core))
    cy = sum(y for x, y in core) / max(1, len(core))
    for p in core:
        if (p[0] - cx) ** 2 + (p[1] - cy) ** 2 < 6 and rng.random() < 0.2:
            cv.put(*p, ramp[1])
    hx, hy = min(core, key=lambda p: (p[0] - cx + 3) ** 2 + (p[1] - cy + 3) ** 2)
    cv.put(hx, hy, ramp[4])
    cv.put(hx + 1, hy, ramp[3])


def basement_wall_variant(base, kind, seed):
    rng = random.Random(seed)
    cv = base.copy()
    cv.wrap = False   # variantele nu au voie să treacă peste margine
    if kind == "cracks":
        # o crăpătură mare, ramificată, și o cărămidă spartă din care se vede întunericul
        hole = set(pts_ellipse(40, 38, 6.5, 5.2)) | set(pts_ellipse(44, 41, 4, 4)) | set(pts_ellipse(36, 41, 3, 3))
        cv.fill(hole, OUTLINE)
        cv.fill([p for p in hole if (p[0] - 1, p[1] - 1) in hole and (p[0] - 2, p[1] - 2) not in hole], BS[1])
        lip = [(px + 1, py + 1) for (px, py) in edge(hole, BOTTOM_RIGHT)] + [(px + 1, py) for (px, py) in edge(hole, RIGHT)]
        cv.fill([p for p in lip if p not in hole], BS[6])
        # adâncimea: în gaură se vede o cărămidă din spate, în umbră
        cv.fill([(x, y) for (x, y) in hole if y in (41, 42) and 38 <= x <= 45], BS[1])
        main = []
        x, y = 39, 33
        for k in range(30):
            main.append((x, y))
            y -= 1
            x += rng.choice([-1, 0, 0, 1]) if k % 2 else 0
        for (px, py) in main:
            cv.put(px, py, OUTLINE)
            if py > 18:
                cv.put(px + 1, py, BS[1])
            if (px + 1, py + 1) not in main:
                cv.put(px + 1, py + 1, BS[6])
        for _ in range(4):
            px, py = rng.choice(main[4:])
            crack(cv, rng, px, py, rng.randint(5, 10), BS, 2, dirs=((1, 0), (1, -1), (-1, -1), (-1, 0)))
        crack(cv, rng, 30, 44, 12, BS, 2, dirs=((-1, 1), (-1, 0), (-1, 1), (0, 1)))
        # moloz căzut din gaură
        for p in [(38, 47), (42, 48), (46, 46), (35, 49), (44, 50)]:
            cv.put(*p, BS[5])
            cv.put(p[0] + 1, p[1], BS[4])
            cv.put(p[0], p[1] + 1, BS[1])
            cv.put(p[0] + 1, p[1] + 1, OUTLINE)
    elif kind == "blood":
        core, drops, runs = splat(rng, 29, 24, 7.5, n_blobs=8, sat=12, drips=6, drip_len=(5, 22))
        paint_splat(cv, rng, core, drops, runs, BLOOD)
    elif kind == "poop":
        core, drops, runs = splat(rng, 31, 30, 9, n_blobs=8, sat=10, drips=5, drip_len=(3, 12))
        light_poop = [OUTLINE, POOP[0], POOP[2], POOP[3], mix(POOP[3], "#e8dcc4", 0.5)]
        # umbra petei pe perete, ca să se desprindă de cărămizi
        for (x, y) in core | drops | runs:
            q = (x + 1, y + 1)
            if q not in core and q not in drops and q not in runs and MARGIN <= q[0] <= T - MARGIN - 1 and MARGIN <= q[1] <= T - MARGIN - 1:
                cv.put(*q, OUTLINE)
        paint_splat(cv, rng, core, drops, runs, light_poop)
        # bucăți: grunji mai închiși în mijlocul petei
        for p in rng.sample(sorted(core), 12):
            cv.put(*p, POOP[1])
            cv.put(p[0] - 1, p[1] - 1, POOP[3])
        # o muscă moartă lipită de perete
        cv.fill([(45, 18), (46, 18)], OUTLINE)
        cv.put(45, 17, "#c8b89a")
        cv.put(47, 17, "#c8b89a")
    return cv


def basement_floor(seed=21):
    rng = random.Random(seed)
    cv = Canvas(T, T, wrap=True)
    n = tile_noise(T, T, 4, seed, octaves=3)
    n2 = tile_noise(T, T, 16, seed + 7, octaves=1)
    for y in range(T):
        for x in range(T):
            v = n[y, x] * 0.75 + n2[y, x] * 0.25
            i = 2 + min(3, int(v * 4.2))  # BS[2..5]
            cv.put(x, y, BS[i])
    # lespezi mari, foarte discrete (pivnița are pardoseală veche de piatră)
    idx, gap, _ = good_voronoi(T, T, 5, seed + 3, min_dist=22, jitter_scale=1.0, max_run=9)
    for y in range(T):
        for x in range(T):
            me = idx[y, x]
            if idx[y, (x + 1) % T] != me or idx[(y + 1) % T, x] != me:
                cv.put(x, y, BS[1] if rng.random() < 0.8 else BS[2])
            elif idx[(y - 1) % T, x] != me or idx[y, (x - 1) % T] != me:
                restep(cv, [(x, y)], BS, +1)
    # pietricele și firimituri
    for _ in range(9):
        px, py = rng.randrange(T), rng.randrange(T)
        r = rng.choice([1, 1, 1.5])
        pts = pts_ellipse(px, py, r + 0.4, r)
        cv.fill(pts, BS[4])
        cv.fill(edge(pts, TOP_LEFT), BS[5])
        cv.fill([(x + 1, y + 1) for x, y in edge(pts, BOTTOM_RIGHT)], BS[1])
    for _ in range(4):
        crack(cv, rng, rng.randrange(T), rng.randrange(T), rng.randint(4, 9), BS, 3)
    for y in range(T):
        for x in range(T):
            r = rng.random()
            if r < 0.05:
                restep(cv, [(x, y)], BS, -1)
            elif r < 0.07:
                restep(cv, [(x, y)], BS, +1)
    return cv


def basement_ceiling(seed=31):
    """Scânduri vechi de lemn (podeaua etajului de sus), în umbră."""
    rng = random.Random(seed)
    R = BS[0:5]   # tonuri închise: tavanul e departe de torțe
    cv = Canvas(T, T, wrap=True, bg=R[0])
    y = 0
    for h in partition(rng, T, 7, 9):
        widths = partition(rng, T, 20, 44)
        x = rng.randrange(T)
        for w in widths:
            body = pts_rect(x, y, x + w - 2, y + h - 2)
            ti = rng.choice([2, 3, 3])
            shade_bevel(cv, body, [R[ti - 2], R[ti - 1], R[ti], R[ti + 1]])
            # fibra lemnului: linii orizontale întrerupte
            for _ in range(rng.randint(2, 4)):
                gy = y + rng.randint(1, h - 3)
                gx = x + rng.randint(1, max(1, w - 8))
                for k in range(rng.randint(4, 14)):
                    if gx + k < x + w - 2:
                        cv.put(gx + k, gy, R[ti - 1])
            # cuie la capetele scândurii
            for nx in (x + 1, x + w - 3):
                ny = y + (h - 1) // 2 - 1
                cv.put(nx, ny, METAL[3])
                cv.put(nx + 1, ny + 1, METAL[1])
            x += w
        y += h
    return cv


def wood_beam(seed, ramp, moss=False, bands=False, wet=False):
    """Grindă de lemn 32×32: fibră orizontală care se înfășoară, noduri, crăpături."""
    rng = random.Random(seed)
    cv = Canvas(BEAM, BEAM, wrap=True, bg=ramp[2])
    n = tile_noise(BEAM, BEAM, 2, seed, octaves=2)
    for y in range(BEAM):
        for x in range(BEAM):
            # fibră: dungi orizontale perturbate de zgomot
            g = math.sin((y + n[y, x] * 6) * 1.25)
            if g > 0.82:
                cv.put(x, y, ramp[1])
            elif g < -0.9:
                cv.put(x, y, ramp[3])
    for _ in range(2):
        kx, ky = rng.randrange(BEAM), rng.randrange(BEAM)
        knot = pts_ellipse(kx, ky, 2.5, 1.5)
        cv.fill(knot, ramp[1])
        cv.fill(pts_ellipse(kx, ky, 1, 0.6), ramp[0])
        cv.fill([(x, y - 1) for x, y in edge(knot, TOP_LEFT)], ramp[3])
    for _ in range(3):
        sx, sy = rng.randrange(BEAM), rng.randrange(BEAM)
        for k in range(rng.randint(5, 12)):
            cv.put(sx + k, sy, ramp[0])
            cv.put(sx + k, sy + 1, ramp[3] if k % 2 else ramp[2])
    if bands:
        # bandă de fier cu nituri (depths)
        for y in range(BEAM):
            for x in range(12, 18):
                cv.put(x, y, METAL[2] if x not in (12, 17) else (METAL[3] if x == 12 else METAL[1]))
        for y in range(3, BEAM, 8):
            cv.put(14, y, METAL[4])
            cv.put(15, y, METAL[3])
            cv.put(15, y + 1, METAL[0])
    if moss:
        # două pete mici de mușchi, nu un model de camuflaj
        for (mx, my) in [(rng.randrange(BEAM), rng.randrange(BEAM)) for _ in range(2)]:
            pts = pts_ellipse(mx, my, rng.uniform(2.5, 4), rng.uniform(1.5, 2.2))
            for p in pts:
                if rng.random() < 0.85:
                    cv.put(*p, MOSS[1])
            for (x, y) in edge(pts, TOP_LEFT):
                cv.put(x, y, MOSS[2])
            for (x, y) in edge(pts, [(0, 1)]):
                cv.put(x, y + 1, MOSS[0])
    if wet:
        # dâre de apă de-a lungul fibrei
        for _ in range(3):
            sx, sy = rng.randrange(BEAM), rng.randrange(BEAM)
            for k in range(rng.randint(3, 6)):
                cv.put(sx + k, sy, ramp[1])
            cv.put(sx, sy, WATER[3])
    for y in range(BEAM):
        for x in range(BEAM):
            if rng.random() < 0.05:
                restep(cv, [(x, y)], ramp, -1)
    return cv


# ================================================================ CAVES

def straightest_run(idx):
    """Cea mai lungă graniță orizontală/verticală dreaptă dintre celule (se citește ca o cusătură)."""
    h, w = idx.shape
    best = 0
    for y in range(h):
        run = 0
        for x in range(2 * w):
            b = idx[y, x % w] != idx[(y + 1) % h, x % w]
            run = run + 1 if b else 0
            best = max(best, min(run, w))
    for x in range(w):
        run = 0
        for y in range(2 * h):
            b = idx[y % h, x] != idx[y % h, (x + 1) % w]
            run = run + 1 if b else 0
            best = max(best, min(run, h))
    return best


def good_voronoi(W, H, n, seed, min_dist, jitter_scale, max_run=7, tries=40):
    """Încearcă seed-uri succesive până când pietrele nu au muchii lungi perfect drepte."""
    best = None
    for k in range(tries):
        idx, gap, pts = tile_voronoi(W, H, n, seed + 97 * k, min_dist=min_dist, jitter_scale=jitter_scale)
        r = straightest_run(idx)
        if best is None or r < best[0]:
            best = (r, idx, gap, pts)
        if r <= max_run:
            break
    return best[1], best[2], best[3]


def stone_cells(cv, rng, idx, ramp, base_choices, hi_i, crevice, deep, wet=0.0, speck=0.1):
    """Pietre din celule Voronoi: crăpătură închisă între ele, muchie luminată stânga-sus."""
    W, H = cv.w, cv.h
    cells = cells_to_pts(idx)
    for ci, pts in cells.items():
        bi = rng.choice(base_choices)
        cv.fill(pts, ramp[bi])
        inner = set(pts)
        tl = wrapped_edge(pts, W, H, TOP_LEFT)
        br = wrapped_edge(pts, W, H, BOTTOM_RIGHT)
        cv.fill(br, crevice)
        brs = set(br)
        br2 = [((x - 1) % W, y) for (x, y) in br] + [(x, (y - 1) % H) for (x, y) in br]
        cv.fill([p for p in br2 if p in inner and p not in brs], ramp[bi - 1])
        cv.fill([p for p in tl if p not in brs], ramp[min(hi_i, bi + 1)])
        rest = [p for p in pts if p not in brs and p not in tl]
        dark_speck(cv, rng, rest, ramp, bi, speck, 0.03)
        if wet and rng.random() < wet:
            # piatră udă: câteva sclipiri reci pe muchia luminată (nu o linie întreagă)
            for p in tl:
                if rng.random() < 0.18:
                    cv.put(*p, ramp[hi_i])
            if tl:
                cv.put(*rng.choice(sorted(tl)), WATER[3])
    # crăpăturile cele mai adânci
    for y in range(H):
        for x in range(W):
            me = idx[y, x]
            if idx[(y + 1) % H, x] != me and idx[y, (x + 1) % W] != me:
                cv.put(x, y, deep)


def caves_wall(seed=41):
    rng = random.Random(seed)
    cv = Canvas(T, T, wrap=True)
    idx, gap, _ = good_voronoi(T, T, 15, seed, min_dist=11, jitter_scale=1.3)
    stone_cells(cv, rng, idx, CV, [4, 4, 3, 5, 4, 3], 6, CV[2], CV[1], wet=0.4)
    # dâre de umezeală care coboară pe pietre
    for _ in range(6):
        x, y = rng.randrange(T), rng.randrange(T)
        for k in range(rng.randint(5, 14)):
            c = cv.get(x, y + k)
            if c[:3] in (C(CV[4])[:3], C(CV[5])[:3]):
                cv.put(x, y + k, CV[3])
    n = tile_noise(T, T, 4, seed + 1, octaves=2)
    restep(cv, [(x, y) for y in range(T) for x in range(T) if n[y, x] > 0.72], CV, -1)
    return cv


def crevice_mask(cv, ramp_dark):
    """Pixelii de crăpătură (cele mai închise tonuri) dintr-o textură de piatră."""
    dark = set(C(c)[:3] for c in ramp_dark)
    return set((x, y) for y in range(cv.h) for x in range(cv.w) if cv.get(x, y)[:3] in dark)


def caves_wall_variant(base, kind, seed):
    rng = random.Random(seed)
    cv = base.copy()
    cv.wrap = False
    lo, hi = MARGIN, T - MARGIN - 1
    if kind == "moss":
        # câteva perne de mușchi, mai multe jos (acolo e umed), plus fire care atârnă
        cores = set()
        for i, (cx, cy, rx, ry) in enumerate([(22, 46, 11, 7), (40, 50, 9, 6), (47, 30, 6, 4.5), (14, 30, 4, 3.5), (30, 56, 7, 3)]):
            cores |= moss_patch(cv, rng, cx, cy, rx, ry, seed + i * 13, lo, hi)
        for _ in range(7):
            x, y = rng.choice(sorted(cores))
            ys = [yy for (xx, yy) in cores if xx == x]
            y = max(ys)
            for k in range(1, rng.randint(2, 4)):
                if y + k <= hi - 1:
                    cv.put(x, y + k, MOSS[1] if k == 1 else MOSS[0])
        # picături de apă care se preling dintr-o crăpătură
        for (x, y0, ln) in [(26, 10, 8), (29, 12, 4), (52, 12, 10)]:
            for y in range(y0, y0 + ln):
                cv.put(x, y, WATER[2])
            cv.put(x, y0 + ln - 1, WATER[4])
            cv.fill(pts_ellipse(x, y0 + ln + 1, 0.8, 1.2), WATER[2])
            cv.put(x, y0 + ln + 2, WATER[1])
    elif kind == "mushrooms":
        # ciorchine de ciuperci galbene care ies dintr-o crăpătură
        for (mx, my, r) in [(24, 40, 6), (33, 36, 4.5), (17, 45, 3.5), (40, 43, 3), (29, 47, 3)]:
            mushroom(cv, int(mx), int(my), r)
        for _ in range(10):
            cv.put(rng.randint(14, 44), rng.randint(46, 52), SHROOM[3] if rng.random() < 0.3 else SHROOM[1])
    elif kind == "bones":
        # craniu și oase prinse în piatră
        skull(cv, 22, 20)
        bone(cv, 36, 40, 52, 35)
        bone(cv, 12, 48, 25, 51)
        bone(cv, 40, 18, 44, 26)
        for _ in range(8):
            cv.put(rng.randint(10, 54), rng.randint(14, 56), BONE[0])
    return cv


def mushroom(cv, mx, my, r):
    """Ciupercă: pălărie umbrită sferic + picior palid, contur închis."""
    stem = pts_rect(mx - max(1, int(r / 3)), my - int(r * 0.3), mx + max(1, int(r / 3)) - 1, my + int(r * 0.9))
    cap = [(x, y) for (x, y) in pts_ellipse(mx - 0.5, my - r * 0.35, r, r * 0.7) if y <= my - r * 0.15]
    layer = Canvas(cv.w, cv.h)
    layer.fill(stem, BONE[1])
    layer.fill(edge(stem, [(-1, 0)]), BONE[2])
    shade_sphere(layer, cap, SHROOM, mx - 0.5, my - r * 0.35, r, r * 0.7, dither=0.2)
    for (x, y) in edge(cap, [(0, 1)]):
        layer.put(x, y, SHROOM[0])
    # pete pe pălărie
    for (dx, dy) in [(-0.4, -0.55), (0.25, -0.7), (0.5, -0.35)]:
        layer.put(int(mx + dx * r), int(my + dy * r), SHROOM[3])
    layer.outline(OUTLINE)
    cv.blit(layer, 0, 0)


def skull(cv, x, y):
    """Craniu mic (12×11) în culori de os, cu orbite negre."""
    sk = Canvas(cv.w, cv.h)
    head = set(pts_ellipse(x + 5.5, y + 4.5, 5.5, 4.8))
    jaw = set(pts_rect(x + 2, y + 7, x + 9, y + 10))
    shape = head | jaw
    shade_sphere(sk, shape, BONE[0:3], x + 5.5, y + 4.5, 6, 6, dither=0.0)
    for ex in (x + 3, x + 7):
        sk.fill(pts_rect(ex, y + 4, ex + 1, y + 6), OUTLINE)
    sk.put(x + 5, y + 7, OUTLINE)
    sk.put(x + 6, y + 7, OUTLINE)
    for tx in range(x + 3, x + 9, 2):
        sk.put(tx, y + 9, BONE[0])
    sk.put(x + 3, y + 1, BONE[3])
    sk.put(x + 4, y + 1, BONE[3])
    sk.outline(OUTLINE)
    cv.blit(sk, 0, 0)


def bone(cv, x0, y0, x1, y1):
    """Os lung: corp de 2 px + capete umflate."""
    b = Canvas(cv.w, cv.h)
    body = dilate(pts_line(x0, y0, x1, y1), 1, diagonal=False)
    knobs = set(pts_ellipse(x0, y0, 2, 2)) | set(pts_ellipse(x1, y1, 2, 2))
    shape = body | knobs
    b.fill(shape, BONE[1])
    b.fill(edge(shape, TOP_LEFT), BONE[2])
    b.fill(edge(shape, BOTTOM_RIGHT), BONE[0])
    b.outline(OUTLINE)
    cv.blit(b, 0, 0)


def caves_floor(seed=51):
    rng = random.Random(seed)
    cv = Canvas(T, T, wrap=True)
    idx, gap, _ = good_voronoi(T, T, 9, seed, min_dist=16, jitter_scale=1.2)
    stone_cells(cv, rng, idx, CV, [3, 4, 4, 3], 5, CV[2], CV[1], speck=0.14)
    # apa stă în rosturile dintre pietre (bălți mari s-ar vedea ca un model care se repetă)
    n = tile_noise(T, T, 3, seed + 2, octaves=2)
    crev = crevice_mask(cv, CV[0:3])
    pud = set()
    for (x, y) in crev:
        if n[y, x] > 0.45:
            pud.add((x, y))
    # câteva ochiuri mici de apă la întâlnirea rosturilor
    for (x, y) in sorted(crev):
        if n[y, x] > 0.8 and rng.random() < 0.08:
            pud |= set(((x + dx) % T, (y + dy) % T) for (dx, dy) in [(0, 0), (1, 0), (0, 1), (1, 1), (-1, 0)])
    for (x, y) in pud:
        cv.put(x, y, WATER[1])
    for (x, y) in pud:
        if ((x + 1) % T, y) not in pud and rng.random() < 0.5:
            cv.put(x, y, WATER[2])
    pl = sorted(pud)
    for _ in range(9):
        x, y = rng.choice(pl)
        cv.put(x, y, WATER[3] if rng.random() < 0.7 else WATER[4])
    for _ in range(14):
        p = (rng.randrange(T), rng.randrange(T))
        if p not in pud:
            cv.put(*p, CV[5])
            cv.put(p[0] + 1, p[1] + 1, CV[2])
    return cv


def caves_ceiling(seed=61):
    rng = random.Random(seed)
    cv = Canvas(T, T, wrap=True)
    idx, gap, _ = good_voronoi(T, T, 22, seed, min_dist=9, jitter_scale=1.0, max_run=6)
    stone_cells(cv, rng, idx, CV, [3, 3, 4, 3], 4, CV[1], CV[0], speck=0.16)
    for _ in range(9):
        x, y = rng.randrange(T), rng.randrange(T)
        cv.put(x, y, WATER[3])
        cv.put(x, y + 1, WATER[2])
    return cv


# ================================================================ DEPTHS

def depths_wall(seed=71):
    rng = random.Random(seed)
    cv = Canvas(T, T, wrap=True, bg=DP[1])
    for y in range(T):
        for x in range(T):
            if rng.random() < 0.2:
                cv.put(x, y, DP[0])
    for (x, y, w, h) in brick_layout(rng, T, T, (15, 17), (18, 34), min_joint=8):
        bi = rng.choice([3, 3, 2, 2, 3, 4])
        ramp = [DP[bi - 2], DP[bi - 1], DP[bi], DP[min(5, bi + 1)]]
        body = set(pts_rect(x, y, x + w - 2, y + h - 2))
        for cx, cy in [(x, y), (x + w - 2, y), (x, y + h - 2), (x + w - 2, y + h - 2)]:
            body.discard((cx, cy))
        # blocuri mari, ciupite pe muchii
        for _ in range(rng.randint(0, 3)):
            ex = rng.randint(x + 1, x + w - 3)
            ey = rng.choice([y, y + h - 2])
            body.discard((ex, ey))
            body.discard((ex + 1, ey))
        shade_bevel(cv, body, ramp)
        # a doua linie de lumină: blocurile mari au o teșitură mai lată
        e_all = set(edge(body, TOP_LEFT + BOTTOM_RIGHT))
        for (px, py) in edge(body, TOP_LEFT):
            q = (px + 1, py + 1)
            if q in body and q not in e_all and rng.random() < 0.5:
                cv.put(*q, ramp[3])
        inner = [p for p in body if p not in e_all]
        dark_speck(cv, rng, inner, DP, bi, 0.14, 0.03)
        for _ in range(rng.randint(0, 2)):
            px, py = rng.choice(inner)
            pit(cv, px, py, DP, bi)
        if rng.random() < 0.35:
            crack(cv, rng, x + rng.randint(3, w - 8), y + rng.randint(2, h - 6), rng.randint(4, 8), DP, bi,
                  dirs=((1, 1), (0, 1), (1, 0)))
    n = tile_noise(T, T, 4, seed + 1, octaves=3)
    restep(cv, [(x, y) for y in range(T) for x in range(T) if n[y, x] > 0.7], DP, -1)
    # carne închegată în rosturi: bucăți mici care ies din mortar
    mortar = sorted(crevice_mask(cv, DP[0:2]))
    for _ in range(7):
        x, y = rng.choice(mortar)
        for k in range(rng.randint(2, 6)):
            c = cv.get(x + k, y)
            if c[:3] in (C(DP[1])[:3], C(DP[0])[:3]):
                cv.put(x + k, y, FLESH[1] if k % 3 else FLESH[2])
                if rng.random() < 0.3:
                    cv.put(x + k, y + 1, FLESH[0])
    for _ in range(8):
        cv.put(rng.randrange(T), rng.randrange(T), BLOOD[1])
    return cv


def depths_wall_variant(base, kind, seed):
    rng = random.Random(seed)
    cv = base.copy()
    cv.wrap = False
    lo, hi = MARGIN, T - MARGIN - 1
    if kind == "chains":
        for (x, top, length) in [(17, 9, 34), (45, 9, 44)]:
            # placă de fier bătută în perete, de care atârnă lanțul
            plate = pts_rect(x - 3, top - 4, x + 3, top)
            shade_bevel(cv, plate, METAL[1:5])
            cv.frame(x - 4, top - 5, x + 4, top + 1, OUTLINE)
            cv.put(x - 2, top - 3, METAL[5])
            cv.put(x + 2, top - 1, METAL[0])
            chain(cv, x, top + 1, length)
        # cârlig la capătul primului lanț, cătușă la capătul celui de-al doilea
        hook = Canvas(T, T)
        hook.fill([(17, 45), (17, 46), (18, 47), (19, 48), (19, 49), (18, 50), (17, 50), (16, 49)], METAL[4])
        hook.put(16, 48, METAL[3])
        hook.outline(OUTLINE)
        cv.blit(hook, 0, 0)
        cuff = Canvas(T, T)
        ring = set(pts_ellipse(45, 56, 3.5, 2.6)) - set(pts_ellipse(45, 56, 1.8, 1.0))
        cuff.fill(ring, METAL[3])
        cuff.fill(edge(ring, BOTTOM_RIGHT), METAL[2])
        cuff.fill(edge(ring, TOP_LEFT), METAL[4])
        cuff.outline(OUTLINE)
        cv.blit(cuff, 0, 0)
        # rugină și sânge vechi sub lanțuri
        for _ in range(12):
            cv.put(rng.randint(12, 50), rng.randint(20, 58), BLOOD[0])
    elif kind == "flesh":
        # carne închegată care a crescut peste blocuri și se întinde pe rosturi
        n = tile_noise(T, T, 6, seed, octaves=3)
        blob = set()
        for y in range(lo + 3, hi - 3):
            for x in range(lo + 3, hi - 3):
                d = ((x - 32) / 19) ** 2 + ((y - 34) / 16) ** 2
                if d + (n[y, x] - 0.5) * 1.0 < 0.75:
                    blob.add((x, y))
        mortar = crevice_mask(cv, DP[0:2])
        tendr = set()
        for (x, y) in mortar:
            if lo + 1 <= x <= hi - 1 and lo + 1 <= y <= hi - 1:
                d = ((x - 32) / 27) ** 2 + ((y - 34) / 23) ** 2
                if d < 1.0 and n[y, x] > 0.35:
                    tendr.add((x, y))
        layer = Canvas(T, T)
        layer.fill(tendr, FLESH[1])
        layer.fill([p for p in tendr if rng.random() < 0.3], FLESH[2])
        lumps = [(25, 31, 8, 7), (39, 36, 9, 8), (31, 43, 8, 6), (42, 26, 5, 5), (20, 41, 5, 5), (33, 27, 5, 4)]
        for (lx, ly, rx, ry) in lumps:
            pts = [p for p in pts_ellipse(lx, ly, rx, ry) if p in blob]
            shade_sphere(layer, pts, FLESH[0:4], lx, ly, rx, ry, dither=0.4)
        rest = [p for p in blob if layer.get(*p)[3] == 0]
        layer.fill(rest, FLESH[1])
        # vene închise
        for _ in range(9):
            x, y = rng.choice(sorted(blob))
            for k in range(rng.randint(4, 9)):
                if (x, y) in blob:
                    layer.put(x, y, FLESH[0])
                x += rng.choice([-1, 0, 1])
                y += rng.choice([-1, 0, 1, 1])
        # luciu umed pe fiecare bulgăre
        for (lx, ly, rx, ry) in lumps:
            hx, hy = int(lx - rx * 0.45), int(ly - ry * 0.5)
            if (hx, hy) in blob:
                layer.put(hx, hy, FLESH[4])
                layer.put(hx + 1, hy, FLESH[3])
        # umbra pe care o aruncă pe perete (dreapta-jos)
        for (x, y) in blob:
            for q in ((x + 1, y + 1), (x + 1, y), (x, y + 1)):
                if q not in blob and q not in tendr and lo <= q[0] <= hi and lo <= q[1] <= hi:
                    cv.put(*q, OUTLINE)
        cv.blit(layer, 0, 0)
        # picături care atârnă din carne
        for _ in range(4):
            x = rng.randint(22, 44)
            ys = [y for (xx, y) in blob if xx == x]
            if ys:
                y0 = max(ys) + 1
                for y in range(y0, min(hi, y0 + rng.randint(2, 6))):
                    cv.put(x, y, FLESH[1])
                cv.put(x, min(hi, y0 + 1), FLESH[2])
    elif kind == "blood":
        # sânge care se prelinge dintr-o crăpătură lată
        cr = []
        x, y = 10, 16
        while x < 54:
            cr.append((x, y))
            x += 1
            if rng.random() < 0.35:
                y += rng.choice([-1, 1])
            y = max(12, min(20, y))
        for (x, y) in cr:
            cv.put(x, y, OUTLINE)
            cv.put(x, y - 1, DP[1])
            cv.put(x, y + 1, BLOOD[1])
            if rng.random() < 0.3:
                cv.put(x, y + 2, DP[4])
        for i, (x, y) in enumerate(cr):
            if i % 3:
                continue
            ln = rng.choice([3, 4, 6, 9, 13, 18, 26, 34])
            wide = ln > 10 and rng.random() < 0.6
            for k in range(2, ln):
                yy = y + k
                if yy >= hi - 1:
                    break
                cv.put(x, yy, BLOOD[2] if k > 2 else BLOOD[3])
                if wide and k < ln - 2:
                    cv.put(x + 1, yy, BLOOD[1])
            end = min(hi - 1, y + ln)
            cv.put(x, end, BLOOD[3])
            if wide:
                cv.put(x + 1, end, BLOOD[1])
        for (x, y) in cr[::4]:
            cv.put(x, y + 1, BLOOD[4])
    return cv


def chain(cv, x, y0, length):
    """Lanț vertical gros: zale ovale văzute din față alternate cu zale din profil (bară)."""
    layer = Canvas(cv.w, cv.h)
    y = y0
    k = 0
    while y < y0 + length:
        if k % 2 == 0:
            ring = set(pts_ellipse(x, y + 4, 3, 4.4)) - set(pts_rect(x - 1, y + 2, x, y + 6))
            layer.fill(ring, METAL[3])
            layer.fill(edge(ring, TOP_LEFT), METAL[4])
            layer.fill(edge(ring, BOTTOM_RIGHT), METAL[2])
            layer.put(x - 2, y + 2, METAL[5])
        else:
            layer.fill(pts_rect(x - 1, y + 1, x, y + 7), METAL[3])
            layer.fill(pts_rect(x - 1, y + 1, x - 1, y + 7), METAL[4])
            layer.put(x - 1, y + 2, METAL[5])
        y += 6
        k += 1
    layer.outline(OUTLINE)
    cv.blit(layer, 0, 0)


def depths_floor(seed=81):
    rng = random.Random(seed)
    cv = Canvas(T, T, wrap=True, bg=DP[0])
    for (x, y, w, h) in brick_layout(rng, T, T, (15, 17), (14, 22), min_joint=6):
        bi = rng.choice([2, 3, 3, 2])
        ramp = [DP[bi - 2], DP[bi - 1], DP[bi], DP[min(5, bi + 1)]]
        body = set(pts_rect(x, y, x + w - 2, y + h - 2))
        for cx, cy in [(x, y), (x + w - 2, y), (x, y + h - 2), (x + w - 2, y + h - 2)]:
            body.discard((cx, cy))
        shade_bevel(cv, body, ramp)
        inner = [p for p in body if p not in edge(body, TOP_LEFT + BOTTOM_RIGHT)]
        dark_speck(cv, rng, inner, DP, bi, 0.16, 0.03)
        if rng.random() < 0.45:
            crack(cv, rng, x + rng.randint(2, w - 6), y + rng.randint(2, h - 5), rng.randint(3, 7), DP, bi)
    # sânge închegat, uscat: pete mici și neregulate, mai multe în rosturi (acolo s-a scurs)
    n = tile_noise(T, T, 8, seed + 4, octaves=2)
    mortar = crevice_mask(cv, DP[0:2])
    for y in range(T):
        for x in range(T):
            v = n[y, x] + (0.12 if (x, y) in mortar else 0)
            if v > 0.82:
                cv.put(x, y, BLOOD[1] if v < 0.9 else FLESH[2])
            elif v > 0.77 and rng.random() < 0.5:
                cv.put(x, y, BLOOD[0])
    for _ in range(5):
        x, y = rng.randrange(T), rng.randrange(T)
        if n[y, x] > 0.85:
            cv.put(x, y, BLOOD[3])
    return cv


def depths_ceiling(seed=91):
    rng = random.Random(seed)
    cv = Canvas(T, T, wrap=True)
    idx, gap, _ = good_voronoi(T, T, 14, seed, min_dist=12, jitter_scale=1.0)
    stone_cells(cv, rng, idx, DP, [2, 3, 3], 4, DP[1], DP[0], speck=0.18)
    # vene de carne care se târăsc pe rosturi
    for y in range(T):
        for x in range(T):
            if gap[y, x] < 1.0 and rng.random() < 0.45:
                cv.put(x, y, FLESH[1] if rng.random() < 0.7 else FLESH[2])
    return cv


# ================================================================ UȘI (64×64, ca în Isaac)

DOOR_CX = 31.5


def arch_shape(x0, x1, top, bottom, r=None, cy=None):
    """Formă de arcadă: laturi drepte + semicerc sus. Coordonate inclusive."""
    cx = (x0 + x1) / 2
    r = r if r is not None else (x1 - x0 + 1) / 2
    cy = cy if cy is not None else top + r
    pts = set()
    for y in range(top, bottom + 1):
        for x in range(x0, x1 + 1):
            if y >= cy:
                pts.add((x, y))
            else:
                if ((x - cx) / r) ** 2 + ((y - cy) / r) ** 2 <= 1.0:
                    pts.add((x, y))
    return pts


def door_geometry(kind):
    if kind == "bedroom":
        # ușa de dormitor are colțurile de sus mai pătrate (silueta diferită ajută la citire)
        outer = set(p for p in pts_rect(6, 8, 57, 63)) | arch_shape(6, 57, 4, 30)
        inner = set(p for p in pts_rect(13, 16, 50, 63))
    else:
        outer = arch_shape(4, 59, 2, 63)
        inner = arch_shape(13, 50, 12, 63)
    return outer, inner


def frame_blocks(outer, inner, n_arch=9, side_rows=(34, 46, 56)):
    """Împarte rama în blocuri de piatră (bolțari) radiale pe arc + blocuri pe laturi."""
    ring = outer - inner
    cy = 2 + 28
    blocks = {}
    for (x, y) in ring:
        if y < cy + 2:
            ang = math.atan2(y - cy, x - DOOR_CX)  # -pi..0
            a = (ang + math.pi) / math.pi
            k = min(n_arch - 1, max(0, int(a * n_arch)))
            key = ("a", k)
        else:
            side = 0 if x < DOOR_CX else 1
            r = sum(1 for s in side_rows if y >= s)
            key = ("s", side, r)
        blocks.setdefault(key, set()).add((x, y))
    return blocks


def draw_frame(cv, outer, inner, ramp, rng, mortar, speck=True):
    """Rama de piatră: fiecare bloc are lumină stânga-sus și umbră dreapta-jos, rosturi închise."""
    blocks = frame_blocks(outer, inner)
    for key, pts in blocks.items():
        bi = rng.choice([2, 2, 3, 2])
        r4 = [ramp[max(0, bi - 2)], ramp[bi - 1], ramp[bi], ramp[min(len(ramp) - 1, bi + 1)]]
        shade_bevel(cv, pts, r4)
        if speck:
            inner_pts = [p for p in pts if p not in edge(pts, TOP_LEFT + BOTTOM_RIGHT)]
            for p in inner_pts:
                if rng.random() < 0.1:
                    cv.put(*p, ramp[bi - 1])
    # rosturile dintre blocuri
    owner = {}
    for key, pts in blocks.items():
        for p in pts:
            owner[p] = key
    for (x, y), k in owner.items():
        for dx, dy in ((1, 0), (0, 1)):
            q = (x + dx, y + dy)
            if q in owner and owner[q] != k:
                cv.put(x, y, mortar)
    # umbra pe care o aruncă rama în deschizătură (lumina vine din stânga-sus)
    return blocks


def door_leaves(cv, inner, wood, metal, rng, split=True, bands=(24, 50), studs=True):
    """Cele două canate de lemn: scânduri verticale, benzi de fier, inele."""
    xs = [x for x, y in inner]
    x0, x1 = min(xs), max(xs)
    for (x, y) in inner:
        plank = (x - x0) // 6
        edge_l = (x - x0) % 6 == 0
        c = wood[2] if plank % 2 == 0 else wood[1 + 1]
        if edge_l:
            c = wood[1]
        elif (x - x0) % 6 == 1:
            c = wood[3]
        cv.put(x, y, c)
    for (x, y) in inner:
        if rng.random() < 0.08:
            cv.put(x, y, wood[1])
    if split:
        for (x, y) in inner:
            if x in (31, 32):
                cv.put(x, y, OUTLINE if x == 31 else wood[0])
    for by in bands:
        for (x, y) in inner:
            if by <= y <= by + 2:
                cv.put(x, y, metal[3] if y == by else (metal[2] if y == by + 1 else metal[1]))
        if studs:
            for x in range(x0 + 2, x1, 5):
                if (x, by + 1) in inner:
                    cv.put(x, by + 1, metal[4])
                    cv.put(x + 1, by + 2, metal[0])


def ring_handle(cv, x, y, metal):
    ring = set(pts_ellipse(x, y, 2.5, 2.5)) - set(pts_ellipse(x, y, 1.2, 1.2))
    cv.fill(ring, metal[3])
    cv.fill(edge(ring, BOTTOM_RIGHT), metal[1])
    cv.put(int(x) - 1, int(y) - 2, metal[4])
    cv.put(int(x), y - 3, metal[2])


def inner_shadow(cv, inner, ramp_dark, depth=2):
    """Rama umbrește marginea de sus și din stânga a ușii."""
    s = set(inner)
    for (x, y) in s:
        if (x, y - depth) not in s or (x - 1, y) not in s:
            cv.put(x, y, ramp_dark)


def make_door(floor, kind, seed, open_=False, frame_only=False):
    rng = random.Random(seed)
    cv = Canvas(64, 64)
    outer, inner = door_geometry(kind)
    stone = {"basement": BS[1:], "caves": CV[1:], "depths": DP[0:]}[floor]
    mortar = {"basement": BS[1], "caves": CV[1], "depths": DP[0]}[floor]
    if kind in ("normal",):
        draw_frame(cv, outer, inner, stone, rng, mortar)
    elif kind == "treasure":
        draw_frame(cv, outer, inner, [GOLD[0], GOLD[0], GOLD[1], GOLD[2], GOLD[3]], rng, mix(GOLD[0], OUTLINE, 0.5))
    elif kind == "boss":
        flesh = [FLESH[0], FLESH[1], FLESH[2], BLOOD[2], FLESH[3]]
        draw_frame(cv, outer, inner, flesh, rng, FLESH[0])
    elif kind == "bedroom":
        wood_ramp = ["#2a1b13", "#4a2e17", "#6b4423", "#8a5a2b", "#a8743d"]
        draw_frame(cv, outer, inner, wood_ramp, rng, "#2a1b13")
        # rama de lemn: fibre orizontale/verticale
        for (x, y) in outer - inner:
            if rng.random() < 0.12:
                cv.put(x, y, wood_ramp[1])
    if frame_only:
        pass   # doar rama: deschizătura rămâne transparentă
    elif open_:
        # deschizătura: întuneric, cu podeaua care se vede ușor jos
        for (x, y) in inner:
            cv.put(x, y, OUTLINE)
        for (x, y) in inner:
            if y >= 56:
                cv.put(x, y, BS[1] if floor == "basement" else (CV[1] if floor == "caves" else DP[1]))
            if y >= 61:
                cv.put(x, y, BS[2] if floor == "basement" else (CV[2] if floor == "caves" else DP[2]))
        if kind == "boss":
            for (x, y) in [(26, 38), (27, 38), (36, 38), (37, 38)]:
                cv.put(x, y, BLOOD[3])   # ochi roșii în întuneric
    else:
        if kind == "normal":
            door_leaves(cv, inner, WOOD, METAL, rng)
            ring_handle(cv, 27.5, 40, METAL)
            ring_handle(cv, 36.5, 40, METAL)
        elif kind == "treasure":
            door_leaves(cv, inner, WOOD, GOLD + [GOLD[3]], rng)
            # încuietoare aurie în centru, cu gaura cheii
            lock = pts_rect(28, 35, 35, 43)
            shade_bevel(cv, lock, [GOLD[0], GOLD[1], GOLD[2], GOLD[3]])
            cv.fill([(31, 38), (32, 38), (31, 39), (32, 39), (31, 40), (32, 40), (31, 41)], OUTLINE)
            # coroniță deasupra (simbolul camerei de comoară)
            crown = [(24, 18), (26, 15), (28, 18), (31, 14), (32, 14), (35, 18), (37, 15), (39, 18), (39, 21), (24, 21)]
            cp = pts_poly(crown)
            cpl = Canvas(64, 64)
            shade_bevel(cpl, cp, [GOLD[0], GOLD[1], GOLD[2], GOLD[3]])
            cpl.outline(OUTLINE)
            cv.blit(cpl, 0, 0)
        elif kind == "boss":
            dark = [OUTLINE, "#1a1316", "#2a1f24", "#3d2b33", "#4a3a40"]
            door_leaves(cv, inner, [OUTLINE, mix("#5c0d0d", OUTLINE, 0.5), "#5c0d0d", mix("#5c0d0d", "#9e1b1b", 0.5), "#9e1b1b"],
                        METAL, rng, bands=(26, 52))
            # țepi de fier pe canate
            for (sx, sy) in [(20, 36), (26, 44), (37, 36), (43, 44), (20, 56), (43, 56)]:
                cv.put(sx, sy, METAL[4])
                cv.put(sx + 1, sy, METAL[2])
                cv.put(sx, sy + 1, METAL[2])
                cv.put(sx + 1, sy + 1, METAL[0])
        elif kind == "bedroom":
            # ușă vopsită, un singur canat, cu un desen cu creioane lipit pe ea
            pale = [mix("#a8743d", OUTLINE, 0.3), "#8a5a2b", "#a8743d", mix("#a8743d", "#e8dcc4", 0.4), mix("#a8743d", "#fff0e8", 0.65)]
            for (x, y) in inner:
                cv.put(x, y, pale[2] if (x - 13) % 9 else pale[1])
            for (x, y) in inner:
                if (x - 13) % 9 == 1:
                    cv.put(x, y, pale[3])
            # panouri ale ușii
            for (px0, py0, px1, py1) in [(17, 20, 46, 34), (17, 40, 46, 58)]:
                cv.frame(px0, py0, px1, py1, pale[1])
                cv.line(px0 + 1, py0 + 1, px1 - 1, py0 + 1, pale[3])
                cv.line(px0 + 1, py0 + 1, px0 + 1, py1 - 1, pale[3])
            # desenul: hârtie cu un copil care plânge, lipit cu bandă
            paper = pts_rect(21, 22, 36, 33)
            cv.fill(paper, "#e8dcc4")
            cv.fill(edge(paper, BOTTOM_RIGHT), "#c8b89a")
            cv.put(21, 22, "#c8b89a")
            face = set(pts_ellipse(28, 27, 3.5, 3.2))
            cv.fill(edge(face, [(1, 0), (-1, 0), (0, 1), (0, -1)]), "#4a7fbf")
            cv.put(27, 26, OUTLINE)
            cv.put(30, 26, OUTLINE)
            cv.put(27, 28, "#7fb2e5")
            cv.put(27, 29, "#7fb2e5")
            cv.put(30, 28, "#7fb2e5")
            cv.line(28, 30, 29, 30, "#d33c3c")
            cv.fill([(33, 25), (35, 25), (32, 26), (33, 26), (34, 26), (35, 26), (36, 26), (33, 27), (34, 27), (35, 27), (34, 28)], "#d33c3c")
            cv.put(28, 22, "#d6ecff")
            cv.put(29, 22, "#d6ecff")
            # clanța
            cv.fill(pts_rect(41, 46, 43, 48), GOLD[1])
            cv.put(41, 46, GOLD[3])
            cv.put(43, 48, GOLD[0])
        inner_shadow(cv, inner, OUTLINE if kind == "boss" else WOOD[0], depth=2)
    # decoruri peste ramă
    if kind == "boss":
        boss_decor(cv, outer, inner, rng, open_)
    elif kind == "treasure":
        for (x, y) in [(8, 40), (55, 40), (8, 54), (55, 54)]:
            cv.put(x, y, GOLD[3])
    cv.outline(OUTLINE)
    # pragul ușii: o linie de piatră jos pe toată lățimea
    return cv


def boss_decor(cv, outer, inner, rng, open_):
    """Ușa de boss: dinți pe marginea interioară a arcului, cranii jos, coarne/țepi pe exterior."""
    # dinți: triunghiuri mici care intră în deschizătură
    s = set(inner)
    teeth_layer = Canvas(64, 64)
    arch = [(x, y) for (x, y) in edge(s, [(0, -1), (-1, 0), (1, 0)]) if y < 46]
    arch.sort(key=lambda p: math.atan2(p[1] - 31, p[0] - DOOR_CX))
    for i, (x, y) in enumerate(arch):
        if i % 4 != 0:
            continue
        # direcția spre centru
        dx, dy = DOOR_CX - x, 34 - y
        L = math.hypot(dx, dy) or 1
        dx, dy = dx / L, dy / L
        tip = (round(x + dx * 4), round(y + dy * 4))
        perp = (-dy, dx)
        a = (round(x + perp[0] * 1.6), round(y + perp[1] * 1.6))
        b = (round(x - perp[0] * 1.6), round(y - perp[1] * 1.6))
        tri = pts_poly([a, b, tip])
        teeth_layer.fill(tri, BONE[2])
        teeth_layer.fill(edge(tri, BOTTOM_RIGHT), BONE[1])
        teeth_layer.put(*tip, BONE[1])
    teeth_layer.outline(OUTLINE)
    cv.blit(teeth_layer, 0, 0)
    # cranii la baza ramei
    skull(cv, 2, 50)
    skull(cv, 50, 50)
    # craniu mare drept cheie de boltă
    big = Canvas(64, 64)
    head = set(pts_ellipse(31.5, 7, 7, 6))
    jaw = set(pts_rect(27, 10, 36, 14))
    shp = head | jaw
    shade_sphere(big, shp, BONE[0:3], 31.5, 7, 7.5, 7.5, dither=0.0)
    for ex in (27, 34):
        big.fill(pts_rect(ex, 6, ex + 2, 9), OUTLINE)
        big.put(ex + 1, 7, BLOOD[3])
    big.fill([(31, 10), (32, 10), (31, 11), (32, 11)], OUTLINE)
    for tx in range(28, 36, 2):
        big.put(tx, 13, BONE[0])
        big.put(tx, 14, OUTLINE)
    big.put(28, 3, BONE[3])
    big.put(29, 2, BONE[3])
    big.outline(OUTLINE)
    cv.blit(big, 0, 0)
    # coarne pe exteriorul ramei
    for side in (-1, 1):
        horn = []
        bx = 31.5 + side * 22
        for k in range(8):
            horn.append((round(bx + side * (k * 0.7)), round(14 - k * 1.2)))
        hl = Canvas(64, 64)
        pts = dilate(horn, 1, diagonal=False)
        pts = set(p for p in pts if 0 <= p[0] < 64 and 0 <= p[1] < 64)
        hl.fill(pts, BONE[1])
        hl.fill(edge(pts, BOTTOM_RIGHT), BONE[0])
        hl.fill(edge(pts, TOP_LEFT), BONE[2])
        hl.outline(OUTLINE)
        cv.blit(hl, 0, 0)
    # stropi de sânge pe ramă
    for (x, y) in outer - inner:
        if rng.random() < 0.04:
            cv.put(x, y, BLOOD[3])


# ================================================================ generarea și salvarea

def moss_patch(cv, rng, cx, cy, rx, ry, seed, lo, hi):
    """Pernă de mușchi: formă neregulată din zgomot, margine „pufoasă” (pixeli răzleți), lumină sus-stânga."""
    n = tile_noise(T, T, 8, seed, octaves=2)
    core = set()
    for y in range(int(cy - ry - 2), int(cy + ry + 3)):
        for x in range(int(cx - rx - 2), int(cx + rx + 3)):
            if not (lo + 1 <= x <= hi - 1 and lo + 1 <= y <= hi - 1):
                continue
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if d + (n[y % T, x % T] - 0.5) * 0.9 < 1.0:
                core.add((x, y))
    fuzz = set()
    for (x, y) in edge(core, [(1, 0), (-1, 0), (0, 1), (0, -1)]):
        for q in ((x + rng.choice([-1, 1]), y), (x, y + rng.choice([-1, 1]))):
            if q not in core and rng.random() < 0.45 and lo + 1 <= q[0] <= hi - 1 and lo + 1 <= q[1] <= hi - 1:
                fuzz.add(q)
    layer = Canvas(T, T)
    shade_sphere(layer, core, MOSS[0:4], cx, cy, rx + 1, ry + 1, dither=0.5)
    layer.fill(fuzz, MOSS[1])
    # smocuri: pixeli mai deschiși răzleți
    for p in core:
        if rng.random() < 0.07:
            layer.put(*p, MOSS[3])
    # umbra pernei pe piatră
    for (x, y) in edge(core, [(0, 1)]):
        if (x, y + 1) not in core and lo <= y + 1 <= hi:
            cv.put(x, y + 1, CV[1])
    cv.blit(layer, 0, 0)
    return core


def generate():
    out = {}
    walls = {
        "basement": (basement_wall(), basement_wall_variant, ["cracks", "blood", "poop"]),
        "caves": (caves_wall(), caves_wall_variant, ["moss", "mushrooms", "bones"]),
        "depths": (depths_wall(), depths_wall_variant, ["chains", "flesh", "blood"]),
    }
    for fi, floor in enumerate(FLOORS):
        d = {}
        base, fn, kinds = walls[floor]
        d["wall"] = base
        for i, k in enumerate(kinds):
            d["wall_%d" % (i + 1)] = fn(base, k, 1000 + fi * 10 + i)
        d["floor"] = {"basement": basement_floor, "caves": caves_floor, "depths": depths_floor}[floor]()
        d["ceiling"] = {"basement": basement_ceiling, "caves": caves_ceiling, "depths": depths_ceiling}[floor]()
        d["beam"] = {
            "basement": lambda: wood_beam(301, [OUTLINE, "#2a1b13", "#4a2e17", "#6b4423", "#8a5a2b"]),
            "caves": lambda: wood_beam(302, [OUTLINE, mix("#4a2e17", "#43474d", 0.5), mix("#6b4423", "#5c6168", 0.55),
                                             mix("#8a5a2b", "#7a7f86", 0.55), mix("#a8743d", "#7a7f86", 0.5)], moss=True, wet=True),
            "depths": lambda: wood_beam(303, [OUTLINE, "#1a1316", mix("#2a1f24", "#4a2e17", 0.4), mix("#3d2b33", "#6b4423", 0.35),
                                              "#4a3a40"], bands=True),
        }[floor]()
        for kind in ("normal", "boss", "treasure", "bedroom"):
            d["door_" + kind] = make_door(floor, kind, 500 + fi * 7 + len(kind))
            d["door_%s_open" % kind] = make_door(floor, kind, 500 + fi * 7 + len(kind), open_=True)
        d["door_frame"] = make_door(floor, "normal", 500 + fi * 7 + 6, frame_only=True)
        out[floor] = d
    return out


VARIANT_NOTES = {   # ce conține fiecare variantă de perete (wall_1..3)
    "basement": ["cracks + broken brick", "blood splat", "poop smear"],
    "caves": ["moss + drips", "mushrooms", "skull + bones"],
    "depths": ["chains", "clotted flesh", "bleeding crack"],
}


def save_all(tex):
    for floor, d in tex.items():
        for name, cv in d.items():
            cv.save(os.path.join(ENV_DIR, floor, name + ".png"))


# ---------------------------------------------------------------- previzualizare de hol (doar pentru planșă)

def corridor_preview(d, floor, W=320, H=180, seed=5):
    """Randare simplă tip ray-cast a holului, ca să judecăm texturile „în joc” (nu e codul jocului)."""
    rng = random.Random(seed)
    tex = {k: np.array(v.image().convert("RGBA")).astype(float) for k, v in d.items()}
    hw, ch, cam_y, zend = 1.6, 2.6, 1.15, 13.0
    f = 150.0
    out = np.zeros((H, W, 3))
    variants = ["wall", "wall", "wall_1", "wall", "wall_2", "wall", "wall_3", "wall"]
    seg_l = [variants[(i * 3 + 1) % len(variants)] for i in range(12)]
    seg_r = [variants[(i * 5 + 4) % len(variants)] for i in range(12)]
    torches = [(-hw, 3.0), (hw, 6.5), (-hw, 10.0)]
    warm = np.array([1.12, 1.0, 0.86])
    for j in range(H):
        for i in range(W):
            dx = (i + 0.5 - W / 2) / f
            dy = (H / 2 - j - 0.5) / f + 0.08
            dz = 1.0
            hits = []
            if dx > 1e-6:
                hits.append(((hw) / dx, "wr"))
            if dx < -1e-6:
                hits.append(((-hw) / dx, "wl"))
            if dy < -1e-6:
                hits.append(((-cam_y) / dy, "fl"))
            if dy > 1e-6:
                hits.append(((ch - cam_y) / dy, "ce"))
            hits.append((zend, "end"))
            t, kind = min(h for h in hits if h[0] > 0)
            px, py, pz = dx * t, cam_y + dy * t, dz * t
            col = None
            if kind in ("wl", "wr"):
                seg = int(pz // 2)
                name = (seg_l if kind == "wl" else seg_r)[seg % 12]
                u = (pz * 32) % 64
                if kind == "wl":
                    u = 63 - u
                v = ((ch - py) * 32) % 64
                col = tex[name][int(v), int(u)]
            elif kind == "fl":
                u, v = (px * 32) % 64, (pz * 32) % 64
                col = tex["floor"][int(63 - v), int(u)]
            elif kind == "ce":
                if (pz % 3.0) < 0.5:
                    u, v = (px * 32) % 32, (pz * 64) % 32
                    col = tex["beam"][int(v), int(u)]
                else:
                    u, v = (px * 32) % 64, (pz * 32) % 64
                    col = tex["ceiling"][int(v), int(u)]
            else:
                u = px * 32 + 32
                v = (ch - py) * 32
                dv = v - (ch * 32 - 64)
                if 0 <= u < 64 and 0 <= dv < 64 and tex["door_normal"][int(dv), int(u)][3] > 0:
                    col = tex["door_normal"][int(dv), int(u)]
                else:
                    col = tex["wall"][int(v) % 64, int(u) % 64]
            rgb = col[:3] / 255.0
            # lumină: torțe calde + atenuare cu distanța, posterizată pe trepte
            light = 0.18 + 1.2 / (1 + 0.03 * pz * pz)
            for (tx, tz) in torches:
                dd = (px - tx) ** 2 + (py - 1.7) ** 2 + (pz - tz) ** 2
                light += 1.1 / (1 + 1.6 * dd)
            light = math.floor(min(light, 1.6) * 6) / 6
            out[j, i] = np.clip(rgb * light * (warm if floor != "caves" else np.array([1.05, 1.0, 0.95])), 0, 1)
    img = Image.fromarray((out * 255).astype(np.uint8), "RGB").convert("RGBA")
    return img


def review(tex):
    sheet = Sheet("Darkest Basement - environment textures (tiled 3x3)")
    for floor in FLOORS:
        d = tex[floor]
        imgs = {k: v.image() for k, v in d.items()}
        prev = upscale(corridor_preview(d, floor), 2)
        sheet.row([(prev, "%s - corridor preview (rough raycast, x2)" % floor)], heading=floor)
        sheet.row([(upscale(tile_grid(imgs[n]), 2), "%s %s" % (n, "" if n == "wall" else "- " + note))
                   for n, note in [("wall", ""), ("wall_1", VARIANT_NOTES[floor][0]),
                                   ("wall_2", VARIANT_NOTES[floor][1]), ("wall_3", VARIANT_NOTES[floor][2])]])
        strip = Image.new("RGBA", (64 * 6, 64))
        for i, n in enumerate(["wall", "wall_1", "wall", "wall_2", "wall_3", "wall"]):
            strip.paste(imgs[n], (i * 64, 0))
        sheet.row([(upscale(tile_grid(imgs["floor"]), 2), "floor"),
                   (upscale(tile_grid(imgs["ceiling"]), 2), "ceiling"),
                   (upscale(tile_grid(imgs["beam"]), 4), "beam (32px, x4)"),
                   ])
        sheet.row([(upscale(strip, 2), "mixed strip: wall,1,wall,2,3,wall (variant seams)")])
        sheet.row([(on_checker(imgs[n], 3), n.replace("door_", "")) for n in
                   ["door_normal", "door_boss", "door_treasure", "door_bedroom", "door_frame"]])
        sheet.row([(on_checker(imgs[n], 3), n.replace("door_", "")) for n in
                   ["door_normal_open", "door_boss_open", "door_treasure_open", "door_bedroom_open"]])
    im = sheet.render()
    os.makedirs(REVIEW_DIR, exist_ok=True)
    im.save(os.path.join(REVIEW_DIR, "environment.png"))
    return im


def check_seams(tex):
    """Verifică numeric că texturile de bază se leagă și că variantele au aceleași margini ca baza."""
    bad = []
    for floor, d in tex.items():
        base = d["wall"].a
        for k in ("wall_1", "wall_2", "wall_3"):
            v = d[k].a
            for (a, b) in [(base[:, :MARGIN], v[:, :MARGIN]), (base[:, -MARGIN:], v[:, -MARGIN:]),
                           (base[:MARGIN], v[:MARGIN]), (base[-MARGIN:], v[-MARGIN:])]:
                if not np.array_equal(a, b):
                    bad.append("%s/%s margin differs from wall" % (floor, k))
                    break
    return bad


if __name__ == "__main__":
    tex = generate()
    save_all(tex)
    problems = check_seams(tex)
    for p in problems:
        print("SEAM:", p)
    for floor in FLOORS:
        for n in ("wall", "floor", "ceiling", "beam"):
            lr, tb = seam_score(tex[floor][n].image())
            print("%-9s %-8s edge/inner ratio  lr=%.2f tb=%.2f" % (floor, n, lr, tb))
    review(tex)
    print("ok")
