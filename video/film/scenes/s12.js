// Scena 12 — Temă pentru acasă: trei exerciții ca trei cărți care se întorc, fiecare cu rândul de cod de unde pornești.
// Final: titlul jocului, echipa coboară prin trapă.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, PAL, K, toScreen } from '../px.js';
import { wall, vignette, dust } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag, rich } from '../ui.js';
import { card, unit } from '../kit.js';

const E = L.ease;
const EX = [
  { hero: 'isaac', art: 'polyphemus', name: 'O carte nouă', desc: 'Adaug-o în baza de cărți și joac-o.' },
  { hero: 'cain', art: 'dice_shard', name: 'Altă curbă', desc: 'Schimbă easing-ul camerei.' },
  { hero: 'azazel', art: 'maw_of_the_void', name: 'Monstro ×2', desc: 'Dublează-i viața. Simulează.' },
];

export default {
  id: 's12', title: 'Temă pentru acasă', music: 'boss', fadeOut: 1.2,
  plan: [{ title: 2.4 }, { line: 1, gap: -0.4 }, { line: 2, gap: 0.6 }, { pause: 0.8 }, { line: 3, gap: 0.4 }, { pause: 0.8 }, { line: 4, gap: 0.4 }, { pause: 1.0 }, { line: 5, gap: 0.6 }],
  post: 4.2,
  draw(ctx, t, S) {
    const I = S.I;
    const c = wall(ctx, I, t, { torches: [[40, 110], [600, 110]] });
    dust(ctx, I, t, c, 18, 5);
    const end = L.prog(t, S.w(5, 'spor') - 0.6, S.w(5, 'spor') + 0.4);
    // cele trei cărți: cu fața în jos, apoi se întorc pe rând
    EX.forEach((ex, i) => {
      const flipAt = S.L(i + 2) - 0.1, kIn = E.outBack(L.prog(t, S.L(1) + 0.2 + i * 0.25, S.L(1) + 0.6 + i * 0.25), 1.4);
      if (kIn <= 0) return;
      const fp = L.prog(t, flipAt, flipAt + 0.45), sx = Math.abs(Math.cos(fp * Math.PI)), back = fp < 0.5;
      const active = t > flipAt && (i === 2 ? t < S.L(5) : t < S.L(i + 3) - 0.1);
      const x = 152 + i * 168, y = 118 - (active ? 10 : 0) + end * 260;
      card(ctx, I, ex.art, x, y, c, { s: 2 * kIn, fx: Math.max(0.02, sx), back, alpha: 1 - end, glow: active ? 0.8 : 0, hero: ex.hero, name: ex.name, desc: ex.desc, cost: i + 1, text: !back && sx > 0.3, art: ex.art });
      if (!back && t < S.L(5)) { const [lx, ly] = toScreen(c, x, 218); txt(ctx, `EXERCIȚIUL ${i + 1}`, lx, ly, { size: 44, font: 'Micro5', weight: 400, color: active ? PAL.gold : PAL.paper2, alpha: 1 - end }); }
    });
    // rândul de cod de la care pornești
    const on = (a, b) => Math.min(L.prog(t, a, a + 0.4), 1 - L.prog(t, b - 0.3, b));
    const k1 = on(S.w(2, 'carddatabase') - 0.2, S.L(3) - 0.1);
    if (k1 > 0) { code(ctx, { x: 160, y: 700, w: 1600, ...snip(S.data, 'card_tear'), re: ['Add'], k: k1, focus: 1, size: 26, title: 'copiază rândul, schimbă id-ul și efectul' }); }
    const k2 = on(S.w(3, 'easing') - 0.2, S.L(4) - 0.1);
    if (k2 > 0) {
      code(ctx, { x: 160, y: 700, w: 1600, ...snip(S.data, 'ease_orbit'), re: ['InOutCubic'], k: k2, focus: 1, size: 28, title: 'Exploration/CameraRig.cs · Orbit()' });
      rich(ctx, [['Ease.InOutCubic', PAL.paper2], ['  →  ', PAL.paper3], ['Ease.OutBack', PAL.gold], ['  sau  ', PAL.paper3], ['Ease.Linear', PAL.blood]], 960, 880, { size: 36, font: 'JetBrains Mono', weight: 700, alpha: k2 * L.prog(t, S.w(3, 'back') - 0.3, S.w(3, 'back')) });
    }
    const k3 = on(S.w(4, 'monstro') - 0.3, S.L(5) - 0.1);
    if (k3 > 0) {
      code(ctx, { x: 160, y: 680, w: 1600, ...snip(S.data, 'monstro'), re: ['260'], k: k3, focus: 1, size: 25, mark: { 0: ['260'] } });
      rich(ctx, [['260 → 520', PAL.gold], ['     apoi     ', PAL.paper3], ['tools/sim/run.sh', PAL.poison]], 960, 860, { size: 38, font: 'JetBrains Mono', weight: 700, alpha: k3 * L.prog(t, S.w(4, 'rulează') - 0.3, S.w(4, 'rulează')) });
      txt(ctx, 'Wins = ?', 960, 920, { size: 34, font: 'JetBrains Mono', weight: 700, color: PAL.paper, alpha: k3 * L.prog(t, S.w(4, 'rata') - 0.3, S.w(4, 'rata')) });
    }
    // finalul: logo-ul, echipa coboară prin trapă
    if (end > 0) {
      const logo = I('logo');
      cam(ctx, c); spr(ctx, logo, 320, 120, { ax: 0.5, ay: 0.5, alpha: end, sx: E.outBack(end, 1.3), sy: E.outBack(end, 1.3) });
      cam(ctx, c); spr(ctx, I('prop/trapdoor'), 320, 300, { ax: 0.5, ay: 0.5, sx: 2, sy: 2, alpha: end });
      ['lilith', 'isaac', 'cain', 'azazel'].forEach((id, i) => {
        const go = L.prog(t, S.w(5, 'coborât') + 0.6 + i * 0.35, S.w(5, 'coborât') + 1.4 + i * 0.35);
        const x = L.lerp(150 + i * 50, 320, E.inOutCubic(go)), y = 300 + 12 * go, a = end * (1 - L.prog(go, 0.8, 1));
        cam(ctx, c); ctx.globalAlpha = a; spr(ctx, I(`${id}/back_walk_${Math.floor(t * 8 + i) % 4}`), x, y, { sx: 2 * (1 - 0.4 * go), sy: 2 * (1 - 0.4 * go) }); ctx.globalAlpha = 1;
      });
      txt(ctx, 'Spor la coborât.', 960, 600, { size: 64, weight: 700, color: PAL.gold, alpha: end * L.prog(t, S.w(5, 'spor'), S.w(5, 'spor') + 0.5) });
      txt(ctx, 'Darkest Basement · cum e făcut', 960, 1010, { size: 30, color: PAL.paper2, alpha: end * L.prog(t, S.E(5) + 0.5, S.E(5) + 1.2) });
    }
    vignette(ctx, 0.7);
    titleCard(ctx, t, { title: this.title, sub: 'trei exerciții' }, 2.4);
    const C = [[1, 'ca să-l faci *al tău*'], [2, '1. o carte nouă: *Model → View*, fără UI nou'], [3, '2. altă *curbă de easing* la cameră'], [4, '3. Monstro ×2: *noua rată de victorie*'], [5, 'de ce s-a schimbat cifra?']];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n < 5 ? S.L(n + 1) - 0.1 : S.w(5, 'spor') - 0.4);
  },
  sfx(S) {
    const out = [];
    [0, 1, 2].forEach((i) => out.push({ t: S.L(1) + 0.3 + i * 0.25, id: 'card_draw', gain: 0.3 }, { t: S.L(i + 2) - 0.1, id: 'card_flip_reward', gain: 0.4 }));
    out.push({ t: S.w(5, 'spor') - 0.5, id: 'card_reward_rare', gain: 0.4 }, { t: S.w(5, 'coborât') + 1.2, id: 'trapdoor_descend', gain: 0.45 }, { t: S.E(5) + 2.2, id: 'victory_sting', gain: 0.4 });
    return out;
  },
};
