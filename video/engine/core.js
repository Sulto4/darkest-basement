// Motorul (adaptat din voost-motion-reels/engine/core.js): încarcă film/film.js, pregătește scena 1920×1080
// și expune window.__film pentru randare.
// Previzualizare: engine/player.html  (spațiu = play/pauză, ←/→ = un cadru, Shift+←/→ = 1 s, S = zona sigură, M = mut, &t=123.4 = sare)
// Randare:        engine/player.html?render=1  (fără UI, viewport exact 1920×1080)
import { W, H, SAFE } from './lib.js';

const q = new URLSearchParams(location.search);
const RENDER = q.has('render');
const FPS = 30;
const ROOT = new URL('../', import.meta.url).href; // rădăcina folderului video/

document.body.classList.toggle('render', RENDER);
const frame = document.getElementById('frame');
const canvas = document.createElement('canvas');
canvas.width = W; canvas.height = H;
frame.append(canvas);
const safe = document.createElement('div');
safe.className = 'safe';
safe.style.cssText = `left:${SAFE.left}px;top:${SAFE.top}px;width:${SAFE.right - SAFE.left}px;height:${SAFE.bottom - SAFE.top}px`;
frame.append(safe);

const film = (await import(`${ROOT}film/film.js`)).default;
const url = (p) => new URL(p, ROOT).href;

async function loadImages(map = {}) {
  const out = {};
  await Promise.all(Object.entries(map).map(([k, src]) => new Promise((res, rej) => {
    const im = new Image();
    im.onload = () => res((out[k] = im));
    im.onerror = () => rej(new Error('nu se încarcă imaginea ' + src));
    im.src = url(src);
  })));
  return out;
}
async function loadJSON(map = {}) {
  const out = {};
  await Promise.all(Object.entries(map).map(async ([k, src]) => { out[k] = await (await fetch(url(src))).json(); }));
  return out;
}

const stage = { W, H, canvas, ctx: canvas.getContext('2d'), url, render: RENDER, img: {}, data: {}, fps: FPS };

const ready = (async () => {
  const pre = film.preload ? await film.preload(loadJSON) : {};
  await Promise.all((film.fonts || []).map((f) => document.fonts.load(f, 'AĂÂÎȘȚaăâîșț09{}')));
  await document.fonts.ready;
  stage.img = await loadImages(typeof film.images === 'function' ? film.images(pre) : film.images);
  stage.data = { ...pre, ...(await loadJSON(film.json)) };
  if (film.setup) await film.setup(stage);
  draw(0);
})();

let lastT = -1;
function draw(t) {
  t = Math.max(0, Math.min(film.duration, t));
  const c = stage.ctx;
  c.setTransform(1, 0, 0, 1, 0, 0);
  c.globalAlpha = 1;
  c.globalCompositeOperation = 'source-over';
  c.filter = 'none';
  c.fillStyle = '#000'; c.fillRect(0, 0, W, H);
  film.draw(t, stage);
  lastT = t;
}

// Planul de sunet (căi relative la video/).
function audioPlan() {
  const a = typeof film.audio === 'function' ? film.audio(stage) : film.audio || {};
  return { duration: film.duration, fps: FPS, music: a.music || [], vo: a.vo || [], sfx: (a.sfx || []).slice().sort((x, y) => x.t - y.t), master: a.master ?? 1, duck: a.duck ?? 0.5 };
}

// duration e getter: filmul o calculează în setup() din datele vocii.
window.__film = { ready, get duration() { return film.duration; }, fps: FPS, seek: (t) => draw(t), audioPlan,
  segments: () => (film.segments ? film.segments() : [{ id: 'all', kind: 'scene', f0: 0, f1: Math.round(film.duration * FPS) }]),
  scenes: () => (film.sceneTimes ? film.sceneTimes() : []), title: film.title };

// ── previzualizare cu sunet ───────────────────────────────────
if (!RENDER) {
  await ready;
  const fit = () => { const s = Math.min((innerHeight - 70) / H, innerWidth / W); frame.style.transform = `scale(${s})`; frame.parentElement.style.height = H * s + 'px'; frame.parentElement.style.width = W * s + 'px'; };
  addEventListener('resize', fit); fit();
  const bar = document.getElementById('bar'), time = document.getElementById('time'), play = document.getElementById('play');
  bar.max = film.duration; bar.step = 1 / FPS;
  let ac, buffers = {}, nodes = [], playing = false, t0 = 0, startAt = 0, muted = false;
  const plan = audioPlan();
  async function decode() {
    if (ac) return; ac = new AudioContext();
    const files = new Set([...plan.music.map((m) => m.src), ...plan.vo.map((v) => v.src), ...plan.sfx.map((s) => s.src)].filter(Boolean));
    await Promise.all([...files].map(async (f) => { try { buffers[f] = await ac.decodeAudioData(await (await fetch(url(f))).arrayBuffer()); } catch (e) { console.warn('sunet lipsă', f); } }));
  }
  function src(file, at, gain, from = 0, to) {
    const b = buffers[file]; if (!b) return;
    const now = ac.currentTime, rel = at - t0;
    const off = from + Math.max(0, -rel), len = (to ?? b.duration) - off;
    if (len <= 0) return;
    const s = ac.createBufferSource(), g = ac.createGain();
    s.buffer = b; g.gain.value = muted ? 0 : gain; s.connect(g).connect(ac.destination);
    s.start(now + Math.max(0, rel), off, len); nodes.push(s);
  }
  function startAudio() {
    plan.music.forEach((m) => src(m.src, m.at || 0, (m.gain ?? 0.6) * 0.35, m.from || 0, m.to));
    plan.vo.forEach((v) => src(v.src, v.at, v.gain ?? 1, v.from || 0, v.to));
    plan.sfx.forEach((s) => s.t >= t0 - 0.05 && src(s.src, s.t, (s.gain ?? 0.8) * 0.6));
  }
  const stopAudio = () => { nodes.forEach((n) => { try { n.stop(); } catch {} }); nodes = []; };
  async function toggle() {
    await decode();
    if (playing) { stopAudio(); playing = false; t0 = lastT; play.textContent = '▶'; return; }
    t0 = lastT >= film.duration - 0.01 ? 0 : lastT;
    startAt = ac.currentTime; playing = true; play.textContent = '❚❚'; startAudio(); loop();
  }
  function loop() {
    if (!playing) return;
    const t = t0 + (ac.currentTime - startAt);
    if (t >= film.duration) { draw(film.duration); playing = false; play.textContent = '▶'; return ui(); }
    draw(t); ui(); requestAnimationFrame(loop);
  }
  const ui = () => { bar.value = lastT; time.textContent = lastT.toFixed(2) + ' / ' + film.duration.toFixed(2); };
  const jump = (t) => { if (playing) { stopAudio(); playing = false; play.textContent = '▶'; } draw(t); ui(); };
  bar.oninput = () => jump(+bar.value);
  play.onclick = toggle;
  addEventListener('keydown', (e) => {
    if (e.key === ' ') { e.preventDefault(); toggle(); }
    if (e.key === 'ArrowRight') jump(lastT + (e.shiftKey ? 1 : 1 / FPS));
    if (e.key === 'ArrowLeft') jump(lastT - (e.shiftKey ? 1 : 1 / FPS));
    if (e.key === 's' || e.key === 'S') document.body.classList.toggle('show-safe');
    if (e.key === 'm' || e.key === 'M') { muted = !muted; }
  });
  const tq = parseFloat(q.get('t')); if (!isNaN(tq)) jump(tq);
  ui();
}
