// Scena 8 — Echilibrare prin simulare (Monte Carlo). Grila are 500 de celule = seed-urile 1..500 jucate de AutoPlayer;
// culorile sunt rezultatele REALE (film/data/sim500.json, din tools/sim): victorie sau etajul pe care a murit echipa.
import * as L from '../../engine/lib.js';
import { screen, PAL } from '../px.js';
import { wall, vignette } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag, panel, rich } from '../ui.js';

const E = L.ease;
const GX = 140, GY = 170, CS = 30, GAP = 4, COLS = 25; // grila: 25 × 20
// culorile etajelor din paleta jocului: Caves gri-albăstrui, Depths roșu-carne; victoria aurie
const COL = { win: '#e0b04a', f2: '#6d747c', f3: '#8a2f3a', loss: '#5c0d0d', none: 'rgba(232,220,196,0.12)' };

export default {
  id: 's08', num: 7, title: 'Echilibrare prin simulare', music: 'explore', musicFrom: 60,
  plan: [{ title: 2.6 }, { line: 1, gap: -0.5 }, { line: 2, gap: 0.5 }, { line: 3, gap: 0.5 }, { pause: 0.6 }, { line: 4, gap: 0.4 }, { pause: 0.4 }, { line: 5, gap: 0.6 }, { line: 6, gap: 0.5 }, { pause: 0.6 }, { line: 7, gap: 0.5 }, { line: 8, gap: 0.6 }],
  post: 1.4,
  setup(S) {
    const runs = S.data.sim.runs;
    S.runs = runs;
    // estimatorul p̂_n (proporția de victorii după primele n run-uri)
    let w = 0; S.phat = runs.map((r, i) => (w += r.win) / (i + 1));
    S.wins = w;
    S.T = { fill0: S.w(3, 'simulatorul'), fill1: S.E(3) + 0.4, v1: S.w(5, 'zero'), fix: S.w(6, 'crescut'), win: S.w(6, 'două'), pct: S.w(6, 'patruzeci'), mc: S.w(4, 'monte') };
  },
  draw(ctx, t, S) {
    const I = S.I, T = S.T;
    wall(ctx, I, t, { dark: 0.62, torches: [[18, 80], [622, 80]] });
    const on = (a, b, ia = 0.4, ob = 0.3) => Math.min(L.prog(t, a, a + ia), 1 - L.prog(t, b - ob, b));
    // ── grila de run-uri ──
    const kg = on(S.L(1) + 0.3, S.dur);
    if (kg > 0) {
      panel(ctx, GX - 30, GY - 70, COLS * (CS + GAP) + 56, 20 * (CS + GAP) + 110, { alpha: kg });
      const nFill = Math.floor(500 * E.inOutSine(L.prog(t, T.fill0, T.fill1)));
      const v1 = t > T.v1 && t < T.fix + 0.6; // prima versiune: toate pierdute
      const refill = Math.floor(500 * E.inOutSine(L.prog(t, T.fix + 0.6, T.win - 0.2)));
      screen(ctx); ctx.globalAlpha = kg;
      for (let i = 0; i < 500; i++) {
        const r = S.runs[i], x = GX + (i % COLS) * (CS + GAP), y = GY + Math.floor(i / COLS) * (CS + GAP);
        let col = COL.none;
        const real = r.win ? COL.win : r.floors >= 2 ? COL.f3 : COL.f2;
        if (v1) col = i < Math.floor(500 * L.prog(t, T.v1, T.v1 + 0.8)) ? COL.loss : (i < nFill ? real : COL.none);
        else if (t >= T.fix + 0.6) col = i < refill ? real : COL.loss;
        else if (i < nFill) col = real;
        ctx.fillStyle = col; ctx.fillRect(x, y, CS, CS);
        ctx.fillStyle = 'rgba(27,15,15,0.55)'; ctx.fillRect(x, y + CS - 4, CS, 4);
      }
      ctx.globalAlpha = 1;
      const shown = v1 ? 0 : t >= T.fix + 0.6 ? S.runs.slice(0, refill).filter((r) => r.win).length : S.runs.slice(0, nFill).filter((r) => r.win).length;
      const n = v1 ? 500 : t >= T.fix + 0.6 ? Math.max(refill, 1) : Math.max(nFill, 1);
      txt(ctx, `seed 1…500 · victorii: ${shown} / ${v1 ? 500 : Math.min(500, n)}`, GX - 4, GY - 36, { size: 32, align: 'left', color: PAL.paper, alpha: kg * L.prog(t, T.fill0 - 0.3, T.fill0) });
      // legenda
      [[COL.win, 'victorie'], [COL.f3, 'mort în Depths'], [COL.f2, 'mort în Caves']].forEach(([c, s], j) => {
        const x = GX + j * 260, y = GY + 20 * (CS + GAP) + 26;
        screen(ctx); ctx.globalAlpha = kg; ctx.fillStyle = c; ctx.fillRect(x, y - 12, 24, 24); ctx.globalAlpha = 1;
        txt(ctx, s, x + 36, y, { size: 26, align: 'left', color: PAL.paper2, alpha: kg });
      });
    }
    // ── 2: politica lacomă ──
    const kp = on(S.L(2) - 0.2, S.L(3) + 0.6);
    if (kp > 0) {
      const items = [['ui/intent_attack', 'damage unde contează', S.w(2, 'damage')], ['card/yum_heart', 'se vindecă atunci când e jos', S.w(2, 'vindecă')], ['ui/status_block', 'Block când vine o lovitură', S.w(2, 'block')]];
      items.forEach(([ic, s, at], j) => {
        const k = E.outBack(L.prog(t, at - 0.2, at + 0.2), 1.6) * kp;
        const y = 260 + j * 130;
        panel(ctx, 1050, y - 50, 760, 100, { alpha: L.clamp(k) });
        screen(ctx); ctx.globalAlpha = L.clamp(k); ctx.imageSmoothingEnabled = false; ctx.drawImage(I(ic), 1080, y - 30, 60, ic.startsWith('card') ? 38 : 60); ctx.globalAlpha = 1;
        txt(ctx, s, 1170, y, { size: 36, align: 'left', alpha: L.clamp(k) });
      });
      const sn = snip(S.data, 'ai_score');
      code(ctx, { x: 1050, y: 640, w: 800, ...sn, re: ['score \\+= effective', 'Heal'], k: kp * L.prog(t, S.w(2, 'lacom') - 0.2, S.w(2, 'lacom') + 0.2), focus: 0.6, size: 18, title: 'AutoPlayer' });
    }
    // ── 3: terminalul ──
    const kt = on(S.L(3) + 0.5, S.L(4) - 0.1);
    if (kt > 0) {
      const tl = [['$ tools/sim/run.sh', PAL.paper2], ['Tests: 35 passed, 0 failed.'], ['Runs=500 seeds=1..500'], ['Wins=219 (43.8%)', PAL.gold], ['Normal rounds=3.53  Boss rounds=6.78']];
      panel(ctx, 1040, 170, 820, 400, { alpha: kt, fill: 'rgba(8,6,6,0.96)' });
      txt(ctx, 'câteva secunde, fără Unity', 1450, 230, { size: 40, color: PAL.gold, alpha: kt });
      tl.forEach(([s, c], j) => txt(ctx, s, 1060, 320 + j * 50, { size: 30, font: 'JetBrains Mono', weight: 600, color: c || '#cfe8b0', align: 'left', shadow: null, alpha: kt * L.prog(t, S.L(3) + 0.6 + j * 0.4, S.L(3) + 0.8 + j * 0.4) }));
    }
    // ── 4: estimatorul p̂_n converge, cu banda de încredere ──
    const ke = on(S.L(4) - 0.2, S.L(5) - 0.1);
    if (ke > 0) plot(ctx, t, S, ke);
    // ── 5–6: prima versiune vs după calibrare ──
    const k5 = on(S.L(5) - 0.1, S.L(7) - 0.1);
    if (k5 > 0) {
      const after = t > T.fix + 0.4;
      panel(ctx, 1050, 200, 780, 560, { alpha: k5 });
      txt(ctx, after ? 'după calibrare' : 'prima versiune', 1440, 260, { size: 40, weight: 700, color: after ? PAL.gold : PAL.blood, alpha: k5 });
      if (!after) {
        txt(ctx, '0 / 500 victorii', 1440, 380, { size: 76, weight: 700, color: PAL.blood, alpha: k5 });
        txt(ctx, '2,33 runde pe luptă', 1440, 480, { size: 44, color: PAL.paper, alpha: k5 * L.prog(t, S.w(5, 'runde') - 0.3, S.w(5, 'runde')) });
        txt(ctx, 'prea brutal', 1440, 570, { size: 40, color: PAL.paper2, alpha: k5 * L.prog(t, S.w(5, 'brutal') - 0.2, S.w(5, 'brutal') + 0.1) });
      } else {
        const s1 = L.prog(t, T.fix, T.fix + 0.8), s2 = L.prog(t, S.w(6, 'scăzut') - 0.2, S.w(6, 'scăzut') + 0.6);
        slider(ctx, 'HP inamici', '+50%', 1110, 360, s1, k5, PAL.poison);
        slider(ctx, 'damage inamici', 'mai mic', 1110, 450, -s2 * 0.5, k5, PAL.blood);
        const kw = L.prog(t, T.win - 0.2, T.win + 0.2) * k5;
        txt(ctx, '219 / 500', 1440, 590, { size: 80, weight: 700, color: PAL.gold, alpha: kw });
        txt(ctx, '= 43,8%', 1440, 680, { size: 64, weight: 700, color: PAL.white, alpha: k5 * L.prog(t, T.pct - 0.2, T.pct + 0.2) });
      }
    }
    // ── 7: rundele pe luptă, față de țintă ──
    const k7 = on(S.L(7) - 0.2, S.L(8) - 0.1);
    if (k7 > 0) rounds(ctx, t, S, k7);
    // ── 8: trei loturi, intervale de încredere ──
    const k8 = on(S.L(8) - 0.2, S.dur);
    if (k8 > 0) lots(ctx, t, S, k8);
    vignette(ctx, 0.6);
    titleCard(ctx, t, { num: this.num, title: this.title, sub: 'Monte Carlo: 500 de run-uri jucate de un AI' });
    const C = [[1, 'îl pui pe calculator *să joace*'], [2, 'AutoPlayer: o politică *lacomă*'], [3, '500 de run-uri în *câteva secunde*'], [4, '*Monte Carlo*: estimezi din încercări'], [5, 'prima versiune: *0 din 500*'], [6, 'după calibrare: *43,8%*'], [7, 'runde pe luptă: *în țintă*'], [8, 'eroare de câteva procente: *verifici pe alte seed-uri*']];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n < 8 ? S.L(n + 1) - 0.1 : S.dur - 0.2);
  },
  sfx(S) {
    const T = S.T, out = [];
    for (let i = 0; i < 10; i++) out.push({ t: L.lerp(T.fill0, T.fill1, i / 10), id: 'ui_click', gain: 0.12, rate: 0.9 + i * 0.03 });
    out.push({ t: T.v1, id: 'defeat_sting', gain: 0 }, { t: T.v1, id: 'debuff', gain: 0.35 }, { t: T.fix, id: 'buff', gain: 0.3 }, { t: T.win, id: 'victory_sting', gain: 0.4 });
    return out.filter((c) => c.gain > 0);
  },
};

function slider(ctx, label, val, x, y, k, a, col) {
  txt(ctx, label, x, y, { size: 32, align: 'left', alpha: a });
  screen(ctx); ctx.globalAlpha = a; ctx.fillStyle = PAL.b4; ctx.fillRect(x + 300, y - 6, 300, 12);
  ctx.fillStyle = col; const w = 150 * k; ctx.fillRect(x + 450 + Math.min(0, w), y - 6, Math.abs(w), 12);
  ctx.fillStyle = PAL.paper; ctx.fillRect(x + 450 + w - 8, y - 20, 16, 40); ctx.globalAlpha = 1;
  txt(ctx, val, x + 620, y, { size: 32, weight: 700, color: col, align: 'left', alpha: a * Math.min(1, Math.abs(k) * 3) });
}

// p̂_n pentru n = 1..500, cu banda 95% în jurul lui 0,438
function plot(ctx, t, S, k) {
  const x0 = 1060, y0 = 760, w = 780, h = 460, p = 0.438;
  panel(ctx, x0 - 30, y0 - h - 60, w + 60, h + 130, { alpha: k });
  const X = (n) => x0 + (w * n) / 500, Y = (v) => y0 - h * v;
  screen(ctx); ctx.globalAlpha = k;
  ctx.fillStyle = 'rgba(224,176,74,0.15)'; ctx.beginPath();
  for (let n = 5; n <= 500; n += 5) ctx.lineTo(X(n), Y(Math.min(1, p + 1.96 * Math.sqrt((p * (1 - p)) / n))));
  for (let n = 500; n >= 5; n -= 5) ctx.lineTo(X(n), Y(Math.max(0, p - 1.96 * Math.sqrt((p * (1 - p)) / n))));
  ctx.fill();
  ctx.strokeStyle = PAL.paper3; ctx.lineWidth = 3; ctx.setLineDash([10, 8]); ctx.beginPath(); ctx.moveTo(X(0), Y(p)); ctx.lineTo(X(500), Y(p)); ctx.stroke(); ctx.setLineDash([]);
  const m = Math.floor(500 * E.inOutSine(L.prog(t, S.L(4) + 0.2, S.E(4) + 0.6)));
  ctx.strokeStyle = PAL.gold; ctx.lineWidth = 5; ctx.beginPath();
  for (let n = 1; n <= m; n++) ctx.lineTo(X(n), Y(S.phat[n - 1]));
  ctx.stroke();
  ctx.fillStyle = PAL.paper2; ctx.fillRect(x0, y0, w, 3); ctx.fillRect(x0, y0 - h, 3, h);
  ctx.globalAlpha = 1;
  txt(ctx, 'p̂ după n run-uri', x0 + 10, y0 - h - 24, { size: 30, align: 'left', alpha: k });
  txt(ctx, 'n = 500', x0 + w, y0 + 34, { size: 26, align: 'right', color: PAL.paper2, alpha: k });
  txt(ctx, '0,438', x0 + w + 6, Y(p) - 26, { size: 28, align: 'right', color: PAL.gold, alpha: k });
  if (m > 0) txt(ctx, (S.phat[m - 1] * 100).toFixed(1).replace('.', ',') + '%', X(m) + 12, Y(S.phat[m - 1]) - 30, { size: 30, align: 'left', color: PAL.white, alpha: k });
}

function rounds(ctx, t, S, k) {
  // histograma mediei rundelor pe luptă normală, pe fiecare run (500 de valori), cu ținta 3–4
  const vals = S.runs.map((r) => r.normalRounds).filter((v) => v > 0);
  const bins = new Array(16).fill(0), lo = 2, hi = 6;
  for (const v of vals) bins[Math.min(15, Math.max(0, Math.floor(((v - lo) / (hi - lo)) * 16)))]++;
  const x0 = 1060, y0 = 640, w = 760, h = 330, mx = Math.max(...bins);
  panel(ctx, x0 - 30, y0 - h - 90, w + 60, h + 300, { alpha: k });
  txt(ctx, 'runde pe luptă normală (media fiecărui run)', x0, y0 - h - 50, { size: 30, align: 'left', alpha: k });
  screen(ctx); ctx.globalAlpha = k;
  ctx.fillStyle = 'rgba(124,192,74,0.18)'; ctx.fillRect(x0 + (w * (3 - lo)) / (hi - lo), y0 - h, (w * 1) / (hi - lo), h);
  const g = E.outCubic(L.prog(t, S.L(7), S.L(7) + 1));
  bins.forEach((b, i) => { const bh = (h * b * g) / mx; ctx.fillStyle = PAL.gold; ctx.fillRect(x0 + (i * w) / 16 + 3, y0 - bh, w / 16 - 6, bh); });
  ctx.fillStyle = PAL.paper2; ctx.fillRect(x0, y0, w, 3);
  ctx.globalAlpha = 1;
  [2, 3, 4, 5, 6].forEach((v) => txt(ctx, String(v), x0 + (w * (v - lo)) / (hi - lo), y0 + 30, { size: 26, color: PAL.paper2, alpha: k }));
  txt(ctx, 'media 3,53 · ținta 3–4', x0 + w / 2, y0 + 90, { size: 36, weight: 700, color: PAL.poison, alpha: k * g });
  txt(ctx, 'boss: 6,78 runde · ținta 6–8', x0 + w / 2, y0 + 150, { size: 36, weight: 700, color: PAL.gold, alpha: k * L.prog(t, S.w(7, 'boss') - 0.2, S.w(7, 'boss') + 0.2) });
  txt(ctx, 'Monstro 6,22 · Gurdy 6,59 · Mom 7,72', x0 + w / 2, y0 + 200, { size: 28, color: PAL.paper2, alpha: k * L.prog(t, S.w(7, 'boss') + 0.3, S.w(7, 'boss') + 0.7) });
}

function lots(ctx, t, S, k) {
  const x0 = 1060, w = 760, y0 = 220;
  panel(ctx, x0 - 30, y0 - 70, w + 60, 640, { alpha: k });
  txt(ctx, 'rata de victorie, trei loturi de câte 500', x0, y0 - 30, { size: 30, align: 'left', alpha: k });
  const X = (p) => x0 + 60 + ((p - 0.25) / 0.4) * (w - 120);
  screen(ctx); ctx.globalAlpha = k;
  ctx.fillStyle = 'rgba(124,192,74,0.16)'; ctx.fillRect(X(0.35), y0 + 20, X(0.55) - X(0.35), 420);
  ctx.globalAlpha = 1;
  txt(ctx, 'ținta 35–55%', (X(0.35) + X(0.55)) / 2, y0 + 470, { size: 28, color: PAL.poison, alpha: k });
  [['seed 1–500', 0.438, S.L(8)], ['seed 1001–1500', 0.51, S.w(8, 'alte') - 0.2], ['seed 3001–3500', 0.498, S.w(8, 'alte') + 0.3]].forEach(([lbl, p, at], j) => {
    const kk = L.prog(t, at, at + 0.4) * k; if (kk <= 0) return;
    const y = y0 + 90 + j * 130, e = 1.96 * Math.sqrt((p * (1 - p)) / 500);
    screen(ctx); ctx.globalAlpha = kk; ctx.fillStyle = PAL.paper2; ctx.fillRect(X(p - e), y - 3, X(p + e) - X(p - e), 6); ctx.fillRect(X(p - e), y - 16, 4, 32); ctx.fillRect(X(p + e) - 4, y - 16, 4, 32);
    ctx.fillStyle = PAL.gold; ctx.beginPath(); ctx.arc(X(p), y, 14, 0, 7); ctx.fill(); ctx.globalAlpha = 1;
    txt(ctx, lbl, x0, y - 44, { size: 26, align: 'left', color: PAL.paper2, alpha: kk });
    txt(ctx, (p * 100).toFixed(1).replace('.', ',') + '%', X(p), y - 44, { size: 30, weight: 700, color: PAL.gold, alpha: kk });
  });
  const ks = L.prog(t, S.w(8, 'eroarea') - 0.2, S.w(8, 'eroarea') + 0.3) * k;
  txt(ctx, 'eroarea standard = √(p(1−p)/n) ≈ 2,2%', x0 + w / 2, y0 + 540, { size: 32, font: 'JetBrains Mono', weight: 600, color: PAL.paper, alpha: ks });
}
