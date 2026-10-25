// Capturas em sequência de uma partida. Uso: node Tools/netproof/burst.mjs <pastaDaBuild> <porta> <prefixo> <query> <inícioMs> <n> <intervaloMs>
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const [dir = 'Build/WebGL', port = '8140', prefix = 'burst', query = 'mode=solo&spec=staut', start = '15000', n = '10', every = '1500'] = process.argv.slice(2);
const outDir = join(root, 'Build', 'shots');
mkdirSync(outDir, { recursive: true });
const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), resolve(root, dir), String(port)], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({ headless: false, channel: 'msedge', args: ['--ignore-gpu-blocklist'] });
try {
  await sleep(800);
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  await page.goto(`http://localhost:${port}/?${query}`);
  await sleep(Number(start));
  for (let i = 0; i < Number(n); i++) {
    await page.screenshot({ path: join(outDir, `${prefix}-${i}.png`) });
    await sleep(Number(every));
  }
} finally { await browser.close(); server.kill(); }
