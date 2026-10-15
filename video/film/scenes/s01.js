// Scena 1 — Jocul în 30 de secunde. Gameplay (capturi seed 7, sloturi pentru clipuri live), apoi titlul se desface în bucăți.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, PAL, K } from '../px.js';

// Dizolvarea logo-ului: fiecare pixel are un prag (Bayer 4×4 + zgomot + o ușoară baleiere de la stânga); apare când k îl depășește.
let LOGO = null;
const DRIPS = [[33, 51], [48, 55], [78, 51], [171, 52], [182, 50], [249, 57]]; // vârfurile scurgerilor de sânge din title_logo.png
function prepLogo(img) {
  if (typeof document === 'undefined' || !img) return;
  const c = document.createElement('canvas'); c.width = img.width; c.height = img.height;
  const g = c.getContext('2d'); g.drawImage(img, 0, 0);
  const src = g.getImageData(0, 0, c.width, c.height), th = new Float32Array(c.width * c.height), r = L.rng(7);
  const B = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
  for (let y = 0; y < c.height; y++) for (let x = 0; x < c.width; x++) th[y * c.width + x] = 0.35 * (B[(y & 3) * 4 + (x & 3)] / 16) + 0.4 * r() + 0.25 * (x / c.width);
  LOGO = { img, w: c.width, h: c.height, src, th, out: g.createImageData(c.width, c.height), canvas: c, g };
}
function logoReveal(ctx, I, k, t, R0) {
  if (!LOGO || k <= 0) return;
  const { w, h, src, th, out, canvas, g, img } = LOGO, X = 960 - w * 2, Y = 330 - h * 2;
  screen(ctx); ctx.imageSmoothingEnabled = false;
  if (k >= 1) { ctx.drawImage(img, X, Y, w * 4, h * 4); return; }
  const s = src.data, d = out.data, kk = k * 1.05;
  for (let i = 0; i < w * h; i++) {
    const o = i * 4;
    if (s[o + 3] === 0 || th[i] > kk) { d[o + 3] = 0; continue; }
    const fresh = kk - th[i] < 0.06; // pixelii care tocmai au apărut ard o clipă, ca un jar
    d[o] = fresh ? 255 : s[o]; d[o + 1] = fresh ? 214 : s[o + 1]; d[o + 2] = fresh ? 120 : s[o + 2]; d[o + 3] = s[o + 3];
  }
  g.putImageData(out, 0, 0);
  ctx.drawImage(canvas, X, Y, w * 4, h * 4);
}
// picături care se formează la vârful fiecărei scurgeri, cad și se sting (pătrate de 4 px: un pixel de artă la ×4)
function drips(ctx, t, t0) {
  if (!LOGO || t < t0) return;
  const X = 960 - LOGO.w * 2, Y = 330 - LOGO.h * 2;
  screen(ctx);
  DRIPS.forEach(([x, y], i) => {
    const per = 1.7 + (i % 3) * 0.45, ph = ((t - t0 + i * 0.37) % per) / per;
    const px = X + x * 4, py = Y + (y + 1) * 4;
    if (ph < 0.45) { const g = ph / 0.45; ctx.fillStyle = '#9e1b1b'; ctx.fillRect(px, py, 4, Math.round(g * 2) * 4 + 4); return; }
    const f = (ph - 0.45) / 0.55, fall = 6 + f * f * 120;
    ctx.globalAlpha = 1 - f * 0.9; ctx.fillStyle = '#d33c3c'; ctx.fillRect(px, py + Math.round(fall / 4) * 4, 4, 8); ctx.globalAlpha = 1;
  });
}
import { wall, vignette, dust } from '../bg.js';
import { txt, captionAt, measure } from '../ui.js';
import { unit } from '../kit.js';

const E = L.ease;
const G = 0.34; // pauzele dintre replici, strânse: e montajul de deschidere

export default {
  id: 's01', title: 'Jocul în 30 de secunde', music: 'combat', musicFrom: 6, fadeIn: false, fadeOut: 0.5, post: 1.6,
  plan: [
    { pause: 0.5 },
    { slot: { id: 's01-intro', shots: [] }, lines: [1, 2], gap: 0.25, inner: G, lead: 0.75, tail: 0 },
    { slot: { id: 's01-bucla', shots: [] }, lines: [3, 3], gap: G, lead: G, tail: 0 },
    { slot: { id: 's01-carti', shots: [] }, lines: [4, 4], gap: G, lead: G, tail: 0 },
    { slot: { id: 's01-bosi', shots: [] }, lines: [5, 5], gap: G, lead: G, tail: 0.6 },
    { line: 6, gap: 0.5 },
    { line: 7, gap: 0.45 },
  ],
  setup(S) {
    // tăieturile cad pe cuvinte: „hartă” → harta, „hol” → holul, „ușa” → ușa...
    const sl = (id) => S.slot(id);
    sl('s01-intro').shots = [
      { key: 'menu', z: [1.0, 1.1], f: [0.25, 0.2] },
      { key: 'corridor', at: S.w(2, 'cobori') - 0.1, z: [1.04, 1.14], f: [0.5, 0.7] },
      { key: 'side_view', at: S.w(2, 'monștri') - 0.15, z: [1.1, 1.0], f: [0.65, 0.6] },
    ];
    sl('s01-bucla').shots = [
      { key: 'map1', z: [1.12, 1.25], f: [0.5, 0.45] },
      { key: 'corridor2', at: S.w(3, 'mergi') - 0.05, z: [1.0, 1.1] },
      { key: 'door', at: S.w(3, 'deschizi') - 0.05, z: [1.0, 1.12], f: [0.5, 0.45] },
      { key: 'hero_turn', at: S.w(3, 'bați') - 0.1, z: [1.06, 1.0], f: [0.5, 0.8] },
    ];
    sl('s01-carti').shots = [
      { key: 'card_played', z: [1.15, 1.05], f: [0.45, 0.85] },
      { key: 'beam', at: S.w(4, 'câștigi') - 0.1, z: [1.0, 1.1], f: [0.4, 0.55] },
      { key: 'reward', at: S.w(4, 'primești') - 0.1, z: [1.0, 1.15], f: [0.5, 0.4] },
      { key: 'trapdoor', at: S.w(4, 'cobori') - 0.1, z: [1.12, 1.0], f: [0.5, 0.6] },
    ];
    sl('s01-bosi').shots = [
      { key: 'monstro_impact', z: [1.0, 1.1], f: [0.6, 0.5] },
      { key: 'gurdy_impact', at: S.w(5, 'boși') - 0.1, z: [1.1, 1.0], f: [0.7, 0.4] },
      { key: 'mom_foot', at: S.w(5, 'final') - 0.1, z: [1.0, 1.08], f: [0.65, 0.4] },
      { key: 'mom_impact', at: S.w(5, 'mom') - 0.05, z: [1.06, 1.14], f: [0.5, 0.5] },
    ];
    S.revealAt = S.slot('s01-bosi').t1 + 0.15; S.mathAt = S.w(6, 'matematică'); S.buildAt = S.w(6, 'construi'); S.subAt = S.w(7, 'pusă') - 0.3;
    prepLogo(S.I('logo'));
  },
  draw(ctx, t, S) {
    const I = S.I;
    const c = wall(ctx, I, t, { dark: 0.74, torches: [[60, 150], [580, 150]] });
    dust(ctx, I, t, c, 22, 11);
    // titlul: logo-ul real al jocului (title_logo, 300×60) la ×4, un factor întreg; apare pixel cu pixel, apoi curge sânge
    const R0 = S.revealAt, k = L.prog(t, R0, R0 + 1.9);
    const gk = E.outCubic(L.prog(t, R0 + 0.3, R0 + 2.2));
    if (gk > 0) {
      screen(ctx); ctx.save(); ctx.globalCompositeOperation = 'lighter';
      const g = ctx.createRadialGradient(960, 330, 0, 960, 330, 760);
      const p = 0.9 + 0.1 * Math.sin(t * 2.3);
      g.addColorStop(0, `rgba(190,40,30,${0.26 * gk * p})`); g.addColorStop(0.45, `rgba(120,30,20,${0.12 * gk * p})`); g.addColorStop(1, 'rgba(0,0,0,0)');
      ctx.fillStyle = g; ctx.fillRect(0, 0, 1920, 1080); ctx.restore();
    }
    logoReveal(ctx, I, k, t, R0);
    drips(ctx, t, R0 + 1.6);
    // simbolurile de matematică, într-un singur rând sub titlu, pe rând
    const SYM = ['G=(V,E)', 'BFS', 'seed', 'n!', 'sort', 'f∘g', '⌊x⌋', 'p̂', 'f(t)', 'x²'];
    const line = E.inOutCubic(L.prog(t, S.mathAt - 0.3, S.mathAt + 0.3));
    if (line > 0) { screen(ctx); ctx.fillStyle = PAL.gold2; ctx.fillRect(960 - 620 * line, 512, 1240 * line, 4); }
    // așezate după lățimea reală, cu spații egale între ele, centrate sub titlu
    const ws = SYM.map((sym) => measure(ctx, sym, 46, 'JetBrains Mono', 700)), gap = 46, tot = ws.reduce((a, b) => a + b, 0) + gap * (SYM.length - 1);
    let sx = 960 - tot / 2;
    SYM.forEach((sym, i) => {
      const kk = E.outBack(L.prog(t, S.mathAt + i * 0.11, S.mathAt + i * 0.11 + 0.35), 2.2);
      if (kk > 0.01) txt(ctx, sym, sx + ws[i] / 2, 568, { size: 46 * kk, font: 'JetBrains Mono', weight: 700, color: i % 2 ? PAL.paper : PAL.gold, alpha: L.clamp(kk) });
      sx += ws[i] + gap;
    });
    txt(ctx, 'cum e făcut, piesă cu piesă', 960, 650, { size: 50, color: PAL.paper, alpha: L.prog(t, S.subAt, S.subAt + 0.5) });
    // eroii intră pe „construi”, cu Gaperul în dreapta
    cam(ctx, c);
    const enter = E.outCubic(L.prog(t, S.buildAt - 0.2, S.buildAt + 1.0));
    ['lilith', 'isaac', 'cain', 'azazel'].forEach((id, i) => {
      const x = 170 + i * 66 - (1 - enter) * 220, bob = Math.abs(Math.sin(t * 7 + i)) * (1 - enter) * 4;
      ctx.globalAlpha = enter; unit(ctx, I, id, x, 296 - bob, t, { phase: i * 0.7, scale: 2 }); ctx.globalAlpha = 1;
    });
    cam(ctx, c); ctx.globalAlpha = enter; unit(ctx, I, 'gaper', 500 + (1 - enter) * 180, 296, t, { scale: 2 }); ctx.globalAlpha = 1;
    vignette(ctx, 0.85);
    captionAt(ctx, t, 'fiecare piesă = *o idee pe care o știi deja*', S.L(7) + 0.2, S.dur - 0.3);
  },
  sfx(S) {
    const out = [];
    const sl = (id) => S.slot(id);
    const cut = { corridor: 'footstep_stone_1', side_view: 'gaper_groan', map1: 'ui_click', corridor2: 'footstep_stone_2', door: 'door_open', hero_turn: 'card_draw',
      card_played: 'card_play', beam: 'brimstone_beam', reward: 'card_flip_reward', trapdoor: 'trapdoor_descend', monstro_impact: 'monstro_land', gurdy_impact: 'monster_roar', mom_foot: 'mom_stomp', mom_impact: 'hit_flesh_2' };
    for (const id of ['s01-intro', 's01-bucla', 's01-carti', 's01-bosi']) {
      const s = sl(id);
      s.shots.forEach((sh, i) => { const at = sh.at ?? s.t0; if (cut[sh.key]) out.push({ t: at, id: cut[sh.key], gain: sh.key === 'beam' || sh.key.startsWith('mom') || sh.key.startsWith('monstro') ? 0.45 : 0.32 }); });
    }
    out.push({ t: S.revealAt + 0.05, id: 'door_boss_open', gain: 0.55 }, { t: S.mathAt, id: 'card_draw', gain: 0.3 }, { t: S.subAt + 0.2, id: 'card_reward_rare', gain: 0.4 });
    return out;
  },
};
