// Randează filmul pe segmente (o scenă = unul sau mai multe segmente; fiecare slot de gameplay e segmentul lui).
// Cadrele nu ajung pe disc: fiecare worker ține o pagină Chrome și un ffmpeg, randează o bucată continuă de cadre
// și le trimite JPEG prin pipe; bucățile se lipesc fără recodare (concat -c copy).
// Dacă există out/footage/<slot>.mp4 (clip live din build-ul de QA), segmentul slotului se codează din clip, nu din Chrome.
//
//   node tools/render.mjs plan                     → out/plan.json (sunet) + out/segments.json
//   node tools/render.mjs seg s04 s05 [--workers 4] → out/seg/<id>.mp4 pentru segmentele scenelor date (sau id-uri de segment)
//   node tools/render.mjs seg all                  → toate segmentele
//   node tools/render.mjs concat <out.mp4>          → lipește toate segmentele în ordine (doar video) și verifică numărul de cadre
import fs from 'fs';
import path from 'path';
import { spawn, execFileSync } from 'child_process';
import { serve, launch, openFilm, ROOT } from './serve.mjs';

const OUT = path.join(ROOT, 'out');
const SEG = path.join(OUT, 'seg');
const args = process.argv.slice(2);
const cmd = args[0];
const opt = (k, d) => { const i = args.indexOf(k); return i >= 0 ? args[i + 1] : d; };
const WORKERS = +opt('--workers', '4');
const X264 = ['-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p', '-profile:v', 'high', '-level', '4.2', '-g', '60', '-threads', '2', '-an'];
fs.mkdirSync(SEG, { recursive: true });

function frames(file) {
  const o = execFileSync('ffprobe', ['-v', 'error', '-count_packets', '-select_streams', 'v:0', '-show_entries', 'stream=nb_read_packets', '-of', 'csv=p=0', file]).toString().trim();
  return +o;
}

async function withFilm(fn) {
  const { srv, base } = await serve();
  const browser = await launch();
  try { return await fn(browser, base); } finally { await browser.close(); srv.close(); }
}

async function writePlan(page) {
  const plan = await page.evaluate(() => window.__film.audioPlan());
  const segs = await page.evaluate(() => window.__film.segments());
  const scenes = await page.evaluate(() => window.__film.scenes());
  fs.writeFileSync(path.join(OUT, 'plan.json'), JSON.stringify(plan, null, 1));
  fs.writeFileSync(path.join(OUT, 'segments.json'), JSON.stringify({ fps: 30, duration: plan.duration, scenes, segments: segs }, null, 1));
  return { plan, segs };
}

// O bucată continuă [a, b) de cadre globale → un MP4, printr-o pagină și un ffmpeg.
async function renderChunk(page, a, b, file) {
  const ff = spawn('ffmpeg', ['-y', '-v', 'error', '-f', 'image2pipe', '-framerate', '30', '-c:v', 'mjpeg', '-i', '-', ...X264, '-r', '30', file], { stdio: ['pipe', 'inherit', 'inherit'] });
  const done = new Promise((res, rej) => ff.on('close', (c) => (c === 0 ? res() : rej(new Error('ffmpeg ' + c)))));
  const cdp = await page.context().newCDPSession(page);
  for (let i = a; i < b; i++) {
    await page.evaluate((t) => window.__film.seek(t), i / 30);
    const { data } = await cdp.send('Page.captureScreenshot', { format: 'jpeg', quality: 93, optimizeForSpeed: false, captureBeyondViewport: false });
    if (!ff.stdin.write(Buffer.from(data, 'base64'))) await new Promise((r) => ff.stdin.once('drain', r));
  }
  ff.stdin.end();
  await done;
}

async function renderSegments(ids) {
  await withFilm(async (browser, base) => {
    const first = await openFilm(browser, base);
    const { segs } = await writePlan(first);
    const want = ids.includes('all') ? segs : segs.filter((s) => ids.includes(s.id) || ids.includes(s.scene));
    if (!want.length) throw new Error('niciun segment pentru ' + ids.join(' '));
    const pages = [first, ...(await Promise.all(Array.from({ length: WORKERS - 1 }, () => openFilm(browser, base))))];
    for (const s of want) {
      const t0 = Date.now(), file = path.join(SEG, s.id + '.mp4'), n = s.f1 - s.f0;
      const foot = s.footage && path.join(OUT, 'footage', s.footage + '.mp4');
      if (foot && fs.existsSync(foot)) {
        // clip live: îl potrivim la exact n cadre 1920×1080 @30 (taie surplusul, ține ultimul cadru dacă e prea scurt)
        execFileSync('ffmpeg', ['-y', '-v', 'error', '-i', foot, '-vf', `fps=30,scale=1920:1080:flags=neighbor,tpad=stop_mode=clone:stop=${n}`, '-frames:v', String(n), ...X264, file]);
        console.log(`${s.id}: ${n} cadre din footage/${s.footage}.mp4`);
        continue;
      }
      const k = Math.min(pages.length, Math.max(1, Math.ceil(n / 60)));
      const parts = [];
      await Promise.all(Array.from({ length: k }, (_, w) => {
        const a = s.f0 + Math.floor((n * w) / k), b = s.f0 + Math.floor((n * (w + 1)) / k);
        const pf = path.join(SEG, `${s.id}.part${w}.mp4`); parts.push(pf);
        return renderChunk(pages[w], a, b, pf);
      }));
      const list = path.join(SEG, s.id + '.txt');
      fs.writeFileSync(list, parts.map((p) => `file '${p}'`).join('\n'));
      execFileSync('ffmpeg', ['-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', list, '-c', 'copy', file]);
      parts.forEach((p) => fs.rmSync(p)); fs.rmSync(list);
      const got = frames(file);
      console.log(`${s.id}: ${got}/${n} cadre în ${((Date.now() - t0) / 1000).toFixed(0)} s (${((Date.now() - t0) / n).toFixed(0)} ms/cadru)`);
      if (got !== n) throw new Error(`${s.id}: ${got} cadre în loc de ${n}`);
    }
    const errs = pages.flatMap((p) => p.errors);
    if (errs.length) { console.log('ERORI DE PAGINĂ:', errs.slice(0, 5).join(' | ')); process.exitCode = 1; }
  });
}

if (cmd === 'plan') {
  await withFilm(async (browser, base) => { const { plan, segs } = await writePlan(await openFilm(browser, base)); console.log(`plan: ${plan.duration.toFixed(2)} s, ${segs.length} segmente, ${plan.vo.length} replici, ${plan.sfx.length} efecte`); });
} else if (cmd === 'seg') {
  await renderSegments(args.slice(1).filter((a) => !a.startsWith('--') && a !== opt('--workers')));
} else if (cmd === 'concat') {
  const { segments } = JSON.parse(fs.readFileSync(path.join(OUT, 'segments.json')));
  const bad = segments.filter((s) => !fs.existsSync(path.join(SEG, s.id + '.mp4')) || frames(path.join(SEG, s.id + '.mp4')) !== s.f1 - s.f0);
  if (bad.length) { console.error('segmente lipsă sau cu alt număr de cadre (re-randează-le):', bad.map((s) => s.id).join(' ')); process.exit(2); }
  const list = path.join(SEG, 'all.txt');
  fs.writeFileSync(list, segments.map((s) => `file '${path.join(SEG, s.id + '.mp4')}'`).join('\n'));
  execFileSync('ffmpeg', ['-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', list, '-c', 'copy', args[1]]);
  console.log(`video: ${args[1]} (${frames(args[1])} cadre, ${segments.length} segmente)`);
} else {
  console.error('folosire: node tools/render.mjs plan | seg <scene|segmente|all> [--workers N] | concat <out.mp4>'); process.exit(2);
}
