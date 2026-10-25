// Medição no navegador (spec §12.4): FPS com 300 e 400 inimigos, memória, tamanho do download,
// e capturas de tela (menu, partida) para revisão visual.
// Uso: node Tools/netproof/perf.mjs [pastaDaBuild] [porta] [--headed]
//   --edge usa o Microsoft Edge instalado (GPU real e codec AAC do áudio da Unity).
//   --headed abre uma janela do Chromium com a GPU real (sem ela, o Chromium usa SwiftShader, em software).
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync, readdirSync, statSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '..', '..');
const args = process.argv.slice(2).filter((a) => !a.startsWith('--'));
const edge = process.argv.includes('--edge');
const headed = edge || process.argv.includes('--headed');
const buildDir = resolve(root, args[0] || 'Build/WebGL');
const port = Number(args[1] || 8082);
const outDir = join(root, 'Build', 'perf');
mkdirSync(outDir, { recursive: true });

const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), buildDir, String(port)], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = (...a) => console.log(new Date().toISOString().slice(11, 19), ...a);

const browser = await chromium.launch({
  headless: !headed,
  channel: edge ? 'msedge' : undefined,
  args: headed
    ? ['--ignore-gpu-blocklist', '--disable-background-timer-throttling', '--disable-renderer-backgrounding']
    : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--disable-background-timer-throttling', '--disable-renderer-backgrounding'],
});

function watch(page, lines) {
  page.on('console', (m) => {
    const t = m.text();
    if (t.includes('[ES]') || /exception|error/i.test(t)) lines.push(t);
  });
  page.on('pageerror', (e) => lines.push('PAGEERROR ' + e.message));
}

const parse = (l) => {
  const m = l.match(/tick=(\d+).*?enemies=(\d+).*?fps=(\d+)/);
  return m ? { tick: +m[1], enemies: +m[2], fps: +m[3] } : null;
};

async function measure(n) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  const page = await ctx.newPage();
  const lines = [];
  watch(page, lines);
  await page.goto(`http://localhost:${port}/?mode=solo&bench=${n}&log=1`);
  const t0 = Date.now();
  let states = [];
  while (Date.now() - t0 < 120000) {
    states = lines.filter((l) => l.includes('STATE')).map(parse).filter(Boolean);
    if (states.length && states.at(-1).enemies >= n * 0.9) break;
    await sleep(500);
  }
  const from = states.length;
  await sleep(12000);
  states = lines.filter((l) => l.includes('STATE')).map(parse).filter(Boolean).slice(from);
  await page.screenshot({ path: join(outDir, `bench-${n}.png`) });
  const env = await page.evaluate(() => {
    const gl = document.createElement('canvas').getContext('webgl2');
    const ext = gl && gl.getExtension('WEBGL_debug_renderer_info');
    return {
      renderer: gl ? (ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER)) : 'sem WebGL2',
      jsHeapMB: performance.memory ? Math.round(performance.memory.totalJSHeapSize / 1048576) : null,
      wasmHeapMB: window.esUnity && window.esUnity.Module && window.esUnity.Module.HEAPU8 ? Math.round(window.esUnity.Module.HEAPU8.length / 1048576) : null,
    };
  });
  await ctx.close();
  const fps = states.map((s) => s.fps);
  return {
    target: n, samples: states.length, enemies: states.at(-1)?.enemies ?? 0,
    fpsAvg: fps.length ? Math.round(fps.reduce((a, b) => a + b, 0) / fps.length) : 0,
    fpsMin: fps.length ? Math.min(...fps) : 0,
    errors: lines.filter((l) => l.startsWith('PAGEERROR') || /exception/i.test(l)).slice(0, 5),
    ...env,
  };
}

async function shots() {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  const page = await ctx.newPage();
  const lines = [];
  watch(page, lines);
  await page.goto(`http://localhost:${port}/`);
  await page.waitForFunction(() => window.esUnity, null, { timeout: 120000 });
  await sleep(3000);
  await page.screenshot({ path: join(outDir, 'menu.png') });
  await page.goto(`http://localhost:${port}/?mode=solo&log=1`);
  await page.waitForFunction(() => window.esUnity, null, { timeout: 120000 });
  await sleep(6000);
  await page.screenshot({ path: join(outDir, 'solo-6s.png') });
  await page.keyboard.down('KeyD');
  await sleep(1500);
  await page.keyboard.up('KeyD');
  await page.keyboard.press('Space');
  await sleep(500);
  await page.screenshot({ path: join(outDir, 'solo-especial.png') });
  await sleep(14000);
  await page.screenshot({ path: join(outDir, 'solo-20s.png') });
  await ctx.close();
  return lines.filter((l) => !l.includes('STATE')).slice(0, 30);
}

function buildSize(dir) {
  let total = 0;
  const files = {};
  for (const f of readdirSync(join(dir, 'Build'))) { const s = statSync(join(dir, 'Build', f)).size; files[f] = s; total += s; }
  return { totalMB: +(total / 1048576).toFixed(2), files };
}

const report = { started: new Date().toISOString(), headed, edge, size: buildSize(buildDir) };
try {
  report.consoleAtStart = await shots();
  report.bench300 = await measure(300);
  log('300', JSON.stringify(report.bench300));
  report.bench400 = await measure(400);
  log('400', JSON.stringify(report.bench400));
} catch (e) {
  report.error = e.message;
} finally {
  writeFileSync(join(outDir, 'report.json'), JSON.stringify(report, null, 2));
  log('tamanho', report.size.totalMB, 'MB', report.error || '');
  await browser.close();
  server.kill();
}
