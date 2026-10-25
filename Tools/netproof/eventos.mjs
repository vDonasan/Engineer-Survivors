// Prova dos eventos e das camadas visuais no navegador (Edge), com capturas.
// Uso: node Tools/netproof/eventos.mjs [pastaDaBuild] [porta] [prefixo] [evt] [itens] [esperaMs] [n] [intervaloMs]
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const [dir = 'Build/WebGL', port = '8200', prefix = 'evt', evt = '', itens = '', wait = '6000', n = '6', every = '2000'] = process.argv.slice(2);
const outDir = join(root, 'Build', 'shots');
mkdirSync(outDir, { recursive: true });
const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), resolve(root, dir), port], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({ headless: false, channel: 'msedge', args: ['--ignore-gpu-blocklist'] });
const log = [];
try {
  await sleep(800);
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('console', (m) => { const t = m.text(); if (t.includes('[ES]') || /exception|error/i.test(t)) log.push(t); });
  page.on('pageerror', (e) => log.push('PAGEERROR ' + e.message));
  const q = `mode=solo&spec=staut&log=1&god=1&autopick=1${evt ? `&evt=${evt}` : ''}${itens ? `&itens=${itens}` : ''}`;
  await page.goto(`http://localhost:${port}/?${q}`);
  // espera a partida começar
  const end = Date.now() + 60000;
  while (Date.now() < end && !log.some((l) => l.includes('STATE') && /t=[1-9]/.test(l))) await sleep(500);
  await sleep(Number(wait));
  for (let i = 0; i < Number(n); i++) { await page.screenshot({ path: join(outDir, `${prefix}-${i}.png`) }); await sleep(Number(every)); }
  writeFileSync(join(outDir, `${prefix}-log.txt`), log.join('\n'));
  console.log(log.filter((l) => /exception|PAGEERROR/i.test(l)).slice(0, 5).join('\n') || 'sem erros');
} finally { await browser.close(); server.kill(); }
