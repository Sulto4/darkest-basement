// Scena 10 — 3D care arată a pixel art. Billboard = un quad paralel cu planul camerei (f = forward, f.y = 0, normalizat);
// arcul camerei la intrarea în luptă; PPU 32, UI la 640×360 mărit cu un factor întreg; camera de luptă vede 10 unități.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, PAL, K } from '../px.js';
import { wall, vignette } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag, panel } from '../ui.js';
import { shot } from '../kit.js';

const E = L.ease;
let CROP = null; // fața lui Isaac (decupaj din side_idle_0), pentru demonstrația de scalare

export default {
  id: 's10', num: 9, title: '3D care arată a pixel art', music: 'explore', musicFrom: 10,
  plan: [
    { title: 2.6 }, { line: 1, gap: -0.5 }, { line: 2, gap: 0.5 }, { pause: 0.5 }, { line: 3, gap: 0.5 }, { line: 4, gap: 0.6 }, { pause: 0.4 },
    { slot: { id: 's10-arc', label: 'în joc: camera trece din spate în lateral' }, dur: 4.0 },
    { line: 5, gap: 0.6 }, { line: 6, gap: 0.5 }, { pause: 0.4 }, { line: 7, gap: 0.5 }, { pause: 0.4 }, { line: 8, gap: 0.6 },
  ],
  post: 1.4,
  setup(S) {
    S.slot('s10-arc').shots = [{ key: 'room_combat', z: [1.0, 1.06] }, { key: 'side_view', z: [1.06, 1.0] }];
    const img = S.I('isaac/side_idle_0');
    if (typeof document === 'undefined') return; // verificarea din Node (tools/check.mjs) n-are canvas
    CROP = document.createElement('canvas'); CROP.width = 16; CROP.height = 12;
    const g = CROP.getContext('2d'); g.imageSmoothingEnabled = false; g.drawImage(img, 22, 14, 16, 12, 0, 0, 16, 12);
  },
  draw(ctx, t, S) {
    const I = S.I;
    const c = wall(ctx, I, t, { dark: 0.62, torches: [[18, 80], [622, 80]] });
    const on = (a, b, ia = 0.4, ob = 0.3) => Math.min(L.prog(t, a, a + ia), 1 - L.prog(t, b - ob, b));
    // ── 1–3: billboard-uri, de sus ──
    const kB = on(S.L(1) - 0.2, S.L(4) - 0.1);
    if (kB > 0) {
      shot(ctx, I, 'corridor', 120, 160, 720, 405, { k: kB * L.prog(t, S.L(1), S.L(1) + 0.4), p: L.prog(t, S.L(1), S.L(2) + 2), z0: 1.0, z1: 1.25, fx: 0.42, fy: 0.75 });
      txt(ctx, 'ce vezi în joc', 480, 610, { size: 30, color: PAL.paper2, alpha: kB });
      topDown(ctx, t, S, kB);
    }
    // ── 4: arcul camerei ──
    const kA = on(S.L(4) - 0.2, S.slot('s10-arc').t0);
    if (kA > 0) arc(ctx, t, S, kA);
    // ── 5–7: pixelii ──
    const kP = on(S.L(5) - 0.2, S.L(8) - 0.1);
    if (kP > 0) pixels(ctx, t, S, c, kP);
    // ── 8: camera de luptă: 10 unități ──
    const k8 = on(S.L(8) - 0.2, S.dur);
    if (k8 > 0) {
      shot(ctx, I, 'side_view', 160, 140, 1600, 900 * 0.75, { k: k8, z0: 1, z1: 1, p: 0 });
      const x0 = 160, w = 1600, y = 140 + 675 - 60;
      screen(ctx); ctx.globalAlpha = k8;
      for (let i = 0; i <= 10; i++) { ctx.fillStyle = i % 5 ? PAL.paper : PAL.gold; ctx.fillRect(x0 + (i * w) / 10 - 2, y - (i % 5 ? 14 : 24), 4, i % 5 ? 28 : 48); }
      ctx.fillStyle = PAL.paper; ctx.fillRect(x0, y - 2, w * L.prog(t, S.w(8, 'zece') - 0.3, S.w(8, 'zece') + 0.5), 4);
      ctx.globalAlpha = 1;
      tag(ctx, '10 unități = 640 px  →  64 px pe unitate = 2 × 2 pe pixel de artă', 960, 870, { size: 34, alpha: k8 * L.prog(t, S.w(8, 'doi') - 0.4, S.w(8, 'doi')), color: PAL.gold });
    }
    vignette(ctx, 0.6);
    titleCard(ctx, t, { num: this.num, title: this.title, sub: 'billboard-uri, vectori și scalare întreagă' });
    const C = [[1, 'personajele sunt *plate*: billboard-uri'], [2, 'f = forward, *f.y = 0*, normalizat'], [3, '*paralel* cu planul camerei, nu spre ea'], [4, 'unghi, rază și înălțime: *interpolate separat*'],
      [5, '32 px = 1 unitate · UI la *640×360*'], [6, 'un factor *întreg*: ×3 pe Full HD'], [7, '×2,5: pixeli *inegali*'], [8, 'camera de luptă: *10 unități* în lățime']];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n === 4 ? S.slot('s10-arc').t0 : n < 8 ? S.L(n + 1) - 0.1 : S.dur - 0.2);
  },
  sfx(S) {
    return [{ t: S.L(1) + 0.1, id: 'footstep_stone_1', gain: 0.3 }, { t: S.L(1) + 0.5, id: 'footstep_stone_2', gain: 0.3 }, { t: S.w(3, 'paralel'), id: 'ui_click', gain: 0.3 },
      { t: S.slot('s10-arc').t0, id: 'turn_start', gain: 0.45 }, { t: S.slot('s10-arc').t0 + 2.0, id: 'gaper_groan', gain: 0.35 }, { t: S.w(6, 'trei'), id: 'ui_click', gain: 0.3 }, { t: S.w(7, 'doi'), id: 'debuff', gain: 0.3 }];
  },
};

// Vedere de sus: holul, camera cu direcția f, patru quad-uri (eroii). Camera se rotește; quad-urile rămân perpendiculare pe f.
function topDown(ctx, t, S, k) {
  const ox = 1340, oy = 560, sc = 1;
  panel(ctx, 920, 130, 880, 800, { alpha: k, fill: 'rgba(232,220,196,0.96)', edge: PAL.paper3, hi: PAL.paper });
  const yaw = t > S.L(2) ? Math.sin((t - S.L(2)) * 0.9) * 0.45 * L.prog(t, S.L(2), S.L(2) + 1) : 0;
  const f = [Math.sin(yaw), -Math.cos(yaw)]; // forward pe podea (sus pe ecran = înainte)
  const camP = [ox, oy + 300];
  const heroes = [[-60, -60], [60, -60], [-60, 40], [60, 40]].map(([x, y]) => [ox + x, oy + y]);
  const lookAt = t > S.L(3) - 0.2 && t < S.L(4) - 0.2 ? E.inOutCubic(L.prog(t, S.L(3) - 0.2, S.L(3) + 0.4)) * (1 - L.prog(t, S.w(3, 'paralel') - 0.2, S.w(3, 'paralel') + 0.4)) : 0;
  screen(ctx); ctx.save(); ctx.globalAlpha = k;
  // pereții holului
  ctx.fillStyle = PAL.b3; ctx.fillRect(ox - 200, 170, 14, 720); ctx.fillRect(ox + 186, 170, 14, 720);
  // conul camerei
  ctx.strokeStyle = 'rgba(42,27,19,0.4)'; ctx.lineWidth = 3; ctx.setLineDash([10, 8]);
  for (const s of [-1, 1]) { const a = yaw + s * 0.45; ctx.beginPath(); ctx.moveTo(...camP); ctx.lineTo(camP[0] + Math.sin(a) * 700, camP[1] - Math.cos(a) * 700); ctx.stroke(); }
  ctx.setLineDash([]);
  // vectorul f
  ctx.strokeStyle = PAL.blood; ctx.lineWidth = 7; ctx.beginPath(); ctx.moveTo(...camP); ctx.lineTo(camP[0] + f[0] * 160, camP[1] + f[1] * 160); ctx.stroke();
  // quad-urile: segmente de 70 px; paralele cu planul camerei (⊥ f) sau orientate spre poziția camerei
  heroes.forEach(([x, y]) => {
    const toCam = Math.atan2(camP[0] - x, -(camP[1] - y)); // unghiul direcției spre cameră
    const ang = L.lerp(yaw, -toCam + Math.PI * 0, lookAt);
    const dirx = Math.cos(ang), diry = Math.sin(ang);
    ctx.strokeStyle = PAL.ink; ctx.lineWidth = 10; ctx.beginPath(); ctx.moveTo(x - dirx * 38, y - diry * 38); ctx.lineTo(x + dirx * 38, y + diry * 38); ctx.stroke();
  });
  ctx.restore();
  // sprite-urile din spate deasupra quad-urilor
  heroes.forEach(([x, y], i) => { cam(ctx, { ox: x - 48, oy: y - 102, z: 2 / 3 }); ctx.globalAlpha = k * 0.95; spr(ctx, S.I(['isaac', 'cain', 'lilith', 'azazel'][i] + '/back_idle_0'), 24, 48, {}); ctx.globalAlpha = 1; });
  // camera: un pătrat cu obiectiv
  screen(ctx); ctx.globalAlpha = k; ctx.fillStyle = PAL.ink; ctx.fillRect(camP[0] - 26, camP[1] - 18, 52, 40); ctx.fillStyle = PAL.paper2; ctx.fillRect(camP[0] - 8, camP[1] - 30, 16, 14); ctx.globalAlpha = 1;
  txt(ctx, 'camera', camP[0], camP[1] + 50, { size: 28, color: PAL.ink, shadow: null, alpha: k });
  txt(ctx, 'f', camP[0] + f[0] * 180 + 20, camP[1] + f[1] * 180, { size: 40, font: 'JetBrains Mono', weight: 700, color: PAL.blood, shadow: null, alpha: k });
  txt(ctx, 'vedere de sus', 1360, 170, { size: 30, color: PAL.ink, shadow: null, alpha: k });
  if (lookAt > 0.05) txt(ctx, '„privește spre cameră”: pe margini se rotesc diferit', 1360, 890, { size: 28, color: PAL.blood, shadow: null, alpha: k * lookAt });
  else if (t > S.w(3, 'paralel')) txt(ctx, 'toate paralele cu planul camerei', 1360, 890, { size: 28, color: PAL.ink, shadow: null, alpha: k });
  const kc = L.prog(t, S.w(2, 'direcția') - 0.3, S.w(2, 'direcția') + 0.2) * (1 - L.prog(t, S.L(3) - 0.4, S.L(3) - 0.1));
  if (kc > 0) code(ctx, { x: 100, y: 640, w: 780, ...snip(S.data, 'billboard', ['Vector3 f =', 'f.y = 0f', 'sqrMagnitude']), re: ['f.y = 0f', 'LookRotation'], k: kc * k, focus: 1, size: 21 });
}

function arc(ctx, t, S, k) {
  // vedere de sus: pivotul (scena de luptă), camera pe un arc din spate (jos) spre lateral (stânga)
  const px = 560, py = 470, ra = 300, rb = 220;
  panel(ctx, 110, 120, 900, 720, { alpha: k, fill: 'rgba(232,220,196,0.97)', edge: PAL.paper3, hi: PAL.paper });
  const cyc = (((t - S.L(4) - 0.3) % 2.8) + 2.8) % 2.8, kk = E.inOutCubic(Math.min(1, cyc / 2.0)); // 2 s de mișcare, 0,8 s pauză
  const a0 = Math.PI / 2, d = Math.PI / 2; // de la „spate” (jos) la „lateral” (stânga)
  screen(ctx); ctx.globalAlpha = k;
  ctx.strokeStyle = 'rgba(42,27,19,0.35)'; ctx.lineWidth = 3; ctx.setLineDash([10, 8]); ctx.beginPath();
  for (let i = 0; i <= 40; i++) { const u = i / 40, an = a0 + d * u, r = L.lerp(ra, rb, u); ctx.lineTo(px + Math.cos(an) * r, py + Math.sin(an) * r); }
  ctx.stroke(); ctx.setLineDash([]);
  const an = a0 + d * kk, r = L.lerp(ra, rb, kk), cx = px + Math.cos(an) * r, cy = py + Math.sin(an) * r;
  ctx.fillStyle = PAL.blood; ctx.beginPath(); ctx.arc(px, py, 12, 0, 7); ctx.fill();
  ctx.strokeStyle = PAL.ink; ctx.lineWidth = 4; ctx.beginPath(); ctx.moveTo(px, py); ctx.lineTo(cx, cy); ctx.stroke();
  // camera: un dreptunghi rotit spre pivot
  ctx.save(); ctx.translate(cx, cy); ctx.rotate(an + Math.PI / 2); ctx.fillStyle = PAL.ink; ctx.fillRect(-24, -16, 48, 32); ctx.fillStyle = PAL.paper2; ctx.fillRect(-7, -28, 14, 12); ctx.restore();
  ctx.globalAlpha = 1;
  txt(ctx, 'pivot', px + 50, py - 30, { size: 28, color: PAL.ink, shadow: null, alpha: k });
  const mx = (px + cx) / 2, my = (py + cy) / 2, nx = -(cy - py) / r, ny = (cx - px) / r;
  txt(ctx, 'r', mx + nx * 26, my + ny * 26, { size: 40, font: 'JetBrains Mono', weight: 700, color: PAL.ink, shadow: null, alpha: k });
  txt(ctx, 'din spate', px, py + ra + 40, { size: 26, color: PAL.ink, shadow: null, alpha: k });
  txt(ctx, 'lateral', px - rb - 80, py - 40, { size: 26, color: PAL.ink, shadow: null, alpha: k });
  // înălțimea pe parcurs: lerp + 0,9·sin(πk) (o mică „respirație” de macara)
  const gx = 700, gy = 300, gw = 260, gh = 120;
  screen(ctx); ctx.globalAlpha = k; ctx.fillStyle = 'rgba(42,27,19,0.08)'; ctx.fillRect(gx - 20, gy - gh - 40, gw + 40, gh + 80);
  ctx.strokeStyle = PAL.ink; ctx.lineWidth = 4; ctx.beginPath();
  for (let i = 0; i <= 40; i++) { const u = i / 40; ctx.lineTo(gx + u * gw, gy - (0.25 + 0.25 * u + 0.5 * Math.sin(Math.PI * u)) * gh); }
  ctx.stroke();
  ctx.fillStyle = PAL.blood; ctx.beginPath(); ctx.arc(gx + kk * gw, gy - (0.25 + 0.25 * kk + 0.5 * Math.sin(Math.PI * kk)) * gh, 9, 0, 7); ctx.fill(); ctx.globalAlpha = 1;
  txt(ctx, 'înălțimea h(k)', gx + gw / 2, gy + 26, { size: 24, color: PAL.ink, shadow: null, alpha: k });
  // dreapta: ce vede camera (capturi din joc, sincron cu poziția de pe arc), apoi codul
  const kc = L.prog(t, S.w(4, 'interpolează') - 0.3, S.w(4, 'interpolează') + 0.2);
  const pip = k * (1 - kc);
  if (pip > 0) {
    shot(ctx, S.I, 'room_combat', 1080, 150, 760, 428, { k: pip, z0: 1, z1: 1, p: 0, alpha: 1 - L.clamp((kk - 0.35) / 0.3) });
    shot(ctx, S.I, 'side_view', 1080, 150, 760, 428, { k: pip, z0: 1, z1: 1, p: 0, alpha: L.clamp((kk - 0.35) / 0.3) });
    txt(ctx, 'ce vede camera', 1460, 610, { size: 28, color: PAL.paper2, alpha: pip });
  }
  if (kc > 0) code(ctx, { x: 1060, y: 140, w: 800, ...snip(S.data, 'cam_orbit', ['float angle', 'float radius', 'float height']), re: ['Sin\\(k'], k: k * kc, focus: 1, size: 21 });
  const F = [['θ(k) = θa + Δθ · k', S.w(4, 'unghiul')], ['r(k) = lerp(ra, rb, k)', S.w(4, 'raza')], ['h(k) = lerp(ha, hb, k) + 0,9 · sin(πk)', S.w(4, 'sinus')], ['k = InOutCubic(t)', S.w(4, 'mijloc')]];
  F.forEach(([s, at], i) => txt(ctx, s, 1080, 680 + i * 62, { size: 34, font: 'JetBrains Mono', weight: 700, color: i === 2 ? PAL.gold : PAL.paper, align: 'left', alpha: k * L.prog(t, at - 0.3, at + 0.1) }));
}

// pixelii: Isaac mărit cu grila; ×3 (pătrate egale) vs ×2,5 (coloane de 2 și 3)
function pixels(ctx, t, S, c, k) {
  const I = S.I;
  const k5 = Math.min(L.prog(t, S.L(5) - 0.2, S.L(5) + 0.3), 1 - L.prog(t, S.L(6) - 0.3, S.L(6)));
  if (k5 > 0) {
    // sprite-ul 48×48 la ×12, cu grila de pixeli de artă
    const s = 12, x0 = 240, y0 = 130;
    screen(ctx); ctx.globalAlpha = k * k5; ctx.imageSmoothingEnabled = false;
    ctx.fillStyle = 'rgba(20,12,10,0.9)'; ctx.fillRect(x0 - 12, y0 - 12, 48 * s + 24, 48 * s + 24);
    ctx.drawImage(I('isaac/side_idle_0'), x0, y0, 48 * s, 48 * s);
    ctx.fillStyle = 'rgba(232,220,196,0.13)';
    for (let i = 0; i <= 48; i++) { ctx.fillRect(x0 + i * s, y0, 1, 48 * s); ctx.fillRect(x0, y0 + i * s, 48 * s, 1); }
    ctx.fillStyle = PAL.gold; ctx.fillRect(x0, y0 + 48 * s + 30, 32 * s, 8);
    ctx.globalAlpha = 1;
    txt(ctx, '32 px = 1 unitate în lume', x0 + 16 * s, y0 + 48 * s + 70, { size: 34, color: PAL.gold, alpha: k * k5 });
    const ku = L.prog(t, S.w(5, 'interfața') - 0.3, S.w(5, 'interfața') + 0.2) * k5;
    if (ku > 0) { panel(ctx, 1040, 300, 640, 360, { alpha: ku * k, fill: 'rgba(8,6,6,0.9)', edge: PAL.gold, hi: PAL.gold2 }); txt(ctx, '640 × 360', 1360, 480, { size: 72, font: 'Micro5', weight: 400, color: PAL.gold, alpha: ku * k }); txt(ctx, 'rezoluția interfeței', 1360, 560, { size: 30, color: PAL.paper2, alpha: ku * k }); }
  }
  const k6 = Math.min(L.prog(t, S.L(6) - 0.2, S.L(6) + 0.3), 1);
  if (k6 > 0) {
    // stânga ×3, dreapta ×2,5; fiecare pixel de ecran mărit ×10 ca să se vadă
    const M = 9;
    const draw = (f, x0, y0, label, col, alpha) => {
      const w = Math.round(16 * f), h = Math.round(12 * f);
      const tmp = document.createElement('canvas'); tmp.width = w; tmp.height = h;
      const g = tmp.getContext('2d'); g.imageSmoothingEnabled = false; g.drawImage(CROP, 0, 0, w, h);
      screen(ctx); ctx.globalAlpha = alpha; ctx.imageSmoothingEnabled = false;
      ctx.fillStyle = 'rgba(20,12,10,0.92)'; ctx.fillRect(x0 - 16, y0 - 16, w * M + 32, h * M + 32);
      ctx.drawImage(tmp, x0, y0, w * M, h * M);
      ctx.fillStyle = 'rgba(0,0,0,0.25)'; for (let i = 0; i <= w; i++) ctx.fillRect(x0 + i * M, y0, 1, h * M); for (let j = 0; j <= h; j++) ctx.fillRect(x0, y0 + j * M, w * M, 1);
      ctx.globalAlpha = 1;
      txt(ctx, label, x0 + (w * M) / 2, y0 + h * M + 50, { size: 40, weight: 700, color: col, alpha });
    };
    draw(3, 120, 200, '×3: fiecare pixel = 3 × 3', PAL.poison, k * k6);
    const k7 = L.prog(t, S.L(7) - 0.2, S.L(7) + 0.3);
    if (k7 > 0) draw(2.5, 1010, 200, '×2,5: coloane de 2 și 3', PAL.blood, k * k7);
    const kb = L.prog(t, S.w(7, 'bandă') - 0.4, S.w(7, 'bandă'));
    if (kb > 0) {
      // un ecran 1366×768: UI ×2 = 1280×720, restul bandă neagră
      const sx = 1280, sy = 650, sw = 1366 / 3.4, sh = 768 / 3.4;
      screen(ctx); ctx.globalAlpha = k * kb; ctx.fillStyle = '#000'; ctx.fillRect(sx, sy, sw, sh);
      ctx.fillStyle = PAL.b2; ctx.fillRect(sx + (sw - 1280 / 3.4) / 2, sy + (sh - 720 / 3.4) / 2, 1280 / 3.4, 720 / 3.4); ctx.globalAlpha = 1;
      txt(ctx, '1366×768 → ×2, bandă neagră', sx + sw / 2, sy + sh + 34, { size: 26, color: PAL.paper2, alpha: k * kb });
      code(ctx, { x: 120, y: 660, w: 1100, ...snip(S.data, 'ui_scale', ['// Cel mai mare', 'Scale = Mathf']), re: ['Scale = Mathf'], k: k * kb, focus: 1, size: 21 });
    }
  }
}
