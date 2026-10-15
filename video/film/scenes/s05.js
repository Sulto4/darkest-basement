// Scena 5 — Aleatoriu, dar controlat. Seed → aceeași hartă; xorshift pe biți (starea reală a GameRng(7)); de ce nu „% n”;
// fluxuri separate (Fork); Fisher–Yates pe pachetul de start al lui Isaac. Toate numerele vin din portul exact (film/model.js).
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, box, line, PAL, K, toScreen } from '../px.js';
import { wall, vignette } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag, panel } from '../ui.js';
import { card, die } from '../kit.js';
import { GameRng, floorMap } from '../model.js';

const E = L.ease;
const bits = (x) => Array.from({ length: 32 }, (_, i) => (x >>> (31 - i)) & 1);
const hex = (x) => '0x' + (x >>> 0).toString(16).toUpperCase().padStart(8, '0');

export default {
  id: 's05', num: 4, title: 'Aleatoriu, dar controlat', music: 'explore', musicFrom: 30,
  plan: [
    { title: 2.6 }, { line: 1, gap: -0.5 }, { line: 2, gap: 0.5 }, { pause: 0.6 },
    { slot: { id: 's05-seed', label: 'seed 7: meniul, apoi finalul run-ului' }, lines: [3, 3], gap: 0.5, lead: 0.4, tail: 0.5 },
    { line: 4, gap: 0.5 }, { pause: 0.8 }, { line: 5, gap: 0.4 }, { line: 6, gap: 0.7 }, { pause: 0.8 }, { line: 7, gap: 0.6 }, { line: 8, gap: 0.5 }, { pause: 0.5 },
    { line: 9, gap: 0.7 }, { pause: 2.4 }, { line: 10, gap: 0.3 },
  ],
  post: 1.2,
  setup(S) {
    S.slot('s05-seed').shots = [{ key: 'menu', z: [1.15, 1.6], f: [0.05, 0.78] }, { key: 'victory', at: S.w(3, 'reproduci') - 0.3, z: [1.1, 1.5], f: [0.85, 0.12] }];
    S.maps = { a: floorMap(3), b: floorMap(42), c: floorMap(2024), m7: floorMap(7) };
    // xorshift pe starea inițială reală a GameRng(7)
    const r = new GameRng(7); const x0 = r.state;
    const x1 = (x0 ^ (x0 << 13)) >>> 0, x2 = (x1 ^ (x1 >>> 17)) >>> 0, x3 = (x2 ^ (x2 << 5)) >>> 0;
    S.xs = { x0, x1, x2, x3 };
    const chain = []; const r2 = new GameRng(7); for (let i = 0; i < 6; i++) chain.push(r2.next());
    S.chain = chain;
    // fluxurile: seed-urile derivate (FNV-1a ^ seed) și primele valori Range(0, 100)
    const root = new GameRng(7);
    S.streams = ['map', 'combat', 'loot'].map((n) => { const f = root.fork(n); const g = new GameRng(f.seed); return { n, seed: f.seed, vals: Array.from({ length: 6 }, () => g.range(0, 100)) }; });
    { const g = new GameRng(root.fork('combat').seed); g.range(0, 100); S.combatShift = Array.from({ length: 6 }, () => g.range(0, 100)); }
    // Fisher–Yates pe pachetul de start al lui Isaac, cu fluxul de luptă al seed-ului 7
    const deck = ['tear', 'tear', 'yum_heart', 'the_sad_onion', 'spoon_bender'];
    const g = new GameRng(7).fork('combat'), swaps = [], order = deck.map((_, i) => i);
    for (let i = order.length - 1; i > 0; i--) { const j = g.range(0, i + 1); swaps.push([i, j]); [order[i], order[j]] = [order[j], order[i]]; }
    S.deck = deck; S.swaps = swaps;
    const fy0 = S.w(9, 'ultima') - 0.2, fy1 = S.L(10) - 0.2;
    S.swapT = swaps.map((_, k) => L.lerp(fy0, fy1, k / swaps.length));
    S.swapD = (fy1 - fy0) / swaps.length;
  },
  draw(ctx, t, S) {
    const I = S.I;
    const c = wall(ctx, I, t, { dark: 0.6, torches: [[30, 70], [610, 70]] });
    const on = (a, b, ia = 0.4, ob = 0.3) => Math.min(L.prog(t, a, a + ia), 1 - L.prog(t, b - ob, b));
    // ── 1–2: hărți diferite, apoi același seed = aceeași hartă ──
    const k1 = on(S.L(1) + 0.2, S.L(2) + 0.6);
    if (k1 > 0) [['a', 3], ['b', 42], ['c', 2024]].forEach(([key, sd], i) => miniMap(ctx, I, S.maps[key], 380 + i * 580, 470, 99, k1 * E.outBack(L.prog(t, S.L(1) + 0.3 + i * 0.35, S.L(1) + 0.7 + i * 0.35), 1.5), `seed ${sd}`));
    const k2 = on(S.w(2, 'seed') - 0.1, S.L(3) - 0.2);
    if (k2 > 0) {
      const grow = L.prog(t, S.w(2, 'același') - 0.3, S.E(2) + 0.6);
      const n = 1 + Math.floor(grow * 9.99);
      miniMap(ctx, I, S.maps.m7, 560, 470, n, k2, 'seed 7');
      miniMap(ctx, I, S.maps.m7, 1360, 470, n, k2, 'seed 7');
      const eq = L.prog(t, S.E(2) + 0.3, S.E(2) + 0.6) * k2;
      if (eq > 0) txt(ctx, '=', 960, 480, { size: 160, weight: 700, color: PAL.gold, alpha: eq });
    }
    // ── 4: xorshift pe 32 de biți ──
    const k4i = on(S.L(4) - 0.1, S.w(4, 'număr'));
    if (k4i > 0) { panel(ctx, 560, 380, 800, 260, { alpha: k4i }); txt(ctx, 'GameRng', 960, 470, { size: 96, font: 'Micro5', weight: 400, color: PAL.gold, alpha: k4i }); txt(ctx, 'xorshift pe 32 de biți: o stare, trei operații', 960, 565, { size: 36, color: PAL.paper, alpha: k4i }); }
    const k4 = on(S.w(4, 'număr') - 0.3, S.L(5) - 0.05);
    if (k4 > 0) xorshift(ctx, t, S, k4);
    // ── 5: funcție deterministă: același lanț, de două ori ──
    const k5 = on(S.L(5) + 0.1, S.L(6) - 0.1);
    if (k5 > 0) {
      [0, 1].forEach((row) => {
        const y = 400 + row * 200;
        txt(ctx, row ? 'a doua oară' : 'prima oară', 140, y, { size: 32, color: PAL.paper2, align: 'left', alpha: k5 });
        S.chain.slice(0, 4).forEach((v, i) => {
          const at = S.L(5) + 0.2 + i * 0.45 + row * 0.25, k = L.prog(t, at, at + 0.3) * k5;
          if (k <= 0) return;
          tag(ctx, String(v), 520 + i * 360, y, { size: 32, alpha: k, color: row && t > S.w(5, 'deterministă') ? PAL.gold : PAL.paper });
          if (i) txt(ctx, '→', 520 + i * 360 - 180, y, { size: 40, color: PAL.paper3, alpha: k });
        });
      });
      txt(ctx, 'x → xorshift(x): aceeași intrare, aceeași ieșire', 960, 830, { size: 36, color: PAL.paper, alpha: k5 * L.prog(t, S.w(5, 'funcție') - 0.1, S.w(5, 'funcție') + 0.3) });
    }
    // ── 6: restul împărțirii favorizează unele resturi; respingem coada ──
    const k6 = on(S.L(6) - 0.1, S.L(7) - 0.1);
    if (k6 > 0) modulo(ctx, t, S, k6);
    // ── 7–8: fluxuri separate ──
    const k7 = on(S.L(7) - 0.1, S.L(9) - 0.2);
    if (k7 > 0) streams(ctx, t, S, k7);
    // ── 9–10: Fisher–Yates ──
    const k9 = on(S.L(9) - 0.2, S.dur);
    if (k9 > 0) fisher(ctx, t, S, c, k9);
    vignette(ctx, 0.65);
    titleCard(ctx, t, { num: this.num, title: this.title, sub: 'același seed, același run' });
    const C = [[1, 'altfel la fiecare run, dar *repetabil*'], [2, 'un *seed* = un run întreg'], [4, 'xorshift: *deplasări de biți* și XOR'], [5, 'o funcție *deterministă* care arată aleator'],
      [6, '% n favorizează unele resturi: *respingem* coada'], [7, 'trei fluxuri: *hartă*, *luptă*, *recompense*'], [8, 'un zar în plus în luptă: *harta nu se mișcă*'], [9, '*Fisher–Yates*: de la ultima carte spre prima'], [10, '5! = 120 de ordini, *toate la fel de probabile*']];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n === 2 ? S.slot('s05-seed').t0 : n === 9 ? S.L(10) - 0.1 : n < 10 ? S.L(n + 1) - 0.1 : S.dur - 0.2);
  },
  sfx(S) {
    const out = [];
    [0, 1, 2].forEach((i) => out.push({ t: S.L(1) + 0.3 + i * 0.35, id: 'ui_click', gain: 0.3, rate: 1 + i * 0.08 }));
    out.push({ t: S.E(2) + 0.3, id: 'buff', gain: 0.3 });
    out.push({ t: S.slot('s05-seed').t0, id: 'ui_click', gain: 0.35 });
    out.push({ t: S.w(4, 'deplasări'), id: 'card_draw', gain: 0.25 }, { t: S.w(6, 'aruncă'), id: 'debuff', gain: 0.3 });
    out.push({ t: S.w(8, 'zar'), id: 'shuffle', gain: 0.25 });
    S.swapT.forEach((a, k) => out.push({ t: a + 0.05, id: 'card_draw', gain: 0.3, rate: 0.95 + 0.05 * k }));
    out.push({ t: S.L(10), id: 'shuffle', gain: 0.3 });
    return out;
  },
};

// hartă mică (primele n camere), centrată în (x, y) pe ecran
function miniMap(ctx, I, M, x, y, n, k, label) {
  if (k <= 0) return;
  const s = 92, z = E.outBack(L.clamp(k), 1.4);
  screen(ctx); ctx.save(); ctx.globalAlpha = L.clamp(k); ctx.imageSmoothingEnabled = false;
  panel(ctx, x - 250, y - 230, 500, 450, { alpha: L.clamp(k) });
  const px = (gx) => x - 2 * s + gx * s, py = (gy) => y - 1.5 * s + gy * s + 20;
  ctx.strokeStyle = PAL.paper3; ctx.lineWidth = 6; ctx.setLineDash([12, 6]);
  M.corridors.slice(0, Math.max(0, n - 1)).forEach((e) => { const A = M.rooms[e.a], B = M.rooms[e.b]; ctx.beginPath(); ctx.moveTo(px(A.x), py(A.y)); ctx.lineTo(px(B.x), py(B.y)); ctx.stroke(); });
  if (n >= M.rooms.length) M.corridors.slice(M.rooms.length - 1).forEach((e) => { const A = M.rooms[e.a], B = M.rooms[e.b]; ctx.beginPath(); ctx.moveTo(px(A.x), py(A.y)); ctx.lineTo(px(B.x), py(B.y)); ctx.stroke(); });
  ctx.setLineDash([]);
  M.rooms.slice(0, n).forEach((r) => { ctx.drawImage(I('ui/map_cell'), px(r.x) - 32 * z, py(r.y) - 32 * z, 64 * z, 64 * z); const ic = { start: 'map_start', boss: 'map_boss', treasure: 'map_treasure' }[r.type]; if (ic && n >= M.rooms.length || r.type === 'start') ctx.drawImage(I('ui/' + (ic || 'map_start')), px(r.x) - 24, py(r.y) - 24, 48, 48); });
  ctx.restore();
  txt(ctx, label, x, y - 192, { size: 40, weight: 700, color: PAL.gold, alpha: L.clamp(k) });
}

function bitRow(ctx, b, x, y, alpha, o = {}) {
  const { hi = null, col = PAL.paper, w = 50 } = o;
  for (let i = 0; i < 32; i++) {
    const X = x + i * w;
    screen(ctx); ctx.globalAlpha = alpha;
    ctx.fillStyle = b[i] ? (hi && hi[i] ? PAL.gold : col) : 'rgba(42,27,19,0.9)';
    ctx.fillRect(X, y, w - 6, 54);
    ctx.fillStyle = PAL.outline; ctx.fillRect(X, y + 48, w - 6, 6);
    ctx.globalAlpha = 1;
    txt(ctx, String(b[i]), X + (w - 6) / 2, y + 26, { size: 30, font: 'Silkscreen', weight: 400, color: b[i] ? PAL.ink : PAL.paper3, shadow: null, alpha });
  }
}
function xorshift(ctx, t, S, k) {
  const { x0, x1, x2, x3 } = S.xs;
  const t0 = S.w(4, 'număr'), tA = S.w(4, 'deplasări'), tB = S.w(4, 'exclusiv');
  const steps = [[x0, 13, 'l', x1], [x1, 17, 'r', x2], [x2, 5, 'l', x3]];
  const dur = (S.L(5) + 0.2 - tA) / 3;
  let si = Math.min(2, Math.max(0, Math.floor((t - tA) / dur)));
  if (t < tA) si = -1;
  const X = 160;
  panel(ctx, 110, 200, 1700, 500, { alpha: k * 0.95 });
  txt(ctx, 'starea GameRng(7):  x = ' + hex(t < tA ? x0 : steps[si][0]), 160, 250, { size: 38, font: 'JetBrains Mono', weight: 600, color: PAL.paper, align: 'left', alpha: k * L.prog(t, t0 - 0.2, t0 + 0.2) });
  bitRow(ctx, bits(si < 0 ? x0 : steps[si][0]), X, 320, k * L.prog(t, t0 - 0.2, t0 + 0.3));
  if (si >= 0) {
    const [a, n, dir, res] = steps[si];
    const lt = (t - (tA + si * dur)) / dur;
    const sh = dir === 'l' ? (a << n) >>> 0 : a >>> n;
    const slide = E.inOutCubic(L.clamp(lt / 0.35)) * n * (dir === 'l' ? -1 : 1);
    ctx.save(); ctx.beginPath(); ctx.rect(X - 6, 390, 32 * 50 + 6, 80); ctx.clip();
    bitRow(ctx, bits(a), X + slide * 50, 400, k * (1 - L.prog(lt, 0.35, 0.45)), { col: PAL.tear });
    bitRow(ctx, bits(sh), X, 400, k * L.prog(lt, 0.35, 0.45), { col: PAL.tear });
    ctx.restore();
    txt(ctx, `x ${dir === 'l' ? '<<' : '>>'} ${n}`, 160, 500, { size: 34, font: 'JetBrains Mono', weight: 600, color: PAL.tear, align: 'left', alpha: k });
    const xr = L.prog(lt, 0.5, 0.7);
    if (xr > 0) {
      const ra = bits(res), rb = bits(a);
      bitRow(ctx, ra, X, 560, k * xr, { hi: ra.map((v, i) => v !== rb[i]) });
      txt(ctx, `x ^= x ${dir === 'l' ? '<<' : '>>'} ${n}   →   ${hex(res)}`, 160, 660, { size: 34, font: 'JetBrains Mono', weight: 600, color: PAL.gold, align: 'left', alpha: k * xr });
    }
  }
  const sn = snip(S.data, 'rng_next');
  code(ctx, { x: 160, y: 740, w: 1600, ...sn, re: ['x \\^='], k: k * L.prog(t, tB - 0.3, tB + 0.2), focus: 1, size: 30 });
}
function modulo(ctx, t, S, k) {
  const tb = S.w(6, 'restul'), tr = S.w(6, 'aruncă');
  txt(ctx, 'un generator de jucărie: 8 valori, 0…7, și vrem un număr din {0, 1, 2}', 960, 210, { size: 36, color: PAL.paper, alpha: k });
  for (let v = 0; v < 8; v++) {
    const bin = v % 3, slot = Math.floor(v / 3);
    const at = tb + v * 0.18, f = E.outBounce(L.prog(t, at, at + 0.6));
    const x0 = 360 + v * 170, y0 = 330;
    const x1 = 560 + bin * 400 + (slot - 1) * 90, y1 = 640;
    const rej = v < 2 && t > tr;
    const x = L.lerp(x0, x1, f), y = rej ? L.lerp(y1, 1200, E.inCubic(L.prog(t, tr + 0.4, tr + 1.2))) : L.lerp(y0, y1, f);
    screen(ctx); ctx.globalAlpha = k * (rej ? 0.6 : 1);
    ctx.fillStyle = PAL.outline; ctx.beginPath(); ctx.arc(x + 4, y + 4, 40, 0, 7); ctx.fill();
    ctx.fillStyle = rej ? PAL.paper3 : [PAL.gold, PAL.tear, PAL.poison][bin]; ctx.beginPath(); ctx.arc(x, y, 40, 0, 7); ctx.fill();
    ctx.globalAlpha = 1;
    txt(ctx, String(v), x, y + 2, { size: 40, font: 'Silkscreen', weight: 400, color: PAL.ink, shadow: null, alpha: k });
  }
  [0, 1, 2].forEach((b) => {
    const cnt = t > tr + 0.8 ? 2 : b === 2 ? 2 : 3;
    panel(ctx, 560 + b * 400 - 160, 690, 320, 70, { alpha: k * 0.9 });
    txt(ctx, `rest ${b}: ${cnt} valori`, 560 + b * 400, 725, { size: 36, color: cnt === 3 ? PAL.blood : PAL.paper, alpha: k * L.prog(t, tb + 1.6, tb + 2.0) });
  });
  const sn = snip(S.data, 'rng_range');
  code(ctx, { x: 360, y: 790, w: 1200, ...snip(S.data, 'rng_range', ['Respingem', 'uint limit', 'do \\{']), re: ['uint limit', 'do \\{'], k: k * L.prog(t, tr - 0.4, tr), focus: 1, size: 24 });
}
function streams(ctx, t, S, k) {
  const ts = S.w(7, 'principal');
  txt(ctx, 'seed 7', 230, 530, { size: 64, weight: 700, color: PAL.gold, alpha: k * L.prog(t, S.L(7), S.L(7) + 0.3) });
  const names = { map: 'hartă', combat: 'luptă', loot: 'recompense' };
  const tExtra = S.w(8, 'zar');
  S.streams.forEach((st, i) => {
    const y = 330 + i * 200, at = S.w(7, ['hartă', 'luptă', 'recompense'][i]) - 0.2;
    const kk = k * L.prog(t, at, at + 0.4);
    if (kk <= 0) return;
    screen(ctx); ctx.globalAlpha = kk; ctx.strokeStyle = PAL.paper3; ctx.lineWidth = 6; ctx.beginPath(); ctx.moveTo(390, 530); ctx.bezierCurveTo(470, 530, 440, y, 540, y); ctx.stroke(); ctx.globalAlpha = 1;
    tag(ctx, `Fork("${st.n}")`, 680, y, { size: 30, alpha: kk, color: PAL.gold });
    txt(ctx, names[st.n], 680, y + 50, { size: 28, color: PAL.paper2, alpha: kk });
    const shifted = st.n === 'combat' && t > tExtra + 0.3;
    const vals = shifted ? S.combatShift : st.vals;
    vals.forEach((v, j) => {
      const vk = kk * L.prog(t, at + 0.3 + j * 0.12, at + 0.5 + j * 0.12);
      tag(ctx, String(v), 900 + j * 150, y, { size: 32, alpha: vk, color: shifted ? PAL.blood : PAL.paper });
    });
    if (st.n !== 'combat' && t > tExtra + 0.6) txt(ctx, '✓ la fel', 1820, y, { size: 30, color: PAL.poison, alpha: k * L.prog(t, tExtra + 0.6, tExtra + 0.9) });
  });
  const ex = k * L.prog(t, tExtra - 0.1, tExtra + 0.2);
  if (ex > 0) tag(ctx, '+1 aruncare în luptă', 1000, 330 + 200 - 62, { size: 28, alpha: ex, color: PAL.blood });
  const sn = snip(S.data, 'run_fork');
  code(ctx, { x: 160, y: 120, w: 1600, ...snip(S.data, 'run_fork', ['Fork\\("map"\\)']), re: ['Fork'], k: k * L.prog(t, ts - 0.2, ts + 0.2) * (1 - L.prog(t, S.L(8) - 0.3, S.L(8))), focus: 1, size: 25 });
}
function fisher(ctx, t, S, c, k) {
  const pos = S.deck.map((_, i) => i); // poziția fiecărei cărți
  let cur = S.deck.map((_, i) => i); // cur[slot] = indexul cărții
  const anim = {};
  S.swaps.forEach(([i, j], s) => {
    const a = S.swapT[s], p = E.inOutCubic(L.prog(t, a + 0.25, a + 0.25 + S.swapD * 0.6));
    const ci = cur[i], cj = cur[j];
    if (t >= a + 0.25 + S.swapD * 0.6) { cur[i] = cj; cur[j] = ci; }
    else if (t >= a) { anim[ci] = [i, j, p]; anim[cj] = [j, i, p]; }
  });
  let step = -1; S.swapT.forEach((a, s) => { if (t >= a) step = s; });
  const slotX = (s) => 108 + s * 106;
  // cărțile
  const drawn = [];
  for (let s = 0; s < 5; s++) drawn.push([cur[s], s]);
  Object.keys(anim).forEach((ci) => { const idx = drawn.findIndex(([c2]) => c2 === +ci); if (idx >= 0) drawn.splice(idx, 1); });
  drawn.forEach(([ci, s]) => {
    const locked = step >= 0 && s > 4 - (step + (t >= S.swapT[step] + 0.25 + S.swapD * 0.6 ? 1 : 0)) && t > S.w(9, 'ultima');
    card(ctx, S.I, S.deck[ci], slotX(s), 150, c, { s: 1, alpha: k, glow: locked ? 0.7 : 0 });
  });
  Object.entries(anim).forEach(([ci, [from, to, p]]) => {
    const x = L.lerp(slotX(from), slotX(to), p), y = 150 - Math.sin(p * Math.PI) * 36 * (from > to ? 1 : -1);
    card(ctx, S.I, S.deck[ci], x, y, c, { s: 1.05, alpha: k, rot: Math.sin(p * Math.PI) * 0.15 });
  });
  // indicatorii i și j, zarul
  if (step >= 0 && t < S.L(10) - 0.2) {
    const [i, j] = S.swaps[step];
    const [ix, iy] = toScreen(c, slotX(i), 214), [jx, jy] = toScreen(c, slotX(j), 214);
    txt(ctx, 'i', ix, iy + 30, { size: 52, weight: 700, color: PAL.gold, alpha: k });
    const jk = L.prog(t, S.swapT[step] + 0.1, S.swapT[step] + 0.25);
    txt(ctx, 'j', jx + (i === j ? 50 : 0), jy + 30, { size: 52, weight: 700, color: PAL.tear, alpha: k * jk });
    die(ctx, c, 600, 70, S.swaps[step][1], { s: 1.3, alpha: k * jk, rot: (1 - jk) * 2 });
    txt(ctx, `j = Range(0, ${i + 1})`, 1700, 140, { size: 34, font: 'JetBrains Mono', weight: 600, color: PAL.tear, alpha: k * jk });
  }
  const f10 = k * L.prog(t, S.L(10) - 0.1, S.L(10) + 0.3);
  if (f10 > 0) {
    txt(ctx, '5! = 120', 960, 820, { size: 96, weight: 700, color: PAL.gold, alpha: f10 });
    txt(ctx, 'fiecare ordine: probabilitate 1/120', 960, 905, { size: 38, color: PAL.paper, alpha: f10 });
  }
  const sn = snip(S.data, 'rng_shuffle');
  code(ctx, { x: 360, y: 760, w: 1200, ...sn, re: ['Range\\(0, i \\+ 1\\)'], k: k * L.prog(t, S.w(9, 'fisher'), S.w(9, 'fisher') + 0.4) * (1 - f10), focus: 1, size: 26 });
}
