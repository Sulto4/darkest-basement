// Scena 1 — Jocul în 30 de secunde. Gameplay (capturi seed 7, sloturi pentru clipuri live), apoi titlul se desface în bucăți.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, PAL, K } from '../px.js';
import { wall, vignette, dust } from '../bg.js';
import { txt, captionAt } from '../ui.js';
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
    S.splitAt = S.w(6, 'desfacem'); S.mathAt = S.w(6, 'matematică'); S.buildAt = S.w(6, 'construi'); S.backAt = S.w(7, 'pusă') - 0.3;
  },
  draw(ctx, t, S) {
    const I = S.I;
    const c = wall(ctx, I, t, { dark: 0.7, torches: [[60, 150], [580, 150]] });
    dust(ctx, I, t, c, 22, 11);
    // eroii, în rând, sub titlu
    cam(ctx, c);
    const enter = E.outCubic(L.prog(t, S.L(6) - 0.4, S.L(6) + 0.8));
    ['lilith', 'isaac', 'cain', 'azazel'].forEach((id, i) => {
      const x = 150 + i * 78 - (1 - enter) * 160, bob = Math.abs(Math.sin(t * 7 + i)) * (1 - enter) * 4;
      ctx.globalAlpha = enter;
      unit(ctx, I, id, x, 296 - bob, t, { phase: i * 0.7, scale: 2 });
      ctx.globalAlpha = 1;
    });
    cam(ctx, c);
    ctx.globalAlpha = enter;
    unit(ctx, I, 'gaper', 520 + (1 - enter) * 140, 296, t, { scale: 2 });
    ctx.globalAlpha = 1;
    // titlul: logo-ul jocului, desfăcut în fâșii la „desfacem” și reasamblat la „pusă la treabă”
    const logo = I('logo');
    const kIn = E.outBack(L.prog(t, S.L(6) - 0.5, S.L(6) + 0.3), 1.4);
    const split = E.inOutCubic(L.prog(t, S.splitAt, S.splitAt + 0.7)) * (1 - E.inOutCubic(L.prog(t, S.backAt, S.backAt + 0.8)));
    const N = 10, sw = logo.width / N;
    const SYM = ['G=(V,E)', 'BFS', 'seed', 'n!', 'sort', 'f∘g', '⌊x⌋', 'p̂', 'f(t)', 'x²'];
    for (let i = 0; i < N; i++) {
      const r = L.rng(70 + i);
      const ox = (i - (N - 1) / 2) * 15 * split, oy = (Math.sin(i * 1.9) * 10 + (r() - 0.5) * 6) * split, rot = (r() - 0.5) * 0.3 * split;
      cam(ctx, c);
      ctx.save();
      ctx.globalAlpha = L.clamp(kIn);
      ctx.translate(320 - logo.width / 2 + sw * (i + 0.5) + ox, 108 + oy);
      ctx.rotate(rot); ctx.scale(kIn, kIn);
      ctx.drawImage(logo, sw * i, 0, sw, logo.height, -sw / 2, -logo.height / 2, sw, logo.height);
      ctx.restore();
      // simbolurile de matematică ies din fiecare bucată
      const kk = E.outBack(L.prog(t, S.mathAt + i * 0.07, S.mathAt + i * 0.07 + 0.3), 2) * split;
      if (kk > 0.01) {
        const sx = (c.ox ?? 0) + (320 - logo.width / 2 + sw * (i + 0.5) + ox) * K, sy = (c.oy ?? 0) + (108 + 52 + (i % 2) * 14) * K;
        txt(ctx, SYM[i], sx, sy, { size: 50 * kk, weight: 700, color: i % 2 ? PAL.paper : PAL.gold, alpha: L.clamp(kk) });
      }
    }
    const sub = L.prog(t, S.backAt + 0.6, S.backAt + 1.1);
    txt(ctx, 'cum e făcut, piesă cu piesă', 960, 520, { size: 52, color: PAL.paper, alpha: sub });
    vignette(ctx, 0.8);
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
    out.push({ t: S.splitAt, id: 'punch', gain: 0.5 }, { t: S.mathAt, id: 'card_draw', gain: 0.3 }, { t: S.backAt + 0.5, id: 'card_reward_rare', gain: 0.45 });
    return out;
  },
};
