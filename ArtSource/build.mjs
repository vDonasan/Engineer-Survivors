// Gera a pixel art do jogo pixel a pixel em PNG (sem rasterizador, sem redimensionamento).
// Uso: npm --prefix ArtSource run build
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync, readFileSync, readdirSync, rmSync, existsSync, statSync } from 'node:fs';
import { join, dirname, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { encodePng } from './lib/png.mjs';
import { characterSprites, SPECS, CHAR_ANIMS } from './lib/characters.mjs';
import { enemySprites } from './lib/enemies.mjs';
import { worldSprites } from './lib/world.mjs';
import { iconSprites } from './lib/icons.mjs';
import { buildLoading } from './loading.mjs';
import { portraitSprites, EMOTIONS } from './lib/portraits.mjs';
import { heroSprites, heroPortraitSprites, HEROES } from './lib/heroes.mjs';
import { allySprites } from './lib/kitart.mjs';
import { uiSprites } from './lib/uiart.mjs';
import { sceneSprites } from './lib/sceneart.mjs';
import { bossSprites, bossFxSprites, BOSS_ANIMS } from './lib/bosses.mjs';
import { eventIcons, eventWorldSprites, accessorySprites, headAnchor } from './lib/eventart.mjs';

export { SPECS, CHAR_ANIMS, HEROES };

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
export const CATEGORIES = ['Characters', 'Enemies', 'Bosses', 'Weapons', 'Abilities', 'Effects', 'Tilesets', 'Environment', 'UI', 'Portraits'];

export function allSprites() {
  const content = JSON.parse(readFileSync(join(root, 'Assets', '_Project', 'Content', 'Data', 'sp1-content.json'), 'utf8'));
  return [...characterSprites(), ...heroSprites(), ...enemySprites(), ...worldSprites(), ...allySprites(), ...uiSprites(), ...sceneSprites(), ...iconSprites(content), ...portraitSprites(), ...heroPortraitSprites(), ...bossSprites(), ...bossFxSprites(), ...eventIcons(), ...eventWorldSprites(), ...accessorySprites()];
}

function listPngs(dir) {
  if (!existsSync(dir)) return [];
  return readdirSync(dir).flatMap((n) => {
    const f = join(dir, n);
    return statSync(f).isDirectory() ? listPngs(f) : n.endsWith('.png') ? [f] : [];
  });
}

function removeEmptyDirs(dir) {
  if (!existsSync(dir)) return;
  for (const n of readdirSync(dir)) { const f = join(dir, n); if (existsSync(f) && statSync(f).isDirectory()) removeEmptyDirs(f); }
  const left = readdirSync(dir).filter((n) => !n.endsWith('.meta'));
  if (left.length === 0 && !CATEGORIES.some((c) => dir.endsWith(sep + c))) {
    rmSync(dir, { recursive: true, force: true });
    rmSync(dir + '.meta', { force: true });
  }
}

function animationsDoc(sprites) {
  const lines = ['# Animações e spritesheets (gerado por ArtSource — não editar à mão)', '',
    'Pixel art nativo. Personagens 32×32 px, PPU 32, pivô nos pés (x=16, y=1 a partir de baixo).',
    'Um quadro por arquivo: `Characters/<engenharia>/char_<engenharia>_<animação>_<direção>_<quadro>.png`.',
    'Direções: `down`, `up`, `left`, `right` (direita = espelho exato da esquerda). Animações sem direção usam `down`.', '',
    '| Animação | Direções | Quadros |', '|---|---|---|',
    ...Object.entries(CHAR_ANIMS).map(([a, { dirs, frames }]) => `| ${a} | ${dirs.join(', ')} | ${frames} |`), '',
    '## Registro por personagem e engenharia', ''];
  for (const spec of [...HEROES, ...SPECS]) {
    const done = Object.entries(CHAR_ANIMS).map(([a, { dirs, frames }]) => {
      const ok = dirs.every((d) => [...Array(frames).keys()].every((f) => sprites.some((s) => s.id === `char_${spec}_${a}_${d}_${f}`)));
      return `${a} ${ok ? '✔' : '✘'}`;
    });
    lines.push(`- **${spec}**: ${done.join(' · ')}`);
  }
  lines.push('', `## Retratos (64×64)`, '', `Emoções: ${EMOTIONS.join(', ')} — \`Portraits/portrait_<personagem ou engenharia>_<emoção>.png\`.`);
  lines.push('', '## Armas dos personagens', '', 'Projéteis, companheiros e zonas em `Weapons/Personagens/ally_<sprite>_<quadro>.png` (nome do enum AllySprite). Cabazord Master 48×48; carro 24×24 com 8 direções.');
  lines.push('', '## Jean', '', 'Montado no porco em todas as animações, menos caído/reviver (ele cai do porco).');
  lines.push('', '## Bosses', '', `Guirotto 48×48 e Mago da Elétrica 48×56 em \`Bosses/<boss>/boss_<boss>_<animação>_<direção>_<quadro>.png\`. Andar (normal e bravo) nas 8 direções (as da direita espelham as da esquerda); demais animações de frente: ${Object.entries(BOSS_ANIMS).map(([a, n]) => `${a} (${n})`).join(', ')}. Projéteis em \`Effects/fx_boss_*\`.`);
  lines.push('', '## Inimigos', '', 'Microbô 16×16, torreta 32×32, prensa 48×48; 2 quadros cada + variante elite (contorno magenta).');
  return lines.join('\n') + '\n';
}

export function buildAll({ outDir = join(root, 'Assets', '_Project', 'Art'), dryRun = false } = {}) {
  const sprites = allSprites();
  const ids = new Set();
  const manifest = { sprites: [], hashes: {} };
  const written = new Set();
  for (const s of sprites) {
    if (ids.has(s.id)) throw new Error(`sprite duplicado: ${s.id}`);
    ids.add(s.id);
    if (s.px.w !== s.w || s.px.h !== s.h) throw new Error(`tamanho incoerente: ${s.id}`);
    const png = encodePng(s.w, s.h, s.px.toRGBA());
    const file = s.sub ? `${s.category}/${s.sub}/${s.id}.png` : `${s.category}/${s.id}.png`;
    manifest.hashes[s.id] = createHash('sha1').update(png).digest('hex');
    manifest.sprites.push({ id: s.id, category: s.category, file, w: s.w, h: s.h, pivot: s.pivot, border: s.border || null, ...(s.anchor || s.id.startsWith('char_') ? { anchor: s.anchor || headAnchor(s.px) } : {}) });
    if (!dryRun) {
      const full = join(outDir, file);
      mkdirSync(dirname(full), { recursive: true });
      const old = existsSync(full) ? readFileSync(full) : null;
      if (!old || !old.equals(png)) writeFileSync(full, png);
      written.add(full);
    }
  }
  if (!dryRun) {
    for (const f of listPngs(outDir)) if (!written.has(f)) { rmSync(f); rmSync(f + '.meta', { force: true }); }
    for (const c of CATEGORIES) mkdirSync(join(outDir, c), { recursive: true });
    removeEmptyDirs(outDir);
    writeFileSync(join(outDir, 'manifest.json'), JSON.stringify({ sprites: manifest.sprites }, null, 1));
    writeFileSync(join(outDir, 'ANIMACOES.md'), animationsDoc(manifest.sprites));
    writeFileSync(join(outDir, 'Bosses', 'LEIA-ME.txt'), 'Guirotto (04:00) e Mago da Elétrica (09:00): gerados por ArtSource/lib/bosses.mjs a partir das imagens de referência.\n');
    if (outDir === join(root, 'Assets', '_Project', 'Art')) buildLoading();
  }
  void relative;
  return manifest;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const m = buildAll();
  console.log(`${m.sprites.length} sprites de pixel art gerados.`);
}
