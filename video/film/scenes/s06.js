// Scena 6 — Lupta pe numere. Inițiativa = viteză + zar, apoi o sortare cu ordine totală; damage-ul = compunere de funcții
// (cu exemplul real de rotunjire: ⌊1,5·⌊1,5·5⌋⌋ = 10, dar ⌊2,25·5⌋ = 11); statusurile = 8 numere; intențiile.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, box, line, PAL, K, toScreen, slice9 } from '../px.js';
import { wall, vignette, floor } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag, panel, rich } from '../ui.js';
import { unit, die, portrait, dmgNumber, hpBar } from '../kit.js';

const E = L.ease;
// formația (ca în joc: eroii în stânga, rangul 1 spre centru; inamicii în dreapta)
const UNITS = [
  { id: 'lilith', x: 50, spd: 4, roll: 0, hero: true }, { id: 'isaac', x: 116, spd: 5, roll: 2, hero: true },
  { id: 'cain', x: 182, spd: 6, roll: 1, hero: true }, { id: 'azazel', x: 248, spd: 4, roll: 2, hero: true },
  { id: 'gaper', x: 392, spd: 3, roll: 3, hero: false }, { id: 'fly', x: 470, spd: 7, roll: 3, hero: false }, { id: 'pooter', x: 548, spd: 6, roll: 1, hero: false },
];
UNITS.forEach((u) => (u.ini = u.spd + u.roll));
// comparatorul din TurnOrder.Roll: inițiativă descrescător, apoi eroii întâi, apoi id-ul (ordinal)
const ORDER = UNITS.slice().sort((a, b) => b.ini - a.ini || (b.hero - a.hero) || (a.id < b.id ? -1 : 1));
const STATUS = [['block', 5], ['bleed', 2], ['poison', 3], ['stun', 0], ['weak', 1], ['mark', 2], ['strength', 2], ['taunt', 0]];
const AFTER = { block: 0, bleed: 1, poison: 2, stun: 0, weak: 0, mark: 1, strength: 2, taunt: 0 };

export default {
  id: 's06', num: 5, title: 'Lupta pe numere', music: 'combat', musicFrom: 20,
  plan: [
    { title: 2.6 }, { line: 1, gap: -0.5 }, { pause: 0.6 }, { line: 2, gap: 0.3 }, { pause: 0.6 }, { line: 3, gap: 0.4 }, { pause: 0.4 },
    { line: 4, gap: 0.7 }, { line: 5, gap: 0.45 }, { line: 6, gap: 0.45 }, { pause: 1.0 }, { line: 7, gap: 0.3 }, { pause: 0.6 }, { line: 8, gap: 0.6 }, { pause: 0.4 }, { line: 9, gap: 0.6 },
    { slot: { id: 's06-runda', label: 'în joc: bara de ture și intențiile' }, dur: 4.2 },
  ],
  post: 0.5,
  setup(S) {
    S.slot('s06-runda').shots = [{ key: 'combat_round', z: [1.0, 1.15], f: [0.5, 0.1] }, { key: 'hero_turn', z: [1.12, 1.02], f: [0.6, 0.5] }];
    S.T = {
      spd: S.L(1) + 0.4, roll: S.w(1, 'zar'), add: S.w(1, 'adună'), sort: S.w(2, 'sortăm'), tie: S.w(3, 'egalitate'), id: S.w(3, 'id'),
      base: S.w(4, 'baza'), str: S.w(4, 'strength'), weak: S.w(5, 'slăbit'), mark: S.w(5, 'marcată'), crit: S.w(5, 'crit'),
      floor: S.w(6, 'rotunjești'), block: S.w(6, 'block'), hit: S.E(6) + 0.3, comm: S.w(7, 'comută'), vec: S.w(8, 'vector'), tick: S.w(8, 'scad'), intent: S.w(9, 'intenția'), bite: S.w(9, 'muște'),
    };
  },
  draw(ctx, t, S) {
    const I = S.I, T = S.T;
    const c = wall(ctx, I, t, { torches: [[320, 90]] });
    const on = (a, b, ia = 0.4, ob = 0.3) => Math.min(L.prog(t, a, a + ia), 1 - L.prog(t, b - ob, b));
    // ── 1–3 și 9: scena de luptă ──
    const kStage = Math.max(on(S.L(1) - 0.3, S.L(4) - 0.4), on(S.L(9) - 0.3, S.dur));
    if (kStage > 0) {
      floor(ctx, I, 262, c);
      UNITS.forEach((u, i) => {
        cam(ctx, c); ctx.globalAlpha = kStage;
        unit(ctx, I, u.id, u.x, 262, t, { phase: i * 0.6, scale: 2 });
        ctx.globalAlpha = 1;
        const inSort = t < S.L(4);
        if (inSort) {
          const top = 262 - (u.hero || u.id === 'gaper' ? 90 : 62);
          const [sx, sy] = toScreen(c, u.x, top);
          const ks = L.prog(t, T.spd + i * 0.08, T.spd + 0.3 + i * 0.08) * kStage;
          txt(ctx, `viteză ${u.spd}`, sx, sy - 150, { size: 32, color: PAL.paper2, alpha: ks * (1 - L.prog(t, T.sort - 0.2, T.sort)) });
          const kr = L.prog(t, T.roll + i * 0.06, T.roll + 0.5 + i * 0.06);
          if (kr > 0 && t < T.sort) die(ctx, c, u.x, top - 34, kr < 1 ? Math.floor(t * 20 + i) % 4 : u.roll, { s: 1.1, rot: (1 - kr) * 4, alpha: kStage });
          const ka = L.prog(t, T.add + i * 0.05, T.add + 0.3 + i * 0.05) * kStage * (1 - L.prog(t, T.sort - 0.2, T.sort));
          if (ka > 0) txt(ctx, `${u.spd}+${u.roll} = ${u.ini}`, sx, sy - 30, { size: 40, weight: 700, color: PAL.gold, alpha: ka });
        }
      });
      // bara de ture: portretele urcă și se sortează
      const kb = on(T.sort - 0.6, S.L(4) - 0.1);
      if (kb > 0) {
        const ps = E.inOutCubic(L.prog(t, T.sort, T.sort + 1.4));
        UNITS.forEach((u, i) => {
          const j = ORDER.indexOf(u);
          const x = L.lerp(800 + i * 110, 600 + j * 120, ps) + 60, y = 150 - Math.sin(ps * Math.PI) * (j % 2 ? 40 : -40) * (i !== j ? 1 : 0);
          screen(ctx); ctx.globalAlpha = kb;
          cam(ctx, { ox: x - 14 * 3, oy: y - 16 * 3 });
          portrait(ctx, I, u.id, 14, 16, { active: false, alpha: kb, scale: 1 });
          screen(ctx);
          txt(ctx, String(u.ini), x, y + 74, { size: 34, font: 'Silkscreen', weight: 400, color: PAL.gold, alpha: kb });
        });
        // egalitățile
        const kt = on(T.tie - 0.1, S.L(4) - 0.1);
        if (kt > 0) {
          const j0 = ORDER.findIndex((u) => u.ini === 7), j1 = ORDER.map((u) => u.ini).lastIndexOf(7);
          screen(ctx); ctx.globalAlpha = kt; ctx.fillStyle = PAL.gold; ctx.fillRect(600 + j0 * 120 + 10, 220, (j1 - j0) * 120 + 100, 5); ctx.globalAlpha = 1;
          txt(ctx, 'egalitate la 7: eroii întâi, apoi id-ul', 600 + ((j0 + j1) / 2) * 120 + 60, 262, { size: 30, color: PAL.paper, alpha: kt });
          const ki = L.prog(t, T.id - 0.1, T.id + 0.3) * kt;
          if (ki > 0) txt(ctx, '"cain" < "isaac"', 600 + ((j0 + j1) / 2) * 120 + 60, 306, { size: 30, font: 'JetBrains Mono', weight: 600, color: PAL.gold, alpha: ki });
        }
      }
      const kc = on(S.w(3, 'comparatorul') - 0.3, S.L(4) - 0.5);
      if (kc > 0) code(ctx, { x: 160, y: 380, w: 1600, ...snip(S.data, 'turn_order'), re: ['entries.Sort'], k: kc, focus: 1, size: 25 });
      // intențiile (9)
      const ki = on(T.intent - 0.2, S.dur);
      if (ki > 0) {
        const intents = [['gaper', 390, 3], ['fly', 460, 1], ['pooter', 530, 2]];
        intents.forEach(([id, x, v], i) => {
          const k = E.outBack(L.prog(t, T.intent + i * 0.15, T.intent + 0.3 + i * 0.15), 2) * ki;
          const y = 262 - (id === 'gaper' ? 92 : 66);
          cam(ctx, c); spr(ctx, I('ui/intent_attack'), x - 6, y, { ax: 0.5, ay: 1, sx: 2 * k, sy: 2 * k });
          const [sx, sy] = toScreen(c, x + 10, y - 12); txt(ctx, String(v), sx, sy, { size: 36, font: 'Silkscreen', weight: 400, color: PAL.white, alpha: L.clamp(k) });
        });
        const kbt = L.prog(t, T.bite - 0.2, T.bite + 0.3) * ki;
        if (kbt > 0) {
          cam(ctx, c); ctx.globalAlpha = kbt;
          line(ctx, 380, 172, L.lerp(380, 262, kbt), L.lerp(172, 190, kbt), PAL.blood, { dash: 3, gap: 2, th: 2 });
          ctx.globalAlpha = 1;
          const [sx, sy] = toScreen(c, 250, 150); tag(ctx, 'Gaper vrea să-l muște pe Azazel', sx, sy - 40, { size: 30, alpha: kbt, color: PAL.blood });
        }
      }
    }
    // ── 4–7: formula de damage ──
    const kd = on(S.L(4) - 0.3, S.L(8) - 0.1);
    if (kd > 0) damage(ctx, t, S, c, kd);
    // ── 8: statusurile ──
    const kv = on(S.L(8) - 0.2, S.L(9) - 0.2);
    if (kv > 0) {
      STATUS.forEach(([s, v], i) => {
        const x = 300 + i * 190, y = 360;
        const k = E.outBack(L.prog(t, T.vec - 0.4 + i * 0.07, T.vec - 0.1 + i * 0.07), 1.6) * kv;
        panel(ctx, x - 80, y - 90, 160, 230, { alpha: L.clamp(k) });
        screen(ctx); ctx.globalAlpha = L.clamp(k); ctx.imageSmoothingEnabled = false; ctx.drawImage(I('ui/status_' + s), x - 36, y - 70, 72, 72); ctx.globalAlpha = 1;
        const ticked = t > T.tick + 0.3 + i * 0.05;
        const val = ticked ? AFTER[s] : v;
        txt(ctx, String(val), x, y + 50, { size: 64, font: 'Silkscreen', weight: 400, color: ticked && val !== v ? PAL.gold : PAL.paper, alpha: L.clamp(k) });
        txt(ctx, s[0].toUpperCase() + s.slice(1), x, y + 112, { size: 26, color: PAL[s] || PAL.paper2, alpha: L.clamp(k) });
        txt(ctx, `[${i}]`, x, y - 112, { size: 26, font: 'JetBrains Mono', weight: 600, color: PAL.paper3, alpha: L.clamp(k) });
      });
      code(ctx, { x: 360, y: 640, w: 1200, ...snip(S.data, 'statusset'), re: ['new int\\[8\\]'], k: kv * L.prog(t, S.L(8) + 0.3, S.L(8) + 0.7), focus: 1, size: 25 });
    }
    vignette(ctx, 0.65);
    titleCard(ctx, t, { num: this.num, title: this.title, sub: 'inițiativă, damage, statusuri' });
    const C = [[1, 'inițiativa = *viteză + zar(0..3)*'], [2, 'ordinea turelor = *o sortare*'], [3, 'comparator = *ordine totală*'], [4, 'damage = *compunere de funcții*'], [5, 'slăbit ×0,75 · marcat ×1,5 · crit ×1,5'],
      [6, 'rotunjești în jos, *apoi* scazi Block-ul'], [7, '*rotunjirea nu comută* cu înmulțirea'], [8, 'statusurile: *8 numere* într-un vector'], [9, 'intenția vizibilă: lupta devine *un puzzle*']];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n < 9 ? S.L(n + 1) - 0.1 : S.slot('s06-runda').t0);
  },
  sfx(S) {
    const T = S.T, out = [];
    UNITS.forEach((_, i) => out.push({ t: T.roll + 0.5 + i * 0.06, id: 'ui_click', gain: 0.18, rate: 0.9 + i * 0.05 }));
    out.push({ t: T.sort, id: 'shuffle', gain: 0.3 });
    for (const k of ['base', 'str', 'mark', 'crit', 'floor', 'block']) out.push({ t: T[k], id: 'ui_click', gain: 0.25 });
    out.push({ t: T.hit, id: 'crit_hit', gain: 0.5 }, { t: T.hit + 0.02, id: 'punch', gain: 0.4 }, { t: T.tick + 0.3, id: 'turn_start', gain: 0.25 }, { t: T.intent, id: 'gaper_groan', gain: 0.35 });
    out.push({ t: S.slot('s06-runda').t0, id: 'turn_start', gain: 0.4 }, { t: S.slot('s06-runda').t0 + 2.1, id: 'card_draw', gain: 0.4 });
    return out;
  },
};

// Formula de damage, termen cu termen: 5 → +2 → (×0,75 sărit) → ×1,5 → ×1,5 → ⌊⌋ → −4 = 11, apoi lovitura.
function damage(ctx, t, S, c, k) {
  const T = S.T, I = S.I;
  // Cain lovește un Gaper marcat, cu Block (pe podeaua camerei)
  floor(ctx, I, 170, c);
  const atk = t > T.hit - 0.25 && t < T.hit + 0.35;
  const lunge = Math.sin(Math.PI * L.clamp((t - (T.hit - 0.25)) / 0.6)) * 40;
  cam(ctx, c); ctx.globalAlpha = k;
  unit(ctx, I, 'cain', 160 + (atk ? lunge : 0), 168, t, { pose: atk ? 'attack' : 'idle', scale: 2 });
  const hurt = t > T.hit && t < T.hit + 0.4;
  unit(ctx, I, 'gaper', 470 + (hurt ? 6 : 0), 168, t, { pose: hurt ? 'hurt' : 'idle', scale: 2, flash: t > T.hit && t < T.hit + 0.12 ? 1 : 0 });
  ctx.globalAlpha = 1;
  // statusurile vizibile: Strength pe Cain, Mark + Block pe Gaper
  const icon = (key, x, y, v, a) => { if (a <= 0) return; cam(ctx, c); spr(ctx, I('ui/status_' + key), x, y, { ax: 0.5, ay: 0.5, sx: 2, sy: 2, alpha: a }); const [sx, sy] = toScreen(c, x + 14, y + 6); txt(ctx, String(v), sx, sy, { size: 30, font: 'Silkscreen', weight: 400, alpha: a }); };
  icon('strength', 150, 186, 2, k * L.prog(t, T.str - 0.2, T.str));
  icon('mark', 452, 186, 2, k * L.prog(t, T.mark - 0.2, T.mark));
  icon('block', 492, 186, t > T.hit ? 0 : 4, k * L.prog(t, T.block - 0.2, T.block));
  cam(ctx, c); hpBar(ctx, 440, 196, 60, t > T.hit ? 16 : 27, 27, { lag: t > T.hit + 0.5 ? 16 : 27 });
  dmgNumber(ctx, c, '11!', 470, 70, t - T.hit, { color: PAL.stun, size: 84 });
  // banda de funcții
  const boxes = [
    ['baza', '5', T.base, '5'], ['+ Strength', '+2', T.str, '7'], ['slăbit?', '×0,75', T.weak, '7', true], ['marcat', '×1,5', T.mark, '10,5'],
    ['crit', '×1,5', T.crit, '15,75'], ['rotunjire', '⌊ ⌋', T.floor, '15'], ['− Block', '−4', T.block, '11'],
  ];
  const y = 660;
  const hide = 1 - L.prog(t, S.L(7) - 0.2, S.L(7) + 0.1);
  if (hide > 0) boxes.forEach(([lbl, op, at, val, skip], i) => {
    const x = 250 + i * 236, kk = E.outBack(L.prog(t, at - 0.3, at), 1.6) * k * hide;
    if (kk <= 0) return;
    panel(ctx, x - 100, y - 70, 200, 140, { alpha: L.clamp(kk) * (skip ? 0.5 : 1), fill: skip ? 'rgba(40,30,30,0.8)' : 'rgba(20,12,10,0.92)' });
    txt(ctx, op, x, y - 10, { size: 52, weight: 700, color: skip ? PAL.paper3 : PAL.gold, alpha: L.clamp(kk) });
    txt(ctx, lbl, x, y + 44, { size: 28, color: skip ? PAL.paper3 : PAL.paper2, alpha: L.clamp(kk) });
    if (skip) { const ks = L.prog(t, at + 0.2, at + 0.5); txt(ctx, 'nu e', x, y - 96, { size: 26, color: PAL.paper3, alpha: ks * k }); }
    if (i) txt(ctx, '→', x - 118, y, { size: 36, color: PAL.paper3, alpha: L.clamp(kk) });
    // valoarea care curge pe bandă
    const kv = L.prog(t, at, at + 0.3) * k * hide;
    if (kv > 0) txt(ctx, val, x, y + 118, { size: 48, font: 'JetBrains Mono', weight: 700, color: i === 6 ? PAL.stun : PAL.white, alpha: kv });
  });
  // 7: compunerea și contraexemplul la rotunjire
  const k7 = Math.min(L.prog(t, S.L(7) - 0.1, S.L(7) + 0.3), 1 - L.prog(t, S.L(8) - 0.4, S.L(8) - 0.1));
  if (k7 > 0) {
    panel(ctx, 160, 120, 1600, 360, { alpha: k7, fill: 'rgba(16,10,9,0.97)' });
    txt(ctx, 'D = ⌊ crit ∘ marcat ∘ slăbit ∘ (+Str) (bază) ⌋ − Block', 960, 190, { size: 40, font: 'JetBrains Mono', weight: 600, color: PAL.paper, alpha: k7 });
    const kc = L.prog(t, T.comm - 0.6, T.comm - 0.2) * k7;
    rich(ctx, [['⌊1,5 · ⌊1,5 · 5⌋⌋ = ', PAL.paper], ['10', PAL.blood], ['     dar     ', PAL.paper3], ['⌊1,5 · 1,5 · 5⌋ = ', PAL.paper], ['11', PAL.poison]], 960, 300, { size: 40, font: 'JetBrains Mono', weight: 600, alpha: kc });
    txt(ctx, 'codul rotunjește o singură dată, la final', 960, 400, { size: 34, color: PAL.gold, alpha: kc });
    const dm = snip(S.data, 'damage');
    code(ctx, { x: 160, y: 520, w: 1600, ...snip(S.data, 'damage', ['double damage', 'Status.Weak', 'Status.Mark', 'if \\(crit\\)', 'Math.Floor']), re: ['Math.Floor'], k: k7, focus: 0.6, size: 24 });
  }
}
