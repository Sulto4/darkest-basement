// Scena 9 — Mișcarea care se simte bine. Lerp, easing (funcții pe [0,1]), atacul din UnitView (anticipare OutQuad 0,16 s,
// salt OutCubic 0,1 s, întoarcere InOutQuad 0,22 s), respirația (sinus), SmoothDamp, shake = trauma² cu zgomot, hit-stop.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, PAL, K, toScreen } from '../px.js';
import { wall, vignette, floor } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag, panel } from '../ui.js';
import { unit, card, dmgNumber } from '../kit.js';

const E = L.ease;
const outQuad = (k) => 1 - (1 - k) * (1 - k), inOutQuad = (k) => (k < 0.5 ? 2 * k * k : 1 - Math.pow(-2 * k + 2, 2) / 2);
const outCubic = (k) => 1 - Math.pow(1 - k, 3);
const outBack = (k) => { const c1 = 1.70158, c3 = c1 + 1; return 1 + c3 * Math.pow(k - 1, 3) + c1 * Math.pow(k - 1, 2); };
const GR = { x: 130, y: 200, w: 640, h: 560 }; // panoul cu grafice (ecran)

export default {
  id: 's09', num: 8, title: 'Mișcarea care se simte bine', music: 'menu',
  plan: [
    { title: 2.6 }, { line: 1, gap: -0.5 }, { line: 2, gap: 0.5 }, { line: 3, gap: 0.4 }, { pause: 0.4 }, { line: 4, gap: 0.5 }, { line: 5, gap: 0.5 }, { pause: 0.6 }, { line: 6, gap: 0.5 }, { pause: 0.6 },
    { line: 7, gap: 0.6 }, { pause: 0.8 }, { line: 8, gap: 0.4 }, { pause: 0.5 },
    { slot: { id: 's09-atac', label: 'în joc: anticipare, salt, lovitură' }, dur: 3.6 },
    { line: 9, gap: 0.6 }, { line: 10, gap: 0.5 }, { pause: 0.5 }, { line: 11, gap: 0.6 }, { line: 12, gap: 0.4 }, { line: 13, gap: 0.5 }, { pause: 0.3 }, { line: 14, gap: 0.5 }, { pause: 0.6 },
    { slot: { id: 's09-impact', label: 'în joc: hit-stop, shake, flash' }, dur: 4.2 },
  ],
  post: 0.5,
  setup(S) {
    S.slot('s09-atac').shots = [{ key: 'card_played', z: [1.1, 1.0], f: [0.4, 0.6] }, { key: 'hit', z: [1.0, 1.1], f: [0.45, 0.45] }];
    S.slot('s09-impact').shots = [{ key: 'crit', z: [1.0, 1.08], f: [0.55, 0.45] }, { key: 'monstro_impact', z: [1.08, 1.0], f: [0.55, 0.5] }];
    S.T = { hits: [S.w(11, 'lovitură') - 0.1, S.w(11, 'unu') + 0.2, S.E(11) + 0.2], stop: S.w(14, 'hit'), freeze: S.w(14, 'înghețat') };
  },
  draw(ctx, t, S) {
    const I = S.I, T = S.T;
    // shake-ul demonstrat la 14 (trauma² · zgomot), aplicat întregii scene
    const tr = t > T.freeze ? Math.max(0, 0.85 - 1.5 * (t - T.freeze - 0.09)) : 0;
    const sh = tr * tr, c0 = { ox: 26 * sh * L.noise(t * 18, 3), oy: 26 * sh * L.noise(t * 18, 7) };
    const c = wall(ctx, I, t, { c: c0, dark: 0.6, torches: [[20, 70], [620, 70]] });
    const on = (a, b, ia = 0.4, ob = 0.3) => Math.min(L.prog(t, a, a + ia), 1 - L.prog(t, b - ob, b));
    const W = (n, w) => S.w(n, w);
    // ── 1: Azazel respiră ──
    const k1 = on(S.L(1) - 0.2, S.L(2));
    if (k1 > 0) { floor(ctx, I, 270, c); cam(ctx, c); ctx.globalAlpha = k1; unit(ctx, I, 'azazel', 320, 270, t, { scale: 3 }); ctx.globalAlpha = 1; }
    // ── 2–6: lerp și easing ──
    const kL = on(S.L(2) - 0.2, S.L(7) - 0.1);
    if (kL > 0) {
      // pista: de la a la b
      const ax = 1000, bx = 1760, ty = 640;
      panel(ctx, 900, 420, 960, 380, { alpha: kL });
      screen(ctx); ctx.globalAlpha = kL; ctx.fillStyle = PAL.b3; ctx.fillRect(ax, ty + 40, bx - ax, 6); ctx.globalAlpha = 1;
      txt(ctx, 'a', ax, ty + 90, { size: 44, font: 'JetBrains Mono', weight: 700, color: PAL.paper, alpha: kL });
      txt(ctx, 'b', bx, ty + 90, { size: 44, font: 'JetBrains Mono', weight: 700, color: PAL.paper, alpha: kL });
      // ce curbă e activă acum
      const mode = t < S.L(4) ? 'lin' : t < S.L(5) ? 'lin' : t < S.L(6) ? 'cubic' : 'back';
      const f = mode === 'cubic' ? outCubic : mode === 'back' ? outBack : (k) => k;
      const loop = 1.6, lt = ((t - S.L(2)) % loop) / loop, kk = L.clamp(lt / 0.75);
      const tv = t < S.L(3) ? E.inOutSine(L.prog(t, W(2, 'ori'), W(2, 'ori') + 1.2)) : kk;
      // urmele (onion skin): distanța dintre ele = viteza
      if (t > S.L(3)) for (let i = 0; i <= 10; i++) { const x = L.lerp(ax, bx, f(i / 10)); screen(ctx); ctx.globalAlpha = 0.35 * kL; ctx.fillStyle = mode === 'lin' ? PAL.paper2 : PAL.gold; ctx.fillRect(x - 3, ty + 52, 6, 22); ctx.globalAlpha = 1; }
      const x = L.lerp(ax, bx, f(tv));
      cam(ctx, { ox: x - 24 * K, oy: ty - 48 * K + 48 }); ctx.globalAlpha = kL; unit(ctx, I, 'isaac', 24, 48, t, { scale: 1, shadow: false }); ctx.globalAlpha = 1;
      if (t < S.L(4)) txt(ctx, `t = ${tv.toFixed(2).replace('.', ',')}`, 1380, 470, { size: 40, font: 'JetBrains Mono', weight: 600, color: PAL.gold, alpha: kL });
      else txt(ctx, mode === 'lin' ? 'f(t) = t' : mode === 'cubic' ? 'f(t) = 1 − (1 − t)³' : 'OutBack: trece de 1', 1380, 470, { size: 40, font: 'JetBrains Mono', weight: 600, color: PAL.gold, alpha: kL });
      // formula lerp
      const kf = on(W(2, 'interpolare') - 0.2, S.L(4) - 0.1);
      txt(ctx, 'lerp(a, b, t) = a + (b − a) · t', 1380, 300, { size: 50, font: 'JetBrains Mono', weight: 700, color: PAL.paper, alpha: kf * kL });
      // graficul
      graph(ctx, t, S, kL * L.prog(t, S.L(3) - 0.2, S.L(3) + 0.3), mode, f, kk);
      // 4: definiția unei funcții de easing
      const k4 = on(S.L(4) - 0.1, S.L(5) - 0.1);
      txt(ctx, 'f : [0, 1] → [0, 1],  f(0) = 0,  f(1) = 1', 1380, 300, { size: 44, font: 'JetBrains Mono', weight: 700, color: PAL.paper, alpha: k4 });
      // 6: cartea care „pop”-ează cu OutBack
      const k6 = on(S.L(6) - 0.1, S.L(7) - 0.1);
      if (k6 > 0) { const p = ((t - S.L(6)) % 1.4) / 0.5; const s = p < 1 ? outBack(L.clamp(p)) : 1; card(ctx, I, 'tear', 445, 105, c, { s: Math.max(0.01, s), alpha: k6 }); }
      const kc5 = on(S.L(5) - 0.1, S.L(6) - 0.1), kc6 = on(S.L(6), S.L(7) - 0.1);
      if (kc5 > 0) code(ctx, { x: 900, y: 830, w: 960, ...snip(S.data, 'ease_cubic'), re: ['OutCubic'], k: kc5, focus: 1, size: 26 });
    }
    // ── 7–8: atacul, squash & stretch, respirația ──
    const kA = on(S.L(7) - 0.2, S.slot('s09-atac').t0);
    if (kA > 0) attack(ctx, t, S, c, kA);
    // ── 9–10: SmoothDamp ──
    const kS = on(S.L(9) - 0.2, S.L(11) - 0.1);
    if (kS > 0) smooth(ctx, t, S, c, kS);
    // ── 11–13: trauma, trauma², zgomot ──
    const kT = on(S.L(11) - 0.2, S.L(14) - 0.1);
    if (kT > 0) trauma(ctx, t, S, kT);
    // ── 14: hit-stop pe o lovitură reală ──
    const kH = on(S.L(14) - 0.2, S.slot('s09-impact').t0);
    if (kH > 0) hitstop(ctx, t, S, c, kH);
    vignette(ctx, 0.6);
    titleCard(ctx, t, { num: this.num, title: this.title, sub: 'juice-ul e matematică' });
    const C = [[1, 'senzația = *matematică*'], [2, '*lerp*: interpolare liniară'], [3, 't uniform = mișcare *robotică*'], [4, 'easing: o funcție *[0,1] → [0,1]*'], [5, '*OutCubic*: pleacă repede, frânează moale'], [6, '*OutBack*: trece de 1 și revine = pop'],
      [7, 'anticipare, salt, întoarcere: *trei curbe*'], [8, 'squash & stretch, respirația = *un sinus*'], [9, '*SmoothDamp*: arc cu amortizare critică'], [10, 'ținta sare, *camera nu*'], [11, '*trauma* ∈ [0, 1], scade în timp'], [12, 'mișcarea = *trauma²*'], [13, '*zgomot continuu*, nu random'], [14, '*hit-stop*: câteva zeci de ms înghețate']];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n === 8 ? S.slot('s09-atac').t0 : n === 14 ? S.slot('s09-impact').t0 : S.L(n + 1) - 0.1);
  },
  sfx(S) {
    const T = S.T, out = [];
    out.push({ t: S.L(6) + 0.05, id: 'card_draw', gain: 0.3 });
    // atacul în slow-motion: salt + lovitură
    for (let i = 0; i < 3; i++) { const a = S.L(7) + 0.4 + i * 3.1; out.push({ t: a + 0.16 * 5, id: 'punch', gain: 0.35 }, { t: a + 0.26 * 5, id: 'hit_flesh_1', gain: 0.35 }); }
    out.push({ t: S.slot('s09-atac').t0, id: 'card_play', gain: 0.45 }, { t: S.slot('s09-atac').t0 + 1.8, id: 'hit_flesh_2', gain: 0.5 });
    T.hits.forEach((a, i) => out.push({ t: a, id: 'hit_flesh_1', gain: 0.3 + i * 0.1 }));
    out.push({ t: T.freeze, id: 'crit_hit', gain: 0.55 }, { t: T.freeze, id: 'punch', gain: 0.4 });
    out.push({ t: S.slot('s09-impact').t0, id: 'crit_hit', gain: 0.55 }, { t: S.slot('s09-impact').t0 + 2.1, id: 'monstro_land', gain: 0.55 });
    return out;
  },
};

// graficul curbei active: axele [0,1]×[0,1] și un punct care urcă pe curbă
function graph(ctx, t, S, k, mode, f, kk) {
  if (k <= 0) return;
  const { x, y, w, h } = GR;
  panel(ctx, x - 40, y - 40, w + 80, h + 110, { alpha: k });
  const X = (u) => x + u * w, Y = (v) => y + h - v * (h * 0.8);
  screen(ctx); ctx.globalAlpha = k;
  ctx.fillStyle = PAL.paper3; ctx.fillRect(x, y + h, w, 3); ctx.fillRect(x, y + h * 0.1, 3, h * 0.9);
  ctx.setLineDash([8, 8]); ctx.strokeStyle = PAL.b2; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(X(0), Y(1)); ctx.lineTo(X(1), Y(1)); ctx.stroke(); ctx.setLineDash([]);
  // curbele: liniarul rămâne ca referință
  const curve = (fn, col, lw) => { ctx.strokeStyle = col; ctx.lineWidth = lw; ctx.beginPath(); for (let i = 0; i <= 80; i++) ctx.lineTo(X(i / 80), Y(fn(i / 80))); ctx.stroke(); };
  curve((u) => u, mode === 'lin' ? PAL.paper : PAL.b2, mode === 'lin' ? 6 : 3);
  const draw = mode === 'lin' ? 0 : L.prog(t, mode === 'cubic' ? S.L(5) : S.L(6), (mode === 'cubic' ? S.L(5) : S.L(6)) + 0.8);
  if (mode !== 'lin') { ctx.save(); ctx.beginPath(); ctx.rect(x - 10, y - 60, w * draw + 10, h + 80); ctx.clip(); curve(f, PAL.gold, 7); ctx.restore(); }
  ctx.fillStyle = PAL.white; ctx.beginPath(); ctx.arc(X(kk), Y(f(kk)), 12, 0, 7); ctx.fill();
  ctx.globalAlpha = 1;
  txt(ctx, 't', X(1) + 24, Y(0), { size: 34, font: 'JetBrains Mono', weight: 700, color: PAL.paper2, alpha: k });
  txt(ctx, 'f(t)', X(0) + 56, Y(1) - 56, { size: 34, font: 'JetBrains Mono', weight: 700, color: PAL.paper2, alpha: k });
  txt(ctx, '1', X(0) - 26, Y(1), { size: 28, font: 'JetBrains Mono', weight: 600, color: PAL.paper3, alpha: k });
  txt(ctx, '0', X(0) - 20, Y(0) + 26, { size: 28, font: 'JetBrains Mono', weight: 600, color: PAL.paper3, alpha: k });
}

// atacul lui Azazel, cu timpii din UnitView, încetinit ×5 (bucla durează 3,1 s); plus squash & stretch și respirația
function attack(ctx, t, S, c, k) {
  const I = S.I;
  floor(ctx, I, 250, c);
  const slow = 5, lt = ((t - S.L(7) - 0.4) % 3.1 + 3.1) % 3.1, r = lt / slow; // timpul „real” din animație
  let off = 0, phase = 'stă', sx = 1, sy = 1, pose = 'idle';
  if (r < 0.16) { off = -14 * outQuad(r / 0.16); phase = 'anticipare'; sx = 1.12; sy = 0.88; pose = 'attack'; }
  else if (r < 0.26) { off = L.lerp(-14, 150, outCubic((r - 0.16) / 0.1)); phase = 'salt'; sx = 0.85; sy = 1.14; pose = 'attack'; }
  else if (r < 0.4) { off = 150; phase = 'impact'; sx = 1.12; sy = 0.9; pose = 'attack'; }
  else if (r < 0.62) { off = L.lerp(150, 0, inOutQuad((r - 0.4) / 0.22)); phase = 'acasă'; }
  const showSq = t > S.L(8) - 0.2;
  cam(ctx, c); ctx.globalAlpha = k;
  unit(ctx, I, 'azazel', 170 + off, 250, t, { scale: 2, pose, sx: showSq ? sx : 1, sy: showSq ? sy : 1, frame: pose === 'attack' ? (r < 0.16 ? 0 : 1) : undefined });
  const hit = r >= 0.26 && r < 0.36;
  unit(ctx, I, 'gaper', 420, 250, t, { scale: 2, flash: hit ? 1 : 0, pose: hit ? 'hurt' : 'idle' });
  ctx.globalAlpha = 1;
  // cele trei faze, cu curba lor
  const ph = [['anticipare', 'OutQuad · 0,16 s', 0, 0.16], ['salt', 'OutCubic · 0,1 s', 0.16, 0.26], ['acasă', 'InOutQuad · 0,22 s', 0.4, 0.62]];
  ph.forEach(([n, d, a, b], i) => {
    const x = 330 + i * 420, act = r >= a && r < b;
    const kk = k * L.prog(t, S.w(7, ['înapoi', 'sare', 'acasă'][i]) - 0.3, S.w(7, ['înapoi', 'sare', 'acasă'][i]));
    if (kk <= 0) return;
    panel(ctx, x - 180, 790, 360, 130, { alpha: kk, fill: act ? 'rgba(60,40,20,0.95)' : 'rgba(20,12,10,0.92)', edge: act ? PAL.gold : PAL.b3 });
    txt(ctx, n, x, 830, { size: 36, weight: 700, color: act ? PAL.gold : PAL.paper, alpha: kk });
    txt(ctx, d, x, 880, { size: 28, font: 'JetBrains Mono', weight: 600, color: PAL.paper2, alpha: kk });
  });
  txt(ctx, 'încetinit de 5 ori', 1700, 130, { size: 30, color: PAL.paper2, alpha: k });
  // 8: respirația: patru eroi, același sinus cu faze diferite
  const kb = k * L.prog(t, S.w(8, 'respiră') - 0.3, S.w(8, 'respiră') + 0.1);
  if (kb > 0) {
    panel(ctx, 1150, 170, 700, 380, { alpha: kb });
    txt(ctx, 'scaleY = 1 + 0,028 · sin(2π · 0,55 · t + φ)', 1500, 215, { size: 26, font: 'JetBrains Mono', weight: 600, color: PAL.gold, alpha: kb });
    ['lilith', 'isaac', 'cain', 'azazel'].forEach((id, i) => {
      cam(ctx, { ox: 1210 + i * 160 - 24 * 3, oy: 310 - 48 * 3 + 160 }); ctx.globalAlpha = kb;
      unit(ctx, I, id, 24, 48, t, { phase: i * 0.7, scale: 1, shadow: false }); ctx.globalAlpha = 1;
      // curba sinusului sub fiecare
      screen(ctx); ctx.globalAlpha = kb; ctx.strokeStyle = PAL.tear; ctx.lineWidth = 3; ctx.beginPath();
      for (let j = 0; j <= 40; j++) { const tt = t - 2 + j / 20; ctx.lineTo(1170 + i * 160 + j * 3, 470 - 900 * 0.028 * Math.sin(tt * 2 * Math.PI * 0.55 + i * 0.7 * 2.1)); }
      ctx.stroke(); ctx.globalAlpha = 1;
    });
  }
}

function smooth(ctx, t, S, c, k) {
  // ținta sare la momente fixe; camera o urmează: soluția exactă a arcului critic x(t) = ț + (x0 − ț)(1 + ωt)e^{−ωt}, ω = 2/smoothTime
  const st = 0.22, w = 2 / st;
  const x0 = 260, y0 = 720, gw = 1400, gh = 360;
  panel(ctx, x0 - 60, y0 - gh - 110, gw + 120, gh + 200, { alpha: k });
  const T0 = S.L(9) + 0.6, span = 2.4;
  const loopT = ((t - T0) % span + span) % span;
  screen(ctx); ctx.globalAlpha = k;
  const X = (u) => x0 + (u / span) * gw, Y = (v) => y0 - v * gh * 0.8;
  ctx.fillStyle = PAL.paper3; ctx.fillRect(x0, y0, gw, 3);
  // ținta: treaptă la 0,3 s
  ctx.strokeStyle = PAL.paper2; ctx.lineWidth = 4; ctx.setLineDash([12, 8]); ctx.beginPath(); ctx.moveTo(X(0), Y(0)); ctx.lineTo(X(0.3), Y(0)); ctx.lineTo(X(0.3), Y(1)); ctx.lineTo(X(span), Y(1)); ctx.stroke(); ctx.setLineDash([]);
  const resp = (zeta, u) => { if (u < 0.3) return 0; const tt = u - 0.3; if (zeta === 1) return 1 - (1 + w * tt) * Math.exp(-w * tt); if (zeta < 1) { const wd = w * Math.sqrt(1 - zeta * zeta); return 1 - Math.exp(-zeta * w * tt) * (Math.cos(wd * tt) + (zeta / Math.sqrt(1 - zeta * zeta)) * Math.sin(wd * tt)); } const r1 = -w * (zeta - Math.sqrt(zeta * zeta - 1)), r2 = -w * (zeta + Math.sqrt(zeta * zeta - 1)); return 1 - (r2 * Math.exp(r1 * tt) - r1 * Math.exp(r2 * tt)) / (r2 - r1); };
  const kc = L.prog(t, S.w(9, 'amortizare') - 0.3, S.w(9, 'amortizare') + 0.3);
  const curves = [[0.3, PAL.blood, 'subamortizat: oscilează', S.w(9, 'oscileze') - 0.4], [2.5, PAL.tear, 'supraamortizat: lent', S.w(9, 'repede') - 0.2], [1, PAL.gold, 'critic: SmoothDamp', S.w(9, 'amortizare') - 0.3]];
  curves.forEach(([z, col, lbl, at], i) => {
    const kk = L.prog(t, at, at + 0.4);
    if (kk <= 0) return;
    ctx.globalAlpha = k * kk; ctx.strokeStyle = col; ctx.lineWidth = z === 1 ? 7 : 4; ctx.beginPath();
    for (let j = 0; j <= 160; j++) { const u = (j / 160) * span; ctx.lineTo(X(u), Y(resp(z, u))); }
    ctx.stroke(); ctx.globalAlpha = 1;
    txt(ctx, lbl, X(span) - 10, Y(0.15) + 50 - i * 46 - 140, { size: 30, align: 'right', color: col, alpha: k * kk });
  });
  // punctul care urmărește
  ctx.globalAlpha = k; ctx.fillStyle = PAL.white; ctx.beginPath(); ctx.arc(X(loopT), Y(resp(1, loopT)), 12, 0, 7); ctx.fill(); ctx.globalAlpha = 1;
  txt(ctx, 'poziția camerei în timp', x0, y0 - gh - 60, { size: 32, align: 'left', alpha: k });
  const k10 = L.prog(t, S.L(10) - 0.1, S.L(10) + 0.3) * k;
  if (k10 > 0) {
    txt(ctx, 'smoothTime = 0,22 s', 1280, y0 - gh - 60, { size: 36, font: 'JetBrains Mono', weight: 700, color: PAL.gold, alpha: k10 });
    code(ctx, { x: 200, y: 830, w: 1520, ...snip(S.data, 'cam_follow', ['SmoothDamp']), re: ['SmoothDamp'], k: k10, focus: 1, size: 25 });
  }
}

function trauma(ctx, t, S, k) {
  const T = S.T;
  // trauma(t): fiecare lovitură adaugă, scade liniar cu 1,5/s (ScreenShake.Decay)
  const adds = [[T.hits[0], 0.25], [T.hits[1], 0.55], [T.hits[2], 0.3]];
  const trAt = (u) => { let v = 0, last = S.L(11) - 1; for (const [a, d] of adds) { if (u < a) break; v = Math.max(0, v - 1.5 * (a - last)); v = Math.min(1, v + d); last = a; } return Math.max(0, v - 1.5 * (u - last)); };
  const x0 = 200, y0 = 620, gw = 1520, gh = 380, span = 8;
  const tA = S.L(11) - 0.5;
  panel(ctx, x0 - 50, y0 - gh - 90, gw + 100, gh + 160, { alpha: k });
  const X = (u) => x0 + ((u - tA) / span) * gw, Y = (v) => y0 - v * gh * 0.85;
  screen(ctx); ctx.globalAlpha = k;
  ctx.fillStyle = PAL.paper3; ctx.fillRect(x0, y0, gw, 3);
  const now = Math.min(t, tA + span);
  const sq = L.prog(t, S.w(12, 'pătrat') - 0.3, S.w(12, 'pătrat') + 0.3);
  ctx.strokeStyle = PAL.paper; ctx.lineWidth = 5; ctx.beginPath();
  for (let u = tA; u <= now; u += 0.02) ctx.lineTo(X(u), Y(trAt(u)));
  ctx.stroke();
  if (sq > 0) { ctx.globalAlpha = k * sq; ctx.strokeStyle = PAL.gold; ctx.lineWidth = 7; ctx.beginPath(); for (let u = tA; u <= now; u += 0.02) ctx.lineTo(X(u), Y(trAt(u) ** 2)); ctx.stroke(); }
  ctx.globalAlpha = 1;
  txt(ctx, 'trauma', x0 + 10, y0 - gh - 46, { size: 34, align: 'left', color: PAL.paper, alpha: k });
  if (sq > 0) txt(ctx, 'trauma² = cât se mișcă ecranul', x0 + 220, y0 - gh - 46, { size: 34, align: 'left', color: PAL.gold, alpha: k * sq });
  adds.forEach(([a, d]) => { if (t > a) txt(ctx, `+${String(d).replace('.', ',')}`, X(a), Y(Math.min(1, trAt(a + 0.001))) - 40, { size: 30, weight: 700, color: PAL.blood, alpha: k * L.prog(t, a, a + 0.2) }); });
  // 13: random vs zgomot continuu
  const kn = k * L.prog(t, S.L(13) - 0.2, S.L(13) + 0.3);
  if (kn > 0) {
    panel(ctx, x0 - 50, 700, gw + 100, 220, { alpha: kn });
    const r = L.rng(5);
    screen(ctx); ctx.globalAlpha = kn;
    ctx.strokeStyle = PAL.blood; ctx.lineWidth = 3; ctx.beginPath();
    for (let i = 0; i <= 120; i++) ctx.lineTo(x0 + i * 6, 810 + (r() - 0.5) * 140);
    ctx.stroke();
    ctx.strokeStyle = PAL.poison; ctx.lineWidth = 5; ctx.beginPath();
    for (let i = 0; i <= 120; i++) ctx.lineTo(x0 + 790 + i * 6, 810 + L.noise(i * 0.09 + t * 0.4, 4) * 70);
    ctx.stroke(); ctx.globalAlpha = 1;
    txt(ctx, 'Random: sare', x0 + 360, 735, { size: 30, color: PAL.blood, alpha: kn });
    txt(ctx, 'Perlin: tremură', x0 + 1150, 735, { size: 30, color: PAL.poison, alpha: kn });
  }
  const kc = k * L.prog(t, S.w(12, 'pătrat') + 0.2, S.w(12, 'pătrat') + 0.6) * (1 - L.prog(t, S.L(13) - 0.4, S.L(13) - 0.1));
  if (kc > 0) code(ctx, { x: 200, y: 690, w: 1520, ...snip(S.data, 'shake_tick', ['Trauma \\* Trauma', 'localPosition']), re: ['Trauma \\* Trauma', 'Noise'], k: kc, focus: 1, size: 24 });
}

function hitstop(ctx, t, S, c, k) {
  const T = S.T, I = S.I;
  floor(ctx, I, 250, c);
  // Cain lovește la T.freeze: lumea îngheață 0,1 s (timpul jocului stă), apoi shake-ul se stinge
  const loc = t - T.freeze;
  const frozen = loc >= 0 && loc < 0.12;
  cam(ctx, c); ctx.globalAlpha = k;
  const lunge = loc < -0.15 ? 0 : loc < 0 ? outCubic((loc + 0.15) / 0.15) * 120 : loc < 0.12 ? 120 : Math.max(0, 120 - 120 * inOutQuad(L.clamp((loc - 0.12) / 0.3)));
  unit(ctx, I, 'cain', 200 + lunge, 250, t, { scale: 2, pose: loc > -0.15 && loc < 0.4 ? 'attack' : 'idle' });
  unit(ctx, I, 'gaper', 430 + (loc > 0.12 && loc < 0.3 ? 8 : 0), 250, t, { scale: 2, flash: frozen ? 1 : 0, pose: loc > 0 && loc < 0.5 ? 'hurt' : 'idle' });
  ctx.globalAlpha = 1;
  dmgNumber(ctx, c, '12!', 430, 130, loc - 0.12, { color: PAL.stun, size: 90 });
  if (frozen) { screen(ctx); ctx.fillStyle = 'rgba(255,255,255,0.18)'; ctx.fillRect(0, 0, 1920, 1080); }
  // banda de cadre: înghețul
  const x0 = 360, y0 = 830, fw = 40;
  for (let i = 0; i < 30; i++) {
    const fr = (i - 10) / 30, isF = fr >= 0 && fr < 0.12;
    const cur = Math.abs(loc - fr) < 1 / 60;
    screen(ctx); ctx.globalAlpha = k; ctx.fillStyle = isF ? PAL.tear : cur ? PAL.gold : PAL.b3; ctx.fillRect(x0 + i * fw, y0, fw - 6, 50); ctx.globalAlpha = 1;
  }
  txt(ctx, 'înghețat: 0,1 s', x0 + 10 * fw + 60, y0 - 30, { size: 30, color: PAL.tear, alpha: k * L.prog(t, T.freeze, T.freeze + 0.2) });
  code(ctx, { x: 1100, y: 120, w: 760, ...snip(S.data, 'hitstop', ['Freeze\\(float', 'float real', 'timeScale = 0']), re: ['timeScale = 0'], k: k * L.prog(t, S.w(14, 'greutate') - 0.6, S.w(14, 'greutate')), focus: 1, size: 22 });
}
