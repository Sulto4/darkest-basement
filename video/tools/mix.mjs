// Mixajul final din out/plan.json (scris de render.mjs plan), calculat eșantion cu eșantion în Node.
// Copiat din voost-motion-reels/tools/mix.mjs; adaptat la film lung (≤ 20 min), efecte cu `src`, balanța vocii calculată aici.
// muzică (segmente, fade-uri, ducking sub voce) + voce + efecte → WAV 48 kHz, apoi limiter + loudnorm -14 LUFS.
// Memorie fixă: ~12 MB pe pistă pentru 30 s. ffmpeg doar decodează fișierele și normalizează un singur WAV.
// (Varianta veche cu graf amix/apad/adelay în ffmpeg a umplut 30 GB RAM pe HP Forge pe 11.10.2026 — nu o readuce.)
// Folosire: node tools/mix.mjs <plan.json> <ieșire.wav>   (scrie și vo.wav, music.wav, sfx.wav lângă, pentru QA)
import fs from 'fs';
import path from 'path';
import { execFileSync, spawnSync } from 'child_process';
import { ROOT } from './serve.mjs';

const SR = 48000;
const [planFile, out] = process.argv.slice(2);
const plan = JSON.parse(fs.readFileSync(planFile, 'utf8'));
const D = plan.duration;
if (!(D > 0 && D < 1200)) throw new Error('durată suspectă: ' + D);
const N = Math.ceil(D * SR);

// Decodare (o dată pe fișier) → [L, R] Float32Array
const cache = new Map();
function decode(rel) {
  const p = path.resolve(ROOT, rel);
  if (cache.has(p)) return cache.get(p);
  if (!fs.existsSync(p)) throw new Error('lipsește ' + rel);
  const raw = execFileSync('ffmpeg', ['-v', 'error', '-i', p, '-t', '600', '-f', 'f32le', '-ac', '2', '-ar', String(SR), '-'], { maxBuffer: 1024 * 1024 * 1024, timeout: 120000 });
  const all = new Float32Array(raw.buffer.slice(raw.byteOffset, raw.byteOffset + raw.byteLength));
  const n = all.length / 2, L = new Float32Array(n), R = new Float32Array(n);
  for (let i = 0; i < n; i++) { L[i] = all[2 * i]; R[i] = all[2 * i + 1]; }
  const v = [L, R];
  cache.set(p, v);
  return v;
}

const bus = () => [new Float32Array(N), new Float32Array(N)];
// Adaugă src[from..to] la momentul at, cu gain, pan (-1..1), rate (ca asetrate: >1 = mai sus și mai scurt), fade-uri.
function add(dst, [L, R], { at = 0, from = 0, to, gain = 1, pan, rate = 1, fadeIn = 0, fadeOut = 0 }) {
  const n = L.length;
  const s0 = Math.max(0, Math.round(from * SR));
  const s1 = Math.min(n - 1, to != null ? Math.round(to * SR) : n - 1);
  const len = Math.max(0, Math.floor((s1 - s0) / rate));
  const o = Math.round(at * SR);
  const gl = pan != null ? Math.cos(((pan + 1) * Math.PI) / 4) * Math.SQRT2 : 1;
  const gr = pan != null ? Math.sin(((pan + 1) * Math.PI) / 4) * Math.SQRT2 : 1;
  const fi = Math.round(fadeIn * SR), fo = Math.round(fadeOut * SR);
  for (let k = Math.max(0, -o); k < len && o + k < N; k++) {
    const x = s0 + k * rate, i = Math.floor(x), f = x - i;
    let g = gain;
    if (fi && k < fi) g *= k / fi;
    if (fo && k > len - fo) g *= Math.max(0, (len - k) / fo);
    const j = o + k;
    dst[0][j] += (L[i] + (L[i + 1] - L[i]) * f) * g * gl;
    dst[1][j] += (R[i] + (R[i + 1] - R[i]) * f) * g * gr;
  }
}

// Nivel de referință: fiecare sursă de voce și fiecare segment de muzică e adus la un RMS fix (pe porțiunile
// ne-silențioase) înainte de `gain`, ca balanța să nu depindă de cât de tare e masterizată piesa sau de motorul vocii.
// VO -18 dBFS, muzică -20 dBFS. plan.normalize === false păstrează nivelurile brute.
const NORM = plan.normalize !== false;
function rmsDb([L, R], a = 0, b) {
  const s0 = Math.max(0, Math.round(a * SR)), s1 = Math.min(L.length, b != null ? Math.round(b * SR) : L.length);
  const blk = 2400; let sum = 0, n = 0;
  for (let i = s0; i + blk <= s1; i += blk) {
    let e = 0; for (let j = i; j < i + blk; j++) e += (L[j] * L[j] + R[j] * R[j]) / 2;
    e /= blk; if (e > 1e-5) { sum += e; n++; } // poartă la -50 dB
  }
  return n ? 10 * Math.log10(sum / n) : -100;
}
const normGain = (src, a, b, target) => (NORM ? Math.pow(10, (target - rmsDb(src, a, b)) / 20) : 1);

const vo = bus(), sfx = bus(), mus = bus();
const voFiles = [...new Set(plan.vo.map((v) => v.src))];
// vocea: un singur câștig pentru toate replicile (media lor), ca dinamica dintre replici să rămână
let voNorm = 1;
if (NORM && voFiles.length) { const dbs = voFiles.map((f) => rmsDb(decode(f))).filter((x) => x > -90); voNorm = Math.pow(10, (-18 - dbs.reduce((a, b) => a + b, 0) / dbs.length) / 20); }
for (const v of plan.vo) add(vo, decode(v.src), { at: v.at, from: v.from || 0, to: v.to, gain: (v.gain ?? 1) * voNorm });
for (const s of plan.sfx) add(sfx, decode(s.src), { at: s.t, gain: s.gain ?? 0.8, pan: s.pan, rate: s.rate || 1 });
const segs = plan.music ? (Array.isArray(plan.music) ? plan.music : [plan.music]) : [];
const musNorm = new Map(); // același câștig pentru toate segmentele aceleiași piese
for (const m of segs) {
  const len = m.to != null ? m.to - (m.from || 0) : D - (m.at || 0);
  if (!musNorm.has(m.src)) { const r = segs.filter((x) => x.src === m.src); const a = Math.min(...r.map((x) => x.from || 0)); const b = Math.max(...r.map((x) => (x.to != null ? x.to : (x.from || 0) + D - (x.at || 0)))); musNorm.set(m.src, normGain(decode(m.src), a, b, -20)); }
  add(mus, decode(m.src), { at: m.at || 0, from: m.from || 0, to: (m.from || 0) + len, gain: (m.gain ?? 0.6) * musNorm.get(m.src), fadeIn: m.fadeIn || 0, fadeOut: m.fadeOut || 0 });
}

// Ducking: anvelopa vocii (atac 15 ms, eliberare 350 ms) coboară muzica cu până la `duck` (0.5 ≈ -6 dB).
const duck = plan.duck ?? segs.find((m) => m.duck != null)?.duck ?? 0.5;
if (duck > 0 && plan.vo.length) {
  const aA = Math.exp(-1 / (0.015 * SR)), aR = Math.exp(-1 / (0.35 * SR));
  let env = 0;
  for (let i = 0; i < N; i++) {
    const x = Math.max(Math.abs(vo[0][i]), Math.abs(vo[1][i]));
    env = x > env ? aA * env + (1 - aA) * x : aR * env + (1 - aR) * x;
    const k = Math.min(1, Math.max(0, (env - 0.01) / 0.06));
    const g = 1 - duck * k * k * (3 - 2 * k);
    mus[0][i] *= g; mus[1][i] *= g;
  }
}

function writeWav(file, [L, R], scale = 1) {
  const b = Buffer.alloc(44 + N * 4);
  b.write('RIFF', 0); b.writeUInt32LE(36 + N * 4, 4); b.write('WAVE', 8); b.write('fmt ', 12);
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(2, 22); b.writeUInt32LE(SR, 24); b.writeUInt32LE(SR * 4, 28); b.writeUInt16LE(4, 32); b.writeUInt16LE(16, 34);
  b.write('data', 36); b.writeUInt32LE(N * 4, 40);
  const c = (x) => Math.max(-32768, Math.min(32767, Math.round(x * scale * 32767)));
  for (let i = 0; i < N; i++) { b.writeInt16LE(c(L[i]), 44 + i * 4); b.writeInt16LE(c(R[i]), 46 + i * 4); }
  fs.writeFileSync(file, b);
}

const master = plan.master ?? 1;
const mix = bus();
for (let c = 0; c < 2; c++) for (let i = 0; i < N; i++) mix[c][i] = (mus[c][i] + vo[c][i] + sfx[c][i]) * master;
const dir = path.dirname(out);
if (process.argv.includes('--stems')) for (const [name, b] of [['vo', vo], ['sfx', sfx], ['music', mus]]) writeWav(path.join(dir, name + '.wav'), b);
// Balanța: în ferestrele de 400 ms în care se vorbește, cât de tare e vocea față de muzică și efecte (dB RMS).
{
  const Wn = Math.round(0.4 * SR), rows = [];
  const rms = (b, i) => { let e = 0; for (let j = i; j < i + Wn; j++) { const x = (b[0][j] + b[1][j]) / 2; e += x * x; } return Math.sqrt(e / Wn); };
  const rmsSum = (i) => { let e = 0; for (let j = i; j < i + Wn; j++) { const x = (mus[0][j] + mus[1][j] + sfx[0][j] + sfx[1][j]) / 2; e += x * x; } return Math.sqrt(e / Wn); };
  for (let i = 0; i + Wn < N; i += Wn / 2) { const v = rms(vo, i); if (v > 0.02) rows.push([i / SR, 20 * Math.log10(v / (rmsSum(i) + 1e-9)), 20 * Math.log10(v / (rms(mus, i) + 1e-9)), 20 * Math.log10(v / (rms(sfx, i) + 1e-9))]); }
  const med = (k) => { const a = rows.map((r) => r[k]).sort((x, y) => x - y); return a[Math.floor(a.length / 2)]; };
  const low = rows.filter((r) => r[1] < 6);
  console.log(`balanță: ${rows.length} ferestre cu voce; voce peste (muzică+efecte) median ${med(1).toFixed(1)} dB, minim ${Math.min(...rows.map((r) => r[1])).toFixed(1)} dB; peste muzică ${med(2).toFixed(1)} dB; peste efecte ${med(3).toFixed(1)} dB`);
  console.log('sub 6 dB:', low.slice(0, 30).map((r) => `${r[0].toFixed(1)}s(${r[1].toFixed(1)})`).join(' ') || 'niciuna');
}

// Loudness: ffmpeg doar MĂSOARĂ (loudnorm print_format=json); câștigul și limiterul se aplică aici,
// ca durata să rămână exactă (loudnorm din ffmpeg tăia ~0,09 s de la final).
let peakPre = 0;
for (let c = 0; c < 2; c++) for (let i = 0; i < N; i++) peakPre = Math.max(peakPre, Math.abs(mix[c][i]));
const pre = path.join(dir, 'mix-pre.wav');
const sc = peakPre > 0.98 ? 0.98 / peakPre : 1;
writeWav(pre, mix, sc);
const r = spawnSync('ffmpeg', ['-hide_banner', '-nostats', '-i', pre, '-af', 'loudnorm=I=-14:TP=-1.5:LRA=11:print_format=json', '-f', 'null', '-'], { encoding: 'utf8', timeout: 120000 });
const inI = parseFloat(JSON.parse(r.stderr.slice(r.stderr.lastIndexOf('{'))).input_i);
if (!isFinite(inI)) throw new Error('nu pot măsura loudness-ul');
fs.unlinkSync(pre);
const gain = Math.pow(10, (-14 - inI) / 20) * sc; // inI e măsurat pe fișierul scalat cu sc
// Limiter cu lookahead 5 ms, plafon 0.84 (true peak ~-1 dBFS), eliberare 80 ms.
const CEIL = 0.84, LA = Math.round(0.005 * SR), rel = 1 - Math.exp(-1 / (0.08 * SR));
const req = new Float32Array(N);
for (let i = 0; i < N; i++) { const p = Math.max(Math.abs(mix[0][i]), Math.abs(mix[1][i])) * gain; req[i] = p > CEIL ? CEIL / p : 1; }
const look = new Float32Array(N);
const dq = []; // minim pe fereastra [i, i+LA] cu deque
for (let i = N - 1; i >= 0; i--) {
  while (dq.length && req[dq[dq.length - 1]] >= req[i]) dq.pop();
  dq.push(i);
  while (dq[0] > i + LA) dq.shift();
  look[i] = req[dq[0]];
}
let g = 1, peak = 0;
for (let i = 0; i < N; i++) {
  const t = look[i];
  g = t < g ? t : g + (t - g) * rel;
  for (let c = 0; c < 2; c++) { const x = mix[c][i] * gain * g; mix[c][i] = x; peak = Math.max(peak, Math.abs(x)); }
}
writeWav(out, mix);
console.log(`mix gata: ${out} (${D.toFixed(2)} s, normalizare voce ${(20 * Math.log10(voNorm)).toFixed(1)} dB, muzică ${[...musNorm.values()].map((g) => (20 * Math.log10(g)).toFixed(1)).join('/')} dB, ${cache.size} fișiere, ${plan.sfx.length} efecte, ${plan.vo.length} replici, ${segs.length} segmente muzică; loudness ${inI.toFixed(1)} → -14 LUFS, câștig ${(20 * Math.log10(gain)).toFixed(1)} dB, vârf final ${peak.toFixed(2)})`);
