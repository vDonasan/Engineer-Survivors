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
const target = process.argv[2] || 'Build/WebGL-proof';
// alvo pode ser uma pasta de build (servida localmente) ou uma URL publicada
const remote = /^https?:/.test(target);
const buildDir = remote ? null : resolve(root, target);
const port = Number(process.argv[3] || 8081);
const outDir = join(root, 'Build', 'netproof', process.argv[4] || '');
mkdirSync(outDir, { recursive: true });

const server = remote ? null : spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), buildDir, String(port)], { stdio: 'ignore' });
const base = remote ? target.replace(/\/$/, '') : `http://localhost:${port}`;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = (...a) => console.log(new Date().toISOString().slice(11, 19), ...a);

// ES_BROWSER=msedge usa o Edge instalado (tem o codec AAC do áudio da Unity; o Chromium do Playwright não tem).
const browser = await chromium.launch({
  headless: true,
  channel: process.env.ES_BROWSER || undefined,
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
  const al = l.match(/allies=(\d+)/);
  const players = {};
  // aceita "x.y;z.w" (invariante) e "x,y,z,w" (cultura pt-BR, uma casa decimal)
  for (const p of l.matchAll(/p(\d)=\((-?\d+)[.,](\d);?,?(-?\d+)[.,](\d)\)/g)) players[p[1]] = [Number(p[2] + '.' + p[3]), Number(p[4] + '.' + p[5])];
  return m ? { tick: Number(m[1]), enemies: Number(m[2]), allies: al ? Number(al[1]) : 0, players } : null;
};

const report = { started: new Date().toISOString(), ok: false, checks: {} };
try {
  const hostCtx = await browser.newContext({ viewport: { width: 960, height: 600 } });
  const clientCtx = await browser.newContext({ viewport: { width: 960, height: 600 } });
  const host = await hostCtx.newPage(), client = await clientCtx.newPage();
  const hl = [], cl = [];
  watch(host, 'HOST  ', hl);
  watch(client, 'CLIENT', cl);

  // PROOF_BOSS=1: o host adianta o relógio para 08:55 com bosses fracos e jogadores invulneráveis
  const bossMode = process.env.PROOF_BOSS === '1';
  const bossQ = bossMode ? '&t=535&god=1&autopick=1&bosshp=0.06' : '';
  await host.goto(`${base}/?mode=host&autostart=2&name=Host&spec=giulia&log=1&evolve=3${bossQ}`);
  const code = await waitFor(hl, /CODE=([A-Z0-9]+)/, 120000);
  report.checks.roomCreated = !!code;
  if (!code) throw new Error('host não criou a sala');
  log('código da sala:', code[1]);

  await client.goto(`${base}/?mode=join&code=${code[1]}&name=Cliente&spec=badotti&log=1${bossMode ? '&autopick=1' : ''}`);
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
  report.checks.enemyCountsAgree = !!hLast && !!cLast && Math.abs(hLast.enemies - cLast.enemies) <= Math.max(3, hLast.enemies * 0.15);
  const firstC = cs.find((s) => s.players['1']), lastC = cLast?.players['1'], lastH = hLast?.players['1'];
  report.checks.clientMovedOnHost = !!(firstC && lastH && Math.abs(lastH[0] - firstC.players['1'][0]) > 3);
  report.checks.positionsAgree = !!(lastC && lastH && Math.hypot(lastC[0] - lastH[0], lastC[1] - lastH[1]) < 2.5);
  // tiros, zonas e companheiros chegam ao cliente pelo snapshot (Master do Badotti, armas da Giulia)
  report.checks.clientSeesAllies = cs.some((s) => s.allies >= 2);
  report.checks.noPageErrors = ![...hl, ...cl].some((l) => l.startsWith('PAGEERROR'));
  if (bossMode) {
    // bosses: o cliente vê a chegada e a fúria; a vitória e o Modo Infinito valem para os dois
    const lastLine = (lines) => [...lines].reverse().find((l) => l.includes('STATE')) || '';
    const until = async (fn, ms) => { const end = Date.now() + ms; while (Date.now() < end) { if (fn()) return true; await sleep(1000); } return false; };
    await until(() => /phase=Victory/.test(lastLine(hl)), 150000);
    await sleep(3500);
    await host.screenshot({ path: join(outDir, 'boss-host-vitoria.png') });
    await client.screenshot({ path: join(outDir, 'boss-client-vitoria.png') });
    report.checks.clientSeesBoss = cl.some((l) => /bosses=[1-9]/.test(l));
    report.checks.clientSeesBravo = cl.some((l) => l.includes('/BRAVO'));
    report.checks.bothVictory = /phase=Victory/.test(lastLine(hl)) && /phase=Victory/.test(lastLine(cl));
    const [ix, iy] = (process.env.PROOF_INFINITE_CLICK || '480,422').split(',').map(Number); // botão na janela 960×600
    await host.mouse.move(ix, iy); await sleep(300);
    await host.mouse.down(); await sleep(80); await host.mouse.up();
    await until(() => /infinite=True.*|phase=Running.*infinite=True/.test(lastLine(cl)) && /phase=Running/.test(lastLine(cl)), 20000);
    await sleep(3000);
    await client.screenshot({ path: join(outDir, 'boss-client-infinito.png') });
    report.checks.bothInfinite = /phase=Running.*infinite=True/.test(lastLine(hl)) && /phase=Running.*infinite=True/.test(lastLine(cl));
  }
  report.ok = Object.values(report.checks).every(Boolean);
  writeFileSync(join(outDir, 'host-log.txt'), hl.join('\n'));
  writeFileSync(join(outDir, 'client-log.txt'), cl.join('\n'));
} catch (e) {
  report.error = e.message;
} finally {
  writeFileSync(join(outDir, 'report.json'), JSON.stringify(report, null, 2));
  log('RESULTADO', report.ok ? 'APROVADO' : 'REPROVADO', JSON.stringify(report.checks), report.error || '');
  await browser.close();
  server?.kill();
  process.exit(report.ok ? 0 : 1);
}
