// Capturas da interface: menu, escolha de personagem e partida (HUD e subida de nível).
// Uso: node Tools/netproof/ui-shots.mjs [pastaDaBuild] [porta]
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const [dir = 'Build/WebGL', port = '8260'] = process.argv.slice(2);
const outDir = join(root, 'Build', 'shots');
mkdirSync(outDir, { recursive: true });
const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), resolve(root, dir), port], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({ headless: false, channel: process.env.ES_BROWSER || 'msedge', args: ['--ignore-gpu-blocklist'] });
try {
  await sleep(800);
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', (e) => console.log('[pageerror]', e.message));
  await page.goto(`http://localhost:${port}/`);
  await sleep(14000);
  await page.screenshot({ path: join(outDir, 'ui-menu.png') });
  await page.goto(`http://localhost:${port}/?mode=solo&spec=miyuki&log=1&god=1&itens=tequila,chapeu_japones,motor_astra&evt=coxinha`);
  await sleep(16000);
  await page.screenshot({ path: join(outDir, 'ui-hud.png') });
  await sleep(14000);
  await page.screenshot({ path: join(outDir, 'ui-levelup.png') });
  console.log('ok');
} finally {
  await browser.close();
  server.kill();
}
