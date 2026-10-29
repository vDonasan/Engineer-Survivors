// Captura TODO o console de uma partida longa (eventos, boss, itens) e resume as mensagens únicas.
// Uso: node Tools/netproof/console-all.mjs [pastaDaBuild] [porta] [segundos] [query extra]
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const [dir = 'Build/WebGL', port = '8250', seconds = '90', extra = ''] = process.argv.slice(2);
const outDir = join(root, 'Build', 'shots');
mkdirSync(outDir, { recursive: true });
const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), resolve(root, dir), port], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({ headless: false, channel: 'msedge', args: ['--ignore-gpu-blocklist'] });
const all = [];
try {
  await sleep(800);
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('console', (m) => all.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => all.push(`[pageerror] ${e.message}`));
  await page.goto(`http://localhost:${port}/?mode=solo&spec=staut&log=1&god=1&autopick=1&t=228&evt=coxinha,motoqueiro,jalapeno,bolha_social,dez_na_prova,zero_na_prova,chamado_de_burro,laboratorio_incompleto&itens=chapeu_japones,camiseta_contrario,tequila,protoboard,cenoura_podre,motor_astra,iogurte_80${extra}`);
  // CLICK=1: um clique logo no começo, como um jogador (o navegador só libera o áudio depois de um gesto)
  if (process.env.CLICK === '1') { await sleep(4000); await page.mouse.click(640, 120); }
  await sleep(Number(seconds) * 1000);
  await page.screenshot({ path: join(outDir, 'console-all.png') });
} finally {
  await browser.close();
  server.kill();
  writeFileSync(join(outDir, 'console-all.txt'), all.join('\n'));
  const norm = (t) => t.replace(/\d+(\.\d+)?/g, '#').slice(0, 160);
  const counts = new Map();
  for (const l of all) if (!l.includes('[ES] STATE')) counts.set(norm(l), (counts.get(norm(l)) || 0) + 1);
  console.log([...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 40).map(([k, v]) => `${v}\t${k}`).join('\n'));
}
