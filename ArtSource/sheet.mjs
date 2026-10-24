// Folha de contato para revisão: ampliação por fator inteiro (vizinho mais próximo).
// Uso: node sheet.mjs <filtro> <saida.png> [escala] [colunas]
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { encodePng, decodePng } from './lib/png.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const art = join(here, '..', 'Assets', '_Project', 'Art');
const [filter = '', out = join(here, '..', 'Build', 'sheet.png'), s = '4', c = '12'] = process.argv.slice(2);
const scale = Number(s), cols0 = Number(c);
const { sprites } = JSON.parse(readFileSync(join(art, 'manifest.json'), 'utf8'));
const list = sprites.filter((x) => new RegExp(filter).test(x.id));
const cell = Math.max(...list.map((x) => Math.max(x.w, x.h))) * scale + 4;
const cols = Math.min(cols0, list.length), rows = Math.ceil(list.length / cols);
const W = cols * cell, H = rows * cell, buf = new Uint8Array(W * H * 4);
for (let i = 0; i < W * H; i++) { buf[i * 4] = 0x4a; buf[i * 4 + 1] = 0x4f; buf[i * 4 + 2] = 0x57; buf[i * 4 + 3] = 255; }
list.forEach((sp, k) => {
  const img = decodePng(readFileSync(join(art, sp.file)));
  const ox = (k % cols) * cell + 2, oy = Math.floor(k / cols) * cell + 2;
  for (let y = 0; y < img.h * scale; y++) for (let x = 0; x < img.w * scale; x++) {
    const si = ((Math.floor(y / scale) * img.w) + Math.floor(x / scale)) * 4;
    if (!img.data[si + 3]) continue;
    const di = ((oy + y) * W + ox + x) * 4;
    buf[di] = img.data[si]; buf[di + 1] = img.data[si + 1]; buf[di + 2] = img.data[si + 2];
  }
});
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, encodePng(W, H, buf));
console.log(`${list.length} sprites -> ${out}`);
