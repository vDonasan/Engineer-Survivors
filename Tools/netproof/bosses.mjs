// Prova dos bosses no navegador (Edge): chegada, luta, fúria, morte, vitória e Modo Infinito, com capturas.
// Uso: node Tools/netproof/bosses.mjs [pastaDaBuild] [porta] <cenário: guirotto|mago>
// guirotto: relógio em 03:53, Guirotto com 25% da vida (entra em fúria e morre rápido).
// mago: relógio em 08:55, Mago com 6% da vida; após a vitória clica em CONTINUAR NO MODO INFINITO.
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const buildDir = resolve(root, process.argv[2] || 'Build/WebGL');
const port = Number(process.argv[3] || 8170);
const scenario = process.argv[4] || 'guirotto';
const outDir = join(root, 'Build', 'shots');
mkdirSync(outDir, { recursive: true });
const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), buildDir, String(port)], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({ headless: false, channel: 'msedge', args: ['--ignore-gpu-blocklist'] });
const log = [];
let last = '';
try {
  await sleep(800);
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('console', (m) => { const t = m.text(); if (t.includes('[ES]') || /error|exception/i.test(t)) { log.push(t); if (t.includes('STATE')) last = t; } });
  const q = scenario === 'mago' ? 't=535&bosshp=0.06' : 't=233&bosshp=0.25';
  await page.goto(`http://localhost:${port}/?mode=solo&spec=staut&log=1&god=1&autopick=1&${q}`);
  const shot = (n) => page.screenshot({ path: join(outDir, `boss-${scenario}-${n}.png`) });
  const waitFor = async (re, ms) => { const end = Date.now() + ms; while (Date.now() < end) { if (re.test(last)) return true; await sleep(500); } return false; };

  if (!(await waitFor(/t=23[4-9]|t=53[6-9]/, 60000))) throw new Error('partida não começou: ' + last);
  await sleep(scenario === 'mago' ? 1500 : 2000);
  await shot('1-aviso');
  await waitFor(/boss\d=/, 20000);
  await sleep(700);
  await shot('2-entrada');
  await sleep(4000);
  await shot('3-luta');
  await sleep(4000);
  await shot('4-luta');
  if (scenario === 'guirotto') {
    for (let i = 0; i < 8; i++) { await sleep(4000); await shot(`5-luta-${i}`); if (/defeated=1/.test(last)) break; }
  } else {
    const won = await waitFor(/phase=Victory/, 120000);
    await sleep(3000);
    await shot('5-vitoria');
    if (won) {
      // botão CONTINUAR NO MODO INFINITO: posição na captura da vitória (env CLICK="x,y")
      if (process.env.CLICK) {
        const [x, y] = process.env.CLICK.split(',').map(Number);
        await page.mouse.move(x, y);
        await sleep(300);
        await page.mouse.down(); await sleep(80); await page.mouse.up();
        await sleep(4000);
        await shot('6-infinito');
        await sleep(8000);
        await shot('7-infinito');
      }
      writeFileSync(join(outDir, `boss-${scenario}-state.txt`), last + '\n');
    }
  }
  writeFileSync(join(outDir, `boss-${scenario}-log.txt`), log.join('\n'));
  console.log(last);
} finally { await browser.close(); server.kill(); }
