// Folha de contato para revisão visual: node contact-sheet.mjs <filtro> <saida.png> [escala]
import { Resvg } from '@resvg/resvg-js';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const art = join(here, '..', 'Assets', '_Project', 'Art');
const [filter = '', out = join(here, '..', 'Build', 'art-contact.png'), scaleArg = '1'] = process.argv.slice(2);
const scale = Number(scaleArg);
const { sprites } = JSON.parse(readFileSync(join(art, 'manifest.json'), 'utf8'));
const list = sprites.filter((s) => s.id.includes(filter));
const cell = Math.max(...list.map((s) => Math.max(s.w, s.h))) * scale + 8;
const cols = Math.min(12, list.length);
const rows = Math.ceil(list.length / cols);
let body = `<rect width="100%" height="100%" fill="#4a4f57"/>`;
list.forEach((s, i) => {
  const x = (i % cols) * cell + 4, y = Math.floor(i / cols) * cell + 4;
  const data = readFileSync(join(art, s.file)).toString('base64');
  body += `<image x="${x}" y="${y}" width="${s.w * scale}" height="${s.h * scale}" href="data:image/png;base64,${data}"/>`;
});
const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${cols * cell}" height="${rows * cell}">${body}</svg>`;
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, new Resvg(svg).render().asPng());
console.log(`${list.length} sprites -> ${out}`);
