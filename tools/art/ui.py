"""Kitul de UI: rame de cărți, iconițe, panouri 9-slice, butoane, bara de ture, bara de viață, logo, cursor.

Rulează:  python3 tools/art/ui.py
Scrie în Assets/_Game/Resources/Sprites/UI/ (+ ui_slices.json) și planșele docs/art-review/ui.png și
docs/art-review/card-ui-mock.png (cadru 640×360 mărit ×3).

Rezoluția de referință e 640×360, afișată ×3. De aceea iconițele sunt de 12×12 și cartea de 64×90:
la 1× fiecare pixel contează, așa că iconițele mici sunt desenate pixel cu pixel (hărți ASCII).
"""
import json
import math
import os
import random

from PIL import Image

from envkit import (CLEAR, OUTLINE, REVIEW_DIR, UI_DIR, BOTTOM_RIGHT, TOP_LEFT, TOP, C, Canvas, Sheet, dilate,
                    draw_text, edge, font, mix, on_checker, pts_ellipse, pts_line, pts_poly, pts_rect,
                    pts_rect_border, shade_bevel, shade_sphere, text_size, tile_noise, upscale, wrap_text)
from environment import BLOOD, BONE, GOLD, METAL, POOP, WOOD
import palette as P

INK = P.UI["ink"]
PAPER = [mix("#c8b89a", INK, 0.35), "#c8b89a", mix("#e8dcc4", "#c8b89a", 0.45), "#e8dcc4", mix("#e8dcc4", "#fff0e8", 0.5)]
TEAR = [mix(P.TEAR[2], OUTLINE, 0.4), P.TEAR[2], P.TEAR[1], P.TEAR[0]]   # închis -> deschis


def ramp4(c, light="#fff0e8"):
    """Rampă de 4 tonuri dintr-o culoare de bază: umbră adâncă, umbră, bază, lumină."""
    return [mix(c, OUTLINE, 0.55), mix(c, OUTLINE, 0.28), C(c), mix(c, light, 0.32)]


# ================================================================ hărți ASCII pentru iconițe

def from_ascii(rows, legend):
    h, w = len(rows), max(len(r) for r in rows)
    cv = Canvas(w, h)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in legend:
                cv.put(x, y, legend[ch])
    return cv


def lg(ramp, **extra):
    """Legendă standard: o contur, D umbră, B bază, L lumină, W luciu."""
    d = {"o": OUTLINE, "X": ramp[0], "D": ramp[1], "B": ramp[2], "L": ramp[3]}
    d["W"] = extra.pop("W", mix(ramp[3], "#ffffff", 0.55))
    d.update(extra)
    return d


ICONS = {}

ICONS["status_block"] = ([
    "............",
    ".oooooooooo.",
    ".oLLLLLLLDo.",
    ".oLWLBBBBDo.",
    ".oLLBBBBBDo.",
    ".oLBBBBBBDo.",
    ".oLBBBBBBDo.",
    "..oBBBBBDo..",
    "..oBBBBBDo..",
    "...oBBBDo...",
    "....oBDo....",
    ".....oo.....",
], lg(ramp4(P.STATUS["block"])))

ICONS["status_bleed"] = ([
    "....o.......",
    "...oLo......",
    "...oLBo.....",
    "..oLWBBo....",
    "..oLBBBDo...",
    ".oLBBBBBDo..",
    ".oLBBBBBDo.o",
    ".oBBBBBBDooL",
    ".oBBBBBDDooB",
    "..oBBBDDo.oD",
    "...ooooo...o",
    "............",
], lg([BLOOD[0], BLOOD[1], BLOOD[2], BLOOD[3]], W=BLOOD[4]))

ICONS["status_poison"] = ([
    "...oooooo...",
    "..oLLLLBBo..",
    ".oLWLLBBBDo.",
    ".oLLBBBBBDo.",
    ".oLooBBooDo.",
    ".oBooBBooDo.",
    ".oBBBooBBDo.",
    "..oBBBBBDo..",
    "..oDBBBBDo..",
    "..oBoBoBoo..",
    "...oooooo...",
    "............",
], lg(ramp4(P.STATUS["poison"])))

ICONS["status_stun"] = ([
    ".....oo.....",
    "....oWLo....",
    "....oLBo....",
    "ooooLLBBoooo",
    "oWLLLLBBBBDo",
    ".oLLLBBBBDo.",
    "..oLBBBBDo..",
    "..oLBBBBDo..",
    ".oLBBooBBDo.",
    ".oBDo..oBDo.",
    "oDDo....oDDo",
    ".oo......oo.",
], lg(ramp4(P.STATUS["stun"])))

ICONS["status_weak"] = ([
    ".....oo.....",
    "....oLDo....",
    "..ooLLDDoo..",
    "..oHHHHHHo..",
    "...ooLDoo...",
    "....oLDo....",
    "....oLDo....",
    "....oWBo....",
    ".....oo.o...",
    "......oLDo..",
    ".......oLDo.",
    "........oo..",
], lg(ramp4(P.STATUS["weak"]), H=mix(P.STATUS["weak"], OUTLINE, 0.45)))

ICONS["status_mark"] = ([
    ".....oo.....",
    "...ooBBoo...",
    "..oBBooBBo..",
    ".oBo.oo.oDo.",
    ".oBo.oo.oDo.",
    "oBooooooooDo",
    "oBooooooooDo",
    ".oBo.oo.oDo.",
    ".oBo.oo.oDo.",
    "..oDDooDDo..",
    "...ooDDoo...",
    ".....oo.....",
], lg(ramp4(P.STATUS["mark"])))

ICONS["status_strength"] = ([
    "......ooo...",
    ".....oLLBo..",
    ".....oLBBo..",
    "......oLBo..",
    "......oLBo..",
    "..oooooLBo..",
    ".oLLWLLBBDo.",
    "oLLLLLBBBDo.",
    "oBBBBBBBBDo.",
    ".oDDDDDDDo..",
    "..ooooooo...",
    "............",
], lg(ramp4(P.STATUS["strength"])))

ICONS["status_taunt"] = ([
    "..o..oo..o..",
    "..oooLLooo..",
    ".oLLLooBBBo.",
    "ooLLLooBBBoo",
    ".oLLLooBBBo.",
    "..oLLooBBo..",
    ".oLLLooBBDo.",
    "ooLLLBBBBDoo",
    ".oLLLooBBDo.",
    "..oDDooDDo..",
    "..ooo..ooo..",
    "............",
], lg(ramp4(P.STATUS["taunt"])))

# ---- intenții (deasupra inamicilor, pe fundal închis)
STEEL = [METAL[1], METAL[3], METAL[4], METAL[5]]
ICONS["intent_attack"] = ([
    ".........ooo",
    "........oWLo",
    ".......oWLDo",
    "......oWLDo.",
    ".....oWLDo..",
    "..o.oWLDo...",
    "..ooWLDo....",
    "...oLDo.....",
    "..oHoooo....",
    ".oHHo.......",
    "oHHo........",
    "ooo.........",
], lg(STEEL, W="#ffffff", H=BLOOD[2]))

ICONS["intent_attack_aoe"] = ([
    ".......oo..o",
    "..o...oWo.oW",
    ".oW..oWRooWR",
    "oWR.oWRooWRo",
    "WRooWRooWRo.",
    "RooWRooWRo..",
    "ooWRooWRo...",
    "oWRooWRo....",
    "WRooWRo.....",
    "RooWRo......",
    "o.oRo.......",
    "...o........",
], {"o": OUTLINE, "W": "#fff0e8", "R": BLOOD[3]})

ICONS["intent_block"] = ([
    "............",
    ".oooooooooo.",
    ".oLLLLLLLDo.",
    ".oLWLBBBBDo.",
    ".oLLBooBBDo.",
    ".oLBoLDoBDo.",
    ".oLBoDDoBDo.",
    "..oBBooBDo..",
    "..oBBBBBDo..",
    "...oBBBDo...",
    "....oBDo....",
    ".....oo.....",
], lg([METAL[1], METAL[2], METAL[3], METAL[4]], W=METAL[5]))

ICONS["intent_debuff"] = ([
    "...oooooo...",
    "...oLBBDo...",
    "...oLBBDo...",
    "...oLBBDo...",
    "ooooLBBDoooo",
    "oLLLBBBBBDDo",
    ".oLBBBBBBDo.",
    "..oLBBBBDo..",
    "...oLBBDo...",
    "....oBDoGo..",
    ".....oo.oGo.",
    ".........o..",
], lg(ramp4(P.LILITH["dress"]), G=P.STATUS["poison"]))

ICONS["intent_buff"] = ([
    ".....oo.....",
    "....oLDo....",
    "...oLBBDo...",
    "..oLBBBBDo..",
    ".oLLBBBBBDo.",
    "oLLLBBBBBDDo",
    "ooooLBBDoooo",
    "...oLBBDo...",
    "...oLBBDo...",
    "...oLBBDo...",
    "...oLBBDo...",
    "...oooooo...",
], lg(ramp4(P.STATUS["mark"])))

ICONS["intent_summon"] = ([
    "..ooo..ooo..",
    ".oWWLooLWWo.",
    ".oWLLooLLWo.",
    "..oLLooLLo..",
    "...ooBBoo...",
    "..oBBBBBBo..",
    ".oBRRBBRRBo.",
    ".oBRRBBRRBo.",
    ".oBBBBBBBBo.",
    "..oBBooBBo..",
    "...oo..oo...",
    "............",
], {"o": OUTLINE, "W": "#e8dcc4", "L": "#c8b89a", "B": "#2a2226", "R": BLOOD[3]})

ICONS["intent_unknown"] = ([
    "...oooooo...",
    "..oLLLLLLo..",
    ".oLLooooLLo.",
    ".oLo...oLLo.",
    "..o...oLLo..",
    ".....oLLo...",
    "....oLLo....",
    "....oLLo....",
    ".....oo.....",
    "....oLLo....",
    "....oLLo....",
    ".....oo.....",
], {"o": OUTLINE, "L": "#e8dcc4"})

# ---- energia: lacrimi (cărțile costă lacrimi)
ICONS["energy_full"] = ([
    ".....oo.....",
    "....oLLo....",
    "....oLBo....",
    "...oLWBBo...",
    "..oLWBBBDo..",
    ".oLLBBBBBDo.",
    ".oLBBBBBBDo.",
    ".oLBBBBBBDo.",
    ".oBBBBBBDDo.",
    "..oBBBBDDo..",
    "...oDDDDo...",
    "....oooo....",
], lg(TEAR, W="#ffffff"))

ICONS["energy_empty"] = ([
    ".....oo.....",
    "....oDDo....",
    "....oXXo....",
    "...oDXXXo...",
    "..oDXXXXXo..",
    ".oDXXXXXXXo.",
    ".oDXXXXXXXo.",
    ".oDXXXXXXXo.",
    ".oXXXXXXXLo.",
    "..oXXXXXLo..",
    "...oLLLLo...",
    "....oooo....",
], {"o": OUTLINE, "X": mix(P.TEAR[2], OUTLINE, 0.72), "D": mix(P.TEAR[2], OUTLINE, 0.5),
    "L": mix(P.TEAR[2], OUTLINE, 0.3)})

# ---- harta (pe pergament: cerneală închisă și culori pline)
ICONS["map_unknown"] = ([
    "...oooooo...",
    "..oBBBBBBo..",
    ".oBBooooBBo.",
    ".oBo...oBBo.",
    "..o...oBBo..",
    ".....oBBo...",
    "....oBBo....",
    "....oBBo....",
    ".....oo.....",
    "....oBBo....",
    "....oBBo....",
    ".....oo.....",
], {"o": OUTLINE, "B": mix("#9a9a9a", INK, 0.2)})

ICONS["map_combat"] = ([
    "oo........oo",
    "oWo......oWo",
    ".oWo....oWo.",
    "..oWo..oWo..",
    "...oWooWo...",
    "....oWWo....",
    "...oLooLo...",
    "..oHoLLoHo..",
    ".oHo.oo.oHo.",
    "oHHo....oHHo",
    "ooo......ooo",
    "............",
], {"o": OUTLINE, "W": METAL[5], "L": METAL[3], "H": BLOOD[1]})

ICONS["map_treasure"] = ([
    "............",
    "o...o..o...o",
    "oo.oWo.oo.oo",
    "oLooLLoLLooL",
    "oLLoLLoLLoLL",
    "oLWLLBBBLLBo",
    "oLLLBBBBBBBo",
    "oLBBRBBRBBDo",
    "oBBBBBBBBBDo",
    "oDDDDDDDDDDo",
    "oooooooooooo",
    "............",
], {"o": OUTLINE, "W": GOLD[3], "L": GOLD[2], "B": GOLD[1], "D": GOLD[0], "R": BLOOD[3]})

ICONS["map_bedroom"] = ([
    "............",
    "oo..........",
    "oWo.........",
    "oWooooooooo.",
    "oWLLoBBBBBBo",
    "oWLLoBWBBWBo",
    "oWoooBBBBBBo",
    "oWDDDDDDDDDo",
    "oWoooooooooo",
    "oWo......oWo",
    "ooo......ooo",
    "............",
], {"o": OUTLINE, "W": WOOD[3], "L": "#fff0e8", "B": P.TEAR[1], "D": P.TEAR[2]})

ICONS["map_curio"] = ([
    "......o.....",
    ".....oLo....",
    "....oLBo....",
    "...oooooo...",
    "..oLLBBBBo..",
    "..oLBBBBDo..",
    ".oooooooooo.",
    "oLLWBBBBBBDo",
    "oLBBBBBBBBDo",
    "oBBBBBBBBDDo",
    ".oDDDDDDDDo.",
    "..oooooooo..",
], {"o": OUTLINE, "W": mix(POOP[3], "#fff0e8", 0.4), "L": POOP[3], "B": POOP[2], "D": POOP[1]})

ICONS["map_boss"] = ([
    "...oooooo...",
    "..oLLLLBBo..",
    ".oLWLLBBBDo.",
    ".oLLBBBBBDo.",
    ".oLooBBooDo.",
    ".oBoRBBoRDo.",
    ".oBBBooBBDo.",
    "..oBBBBBDo..",
    "..oDBBBBDo..",
    "..oBoBoBoo..",
    "...oooooo...",
    "............",
], {"o": OUTLINE, "W": BONE[3], "L": BONE[2], "B": BONE[1], "D": BONE[0], "R": BLOOD[3]})

ICONS["map_start"] = ([
    ".....oo.....",
    "....oLLo....",
    "...oLLBBo...",
    "..oLLBBBBo..",
    ".oLLBBBBBBo.",
    "oooooooooooo",
    ".oPPPPPPPPo.",
    ".oPPoooPPPo.",
    ".oPPoDoPPPo.",
    ".oPPoDoPPDo.",
    ".oooooooooo.",
    "............",
], {"o": OUTLINE, "L": BLOOD[3], "B": BLOOD[2], "P": "#e8dcc4", "D": WOOD[2]})

ICONS["map_empty"] = ([
    "............",
    "............",
    "............",
    "............",
    ".....oo.....",
    "....oBBo....",
    "....oBBo....",
    ".....oo.....",
    "............",
    "............",
    "............",
    "............",
], {"o": OUTLINE, "B": mix("#9a9a9a", INK, 0.2)})

ICONS["map_current"] = ([
    "...oooooo...",
    "..oWLLLLBo..",
    ".oWLLLLLBBo.",
    ".oLLLLLLLBo.",
    ".oLooLLooBo.",
    ".oLoEDLoEDo.",
    ".oTLLLLLTBo.",
    ".oTLLooLTBo.",
    "..oTLLLLTo..",
    "...oooooo...",
    "............",
    "............",
], {"o": OUTLINE, "W": P.SKIN[0], "L": P.SKIN[1], "B": P.SKIN[2], "E": "#ffffff", "D": OUTLINE,
    "T": P.TEAR[1]})


def icon(name):
    rows, legend = ICONS[name]
    return from_ascii(rows, legend)


# ================================================================ cărți (64×90)

CARD_W, CARD_H = 64, 90
ART = (4, 4, 59, 39)          # fereastra ilustrației: 56×36
PLATE = (4, 42, 59, 50)       # numele
TEXT = (4, 53, 59, 83)        # descrierea
GEM_AT = (28, 82)             # bijuteria de raritate (8×8) se pune aici

HEROES = {
    "isaac": {"ramp": [mix(P.TEAR[2], OUTLINE, 0.45), P.TEAR[2], P.TEAR[1], P.TEAR[0]],
              "plate": mix(P.TEAR[2], OUTLINE, 0.5), "accent": "#e8dcc4"},
    "cain": {"ramp": [mix(P.CAIN["clothes"][1], OUTLINE, 0.5), P.CAIN["clothes"][1], P.CAIN["clothes"][0],
                      mix(P.CAIN["clothes"][0], "#c9a227", 0.35)],
             "plate": P.CAIN["hair"][1], "accent": P.CAIN["hair"][0]},
    "lilith": {"ramp": [P.LILITH["hair"][1], P.LILITH["hair"][0], P.LILITH["dress"], mix(P.LILITH["dress"], "#fff0e8", 0.3)],
               "plate": P.LILITH["hair"][1], "accent": mix(P.LILITH["dress"], "#fff0e8", 0.45)},
    "azazel": {"ramp": [P.AZAZEL["black"][1], P.AZAZEL["black"][0], "#5c0d0d", "#9e1b1b"],
               "plate": P.AZAZEL["black"][0], "accent": P.AZAZEL["eye"]},
}


def card_silhouette():
    s = set(pts_rect(0, 0, CARD_W - 1, CARD_H - 1))
    for (x, y) in [(0, 0), (1, 0), (0, 1), (CARD_W - 1, 0), (CARD_W - 2, 0), (CARD_W - 1, 1),
                   (0, CARD_H - 1), (1, CARD_H - 1), (0, CARD_H - 2),
                   (CARD_W - 1, CARD_H - 1), (CARD_W - 2, CARD_H - 1), (CARD_W - 1, CARD_H - 2)]:
        s.discard((x, y))
    return s


def paper_fill(cv, rect, seed, stains=True):
    """Pergament: bază deschisă, foarte puțin zgomot (textul trebuie să rămână lizibil), pete pe margini."""
    rng = random.Random(seed)
    x0, y0, x1, y1 = rect
    cv.rect(x0, y0, x1, y1, PAPER[3])
    n = tile_noise(64, 64, 4, seed, octaves=2)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            v = n[(y - y0) % 64, (x - x0) % 64]
            border = min(x - x0, x1 - x, y - y0, y1 - y)
            if v > 0.72 or (border == 0 and rng.random() < 0.5):
                cv.put(x, y, PAPER[2])
            elif rng.random() < 0.025:
                cv.put(x, y, PAPER[2])
    if stains:
        # o pată de cafea / murdărie într-un colț
        cx, cy = x1 - 5, y1 - 4
        ring = set(pts_ellipse(cx, cy, 4, 3)) - set(pts_ellipse(cx, cy, 3, 2))
        for p in ring:
            if rng.random() < 0.7 and x0 <= p[0] <= x1 and y0 <= p[1] <= y1:
                cv.put(*p, PAPER[1])


def card_frame(hero):
    h = HEROES[hero]
    r = h["ramp"]
    rng = random.Random(hash(hero) % 1000 if False else sum(map(ord, hero)))
    cv = Canvas(CARD_W, CARD_H)
    sil = card_silhouette()
    cv.fill(sil, r[2])
    # textură discretă pe ramă
    for p in sil:
        if rng.random() < 0.08:
            cv.put(*p, r[1])
    # bizou exterior: lumină stânga-sus, umbră dreapta-jos, contur
    inner1 = set(p for p in sil if p not in edge(sil, [(1, 0), (-1, 0), (0, 1), (0, -1)]))
    cv.fill([p for p in edge(inner1, TOP_LEFT)], r[3])
    cv.fill([p for p in edge(inner1, BOTTOM_RIGHT)], r[1])
    cv.fill(edge(sil, [(1, 0), (-1, 0), (0, 1), (0, -1)]), OUTLINE)

    def inset(rect, fill=None):
        """Locaș adâncit: margine stânga-sus în umbră, dreapta-jos luminată, linie închisă în jur."""
        x0, y0, x1, y1 = rect
        cv.frame(x0 - 1, y0 - 1, x1 + 1, y1 + 1, OUTLINE)
        cv.line(x0 - 2, y0 - 2, x1 + 1, y0 - 2, r[1])
        cv.line(x0 - 2, y0 - 2, x0 - 2, y1 + 1, r[1])
        cv.line(x0 - 1, y1 + 2, x1 + 2, y1 + 2, r[3])
        cv.line(x1 + 2, y0 - 1, x1 + 2, y1 + 2, r[3])
        if fill:
            cv.rect(x0, y0, x1, y1, fill)

    inset(ART)
    cv.clear(pts_rect(*ART))                       # fereastra ilustrației e transparentă
    inset(PLATE, h["plate"])
    # plăcuța de nume: o linie de lumină sus, ca o bandă de metal/piele
    cv.line(PLATE[0], PLATE[1], PLATE[2], PLATE[1], mix(h["plate"], "#fff0e8", 0.18))
    inset(TEXT)
    paper_fill(cv, TEXT, sum(map(ord, hero)) + 3)
    # locașul bijuteriei, jos la mijloc
    sock = set(pts_rect(GEM_AT[0] - 1, GEM_AT[1] - 1, GEM_AT[0] + 8, GEM_AT[1] + 7))
    cv.fill(sock, OUTLINE)
    cv.fill(edge(sock, TOP_LEFT), r[1])
    # nituri în colțuri
    for (x, y) in [(2, 2), (CARD_W - 4, 2), (2, CARD_H - 4), (CARD_W - 4, CARD_H - 4)]:
        cv.fill([(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)], h["accent"])
        cv.put(x + 1, y + 1, r[1])
        cv.put(x, y, mix(h["accent"], "#ffffff", 0.4))
    hero_motif(cv, hero, r, h)
    return cv


def hero_motif(cv, hero, r, h):
    """Un motiv mic pe rama de jos, ca să recunoști eroul și fără culoare."""
    y = CARD_H - 5
    xs = [7, 15, 22, 41, 48, 56]
    if hero == "isaac":           # lacrimi
        for x in xs:
            cv.put(x, y - 1, P.TEAR[0])
            cv.fill([(x - 1, y), (x, y), (x + 1, y)], P.TEAR[1])
            cv.put(x, y + 1, P.TEAR[2])
    elif hero == "cain":          # cusături (ochiul cusut / haine peticite)
        for x in range(6, 26, 3):
            cv.put(x, y, r[0])
            cv.put(x + 1, y - 1, h["accent"])
        for x in range(39, 59, 3):
            cv.put(x, y, r[0])
            cv.put(x + 1, y - 1, h["accent"])
    elif hero == "lilith":        # stele mici (pentagrame stilizate)
        for x in xs:
            cv.fill([(x, y - 1), (x - 1, y), (x, y), (x + 1, y), (x, y + 1)], h["accent"])
    elif hero == "azazel":        # coarne în colțurile de sus și flăcări jos
        for (x, d) in [(4, 1), (CARD_W - 5, -1)]:
            cv.fill([(x, 3), (x + d, 2), (x + d, 1), (x + 2 * d, 0)], P.AZAZEL["horn"])
        for x in xs:
            cv.put(x, y, "#9e1b1b")
            cv.put(x, y - 1, P.AZAZEL["eye"])
            cv.put(x, y + 1, "#5c0d0d")


def card_back():
    cv = Canvas(CARD_W, CARD_H)
    sil = card_silhouette()
    leather = [mix("#2a1b13", OUTLINE, 0.3), "#2a1b13", "#3a271c", "#4a3326"]
    cv.fill(sil, leather[2])
    # model romb, ca la o copertă veche
    for (x, y) in sil:
        if (x + y) % 8 == 0 or (x - y) % 8 == 0:
            cv.put(x, y, leather[1])
    # chenar auriu dublu
    for d, c in [(2, GOLD[1]), (3, GOLD[2]), (5, GOLD[0])]:
        cv.frame(d, d, CARD_W - 1 - d, CARD_H - 1 - d, c)
    inner1 = set(p for p in sil if p not in edge(sil, [(1, 0), (-1, 0), (0, 1), (0, -1)]))
    cv.fill([p for p in edge(inner1, TOP_LEFT)], leather[3])
    cv.fill(edge(sil, [(1, 0), (-1, 0), (0, 1), (0, -1)]), OUTLINE)
    # emblema: o lacrimă mare cu o cruce, ca simbol al jocului
    tear = set(pts_ellipse(31.5, 49, 11, 11))
    for y in range(22, 49):
        t = (y - 22) / 27.0
        half = 11 * math.sin(t * math.pi / 2) ** 1.4
        for x in range(64):
            if abs(x - 31.5) <= half:
                tear.add((x, y))
    em = Canvas(CARD_W, CARD_H)
    shade_sphere(em, tear, [TEAR[0], TEAR[1], TEAR[2], TEAR[3]], 31.5, 46, 12, 18, dither=0.25)
    em.fill([(26, 38), (25, 39), (25, 40), (25, 41), (26, 42)], "#ffffff")
    for y in range(41, 58):
        em.put(31, y, "#e8dcc4")
        em.put(32, y, "#c8b89a")
    for x in range(26, 38):
        em.put(x, 46, "#e8dcc4")
        em.put(x, 47, "#c8b89a")
    em.outline(OUTLINE)
    em.outline(GOLD[1])
    cv.blit(em, 0, 0)
    for (x, y) in [(9, 9), (CARD_W - 10, 9), (9, CARD_H - 10), (CARD_W - 10, CARD_H - 10)]:
        cv.fill([(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)], GOLD[2])
        cv.put(x, y, GOLD[3])
        cv.put(x + 1, y + 1, GOLD[0])
    return cv


def card_art_mask():
    cv = Canvas(ART[2] - ART[0] + 1, ART[3] - ART[1] + 1, bg="#ffffff")
    return cv


def card_glow(color):
    """Contur de strălucire în jurul cărții (72×98, cartea stă la (4,4)). 4 inele cu alfa în trepte."""
    cv = Canvas(CARD_W + 8, CARD_H + 8)
    sil = set((x + 4, y + 4) for (x, y) in card_silhouette())
    prev = sil
    c = C(color)
    for k, a in enumerate([235, 160, 95, 45]):
        ring = dilate(prev, 1, diagonal=(k % 2 == 1)) - prev
        for p in ring:
            if 0 <= p[0] < cv.w and 0 <= p[1] < cv.h:
                cv.put(*p, c[:3] + (a,))
        prev = prev | ring
    return cv


def gem(rarity):
    base = P.RARITY[rarity]
    r = ramp4(base)
    rows = [
        "..oooo..",
        ".oWLLDo.",
        "oLWLBBDo",
        "oLLBBBDo",
        ".oLBBDo.",
        "..oBDo..",
        "...oo...",
        "........",
    ]
    return from_ascii(rows, {"o": OUTLINE, "W": mix(r[3], "#ffffff", 0.6), "L": r[3], "B": r[2], "D": r[1]})


def cost_orb():
    """Bulina de cost: o lacrimă mare (cărțile costă lacrimi). Cifra o scrie codul, albă cu contur."""
    cv = Canvas(16, 16)
    body = set(pts_ellipse(7.5, 9.5, 6, 5.6))
    for y in range(1, 10):
        t = (y - 1) / 8.0
        half = 6 * math.sin(t * math.pi / 2) ** 1.3
        for x in range(16):
            if abs(x - 7.5) <= half:
                body.add((x, y))
    shade_sphere(cv, body, TEAR, 7.5, 9, 6.5, 7, dither=0.2)
    cv.fill([(4, 7), (4, 8), (5, 6)], "#ffffff")
    cv.outline(OUTLINE)
    return cv


def rank_pip(on):
    """Bulina de rang (cărțile se joacă doar din anumite ranguri, ca în Darkest Dungeon)."""
    rows = [".ooo.", "oLBBo", "oBBDo", "oBDDo", ".ooo."]
    if on:
        return from_ascii(rows, {"o": OUTLINE, "L": GOLD[3], "B": GOLD[2], "D": GOLD[1]})
    return from_ascii(rows, {"o": OUTLINE, "L": "#4a3a40", "B": "#2a1f24", "D": "#1a1316"})


# ================================================================ panouri 9-slice

SLICES = {}


def register(name, l, r, t, b, mode="sliced"):
    SLICES[name] = {"sprite": name, "left": l, "right": r, "top": t, "bottom": b, "drawMode": mode}


def panel_paper():
    """Hârtie mototolită, ruptă pe margini. Marginile și centrul se repetă cu perioada 24,
    deci merge și în modul Tiled, și Sliced (centrul e aproape plat)."""
    W = H = 48
    B = 12
    P24 = 24
    cv = Canvas(W, H)
    rng = random.Random(77)
    # profilul marginii rupte: periodic cu 24 px, folosit pe toate laturile
    prof = [0, 0, 1, 1, 0, 0, 0, 1, 2, 1, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 2, 1, 0]

    def inside(x, y):
        # distanța până la marginea (ruptă) a hârtiei, pe fiecare latură
        if x < B:
            ex = x - 1 - prof[y % P24]
        elif x >= W - B:
            ex = (W - 2 - x) - prof[(y + 7) % P24]
        else:
            ex = 99
        if y < B:
            ey = y - 1 - prof[(x + 3) % P24]
        elif y >= H - B:
            ey = (H - 2 - y) - prof[(x + 11) % P24]
        else:
            ey = 99
        return min(ex, ey)

    n = tile_noise(P24, P24, 3, 81, octaves=2)
    for y in range(H):
        for x in range(W):
            d = inside(x, y)
            if d < 0:
                continue
            if d == 0:
                c = PAPER[0]                     # marginea arsă
            elif d == 1:
                c = PAPER[1]
            elif d <= 3:
                c = PAPER[2] if n[y % P24, x % P24] > 0.35 else PAPER[1]
            else:
                c = PAPER[3]
                if 4 <= d <= 6 and n[y % P24, x % P24] > 0.6:
                    c = PAPER[2]
            cv.put(x, y, c)
    # cute în colțuri (doar în colțuri, ca să nu se repete)
    for (x0, y0, dx, dy) in [(3, 5, 1, 1), (44, 6, -1, 1), (5, 42, 1, -1), (42, 43, -1, -1)]:
        for k in range(6):
            cv.put(x0 + dx * k, y0 + dy * (k // 2), PAPER[2])
            cv.put(x0 + dx * k, y0 + dy * (k // 2) + 1, PAPER[4])
    # o pată de cerneală / sânge pe colțul din dreapta-jos
    for p in pts_ellipse(41, 41, 2, 1.5):
        cv.put(*p, PAPER[1])
    cv.put(40, 40, PAPER[0])
    cv.outline(OUTLINE)
    register("panel_paper", B, B, B, B, mode="tiled")
    return cv


def panel_dark():
    """Placă închisă de lemn/fier pentru HUD: ramă teșită, nituri, centru plat (merge în modul Sliced)."""
    W = H = 32
    B = 8
    cv = Canvas(W, H)
    wood = ["#1a1316", "#2a1b13", "#3a271c", "#4a3326"]
    sil = set(pts_rect(0, 0, W - 1, H - 1)) - {(0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)}
    cv.fill(sil, wood[2])
    inner = set(pts_rect(4, 4, W - 5, H - 5))
    # ramă: lumină stânga-sus, umbră dreapta-jos
    ring = sil - inner
    cv.fill(ring, wood[2])
    cv.fill([p for p in ring if p[0] == 1 or p[1] == 1], wood[3])
    cv.fill([p for p in ring if p[0] == W - 2 or p[1] == H - 2], wood[1])
    cv.fill(edge(sil, [(1, 0), (-1, 0), (0, 1), (0, -1)]), OUTLINE)
    # centrul adâncit
    cv.fill(inner, mix("#2a1b13", "#1a1316", 0.4))
    cv.frame(3, 3, W - 4, H - 4, OUTLINE)
    cv.line(4, 4, W - 5, 4, "#1a1316")
    cv.line(4, 4, 4, H - 5, "#1a1316")
    # nituri
    for (x, y) in [(1, 1), (W - 3, 1), (1, H - 3), (W - 3, H - 3)]:
        cv.fill([(x + 0, y + 0)], METAL[4])
        cv.put(x + 1, y + 1, METAL[1])
        cv.put(x + 1, y, METAL[3])
        cv.put(x, y + 1, METAL[2])
    register("panel_dark", B, B, B, B)
    return cv


def tooltip_panel():
    """Pergament mic pentru tooltip-uri: contur de cerneală, umbră jos, centru plat."""
    W = H = 24
    B = 6
    cv = Canvas(W, H)
    sil = set(pts_rect(0, 0, W - 2, H - 2)) - {(0, 0), (W - 2, 0), (0, H - 2), (W - 2, H - 2)}
    cv.fill(sil, PAPER[3])
    cv.fill([p for p in sil if p[0] in (1, W - 3) or p[1] in (1, H - 3)], PAPER[2])
    cv.fill(edge(sil, [(1, 0), (-1, 0), (0, 1), (0, -1)]), INK)
    # umbra aruncată (1 px dreapta-jos)
    for (x, y) in sil:
        for q in ((x + 1, y + 1),):
            if q not in sil and 0 <= q[0] < W and 0 <= q[1] < H:
                cv.put(*q, OUTLINE[:7] if isinstance(OUTLINE, str) else OUTLINE)
    register("tooltip", B, B, B, B)
    return cv


def button(state):
    """Buton de hârtie cu grosime (umbră jos de 2 px). hover = mai deschis + contur auriu; pressed = apăsat."""
    W, H = 32, 18
    B = 5
    cv = Canvas(W, H)
    pressed = state == "pressed"
    top = 2 if pressed else 0
    face_h = H - 3
    body = set(pts_rect(1, top + 1, W - 2, top + face_h - 1))
    thick = set(pts_rect(1, top + face_h, W - 2, H - 2)) if not pressed else set()
    col = {"normal": PAPER[3], "hover": PAPER[4], "pressed": PAPER[2]}[state]
    cv.fill(body, col)
    cv.fill([p for p in body if p[1] == top + 1], PAPER[4] if state != "pressed" else PAPER[2])
    cv.fill([p for p in body if p[1] == top + face_h - 1], PAPER[2] if state != "pressed" else PAPER[1])
    cv.fill(thick, PAPER[1])
    cv.fill([p for p in thick if p[1] == H - 2], PAPER[0])
    shape = body | thick
    outline_col = GOLD[1] if state == "hover" else INK
    layer = Canvas(W, H)
    layer.fill(shape, (0, 0, 0, 255))
    for (x, y) in pts_rect(0, 0, W - 1, H - 1):
        if (x, y) not in shape and any((x + dx, y + dy) in shape for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            if not ((x in (0, W - 1)) and (y in (top, H - 1 if not pressed else top + face_h))):
                cv.put(x, y, outline_col)
    if state == "hover":
        # al doilea inel auriu, ca să se vadă clar care buton e selectat
        for (x, y) in pts_rect(1, top + 1, W - 2, top + face_h - 1):
            if x in (1, W - 2) or y == top + 1:
                cv.put(x, y, GOLD[2])
    register("button_" + state, B, B, B + (0 if pressed else 0), B)
    return cv


def portrait_frame(active):
    """Rama de portret din bara de ture (portretul 24×24 stă la (2,2)). Cea activă e aurie, cu săgeată jos."""
    cv = Canvas(28, 32)
    ring = set(pts_rect(0, 0, 27, 27)) - set(pts_rect(2, 2, 25, 25))
    ring -= {(0, 0), (27, 0), (0, 27), (27, 27)}
    r = [GOLD[0], GOLD[1], GOLD[2], GOLD[3]] if active else [METAL[0], METAL[1], METAL[2], METAL[3]]
    cv.fill(ring, r[2])
    cv.fill([p for p in ring if p[0] == 1 or p[1] == 1], r[3])
    cv.fill([p for p in ring if p[0] == 26 or p[1] == 26], r[1])
    cv.fill(edge(ring | set(pts_rect(2, 2, 25, 25)), [(1, 0), (-1, 0), (0, 1), (0, -1)]), OUTLINE)
    cv.frame(2, 2, 25, 25, OUTLINE)
    # fundalul portretului: întuneric, ca să se vadă și o ramă goală
    cv.rect(3, 3, 24, 24, mix("#2a1f24", OUTLINE, 0.3))
    for (x, y) in [(1, 1), (26, 1), (1, 26), (26, 26)]:
        cv.put(x, y, r[3] if active else METAL[4])
    if active:
        tri = [(10, 28), (17, 28), (13, 31), (14, 31)]
        cv.fill(pts_poly([(10, 28), (17, 28), (13.5, 31.5)], 64, 64), GOLD[2])
        cv.put(13, 28, GOLD[3])
        lay = Canvas(28, 32)
        lay.a = cv.a.copy()
        cv.outline(OUTLINE)
    else:
        cv.outline(OUTLINE)
    return cv


def hp_parts():
    """Bara de viață: rama (9-slice), umplerea roșie, umplerea „întârziată” deschisă și stratul albastru de block."""
    frame = Canvas(16, 7)
    frame.rect(1, 1, 14, 5, mix("#2a1b13", OUTLINE, 0.4))
    frame.frame(0, 0, 15, 6, OUTLINE)
    frame.line(1, 1, 14, 1, OUTLINE)            # umbra de sus în interior
    for p in [(0, 0), (15, 0), (0, 6), (15, 6)]:
        frame.put(*p, CLEAR)
    register("hpbar_frame", 2, 2, 2, 2)
    fill = Canvas(4, 4)
    for x in range(4):
        fill.put(x, 0, BLOOD[3])
        fill.put(x, 1, BLOOD[2])
        fill.put(x, 2, BLOOD[2])
        fill.put(x, 3, BLOOD[1])
    fill.put(0, 0, BLOOD[4])
    register("hpbar_fill", 1, 1, 0, 0)
    lag = Canvas(4, 4)
    for x in range(4):
        for y in range(4):
            lag.put(x, y, "#e8dcc4" if y < 3 else "#c8b89a")
    register("hpbar_fill_lag", 1, 1, 0, 0)
    block = Canvas(16, 7)
    bl = ramp4(P.STATUS["block"])
    block.frame(0, 0, 15, 6, bl[2])
    block.line(1, 0, 14, 0, bl[3])
    block.line(1, 6, 14, 6, bl[1])
    for p in [(0, 0), (15, 0), (0, 6), (15, 6)]:
        block.put(*p, CLEAR)
    register("hpbar_block", 2, 2, 2, 2)
    return {"hpbar_frame": frame, "hpbar_fill": fill, "hpbar_fill_lag": lag, "hpbar_block": block}


# ================================================================ logo

GLYPHS = {
    "D": [[(0, 0), (0, 14)], [(0, 0), (5, 0), (9, 3), (10, 7), (9, 11), (5, 14), (0, 14)]],
    "A": [[(0, 14), (5, 0), (10, 14)], [(2, 9), (8, 9)]],
    "R": [[(0, 14), (0, 0), (7, 0), (10, 2), (10, 5), (7, 7), (0, 7)], [(5, 7), (10, 14)]],
    "K": [[(0, 0), (0, 14)], [(10, 0), (1, 8)], [(3, 6), (10, 14)]],
    "E": [[(10, 0), (0, 0), (0, 14), (10, 14)], [(0, 7), (7, 7)]],
    "S": [[(10, 1), (7, 0), (3, 0), (0, 2), (0, 5), (3, 7), (7, 7), (10, 9), (10, 12), (7, 14), (3, 14), (0, 13)]],
    "T": [[(0, 0), (10, 0)], [(5, 0), (5, 14)]],
    "B": [[(0, 0), (0, 14)], [(0, 0), (7, 0), (9, 2), (9, 5), (6, 7), (0, 7)], [(6, 7), (10, 9), (10, 12), (7, 14), (0, 14)]],
    "M": [[(0, 14), (0, 0), (5, 8), (10, 0), (10, 14)]],
    "N": [[(0, 14), (0, 0), (10, 14), (10, 0)]],
}


def logo(text="DARKEST BASEMENT", W=300, H=60, seed=13):
    """Titlul desenat de mână: litere zgâriate, umflate, cu contur gros și picături de sânge (stil Isaac)."""
    rng = random.Random(seed)
    letters = Canvas(W, H)
    x = 6
    gw, gh = 15, 30
    base_y = 8
    drips_from = []
    for i, ch in enumerate(text):
        if ch == " ":
            x += 10
            continue
        strokes = GLYPHS[ch]
        ang = math.radians(rng.uniform(-5, 5))
        sx = gw / 10.0 * rng.uniform(0.92, 1.06)
        sy = gh / 14.0 * rng.uniform(0.94, 1.06)
        oy = base_y + rng.randint(-2, 2)
        pts = set()
        for st in strokes:
            path = []
            for (px, py) in st:
                X = (px - 5) * sx
                Y = (py - 7) * sy
                Xr = X * math.cos(ang) - Y * math.sin(ang)
                Yr = X * math.sin(ang) + Y * math.cos(ang)
                path.append((x + gw / 2 + Xr, oy + gh / 2 + Yr))
            # tremurul mâinii: subdivizăm și împingem puțin punctele
            jit = []
            for (a, b) in zip(path, path[1:]):
                L = math.hypot(b[0] - a[0], b[1] - a[1])
                n = max(1, int(L / 3))
                for k in range(n):
                    t = k / n
                    jit.append((a[0] + (b[0] - a[0]) * t + rng.uniform(-0.6, 0.6),
                                a[1] + (b[1] - a[1]) * t + rng.uniform(-0.6, 0.6)))
            jit.append(path[-1])
            for (a, b) in zip(jit, jit[1:]):
                line = pts_line(a[0], a[1], b[0], b[1])
                pts |= dilate(line, 1, diagonal=True)
                if rng.random() < 0.5:
                    pts |= dilate(line, 1, diagonal=False) | set((p[0] + 1, p[1] + 1) for p in line)
        pts = set(p for p in pts if 0 <= p[0] < W and 0 <= p[1] < H)
        # umplutura: os/pergament, jumătatea de jos mai închisă, lumină pe muchia stânga-sus
        ys = [p[1] for p in pts]
        mid = (min(ys) + max(ys)) / 2 + 3
        for p in pts:
            letters.put(*p, PAPER[3] if p[1] < mid else PAPER[2])
        letters.fill(edge(pts, TOP_LEFT), PAPER[4])
        letters.fill(edge(pts, BOTTOM_RIGHT), PAPER[1])
        # zgârieturi: linii fine închise care taie litera
        for _ in range(2):
            p = rng.choice(sorted(pts))
            for k in range(rng.randint(2, 4)):
                q = (p[0] + k, p[1] + k // 2)
                if q in pts:
                    letters.put(*q, PAPER[0])
        if ch in "AKSMNE" and rng.random() < 0.75:
            bottom = max(pts, key=lambda p: (p[1], rng.random()))
            drips_from.append(bottom)
        x += gw + 4
    # conturul gros: 1 px închis + încă 1 px închis-maro (ca o tușă de cerneală)
    letters.outline(OUTLINE, diagonal=True)
    letters.outline(OUTLINE)
    # picături de sânge care curg din litere
    for (dx, dy) in drips_from:
        ln = rng.randint(6, 16)
        wide = rng.random() < 0.6
        for y in range(dy - 1, min(H - 4, dy + ln)):
            letters.put(dx, y, BLOOD[2])
            if wide:
                letters.put(dx + 1, y, BLOOD[1])
            letters.put(dx - 1, y, OUTLINE)
            letters.put(dx + (2 if wide else 1), y, OUTLINE)
        end = min(H - 4, dy + ln)
        drop = set(pts_ellipse(dx + (0.5 if wide else 0), end + 1, 1.6, 1.8))
        for p in drop:
            letters.put(*p, BLOOD[2])
        letters.put(dx, end, BLOOD[3])
        for p in edge(drop, [(1, 0), (-1, 0), (0, 1)]):
            pass
        for q in [(p[0] + dx2, p[1] + dy2) for p in drop for dx2, dy2 in ((1, 0), (-1, 0), (0, 1))]:
            if q not in drop and letters.get(*q)[3] == 0:
                letters.put(*q, OUTLINE)
        letters.put(dx, dy, BLOOD[3])
    # umbra aruncată, ca să stea pe orice fundal
    out = Canvas(W, H)
    sh = letters.a[:, :, 3] > 0
    for y in range(H):
        for x in range(W):
            if sh[y, x] and x + 2 < W and y + 2 < H:
                out.put(x + 2, y + 2, mix("#2a1b13", OUTLINE, 0.5))
    out.blit(letters, 0, 0)
    return out


# ================================================================ cursor

def cursor():
    """Cursor în formă de lacrimă, cu vârful în stânga-sus (acolo e punctul activ: 1,1)."""
    cv = Canvas(16, 16)
    body = set(pts_ellipse(9.5, 9.5, 5.5, 5.5))
    for t in [i / 20 for i in range(21)]:
        # coada lacrimii spre (1,1)
        cx = 1 + (9.5 - 1) * t
        cy = 1 + (9.5 - 1) * t
        r = 5.5 * math.sin(t * math.pi / 2) ** 1.6
        body |= set(pts_ellipse(cx, cy, r, r))
    body = set(p for p in body if 1 <= p[0] < 15 and 1 <= p[1] < 15)
    body.add((1, 1))
    shade_sphere(cv, body, TEAR, 9.5, 9.5, 6.5, 6.5, dither=0.2)
    cv.fill([(6, 7), (6, 8), (7, 6)], "#ffffff")
    cv.put(2, 2, P.TEAR[0])
    cv.outline(OUTLINE)
    cv.put(0, 0, OUTLINE)
    return cv


# ================================================================ harta: celule și legături

def map_cell(fog=False):
    cv = Canvas(16, 16)
    sil = set(pts_rect(0, 0, 15, 15)) - {(0, 0), (15, 0), (0, 15), (15, 15)}
    if fog:
        cv.fill(sil, mix("#c8b89a", INK, 0.55))
        for (x, y) in sil:
            if (x + y) % 3 == 0:
                cv.put(x, y, mix("#c8b89a", INK, 0.7))
    else:
        cv.fill(sil, PAPER[2])
        cv.fill([p for p in sil if p[0] == 1 or p[1] == 1], PAPER[4])
        cv.fill([p for p in sil if p[0] == 14 or p[1] == 14], PAPER[1])
    cv.fill(edge(sil, [(1, 0), (-1, 0), (0, 1), (0, -1)]), INK)
    return cv


def map_link(horizontal):
    w, h = (6, 4) if horizontal else (4, 6)
    cv = Canvas(w, h)
    if horizontal:
        cv.rect(0, 1, w - 1, 2, PAPER[1])
        cv.line(0, 0, w - 1, 0, INK)
        cv.line(0, 3, w - 1, 3, INK)
    else:
        cv.rect(1, 0, 2, h - 1, PAPER[1])
        cv.line(0, 0, 0, h - 1, INK)
        cv.line(3, 0, 3, h - 1, INK)
    return cv


# ================================================================ generare

STATUS_IDS = ["block", "bleed", "poison", "stun", "weak", "mark", "strength", "taunt"]
INTENT_IDS = ["attack", "attack_aoe", "block", "debuff", "buff", "summon", "unknown"]
MAP_IDS = ["unknown", "combat", "treasure", "bedroom", "curio", "boss", "start", "empty", "current"]


def generate():
    out = {}
    for hero in ["isaac", "cain", "lilith", "azazel"]:
        out["card_frame_" + hero] = card_frame(hero)
    out["card_back"] = card_back()
    out["card_art_mask"] = card_art_mask()
    out["card_glow_playable"] = card_glow(P.TEAR[0])
    out["card_glow_selected"] = card_glow(P.STATUS["stun"])
    out["card_glow"] = card_glow("#ffffff")
    for r in ["common", "uncommon", "rare"]:
        out["gem_" + r] = gem(r)
    out["cost_orb"] = cost_orb()
    out["rank_pip_on"] = rank_pip(True)
    out["rank_pip_off"] = rank_pip(False)
    for s in STATUS_IDS:
        out["status_" + s] = icon("status_" + s)
    for s in INTENT_IDS:
        out["intent_" + s] = icon("intent_" + s)
    out["energy_full"] = icon("energy_full")
    out["energy_empty"] = icon("energy_empty")
    for s in MAP_IDS:
        out["map_" + s] = icon("map_" + s)
    out["map_cell"] = map_cell(False)
    out["map_cell_fog"] = map_cell(True)
    out["map_link_h"] = map_link(True)
    out["map_link_v"] = map_link(False)
    out["panel_paper"] = panel_paper()
    out["panel_dark"] = panel_dark()
    out["tooltip"] = tooltip_panel()
    for st in ["normal", "hover", "pressed"]:
        out["button_" + st] = button(st)
    out["portrait_frame"] = portrait_frame(False)
    out["portrait_frame_active"] = portrait_frame(True)
    out.update(hp_parts())
    out["title_logo"] = logo()
    out["cursor_tear"] = cursor()
    return out


def save_all(ui):
    for name, cv in ui.items():
        cv.save(os.path.join(UI_DIR, name + ".png"))
    data = {
        "version": 1,
        "note": "9-slice borders in px (left, right, top, bottom). drawMode: sliced = stretch centre/edges; "
                "tiled = repeat centre/edges (panel_paper has a 24 px period).",
        "slices": [SLICES[k] for k in sorted(SLICES)],
        "cursor": {"sprite": "cursor_tear", "hotspotX": 1, "hotspotY": 1},
        "card": {"size": [CARD_W, CARD_H], "artRect": [ART[0], ART[1], ART[2] - ART[0] + 1, ART[3] - ART[1] + 1],
                 "nameRect": [PLATE[0], PLATE[1], PLATE[2] - PLATE[0] + 1, PLATE[3] - PLATE[1] + 1],
                 "textRect": [TEXT[0], TEXT[1], TEXT[2] - TEXT[0] + 1, TEXT[3] - TEXT[1] + 1],
                 "gemPos": list(GEM_AT), "costOrbPos": [-5, -5], "glowOffset": [-4, -4],
                 "coordinates": "px from the card's top-left corner"},
    }
    with open(os.path.join(UI_DIR, "ui_slices.json"), "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    return data


# ================================================================ desenarea în mock (9-slice + text)

def nine(src, w, h, name):
    """Desenează un sprite 9-slice la mărimea (w,h), exact ca Unity (Sliced sau Tiled)."""
    s = SLICES[name]
    l, r, t, b = s["left"], s["right"], s["top"], s["bottom"]
    im = src.image() if isinstance(src, Canvas) else src
    W, H = im.size
    out = Image.new("RGBA", (w, h))
    xs = [(0, l, 0, l), (l, W - r, l, w - r), (W - r, W, w - r, w)]
    ys = [(0, t, 0, t), (t, H - b, t, h - b), (H - b, H, h - b, h)]
    for (sx0, sx1, dx0, dx1) in xs:
        for (sy0, sy1, dy0, dy1) in ys:
            if sx1 <= sx0 or sy1 <= sy0 or dx1 <= dx0 or dy1 <= dy0:
                continue
            piece = im.crop((sx0, sy0, sx1, sy1))
            if s["drawMode"] == "tiled":
                tile = Image.new("RGBA", (dx1 - dx0, dy1 - dy0))
                for yy in range(0, dy1 - dy0, piece.height):
                    for xx in range(0, dx1 - dx0, piece.width):
                        tile.paste(piece, (xx, yy))
                out.paste(tile, (dx0, dy0))
            else:
                out.paste(piece.resize((dx1 - dx0, dy1 - dy0), Image.NEAREST), (dx0, dy0))
    return out


def card_image(ui, hero, name, cost, rarity, text, art_seed=0):
    """O carte completă pentru mock: ilustrație-placeholder + ramă + nume + text + cost + bijuterie."""
    im = Image.new("RGBA", (CARD_W + 10, CARD_H + 10))
    ox, oy = 5, 5
    # ilustrația: un placeholder desenat (gradient în trepte + o siluetă), stă sub ramă
    art = Image.new("RGBA", (56, 36))
    rng = random.Random(art_seed)
    hr = HEROES[hero]["ramp"]
    for y in range(36):
        for x in range(56):
            band = (y + (x // 7) % 2) // 9
            art.putpixel((x, y), C([hr[0], hr[1], hr[0], OUTLINE][min(3, band)]))
    cvart = Canvas(56, 36)
    cvart.a[:] = __import__("numpy").array(art)
    sil = set(pts_ellipse(28, 20, 9, 8)) | set(pts_rect(22, 24, 34, 35))
    cvart.fill(sil, mix(hr[2], OUTLINE, 0.2))
    cvart.fill(edge(sil, TOP_LEFT), hr[3])
    for _ in range(10):
        cvart.put(rng.randrange(56), rng.randrange(20), hr[3])
    im.alpha_composite(cvart.image(), (ox + ART[0], oy + ART[1]))
    im.alpha_composite(ui["card_frame_" + hero].image(), (ox, oy))
    # numele: Micro 5, pergament pe plăcuța închisă, centrat
    f = font("micro")
    draw_text(im, (ox + 32, oy + PLATE[1] + 2), name, f, "#e8dcc4", anchor="mt")
    # descrierea: Micro 5 cu cerneală, rânduri de 7 px
    lines = wrap_text(text, f, TEXT[2] - TEXT[0] - 3)
    y = oy + TEXT[1] + 3
    for ln in lines[:4]:
        draw_text(im, (ox + 32, y), ln, f, INK, anchor="mt")
        y += 7
    im.alpha_composite(ui["gem_" + rarity].image(), (ox + GEM_AT[0], oy + GEM_AT[1]))
    im.alpha_composite(ui["cost_orb"].image(), (ox - 5, oy - 5))
    draw_text(im, (ox + 3, oy - 1), str(cost), font("silkb"), "#ffffff", outline=OUTLINE, anchor="mt")
    return im


def mock(ui):
    """Cadru 640×360 (×3 = 1920×1080) cu mâna de 3 cărți, energie, bara de ture, tooltip și harta."""
    import numpy as np
    W, H = 640, 360
    env = os.path.join(os.path.dirname(UI_DIR), "..", "Textures", "Env", "basement")
    wall = Image.open(os.path.join(env, "wall.png"))
    floor = Image.open(os.path.join(env, "floor.png"))
    bg = Image.new("RGBA", (W, H))
    for y in range(0, 230, 64):
        for x in range(0, W, 64):
            bg.paste(wall, (x, y - 26))
    for y in range(230, H, 64):
        for x in range(0, W, 64):
            bg.paste(floor, (x, y))
    # întunecare în trepte + vinietă (ca post-procesarea din joc), fără gradient neted
    a = np.array(bg).astype(float)
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H * 0.45) / (H / 2)) ** 2)
    k = np.where(d < 0.6, 0.62, np.where(d < 0.85, 0.5, np.where(d < 1.05, 0.38, 0.26)))
    a[:, :, :3] *= k[:, :, None]
    bg = Image.fromarray(a.astype(np.uint8), "RGBA")
    props = os.path.join(os.path.dirname(UI_DIR), "Props")
    for (n, x, by) in [("torch_0", 120, 150), ("torch_2", 520, 150), ("poop_0", 70, 300), ("rock_1", 586, 296),
                       ("blood_decal_1", 330, 300)]:
        p = Image.open(os.path.join(props, n + ".png"))
        bg.alpha_composite(p, (x - p.width // 2, by - p.height))

    sm = font("silk")
    smb = font("silkb")
    mic = font("micro")

    # --- combatanți: placeholder (umbră + bară de viață + statusuri/intenții), eroii sunt desenați de alt agent
    heroes = [(70, "ISAAC", 18, 24, ["block", "bleed"]), (130, "CAIN", 22, 30, ["strength"]),
              (190, "LILITH", 9, 26, ["weak", "poison"]), (250, "AZAZEL", 30, 30, [])]
    enemies = [(395, "GAPER", 14, 20, "attack", "7"), (455, "POOTER", 8, 8, "attack_aoe", "3x2"),
               (515, "HORF", 12, 18, "debuff", ""), (575, "FLY", 4, 4, "summon", "")]
    for (cx, nm, hp, mx, sts) in heroes:
        shadow = Image.new("RGBA", (40, 6))
        for p in pts_ellipse(19.5, 3, 18, 2.5):
            if 0 <= p[0] < 40 and 0 <= p[1] < 6:
                shadow.putpixel(p, (10, 6, 6, 150))
        bg.alpha_composite(shadow, (cx - 20, 246))
        hpbar(bg, ui, cx - 20, 252, 40, hp, mx, block=("block" in sts))
        draw_text(bg, (cx, 261), "%d/%d" % (hp, mx), sm, "#e8dcc4", outline=OUTLINE, anchor="mt")
        for i, s in enumerate(sts):
            bg.alpha_composite(ui["status_" + s].image(), (cx - 20 + i * 14, 269))
            draw_text(bg, (cx - 20 + i * 14 + 9, 276), "2", sm, "#ffffff", outline=OUTLINE)
        draw_text(bg, (cx, 236), nm, sm, "#c8b89a", outline=OUTLINE, anchor="mt")
    for (cx, nm, hp, mx, intent, num) in enemies:
        shadow = Image.new("RGBA", (40, 6))
        for p in pts_ellipse(19.5, 3, 18, 2.5):
            if 0 <= p[0] < 40 and 0 <= p[1] < 6:
                shadow.putpixel(p, (10, 6, 6, 150))
        bg.alpha_composite(shadow, (cx - 20, 246))
        hpbar(bg, ui, cx - 20, 252, 40, hp, mx)
        draw_text(bg, (cx, 261), "%d/%d" % (hp, mx), sm, "#e8dcc4", outline=OUTLINE, anchor="mt")
        bg.alpha_composite(upscale(ui["intent_" + intent].image(), 1), (cx - 6 - (6 if num else 0), 196))
        if num:
            draw_text(bg, (cx + 2, 199), num, smb, "#ffffff", outline=OUTLINE)
        draw_text(bg, (cx, 236), nm, sm, "#c8b89a", outline=OUTLINE, anchor="mt")

    # --- bara de ture: 8 rame goale pe o placă închisă
    n, fw, gap = 8, 28, 3
    tw = n * fw + (n - 1) * gap
    tx = (W - tw) // 2
    bg.alpha_composite(nine(ui["panel_dark"], tw + 16, 48, "panel_dark"), (tx - 8, 4))
    for i in range(n):
        fr = ui["portrait_frame_active" if i == 0 else "portrait_frame"].image()
        bg.alpha_composite(fr, (tx + i * (fw + gap), 12))
    draw_text(bg, (tx - 14, 10), "ROUND 1", smb, "#e0b04a", outline=OUTLINE, anchor="rt")

    # --- harta: panou de pergament cu celule și iconițe
    mw, mh = 150, 120
    mx0, my0 = W - mw - 6, 6
    bg.alpha_composite(nine(ui["panel_paper"], mw, mh, "panel_paper"), (mx0, my0))
    draw_text(bg, (mx0 + mw // 2, my0 + 9), "BASEMENT I", smb, INK, anchor="mt")
    grid = {(0, 1): "start", (1, 1): "combat", (2, 1): "current", (2, 0): "curio", (3, 1): "unknown",
            (2, 2): "combat", (1, 2): "bedroom", (3, 2): "empty", (4, 2): "boss", (0, 2): "treasure",
            (3, 0): "unknown"}
    fog = {(3, 1), (3, 0), (4, 2)}
    links = [((0, 1), (1, 1)), ((1, 1), (2, 1)), ((2, 1), (2, 0)), ((2, 1), (3, 1)), ((2, 1), (2, 2)),
             ((2, 2), (1, 2)), ((2, 2), (3, 2)), ((3, 2), (4, 2)), ((1, 2), (0, 2)), ((2, 0), (3, 0))]
    gx0, gy0, cs, ls = mx0 + 14, my0 + 24, 16, 6
    pos = lambda c: (gx0 + c[0] * (cs + ls), gy0 + c[1] * (cs + ls))
    for (a_, b_) in links:
        pa, pb = pos(a_), pos(b_)
        if a_[1] == b_[1]:
            bg.alpha_composite(ui["map_link_h"].image(), (min(pa[0], pb[0]) + cs, pa[1] + 6))
        else:
            bg.alpha_composite(ui["map_link_v"].image(), (pa[0] + 6, min(pa[1], pb[1]) + cs))
    for c, kind in grid.items():
        px, py = pos(c)
        bg.alpha_composite(ui["map_cell_fog" if c in fog and kind != "boss" else "map_cell"].image(), (px, py))
        if kind:
            bg.alpha_composite(ui["map_" + kind].image(), (px + 2, py + 2))

    # --- mâna: 3 cărți, cea din mijloc ridicată și selectată
    cards = [("isaac", "Tear Shot", 1, "common", "Deal 6 damage. Rank 1-3."),
             ("azazel", "Brimstone", 2, "rare", "Deal 12 damage to ALL enemies. Apply 1 Mark."),
             ("lilith", "Incubus Lash", 1, "uncommon", "Deal 4 damage twice. Apply 2 Weak.")]
    cx0 = (W - (3 * 64 + 2 * 8)) // 2
    for i, (hero, nm, cost, rar, txt) in enumerate(cards):
        x = cx0 + i * 72
        y = H - 90 - 8 - (14 if i == 1 else 0)
        glow = ui["card_glow_selected" if i == 1 else "card_glow_playable"].image()
        bg.alpha_composite(glow, (x - 4, y - 4))
        bg.alpha_composite(card_image(ui, hero, nm, cost, rar, txt, art_seed=i), (x - 5, y - 5))
    # pachetele: draw / discard
    bg.alpha_composite(ui["card_back"].image().resize((32, 45), Image.NEAREST), (100, H - 54))
    draw_text(bg, (116, H - 9), "DRAW 7", sm, "#e8dcc4", outline=OUTLINE, anchor="mt")

    # --- energia: lacrimi pe o placă
    bg.alpha_composite(nine(ui["panel_dark"], 74, 34, "panel_dark"), (10, H - 44))
    draw_text(bg, (47, H - 39), "TEARS 1/2", sm, "#e8dcc4", outline=OUTLINE, anchor="mt")
    bg.alpha_composite(ui["energy_full"].image(), (33, H - 28))
    bg.alpha_composite(ui["energy_empty"].image(), (49, H - 28))

    # --- tooltip de pergament lângă cartea selectată
    tw_, th_ = 128, 44
    tx_, ty_ = cx0 + 72 + 64 + 14, H - 90 - 8 - 14 - th_ + 4
    bg.alpha_composite(nine(ui["tooltip"], tw_, th_, "tooltip"), (tx_, ty_))
    bg.alpha_composite(ui["status_mark"].image(), (tx_ + 6, ty_ + 5))
    draw_text(bg, (tx_ + 21, ty_ + 7), "MARK", smb, INK)
    yy0 = ty_ + 20
    for ln in wrap_text("Takes 50% more damage from every hit.", mic, tw_ - 14):
        draw_text(bg, (tx_ + 7, yy0), ln, mic, INK)
        yy0 += 8

    # --- butonul End Turn
    bx, by, bw, bh = W - 82, H - 30, 72, 22
    bg.alpha_composite(nine(ui["button_hover"], bw, bh, "button_hover"), (bx, by))
    draw_text(bg, (bx + bw // 2, by + 6), "END TURN", smb, INK, anchor="mt")
    # indiciul de tutorial (fără text lung)
    draw_text(bg, (W // 2, 160), "Drag a card onto an enemy", mic, "#e8dcc4", outline=OUTLINE, anchor="mt")
    return bg


def hpbar(img, ui, x, y, w, hp, mx, block=False):
    img.alpha_composite(nine(ui["hpbar_frame"], w, 7, "hpbar_frame"), (x, y))
    fw = max(0, int((w - 4) * hp / mx))
    if fw:
        img.alpha_composite(nine(ui["hpbar_fill"], fw, 4, "hpbar_fill").crop((0, 0, fw, 4)) if fw >= 2 else
                            ui["hpbar_fill"].image().crop((0, 0, fw, 4)), (x + 2, y + 2))
    if block:
        img.alpha_composite(nine(ui["hpbar_block"], w, 7, "hpbar_block"), (x, y))
        img.alpha_composite(ui["status_block"].image(), (x - 10, y - 3))


# ================================================================ planșa

def review(ui):
    k = 3
    sheet = Sheet("Darkest Basement - UI kit (x3)")
    ck = lambda n, kk=k: (on_checker(ui[n].image(), kk), n)
    sheet.row([ck("card_frame_isaac"), ck("card_frame_cain"), ck("card_frame_lilith"), ck("card_frame_azazel"),
               ck("card_back")], heading="cards 64x90")
    sheet.row([ck("card_glow_playable"), ck("card_glow_selected"), ck("card_art_mask"), ck("cost_orb", 4),
               ck("gem_common", 4), ck("gem_uncommon", 4), ck("gem_rare", 4), ck("rank_pip_on", 4), ck("rank_pip_off", 4)])
    sheet.row([ck("status_" + s, 4) for s in STATUS_IDS], heading="status icons 12x12 (x4)")
    sheet.row([ck("intent_" + s, 4) for s in INTENT_IDS] + [ck("energy_full", 4), ck("energy_empty", 4)],
              heading="intents + energy (x4)")
    sheet.row([ck("map_" + s, 4) for s in MAP_IDS], heading="map icons (x4)")
    sheet.row([ck("map_cell", 4), ck("map_cell_fog", 4), ck("map_link_h", 4), ck("map_link_v", 4),
               ck("panel_paper"), ck("panel_dark"), ck("tooltip"), ck("button_normal"), ck("button_hover"),
               ck("button_pressed")], heading="map cells, 9-slice panels, buttons")
    # panouri întinse la mărimi reale (verificăm 9-slice-ul)
    stretched = Image.new("RGBA", (560, 120), C("#16100e"))
    stretched.alpha_composite(nine(ui["panel_paper"], 200, 110, "panel_paper"), (0, 5))
    stretched.alpha_composite(nine(ui["panel_dark"], 150, 60, "panel_dark"), (210, 5))
    stretched.alpha_composite(nine(ui["tooltip"], 130, 40, "tooltip"), (210, 75))
    stretched.alpha_composite(nine(ui["button_normal"], 80, 22, "button_normal"), (370, 5))
    stretched.alpha_composite(nine(ui["button_hover"], 80, 22, "button_hover"), (370, 35))
    stretched.alpha_composite(nine(ui["button_pressed"], 80, 22, "button_pressed"), (370, 65))
    hp = Image.new("RGBA", (100, 40))
    hpbar(hp, ui, 12, 4, 60, 18, 24)
    hpbar(hp, ui, 12, 20, 60, 7, 24, block=True)
    stretched.alpha_composite(hp, (460, 5))
    sheet.row([(upscale(stretched, 2), "9-slice stretched (x2): paper 200x110, dark, tooltip, buttons, hp bars")])
    sheet.row([ck("portrait_frame"), ck("portrait_frame_active"), ck("hpbar_frame", 4), ck("hpbar_fill", 4),
               ck("hpbar_fill_lag", 4), ck("hpbar_block", 4), ck("cursor_tear", 4)], heading="turn order, hp bar, cursor")
    sheet.row([(on_checker(ui["title_logo"].image(), 3), "title_logo 300x60 (x3)")], heading="title")
    im = sheet.render()
    im.save(os.path.join(REVIEW_DIR, "ui.png"))
    return im


if __name__ == "__main__":
    ui = generate()
    data = save_all(ui)
    review(ui)
    m = mock(ui)
    upscale(m, 3).save(os.path.join(REVIEW_DIR, "card-ui-mock.png"))
    print("ok", len(ui), "sprites,", len(data["slices"]), "slices")
