// Scena 2 — Două lumi: Model și View. Creierul (date, reguli) trimite bilețele unui teatru de păpuși.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, box, line, disc, PAL, K, toScreen, slice9, tile } from '../px.js';
import { wall, vignette } from '../bg.js';
import { txt, titleCard, captionAt, code, panel, snip } from '../ui.js';
import { unit, note, dmgNumber, terminal } from '../kit.js';

const E = L.ease;
const MP = { x: 26, y: 42, w: 268, h: 236 }, VP = { x: 346, y: 42, w: 268, h: 236 };

export default {
  id: 's02', num: 1, title: 'Model și View', music: 'menu',
  plan: [
    { title: 2.6 }, { line: 1, gap: -0.5 }, { line: 2 }, { line: 3 }, { line: 4, gap: 0.6 }, { line: 5, gap: 0.5 }, { line: 6, gap: 0.5 },
    { line: 7, gap: 0.5 }, { pause: 1.4 },
    { slot: { id: 's02-joc', label: 'în joc: cartea, lovitura, moartea' }, dur: 4.2 },
    { line: 8, gap: 0.5 }, { line: 9 }, { line: 10, gap: 0.6 },
  ],
  post: 1.6,
  setup(S) {
    S.slot('s02-joc').shots = [{ key: 'card_played', z: [1.1, 1.0], f: [0.45, 0.8] }, { key: 'hit', z: [1.0, 1.08], f: [0.55, 0.5] }, { key: 'kill', z: [1.04, 1.12], f: [0.5, 0.5] }];
    S.T = {
      play: S.w(5, 'joacă'), ev1: S.w(6, 'cartea'), ev2: S.w(6, 'damage'), ev3: S.w(6, 'murit'),
      p1: S.L(7) + 0.2, p2: S.L(7) + 1.2, p3: S.L(7) + 2.2, curtain: S.w(4, 'view'),
    };
  },
  draw(ctx, t, S) {
    const I = S.I, T = S.T;
    const c = wall(ctx, I, t, { torches: [[312, 70]] });
    const ph8 = E.inOutCubic(L.prog(t, S.L(8) - 0.2, S.L(8) + 0.8)); // la 8: View-ul pleacă, Modelul rămâne singur
    const kIn = E.outBack(L.prog(t, S.L(1) + 0.2, S.L(1) + 0.9), 1.1);
    const mx = MP.x + 146 * ph8 * 0, vx = VP.x + 340 * ph8;
    // ── panoul MODEL ──
    const mFade = 1 - E.inOutCubic(L.prog(t, S.L(10) - 0.3, S.L(10) + 0.4));
    if (kIn > 0 && mFade > 0) {
      cam(ctx, c);
      ctx.globalAlpha = L.clamp(kIn) * mFade;
      slice9(ctx, I('ui/panel_dark'), mx - (1 - kIn) * 40, MP.y, MP.w, MP.h, 8);
      ctx.globalAlpha = 1;
      const [hx, hy] = toScreen(c, mx + MP.w / 2, MP.y + 14);
      txt(ctx, 'MODEL', hx, hy, { size: 60, font: 'Micro5', weight: 400, color: PAL.gold, alpha: L.clamp(kIn) * mFade });
      brain(ctx, c, mx + MP.w / 2, MP.y + 46, t, L.prog(t, S.w(2, 'creierul') - 0.2, S.w(2, 'creierul') + 0.3) * mFade);
      // starea: numere și liste
      const gHp = t < T.ev2 ? 5 : 0;
      const rows = [
        [`isaac.Hp  = 26`, S.w(2, 'viață')],
        [`gaper.Hp  = ${gHp}`, S.w(2, 'viață') + 0.25],
        [`isaac.Hand = [Tear,`, S.w(2, 'cărți')],
        [`   Tear, Spoon Bender]`, S.w(2, 'cărți') + 0.1],
      ];
      rows.forEach(([s, at], i) => {
        const k = L.prog(t, at, at + 0.3) * mFade;
        const [x, y] = toScreen(c, mx + 22, MP.y + 100 + i * 20);
        const hit = i === 1 && t > T.ev2 && t < T.ev2 + 0.6;
        txt(ctx, s, x, y, { size: 30, font: 'JetBrains Mono', weight: 600, color: hit ? PAL.blood : i === 1 && gHp === 0 ? PAL.paper3 : PAL.paper, align: 'left', alpha: k, shadow: null });
      });
      // ce NU are: sprite, cameră, sunet (tăiate)
      const icons = [['sprite', S.w(3, 'sprite')], ['cameră', S.w(3, 'cameră')], ['sunet', S.w(3, 'sunet')]];
      const iconsOut = 1 - L.prog(t, S.L(4), S.L(4) + 0.4);
      icons.forEach(([s, at], i) => {
        const k = E.outBack(L.prog(t, at - 0.25, at + 0.05), 2) * iconsOut * mFade;
        if (k <= 0) return;
        const x = mx + 50 + i * 84, y = MP.y + 196;
        cam(ctx, c);
        ctx.globalAlpha = L.clamp(k);
        rect(ctx, x - 26, y - 14, 52, 28, PAL.b5); box(ctx, x - 26, y - 14, 52, 28, PAL.b3);
        if (i === 0) spr(ctx, I('isaac/portrait'), x, y + 12, { sx: 0.9, sy: 0.9 });
        if (i === 1) { rect(ctx, x - 10, y - 6, 20, 13, PAL.paper2); disc(ctx, x, y, 4, PAL.ink); rect(ctx, x + 5, y - 9, 5, 3, PAL.paper2); }
        if (i === 2) { rect(ctx, x - 9, y - 4, 5, 8, PAL.paper2); for (let j = 0; j < 6; j++) rect(ctx, x - 4 + j, y - 4 - j, 1, 8 + 2 * j, PAL.paper2); for (let j = 0; j < 3; j++) rect(ctx, x + 6 + j * 3, y - 2 - j, 1, 4 + 2 * j, PAL.paper3); }
        const kx = L.prog(t, at + 0.1, at + 0.35);
        if (kx > 0) { line(ctx, x - 22, y - 12, x - 22 + 44 * kx, y - 12 + 24 * kx, PAL.blood, { th: 3 }); }
        ctx.globalAlpha = 1;
        const [lx, ly] = toScreen(c, x, y + 22);
        txt(ctx, s, lx, ly, { size: 26, color: PAL.paper2, alpha: L.clamp(k) });
      });
    }
    // ── panoul VIEW: teatrul de păpuși ──
    const vIn = E.outBack(L.prog(t, S.L(1) + 0.5, S.L(1) + 1.2), 1.1) * (1 - ph8);
    if (vIn > 0.01) {
      cam(ctx, c);
      ctx.globalAlpha = L.clamp(vIn);
      slice9(ctx, I('ui/panel_dark'), vx + (1 - vIn) * 40, VP.y, VP.w, VP.h, 8);
      // scena: perete, podea
      const sx0 = vx + 10, sy0 = VP.y + 34, sw = VP.w - 20, sh = VP.h - 44;
      ctx.save(); ctx.beginPath(); ctx.rect(sx0, sy0, sw, sh); ctx.clip();
      tile(ctx, I('env/basement/wall'), sx0, sy0, sw, sh);
      rect(ctx, sx0, sy0, sw, sh, 'rgba(11,7,7,0.35)');
      tile(ctx, I('env/basement/floor'), sx0, sy0 + sh - 40, sw, 40);
      rect(ctx, sx0, sy0 + sh - 40, sw, 40, 'rgba(11,7,7,0.4)');
      // păpușile
      const shoot = t > T.p1 && t < T.p1 + 0.5;
      const isx = sx0 + 62, gx = sx0 + sw - 62, fy = sy0 + sh - 18;
      unit(ctx, I, 'isaac', isx + (shoot ? -3 : 0), fy, t, { pose: shoot ? 'attack' : 'idle', scale: 2 });
      // lacrima zboară spre Gaper
      const tk = L.prog(t, T.p1 + 0.1, T.p1 + 0.55);
      if (tk > 0 && tk < 1) { const tx = L.lerp(isx + 26, gx - 20, tk), ty = fy - 52 - Math.sin(tk * Math.PI) * 14; disc(ctx, tx, ty, 5, PAL.tear2); disc(ctx, tx - 1, ty - 1, 3, PAL.tear); rect(ctx, tx - 2, ty - 3, 2, 2, PAL.tearHi); }
      const dead = t > T.p3 + 0.15;
      const fl = t > T.p2 && t < T.p2 + 0.14 ? 1 : 0;
      if (!dead) unit(ctx, I, 'gaper', gx + (t > T.p2 && t < T.p2 + 0.2 ? 5 : 0), fy, t, { pose: t > T.p2 && t < T.p2 + 0.4 ? 'hurt' : 'idle', scale: 2, flash: fl });
      else { spr(ctx, I('prop/blood_decal_1'), gx, fy + 2, { ax: 0.5, ay: 0.5, sx: 2, sy: 2, alpha: 0.9 }); unit(ctx, I, 'gaper', gx, fy, t, { pose: 'dead', scale: 2 }); }
      // cortina
      const open = E.inOutCubic(L.prog(t, T.curtain - 0.2, T.curtain + 0.7));
      curtain(ctx, sx0, sy0, sw, sh, open, t);
      ctx.restore();
      ctx.globalAlpha = 1;
      const [hx, hy] = toScreen(c, vx + VP.w / 2, VP.y + 14);
      txt(ctx, 'VIEW', hx, hy, { size: 60, font: 'Micro5', weight: 400, color: PAL.gold, alpha: L.clamp(vIn) });
      dmgNumber(ctx, c, '5', gx, fy - 76, t - T.p2, { size: 72 });
      // desenează, animă, cântă, ascultă
      ['desenează', 'animă', 'cântă', 'ascultă'].forEach((w, i) => {
        const at = S.w(4, w.slice(0, 4)), k = Math.min(L.prog(t, at - 0.1, at + 0.2), 1 - L.prog(t, S.L(5) - 0.3, S.L(5)));
        const [x, y] = toScreen(c, vx + 46 + i * 60, VP.y + 52);
        if (k > 0) txt(ctx, w, x, y, { size: 26, color: PAL.cream, alpha: k });
      });
    }
    // ── mesajele dintre lumi ──
    const lanes = Math.min(L.prog(t, S.L(5) + 0.2, S.L(5) + 0.6), 1 - ph8);
    if (lanes > 0) {
      cam(ctx, c);
      ctx.globalAlpha = lanes;
      line(ctx, 300, 120, 340, 120, PAL.paper3, { dash: 3, gap: 2 }); arrowHeadL(ctx, 300, 120);
      line(ctx, 300, 224, 340, 224, PAL.paper3, { dash: 3, gap: 2 }); arrowHeadR(ctx, 340, 224);
      ctx.globalAlpha = 1;
      const [ax, ay] = toScreen(c, 320, 106), [bx, by] = toScreen(c, 320, 238);
      txt(ctx, 'comenzi', ax, ay, { size: 24, color: PAL.paper2, alpha: lanes });
      txt(ctx, 'evenimente', bx, by, { size: 24, color: PAL.paper2, alpha: lanes });
    }
    // comanda: de la View la Model
    const cp = E.inOutCubic(L.prog(t, T.play - 0.1, T.play + 0.9));
    if (cp > 0 && t < S.L(6) + 0.3) note(ctx, c, 'PlayCard(isaac, tear, gaper)', L.lerp(480, 160, cp), 120 - Math.sin(cp * Math.PI) * 10, { size: 26, alpha: 1 - L.prog(t, S.L(6), S.L(6) + 0.3) });
    // evenimentele: ies din Model, apoi sunt consumate de View una câte una
    const evs = [['CardPlayed', T.ev1, T.p1], ['Damaged  5', T.ev2, T.p2], ['UnitDied', T.ev3, T.p3]];
    evs.forEach(([s, born, used], i) => {
      if (t < born || t > used + 0.35 || ph8 > 0.5) return;
      const pop = E.outBack(L.prog(t, born, born + 0.3), 2);
      const go = E.inCubic(L.prog(t, used - 0.25, used + 0.1));
      const x = L.lerp(150, 470, go), y = L.lerp(238 + 0 * i, 150, go) + (i - 1) * 0 + (1 - go) * (i * 19 - 19) ;
      note(ctx, c, s, x, y + 0, { size: 26, alpha: L.clamp(pop) * (1 - L.prog(t, used + 0.1, used + 0.35)), paper: i === 1 ? '#f4d9b8' : PAL.paper, w: 250 });
    });
    // 8–9: testele și simulatorul rulează fără Unity
    const term = Math.min(E.outCubic(L.prog(t, S.L(8) + 0.6, S.L(8) + 1.1)), 1 - L.prog(t, S.L(10) - 0.3, S.L(10) + 0.2));
    if (term > 0) {
      const tl = [['PASS DamageFormulaWeakMarkCritBlockStrength'], ['PASS MapsConnectedAndBossFarthestFor1000Seeds'], ['PASS RngForksAreStableAndIndependent'], ['PASS ShuffleAndDeckPreserveMultiset'], ['PASS FullRunsAreDeterministic'], ['Tests: 35 passed, 0 failed.', PAL.gold],
        ['$ sim --runs 500 --seed 1', PAL.paper2], ['Runs=500 seeds=1..500'], ['Wins=219 (43.8%) floor clears=500/482/219', PAL.gold], ['Normal rounds=3.53  Boss rounds=6.78']];
      const n = t < S.L(9) ? L.lerp(0, 6, L.prog(t, S.L(8) + 1.1, S.E(8))) : 6 + L.lerp(0, 4, L.prog(t, S.L(9), S.L(9) + 2.5));
      terminal(ctx, 930, 150 + (1 - term) * 30, 900, 560, tl, n, { title: 'tools/sim/run.sh  (fără Unity)', alpha: term, size: 25 });
      const ms = L.prog(t, S.w(8, 'milisecunde') - 0.2, S.w(8, 'milisecunde') + 0.2) * term;
      if (ms > 0) txt(ctx, '35 de teste în câteva milisecunde', 1380, 760, { size: 32, color: PAL.paper, alpha: ms });
    }
    // 10: regula e în assembly
    const asm = E.outCubic(L.prog(t, S.L(10) - 0.2, S.L(10) + 0.3));
    if (asm > 0) {
      const { file, lines } = snip(S.data, 'asmdef');
      code(ctx, { x: 150, y: 140, w: 820, file, lines, re: ['noEngineReferences'], k: asm, focus: L.prog(t, S.w(10, 'motorul') - 0.3, S.w(10, 'motorul') + 0.2), size: 26 });
      const err = E.outBack(L.prog(t, S.w(10, 'încerci') - 0.1, S.w(10, 'încerci') + 0.3), 1.6);
      if (err > 0) {
        code(ctx, { x: 1040, y: 230, w: 760, file: 'Model/Combat/CombatEngine.cs  (o încercare)', lines: ['using System;', 'using System.Collections.Generic;', 'using UnityEngine;'], hl: [2], k: err, focus: 1, size: 25, mark: { 2: ['UnityEngine'] } });
        const bad = E.outBack(L.prog(t, S.w(10, 'compilează') - 0.15, S.w(10, 'compilează') + 0.25), 2);
        if (bad > 0) {
          panel(ctx, 1040, 470, 760, 120, { fill: 'rgba(92,13,13,0.95)', edge: PAL.blood, hi: '#e86a6a', alpha: L.clamp(bad) });
          txt(ctx, "error CS0246: The type or namespace name", 1064, 510, { size: 24, font: 'JetBrains Mono', weight: 600, color: PAL.white, align: 'left', shadow: null, alpha: L.clamp(bad) });
          txt(ctx, "'UnityEngine' could not be found", 1064, 548, { size: 24, font: 'JetBrains Mono', weight: 600, color: PAL.white, align: 'left', shadow: null, alpha: L.clamp(bad) });
        }
      }
    }
    vignette(ctx, 0.7);
    titleCard(ctx, t, { num: this.num, title: this.title, sub: 'logica e separată de ce vezi pe ecran' });
    const C = [
      [1, 'două lumi: *Model* și *View*'], [2, 'Modelul = *creierul*: reguli și stare'], [3, '*C#* curat: doar numere și liste'], [4, 'View-ul = *teatrul de păpuși*'],
      [5, 'View → Model: *comenzi*'], [6, 'Model → View: *evenimente*'], [7, 'View-ul le *povestește* pe ecran'], [8, 'fără grafică: *teste în milisecunde*'],
      [9, 'run-uri întregi, *fără Unity*'], [10, 'regula e scrisă în *assembly*'],
    ];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n === 7 ? S.E(7) + 1.4 : n < 10 ? S.L(n + 1) - 0.1 : S.dur - 0.2);
  },
  sfx(S) {
    const T = S.T;
    return [
      { t: S.L(1) + 0.2, id: 'card_draw', gain: 0.3 }, { t: S.L(1) + 0.5, id: 'card_draw', gain: 0.3, rate: 1.1 },
      { t: T.curtain - 0.1, id: 'shuffle', gain: 0.3 }, { t: T.play, id: 'card_play', gain: 0.35 },
      { t: T.ev1, id: 'ui_click', gain: 0.35 }, { t: T.ev2, id: 'ui_click', gain: 0.35, rate: 1.1 }, { t: T.ev3, id: 'ui_click', gain: 0.35, rate: 1.2 },
      { t: T.p1 + 0.1, id: 'tear_shot', gain: 0.4 }, { t: T.p2, id: 'hit_flesh_1', gain: 0.45 }, { t: T.p3 + 0.15, id: 'enemy_death_splat', gain: 0.45 },
      ...S.slot('s02-joc').shots.map((sh, i) => ({ t: S.slot('s02-joc').t0 + i * 1.4, id: ['card_play', 'hit_flesh_2', 'enemy_death_splat'][i], gain: 0.5 })),
      { t: S.w(10, 'compilează') - 0.1, id: 'debuff', gain: 0.4 },
    ];
  },
};

function arrowHeadL(g, x, y) { for (let i = 0; i < 3; i++) g.fillRect(x + i, y - (2 - i), 1, 2 * (2 - i) + 1); }
function arrowHeadR(g, x, y) { for (let i = 0; i < 3; i++) g.fillRect(x - i, y - (2 - i), 1, 2 * (2 - i) + 1); }

// Creierul (desenat din pixeli): o pată roz cu circumvoluțiuni, care pulsează puțin.
function brain(ctx, c, x, y, t, k) {
  if (k <= 0) return;
  cam(ctx, c);
  ctx.save(); ctx.globalAlpha = L.clamp(k * 2); ctx.translate(x, y); const s = E.outBack(L.clamp(k), 2) * (1 + 0.03 * Math.sin(t * 3)); ctx.scale(s, s);
  disc(ctx, -9, 0, 12, PAL.outline); disc(ctx, 9, 0, 12, PAL.outline); disc(ctx, 0, -5, 12, PAL.outline);
  disc(ctx, -9, 0, 11, '#e9a3a8'); disc(ctx, 9, 0, 11, '#e9a3a8'); disc(ctx, 0, -5, 11, '#f0b9bb');
  for (const [a, b, c2, d] of [[-14, -4, -6, 2], [-4, -10, 2, -4], [6, -2, 14, 4], [-10, 5, -2, 8], [3, 4, 10, -6]]) line(ctx, a, b, c2, d, '#b8676e');
  rect(ctx, -1, -14, 2, 24, '#b8676e');
  ctx.restore();
}

// Cortina roșie a teatrului: două jumătăți care se trag în lateral (open = 0..1).
function curtain(ctx, x, y, w, h, open, t) {
  const hw = Math.ceil((w / 2) * (1 - open * 0.86));
  for (const side of [0, 1]) {
    const x0 = side ? x + w - hw : x;
    rect(ctx, x0, y, hw, h, PAL.blood2);
    for (let i = 0; i < hw; i += 8) rect(ctx, x0 + i + (side ? 4 : 0), y, 3, h, PAL.blood3);
    for (let i = 2; i < hw; i += 8) rect(ctx, x0 + i, y, 1, h, PAL.blood);
  }
  rect(ctx, x, y, w, 12, PAL.blood3);
  for (let i = 0; i < w; i += 12) rect(ctx, x + i, y + 10, 8, 4, PAL.gold2);
}
