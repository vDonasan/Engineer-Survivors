// Prévia ampliada de sprites gerados em memória (sem escrever na pasta de arte).
// Uso: node preview.mjs <módulo:função> <filtro> <saida.png> [escala] [colunas]
import { writeFileSync } from 'node:fs';
import { encodePng } from './lib/png.mjs';
const [mod, filter, out, s = '4', c = '12'] = process.argv.slice(2);
const [file, fn] = mod.split(':');
const sprites = (await import('./lib/' + file + '.mjs'))[fn](...(fn === 'iconSprites' ? [JSON.parse((await import('node:fs')).readFileSync('../Assets/_Project/Content/Data/sp1-content.json', 'utf8'))] : []));
const list = sprites.filter((x) => new RegExp(filter).test(x.id));
const scale = Number(s), cols = Math.min(Number(c), list.length);
const cell = Math.max(...list.map((x) => Math.max(x.w, x.h))) * scale + 4;
const rows = Math.ceil(list.length / cols), W = cols * cell, H = rows * cell, buf = new Uint8Array(W * H * 4);
for (let i = 0; i < W * H; i++) { buf[i * 4] = 0x6a; buf[i * 4 + 1] = 0x70; buf[i * 4 + 2] = 0x78; buf[i * 4 + 3] = 255; }
list.forEach((sp, k) => {
  const rgba = sp.px.toRGBA(), ox = (k % cols) * cell + 2, oy = Math.floor(k / cols) * cell + 2;
  for (let y = 0; y < sp.h * scale; y++) for (let x = 0; x < sp.w * scale; x++) {
    const si = (Math.floor(y / scale) * sp.w + Math.floor(x / scale)) * 4;
    if (!rgba[si + 3]) continue;
    const di = ((oy + y) * W + ox + x) * 4;
    buf[di] = rgba[si]; buf[di + 1] = rgba[si + 1]; buf[di + 2] = rgba[si + 2];
  }
});
writeFileSync(out, encodePng(W, H, buf));
console.log(`${list.length} sprites -> ${out}`);
