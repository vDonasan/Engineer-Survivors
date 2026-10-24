import { test, before } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildAll, SPECS, CHAR_ANIMS } from '../build.mjs';
import { decodePng } from '../lib/png.mjs';
import { PALETTE_HEX } from '../lib/palette.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const artDir = join(root, 'Assets', '_Project', 'Art');
let manifest;
const byId = new Map();
const img = (id) => decodePng(readFileSync(join(artDir, byId.get(id).file)));

before(() => {
  manifest = buildAll({ outDir: artDir });
  for (const s of manifest.sprites) byId.set(s.id, s);
});

test('todo sprite existe com o tamanho declarado; personagens 32×32', () => {
  for (const s of manifest.sprites) {
    assert.ok(existsSync(join(artDir, s.file)), `faltando ${s.file}`);
    const p = img(s.id);
    assert.equal(p.w, s.w, `${s.id} largura`);
    assert.equal(p.h, s.h, `${s.id} altura`);
    if (s.category === 'Characters') assert.ok(s.w === 32 && s.h === 32, `${s.id} não é 32×32`);
  }
});

test('sem suavização: alfa só 0 ou 255 em personagens, inimigos, tiles, ícones e retratos', () => {
  const strict = new Set(['Characters', 'Enemies', 'Tilesets', 'Environment', 'Abilities', 'Portraits', 'Weapons']);
  for (const s of manifest.sprites) {
    if (!strict.has(s.category)) continue;
    const p = img(s.id);
    for (let i = 3; i < p.data.length; i += 4) assert.ok(p.data[i] === 0 || p.data[i] === 255, `${s.id} alfa ${p.data[i]}`);
  }
});

test('cores de personagens e inimigos dentro da paleta', () => {
  const pal = new Set(PALETTE_HEX);
  for (const s of manifest.sprites) {
    if (s.category !== 'Characters' && s.category !== 'Enemies') continue;
    const p = img(s.id);
    for (let i = 0; i < p.data.length; i += 4) {
      if (p.data[i + 3] === 0) continue;
      const hex = '#' + [0, 1, 2].map((k) => p.data[i + k].toString(16).padStart(2, '0')).join('');
      assert.ok(pal.has(hex), `${s.id} cor fora da paleta ${hex}`);
    }
  }
});

test('7 especializações com todas as animações e direções', () => {
  assert.equal(SPECS.length, 7);
  for (const spec of SPECS)
    for (const [anim, { dirs, frames }] of Object.entries(CHAR_ANIMS))
      for (const d of dirs) for (let f = 0; f < frames; f++) assert.ok(byId.has(`char_${spec}_${anim}_${d}_${f}`), `${spec} ${anim} ${d} ${f}`);
});

test('pés alinhados: última linha opaca igual em todos os quadros de pé', () => {
  const standing = ['idle', 'walk', 'attack', 'special', 'hurt', 'stun', 'victory'];
  for (const spec of SPECS) {
    const rows = new Set();
    for (const s of manifest.sprites) {
      if (!s.id.startsWith(`char_${spec}_`) || !standing.some((a) => s.id.startsWith(`char_${spec}_${a}_`))) continue;
      if (s.id.includes('victory') && s.id.endsWith('_1')) continue; // quadro do pulo
      const p = img(s.id);
      let last = -1;
      for (let y = 0; y < p.h; y++) for (let x = 0; x < p.w; x++) if (p.data[(y * p.w + x) * 4 + 3]) last = y;
      rows.add(last);
    }
    assert.equal(rows.size, 1, `${spec}: bases ${[...rows]}`);
  }
});

test('direita é o espelho exato da esquerda', () => {
  for (const spec of SPECS)
    for (const anim of ['idle', 'walk'])
      for (let f = 0; f < CHAR_ANIMS[anim].frames; f++) {
        const l = img(`char_${spec}_${anim}_left_${f}`), r = img(`char_${spec}_${anim}_right_${f}`);
        for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++)
          for (let k = 0; k < 4; k++) assert.equal(r.data[(y * 32 + x) * 4 + k], l.data[(y * 32 + (31 - x)) * 4 + k], `${spec} ${anim} ${f}`);
      }
});

test('silhuetas das 7 engenharias distintas entre si', () => {
  const masks = SPECS.map((s) => { const p = img(`char_${s}_idle_down_0`); return Array.from({ length: 1024 }, (_, i) => p.data[i * 4 + 3] > 0); });
  for (let a = 0; a < SPECS.length; a++)
    for (let b = a + 1; b < SPECS.length; b++) {
      const diff = masks[a].reduce((n, v, i) => n + (v !== masks[b][i] ? 1 : 0), 0);
      assert.ok(diff >= 12, `${SPECS[a]} × ${SPECS[b]}: só ${diff} px de diferença`);
    }
});

test('retratos 64×64 com 9 emoções para as 7 engenharias', () => {
  const emotions = ['raiva', 'medo', 'desespero', 'choro', 'exaustao', 'alivio', 'determinacao', 'vitoria', 'derrota'];
  for (const spec of SPECS) for (const e of emotions) {
    const s = byId.get(`portrait_${spec}_${e}`);
    assert.ok(s && s.w === 64 && s.h === 64, `retrato ${spec}/${e}`);
  }
});

test('ícones para todas as habilidades e especiais do conteúdo; inimigos com elite', () => {
  const content = JSON.parse(readFileSync(join(root, 'Assets', '_Project', 'Content', 'Data', 'sp1-content.json'), 'utf8'));
  for (const a of content.abilities) assert.ok(byId.has(`icon_${a.id}`), `ícone ${a.id}`);
  for (const s of content.specials) assert.ok(byId.has(`icon_${s.id}`), `ícone ${s.id}`);
  for (const e of content.enemies) for (const f of [0, 1]) {
    assert.ok(byId.has(`enemy_${e.id}_${f}`) && byId.has(`enemy_${e.id}_elite_${f}`), `inimigo ${e.id}`);
  }
});

test('ANIMACOES.md documenta personagens e animações', () => {
  const doc = readFileSync(join(artDir, 'ANIMACOES.md'), 'utf8');
  for (const spec of SPECS) assert.ok(doc.includes(spec), spec);
  for (const anim of Object.keys(CHAR_ANIMS)) assert.ok(doc.includes(anim), anim);
});

test('build determinística', () => {
  const again = buildAll({ outDir: artDir, dryRun: true });
  assert.deepEqual(again.hashes, manifest.hashes);
});
