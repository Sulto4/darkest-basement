// Scena 11 — Cum l-ai construi tu. Drumul de învățare desenat ca o hartă de etaj: șapte camere, Isaac trece prin fiecare;
// în dreapta, o mică demonstrație a pasului. Regula: fiecare pas se testează singur.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, line, PAL, K, toScreen } from '../px.js';
import { wall, vignette, board } from '../bg.js';
import { txt, titleCard, captionAt, panel, tag, measure } from '../ui.js';
import { unit, card, terminal } from '../kit.js';

const E = L.ease;
const ROOMS = [[70, 78, 'C# în consolă'], [176, 78, 'Unity de bază'], [282, 78, 'coada de evenimente'], [282, 170, 'interfața'], [176, 170, 'camera și mișcarea'], [70, 170, 'harta: BFS'], [70, 262, 'echilibrarea']];
const GOAL = [200, 262];

export default {
  id: 's11', num: 10, title: 'Cum l-ai construi tu', music: 'menu', musicFrom: 8,
  plan: [{ title: 2.6 }, { line: 1, gap: -0.5 }, { line: 2, gap: 0.6 }, { line: 3, gap: 0.6 }, { line: 4, gap: 0.6 }, { line: 5, gap: 0.6 }, { line: 6, gap: 0.6 }, { line: 7, gap: 0.6 }, { line: 8, gap: 0.6 }, { line: 9, gap: 0.7 }],
  post: 1.6,
  draw(ctx, t, S) {
    const I = S.I;
    const c = wall(ctx, I, t, { torches: [[380, 90], [620, 90]] });
    board(ctx, I, 16, 26, 340, 296, c, L.prog(t, S.L(1) - 0.3, S.L(1) + 0.5));
    if (t < S.L(1)) { finish(ctx, t, S); return; }
    // drumul (holuri punctate) și camerele
    cam(ctx, c);
    const pts = [...ROOMS.map(([x, y]) => [x, y]), GOAL];
    for (let i = 0; i < pts.length - 1; i++) {
      const k = L.prog(t, S.L(1) + 0.3 + i * 0.12, S.L(1) + 0.6 + i * 0.12);
      if (k > 0) line(ctx, pts[i][0], pts[i][1], L.lerp(pts[i][0], pts[i + 1][0], k), L.lerp(pts[i][1], pts[i + 1][1], k), PAL.ink, { dash: 4, gap: 3, th: 3 });
    }
    const cur = curStep(S, t);
    ROOMS.forEach(([x, y, lbl], i) => {
      const k = E.outBack(L.prog(t, S.L(1) + 0.2 + i * 0.12, S.L(1) + 0.5 + i * 0.12), 2);
      if (k <= 0) return;
      const done = t > S.L(9) + 0.3 + i * 0.12 || i < cur, act = i === cur;
      cam(ctx, c); ctx.save(); ctx.translate(x, y); ctx.scale(k, k);
      spr(ctx, I(act ? 'ui/map_current' : 'ui/map_cell'), 0, 0, { ax: 0.5, ay: 0.5, sx: 2, sy: 2 });
      ctx.restore();
      const [sx, sy] = toScreen(c, x, y);
      if (!act) txt(ctx, String(i + 1), sx, sy + 2, { size: 54, font: 'Silkscreen', weight: 400, color: done ? PAL.poison : PAL.ink, shadow: null, alpha: L.clamp(k) });
      const [lx, ly] = toScreen(c, x, y + 27);
      const lw = measure(ctx, lbl, 26, 'Inter', 700) + 20;
      screen(ctx); ctx.globalAlpha = L.clamp(k); ctx.fillStyle = PAL.paper; ctx.fillRect(lx - lw / 2, ly - 18, lw, 36); ctx.globalAlpha = 1;
      txt(ctx, lbl, lx, ly, { size: 26, weight: 700, color: act ? PAL.blood2 : PAL.ink, shadow: null, alpha: L.clamp(k) });
    });
    { const [gx, gy] = GOAL; cam(ctx, c); spr(ctx, I('prop/trapdoor'), gx + 30, gy + 8, { ax: 0.5, ay: 0.5, alpha: L.prog(t, S.L(1) + 1, S.L(1) + 1.4) }); const [sx, sy] = toScreen(c, gx + 30, gy + 30); txt(ctx, 'jocul tău', sx, sy, { size: 26, weight: 700, color: PAL.ink, shadow: null, alpha: L.prog(t, S.L(1) + 1, S.L(1) + 1.4) }); }
    // Isaac pe drum
    if (t > S.L(2) - 0.6) {
      const [x, y] = isaacPos(S, t);
      cam(ctx, c); unit(ctx, I, 'isaac', x, y + 10, t, { scale: 1, frame: Math.floor(t * 6) % 2 });
    }
    // demonstrația pasului curent, în dreapta
    detail(ctx, t, S, c, cur);
    // 9: toate testate
    const k9 = L.prog(t, S.w(9, 'testa') - 0.2, S.w(9, 'testa') + 0.3);
    if (k9 > 0) { panel(ctx, 1150, 300, 700, 360, { alpha: k9 }); txt(ctx, 'fiecare pas', 1500, 400, { size: 52, weight: 700, color: PAL.paper, alpha: k9 }); txt(ctx, 'se testează singur', 1500, 480, { size: 52, weight: 700, color: PAL.gold, alpha: k9 }); txt(ctx, '✓ ✓ ✓ ✓ ✓ ✓ ✓', 1500, 575, { size: 48, color: PAL.poison, alpha: k9 }); }
    finish(ctx, t, S);
  },
  sfx(S) {
    const out = [];
    for (let n = 2; n <= 8; n++) out.push({ t: S.L(n) + 0.05, id: 'footstep_stone_' + (n % 2 ? 1 : 2), gain: 0.35 }, { t: S.L(n) + 0.35, id: 'ui_click', gain: 0.3 });
    out.push({ t: S.w(9, 'testa'), id: 'victory_sting', gain: 0.35 });
    return out;
  },
};

const curStep = (S, t) => { let c = -1; for (let n = 2; n <= 8; n++) if (t >= S.L(n) - 0.1) c = n - 2; return t > S.L(9) - 0.2 ? 99 : c; };
function isaacPos(S, t) {
  let i = 0; for (let n = 2; n <= 8; n++) if (t >= S.L(n) - 0.4) i = n - 2;
  if (t > S.L(9) - 0.2) { const k = E.inOutCubic(L.prog(t, S.L(9) - 0.2, S.L(9) + 0.6)); return [L.lerp(ROOMS[6][0], GOAL[0] + 30, k), L.lerp(ROOMS[6][1], GOAL[1], k)]; }
  const prev = i > 0 ? ROOMS[i - 1] : [ROOMS[0][0] - 50, ROOMS[0][1]];
  const k = E.inOutCubic(L.prog(t, S.L(i + 2) - 0.4, S.L(i + 2) + 0.1));
  return [L.lerp(prev[0], ROOMS[i][0], k), L.lerp(prev[1], ROOMS[i][1], k) - Math.sin(k * Math.PI) * 6];
}

function detail(ctx, t, S, c, cur) {
  if (cur < 0 || cur > 6) return;
  const n = cur + 2, a = S.L(n) - 0.1, b = (n < 8 ? S.L(n + 1) : S.L(9)) - 0.15;
  const k = Math.min(L.prog(t, a, a + 0.35), 1 - L.prog(t, b - 0.25, b));
  if (k <= 0) return;
  const I = S.I, x0 = 1150, y0 = 160, w = 700, h = 640;
  panel(ctx, x0, y0, w, h, { alpha: k });
  txt(ctx, `${cur + 1}. ${ROOMS[cur][2]}`, x0 + 30, y0 + 50, { size: 40, weight: 700, color: PAL.gold, align: 'left', alpha: k });
  const lt = t - a;
  if (cur === 0) {
    const log = [['> dotnet run'], ['Runda 1'], ['Isaac joacă Tear → Gaper −5', PAL.paper], ['Gaper: 22 / 27', PAL.blood], ['Gaper mușcă: Cain −3', PAL.paper], ['Runda 2 …', PAL.paper2]];
    log.forEach(([s, col], i) => txt(ctx, s, x0 + 40, y0 + 140 + i * 56, { size: 30, font: 'JetBrains Mono', weight: 600, color: col || '#cfe8b0', align: 'left', shadow: null, alpha: k * L.prog(lt, 0.3 + i * 0.5, 0.5 + i * 0.5) }));
    txt(ctx, 'clase · liste · dicționare', x0 + w / 2, y0 + h - 60, { size: 32, color: PAL.paper2, alpha: k });
  } else if (cur === 1) {
    ['GameObject', '  Transform', '  SpriteRenderer', '  Breathe : MonoBehaviour', '    void Update()'].forEach((s, i) => txt(ctx, s, x0 + 40, y0 + 140 + i * 52, { size: 30, font: 'JetBrains Mono', weight: 600, color: i ? PAL.paper : PAL.gold, align: 'left', shadow: null, alpha: k * L.prog(lt, 0.2 + i * 0.3, 0.4 + i * 0.3) }));
    cam(ctx, { ox: x0 + 520 - 48 * 3, oy: y0 + 470 - 96 * 3 }); ctx.globalAlpha = k;
    unit(ctx, I, 'isaac', 48, 96, t, { scale: 2, shadow: false }); ctx.globalAlpha = 1;
    txt(ctx, 'scale.y = 1 + 0,03 · sin(t)', x0 + w / 2, y0 + h - 60, { size: 30, font: 'JetBrains Mono', weight: 600, color: PAL.tear, alpha: k });
  } else if (cur === 2) {
    const lq = lt % 3.4;
    ['CardPlayed', 'Damaged 5', 'UnitDied'].forEach((s, i) => { const p = L.clamp((lq - 0.4 - i * 0.9) / 0.6); const a = k * (1 - L.prog(lq, 1.0 + i * 0.9, 1.3 + i * 0.9)); if (a > 0.01) { panel(ctx, x0 + 60 + p * 300, y0 + 150 + i * 110, 300, 80, { alpha: a, fill: 'rgba(232,220,196,0.95)' }); txt(ctx, s, x0 + 210 + p * 300, y0 + 190 + i * 110, { size: 28, font: 'JetBrains Mono', weight: 600, color: PAL.ink, shadow: null, alpha: a }); } });
    ['animă', 'animă', 'animă'].forEach((s, i) => { const a = k * Math.min(L.prog(lq, 1.0 + i * 0.9, 1.2 + i * 0.9), 1 - L.prog(lq, 3.0, 3.3)); if (a > 0) txt(ctx, '▶ ' + ['zboară cartea', 'număr + clipire', 'cade monstrul'][i], x0 + 520, y0 + 190 + i * 110, { size: 26, color: PAL.gold, alpha: a }); });
    txt(ctx, 'Model → evenimente → corutină', x0 + w / 2, y0 + h - 60, { size: 32, color: PAL.paper2, alpha: k });
  } else if (cur === 3) {
    const hover = Math.sin(lt * 2.4) > 0 ? 1 : 0, up = E.outBack(L.clamp(((lt * 2.4) % (2 * Math.PI)) / 1.2), 1.6) * hover;
    ['tear', 'spoon_bender', 'the_sad_onion'].forEach((id, i) => { const isH = i === 1; card(ctx, I, id, 430 + i * 50, 200 - (isH ? 18 * up : 0), c.ox !== undefined ? { ox: 0, oy: 0 } : {}, { s: isH ? 1 + 0.15 * up : 1, alpha: k, rot: (i - 1) * 0.12 }); });
    screen(ctx); ctx.globalAlpha = k; ctx.fillStyle = PAL.white; ctx.beginPath(); ctx.moveTo(1520, 520 - 40 * up); ctx.lineTo(1520, 560 - 40 * up); ctx.lineTo(1532, 550 - 40 * up); ctx.closePath(); ctx.fill(); ctx.globalAlpha = 1;
    txt(ctx, 'hover: se ridică și crește ×1,15', x0 + w / 2, y0 + h - 60, { size: 32, color: PAL.paper2, alpha: k });
  } else if (cur === 4) {
    const gx = x0 + 120, gy = y0 + 470, gw = 460, gh = 300;
    screen(ctx); ctx.globalAlpha = k; ctx.fillStyle = PAL.paper3; ctx.fillRect(gx, gy, gw, 3); ctx.fillRect(gx, gy - gh, 3, gh);
    ctx.strokeStyle = PAL.gold; ctx.lineWidth = 6; ctx.beginPath(); for (let i = 0; i <= 50; i++) { const u = i / 50; ctx.lineTo(gx + u * gw, gy - (1 - (1 - u) ** 3) * gh * 0.9); } ctx.stroke(); ctx.globalAlpha = 1;
    txt(ctx, 'lerp · easing · SmoothDamp', x0 + w / 2, y0 + h - 60, { size: 32, color: PAL.paper2, alpha: k });
  } else if (cur === 5) {
    const nodes = [[0, 0, 0], [1, 0, 1], [1, 1, 2], [2, 1, 3], [0, 1, 1]];
    nodes.forEach(([gx, gy, d], i) => { const x = x0 + 180 + gx * 160, y = y0 + 200 + gy * 160, kk = k * L.prog(lt, 0.3 + d * 0.5, 0.5 + d * 0.5); screen(ctx); ctx.globalAlpha = k; ctx.imageSmoothingEnabled = false; ctx.drawImage(I('ui/map_cell'), x - 48, y - 48, 96, 96); ctx.globalAlpha = 1; txt(ctx, String(d), x, y + 2, { size: 52, font: 'Silkscreen', weight: 400, color: ['#e0b04a', '#d98a3a', '#c7612f', '#9e1b1b'][d], alpha: kk }); });
    txt(ctx, 'distanțe din start cu o coadă', x0 + w / 2, y0 + h - 60, { size: 32, color: PAL.paper2, alpha: k });
  } else if (cur === 6) {
    const r = S.data.sim.runs; for (let i = 0; i < 200; i++) { const x = x0 + 60 + (i % 20) * 29, y = y0 + 120 + Math.floor(i / 20) * 29; screen(ctx); ctx.globalAlpha = k * L.prog(lt, i * 0.01, i * 0.01 + 0.2); ctx.fillStyle = r[i].win ? PAL.gold : PAL.blood2; ctx.fillRect(x, y, 24, 24); ctx.globalAlpha = 1; }
    txt(ctx, 'simulezi → statistici → ajustezi', x0 + w / 2, y0 + h - 60, { size: 32, color: PAL.paper2, alpha: k });
  }
}
function finish(ctx, t, S) {
  vignette(ctx, 0.6);
  titleCard(ctx, t, { num: 10, title: 'Cum l-ai construi tu', sub: 'ordinea de învățare' });
  const C = [[1, 'în ce ordine ai învăța?'], [2, '1. *C# simplu*: o luptă în consolă'], [3, '2. *Unity de bază*: GameObject, Update'], [4, '3. *coada de evenimente*'], [5, '4. *interfața*: Canvas, butoane, hover'], [6, '5. *camera și mișcarea*'], [7, '6. *harta*: BFS pe o grilă'], [8, '7. *echilibrarea*: simulare'], [9, 'fiecare pas *se testează singur*']];
  for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n < 9 ? S.L(n + 1) - 0.1 : S.dur - 0.2);
}
