import { test, before } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildAll } from '../build.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const artDir = join(root, 'Assets', '_Project', 'Art');
let manifest;

function pngSize(file) {
  const b = readFileSync(file);
  assert.equal(b.toString('ascii', 1, 4), 'PNG', `${file} não é PNG`);
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) };
}

before(async () => {
  manifest = await buildAll({ outDir: artDir });
});

test('build gera todos os sprites do manifesto com o tamanho declarado', () => {
  assert.ok(manifest.sprites.length > 90, `poucos sprites: ${manifest.sprites.length}`);
  for (const s of manifest.sprites) {
    const file = join(artDir, s.file);
    assert.ok(existsSync(file), `faltando ${s.file}`);
    const { w, h } = pngSize(file);
    assert.equal(w, s.w, `${s.id} largura`);
    assert.equal(h, s.h, `${s.id} altura`);
  }
});

test('ícones existem para todas as habilidades e especiais do conteúdo', () => {
  const content = JSON.parse(readFileSync(join(root, 'Assets', '_Project', 'Content', 'Data', 'sp1-content.json'), 'utf8'));
  const ids = new Set(manifest.sprites.map((s) => s.id));
  for (const a of content.abilities) assert.ok(ids.has(`icon_${a.id}`), `ícone de ${a.id}`);
  for (const s of content.specials) assert.ok(ids.has(`icon_${s.id}`), `ícone de ${s.id}`);
});

test('retratos: 2 especializações × 9 emoções', () => {
  const emotions = ['raiva', 'medo', 'desespero', 'choro', 'exaustao', 'alivio', 'determinacao', 'vitoria', 'derrota'];
  for (const spec of ['eletrica', 'mecanica'])
    for (const e of emotions)
      assert.ok(manifest.sprites.some((s) => s.id === `portrait_${spec}_${e}`), `retrato ${spec}/${e}`);
});

test('personagens e inimigos têm todas as animações', () => {
  const ids = new Set(manifest.sprites.map((s) => s.id));
  for (const spec of ['eletrica', 'mecanica']) {
    for (const view of ['front', 'back', 'side'])
      for (let f = 0; f < 4; f++) assert.ok(ids.has(`char_${spec}_walk_${view}_${f}`), `${spec} andar ${view} ${f}`);
    for (const pose of ['attack', 'special_0', 'special_1', 'downed']) assert.ok(ids.has(`char_${spec}_${pose}`), `${spec} ${pose}`);
  }
  for (const e of ['microbo_enxame', 'prensa_ambulante', 'torreta_inspecao']) {
    assert.ok(ids.has(`enemy_${e}_0`) && ids.has(`enemy_${e}_1`), `quadros de ${e}`);
    assert.ok(ids.has(`enemy_${e}_elite_0`), `elite de ${e}`);
  }
});

test('build é determinístico', async () => {
  const again = await buildAll({ outDir: artDir, dryRun: true });
  assert.deepEqual(again.hashes, manifest.hashes);
});
