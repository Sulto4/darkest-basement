// Stratul clar de deasupra artei (coordonate de ecran 1920×1080): titluri, propoziția scurtă de jos, panouri de cod, etichete.
// Fontul jocului e Pixelify Sans (are ă â î ș ț), codul e în JetBrains Mono.
import * as L from '../engine/lib.js';
import { PAL } from './px.js';

const E = L.ease;
// Pixelify Sans (fontul jocului) are „5” identic cu „S” și „B” aproape de „G”: într-un video cu cifre și BFS ar încurca.
export const FONT = 'Inter', MONO = 'JetBrains Mono', TITLE = 'Micro5', PIX = 'Pixelify Sans';

export function txt(ctx, s, x, y, o = {}) {
  const { size = 40, font = FONT, weight = 600, color = PAL.paper, align = 'center', base = 'middle', alpha = 1, shadow = PAL.outline, sh = Math.max(2, Math.round(size / 16)), spacing = 0, style = 'normal' } = o;
  if (alpha <= 0) return 0;
  ctx.save();
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.globalAlpha = alpha;
  ctx.font = `${style} ${weight} ${size}px "${font}"`;
  // Pixelify Sans are ligatura „fi” care arată ca un R („Recare”): un letter-spacing nenul oprește ligaturile în Chrome
  ctx.letterSpacing = (spacing || 0.01) + 'px';
  ctx.textAlign = align; ctx.textBaseline = base;
  if (shadow) { ctx.fillStyle = shadow; ctx.fillText(s, x + sh, y + sh); }
  ctx.fillStyle = PAL[color] || color;
  ctx.fillText(s, x, y);
  const w = ctx.measureText(s).width;
  ctx.restore();
  return w;
}
export function measure(ctx, s, size, font = FONT, weight = 600) {
  ctx.save(); ctx.font = `${weight} ${size}px "${font}"`; ctx.letterSpacing = '0.01px'; const w = ctx.measureText(s).width; ctx.restore(); return w;
}
// text cu bucăți colorate: parts = [[text, culoare], ...]
export function rich(ctx, parts, x, y, o = {}) {
  const { size = 40, font = FONT, weight = 600, align = 'center' } = o;
  const ws = parts.map(([s]) => measure(ctx, s, size, font, weight));
  const tot = ws.reduce((a, b) => a + b, 0);
  let cx = align === 'center' ? x - tot / 2 : align === 'right' ? x - tot : x;
  parts.forEach(([s, c], i) => { txt(ctx, s, cx, y, { ...o, color: c, align: 'left' }); cx += ws[i]; });
  return tot;
}

// Panou întunecat cu margine „pixel” (pas de 3 px, ca arta ×3).
export function panel(ctx, x, y, w, h, o = {}) {
  const { fill = 'rgba(20,12,10,0.92)', edge = PAL.b3, hi = PAL.b2, alpha = 1 } = o;
  ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.globalAlpha = alpha;
  x = Math.round(x / 3) * 3; y = Math.round(y / 3) * 3; w = Math.round(w / 3) * 3; h = Math.round(h / 3) * 3;
  ctx.fillStyle = PAL.outline; ctx.fillRect(x - 3, y + 3, w + 6, h - 6); ctx.fillRect(x + 3, y - 3, w - 6, h + 6);
  ctx.fillStyle = fill; ctx.fillRect(x, y, w, h);
  ctx.fillStyle = edge; ctx.fillRect(x, y, w, 3); ctx.fillRect(x, y + h - 3, w, 3); ctx.fillRect(x, y, 3, h); ctx.fillRect(x + w - 3, y, 3, h);
  ctx.fillStyle = hi; ctx.fillRect(x + 3, y + 3, w - 6, 3);
  ctx.restore();
}

// ── propoziția de jos: o singură idee pe ecran ────────────────
// k = progresul intrare/ieșire (0..1). Cuvintele dintre * * sunt aurii.
export function caption(ctx, s, k, o = {}) {
  if (!s || k <= 0) return;
  const { y = 968, size = 46 } = o;
  const parts = s.split('*').map((p, i) => [p, i % 2 ? PAL.gold : PAL.paper]).filter(([p]) => p);
  const w = parts.reduce((a, [p]) => a + measure(ctx, p, size), 0);
  const e = E.outCubic(L.clamp(k));
  const dy = (1 - e) * 24;
  panel(ctx, 960 - w / 2 - 36, y - size * 0.8 + dy, w + 72, size * 1.6, { alpha: e * 0.96 });
  rich(ctx, parts, 960, y + dy + 2, { size, alpha: e });
}
// propoziția care intră la a, iese la b
export function captionAt(ctx, t, s, a, b, o) { const k = Math.min(L.prog(t, a, a + 0.35), 1 - L.prog(t, b - 0.3, b)); caption(ctx, s, k, o); }

// ── cartonașul de capitol, ca „BASEMENT / FLOOR 1 OF 3” din joc ──
export function titleCard(ctx, t, { num, title, sub }, dur = 2.6) {
  const kIn = E.outCubic(L.prog(t, 0.05, 0.55)), kOut = 1 - E.inCubic(L.prog(t, dur - 0.45, dur));
  const a = Math.min(kIn, kOut);
  if (a <= 0) return;
  const y = 470 + (1 - kIn) * 30;
  if (num) txt(ctx, `CAPITOLUL ${num}`, 960, y - 110, { size: 34, font: 'Silkscreen', weight: 400, color: PAL.paper2, spacing: 6, alpha: a });
  txt(ctx, title.toUpperCase(), 960, y, { size: 150, font: TITLE, weight: 400, color: PAL.gold, alpha: a, sh: 7, spacing: 3 });
  const w = measure(ctx, title.toUpperCase(), 150, TITLE, 400) + 40;
  const lk = E.inOutCubic(L.prog(t, 0.3, 1.0)) * kOut;
  ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.globalAlpha = a;
  ctx.fillStyle = PAL.outline; ctx.fillRect(960 - (w / 2) * lk + 6, y + 72 + 6, w * lk, 6);
  ctx.fillStyle = PAL.gold; ctx.fillRect(960 - (w / 2) * lk, y + 72, w * lk, 6);
  ctx.restore();
  if (sub) txt(ctx, sub, 960, y + 128, { size: 36, color: PAL.paper2, alpha: a * L.prog(t, 0.5, 0.9) });
}

// ── panoul de cod ─────────────────────────────────────────────
const KW = new Set('public private internal protected static readonly const sealed abstract override virtual class struct enum interface new return if else for foreach while do switch case default break continue in out ref var void int uint long ulong float double bool string char object null true false this base yield using namespace get set unchecked try catch throw is as typeof params'.split(' '));
const TYPES = /^(List|Dictionary|Queue|HashSet|IEnumerator|IReadOnlyList|Unit|HeroUnit|EnemyUnit|CombatEvent|CombatState|DamageResult|GameRng|Room|FloorMap|FlowState|Status|Ease|Mathf|Math|Vector3|Quaternion|Transform|TweenHandle|Tween|KeyValuePair|Exception|Action|Corridor|CardDef|EffectDef|EnemyDef|Damaged|T|Easing|Time|Random|ScreenShake)$/;
export const noLig = (ctx) => { ctx.letterSpacing = '0.01px'; };
function tokens(line) {
  const out = []; let m;
  const re = /(\/\/.*$)|("(?:[^"\\]|\\.)*")|(\b\d+(?:\.\d+)?[fu]?\b)|([A-Za-z_]\w*)|(\s+)|(.)/g;
  while ((m = re.exec(line))) {
    if (m[1]) out.push([m[1], 'com']);
    else if (m[2]) out.push([m[2], 'str']);
    else if (m[3]) out.push([m[3], 'num']);
    else if (m[4]) out.push([m[4], KW.has(m[4]) ? 'kw' : TYPES.test(m[4]) ? 'type' : /^[A-Z]/.test(m[4]) ? 'id2' : 'id']);
    else out.push([m[0], 'p']);
  }
  return out;
}
const SYN = { kw: '#e0b04a', type: '#7fb2e5', str: '#9fd06a', num: '#ff9a55', com: '#8f7f6c', id: '#e8dcc4', id2: '#f2d3c4', p: '#c8b89a' };

// o = { x, y, w, file, lines, hl: [indici evidențiați], k (apariție 0..1), focus (0..1 cât se estompează restul), size, title, mark }
// lines: șiruri (numerotate de la `start`) sau {n, s} din snippets.json (n = null pentru continuarea unui rând rupt; s = '…' = linii sărite)
export function code(ctx, o) {
  const { x, y, w, file, start = 1, k = 1, focus = 0, mark = {} } = o;
  if (k <= 0) return;
  const lines = o.lines.map((l, i) => (typeof l === 'string' ? { n: start + i, s: l } : l));
  const hl = o.re ? hlRe(lines, o.re) : o.hl || [];
  ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0);
  // fontul se micșorează până încap toate rândurile în lățime
  let size = o.size || 26;
  ctx.font = `500 ${size}px "${MONO}"`; ctx.letterSpacing = '0.01px';
  const nmax = String(Math.max(...lines.map((l) => l.n || 0))).length;
  const longest = Math.max(...lines.map((l) => ctx.measureText(l.s).width));
  const avail = w - 48 - ctx.measureText('0'.repeat(nmax)).width - 28;
  if (longest > avail * 0.93) size = Math.max(16, Math.floor((size * avail * 0.93) / longest)); // cuvintele-cheie bold sunt mai late
  const lh = Math.round(size * 1.5), pad = 24, head = 48;
  const h = head + pad + lines.length * lh + pad * 0.6;
  const e = E.outCubic(L.clamp(k));
  ctx.globalAlpha = e;
  const yy = y + (1 - e) * 30;
  panel(ctx, x, yy, w, h, { fill: 'rgba(16,10,9,0.95)', alpha: e });
  ctx.globalAlpha = e;
  ctx.fillStyle = 'rgba(224,176,74,0.10)'; ctx.fillRect(x + 3, yy + 3, w - 6, head - 6);
  txt(ctx, file, x + pad, yy + head / 2 + 1, { size: 24, font: MONO, weight: 600, color: PAL.gold, align: 'left', shadow: null, alpha: e });
  if (o.title) txt(ctx, o.title, x + w - pad, yy + head / 2 + 1, { size: 22, font: FONT, weight: 500, color: PAL.paper2, align: 'right', shadow: null, alpha: e });
  ctx.font = `500 ${size}px "${MONO}"`; ctx.textBaseline = 'middle'; ctx.textAlign = 'left'; ctx.letterSpacing = '0.01px'; // fără ligaturi în cod
  const gutter = ctx.measureText('0'.repeat(nmax)).width + 28;
  lines.forEach((ln, i) => {
    const ly = yy + head + pad * 0.5 + i * lh + lh / 2;
    const isHl = hl.includes(i);
    const la = e * (isHl ? 1 : 1 - 0.6 * focus) * L.clamp((k * (lines.length + 3) - i) / 2);
    if (la <= 0) return;
    if (isHl && focus > 0) { ctx.globalAlpha = la * focus; ctx.fillStyle = 'rgba(224,176,74,0.16)'; ctx.fillRect(x + 6, ly - lh / 2, w - 12, lh); ctx.fillStyle = PAL.gold; ctx.fillRect(x + 6, ly - lh / 2, 6, lh); }
    ctx.font = `500 ${size}px "${MONO}"`;
    ctx.globalAlpha = la * 0.55; ctx.fillStyle = '#8f7f6c'; if (ln.n) ctx.fillText(String(ln.n), x + pad, ly);
    let cx = x + pad + gutter;
    ctx.globalAlpha = la;
    if (ln.s === '…') { ctx.fillStyle = '#8f7f6c'; ctx.fillText('⋮', cx, ly); return; }
    for (const [s, kind] of tokens(ln.s)) {
      ctx.fillStyle = SYN[kind];
      ctx.font = `${kind === 'com' ? 'italic ' : ''}${kind === 'kw' ? 700 : 500} ${size}px "${MONO}"`;
      ctx.fillText(s, cx, ly);
      const tw = ctx.measureText(s).width;
      if (mark[i] && s.trim() && mark[i].includes(s.trim())) { ctx.fillStyle = PAL.gold; ctx.fillRect(cx, ly + size * 0.62, tw, 3); }
      cx += tw;
    }
  });
  ctx.restore();
  return { h, lh, head, pad, size };
}
// bucată din snippets.json. pick (opțional): listă de regex-uri (șiruri) sau intervale [a, b] de indici; un regex alege primul
// rând care se potrivește plus continuările lui (rândurile rupte). Între bucăți neadiacente apare „⋮”.
export function snip(data, id, pick) {
  const s = data.snippets[id];
  if (!s) throw new Error('snippet lipsă: ' + id);
  const all = s.lines;
  let idx = all.map((_, i) => i);
  if (pick) {
    const set = new Set();
    for (const p of pick) {
      if (Array.isArray(p)) { for (let i = p[0]; i <= p[1]; i++) set.add(i); continue; }
      const re = new RegExp(p), i0 = all.findIndex((l, i) => !set.has(i) && re.test(l.s));
      if (i0 < 0) { console.warn(`snippet ${id}: „${p}” nu se potrivește`); continue; }
      set.add(i0); for (let j = i0 + 1; j < all.length && all[j].n == null; j++) set.add(j);
    }
    idx = [...set].sort((a, b) => a - b);
  }
  const lines = [];
  idx.forEach((i, j) => { if (j && i !== idx[j - 1] + 1) lines.push({ n: null, s: '…' }); lines.push(all[i]); });
  return { file: s.file.replace('Assets/_Game/Scripts/', ''), lines };
}
// indicii rândurilor care se potrivesc cu oricare regex (plus continuările lor), pentru evidențiere
export function hlRe(lines, res) {
  const out = [];
  lines.forEach((l, i) => { if (l.s !== '…' && res.some((r) => new RegExp(r).test(l.s))) { out.push(i); for (let j = i + 1; j < lines.length && lines[j].n == null && lines[j].s !== '…'; j++) out.push(j); } });
  return [...new Set(out)];
}

// eticheta mică (pentru a numi lucruri din desen)
export function tag(ctx, s, x, y, o = {}) {
  const { size = 30, color = PAL.paper, bg = 'rgba(27,15,15,0.85)', alpha = 1, align = 'center' } = o;
  if (alpha <= 0) return;
  const w = measure(ctx, s, size) + 28;
  const x0 = align === 'center' ? x - w / 2 : align === 'right' ? x - w : x;
  panel(ctx, x0, y - size * 0.75, w, size * 1.5, { fill: bg, alpha, edge: PAL.b3, hi: PAL.b4 });
  txt(ctx, s, x0 + w / 2, y + 1, { size, color, alpha, shadow: null });
}
