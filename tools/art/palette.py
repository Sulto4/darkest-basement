"""Paleta comună a jocului (din docs/ART.md). Toate generatoarele de artă o importă."""

def hex_to_rgba(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

OUTLINE = "#1b0f0f"

SKIN = ["#fff0e8", "#f2d3c4", "#dcae9c", "#b9857a"]          # highlight, bază, umbră, umbră adâncă
TEAR = ["#d6ecff", "#7fb2e5", "#4a7fbf"]
BLOOD = ["#d33c3c", "#9e1b1b", "#5c0d0d"]
POOP = ["#a8743d", "#8a5a2b", "#6b4423", "#4a2e17"]

BASEMENT = ["#6e5241", "#5e4232", "#4a3326", "#3a271c", "#2a1b13"]
CAVES = ["#7a7f86", "#5c6168", "#43474d", "#2f3236"]
CAVES_WATER = "#3e6f7a"
CAVES_SHROOM = "#c9a227"
DEPTHS = ["#4a3a40", "#3d2b33", "#2a1f24", "#1a1316"]
DEPTHS_FLESH = "#7a2a33"

AZAZEL = {"black": ["#231c22", "#151015"], "eye": "#e03a3a", "horn": "#4b3b44"}
LILITH = {"hair": ["#3a2c45", "#241c2b"], "dress": "#6b3f7d", "skin": ["#e9cbbf", "#cfa596"],
          "incubus": "#2a2030", "incubus_eye": "#ff5a5a"}
CAIN = {"hair": ["#6b4423", "#4a2e17"], "clothes": ["#5b6b3a", "#43502a"], "patch": "#2a1b13"}

UI = {"parchment": ["#e8dcc4", "#c8b89a"], "ink": "#2a1b13", "gold": ["#e0b04a", "#a8782a"]}

STATUS = {"block": "#6fa8dc", "bleed": "#d33c3c", "poison": "#7cc04a", "stun": "#f2d14a",
          "weak": "#9a8fb0", "mark": "#ff8a3d", "strength": "#e05050", "taunt": "#c0c0c0"}

HERO_FRAME = {"azazel": ["#5c0d0d", "#231c22"], "cain": ["#5b6b3a", "#6b4423"],
              "isaac": ["#7fb2e5", "#e8dcc4"], "lilith": ["#6b3f7d", "#241c2b"]}
RARITY = {"common": "#9a9a9a", "uncommon": "#4a7fbf", "rare": "#e0b04a"}
