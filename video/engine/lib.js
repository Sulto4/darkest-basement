// Unelte de animație (copiate din voost-motion-reels/engine/lib.js, adaptate la 16:9). Totul e funcție pură de timp:
// draw(t) dă mereu același cadru. Import: import * as L from '../engine/lib.js';

export const W = 1920, H = 1080;

// ── matematică ────────────────────────────────────────────────
export const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
export const lerp = (a, b, k) => a + (b - a) * k;
export const invlerp = (a, b, x) => (x - a) / (b - a);
export const remap = (x, a, b, c, d) => lerp(c, d, clamp(invlerp(a, b, x)));
export const smoothstep = (a, b, x) => { const k = clamp(invlerp(a, b, x)); return k * k * (3 - 2 * k); };
export const fract = (x) => x - Math.floor(x);
export const TAU = Math.PI * 2;

// ── easing (k în 0..1) ────────────────────────────────────────
export const ease = {
  linear: (k) => k,
  inQuad: (k) => k * k, outQuad: (k) => 1 - (1 - k) ** 2,
  inCubic: (k) => k ** 3, outCubic: (k) => 1 - (1 - k) ** 3,
  inOutCubic: (k) => (k < 0.5 ? 4 * k ** 3 : 1 - (-2 * k + 2) ** 3 / 2),
  outQuart: (k) => 1 - (1 - k) ** 4, inQuart: (k) => k ** 4,
  inOutQuart: (k) => (k < 0.5 ? 8 * k ** 4 : 1 - (-2 * k + 2) ** 4 / 2),
  outQuint: (k) => 1 - (1 - k) ** 5, inQuint: (k) => k ** 5,
  inExpo: (k) => (k <= 0 ? 0 : 2 ** (10 * k - 10)),
  outExpo: (k) => (k >= 1 ? 1 : 1 - 2 ** (-10 * k)),
  inOutExpo: (k) => (k <= 0 ? 0 : k >= 1 ? 1 : k < 0.5 ? 2 ** (20 * k - 10) / 2 : (2 - 2 ** (-20 * k + 10)) / 2),
  inOutSine: (k) => -(Math.cos(Math.PI * k) - 1) / 2,
  outSine: (k) => Math.sin((k * Math.PI) / 2),
  outBack: (k, s = 1.70158) => 1 + (s + 1) * (k - 1) ** 3 + s * (k - 1) ** 2,
  inBack: (k, s = 1.70158) => (s + 1) * k ** 3 - s * k * k,
  inOutBack: (k, s = 1.70158 * 1.525) => (k < 0.5 ? ((2 * k) ** 2 * ((s + 1) * 2 * k - s)) / 2 : ((2 * k - 2) ** 2 * ((s + 1) * (k * 2 - 2) + s) + 2) / 2),
  outElastic: (k) => (k <= 0 ? 0 : k >= 1 ? 1 : 2 ** (-10 * k) * Math.sin((k * 10 - 0.75) * (TAU / 3)) + 1),
  outBounce: (k) => {
    const n = 7.5625, d = 2.75;
    if (k < 1 / d) return n * k * k;
    if (k < 2 / d) return n * (k -= 1.5 / d) * k + 0.75;
    if (k < 2.5 / d) return n * (k -= 2.25 / d) * k + 0.9375;
    return n * (k -= 2.625 / d) * k + 0.984375;
  },
};

// Progres eased între momentele a și b (secunde). prog(t, 1, 1.4, ease.outExpo)
export const prog = (t, a, b, e = ease.linear) => e(clamp((t - a) / (b - a)));

// Arc: urcă în [a, a+inD], stă, coboară în [b-outD, b]. Util pentru intrare + ieșire.
export const inout = (t, a, b, inD = 0.3, outD = 0.3, ei = ease.outCubic, eo = ease.inCubic) =>
  Math.min(prog(t, a, a + inD, ei), 1 - prog(t, b - outD, b, eo));

// Arc amortizat 0 → 1 cu depășire (pentru „pop”). dt = secunde de la start.
export function spring(dt, { freq = 2.2, damp = 0.32 } = {}) {
  if (dt <= 0) return 0;
  const w = TAU * freq, z = damp, wd = w * Math.sqrt(1 - z * z);
  return 1 - Math.exp(-z * w * dt) * (Math.cos(wd * dt) + ((z * w) / wd) * Math.sin(wd * dt));
}

// Keyframe-uri: keys(t, [[0, 0], [0.5, 100, ease.outExpo], [1, 80]]) — easing-ul e al segmentului care ÎNCEPE la cheia anterioară și se termină aici.
export function keys(t, ks) {
  if (t <= ks[0][0]) return ks[0][1];
  for (let i = 1; i < ks.length; i++) {
    const [t1, v1, e = ease.inOutCubic] = ks[i];
    if (t <= t1) {
      const [t0, v0] = ks[i - 1];
      const k = e(clamp((t - t0) / (t1 - t0)));
      return Array.isArray(v0) ? v0.map((x, j) => lerp(x, v1[j], k)) : lerp(v0, v1, k);
    }
  }
  return ks[ks.length - 1][1];
}

// ── zgomot și aleator determinist ─────────────────────────────
export function rng(seed = 1) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let x = Math.imul(a ^ (a >>> 15), 1 | a);
    x = (x + Math.imul(x ^ (x >>> 7), 61 | x)) ^ x;
    return ((x ^ (x >>> 14)) >>> 0) / 4294967296;
  };
}
const hash = (n) => fract(Math.sin(n * 127.1 + 311.7) * 43758.5453123);
export function noise(x, seed = 0) {
  const i = Math.floor(x), f = x - i, u = f * f * (3 - 2 * f);
  return lerp(hash(i + seed * 57), hash(i + 1 + seed * 57), u) * 2 - 1;
}
export const wiggle = (t, freq = 3, amp = 10, seed = 0) => noise(t * freq, seed) * amp;

// Shake care se stinge: amplitudine amp la t0, zero după dur.
export function shake(t, t0, dur = 0.35, amp = 24, seed = 3) {
  const k = (t - t0) / dur;
  if (k < 0 || k > 1) return [0, 0];
  const a = amp * (1 - k) ** 2;
  return [noise(t * 40, seed) * a, noise(t * 40, seed + 9) * a];
}

// ── ritm ──────────────────────────────────────────────────────
// beatGrid(bpm, offset): bt(n) = momentul bătăii n; pulse(t, decay) = 1 pe bătaie, scade exponențial.
export function beatGrid(bpm, offset = 0) {
  const spb = 60 / bpm;
  return {
    spb, bpm, offset,
    bt: (n) => offset + n * spb,
    bar: (n, beatsPerBar = 4) => offset + n * beatsPerBar * spb,
    index: (t) => Math.floor((t - offset) / spb),
    pulse: (t, decay = 8) => { const d = t - offset; if (d < 0) return 0; return Math.exp(-((d % spb) * decay)); },
  };
}

// ── culoare ───────────────────────────────────────────────────
export function hex(h) {
  h = h.replace('#', '');
  if (h.length === 3) h = [...h].map((c) => c + c).join('');
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
}
export const rgba = (h, a = 1) => { const [r, g, b] = Array.isArray(h) ? h : hex(h); return `rgba(${r | 0},${g | 0},${b | 0},${a})`; };
export const mix = (a, b, k) => { const A = hex(a), B = hex(b); return rgba(A.map((x, i) => lerp(x, B[i], k))); };

// Paleta Voost (voostvision.ro).
export const VOOST = { black: '#0a0a0a', ink: '#141414', cream: '#f5f4f0', white: '#f5f5f0', accent: '#fb7e3c', deep: '#d9531f', light: '#ffa06a', red: '#d30303' };

// ── canvas ────────────────────────────────────────────────────
export function roundRect(ctx, x, y, w, h, r) {
  r = Math.min(r, w / 2, h / 2);
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

// Setează fontul: font(ctx, 900, 160, 'Inter Tight', 'italic'). Variabilele de lățime (Archivo, Fredoka) merg cu ctx.fontStretch.
export function font(ctx, weight, size, family, style = 'normal') {
  ctx.font = `${style} ${weight} ${size}px "${family}"`;
}

// Text cu letter-spacing (px) și aliniere; întoarce lățimea.
export function text(ctx, s, x, y, { align = 'center', spacing = 0, fill, stroke, lineWidth } = {}) {
  ctx.letterSpacing = spacing + 'px';
  ctx.textAlign = align;
  if (fill) { ctx.fillStyle = fill; ctx.fillText(s, x, y); }
  if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = lineWidth || 4; ctx.strokeText(s, x, y); }
  const w = ctx.measureText(s).width;
  ctx.letterSpacing = '0px';
  return w;
}

// Mărimea maximă de font la care s încape în maxW.
export function fitSize(ctx, s, maxW, weight, family, max = 400, style = 'normal', spacing = 0) {
  font(ctx, weight, 100, family, style);
  ctx.letterSpacing = spacing + 'px';
  const w = ctx.measureText(s).width;
  ctx.letterSpacing = '0px';
  return Math.min(max, (100 * maxW) / w);
}

// Grain fin peste cadru (determinist pe cadru). Folosește cu măsură: umflă MP4-ul.
export function grain(ctx, t, amount = 0.05, seed = 7) {
  const r = rng(Math.floor(t * 30) * 7919 + seed);
  ctx.save();
  ctx.globalAlpha = amount;
  for (let i = 0; i < 1400; i++) {
    ctx.fillStyle = r() < 0.5 ? '#000' : '#fff';
    ctx.fillRect(r() * W, r() * H, 2, 2);
  }
  ctx.restore();
}

// ── voce: cuvintele cu timpii lor ─────────────────────────────
// words = [{w, start, end}] relativ la fișier; placements = [{at, from, to}] din audio.vo.
// Întoarce cuvintele în timpul filmului.
export function placeWords(words, placements) {
  const out = [];
  for (const p of placements) {
    for (const w of words) {
      if (w.start >= (p.from ?? 0) - 1e-3 && w.end <= (p.to ?? 1e9) + 1e-3) out.push({ ...w, start: w.start - (p.from ?? 0) + p.at, end: w.end - (p.from ?? 0) + p.at });
    }
  }
  return out;
}
export const activeWord = (words, t) => words.findIndex((w) => t >= w.start && t < w.end);

// Zona sigură (title-safe ~5%) pentru 1920×1080.
export const SAFE = { top: 54, bottom: 1026, left: 96, right: 1824 };

// ── vocea pe replici ──────────────────────────────────────────
// linesJson = audio/vo/<id>.lines.json; ats[i] = momentul din film în care ÎNCEPE vorbirea replicii i.
// Întoarce { vo (pentru audio.vo), words (în timpul filmului, cu .line), lines ([{n, text, start, end, words}]) }.
// Calculează ats din durate (ex. ats[i] = beat-ul cel mai apropiat de lines[i-1].end + pauză), nu din numere scrise de mână:
// când vocea se regenerează (ElevenLabs), tot clipul se re-sincronizează singur.
export function layoutVO(linesJson, ats, { pre = 0.04, post = 0.14, gain = 1 } = {}) {
  const vo = [], words = [], lines = [];
  linesJson.lines.forEach((l, i) => {
    if (ats[i] == null) return;
    const from = Math.max(0, l.speechStart - pre), to = Math.min(l.dur, l.speechEnd + post);
    const at = ats[i] - (l.speechStart - from);
    vo.push({ src: l.src, at, from, to, gain });
    const ws = l.words.map((w, j) => ({ ...w, line: i, idx: j, start: w.start - from + at, end: w.end - from + at }));
    words.push(...ws);
    lines.push({ n: l.n, text: l.text, start: ats[i], end: l.speechEnd - from + at, words: ws });
  });
  return { vo, words, lines };
}
// Durata de vorbire a fiecărei replici (fără liniștea din fișier).
export const speechDur = (linesJson) => linesJson.lines.map((l) => l.speechEnd - l.speechStart);

// ── grila muzicii ─────────────────────────────────────────────
// bj = audio/music/<piesă>.beats.json; from = de unde pornește piesa (s), at = când intră în film.
export function musicGrid(bj, { from = 0, at = 0 } = {}) {
  const sh = (x) => x - from + at;
  const beats = bj.beats.map(sh).filter((x) => x >= -0.01);
  const bars = bj.bars.map(sh).filter((x) => x >= -0.01);
  const nearest = (arr, t) => arr.reduce((a, b) => (Math.abs(b - t) < Math.abs(a - t) ? b : a), arr[0]);
  const after = (arr, t) => arr.find((b) => b >= t - 1e-6) ?? arr[arr.length - 1];
  return {
    bpm: bj.bpm, spb: 60 / bj.bpm, beats, bars,
    snap: (t) => nearest(beats, t), snapBar: (t) => nearest(bars, t),
    next: (t) => after(beats, t), nextBar: (t) => after(bars, t),
    index: (t) => { let i = -1; for (const b of beats) { if (b <= t) i++; else break; } return i; },
    pulse: (t, decay = 8) => { let last = -1e9; for (const b of beats) { if (b <= t) last = b; else break; } return Math.exp(-(t - last) * decay); },
  };
}

// ── motion blur ───────────────────────────────────────────────
// Desenează scena de n ori în intervalul [t - shutter, t] și face media. drawFn(ctx, t) desenează un cadru complet.
// Folosește doar unde se mișcă repede (cost × n). shutter ~ 0.5/fps.
const _mb = {};
export function motionBlur(ctx, t, drawFn, { n = 5, shutter = 1 / 120 } = {}) {
  if (n <= 1) return drawFn(ctx, t);
  const key = ctx.canvas.width + 'x' + ctx.canvas.height;
  const buf = (_mb[key] ||= Object.assign(document.createElement('canvas'), { width: ctx.canvas.width, height: ctx.canvas.height }));
  const b = buf.getContext('2d');
  ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0);
  for (let i = 0; i < n; i++) {
    b.setTransform(1, 0, 0, 1, 0, 0); b.globalAlpha = 1; b.globalCompositeOperation = 'source-over'; b.filter = 'none';
    b.clearRect(0, 0, buf.width, buf.height);
    drawFn(b, t - shutter * (1 - i / (n - 1)));
    ctx.globalAlpha = 1 / (i + 1);
    ctx.drawImage(buf, 0, 0);
  }
  ctx.restore();
}
