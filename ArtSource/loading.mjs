// Recursos da tela de carregamento da página (Assets/WebGLTemplates/ES): folha dos heróis correndo, microbôs,
// ícones de itens que flutuam, o fundo do laboratório e a fonte. Rodado por build.mjs depois de gerar a arte.
import { readFileSync, writeFileSync, copyFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { encodePng, decodePng } from './lib/png.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
const art = join(root, 'Assets', '_Project', 'Art');
const out = join(root, 'Assets', 'WebGLTemplates', 'ES');

export const LOADING_HEROES = ['staut', 'miyuki', 'giulia', 'breno', 'artur', 'badotti', 'garcia', 'jean', 'fufu', 'donasa'];
export const LOADING_ICONS = ['tequila', 'mamao', 'iogurte_80', 'chapeu_japones', 'motor_astra', 'cafe_expresso', 'protoboard', 'cenoura_podre', 'camiseta_contrario', 'cachorro_quente', 'kit_arduino', 'overclock'];

function sheet(cells, cw, ch, cols) {
  const rows = Math.ceil(cells.length / cols), W = cols * cw, H = rows * ch, buf = new Uint8Array(W * H * 4);
  cells.forEach((file, k) => {
    if (!file) return;
    const img = decodePng(readFileSync(join(art, file)));
    const ox = (k % cols) * cw + Math.floor((cw - img.w) / 2), oy = Math.floor(k / cols) * ch + (ch - img.h);
    for (let y = 0; y < img.h; y++) for (let x = 0; x < img.w; x++) {
      const s = (y * img.w + x) * 4, d = ((oy + y) * W + ox + x) * 4;
      for (let i = 0; i < 4; i++) buf[d + i] = img.data[s + i];
    }
  });
  return encodePng(W, H, buf);
}

export function buildLoading() {
  const { sprites } = JSON.parse(readFileSync(join(art, 'manifest.json'), 'utf8'));
  const file = new Map(sprites.map((s) => [s.id, s.file]));
  const need = (id) => { const f = file.get(id); if (!f) throw new Error(`carregamento: sem ${id}`); return f; };
  // linhas: um herói por linha (andar para a direita, 4 quadros) e, por último, o microbô (2 quadros)
  const cells = [];
  for (const h of LOADING_HEROES) for (let f = 0; f < 4; f++) cells.push(need(`char_${h}_walk_right_${f}`));
  for (let f = 0; f < 4; f++) cells.push(need(`enemy_microbo_enxame_${f % 2}`));
  writeFileSync(join(out, 'loading-runners.png'), sheet(cells, 32, 32, 4));
  writeFileSync(join(out, 'loading-icons.png'), sheet(LOADING_ICONS.map((id) => need(`icon_${id}`)), 64, 64, LOADING_ICONS.length));
  copyFileSync(join(art, need('ui_menu_bg')), join(out, 'loading-bg.png'));
  copyFileSync(join(root, 'Assets', '_Project', 'UI', 'Fonts', 'PixelifySans.ttf'), join(out, 'PixelifySans.ttf'));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  buildLoading();
  console.log('tela de carregamento: recursos gerados');
}
