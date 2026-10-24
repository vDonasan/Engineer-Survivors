// Gera a arte do jogo: SVG (por código) -> PNG em Assets/_Project/Art/<Categoria>/<id>.png
// e um manifest.json com tamanho, pivô e bordas (9-slice) de cada sprite.
// Uso: npm --prefix ArtSource run build
import { Resvg } from '@resvg/resvg-js';
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { characterSprites } from './lib/characters.mjs';
import { enemySprites } from './lib/enemies.mjs';
import { arenaSprites } from './lib/arena.mjs';
import { iconSprites } from './lib/icons.mjs';
import { portraitSprites } from './lib/portraits.mjs';
import { fxSprites } from './lib/fx.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');

export function allSprites() {
  const content = JSON.parse(readFileSync(join(root, 'Assets', '_Project', 'Content', 'Data', 'sp1-content.json'), 'utf8'));
  return [...characterSprites(), ...enemySprites(), ...arenaSprites(), ...iconSprites(content), ...portraitSprites(), ...fxSprites()];
}

export async function buildAll({ outDir = join(root, 'Assets', '_Project', 'Art'), dryRun = false } = {}) {
  const sprites = allSprites();
  const ids = new Set();
  const manifest = { sprites: [], hashes: {} };
  for (const s of sprites) {
    if (ids.has(s.id)) throw new Error(`sprite duplicado: ${s.id}`);
    ids.add(s.id);
    const png = new Resvg(s.svg, { fitTo: { mode: 'original' }, font: { loadSystemFonts: false } }).render().asPng();
    const file = `${s.category}/${s.id}.png`;
    manifest.hashes[s.id] = createHash('sha1').update(png).digest('hex');
    manifest.sprites.push({ id: s.id, category: s.category, file, w: s.w, h: s.h, pivot: s.pivot, border: s.border || null });
    if (!dryRun) {
      mkdirSync(join(outDir, s.category), { recursive: true });
      writeFileSync(join(outDir, file), png);
    }
  }
  if (!dryRun) writeFileSync(join(outDir, 'manifest.json'), JSON.stringify({ sprites: manifest.sprites }, null, 2));
  return manifest;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const m = await buildAll();
  console.log(`${m.sprites.length} sprites gerados.`);
}
