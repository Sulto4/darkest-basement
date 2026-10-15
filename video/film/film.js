// „Darkest Basement: cum e făcut” — explainer 16:9, 1920×1080, 30 fps. Fiecare cadru e o funcție pură de timp.
// Scenele sunt în film/scenes/sNN.js; cronologia vine din datele vocii (film/timeline.js).
import * as L from '../engine/lib.js';
import { buildFilm, segments as segs, FPS } from './timeline.js';
import { cam, screen, PAL } from './px.js';
import { ditherIn, vignette, setTheme } from './bg.js';
import { txt } from './ui.js';
import s01 from './scenes/s01.js';
import s02 from './scenes/s02.js';
import s03 from './scenes/s03.js';
import s04 from './scenes/s04.js';
import s05 from './scenes/s05.js';
import s06 from './scenes/s06.js';
import s07 from './scenes/s07.js';
import s08 from './scenes/s08.js';
import s09 from './scenes/s09.js';
import s10 from './scenes/s10.js';
import s11 from './scenes/s11.js';
import s12 from './scenes/s12.js';

const MODS = [s01, s02, s03, s04, s05, s06, s07, s08, s09, s10, s11, s12];
const E = L.ease;
// Durata pieselor jocului (ffprobe), pentru buclele de muzică.
const TRACK = { menu: 41.147, explore: 103.999, combat: 102.859, boss: 84.009, basement: 27.985 };
const GAIN = { menu: 0.62, explore: 0.7, combat: 0.5, boss: 0.55, basement: 0.5 };

let FILM, ASSETS, I;

export default {
  title: 'Darkest Basement: cum e făcut',
  duration: 1,
  fonts: ['700 40px "Pixelify Sans"', '600 40px "Pixelify Sans"', '400 30px "Silkscreen"', '400 30px "Micro5"', '500 26px "JetBrains Mono"', '700 26px "JetBrains Mono"', 'italic 500 26px "JetBrains Mono"', '600 30px "Inter"', '700 30px "Inter"', '500 30px "Inter"'],
  async preload(loadJSON) {
    const d = await loadJSON({ assets: 'out/assets/index.json', ...Object.fromEntries(MODS.map((m) => [m.id, `voice/${m.id}.lines.json`])), sim: 'film/data/sim500.json', snippets: 'film/data/snippets.json', footage: 'footage.json' });
    ASSETS = d.assets;
    return d;
  },
  images() {
    const m = {};
    for (const [k, p] of Object.entries(ASSETS.sprites)) m[k] = p;
    for (const [k, p] of Object.entries(ASSETS.shots)) m['shot/' + k] = p;
    m.logo = ASSETS.logo;
    return m;
  },
  setup(stage) {
    // nume scurte: isaac/side_idle_0, gaper/idle_0, ui/map_boss, card/tear, prop/torch_0, env/basement/wall, shot/map1
    const alias = {};
    for (const k of Object.keys(stage.img)) {
      alias[k] = stage.img[k];
      const m = k.match(/^(Heroes|Enemies|Bosses|Familiars)\/(.+)$/); if (m) alias[m[2]] = stage.img[k];
      if (k.startsWith('UI/')) alias['ui/' + k.slice(3)] = stage.img[k];
      if (k.startsWith('Cards/')) alias['card/' + k.slice(6)] = stage.img[k];
      if (k.startsWith('Props/')) alias['prop/' + k.slice(6)] = stage.img[k];
    }
    I = (name) => alias[name];
    FILM = buildFilm(MODS, stage.data);
    this.duration = FILM.duration;
    for (const S of FILM.scenes) { S.I = I; S.data = stage.data; if (S.mod.setup) S.mod.setup(S, stage); }
  },
  sceneTimes() { return FILM.scenes.map((S) => ({ id: S.id, t0: S.t0, t1: S.t1, title: S.mod.title || S.id })); },
  segments() { return segs(FILM); },
  scene(id) { const S = FILM.scenes.find((x) => x.id === id); return S && { t0: S.t0, t1: S.t1, lines: S.lines, slots: S.slots.map(({ id, t0, t1, shots }) => ({ id, t0, t1, shots })), marks: S.marks }; },
  draw(t, stage) {
    const ctx = stage.ctx;
    const S = FILM.scenes.find((s) => t >= s.t0 && t < s.t1) || FILM.scenes[FILM.scenes.length - 1];
    const lt = t - S.t0;
    setTheme(S.mod.floor || (['s01', 's02', 's03', 's04'].includes(S.id) ? 'basement' : ['s05', 's06', 's07', 's08'].includes(S.id) ? 'caves' : 'depths'));
    const slot = S.slots.find((s) => lt >= s.t0 && lt < s.t1);
    if (slot) drawSlot(ctx, S, slot, lt);
    else {
      ctx.save(); S.mod.draw(ctx, lt, S); ctx.restore();
      cam(ctx, {}); screen(ctx);
    }
    // intrarea în scenă: dizolvare din negru „pe grilă” (nu în sloturi: acolo vine clipul live)
    if (!slot && S.mod.fadeIn !== false) ditherIn(ctx, lt, 0, 0.4);
    if (S.mod.fadeOut) { const k = L.prog(lt, S.dur - S.mod.fadeOut, S.dur); if (k > 0) { screen(ctx); ctx.fillStyle = `rgba(0,0,0,${k})`; ctx.fillRect(0, 0, 1920, 1080); } }
    screen(ctx);
  },
  audio() {
    const vo = [], sfx = [], music = [];
    for (const S of FILM.scenes) {
      for (const v of S.vo) vo.push({ ...v, at: v.at + S.t0 });
      // sloturile cu clip live (footage.json) au efectele lor, puse pe momentele din clip; cele ale scenei din interiorul slotului cad
      const live = S.slots.filter((sl) => S.data.footage?.[sl.id]?.sfx);
      const inLive = (t) => live.some((sl) => t >= sl.t0 - 0.02 && t < sl.t1);
      for (const c of S.mod.sfx ? S.mod.sfx(S) : []) if (c && c.t != null && c.t >= 0 && !inLive(c.t) && (ASSETS.sfx[c.id] || c.src) && (c.gain ?? 1) > 0) sfx.push({ ...c, t: c.t + S.t0, src: ASSETS.sfx[c.id] || c.src });
      for (const sl of live) for (const [t, id, gain] of S.data.footage[sl.id].sfx) if (ASSETS.sfx[id]) sfx.push({ t: sl.t0 + t + S.t0, id, gain, src: ASSETS.sfx[id] });
      for (const sl of S.slots) for (const c of sl.sfx || []) sfx.push({ ...c, t: c.t + S.t0, src: ASSETS.sfx[c.id] });
    }
    // un efect care cade peste vorbire coboară la jumătate: vocea rămâne deasupra
    const spans = vo.map((v) => [v.at, v.at + (v.to - v.from)]);
    for (const c of sfx) if (spans.some(([a, b]) => c.t > a - 0.05 && c.t < b)) c.gain = (c.gain ?? 0.8) * 0.5;
    // muzica: scenele consecutive cu aceeași piesă formează un grup; piesa se repetă cu crossfade, iar grupurile se înlănțuie
    const groups = [];
    for (const S of FILM.scenes) {
      const key = S.mod.music || 'menu';
      if (groups.length && groups[groups.length - 1].key === key) groups[groups.length - 1].t1 = S.t1;
      else groups.push({ key, t0: S.t0, t1: S.t1, from: S.mod.musicFrom || 0 });
    }
    const XF = 1.6;
    groups.forEach((g, gi) => {
      const len = TRACK[g.key], gain = GAIN[g.key], last = gi === groups.length - 1;
      const end = last ? FILM.duration : g.t1 + XF;
      let at = g.t0, from = g.from, first = true;
      while (at < end - 0.05) {
        const to = Math.min(len, from + (end - at));
        const segEnd = at + (to - from);
        music.push({ src: ASSETS.music[g.key], at, from, to, gain, fadeIn: first ? (gi === 0 ? 0.05 : XF) : XF, fadeOut: segEnd >= end - 0.01 ? (last ? 2.5 : XF) : XF });
        at = segEnd - XF; from = 0; first = false;
        if (to < len) break;
      }
    });
    return { music, vo, sfx, duck: 0.72, master: 1 };
  },
};

// Slot de gameplay din capturi (până vine clipul live): Ken Burns, tăieturi scurte între capturi.
// shots: [{key, at?, z?: [z0, z1], f?: [fx, fy]}]; fără `at`, capturile își împart slotul în mod egal.
function drawSlot(ctx, S, sl, lt) {
  const shots = sl.shots;
  const n = shots.length, d = sl.t1 - sl.t0;
  const at = shots.map((s, i) => (s.at != null ? s.at : sl.t0 + (d * i) / n));
  let i = 0; for (let j = 0; j < n; j++) if (lt >= at[j]) i = j;
  const a = at[i], b = i + 1 < n ? at[i + 1] : sl.t1;
  const draw = (k, alpha) => {
    const sh = shots[k], img = I('shot/' + sh.key); if (!img) return;
    const a0 = at[k], b0 = k + 1 < n ? at[k + 1] : sl.t1;
    const p = L.clamp((lt - a0) / Math.max(0.1, b0 - a0));
    const [z0, z1] = sh.z || (k % 2 ? [1.12, 1.03] : [1.03, 1.12]);
    const [fx, fy] = sh.f || [0.5, 0.5];
    const z = L.lerp(z0, z1, E.inOutSine(p));
    const sw = img.width / z, shh = img.height / z;
    screen(ctx); ctx.globalAlpha = alpha; ctx.imageSmoothingEnabled = true;
    ctx.drawImage(img, (img.width - sw) * fx, (img.height - shh) * fy, sw, shh, 0, 0, 1920, 1080);
    ctx.globalAlpha = 1;
  };
  draw(i, 1);
  const xf = 0.16; // tăietură scurtă: un crossfade de câteva cadre
  if (i > 0 && lt - a < xf) draw(i - 1, 1 - (lt - a) / xf);
  if (sl.label) {
    const k = Math.min(L.prog(lt, sl.t0 + 0.2, sl.t0 + 0.5), 1 - L.prog(lt, sl.t1 - 0.35, sl.t1));
    if (k > 0) txt(ctx, sl.label, 72, 1010, { size: 30, align: 'left', color: PAL.paper, alpha: k * 0.9 });
  }
}
