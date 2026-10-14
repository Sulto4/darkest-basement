"""pixkit — trusa comună de pixel art pentru Darkest Basement.

Toate generatoarele din tools/art/ o importă. Ideea de bază:
  1. desenăm forme simple (elipse, poligoane, bloburi) ca *măști* = mulțimi de pixeli (x, y);
  2. le colorăm cu umbrire „lumină din stânga-sus” (3 tonuri + highlight);
  3. punem detaliile pixel cu pixel (stamp() cu artă ASCII);
  4. fiecare strat își primește conturul de 1 px, apoi straturile se suprapun.

Coordonate: (0, 0) e colțul stânga-sus, x crește spre dreapta, y în jos (ca în Pillow).
Culorile sunt mereu șiruri hex „#rrggbb” (sau „#rrggbbaa” pentru alfa), None = transparent.
Totul e determinist: fără random nemarcat, deci rulări repetate dau aceiași octeți.
"""
import math
import os

from PIL import Image, ImageDraw, ImageFont

from palette import OUTLINE

# Matricea Bayer 4×4: praguri 0..15 pentru dither ordonat (model fix, nu zgomot).
BAYER4 = [[0, 8, 2, 10],
          [12, 4, 14, 6],
          [3, 11, 1, 9],
          [15, 7, 13, 5]]

N4 = [(1, 0), (-1, 0), (0, 1), (0, -1)]                      # vecinii pe laturi
N8 = N4 + [(1, 1), (1, -1), (-1, 1), (-1, -1)]               # + vecinii pe diagonală


# ───────────────────────────── culori ─────────────────────────────

def norm(color):
    """Aduce o culoare la forma canonică „#rrggbb[aa]” cu litere mici (None rămâne None)."""
    if color is None:
        return None
    c = color.lower()
    if len(c) == 9 and c.endswith("ff"):
        c = c[:7]                     # alfa 255 e implicit, nu-l mai scriem
    return c


def rgba(color):
    """„#rrggbb[aa]” → (r, g, b, a)."""
    h = color.lstrip("#")
    a = int(h[6:8], 16) if len(h) == 8 else 255
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def to_hex(r, g, b, a=255):
    s = "#%02x%02x%02x" % (r, g, b)
    return s if a == 255 else s + "%02x" % a


def tint(color, k):
    """Nuanță derivată: k < 1 întunecă, k > 1 deschide (spre alb). Pentru culori „în plus” din aceeași familie."""
    r, g, b, a = rgba(color)
    if k <= 1:
        f = lambda v: int(round(v * k))
    else:
        t = k - 1                     # cât ne apropiem de alb
        f = lambda v: int(round(v + (255 - v) * t))
    return to_hex(f(r), f(g), f(b), a)


def mix(c1, c2, t):
    """Amestec liniar între două culori (t=0 → c1, t=1 → c2)."""
    a, b = rgba(c1), rgba(c2)
    return to_hex(*[int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4)])


def with_alpha(color, a):
    r, g, b, _ = rgba(color)
    return to_hex(r, g, b, a)


# ───────────────────────────── pânza ─────────────────────────────

class Canvas:
    """Pânză cu paletă indexată: fiecare pixel ține un indice în self.pal (0 = transparent).

    Paleta crește singură când folosești o culoare nouă. Așa un palette-swap e doar o
    re-mapare de indici, iar numărul de culori al unui sprite se vede direct: len(pal) - 1.
    """

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.pal = [None]
        self._lookup = {None: 0}
        self.px = [[0] * w for _ in range(h)]

    # -- acces la pixeli --
    def idx(self, color):
        color = norm(color)
        if color not in self._lookup:
            self._lookup[color] = len(self.pal)
            self.pal.append(color)
        return self._lookup[color]

    def inside(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def get(self, x, y):
        return self.pal[self.px[y][x]] if self.inside(x, y) else None

    def set(self, x, y, color):
        if self.inside(x, y):
            self.px[y][x] = self.idx(color)

    def opaque(self, x, y):
        return self.inside(x, y) and self.px[y][x] != 0

    def fill(self, pts, color):
        """Colorează toți pixelii dintr-o mască (orice iterabil de (x, y))."""
        i = self.idx(color)
        for x, y in pts:
            if self.inside(x, y):
                self.px[y][x] = i

    def recolor(self, pts, color, only=None):
        """Ca fill, dar atinge doar pixelii deja opaci (și, opțional, doar cei de culoarea `only`)."""
        i = self.idx(color)
        only_i = None if only is None else self._lookup.get(norm(only), -1)
        for x, y in pts:
            if self.opaque(x, y) and (only_i is None or self.px[y][x] == only_i):
                self.px[y][x] = i

    def erase(self, pts):
        for x, y in pts:
            if self.inside(x, y):
                self.px[y][x] = 0

    def mask(self, color="any"):
        """Masca pixelilor opaci ("any") sau a celor de o anumită culoare."""
        if color == "any":
            return {(x, y) for y in range(self.h) for x in range(self.w) if self.px[y][x]}
        i = self._lookup.get(norm(color))
        if i is None:
            return set()
        return {(x, y) for y in range(self.h) for x in range(self.w) if self.px[y][x] == i}

    def copy(self):
        c = Canvas(self.w, self.h)
        c.pal, c._lookup = list(self.pal), dict(self._lookup)
        c.px = [row[:] for row in self.px]
        return c

    def paste(self, src, dx=0, dy=0):
        """Suprapune `src` peste pânză (pixelii transparenți din src nu acoperă nimic)."""
        for y in range(src.h):
            for x in range(src.w):
                i = src.px[y][x]
                if i:
                    self.set(x + dx, y + dy, src.pal[i])
        return self

    def colors(self):
        """Culorile folosite efectiv (fără transparent)."""
        used = {i for row in self.px for i in row if i}
        return sorted(self.pal[i] for i in used)

    # -- forme desenate direct (scurtături peste funcțiile de mască) --
    def ellipse(self, x0, y0, x1, y1, color, ring_color=None):
        m = ellipse(x0, y0, x1, y1)
        self.fill(m, color)
        if ring_color:
            self.fill(inner_edge(m), ring_color)
        return m

    def rect(self, x0, y0, x1, y1, color, ring_color=None):
        m = rect(x0, y0, x1, y1)
        self.fill(m, color)
        if ring_color:
            self.fill(inner_edge(m), ring_color)
        return m

    def line(self, x0, y0, x1, y1, color):
        pts = line(x0, y0, x1, y1)
        self.fill(pts, color)
        return pts

    def polygon(self, points, color):
        m = polygon(points)
        self.fill(m, color)
        return m

    def blob(self, circles, color, threshold=1.0):
        m = blob(circles, threshold)
        self.fill(m, color)
        return m

    # -- export --
    def to_image(self, scale=1):
        img = Image.new("RGBA", (self.w, self.h))
        table = [(0, 0, 0, 0)] + [rgba(c) for c in self.pal[1:]]
        img.putdata([table[i] for row in self.px for i in row])
        if scale != 1:
            img = img.resize((self.w * scale, self.h * scale), Image.NEAREST)
        return img


# ───────────────────────────── măști (forme) ─────────────────────────────
# Toate întorc un set de (x, y). Testăm centrul pixelului (x+0.5, y+0.5), deci formele
# sunt simetrice și fără anti-aliasing: un pixel e fie înăuntru, fie afară.

def ellipse(x0, y0, x1, y1, fat=0.0):
    """Elipsă plină în cutia [x0..x1] × [y0..y1] (inclusiv). `fat` > 0 o face mai „pătrățoasă”."""
    cx, cy = (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2
    rx, ry = (x1 - x0 + 1) / 2, (y1 - y0 + 1) / 2
    pts = set()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            u, v = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            if u * u + v * v <= 1.0 + fat:
                pts.add((x, y))
    return pts


def rect(x0, y0, x1, y1):
    return {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)}


def line(x0, y0, x1, y1):
    """Bresenham: linie de 1 px fără găuri și fără pixeli dubli pe diagonală."""
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


def polyline(points):
    pts = []
    for (a, b), (c, d) in zip(points, points[1:]):
        pts += line(a, b, c, d)
    return pts


def polygon(points):
    """Poligon plin (regula par-impar pe centrele pixelilor). points = [(x, y), ...] în coordonate reale."""
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    pts = set()
    n = len(points)
    for y in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
        for x in range(int(math.floor(min(xs))), int(math.ceil(max(xs))) + 1):
            px, py = x + 0.5, y + 0.5
            inside = False
            j = n - 1
            for i in range(n):
                xi, yi = points[i]
                xj, yj = points[j]
                if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi:
                    inside = not inside
                j = i
            if inside:
                pts.add((x, y))
    return pts


def blob(circles, threshold=1.0):
    """Metaball: suma câmpurilor r²/d² ≥ prag. circles = [(cx, cy, r), ...]. Bun pentru forme organice."""
    xs0 = int(min(c[0] - c[2] * 2 for c in circles))
    xs1 = int(max(c[0] + c[2] * 2 for c in circles)) + 1
    ys0 = int(min(c[1] - c[2] * 2 for c in circles))
    ys1 = int(max(c[1] + c[2] * 2 for c in circles)) + 1
    pts = set()
    for y in range(ys0, ys1 + 1):
        for x in range(xs0, xs1 + 1):
            s = 0.0
            for cx, cy, r in circles:
                d2 = (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2
                s += r * r / max(d2, 1e-6)
            if s >= threshold:
                pts.add((x, y))
    return pts


def shift(mask, dx, dy):
    return {(x + dx, y + dy) for x, y in mask}


def inner_edge(mask, diagonal=False):
    """Pixelii din mască care ating exteriorul (marginea interioară, grosime 1)."""
    nb = N8 if diagonal else N4
    return {(x, y) for x, y in mask if any((x + a, y + b) not in mask for a, b in nb)}


def outer_edge(mask, diagonal=False):
    """Pixelii din afara măștii care o ating (inelul exterior, grosime 1)."""
    nb = N8 if diagonal else N4
    out = set()
    for x, y in mask:
        for a, b in nb:
            if (x + a, y + b) not in mask:
                out.add((x + a, y + b))
    return out


def edge_band(mask, dx, dy):
    """Banda de pixeli care „ies” din mască dacă îi muți cu (dx, dy).

    Cu (dx, dy) = (2, 2) primești o semilună pe marginea din dreapta-jos: exact umbra
    unui obiect luminat din stânga-sus. Cu (-1, -1) primești marginea luminată.
    """
    return {(x, y) for x, y in mask if (x + dx, y + dy) not in mask}


def bbox(mask):
    xs, ys = [p[0] for p in mask], [p[1] for p in mask]
    return min(xs), min(ys), max(xs), max(ys)


# ───────────────────────────── contur ─────────────────────────────

def outline(c, color=OUTLINE, diagonal=False):
    """Contur automat de 1 px: orice pixel transparent lipit de unul opac devine `color`.

    diagonal=False (vecini pe laturi) dă contururi rotunde și subțiri, ca în Isaac;
    diagonal=True le îngroașă la colțuri.
    """
    solid = c.mask()
    ring = {p for p in outer_edge(solid, diagonal) if c.inside(*p)}
    c.fill(ring, color)
    return ring


def clean_orphans(c, color=OUTLINE):
    """Șterge pixelii de contur rămași izolați (fără niciun vecin colorat altfel decât conturul)."""
    i = c._lookup.get(norm(color))
    if i is None:
        return
    for y in range(c.h):
        for x in range(c.w):
            if c.px[y][x] == i:
                if not any(c.opaque(x + a, y + b) and c.px[y + b][x + a] != i for a, b in N8):
                    c.px[y][x] = 0


# ───────────────────────────── umbrire ─────────────────────────────
# Rampă = [highlight, bază, umbră, umbră adâncă]. Lumina vine din stânga-sus.

LIGHT = (-0.55, -0.72, 0.42)          # vectorul spre sursa de lumină (x, y, z spre privitor)


def dither_on(x, y, level):
    """True dacă pixelul (x, y) e „aprins” într-un dither ordonat de nivel level ∈ [0, 1]."""
    return BAYER4[y % 4][x % 4] < level * 16


def dither_fill(c, mask, color, level):
    """Colorează doar o fracție `level` din mască, după modelul Bayer (fără zgomot)."""
    c.fill({p for p in mask if dither_on(p[0], p[1], level)}, color)


def shade_sphere(c, mask, ramp, cuts=(0.93, 0.42, 0.12), light=LIGHT, box=None, dither=0.0):
    """Umbrește masca ca pe o sferă (normala din elipsa ei) și cuantizează în 4 tonuri.

    intensitate = n · L, unde n e normala sferei în pixelul respectiv.
    cuts = pragurile highlight / bază / umbră; sub ultimul prag → umbra adâncă.
    dither > 0 amestecă puțin pragurile cu matricea Bayer (tranziții în tablă de șah).
    """
    x0, y0, x1, y1 = box or bbox(mask)
    cx, cy = (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2
    rx, ry = (x1 - x0 + 1) / 2, (y1 - y0 + 1) / 2
    lx, ly, lz = light
    ln = math.sqrt(lx * lx + ly * ly + lz * lz)
    lx, ly, lz = lx / ln, ly / ln, lz / ln
    for x, y in mask:
        u, v = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
        z = math.sqrt(max(0.0, 1.0 - u * u - v * v))
        i = u * lx + v * ly + z * lz
        if dither:
            i += (BAYER4[y % 4][x % 4] / 15.0 - 0.5) * dither
        if i > cuts[0]:
            col = ramp[0]
        elif i > cuts[1]:
            col = ramp[1]
        elif i > cuts[2]:
            col = ramp[2]
        else:
            col = ramp[3]
        c.set(x, y, col)


def shade_flat(c, mask, ramp, shadow=(2, 2), deep=(1, 1), light_rim=None):
    """Umbrire simplă pentru piese mici: bază + semilună de umbră + margine adâncă.

    shadow / deep = deplasările pentru edge_band (dreapta-jos); light_rim = (-1, -1) adaugă
    highlight pe marginea din stânga-sus.
    """
    c.fill(mask, ramp[1])
    if shadow:
        c.fill(edge_band(mask, *shadow), ramp[2])
    if deep:
        c.fill(edge_band(mask, *deep) & edge_band(mask, 0, 1), ramp[3])
    if light_rim:
        c.fill(edge_band(mask, *light_rim) - edge_band(mask, *shadow if shadow else (1, 1)), ramp[0])


# ───────────────────────────── pixeli puși de mână ─────────────────────────────

def stamp(c, x, y, rows, key, flip=False):
    """Pune pixeli din artă ASCII. rows = listă de șiruri; key = {caracter: culoare}.

    '.' și ' ' înseamnă „nu atinge”; o cheie cu valoarea None șterge pixelul (transparent).
    flip=True oglindește stampila pe orizontală.
    """
    for j, row in enumerate(rows):
        if flip:
            row = row[::-1]
        for i, ch in enumerate(row):
            if ch in ".  ":
                continue
            if ch not in key:
                raise KeyError("caracter necunoscut în stampilă: %r" % ch)
            col = key[ch]
            if col is None:
                c.erase([(x + i, y + j)])
            else:
                c.set(x + i, y + j, col)


# ───────────────────────────── transformări de cadru ─────────────────────────────
# Toate întorc o pânză NOUĂ; originalul rămâne neatins.

def shifted(c, dx, dy):
    out = Canvas(c.w, c.h)
    out.paste(c, dx, dy)
    return out


def mirrored(c):
    out = c.copy()
    out.px = [row[::-1] for row in c.px]
    return out


def flipped_v(c):
    out = c.copy()
    out.px = [row[:] for row in c.px[::-1]]
    return out


def squash_rows(c, rows):
    """Scoate rândurile date; ce e deasupra coboară (picioarele rămân pe loc). Sprite mai scund."""
    keep = [r for r in range(c.h) if r not in set(rows)]
    out = c.copy()
    blank = [0] * c.w
    out.px = [blank[:] for _ in range(c.h - len(keep))] + [c.px[r][:] for r in keep]
    return out


def stretch_rows(c, rows):
    """Dublează rândurile date; ce e deasupra urcă (picioarele rămân pe loc). Sprite mai înalt."""
    seq = []
    for r in range(c.h):
        seq.append(r)
        if r in set(rows):
            seq.append(r)
    seq = seq[len(seq) - c.h:]                   # păstrăm partea de jos, tăiem sus
    out = c.copy()
    out.px = [c.px[r][:] for r in seq]
    return out


def _recenter_cols(c, seq):
    """Construiește o pânză din lista de coloane `seq`, centrată pe mijlocul pânzei."""
    out = c.copy()
    off = (c.w - len(seq)) // 2
    for y in range(c.h):
        row = [0] * c.w
        for i, col in enumerate(seq):
            x = i + off
            if 0 <= x < c.w and col is not None:
                row[x] = c.px[y][col]
        out.px[y] = row
    return out


def stretch_cols(c, cols):
    """Dublează coloanele date (sprite mai lat), păstrând centrul pe loc."""
    seq = []
    for x in range(c.w):
        seq.append(x)
        if x in set(cols):
            seq.append(x)
    return _recenter_cols(c, seq)


def squash_cols(c, cols):
    """Scoate coloanele date (sprite mai îngust), păstrând centrul pe loc."""
    return _recenter_cols(c, [x for x in range(c.w) if x not in set(cols)])


def palette_swap(c, mapping):
    """Înlocuiește culori după dicționarul {veche: nouă}. Doar paleta se schimbă, nu pixelii."""
    out = c.copy()
    m = {norm(k): norm(v) for k, v in mapping.items()}
    new = Canvas(c.w, c.h)
    remap = [new.idx(m.get(col, col)) if col else 0 for col in c.pal]
    new.px = [[remap[i] for i in row] for row in out.px]
    return new


def white_flash(c, color="#ffffff", keep_outline=True):
    """Cadru de „hit flash”: tot ce e opac devine alb (conturul poate rămâne închis)."""
    out = Canvas(c.w, c.h)
    o = norm(OUTLINE)
    for y in range(c.h):
        for x in range(c.w):
            col = c.get(x, y)
            if col:
                out.set(x, y, col if (keep_outline and col == o) else color)
    return out


def silhouette(c, color):
    out = Canvas(c.w, c.h)
    out.fill(c.mask(), color)
    return out


def crop_paste(c, w, h, dx=0, dy=0):
    """Pânză nouă de w×h cu `c` așezată la (dx, dy). Util pentru a schimba dimensiunea."""
    out = Canvas(w, h)
    out.paste(c, dx, dy)
    return out


# ───────────────────────────── fișiere ─────────────────────────────

def _ensure_dir(path):
    d = os.path.dirname(path)
    if d:
        os.makedirs(d, exist_ok=True)


def save_png(c, path, scale=1):
    """Scrie PNG RGBA (fără metadate variabile, deci octeți identici la fiecare rulare)."""
    _ensure_dir(path)
    c.to_image(scale).save(path, optimize=False)


def _font(size):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:                       # Pillow vechi: doar fontul bitmap
        return ImageFont.load_default()


def contact_sheet(sections, path, scale=6, bg="#141011", cell_bg="#211a1b",
                  text="#e8dcc4", dim="#8c7f74", title=None, gap=14):
    """Planșa de review: fiecare secțiune e un rând de cadre mărite ×scale, cu etichete.

    sections = [(titlu_rând, [(etichetă, Canvas), ...]), ...]
    Fundalul celulei e puțin mai deschis decât fundalul planșei, ca să se vadă marginea cadrului.
    """
    f_title, f_row, f_cell = _font(30), _font(22), _font(16)
    label_h, row_title_h = 24, 34
    rows_px = []
    for name, cells in sections:
        cw = sum(cv.w * scale for _, cv in cells) + gap * (len(cells) - 1)
        ch = max(cv.h * scale for _, cv in cells)
        rows_px.append((cw, ch))
    width = max(cw for cw, _ in rows_px) + 2 * gap * 2
    height = gap * 2 + (48 if title else 0)
    height += sum(row_title_h + ch + label_h + gap * 2 for _, ch in rows_px)
    img = Image.new("RGBA", (width, height), rgba(bg))
    d = ImageDraw.Draw(img)
    y = gap * 2
    if title:
        d.text((gap * 2, y), title, fill=rgba(text), font=f_title)
        y += 48
    for (name, cells), (cw, ch) in zip(sections, rows_px):
        d.text((gap * 2, y), name, fill=rgba(text), font=f_row)
        y += row_title_h
        x = gap * 2
        for label, cv in cells:
            w, h = cv.w * scale, cv.h * scale
            d.rectangle([x, y, x + w - 1, y + h - 1], fill=rgba(cell_bg))
            img.alpha_composite(cv.to_image(scale), (x, y))
            d.text((x, y + h + 4), label, fill=rgba(dim), font=f_cell)
            x += w + gap
        y += ch + label_h + gap * 2
    _ensure_dir(path)
    img.convert("RGB").save(path, optimize=False)
    return img
