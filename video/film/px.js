// Desenul „pixel art” al filmului. Spațiul de lucru e cel al interfeței jocului: 640×360 pixeli de artă, mărit ×3 la
// 1920×1080 (exact ca UIRoot din joc). Primitivele (dreptunghiuri, linii Bresenham, discuri) se aliniază la grila de artă;
// sprite-urile pot sta și la poziții fracționare (mișcare lină), dar sunt mereu eșantionate nearest-neighbour.
import * as L from '../engine/lib.js';

export const AW = 640, AH = 360, K = 3;

// Paleta jocului (tools/art/palette.py) + câteva nuanțe derivate.
export const PAL = {
  outline: '#1b0f0f', ink: '#2a1b13', paper: '#e8dcc4', paper2: '#c8b89a', paper3: '#a8956f',
  gold: '#e0b04a', gold2: '#a8782a', cream: '#f2e6c9',
  skin: '#f2d3c4', skin2: '#dcae9c', tear: '#7fb2e5', tear2: '#4a7fbf', tearHi: '#d6ecff',
  blood: '#d33c3c', blood2: '#9e1b1b', blood3: '#5c0d0d',
  poop: '#a8743d', poop2: '#6b4423',
  b1: '#6e5241', b2: '#5e4232', b3: '#4a3326', b4: '#3a271c', b5: '#2a1b13', night: '#140c0a', void: '#0b0707',
  block: '#6fa8dc', bleed: '#d33c3c', poison: '#7cc04a', stun: '#f2d14a', weak: '#9a8fb0', mark: '#ff8a3d', strength: '#e05050', taunt: '#c0c0c0',
  common: '#9a9a9a', uncommon: '#4a7fbf', rare: '#e0b04a',
  green: '#7cc04a', red: '#d33c3c', white: '#fff6e8',
};

// Camera: (z, ox, oy) = zoom și decalaj în pixeli de ecran. Totul desenat „în artă” trece prin ea.
export function cam(ctx, c = {}) {
  const z = c.z ?? 1;
  ctx.setTransform(K * z, 0, 0, K * z, c.ox ?? 0, c.oy ?? 0);
  ctx.imageSmoothingEnabled = false;
}
export const screen = (ctx) => { ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.imageSmoothingEnabled = true; };
// din coordonate de artă în pixeli de ecran (pentru text desenat clar peste artă)
export const toScreen = (c, x, y) => [(c.ox ?? 0) + x * K * (c.z ?? 1), (c.oy ?? 0) + y * K * (c.z ?? 1)];

const R = Math.round;
export function rect(g, x, y, w, h, c) { g.fillStyle = PAL[c] || c; g.fillRect(R(x), R(y), R(w), R(h)); }
export function rectF(g, x, y, w, h, c) { g.fillStyle = PAL[c] || c; g.fillRect(x, y, w, h); }
export function box(g, x, y, w, h, c, t = 1) { // contur
  x = R(x); y = R(y); w = R(w); h = R(h);
  g.fillStyle = PAL[c] || c;
  g.fillRect(x, y, w, t); g.fillRect(x, y + h - t, w, t); g.fillRect(x, y, t, h); g.fillRect(x + w - t, y, t, h);
}
export function line(g, x0, y0, x1, y1, c, { dash = 0, gap = 0, k = 1, th = 1 } = {}) {
  // Bresenham; k ∈ [0,1] = cât din linie e desenat (pentru linii care „se trag”)
  x0 = R(x0); y0 = R(y0); x1 = R(x1); y1 = R(y1);
  g.fillStyle = PAL[c] || c;
  const dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
  const n = Math.max(dx, -dy), lim = Math.floor(n * L.clamp(k));
  let err = dx + dy, i = 0;
  for (;;) {
    if (i > lim) break;
    if (!dash || (i % (dash + gap)) < dash) g.fillRect(x0 - (th >> 1), y0 - (th >> 1), th, th);
    if (x0 === x1 && y0 === y1) break;
    const e2 = 2 * err;
    if (e2 >= dy) { err += dy; x0 += sx; }
    if (e2 <= dx) { err += dx; y0 += sy; }
    i++;
  }
}
export function disc(g, cx, cy, r, c) {
  cx = R(cx); cy = R(cy); g.fillStyle = PAL[c] || c;
  const ri = Math.ceil(r);
  for (let dy = -ri; dy <= ri; dy++) {
    if (r * r - dy * dy < 0) continue;
    const dx = Math.floor(Math.sqrt(r * r - dy * dy) + 0.35);
    g.fillRect(cx - dx, cy + dy, dx * 2 + 1, 1);
  }
}
export function ring(g, cx, cy, r, c, th = 1) {
  cx = R(cx); cy = R(cy); g.fillStyle = PAL[c] || c;
  const n = Math.ceil(r);
  for (let dy = -n; dy <= n; dy++) for (let dx = -n; dx <= n; dx++) {
    const d = Math.sqrt(dx * dx + dy * dy);
    if (d <= r + 0.3 && d > r - th + 0.3) g.fillRect(cx + dx, cy + dy, 1, 1);
  }
}
export function arrowHead(g, x, y, dir, c, s = 3) { // dir: 'r','l','u','d'
  x = R(x); y = R(y); g.fillStyle = PAL[c] || c;
  for (let i = 0; i < s; i++) {
    const w = 2 * (s - i) - 1;
    if (dir === 'r') g.fillRect(x + i, y - (s - i - 1), 1, w);
    if (dir === 'l') g.fillRect(x - i, y - (s - i - 1), 1, w);
    if (dir === 'd') g.fillRect(x - (s - i - 1), y + i, w, 1);
    if (dir === 'u') g.fillRect(x - (s - i - 1), y - i, w, 1);
  }
}
// săgeată dreaptă (orizontală/verticală sau oblică) cu vârf; k = cât s-a desenat
export function arrow(g, x0, y0, x1, y1, c, k = 1, opt = {}) {
  const x = L.lerp(x0, x1, k), y = L.lerp(y0, y1, k);
  line(g, x0, y0, x, y, c, opt);
  if (k > 0.05) {
    const dir = Math.abs(x1 - x0) >= Math.abs(y1 - y0) ? (x1 > x0 ? 'r' : 'l') : (y1 > y0 ? 'd' : 'u');
    arrowHead(g, x, y, dir, c, opt.head || 3);
  }
}

// Bayer 4×4: tranziții „dither” între culori, ca în artă.
const BAYER = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5].map((v) => (v + 0.5) / 16);
export function dither(g, x, y, w, h, c, k) {
  if (k <= 0) return; g.fillStyle = PAL[c] || c;
  x = R(x); y = R(y); w = R(w); h = R(h);
  if (k >= 1) { g.fillRect(x, y, w, h); return; }
  for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) if (BAYER[((y + j) & 3) * 4 + ((x + i) & 3)] < k) g.fillRect(x + i, y + j, 1, 1);
}

// ── sprite-uri ────────────────────────────────────────────────
const tintCache = new Map();
export function tinted(img, color) {
  const key = img.src + '|' + color;
  if (tintCache.has(key)) return tintCache.get(key);
  const c = document.createElement('canvas'); c.width = img.width; c.height = img.height;
  const g = c.getContext('2d'); g.drawImage(img, 0, 0); g.globalCompositeOperation = 'source-in'; g.fillStyle = PAL[color] || color; g.fillRect(0, 0, c.width, c.height);
  tintCache.set(key, c); return c;
}
// (x, y) = punctul de ancoră (implicit jos-centru, ca pivotul personajelor); sx/sy = scară (squash & stretch), rot în radiani.
export function spr(g, img, x, y, o = {}) {
  if (!img) return;
  const { ax = 0.5, ay = 1, sx = 1, sy = 1, flip = false, alpha = 1, flash = 0, flashColor = 'white', rot = 0, snap = false } = o;
  const w = img.width, h = img.height;
  g.save();
  g.globalAlpha *= alpha;
  g.translate(snap ? R(x) : x, snap ? R(y) : y);
  if (rot) g.rotate(rot);
  g.scale((flip ? -1 : 1) * sx, sy);
  g.drawImage(img, -w * ax, -h * ay);
  if (flash > 0) { g.globalAlpha = alpha * L.clamp(flash); g.drawImage(tinted(img, flashColor), -w * ax, -h * ay); }
  g.restore();
}
// panou 9-slice; tiled = centrul și marginile se repetă (pergamentul are perioadă 24)
export function slice9(g, img, x, y, w, h, b, tiled = false) {
  if (!img) return;
  x = R(x); y = R(y); w = Math.max(2 * b, R(w)); h = Math.max(2 * b, R(h));
  const iw = img.width, ih = img.height, cw = iw - 2 * b, ch = ih - 2 * b;
  const D = (sx, sy, sw, sh, dx, dy, dw, dh) => { if (dw > 0 && dh > 0) g.drawImage(img, sx, sy, sw, sh, dx, dy, dw, dh); };
  D(0, 0, b, b, x, y, b, b); D(iw - b, 0, b, b, x + w - b, y, b, b); D(0, ih - b, b, b, x, y + h - b, b, b); D(iw - b, ih - b, b, b, x + w - b, y + h - b, b, b);
  if (!tiled) {
    D(b, 0, cw, b, x + b, y, w - 2 * b, b); D(b, ih - b, cw, b, x + b, y + h - b, w - 2 * b, b);
    D(0, b, b, ch, x, y + b, b, h - 2 * b); D(iw - b, b, b, ch, x + w - b, y + b, b, h - 2 * b);
    D(b, b, cw, ch, x + b, y + b, w - 2 * b, h - 2 * b);
    return;
  }
  for (let xx = x + b; xx < x + w - b; xx += cw) {
    const ww = Math.min(cw, x + w - b - xx);
    D(b, 0, ww, b, xx, y, ww, b); D(b, ih - b, ww, b, xx, y + h - b, ww, b);
    for (let yy = y + b; yy < y + h - b; yy += ch) { const hh = Math.min(ch, y + h - b - yy); D(b, b, ww, hh, xx, yy, ww, hh); }
  }
  for (let yy = y + b; yy < y + h - b; yy += ch) {
    const hh = Math.min(ch, y + h - b - yy);
    D(0, b, b, hh, x, yy, b, hh); D(iw - b, b, b, hh, x + w - b, yy, b, hh);
  }
}
// textură care se repetă (perete, podea)
export function tile(g, img, x, y, w, h, ox = 0, oy = 0) {
  if (!img) return;
  const iw = img.width, ih = img.height;
  const sx0 = ((ox % iw) + iw) % iw, sy0 = ((oy % ih) + ih) % ih;
  for (let yy = y - sy0; yy < y + h; yy += ih) for (let xx = x - sx0; xx < x + w; xx += iw) {
    const cx0 = Math.max(x, xx), cy0 = Math.max(y, yy), cx1 = Math.min(x + w, xx + iw), cy1 = Math.min(y + h, yy + ih);
    if (cx1 > cx0 && cy1 > cy0) g.drawImage(img, cx0 - xx, cy0 - yy, cx1 - cx0, cy1 - cy0, cx0, cy0, cx1 - cx0, cy1 - cy0);
  }
}

// cadre de animație: frames(t, fps, n) → index
export const frameAt = (t, fps, n) => Math.floor(Math.max(0, t) * fps) % n;
// „respirația” din joc (UnitView): scaleY = 1 + 0.028·sin(2π·0.55·t + faza)
export const breath = (t, phase = 0, amp = 0.028) => 1 + amp * Math.sin(t * 2 * Math.PI * 0.55 + phase);
