// Diagnóstico: imprime as mensagens de console (com argumentos) dos primeiros segundos da build.
// Uso: node Tools/netproof/console-dump.mjs Build/WebGL 8083 "?mode=solo"
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const [dir = 'Build/WebGL', port = '8083', query = ''] = process.argv.slice(2);
const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), resolve(root, dir), port], { stdio: 'ignore' });
const browser = await chromium.launch({ headless: true, channel: process.env.ES_BROWSER || undefined });
const page = await browser.newPage();
page.on('console', async (m) => {
  if (!/error|warn/i.test(m.type()) && !/error/i.test(m.text())) return;
  const args = await Promise.all(m.args().map((a) => a.jsonValue().catch(() => '?')));
  console.log(m.type(), JSON.stringify(args).slice(0, 400));
});
await page.goto(`http://localhost:${port}/${query}`);
await page.waitForTimeout(15000);
await browser.close();
server.kill();
