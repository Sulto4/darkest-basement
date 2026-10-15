// Scrie out/timings.json: scenele, replicile (timp global) și sloturile. node tools/timings.mjs
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const film = (await import(path.join(ROOT, 'film/film.js'))).default;
const loadJSON = async (m) => Object.fromEntries(Object.entries(m).map(([k, p]) => [k, JSON.parse(fs.readFileSync(path.join(ROOT, p)))]));
const pre = await film.preload(loadJSON);
film.setup({ img: film.images(pre), data: pre });
const out = { duration: film.duration, scenes: film.sceneTimes().map((s) => { const d = film.scene(s.id); return { ...s, lines: Object.values(d.lines).map((l) => ({ n: l.n, t0: d.t0 + l.start, t1: d.t0 + l.end, text: l.text })), slots: d.slots.map((x) => ({ id: x.id, t0: d.t0 + x.t0, t1: d.t0 + x.t1 })) }; }) };
fs.writeFileSync(path.join(ROOT, 'out/timings.json'), JSON.stringify(out, null, 1));
console.log('out/timings.json', out.duration.toFixed(1), 's');
