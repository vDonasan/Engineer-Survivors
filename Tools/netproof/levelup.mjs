// Prova da tela de nível: atualizar, banir e pular, com capturas. Uso: node Tools/netproof/levelup.mjs [pastaDaBuild] [porta]
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const buildDir = resolve(root, process.argv[2] || 'Build/WebGL');
const port = Number(process.argv[3] || 8160);
const outDir = join(root, 'Build', 'shots');
mkdirSync(outDir, { recursive: true });
const server = spawn(process.execPath, [join(root, 'Tools', 'serve-local.mjs'), buildDir, String(port)], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const browser = await chromium.launch({ headless: false, channel: 'msedge', args: ['--ignore-gpu-blocklist'] });
let last = { tick: -1, lv: 0 };
const states = [];
try {
  await sleep(800);
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('console', (m) => {
    const t = m.text();
    const s = /STATE .*tick=(\d+).*lv=(\d+)/.exec(t);
    if (s) { last = { tick: +s[1], lv: +s[2] }; states.push(last); }
  });
  await page.goto(`http://localhost:${port}/?mode=solo&spec=staut&log=1`);
  // espera a partida pausar (tick parado = oferta aberta no solo)
  const paused = async (ms) => {
    const end = Date.now() + ms;
    while (Date.now() < end) {
      const a = last.tick;
      await sleep(4500);
      if (a > 0 && last.tick === a) return true;
    }
    return false;
  };
  const shot = (n) => page.screenshot({ path: join(outDir, `levelup-${n}.png`) });
  if (!(await paused(90000))) { await shot('0-falha'); throw new Error('nenhuma oferta abriu: ' + JSON.stringify(states.slice(-3))); }
  // sem clique nenhum: a oferta não pode mudar sozinha (PROBE=1 só observa)
  if (process.env.PROBE === '1') { for (let i = 0; i < 6; i++) { await shot(`probe-${i}`); await sleep(1000); } } else {
  await shot('1-aberta');
  await page.mouse.click(440, 672); // Atualizar cartas
  await sleep(1200);
  await shot('2-atualizada');
  await page.mouse.click(640, 672); // Banir
  await sleep(600);
  await shot('3-modo-banir');
  await page.mouse.click(290, 380); // bane a primeira carta
  await sleep(1200);
  await shot('4-banida');
  await page.mouse.click(840, 672); // Pular
  await sleep(3000);
  await shot('5-pulada');
  const tickAfter = last.tick;
  await sleep(4500);
  console.log(JSON.stringify({ resumed: last.tick > tickAfter, lv: last.lv, samples: states.length }));
  }
} finally { await browser.close(); server.kill(); }
