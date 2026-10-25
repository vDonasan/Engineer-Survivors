// Capturas de tela da interface para revisão: menu, escolha de personagem (clicando em Solo) e partida.
// Uso: node Tools/netproof/shots.mjs [pastaDaBuild] [porta] [prefixo]
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const buildDir = resolve(root, process.argv[2] || 'Build/WebGL');
const port = Number(process.argv[3] || 8095);
const prefix = process.argv[4] || 'ui';
const outDir = join(root, 'Build', 'shots');
mkdirSync(outDir, { recursive: true });
const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), buildDir, String(port)], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({ headless: false, channel: 'msedge', args: ['--ignore-gpu-blocklist'] });
try {
  const [vw, vh] = (process.env.VIEW || '1280x720').split('x').map(Number);
  const page = await (await browser.newContext({ viewport: { width: vw, height: vh } })).newPage();
  await page.goto(`http://localhost:${port}/${process.env.START || ''}`);
  await sleep(14000);
  await page.screenshot({ path: join(outDir, `${prefix}-menu.png`) });
  const clicks = (process.env.CLICKS || '').split(';').filter(Boolean);
  let k = 0;
  for (const c of clicks) {
    const [x, y] = c.split(',').map(Number);
    await page.mouse.click(x, y);
    await sleep(1500);
    await page.screenshot({ path: join(outDir, `${prefix}-${++k}.png`) });
  }
  if (process.env.MATCH) {
    await page.goto(`http://localhost:${port}/?${process.env.MATCHQ || `mode=solo&spec=${process.env.MATCH}&log=1&bench=60`}`);
    await sleep(Number(process.env.MATCHWAIT || 22000));
    await page.screenshot({ path: join(outDir, `${prefix}-match.png`) });
  }
} finally { await browser.close(); server.kill(); }
