// Prova de rede automatizada (Task 20): host e cliente em contextos Chromium separados
// (identidades anônimas diferentes), jogando pela internet via Unity Relay (WSS).
// Uso: node Tools/netproof/proof.mjs [pastaDaBuild] [porta]
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '..', '..');
const buildDir = resolve(root, process.argv[2] || 'Build/WebGL-proof');
const port = Number(process.argv[3] || 8081);
const outDir = join(root, 'Build', 'netproof');
mkdirSync(outDir, { recursive: true });

const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), buildDir, String(port)], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = (...a) => console.log(new Date().toISOString().slice(11, 19), ...a);

const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required',
    '--disable-background-timer-throttling', '--disable-renderer-backgrounding', '--disable-backgrounding-occluded-windows'],
});

function watch(page, name, lines) {
  page.on('console', (m) => {
    const t = m.text();
    if (t.includes('[ES]') || /exception|error/i.test(t)) { lines.push(t); if (!t.includes('STATE')) log(name, t.slice(0, 160)); }
  });
  page.on('pageerror', (e) => { lines.push('PAGEERROR ' + e.message); log(name, 'PAGEERROR', e.message); });
}

async function waitFor(lines, re, timeoutMs) {
  const t0 = Date.now();
  while (Date.now() - t0 < timeoutMs) {
    for (const l of lines) { const m = l.match(re); if (m) return m; }
    await sleep(250);
  }
  return null;
}

const parseState = (l) => {
  const m = l.match(/tick=(\d+).*?enemies=(\d+)/);
  const players = {};
  for (const p of l.matchAll(/p(\d)=\((-?[\d.]+),(-?[\d.]+)\)/g)) players[p[1]] = [Number(p[2]), Number(p[3])];
  return m ? { tick: Number(m[1]), enemies: Number(m[2]), players } : null;
};

const report = { started: new Date().toISOString(), ok: false, checks: {} };
try {
  const hostCtx = await browser.newContext({ viewport: { width: 960, height: 600 } });
  const clientCtx = await browser.newContext({ viewport: { width: 960, height: 600 } });
  const host = await hostCtx.newPage(), client = await clientCtx.newPage();
  const hl = [], cl = [];
  watch(host, 'HOST  ', hl);
  watch(client, 'CLIENT', cl);

  await host.goto(`http://localhost:${port}/?mode=host&autostart=2&name=Host&spec=eletrica`);
  const code = await waitFor(hl, /CODE=([A-Z0-9]+)/, 120000);
  report.checks.roomCreated = !!code;
  if (!code) throw new Error('host não criou a sala');
  log('código da sala:', code[1]);

  await client.goto(`http://localhost:${port}/?mode=join&code=${code[1]}&name=Cliente&spec=mecanica`);
  const started = await waitFor(hl, /START players=(\d)/, 120000);
  report.checks.matchStarted = !!started;
  if (!started) throw new Error('partida não iniciou com 2 jogadores');

  await sleep(8000);
  await client.mouse.click(700, 300);
  await client.keyboard.down('KeyD');
  await sleep(4000);
  await client.keyboard.up('KeyD');
  await sleep(6000);

  await host.screenshot({ path: join(outDir, 'host.png') });
  await client.screenshot({ path: join(outDir, 'client.png') });

  const hs = hl.filter((l) => l.includes('STATE')).map(parseState).filter(Boolean);
  const cs = cl.filter((l) => l.includes('STATE')).map(parseState).filter(Boolean);
  const hLast = hs.at(-1), cLast = cs.at(-1);
  report.host = { states: hs.length, last: hLast };
  report.client = { states: cs.length, last: cLast };
  report.checks.clientReceivesTicks = !!cLast && cLast.tick > 200;
  report.checks.bothSeeEnemies = !!hLast && !!cLast && hLast.enemies > 0 && cLast.enemies > 0;
  const firstC = cs.find((s) => s.players['1']), lastC = cLast?.players['1'], lastH = hLast?.players['1'];
  report.checks.clientMovedOnHost = !!(firstC && lastH && Math.abs(lastH[0] - firstC.players['1'][0]) > 3);
  report.checks.positionsAgree = !!(lastC && lastH && Math.hypot(lastC[0] - lastH[0], lastC[1] - lastH[1]) < 2.5);
  report.checks.noPageErrors = ![...hl, ...cl].some((l) => l.startsWith('PAGEERROR'));
  report.ok = Object.values(report.checks).every(Boolean);
  writeFileSync(join(outDir, 'host-log.txt'), hl.join('\n'));
  writeFileSync(join(outDir, 'client-log.txt'), cl.join('\n'));
} catch (e) {
  report.error = e.message;
} finally {
  writeFileSync(join(outDir, 'report.json'), JSON.stringify(report, null, 2));
  log('RESULTADO', report.ok ? 'APROVADO' : 'REPROVADO', JSON.stringify(report.checks), report.error || '');
  await browser.close();
  server.kill();
  process.exit(report.ok ? 0 : 1);
}
