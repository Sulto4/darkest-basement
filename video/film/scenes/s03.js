// Scena 3 — Jocul ca mașină de stări. Graful stărilor pe pergament, un jeton (Isaac) sare din stare în stare,
// în dreapta captura din joc a stării curente; apoi codul real din GameFlow.cs.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, line, PAL, K, toScreen, slice9, arrowHead } from '../px.js';
import { wall, vignette, board } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag } from '../ui.js';
import { shot } from '../kit.js';

const E = L.ease;
// nodurile (în artă) și muchiile
const N = {
  meniu: [74, 70, 'MENIU'], etaj: [74, 140, 'ETAJ NOU'], harta: [214, 82, 'HARTĂ'], hol: [330, 150, 'HOL'], camera: [292, 250, 'CAMERĂ'],
  lupta: [150, 250, 'LUPTĂ'], recomp: [116, 196, 'RECOMPENSĂ'], over: [64, 296, 'GAME OVER'], vict: [354, 56, 'VICTORIE'],
};
const EDGES = [['meniu', 'etaj'], ['etaj', 'harta'], ['harta', 'hol'], ['hol', 'camera'], ['camera', 'lupta'], ['lupta', 'recomp'], ['recomp', 'harta'], ['lupta', 'over'], ['harta', 'vict'], ['camera', 'harta']];
const SHOT = { meniu: 'menu', etaj: 'floor1', harta: 'map1', hol: 'corridor', camera: 'door', lupta: 'hero_turn', recomp: 'reward', over: 'trap', vict: 'victory' };
const BW = 66, BH = 20;

export default {
  id: 's03', num: 2, title: 'Jocul ca mașină de stări', music: 'menu',
  plan: [{ title: 2.6 }, { line: 1, gap: -0.5 }, { line: 2 }, { line: 3, gap: 0.5 }, { line: 4, gap: 0.6 }, { line: 5, gap: 0.6 }, { line: 6, gap: 0.6 }, { line: 7, gap: 0.6 }],
  post: 1.4,
  setup(S) {
    // drumul jetonului: [moment, nod]
    const p = [[0, 'meniu']];
    for (const [w, n] of [['meniu', 'meniu'], ['hartă', 'harta'], ['hol', 'hol'], ['cameră', 'camera'], ['luptă', 'lupta'], ['recompensă', 'recomp']]) p.push([S.w(3, w), n]);
    p.push([S.w(5, 'hartă') - 0.05, 'harta'], [S.w(5, 'hol') - 0.05, 'hol'], [S.w(5, 'cameră') - 0.05, 'camera'], [S.w(5, 'luptă') - 0.05, 'lupta'], [S.w(5, 'recompensa') - 0.05, 'recomp'], [S.w(5, 'hartă', 1) - 0.05, 'harta']);
    S.path = p;
  },
  draw(ctx, t, S) {
    const I = S.I;
    const c = wall(ctx, I, t, { torches: [[452, 120], [620, 120]] });
    const bIn = L.prog(t, S.L(1) - 0.2, S.L(1) + 0.6);
    const codeOn = (a, b) => Math.min(L.prog(t, a, a + 0.4), 1 - L.prog(t, b - 0.3, b));
    const dimB = Math.max(codeOn(S.L(4) - 0.1, S.L(5) - 0.1), codeOn(S.L(6) - 0.1, S.dur));
    board(ctx, I, 14, 22, 404, 312, c, bIn);
    if (bIn > 0.3) {
      // muchiile se trag pe rând, apoi nodurile
      EDGES.forEach(([a, b], i) => {
        const k = L.prog(t, S.L(1) + 0.3 + i * 0.22, S.L(1) + 0.6 + i * 0.22);
        if (k > 0) edge(ctx, c, N[a], N[b], k, curNode(S, t) === b && prevNode(S, t) === a ? PAL.blood2 : PAL.ink);
      });
      Object.entries(N).forEach(([id, [x, y, s]], i) => {
        const k = E.outBack(L.prog(t, S.L(1) + 0.15 + i * 0.18, S.L(1) + 0.45 + i * 0.18), 2);
        if (k <= 0) return;
        const on = curNode(S, t) === id && t > S.L(3) - 0.2;
        const pulse = 1 + 0.08 * Math.sin(Math.PI * L.clamp((t - S.w(2, 'automat')) / 0.5)) * (t > S.w(2, 'automat') ? 1 : 0);
        cam(ctx, c);
        ctx.save(); ctx.translate(x, y); ctx.scale(k * pulse, k * pulse);
        slice9(ctx, I(on ? 'ui/button_hover' : 'ui/button_normal'), -BW / 2, -BH / 2, BW, BH, 5);
        ctx.restore();
        const [sx, sy] = toScreen(c, x, y);
        txt(ctx, s, sx, sy + 1, { size: 25 * k, weight: 700, color: on ? PAL.blood2 : PAL.ink, shadow: null, alpha: L.clamp(k) });
      });
      // jetonul „ești aici”
      if (t > S.L(3) - 0.4) {
        const [x, y] = tokenPos(S, t);
        cam(ctx, c);
        spr(ctx, I('ui/portrait_frame_active'), x, y - 25, { ax: 0.5, ay: 0.5 });
        spr(ctx, I('isaac/portrait'), x, y - 24, { ax: 0.5, ay: 0.5 });
      }
      const dk = L.prog(t, S.w(2, 'automat') + 0.3, S.w(2, 'automat') + 0.7);
      if (dk > 0) { const [sx, sy] = toScreen(c, 300, 300); txt(ctx, 'δ(stare, intrare) = stare nouă', sx, sy, { size: 30, font: 'JetBrains Mono', weight: 600, color: PAL.ink, shadow: null, alpha: dk }); }
      if (dimB > 0) { screen(ctx); ctx.fillStyle = `rgba(11,7,7,${0.55 * dimB})`; ctx.fillRect(0, 0, 1920, 1080); }
    }
    // dreapta: captura stării curente
    const pip = Math.min(L.prog(t, S.L(3) - 0.3, S.L(3) + 0.2), 1 - dimB);
    if (pip > 0) {
      const n = curNode(S, t), since = t - lastHop(S, t);
      shot(ctx, I, SHOT[n], 1300, 300, 560, 315, { k: pip, p: L.clamp(since / 2.5), z0: 1.0, z1: 1.06, alpha: 1 });
      txt(ctx, N[n][2], 1580, 690, { size: 72, font: 'Micro5', weight: 400, color: PAL.gold, alpha: pip });
      if (since < 0.12) { screen(ctx); ctx.fillStyle = `rgba(255,246,232,${0.5 * (1 - since / 0.12) * pip})`; ctx.fillRect(1300, 300, 560, 315); }
    }
    // codul
    const k4 = codeOn(S.L(4) - 0.1, S.L(5) - 0.1);
    if (k4 > 0) {
      const sn = snip(S.data, 'flow_machine');
      const re = t < S.w(4, 'următoare') - 0.3 ? ['while \\(_next'] : ['yield return Safe'];
      code(ctx, { x: 820, y: 230, w: 1040, ...sn, re, k: k4, focus: L.prog(t, S.w(4, 'corutină') - 0.2, S.w(4, 'corutină') + 0.2), size: 30 });
    }
    const k6 = codeOn(S.L(6) - 0.1, S.L(7) - 0.1);
    if (k6 > 0) {
      const sn = snip(S.data, 'flow_routine');
      code(ctx, { x: 820, y: 200, w: 1040, ...sn, re: ['switch', 'case'], k: k6, focus: L.prog(t, S.w(6, 'switch') - 0.2, S.w(6, 'switch') + 0.2), size: 28 });
    }
    const k7 = codeOn(S.L(7) - 0.1, S.dur);
    if (k7 > 0) {
      const sn = snip(S.data, 'flow_comment');
      code(ctx, { x: 160, y: 260, w: 1600, ...sn, re: ['Modelul decide', 'GameFlow doar'], k: k7, focus: L.prog(t, S.w(7, 'modelul') - 0.2, S.w(7, 'modelul') + 0.3), size: 28 });
      const a = E.outBack(L.prog(t, S.w(7, 'decide') - 0.1, S.w(7, 'decide') + 0.3), 2), b = E.outBack(L.prog(t, S.w(7, 'pune') - 0.1, S.w(7, 'pune') + 0.3), 2);
      tag(ctx, 'Modelul: ce e în cameră, cine moare', 620, 640, { size: 34, alpha: L.clamp(a) * k7, color: PAL.gold });
      tag(ctx, 'GameFlow: pune în scenă, așteaptă alegerea', 1300, 640, { size: 34, alpha: L.clamp(b) * k7 });
    }
    vignette(ctx, 0.7);
    titleCard(ctx, t, { num: this.num, title: this.title, sub: 'la orice moment, o singură stare' });
    const C = [[1, 'cum curge jocul?'], [2, '*automat finit*: stări și tranziții'], [3, 'ești mereu într-o *singură* stare'], [4, 'fiecare stare = *o corutină*'], [5, 'bucla de bază a jocului'], [6, 'un *while* cu un *switch*'], [7, 'Modelul *decide*, GameFlow *pune în scenă*']];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n < 7 ? S.L(n + 1) - 0.1 : S.dur - 0.2);
  },
  sfx(S) {
    const out = [{ t: S.L(1) + 0.1, id: 'card_draw', gain: 0.3 }];
    for (const [at, n] of S.path.slice(1)) out.push({ t: at, id: 'ui_click', gain: 0.28, rate: 0.95 + 0.04 * Object.keys(N).indexOf(n) });
    out.push({ t: S.L(4) - 0.1, id: 'card_draw', gain: 0.3 }, { t: S.L(6) - 0.1, id: 'card_draw', gain: 0.3 }, { t: S.L(7) - 0.1, id: 'card_draw', gain: 0.3 });
    return out;
  },
};

const curNode = (S, t) => { let n = S.path[0][1]; for (const [a, id] of S.path) if (t >= a) n = id; return n; };
const prevNode = (S, t) => { let p = null, n = S.path[0][1]; for (const [a, id] of S.path) if (t >= a) { p = n; n = id; } return p; };
const lastHop = (S, t) => { let h = 0; for (const [a] of S.path) if (t >= a) h = a; return h; };
function tokenPos(S, t) {
  let i = 0; for (let j = 0; j < S.path.length; j++) if (t >= S.path[j][0]) i = j;
  const [a, id] = S.path[i], [x1, y1] = N[id];
  const prev = i > 0 ? N[S.path[i - 1][1]] : N[id];
  const k = E.inOutCubic(L.clamp((t - a) / 0.28));
  return [L.lerp(prev[0], x1, k), L.lerp(prev[1], y1, k) - Math.sin(k * Math.PI) * 14];
}
// muchie de la marginea unui nod la marginea celuilalt, cu vârf de săgeată
function edge(ctx, c, A, B, k, col) {
  const [x0, y0] = A, [x1, y1] = B;
  const clip = (dx, dy) => { const sx = dx ? (BW / 2 + 2) / Math.abs(dx) : 1e9, sy = dy ? (BH / 2 + 2) / Math.abs(dy) : 1e9; return Math.min(sx, sy); };
  const dx = x1 - x0, dy = y1 - y0, s0 = clip(dx, dy);
  const ax = x0 + dx * s0, ay = y0 + dy * s0, bx = x1 - dx * s0, by = y1 - dy * s0;
  cam(ctx, c);
  const ex = L.lerp(ax, bx, k), ey = L.lerp(ay, by, k);
  line(ctx, ax, ay, ex, ey, col, { th: 2 });
  if (k > 0.95) arrowHead(ctx, bx, by, Math.abs(dx) > Math.abs(dy) ? (dx > 0 ? 'r' : 'l') : dy > 0 ? 'd' : 'u', col, 4);
}
