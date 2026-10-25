// Combate real no navegador com cada personagem: partida solo, arma evoluída no nível avançado
// (?evolve=3), 150 inimigos mantidos por perto. Registra FPS, aliados desenhados e erros, e salva
// uma captura de tela por personagem em Build/heroes/.
// Uso: node Tools/netproof/heroes.mjs [pastaDaBuild] [porta] [--edge] [--base]
//   --base: sem evoluir (ataque inicial no nível 1).
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ONLY = process.argv.find((a) => a.startsWith('--only='))?.slice(7);
const ALL = ['staut', 'miyuki', 'giulia', 'breno', 'artur', 'badotti', 'garcia', 'jean', 'fufu', 'donasa'];
const HEROES = ONLY ? ALL.filter((h) => ONLY.split(',').includes(h)) : ALL;
const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '..', '..');
const args = process.argv.slice(2).filter((a) => !a.startsWith('--'));
const edge = process.argv.includes('--edge');
const base = process.argv.includes('--base');
const buildDir = resolve(root, args[0] || 'Build/WebGL');
const port = Number(args[1] || 8083);
const outDir = join(root, 'Build', 'heroes');
mkdirSync(outDir, { recursive: true });

const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), buildDir, String(port)], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({
  headless: !edge,
  channel: edge ? 'msedge' : undefined,
  args: edge ? ['--ignore-gpu-blocklist']
    : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--disable-background-timer-throttling'],
});

const report = { started: new Date().toISOString(), evolved: !base, heroes: {} };
let ok = true;
try {
  for (const id of HEROES) {
    const ctx = await browser.newContext({ viewport: { width: 1280, height: 720 } });
    const page = await ctx.newPage();
    const lines = [];
    page.on('console', (m) => { const t = m.text(); if (t.includes('[ES]') || /exception|error/i.test(t)) lines.push(t); });
    page.on('pageerror', (e) => lines.push('PAGEERROR ' + e.message));
    page.on('crash', () => lines.push('PAGEERROR página travou (crash)'));
    await page.goto(`http://localhost:${port}/?mode=solo&spec=${id}&log=1&bench=150${base ? '' : '&evolve=3'}`);
    const t0 = Date.now();
    while (Date.now() - t0 < 90000 && !lines.some((l) => /STATE.*t=(1[2-9]|[2-9]\d)\./.test(l))) await sleep(500);
    if (!page.isClosed()) await page.screenshot({ path: join(outDir, `${id}${base ? '-base' : ''}.png`) });
    else lines.push('PAGEERROR a página fechou antes da captura');
    const states = lines.filter((l) => l.includes('STATE')).map((l) => {
      const m = l.match(/enemies=(\d+) allies=(\d+).*?fps=(\d+)/);
      return m ? { enemies: +m[1], allies: +m[2], fps: +m[3] } : null;
    }).filter(Boolean);
    const late = states.slice(-3);
    const r = {
      states: states.length,
      maxAllies: Math.max(0, ...states.map((s) => s.allies)),
      fps: late.length ? Math.min(...late.map((s) => s.fps)) : 0,
      enemies: late.at(-1)?.enemies ?? 0,
      evolved: lines.some((l) => l.includes('EVOLVED')),
      errors: lines.filter((l) => l.startsWith('PAGEERROR') || /exception/i.test(l)).slice(0, 5),
    };
    r.ok = r.states >= 3 && r.errors.length === 0 && (base || (r.evolved && r.maxAllies > 0));
    ok &&= r.ok;
    report.heroes[id] = r;
    console.log(id, JSON.stringify(r));
    await ctx.close();
  }
} catch (e) {
  report.error = e.message;
  ok = false;
} finally {
  report.ok = ok;
  writeFileSync(join(outDir, `report${base ? '-base' : ''}.json`), JSON.stringify(report, null, 2));
  console.log('RESULTADO', ok ? 'APROVADO' : 'REPROVADO', report.error || '');
  await browser.close();
  server.kill();
  process.exit(ok ? 0 : 1);
}
