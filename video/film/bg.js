// Fundalurile: peretele de cărămidă al subsolului (textura jocului), torțe care pâlpâie, pergamentul pe care desenăm.
import * as L from '../engine/lib.js';
import { AW, AH, K, PAL, cam, screen, spr, slice9, tile, rect, dither, frameAt } from './px.js';

// Etajul curent (basement / caves / depths): filmul „coboară” odată cu jocul. Îl setează film.js pentru fiecare scenă.
let THEME = 'basement';
export const setTheme = (x) => { THEME = x || 'basement'; };

// Peretele: textura env/<etaj>/wall (64×64) repetată, întunecată, cu lumina caldă a torțelor.
export function wall(ctx, I, t, o = {}) {
  const { c = {}, dark = THEME === 'caves' ? 0.6 : 0.5, torches = [[44, 120], [596, 120]], drift = 1, tex = `env/${THEME}/wall`, glow = 1 } = o;
  // derivă lentă a camerei de fundal: nimic nu stă complet pe loc
  const dx = Math.sin(t * 0.21) * 4 * drift, dy = Math.cos(t * 0.17) * 3 * drift;
  const cc = { ...c, ox: (c.ox ?? 0) + dx, oy: (c.oy ?? 0) + dy };
  cam(ctx, cc);
  tile(ctx, I(tex), -16, -16, AW + 32, AH + 32);
  screen(ctx);
  ctx.fillStyle = `rgba(11,7,7,${dark})`; ctx.fillRect(0, 0, 1920, 1080);
  for (const [i, [x, y]] of torches.entries()) torch(ctx, I, t, x, y, cc, i, glow);
  return cc;
}

export function torch(ctx, I, t, x, y, c = {}, seed = 0, glow = 1) {
  const fl = 0.78 + 0.22 * L.noise(t * 7 + seed * 13, seed + 2) * 0.5 + 0.12 * L.noise(t * 17 + seed, seed + 5);
  const sx = (c.ox ?? 0) + x * K, sy = (c.oy ?? 0) + y * K;
  screen(ctx);
  ctx.save();
  ctx.globalCompositeOperation = 'lighter';
  const g = ctx.createRadialGradient(sx, sy - 30, 0, sx, sy - 30, 520 * fl);
  g.addColorStop(0, `rgba(255,150,60,${0.42 * fl * glow})`); g.addColorStop(0.35, `rgba(210,95,35,${0.17 * fl * glow})`); g.addColorStop(1, 'rgba(120,40,10,0)');
  ctx.fillStyle = g; ctx.fillRect(sx - 600, sy - 630, 1200, 1200);
  ctx.restore();
  cam(ctx, c);
  spr(ctx, I('prop/torch_' + frameAt(t + seed * 0.37, 9, 4)), x, y + 16, { ay: 1, sx: 1.5, sy: 1.5 });
}

export function vignette(ctx, k = 0.75) {
  screen(ctx);
  const g = ctx.createRadialGradient(960, 540, 380, 960, 540, 1150);
  g.addColorStop(0, 'rgba(0,0,0,0)'); g.addColorStop(1, `rgba(0,0,0,${k})`);
  ctx.fillStyle = g; ctx.fillRect(0, 0, 1920, 1080);
}

// Praf în aer (dust_mote din joc), determinist.
export function dust(ctx, I, t, c = {}, n = 26, seed = 3) {
  cam(ctx, c);
  const r = L.rng(seed);
  for (let i = 0; i < n; i++) {
    const x0 = r() * AW, y0 = r() * AH, sp = 3 + r() * 6, ph = r() * 10;
    const x = (x0 + Math.sin(t * 0.4 + ph) * 12 + t * sp) % AW, y = (y0 + Math.cos(t * 0.3 + ph) * 8 - t * sp * 0.4 + AH * 4) % AH;
    ctx.globalAlpha = 0.25 + 0.35 * (0.5 + 0.5 * Math.sin(t * 1.3 + ph));
    spr(ctx, I('prop/dust_mote'), x, y, { ay: 0.5 });
  }
  ctx.globalAlpha = 1;
}

// Pergamentul (panel_paper, 9-slice cu centrul repetat), cu umbră.
export function board(ctx, I, x, y, w, h, c = {}, k = 1) {
  if (k <= 0) return;
  cam(ctx, c);
  const e = L.ease.outBack(L.clamp(k), 1.2);
  const cx = x + w / 2, cy = y + h / 2, ww = Math.round(w * (0.9 + 0.1 * e)), hh = Math.round(h * (0.9 + 0.1 * e));
  ctx.globalAlpha = L.clamp(k * 3);
  rect(ctx, cx - ww / 2 + 4, cy - hh / 2 + 5, ww, hh, 'rgba(0,0,0,0.45)');
  slice9(ctx, I('ui/panel_paper'), cx - ww / 2, cy - hh / 2, ww, hh, 12, true);
  ctx.globalAlpha = 1;
}

// Podeaua scenei de luptă (vedere laterală): textura floor + o linie de orizont.
export function floor(ctx, I, y0, c = {}, tex = `env/${THEME}/floor`) {
  cam(ctx, c);
  tile(ctx, I(tex), -16, y0, AW + 32, AH - y0 + 16, 0, 0);
  screen(ctx);
  const sy = (c.oy ?? 0) + y0 * K;
  const g = ctx.createLinearGradient(0, sy, 0, 1080);
  g.addColorStop(0, 'rgba(11,7,7,0.25)'); g.addColorStop(1, 'rgba(11,7,7,0.75)');
  ctx.fillStyle = g; ctx.fillRect(0, sy, 1920, 1080 - sy);
  cam(ctx, c); rect(ctx, -16, y0, AW + 32, 1, PAL.b5);
}

// Intrare în scenă: negrul se dizolvă „pe grilă” (dither Bayer), ca tranzițiile din artă.
const masks = {};
function mask(level, color) { // 16 niveluri precalculate: un fillRect pe pixel ar fi prea lent pe tot cadrul
  const key = level + color;
  if (masks[key]) return masks[key];
  const c = document.createElement('canvas'); c.width = AW; c.height = AH;
  dither(c.getContext('2d'), 0, 0, AW, AH, color, level / 16);
  return (masks[key] = c);
}
export function ditherCover(ctx, k, color = PAL.void) {
  const lv = Math.round(L.clamp(k) * 16);
  if (lv <= 0) return;
  cam(ctx, {});
  if (lv >= 16) { ctx.fillStyle = color; ctx.fillRect(0, 0, AW, AH); return; }
  ctx.drawImage(mask(lv, color), 0, 0);
}
export function ditherIn(ctx, t, a, b, color = PAL.void) { ditherCover(ctx, 1 - L.prog(t, a, b), color); }
