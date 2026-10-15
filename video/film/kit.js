// Piese comune scenelor: personajele jocului (vedere laterală, cu respirație și cadre), cărțile (ramă + ilustrație +
// text generat), bilețelele cu evenimente, numerele de damage, zarul, panoul „terminal”, capturile de joc înrămate.
import * as L from '../engine/lib.js';
import { K, PAL, cam, screen, toScreen, spr, rect, box, slice9, frameAt, breath } from './px.js';
import { txt, panel, MONO, PIX as FONT } from './ui.js';

const E = L.ease;
export const HEROES = ['isaac', 'cain', 'lilith', 'azazel'];
export const HERO_NAME = { isaac: 'Isaac', cain: 'Cain', lilith: 'Lilith', azazel: 'Azazel' };
const BOSSES = new Set(['monstro', 'gurdy', 'mom']);

// Un personaj în vedere laterală. pose: idle | attack | hurt | dead ; eroii privesc spre dreapta, inamicii spre stânga.
export function unit(ctx, I, id, x, y, t, o = {}) {
  const { pose = 'idle', phase = 0, flash = 0, sx = 1, sy = 1, alpha = 1, shadow = true, scale = 1, frame } = o;
  const hero = HEROES.includes(id);
  let name;
  if (pose === 'idle') name = hero ? `${id}/side_idle_${frame ?? frameAt(t + phase, 2.2, 2)}` : `${id}/idle_${frame ?? frameAt(t + phase, 2.6, 2)}`;
  else if (pose === 'attack') name = hero ? `${id}/side_attack_${frame ?? 1}` : `${id}/attack_${frame ?? 1}`;
  else if (pose === 'hurt') name = hero ? `${id}/side_hurt_0` : `${id}/hurt_0`;
  else name = hero ? `${id}/side_dead_0` : `${id}/dead_0`;
  const img = I(name) || I(hero ? `${id}/side_idle_0` : `${id}/idle_0`);
  const br = pose === 'dead' ? 1 : breath(t, phase * 2.1);
  const ga = ctx.globalAlpha; // alpha-ul primit de la scenă (fade) se păstrează: umbra nu-l are voie să-l reseteze
  if (shadow && img) { ctx.globalAlpha = ga * 0.35 * alpha; rect(ctx, x - img.width * 0.28 * scale, y - 1, img.width * 0.56 * scale, 2, '#000'); ctx.globalAlpha = ga; }
  spr(ctx, img, x, y, { sx: sx * scale, sy: sy * br * scale, alpha, flash });
  return img;
}

// Portretul de 24×24 (bara de ture) într-o ramă.
export function portrait(ctx, I, id, x, y, o = {}) {
  const { active = false, alpha = 1, scale = 1 } = o;
  const key = HEROES.includes(id) ? `${id}/portrait` : `${id}/portrait`;
  ctx.globalAlpha = alpha;
  spr(ctx, I(active ? 'ui/portrait_frame_active' : 'ui/portrait_frame'), x, y, { ax: 0.5, ay: 0.5, sx: scale, sy: scale });
  spr(ctx, I(key), x, y + 1 * scale, { ax: 0.5, ay: 0.5, sx: scale, sy: scale });
  ctx.globalAlpha = 1;
}

// Cartea din joc: 64×90, ramă pe culoarea eroului. (x, y) = centrul. Textul se scrie clar, peste artă.
export const CARDS = {
  tear: ['Tear', 'isaac', 1, 'Deal 5 damage.'],
  spoon_bender: ['Spoon Bender', 'isaac', 1, 'Deal 4 damage. Apply 2 Mark.'],
  the_sad_onion: ['The Sad Onion', 'isaac', 1, 'Deal 3 damage 2 times.'],
  yum_heart: ['Yum Heart', 'isaac', 1, 'Heal 5 HP.'],
  lucky_punch: ['Lucky Punch', 'cain', 1, 'Deal 5 damage (25% crit).'],
  moms_knife: ['Mom’s Knife', 'cain', 1, 'Deal 7 damage. Apply 2 Mark.'],
  brimstone_swipe: ['Brimstone Swipe', 'azazel', 1, 'Deal 6 damage.'],
  blood_beam: ['Blood Beam', 'azazel', 2, 'Deal 5 damage. Apply 2 Bleed.'],
  incubus_shot: ['Incubus Shot', 'lilith', 1, 'Deal 5 damage.'],
  book_of_belial: ['Book of Belial', 'isaac', 1, 'Gain 2 Strength.'],
  polyphemus: ['Polyphemus', 'isaac', 2, 'Deal 18 damage.'],
  guardian_angel: ['Guardian Angel', 'lilith', 2, 'Heal 10 HP. Gain 5 Block.'],
  dr_fetus: ['Dr. Fetus', 'cain', 2, 'Deal 6 damage (25% crit).'],
};
export function card(ctx, I, id, x, y, c, o = {}) {
  const { s = 1, rot = 0, alpha = 1, back = false, glow = 0, text = true, name, hero, cost, desc, art, fx = 1 } = o;
  const d = CARDS[id] || [name || id, hero || 'isaac', cost ?? 1, desc || ''];
  const nm = name ?? d[0], hr = hero ?? d[1], cs = cost ?? d[2], ds = desc ?? d[3];
  cam(ctx, c);
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.translate(x, y); ctx.rotate(rot); ctx.scale(s * fx, s);
  if (glow > 0) { ctx.globalAlpha = alpha * glow; spr(ctx, I('ui/card_glow'), 0, 0, { ax: 0.5, ay: 0.5 }); ctx.globalAlpha = alpha; }
  if (back) { spr(ctx, I('ui/card_back'), 0, 0, { ax: 0.5, ay: 0.5 }); ctx.restore(); return; }
  spr(ctx, I(`ui/card_frame_${hr}`), 0, 0, { ax: 0.5, ay: 0.5 });
  const artImg = I(`card/${art || id}`);
  if (artImg) ctx.drawImage(artImg, -28, -41, 56, 36);
  spr(ctx, I('ui/cost_orb'), -32 + 3, -45 + 3, { ax: 0.5, ay: 0.5 });
  ctx.restore();
  if (!text || alpha <= 0.05) return;
  // textul clar: îl desenăm în coordonate de ecran, rotit odată cu cartea
  const [sx, sy] = toScreen(c, x, y), z = K * (c.z ?? 1) * s;
  ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.translate(sx, sy); ctx.rotate(rot); ctx.scale(fx, 1);
  ctx.globalAlpha = alpha;
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.letterSpacing = '0.01px';
  let fs = 7.2 * z; ctx.font = `700 ${fs}px "${FONT}"`;
  while (ctx.measureText(nm).width > 54 * z && fs > 4 * z) { fs -= 0.3 * z; ctx.font = `700 ${fs}px "${FONT}"`; }
  ctx.fillStyle = PAL.outline; ctx.fillText(nm, z * 0.5, 0.5 * z + 0.5 * z); ctx.fillStyle = PAL.cream; ctx.fillText(nm, 0, 0.5 * z);
  ctx.font = `600 ${6.3 * z}px "${FONT}"`; ctx.fillStyle = PAL.ink;
  wrap(ctx, ds, 50 * z).forEach((ln, i, a) => ctx.fillText(ln, 0, (23 + (i - (a.length - 1) / 2) * 7.6) * z));
  ctx.font = `700 ${8 * z}px "${FONT}"`; ctx.fillStyle = PAL.outline; ctx.fillText(String(cs), -29 * z + z * 0.6, -42 * z + z * 0.6); ctx.fillStyle = PAL.white; ctx.fillText(String(cs), -29 * z, -42 * z);
  ctx.restore();
}
function wrap(ctx, s, w) {
  const out = []; let cur = '';
  for (const word of s.split(' ')) { const t = cur ? cur + ' ' + word : word; if (ctx.measureText(t).width > w && cur) { out.push(cur); cur = word; } else cur = t; }
  if (cur) out.push(cur); return out;
}

// Numărul de damage care sare (ca DamageNumbers din joc): alb, galben la crit, verde/roșu la statusuri.
export function dmgNumber(ctx, c, s, x, y, dt, o = {}) {
  if (dt < 0 || dt > (o.life ?? 1.1)) return;
  const { color = PAL.white, size = 64 } = o;
  const up = E.outCubic(L.clamp(dt / 0.5)) * 22, pop = 1 + 0.5 * Math.max(0, 1 - dt / 0.18);
  const a = 1 - L.prog(dt, (o.life ?? 1.1) - 0.3, o.life ?? 1.1);
  const [sx, sy] = toScreen(c, x, y - up);
  txt(ctx, s, sx, sy, { size: size * pop, font: 'Silkscreen', weight: 400, color, alpha: a, sh: 5 });
}

// Bilețel cu un eveniment / o comandă (hârtie, text de cod). (x, y) în artă = centrul.
export function note(ctx, c, s, x, y, o = {}) {
  const { alpha = 1, size = 26, color = PAL.ink, paper = PAL.paper, rot = 0, w } = o;
  if (alpha <= 0) return;
  const [sx, sy] = toScreen(c, x, y);
  ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.translate(sx, sy); ctx.rotate(rot); ctx.globalAlpha = alpha;
  ctx.font = `600 ${size}px "${MONO}"`;
  const tw = w ?? ctx.measureText(s).width + 36, th = size * 1.7;
  ctx.fillStyle = 'rgba(0,0,0,0.4)'; ctx.fillRect(-tw / 2 + 6, -th / 2 + 6, tw, th);
  ctx.fillStyle = PAL.outline; ctx.fillRect(-tw / 2 - 3, -th / 2 - 3, tw + 6, th + 6);
  ctx.fillStyle = paper; ctx.fillRect(-tw / 2, -th / 2, tw, th);
  ctx.fillStyle = 'rgba(168,149,111,0.5)'; ctx.fillRect(-tw / 2, th / 2 - 6, tw, 6);
  ctx.fillStyle = color; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(s, 0, 2);
  ctx.restore();
}

// Zar „pixel” (d4 cu fețele 0..3, ca Range(0, 4) din inițiativă). (x, y) = centrul, în artă.
export function die(ctx, c, x, y, v, o = {}) {
  const { s = 1, rot = 0, alpha = 1, color = PAL.paper } = o;
  cam(ctx, c);
  ctx.save(); ctx.globalAlpha = alpha; ctx.translate(x, y); ctx.rotate(rot); ctx.scale(s, s);
  rect(ctx, -8, -8, 16, 16, PAL.outline); rect(ctx, -7, -7, 14, 14, color); rect(ctx, -7, 5, 14, 2, PAL.paper3);
  ctx.restore();
  const [sx, sy] = toScreen(c, x, y);
  ctx.save(); ctx.setTransform(1, 0, 0, 1, sx, sy); ctx.rotate(rot); ctx.globalAlpha = alpha;
  ctx.font = `400 ${28 * s}px "Silkscreen"`; ctx.letterSpacing = '0.01px'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillStyle = PAL.ink; ctx.fillText(String(v), 1, 2);
  ctx.restore();
}

// Fereastră de terminal (ieșirea simulatorului / a testelor). lines = [[text, culoare?], ...]; n = câte rânduri se văd.
export function terminal(ctx, x, y, w, h, lines, n, o = {}) {
  const { title = 'tools/sim/run.sh', size = 26, alpha = 1 } = o;
  if (alpha <= 0) return;
  panel(ctx, x, y, w, h, { fill: 'rgba(8,6,6,0.96)', alpha });
  txt(ctx, title, x + 22, y + 26, { size: 22, font: MONO, weight: 600, color: PAL.paper2, align: 'left', shadow: null, alpha });
  ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.globalAlpha = alpha; ctx.fillStyle = PAL.b3; ctx.fillRect(x + 3, y + 50, w - 6, 3); ctx.restore();
  const lh = size * 1.45, maxRows = Math.floor((h - 70) / lh);
  const vis = lines.slice(0, Math.floor(n));
  const from = Math.max(0, vis.length - maxRows);
  vis.slice(from).forEach(([s, col], i) => txt(ctx, s, x + 22, y + 72 + i * lh + lh / 2, { size, font: MONO, weight: 500, color: col || '#cfe8b0', align: 'left', shadow: null, alpha }));
  if (n < lines.length && Math.floor(n * 4) % 2 === 0) {
    const yy = y + 72 + Math.min(vis.length - from, maxRows - 1) * lh + lh / 2;
    ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.globalAlpha = alpha; ctx.fillStyle = '#cfe8b0'; ctx.fillRect(x + 22, yy - size * 0.45, size * 0.55, size * 0.9); ctx.restore();
  }
}

// Captură din joc, înrămată (picture-in-picture), cu Ken Burns lent. (x, y, w, h) în pixeli de ecran.
export function shot(ctx, I, key, x, y, w, h, o = {}) {
  const { k = 1, z0 = 1.0, z1 = 1.08, fx = 0.5, fy = 0.5, p = 0, alpha = 1 } = o;
  const img = I('shot/' + key);
  if (!img || alpha <= 0) return;
  const e = E.outCubic(L.clamp(k));
  screen(ctx);
  ctx.save(); ctx.globalAlpha = alpha * e;
  panel(ctx, x - 6, y - 6, w + 12, h + 12, { fill: '#000', alpha: alpha * e });
  ctx.beginPath(); ctx.rect(x, y, w, h); ctx.clip();
  const z = L.lerp(z0, z1, E.inOutSine(L.clamp(p)));
  const sw = img.width / z, sh = img.height / z;
  const sx = (img.width - sw) * fx, sy = (img.height - sh) * fy;
  ctx.imageSmoothingEnabled = true;
  ctx.drawImage(img, sx, sy, sw, sh, x, y, w, h);
  ctx.restore();
}

// Bara de HP a jocului (cadrul hpbar_frame + umplere), în artă. v/max.
export function hpBar(ctx, x, y, w, v, max, o = {}) {
  const { lag = v, block = 0 } = o;
  rect(ctx, x - 1, y - 1, w + 2, 5, PAL.outline);
  rect(ctx, x, y, w, 3, PAL.blood3);
  rect(ctx, x, y, Math.round((w * lag) / max), 3, PAL.paper);
  rect(ctx, x, y, Math.round((w * v) / max), 3, PAL.blood);
  rect(ctx, x, y, Math.round((w * v) / max), 1, '#e86a6a');
  if (block > 0) box(ctx, x - 1, y - 1, w + 2, 5, PAL.block);
}
