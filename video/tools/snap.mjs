// Capturi pentru QA: node tools/snap.mjs <dir> <t1> <t2> ...      → <dir>/t0012.34.jpg (1920×1080)
//                    node tools/snap.mjs <dir> sheet=<cols> <t...> → plus <dir>/sheet.jpg (miniaturi 480×270 cu timpul scris pe ele)
//                    node tools/snap.mjs <dir> scenes=<k>           → k cadre echidistante din fiecare scenă
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';
import { serve, launch, openFilm } from './serve.mjs';

const [out, ...rest] = process.argv.slice(2);
fs.mkdirSync(out, { recursive: true });
const { srv, base } = await serve();
const browser = await launch();
const p = await openFilm(browser, base);
let times = rest.map(Number).filter((x) => !isNaN(x));
const sh = rest.find((r) => r.startsWith('sheet='));
const sc = rest.find((r) => r.startsWith('scenes='));
if (sc) {
  const k = +sc.split('=')[1];
  const scenes = await p.evaluate(() => window.__film.scenes());
  times = scenes.flatMap((s) => Array.from({ length: k }, (_, i) => +(s.t0 + ((i + 0.5) * (s.t1 - s.t0)) / k).toFixed(2)));
}
const files = [];
for (const t of times) {
  await p.evaluate((t) => window.__film.seek(t), t);
  if (sh || sc) {
    await p.evaluate((t) => {
      const c = document.querySelector('canvas').getContext('2d');
      c.setTransform(1, 0, 0, 1, 0, 0); c.fillStyle = 'rgba(0,0,0,.75)'; c.fillRect(0, 0, 330, 70);
      c.fillStyle = '#ffd34a'; c.font = '700 52px Inter'; c.textAlign = 'left'; c.textBaseline = 'top';
      const m = Math.floor(t / 60), s = (t - m * 60).toFixed(1).padStart(4, '0'); c.fillText(`${m}:${s}`, 14, 10);
    }, t);
  }
  const f = path.join(out, `t${t.toFixed(2).padStart(7, '0')}.jpg`);
  await p.screenshot({ path: f, type: 'jpeg', quality: 88 });
  files.push(f);
}
console.log(files.length, 'capturi în', out);
if (p.errors.length) console.log('ERORI:', p.errors.join(' | '));
await browser.close();
srv.close();
if ((sh || sc) && files.length > 1) {
  const cols = sh ? +sh.split('=')[1] || 4 : 4, rows = Math.ceil(files.length / cols);
  const list = path.join(out, 'sheet-list.txt');
  fs.writeFileSync(list, files.map((f) => `file '${path.resolve(f)}'`).join('\n'));
  execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-f', 'concat', '-safe', '0', '-i', list, '-vf', `scale=480:270,tile=${cols}x${rows}:padding=4:color=0x222222`, '-frames:v', '1', '-q:v', '3', path.join(out, 'sheet.jpg')]);
  console.log(path.join(out, 'sheet.jpg'));
}
