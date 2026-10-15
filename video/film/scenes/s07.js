// Scena 7 — Evenimente și coada de animații. Azazel joacă Blood Beam: Modelul scoate instant 7 evenimente (în ordinea reală
// din CombatEngine.PlayCard); dacă le-ai anima deodată e haos, așa că View-ul le ia din coadă una câte una.
import * as L from '../../engine/lib.js';
import { cam, screen, spr, rect, line, disc, PAL, K, toScreen, slice9 } from '../px.js';
import { wall, vignette, floor } from '../bg.js';
import { txt, titleCard, captionAt, code, snip, tag, panel } from '../ui.js';
import { unit, note, dmgNumber, card } from '../kit.js';

const E = L.ease;
const EV = ['EnergyChanged  −2', 'CardPlayed  blood_beam', 'Damaged  gaper 5', 'Damaged  fly 4', 'UnitDied  fly', 'UnitMoved  pooter 3→2', 'StatusApplied  Bleed 2'];
const KIND = ['energy', 'card', 'dmgG', 'dmgF', 'die', 'move', 'bleed'];
const X = { azazel: 300, gaper: 400, fly: 466, pooter: 532 }, FY = 300;

export default {
  id: 's07', num: 6, title: 'Coada de animații', music: 'combat', musicFrom: 50,
  plan: [
    { title: 2.6 }, { line: 1, gap: -0.5 }, { pause: 0.3 }, { line: 2, gap: 0.5 }, { pause: 0.4 }, { line: 3, gap: 0.4 }, { pause: 2.2 }, { line: 4, gap: 0.5 }, { line: 5, gap: 0.6 }, { pause: 0.5 },
    { slot: { id: 's07-lovitura', label: 'în joc: număr, clipire, sânge, shake' }, dur: 4.2 }, { line: 6, gap: 0.5 },
  ],
  post: 1.6,
  setup(S) {
    S.slot('s07-lovitura').shots = [{ key: 'hit', z: [1.0, 1.1], f: [0.55, 0.45] }, { key: 'kill', z: [1.12, 1.02], f: [0.45, 0.5] }, { key: 'beam2', z: [1.0, 1.08], f: [0.5, 0.5] }];
    const t0 = S.w(3, 'primul') - 0.2, d = (S.L(4) - 0.4 - t0) / EV.length;
    S.evT = EV.map((_, i) => t0 + i * d); S.evD = d;
    S.T = { play: S.w(1, 'joci'), burst: S.w(1, 'loc'), ms: S.w(1, 'fracțiune'), chaos: S.w(2, 'deodată') - 0.2, queue: S.w(2, 'coadă') - 0.3, sw: S.w(5, 'switch'), dmg: S.w(5, 'damaged'),
      num: S.w(5, 'număr'), flash: S.w(5, 'clipire'), blood: S.w(5, 'sânge'), shake: S.w(5, 'shake'), exc: S.w(6, 'excepție'), tr: S.w(6, 'try') };
  },
  draw(ctx, t, S) {
    const I = S.I, T = S.T;
    // tremurul de la „haos” și de la shake-ul demonstrat la 5 (trauma² pe zgomot)
    const tr = Math.max(0, 1 - (t - T.chaos) / 0.8) * (t > T.chaos ? 1 : 0) * 0.9 + Math.max(0, 1 - (t - T.shake) / 0.6) * (t > T.shake ? 1 : 0) * 0.7;
    const sh = tr * tr * 18;
    const c0 = { ox: L.noise(t * 18, 1) * sh, oy: L.noise(t * 18, 2) * sh };
    const c = wall(ctx, I, t, { c: c0, torches: [[40, 100], [600, 100]] });
    const on = (a, b, ia = 0.4, ob = 0.3) => Math.min(L.prog(t, a, a + ia), 1 - L.prog(t, b - ob, b));
    // scena de jos
    floor(ctx, I, FY, c);
    stage(ctx, t, S, c);
    // panoul Model + cartea jucată
    const km = on(S.L(1) - 0.2, S.L(4) - 0.2);
    if (km > 0) {
      panel(ctx, 70, 90, 470, 300, { alpha: km });
      txt(ctx, 'MODEL', 305, 130, { size: 60, font: 'Micro5', weight: 400, color: PAL.gold, alpha: km });
      const kc = E.outBack(L.prog(t, T.play - 0.3, T.play + 0.1), 1.4) * (1 - L.prog(t, T.burst, T.burst + 0.2));
      if (kc > 0) card(ctx, I, 'blood_beam', L.lerp(10, 100, kc), 80, c, { s: kc, alpha: L.clamp(kc) });
      const kt = L.prog(t, T.ms - 0.1, T.ms + 0.2) * km;
      if (kt > 0) txt(ctx, '≈ 0,02 ms', 305, 330, { size: 44, font: 'JetBrains Mono', weight: 700, color: PAL.poison, alpha: kt });
    }
    // bilețelele cu evenimente: grămadă la „pe loc”, apoi coadă la „la coadă”, apoi consumate una câte una
    const kq = on(T.burst - 0.05, S.L(4) + 0.2, 0.1);
    if (kq > 0) {
      const lineUp = E.inOutCubic(L.prog(t, T.queue, T.queue + 0.8));
      let consumed = 0; S.evT.forEach((a) => { if (t >= a + 0.25) consumed++; });
      EV.forEach((s, i) => {
        const r = L.rng(40 + i);
        const pile = [580 + r() * 700, 120 + r() * 250, (r() - 0.5) * 0.5];
        const qi = i - consumed;
        const slotX = 230 + qi * 262, slotY = 470;
        let x = L.lerp(pile[0], slotX, lineUp), y = L.lerp(pile[1], slotY, lineUp), rot = pile[2] * (1 - lineUp);
        const pop = E.outBack(L.prog(t, T.burst + i * 0.03, T.burst + 0.25 + i * 0.03), 2);
        let a = L.clamp(pop) * kq;
        if (qi < 0) { const p = L.prog(t, S.evT[i], S.evT[i] + 0.25); y = L.lerp(slotY, 620, p); a *= 1 - p; }
        else if (consumed > 0 && lineUp >= 1) { const sl = E.outCubic(L.prog(t, S.evT[consumed - 1] + 0.15, S.evT[consumed - 1] + 0.45)); x = L.lerp(slotX + 262, slotX, sl); }
        const hot = (T.chaos < t && t < T.queue) || (qi === 0 && t > S.evT[0] - 0.3);
        if (a > 0) noteScreen(ctx, s, x, y, { alpha: a, rot, w: 250, hot });
      });
      if (lineUp > 0.5 && t < S.L(4)) txt(ctx, 'următorul', 230, 405, { size: 26, color: PAL.gold, alpha: lineUp * kq });
    }
    // „?!” la haos
    const kh = on(S.w(2, 'înțelege') - 0.2, T.queue);
    if (kh > 0) txt(ctx, '?!', 1500, 260, { size: 200, weight: 700, color: PAL.blood, alpha: kh, sh: 8 });
    // codul
    const k4 = on(S.L(4) - 0.2, S.L(5) - 0.1);
    if (k4 > 0) code(ctx, { x: 620, y: 70, w: 1240, ...snip(S.data, 'cc_play', ['IEnumerator Play', '^\\{', 'for \\(int i', 'PlayOne\\(events', '^    \\}']), re: ['for \\(int i', 'PlayOne'], k: k4, focus: L.prog(t, S.w(4, 'yield') - 0.2, S.w(4, 'yield') + 0.2), size: 27 });
    const k5 = on(S.L(5) - 0.2, S.slot('s07-lovitura').t0);
    if (k5 > 0) {
      const sn = snip(S.data, 'cc_playone');
      code(ctx, { x: 620, y: 40, w: 1240, ...sn, re: ['case Damaged'], k: k5, focus: L.prog(t, T.dmg - 0.2, T.dmg + 0.2), size: 22 });
    }
    const k6 = on(S.L(6) - 0.2, S.dur);
    if (k6 > 0) {
      code(ctx, { x: 620, y: 70, w: 1240, ...snip(S.data, 'cc_safe', ['static IEnumerator Safe', 'try', 'MoveNext', 'catch', 'LogException', 'yield return current']), re: ['try', 'catch', 'LogException'], k: k6, focus: L.prog(t, T.tr - 0.2, T.tr + 0.2), size: 25 });
      // un eveniment care „crapă”: se face roșu, se rupe, dar coada merge mai departe
      const ke = L.prog(t, T.exc - 0.3, T.exc);
      const brk = L.prog(t, T.exc + 0.2, T.exc + 0.6);
      noteScreen(ctx, 'Damaged  gaper ???', 400, 560 + brk * 40, { alpha: ke * (1 - brk) * k6, w: 300, hot: true, bad: true });
      if (brk > 0) txt(ctx, '✓ lupta continuă', 300, 700, { size: 40, color: PAL.poison, alpha: brk * k6 });
    }
    vignette(ctx, 0.65);
    titleCard(ctx, t, { num: this.num, title: this.title, sub: 'Modelul calculează instant, View-ul povestește încet' });
    const C = [[1, 'Modelul rezolvă totul *pe loc*'], [2, 'toate deodată = *haos*, deci *coadă*'], [3, 'unul câte unul: animă, așteaptă, *următorul*'], [4, '*corutine*: yield return = „continuă mai târziu”'], [5, 'un *switch*: fiecare eveniment are animația lui'], [6, 'un bug vizual *nu blochează* lupta']];
    for (const [n, s] of C) captionAt(ctx, t, s, S.L(n) + 0.15, n === 5 ? S.slot('s07-lovitura').t0 : n < 6 ? S.L(n + 1) - 0.1 : S.dur - 0.2);
  },
  sfx(S) {
    const T = S.T, out = [{ t: T.play, id: 'card_play', gain: 0.35 }, { t: T.burst, id: 'shuffle', gain: 0.35 }, { t: T.chaos, id: 'brimstone_beam', gain: 0.3 }, { t: T.chaos + 0.05, id: 'hit_flesh_1', gain: 0.3 }, { t: T.chaos + 0.1, id: 'enemy_death_splat', gain: 0.3 }];
    const snd = { energy: ['buff', 0.25], card: ['card_play', 0.35], dmgG: ['brimstone_beam', 0.35], dmgF: ['hit_flesh_2', 0.35], die: ['enemy_death_splat', 0.4], move: ['card_draw', 0.25], bleed: ['bleed_apply', 0] };
    S.evT.forEach((a, i) => { const [id, g] = snd[KIND[i]]; if (g) out.push({ t: a + 0.25, id, gain: g }); });
    out.push({ t: T.num, id: 'hit_flesh_1', gain: 0.35 }, { t: T.shake, id: 'punch', gain: 0.35 });
    const sl = S.slot('s07-lovitura'); out.push({ t: sl.t0, id: 'hit_flesh_2', gain: 0.5 }, { t: sl.t0 + 1.4, id: 'enemy_death_splat', gain: 0.5 }, { t: sl.t0 + 2.8, id: 'brimstone_beam', gain: 0.45 });
    out.push({ t: T.exc, id: 'debuff', gain: 0.35 });
    return out;
  },
};

function noteScreen(ctx, s, x, y, o = {}) {
  const { alpha = 1, rot = 0, w = 250, hot = false, bad = false } = o;
  if (alpha <= 0) return;
  ctx.save(); ctx.setTransform(1, 0, 0, 1, x, y); ctx.rotate(rot); ctx.globalAlpha = alpha;
  ctx.fillStyle = 'rgba(0,0,0,0.4)'; ctx.fillRect(-w / 2 + 6, -26, w, 58);
  ctx.fillStyle = PAL.outline; ctx.fillRect(-w / 2 - 3, -31, w + 6, 58);
  ctx.fillStyle = bad ? '#e8a0a0' : hot ? '#f6e3b8' : PAL.paper; ctx.fillRect(-w / 2, -28, w, 52);
  ctx.fillStyle = bad ? PAL.blood : hot ? PAL.gold : PAL.paper3; ctx.fillRect(-w / 2, -28, 8, 52);
  ctx.restore();
  txt(ctx, s, x + 4, y - 2, { size: 22, font: 'JetBrains Mono', weight: 600, color: PAL.ink, shadow: null, alpha });
}

// scena: Azazel vs Gaper, Fly, Pooter. Starea depinde de ce evenimente s-au jucat (sau de „haos”, când pornesc toate deodată).
function stage(ctx, t, S, c) {
  const I = S.I, T = S.T;
  const chaos = t > T.chaos && t < T.queue ? t - T.chaos : -1;
  const at = (i) => (chaos >= 0 ? T.chaos : S.evT[i] + 0.25); // momentul în care pornește animația evenimentului i
  const since = (i) => (chaos >= 0 ? chaos : t - at(i));
  const done = (i) => (chaos >= 0 ? true : t >= at(i));
  const fresh = t < T.chaos || (t > T.queue && t < S.evT[0] + 0.2) || t > S.L(5) - 0.3; // înainte de coadă: totul la loc
  const flyDead = !fresh && done(4), moved = !fresh && done(5);
  cam(ctx, c);
  const atkA = !fresh && done(1) && since(1) < 0.5;
  unit(ctx, I, 'azazel', X.azazel + (atkA ? 8 : 0), FY, t, { pose: atkA ? 'attack' : 'idle', scale: 2 });
  // energia lui Azazel
  for (let k = 0; k < 2; k++) spr(ctx, I(!fresh && done(0) ? 'ui/energy_empty' : 'ui/energy_full'), X.azazel - 14 + k * 16, FY - 104, { ax: 0.5, ay: 0.5 });
  // raza (Blood Beam)
  if (!fresh && done(2) && since(2) < 0.5) { const a = 1 - since(2) / 0.5; ctx.globalAlpha = a; rect(ctx, X.azazel + 20, FY - 50, 260, 8, PAL.blood); rect(ctx, X.azazel + 20, FY - 48, 260, 3, '#ffb0a0'); ctx.globalAlpha = 1; }
  const hitG = !fresh && done(2) && since(2) < 0.15, hitF = !fresh && done(3) && since(3) < 0.15;
  unit(ctx, I, 'gaper', X.gaper, FY, t, { scale: 2, flash: hitG || (t > T.flash && t < T.flash + 0.15) ? 1 : 0, pose: hitG ? 'hurt' : 'idle' });
  if (!flyDead) unit(ctx, I, 'fly', X.fly, FY - 10, t, { scale: 2, flash: hitF ? 1 : 0, pose: hitF ? 'hurt' : 'idle' });
  else { spr(ctx, I('prop/blood_decal_2'), X.fly, FY + 1, { ax: 0.5, ay: 0.5, sx: 2, sy: 2, alpha: 0.85 }); unit(ctx, I, 'fly', X.fly, FY, t, { pose: 'dead', scale: 2, alpha: Math.max(0, 1 - since(4) / 1.2) }); }
  const mv = moved ? E.outCubic(L.clamp(since(5) / 0.4)) : 0;
  unit(ctx, I, 'pooter', L.lerp(X.pooter, X.fly, mv), FY - 6, t, { scale: 2, phase: 2 });
  // Bleed pe Gaper
  if (!fresh && done(6)) { const k = E.outBack(L.clamp(since(6) / 0.3), 2); spr(ctx, I('ui/status_bleed'), X.gaper + 24, FY - 96, { ax: 0.5, ay: 0.5, sx: 2 * k, sy: 2 * k }); }
  if (!fresh) { dmgNumber(ctx, c, '5', X.gaper, FY - 104, since(2), { size: 64 }); dmgNumber(ctx, c, '4', X.fly, FY - 84, since(3), { size: 64 }); }
  // 5: rețeta lui Damaged, pe Gaper
  if (t > S.L(5) - 0.3 && t < S.slot('s07-lovitura').t0) {
    dmgNumber(ctx, c, '6', X.gaper, FY - 104, t - T.num, { size: 72, life: 1.6 });
    if (t > T.blood) { const r = L.rng(9); for (let i = 0; i < 14; i++) { const a = r() * Math.PI, sp = 30 + r() * 50, dt = Math.min(0.6, t - T.blood); const x = X.gaper + Math.cos(a) * sp * dt * 2, y = FY - 50 - Math.sin(a) * sp * dt * 2 + 120 * dt * dt; disc(ctx, x, y, 1.5, PAL.blood); } spr(ctx, I('prop/blood_decal_0'), X.gaper - 10, FY + 2, { ax: 0.5, ay: 0.5, sx: 2, sy: 2, alpha: L.prog(t, T.blood, T.blood + 0.3) }); }
    const lab = [['număr', T.num, -150], ['clipire albă', T.flash, -40], ['sânge', T.blood, 70], ['shake', T.shake, 180]];
    for (const [s, a, dx] of lab) { const k = E.outBack(L.prog(t, a - 0.1, a + 0.2), 2); const [sx, sy] = toScreen(c, X.gaper + dx / 3 * 1.6 - 20, FY + 20); if (k > 0) tag(ctx, s, sx, sy + 20, { size: 28, alpha: L.clamp(k), color: PAL.gold }); }
  }
}
