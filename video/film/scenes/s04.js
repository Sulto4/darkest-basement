// Scena 4 — Harta e un graf. Harta REALĂ a seed-ului 7 (port exact al MapGenerator.cs în film/model.js), construită pas cu pas:
// creștere aleatorie din start (arbore de acoperire), o muchie în plus (buclă), BFS din start, boss-ul = cea mai depărtată cameră.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, box, line, ring, PAL, K, toScreen, slice9 } from '../px.js';
import { wall, vignette, board } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag, panel } from '../ui.js';
import { floorMap } from '../model.js';

const E = L.ease;
const cx = (gx) => 64 + 62 * gx, cy = (gy) => 66 + 62 * gy;
const ICON = { start: 'map_start', combat: 'map_combat', treasure: 'map_treasure', bedroom: 'map_bedroom', curio: 'map_curio', boss: 'map_boss', empty: 'map_empty' };

export default {
  id: 's04', num: 3, title: 'Harta e un graf', music: 'explore',
  plan: [
    { title: 2.6 }, { line: 1, gap: -0.5 }, { line: 2, gap: 0.6 }, { line: 3, gap: 0.6 }, { line: 4, gap: 1.2 }, { line: 5, gap: 0.6 }, { line: 6, gap: 0.6 },
    { line: 7, gap: 0.8 }, { line: 8, gap: 0.4 }, { pause: 0.8 }, { line: 9, gap: 0.4 }, { line: 10, gap: 0.6 }, { pause: 0.6 },
    { slot: { id: 's04-harta', label: 'aceeași hartă, în joc (seed 7)' }, dur: 4.0 },
  ],
  post: 0.6,
  setup(S) {
    const M = (S.M = floorMap(7));
    S.slot('s04-harta').shots = [{ key: 'map1', z: [1.0, 1.35], f: [0.5, 0.42] }];
    // momentele: pasul 1 lent, cuvânt cu cuvânt; pașii 2..9 accelerați
    const t1 = S.w(3, 'libere'), tp = S.w(3, 'alegem'), te = S.w(3, 'legăm');
    S.steps = [{ f: t1, p: tp, e: te }];
    const a = te + 0.7, b = S.L(4) + 3.0, n = M.steps.length - 1;
    for (let i = 0; i < n; i++) { const s0 = a + ((b - a) * i) / n, d = (b - a) / n; S.steps.push({ f: s0, p: s0 + d * 0.45, e: s0 + d * 0.6 }); }
    S.extraAt = S.w(5, 'adăugăm'); S.loopAt = S.w(5, 'bucle'); S.pathAt = S.w(5, 'singur');
    // BFS: valurile, legate de cuvintele „zero”, „unu”, „doi”, „tot așa”
    const W = M.dist.waves, w0 = S.w(8, 'zero'), w1 = S.w(8, 'unu'), w2 = S.w(8, 'doi'), w3 = S.w(8, 'tot');
    S.waves = W.map((wv, i) => (i === 0 ? w1 - 0.15 : i <= 3 ? L.lerp(w2 - 0.3, w3 - 0.2, (i - 1) / 3) : L.lerp(w3, S.E(8) + 0.7, (i - 4) / (W.length - 4))));
    S.zeroAt = w0;
    S.bossAt = S.w(9, 'boss'); S.tieAt = S.w(9, 'egalitate');
    S.typeAt = { treasure: S.w(10, 'comoara'), combat: S.w(10, 'lupte'), bedroom: S.w(10, 'pat'), curio: S.w(10, 'curio') };
  },
  draw(ctx, t, S) {
    const I = S.I, M = S.M;
    const bg = wall(ctx, I, t, { torches: [[372, 110], [626, 110]] });
    // când apare codul, harta se micșorează de la ×3 la ×2 (tot un factor întreg) și face loc în dreapta
    const win = (a, b) => Math.min(L.prog(t, a, a + 0.6), 1 - L.prog(t, b - 0.5, b));
    const zk = E.inOutCubic(Math.max(win(S.w(3, 'alegem') + 0.4, S.L(4) + 1.2), win(S.w(6, 'condiție') - 0.4, S.L(7) + 0.1), win(S.w(8, 'coadă') - 0.8, S.L(10) + 0.1)));
    const z = L.lerp(1, 2 / 3, zk);
    const c = { z, ox: bg.ox + L.lerp(0, 12, zk), oy: bg.oy + L.lerp(0, 216, zk) };
    board(ctx, I, 14, 24, 338, 276, c, L.prog(t, S.L(1) - 0.3, S.L(1) + 0.5));
    if (t < S.L(1) + 0.1) { finish(ctx, t, S); return; }
    // grila 5×4: celulele goale
    cam(ctx, c);
    for (let gy = 0; gy < 4; gy++) for (let gx = 0; gx < 5; gx++) {
      const k = L.prog(t, S.L(1) + 0.2 + (gx + gy) * 0.06, S.L(1) + 0.5 + (gx + gy) * 0.06);
      if (k > 0) { ctx.globalAlpha = 0.28 * k; spr(ctx, I('ui/map_cell_fog'), cx(gx), cy(gy), { ax: 0.5, ay: 0.5, sx: 2, sy: 2 }); }
    }
    ctx.globalAlpha = 1;
    // previzualizare la „noduri … muchiile”: harta finală, apoi se șterge pentru generare
    const pv = Math.min(L.prog(t, S.w(1, 'camerele') - 0.2, S.w(1, 'camerele') + 0.2), 1 - L.prog(t, S.L(2) - 0.3, S.L(2)));
    // câte camere/muchii sunt puse acum
    let nRooms = t >= S.L(2) + 0.4 ? 1 : 0, pending = null;
    S.steps.forEach((st, i) => { if (t >= st.p) nRooms = i + 2; else if (t >= st.f && !pending) pending = { i, st }; });
    const placed = M.rooms.slice(0, nRooms);
    const treeEdges = M.corridors.slice(0, Math.max(0, nRooms - 1));
    const extraK = L.prog(t, S.extraAt, S.extraAt + 0.5);
    // muchiile (holurile)
    const glowTree = L.prog(t, S.w(4, 'arbore') - 0.1, S.w(4, 'arbore') + 0.3) * (1 - L.prog(t, S.L(5) - 0.2, S.L(5) + 0.2));
    const path = [[2, 0], [0, 1], [1, 4]];
    const pathK = Math.min(L.prog(t, S.pathAt - 0.1, S.pathAt + 0.3), 1 - L.prog(t, S.extraAt - 0.1, S.extraAt + 0.2));
    const loopK = Math.min(L.prog(t, S.loopAt - 0.1, S.loopAt + 0.3), 1 - L.prog(t, S.L(6) + 0.3, S.L(6) + 0.7));
    const loop = [[0, 1], [1, 4], [2, 4], [0, 2]];
    const has = (arr, a, b) => arr.some(([x, y]) => (x === a && y === b) || (x === b && y === a));
    cam(ctx, c);
    const drawEdge = (a, b, k, col, th = 3) => { const A = M.rooms[a], B = M.rooms[b]; line(ctx, cx(A.x), cy(A.y), L.lerp(cx(A.x), cx(B.x), k), L.lerp(cy(A.y), cy(B.y), k), col, { th, dash: 4, gap: 2 }); };
    if (pv > 0) { ctx.globalAlpha = pv; M.corridors.forEach((e) => drawEdge(e.a, e.b, 1, PAL.ink)); ctx.globalAlpha = 1; }
    treeEdges.forEach((e, i) => {
      const st = S.steps[i]; const k = st ? L.prog(t, st.e, st.e + 0.25) : 1;
      let col = PAL.ink;
      if (glowTree > 0.5) col = PAL.gold2;
      if (pathK > 0.3 && has(path, e.a, e.b)) col = PAL.blood;
      if (loopK > 0.3 && has(loop, e.a, e.b)) col = PAL.gold2;
      drawEdge(e.a, e.b, k, col, col === PAL.ink ? 3 : 4);
    });
    if (extraK > 0) drawEdge(2, 4, extraK, loopK > 0.3 ? PAL.gold2 : t > S.L(7) ? PAL.ink : PAL.blood2, t > S.L(7) ? 3 : 4);
    // frontiera pasului curent: locurile libere (contur auriu), cu multiplicitatea lor
    if (pending) {
      const fr = {}; M.steps[pending.i].frontier.forEach(([x, y]) => (fr[x + ',' + y] = (fr[x + ',' + y] || 0) + 1));
      const fk = L.prog(t, pending.st.f, pending.st.f + 0.2);
      for (const [key, n] of Object.entries(fr)) {
        const [gx, gy] = key.split(',').map(Number);
        cam(ctx, c); ctx.globalAlpha = fk;
        box(ctx, cx(gx) - 16, cy(gy) - 16, 32, 32, PAL.gold, 2);
        ctx.globalAlpha = 1;
        if (n > 1 && pending.i < 3) { const [sx, sy] = toScreen(c, cx(gx) + 12, cy(gy) - 12); txt(ctx, '×' + n, sx, sy, { size: 26, font: 'Silkscreen', weight: 400, color: PAL.gold2, shadow: null, alpha: fk }); }
      }
    }
    // camerele
    const showRooms = pv > 0 ? M.rooms : placed;
    showRooms.forEach((r, i) => {
      const st = i > 0 ? S.steps[i - 1] : null;
      const k = pv > 0 && i >= nRooms ? pv : E.outBack(L.prog(t, st ? st.p : S.L(2) + 0.4, (st ? st.p : S.L(2) + 0.4) + 0.3), 2);
      if (k <= 0) return;
      cam(ctx, c);
      ctx.save(); ctx.translate(cx(r.x), cy(r.y)); ctx.scale(k, k);
      spr(ctx, I('ui/map_cell'), 0, 0, { ax: 0.5, ay: 0.5, sx: 2, sy: 2 });
      const type = roomType(S, t, r, i);
      if (type) spr(ctx, I('ui/' + ICON[type]), 0, 0, { ax: 0.5, ay: 0.5, sx: 2, sy: 2 });
      ctx.restore();
    });
    // fundăturile: steguleț
    const leafK = Math.min(L.prog(t, S.w(6, 'fundături') - 0.1, S.w(6, 'fundături') + 0.2), 1 - L.prog(t, S.L(7) - 0.1, S.L(7) + 0.3));
    if (leafK > 0) for (const id of [6, 7, 8, 9]) { const r = M.rooms[id]; flag(ctx, c, cx(r.x) + 10, cy(r.y) - 12, leafK, t); }
    // BFS: valul, distanțele, coada
    const bfsOn = t > S.zeroAt - 0.1;
    if (bfsOn) {
      const d = M.dist.d;
      const shown = new Set([0]); let popped = -1;
      S.waves.forEach((at, i) => { if (t >= at) { M.dist.waves[i].push.forEach((n) => shown.add(n)); popped = M.dist.waves[i].pop; } });
      const fade = 1 - L.prog(t, S.typeAt.treasure - 0.4, S.typeAt.treasure);
      for (const id of shown) {
        const r = M.rooms[id], born = id === 0 ? S.zeroAt : S.waves.find((_, i) => M.dist.waves[i].push.includes(id));
        const k = E.outBack(L.prog(t, born, born + 0.25), 2) * fade;
        const col = ['#e0b04a', '#d98a3a', '#c7612f', '#9e1b1b'][d[id]];
        cam(ctx, c); ctx.globalAlpha = 0.5 * k; ring(ctx, cx(r.x), cy(r.y), 19 + 6 * (1 - L.clamp(k)), col, 2); ctx.globalAlpha = 1;
        const [sx, sy] = toScreen(c, cx(r.x), cy(r.y) + 1);
        txt(ctx, String(d[id]), sx, sy, { size: 66 * z * L.clamp(k + 0.001), font: 'Silkscreen', weight: 400, color: col, alpha: L.clamp(k), sh: 4 });
      }
      if (popped >= 0 && t < S.L(9)) { const r = M.rooms[popped]; cam(ctx, c); box(ctx, cx(r.x) - 18, cy(r.y) - 18, 36, 36, PAL.blood, 2); }
      // coada (dreapta)
      const qk = Math.min(L.prog(t, S.zeroAt, S.zeroAt + 0.3), 1 - L.prog(t, S.L(9) - 0.2, S.L(9) + 0.3));
      if (qk > 0) {
        let q = [0]; S.waves.forEach((at, i) => { if (t >= at) q = M.dist.waves[i].queue; });
        if (t < S.waves[0]) q = [0];
        panel(ctx, 1110, 110, 760, 150, { alpha: qk });
        txt(ctx, 'coada BFS', 1140, 145, { size: 32, color: PAL.gold, align: 'left', alpha: qk });
        q.forEach((id, j) => {
          const x = 1150 + j * 92, y = 210;
          screen(ctx); ctx.globalAlpha = qk; ctx.imageSmoothingEnabled = false; ctx.drawImage(I('ui/map_cell'), x, y - 32, 64, 64); ctx.globalAlpha = 1;
          txt(ctx, String(d[id]), x + 32, y + 2, { size: 36, font: 'Silkscreen', weight: 400, color: PAL.ink, shadow: null, alpha: qk });
        });
        if (!q.length) txt(ctx, 'goală: gata', 1150, 210, { size: 34, color: PAL.paper2, align: 'left', alpha: qk });
      }
    }
    // „?” — unde punem boss-ul
    const qm = Math.min(E.outBack(L.prog(t, S.w(7, 'boss') - 0.2, S.w(7, 'boss') + 0.2), 2), 1 - L.prog(t, S.L(8) - 0.2, S.L(8) + 0.2));
    if (qm > 0) { const [sx, sy] = toScreen(c, 188, 156); txt(ctx, '?', sx, sy, { size: 220 * qm, color: PAL.blood, weight: 700, alpha: L.clamp(qm), sh: 8 }); }
    // egalitate la distanța 3: două fundături
    const tie = Math.min(L.prog(t, S.tieAt - 0.1, S.tieAt + 0.2), 1 - L.prog(t, S.L(10) - 0.2, S.L(10)));
    if (tie > 0) for (const id of [7, 9]) { const r = M.rooms[id]; cam(ctx, c); box(ctx, cx(r.x) - 20, cy(r.y) - 20, 40, 40, PAL.blood, 2); }
    // contoarele: n camere, n − 1 muchii
    const ck = Math.min(L.prog(t, S.L(2) + 0.2, S.L(2) + 0.6), 1 - L.prog(t, S.L(6) - 0.3, S.L(6)));
    if (ck > 0) {
      const ne = Math.max(0, nRooms - 1) + (extraK > 0.5 ? 1 : 0);
      panel(ctx, 1110, 640, 760, 210, { alpha: ck });
      txt(ctx, `camere:  ${nRooms}`, 1150, 700, { size: 52, align: 'left', color: PAL.paper, alpha: ck });
      txt(ctx, `muchii:  ${ne}`, 1150, 780, { size: 52, align: 'left', color: extraK > 0.5 ? PAL.gold : PAL.paper, alpha: ck });
      const tk = L.prog(t, S.w(4, 'minus') - 0.1, S.w(4, 'minus') + 0.3);
      if (tk > 0) txt(ctx, extraK > 0.5 ? '= n, deci o buclă' : '= n − 1', 1500, 780, { size: 46, align: 'left', color: PAL.gold, alpha: ck * tk });
    }
    // codul: creșterea (3), muchia în plus (6), BFS (8), boss-ul (9)
    const on = (a, b) => Math.min(L.prog(t, a, a + 0.4), 1 - L.prog(t, b - 0.3, b));
    const kg = on(S.w(3, 'alegem') + 0.6, S.L(4) + 1.0);
    if (kg > 0) { const sn = snip(S.data, 'map_grow'); const hl = sn.lines.map((l, i) => (/frontier\.Add|Pick\(frontier\)|Link\(map/.test(l.s) ? i : -1)).filter((i) => i >= 0); code(ctx, { x: 760, y: 110, w: 1120, ...sn, hl, k: kg, focus: 0.8, size: 25 }); }
    const kx = on(S.w(6, 'condiție') - 0.2, S.L(7) - 0.1);
    if (kx > 0) code(ctx, { x: 760, y: 220, w: 1120, ...snip(S.data, 'map_extra'), re: ['leaves < 2'], k: kx, focus: L.prog(t, S.w(6, 'două') - 0.2, S.w(6, 'două') + 0.2), size: 26 });
    const kb = on(S.w(8, 'coadă') - 0.6, S.L(9) + 0.2);
    if (kb > 0) code(ctx, { x: 760, y: 330, w: 1120, ...snip(S.data, 'map_bfs'), re: ['Queue', 'while', 'Dequeue', 'foreach'], k: kb, focus: 0.7, size: 26 });
    const kbo = on(S.L(9) + 0.2, S.L(10) - 0.1);
    if (kbo > 0) code(ctx, { x: 760, y: 330, w: 1120, ...snip(S.data, 'map_boss'), re: ['distances\\[room.Id\\] >'], k: kbo, focus: L.prog(t, S.tieAt - 0.2, S.tieAt + 0.2), size: 26 });
    // etichete „nod” / „muchie” la prima replică
    if (pv > 0) {
      const r = M.rooms[7], [sx, sy] = toScreen(c, cx(r.x), cy(r.y) - 30); tag(ctx, 'nod = cameră', sx - 60, sy - 10, { size: 30, alpha: pv });
      const [ex, ey] = toScreen(c, (cx(2) + cx(3)) / 2, cy(1) - 26); tag(ctx, 'muchie = hol', ex, ey - 6, { size: 30, alpha: pv });
    }
    finish(ctx, t, S);
  },
  sfx(S) {
    const out = [{ t: S.L(2) + 0.4, id: 'ui_click', gain: 0.35 }];
    S.steps.forEach((st, i) => out.push({ t: st.p, id: 'ui_click', gain: i === 0 ? 0.4 : 0.22, rate: 0.9 + i * 0.03 }));
    out.push({ t: S.extraAt, id: 'card_draw', gain: 0.3 });
    S.waves.forEach((at, i) => { if (S.M.dist.waves[i].push.length) out.push({ t: at, id: 'ui_hover', gain: 0.4, rate: 1 + 0.06 * i }); });
    out.push({ t: S.bossAt, id: 'door_boss_open', gain: 0.4 }, { t: S.typeAt.treasure, id: 'chest_open', gain: 0.35 });
    out.push({ t: S.slot('s04-harta').t0, id: 'ui_click', gain: 0.4 });
    return out;
  },
};

// tipul afișat al camerei la momentul t (startul de la început; boss-ul la „boss”; restul la cuvintele lor)
function roomType(S, t, r, i) {
  const type = r.type;
  if (type === 'start') return t > S.zeroAt - 0.15 && t < S.typeAt.treasure - 0.4 ? null : 'start'; // în timpul BFS-ului se vede doar „0”
  if (type === 'boss') return t >= S.bossAt ? 'boss' : null;
  const at = S.typeAt[type];
  if (at == null) return t >= S.typeAt.combat ? type : null;
  return t >= at ? type : null;
}
function flag(ctx, c, x, y, k, t) {
  cam(ctx, c);
  ctx.globalAlpha = L.clamp(k);
  rect(ctx, x, y - 2, 1, 12, PAL.ink);
  const w = Math.round(6 + Math.sin(t * 8 + x) * 1);
  rect(ctx, x + 1, y - 2, w, 4, PAL.blood);
  ctx.globalAlpha = 1;
}
function finish(ctx, t, S) {
  vignette(ctx, 0.7);
  titleCard(ctx, t, { num: 3, title: 'Harta e un graf', sub: 'arbore de acoperire, bucle și BFS' });
  const C = [[1, 'cameră = *nod*, hol = *muchie*'], [2, 'pornim dintr-o cameră *la întâmplare*'], [3, 'locurile libere de lângă hartă: *frontiera*'], [4, 'n camere, n − 1 muchii, conex = *arbore*'],
    [5, 'o muchie în plus = *o buclă*, deci o alegere'], [6, 'rămân măcar *două fundături*'], [7, 'boss-ul: *cât mai departe* de start'], [8, '*BFS*: distanțe cu o coadă'], [9, 'distanța maximă = *boss*'], [10, 'comoara, patul, curio-ul, luptele']];
  for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n < 10 ? S.L(n + 1) - 0.1 : S.E(10) + 0.5);
}
