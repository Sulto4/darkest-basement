// Server static minim pentru video/ + lansarea Chrome (copiat din voost-motion-reels/tools/serve.mjs, adaptat la 1920×1080).
import http from 'http';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { chromium } from 'playwright-core';

export const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const TYPES = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.webp': 'image/webp', '.woff2': 'font/woff2', '.mp3': 'audio/mpeg', '.wav': 'audio/wav', '.glb': 'model/gltf-binary', '.ogg': 'audio/ogg', '.ttf': 'font/ttf' };

export function serve() {
  return new Promise((res) => {
    const srv = http.createServer((req, rsp) => {
      const p = path.join(ROOT, decodeURIComponent(new URL(req.url, 'http://x').pathname));
      if (!p.startsWith(ROOT) || !fs.existsSync(p) || fs.statSync(p).isDirectory()) { rsp.writeHead(404); return rsp.end(); }
      rsp.writeHead(200, { 'content-type': TYPES[path.extname(p)] || 'application/octet-stream' });
      fs.createReadStream(p).pipe(rsp);
    });
    srv.listen(0, '127.0.0.1', () => res({ srv, base: `http://127.0.0.1:${srv.address().port}` }));
  });
}

const CHROME = process.env.CHROME || ['/usr/bin/google-chrome-stable', '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe'].find((p) => fs.existsSync(p));

export async function launch() {
  // Pe Mac (macOS) folosește GPU-ul prin Metal: reel-urile 3D (three.js) randează de ~10–50× mai repede decât SwiftShader de pe Forge.
  // Pe Forge (Linux): placa Intel UHD 630 prin ANGLE + EGL, dacă userul are acces la /dev/dri (grupul render, din 13.10).
  // Fără acces, Chrome rămâne pe SwiftShader (software). Doar „gl-egl” ajunge la GPU; „vulkan”/„gl” cad pe SwiftShader.
  let dri = false; try { fs.accessSync('/dev/dri/renderD128', fs.constants.R_OK | fs.constants.W_OK); dri = true; } catch {}
  // Desktop Main (Windows, RTX 2070 SUPER): ANGLE pe D3D11, implicit pe Windows.
  const gpu = process.platform === 'darwin' ? ['--use-angle=metal', '--enable-gpu', '--ignore-gpu-blocklist']
    : process.platform === 'win32' ? ['--use-angle=d3d11', '--enable-gpu', '--ignore-gpu-blocklist']
    : dri && !process.env.NO_GPU ? ['--use-gl=angle', '--use-angle=gl-egl', '--ignore-gpu-blocklist', '--enable-gpu'] : [];
  return chromium.launch({ executablePath: CHROME, headless: true, args: [...gpu, '--force-color-profile=srgb', '--hide-scrollbars', '--disable-lcd-text', '--font-render-hinting=none', '--autoplay-policy=no-user-gesture-required'] });
}

export async function openFilm(browser, base, id = 'film') {
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1, reducedMotion: 'no-preference' });
  const p = await ctx.newPage();
  const errors = [];
  p.on('pageerror', (e) => { errors.push(e.message); console.log('PAGEERROR', id, e.message); });
  p.on('console', (m) => {
    if (m.type() === 'error' || m.type() === 'warning') console.log('console', m.type(), m.text());
    if (/context lost|CONTEXT_LOST/i.test(m.text())) p.contextLost = true; // WebGL căzut: cadrele 3D ies negre (13.10, 25 pe PC)
  });
  await p.goto(`${base}/engine/player.html?render=1`, { waitUntil: 'load' });
  // 3D (three.js): pe SwiftShader, compilarea shaderelor din setup() poate bloca pagina minute întregi; polling la 1 s, nu pe rAF.
  await p.waitForFunction(() => window.__film, null, { timeout: 900000, polling: 1000 });
  await p.evaluate(() => window.__film.ready);
  p.errors = errors;
  return p;
}
