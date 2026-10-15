// Verificare rapidă, fără browser (rulează pe Mac): încarcă filmul în Node, construiește cronologia și planul de sunet,
// afișează scenele, sloturile și segmentele. node tools/check.mjs [--audio]
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const film = (await import(path.join(ROOT, 'film/film.js'))).default;
const loadJSON = async (m) => Object.fromEntries(Object.entries(m).map(([k, p]) => [k, JSON.parse(fs.readFileSync(path.join(ROOT, p)))]));
const pre = await film.preload(loadJSON);
const img = film.images(pre);
film.setup({ img, data: pre });
const fmt = (t) => `${Math.floor(t / 60)}:${(t % 60).toFixed(1).padStart(4, '0')}`;
console.log(`durata: ${fmt(film.duration)} (${film.duration.toFixed(2)} s)`);
for (const s of film.sceneTimes()) console.log(`  ${s.id} ${fmt(s.t0)}–${fmt(s.t1)}  ${(s.t1 - s.t0).toFixed(1)} s  ${s.title}`);
const segs = film.segments();
console.log(`${segs.length} segmente; sloturi:`);
for (const s of segs.filter((x) => x.kind === 'slot')) console.log(`  ${s.id.padEnd(16)} ${fmt(s.f0 / 30)}–${fmt(s.f1 / 30)} (${((s.f1 - s.f0) / 30).toFixed(1)} s)`);
if (process.argv.includes('--audio')) {
  const a = film.audio();
  console.log(`audio: ${a.vo.length} replici, ${a.sfx.length} efecte, ${a.music.length} segmente de muzică`);
  for (const m of a.music) console.log(`  ${fmt(m.at)} ${path.basename(m.src)} ${m.from.toFixed(1)}→${m.to.toFixed(1)} gain ${m.gain}`);
}

const si = process.argv.indexOf('--scene');
if (si > 0) {
  const sc = film.scene(process.argv[si + 1]);
  console.log(`scena ${process.argv[si + 1]}: ${fmt(sc.t0)}–${fmt(sc.t1)}`);
  for (const l of Object.values(sc.lines)) console.log(`  ${String(l.n).padStart(2)} ${(sc.t0 + l.start).toFixed(2).padStart(7)}–${(sc.t0 + l.end).toFixed(2).padStart(7)}  ${l.text.slice(0, 70)}`);
  for (const s of sc.slots) console.log(`  slot ${s.id} ${(sc.t0 + s.t0).toFixed(2)}–${(sc.t0 + s.t1).toFixed(2)}`);
}
