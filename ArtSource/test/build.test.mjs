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
  for (const e of content.enemies.filter((x) => x.role !== 'boss')) for (const f of [0, 1]) {
    assert.ok(byId.has(`enemy_${e.id}_${f}`) && byId.has(`enemy_${e.id}_elite_${f}`), `inimigo ${e.id}`);
  }
});

test('dígitos 0–9 e pixel 2×2 para números de dano, arcos e anéis', () => {
  for (let d = 0; d <= 9; d++) {
    const s = byId.get(`fx_digit_${d}`);
    assert.ok(s, `dígito ${d}`);
    assert.equal(s.w, 5); assert.equal(s.h, 7);
  }
  const px = byId.get('fx_px');
  assert.ok(px); assert.equal(px.w, 2); assert.equal(px.h, 2);
  const a = img('fx_digit_8'), b = img('fx_digit_0');
  assert.notDeepEqual([...a.data], [...b.data], '8 e 0 diferentes');
});

test('inimigos têm silhueta branca (piscar ao levar dano) com a mesma máscara', () => {
  const content = JSON.parse(readFileSync(join(root, 'Assets', '_Project', 'Content', 'Data', 'sp1-content.json'), 'utf8'));
  for (const e of content.enemies.filter((x) => x.role !== 'boss')) for (const v of ['', 'elite_']) for (const f of [0, 1]) {
    const src = img(`enemy_${e.id}_${v}${f}`), fl = img(`enemy_${e.id}_${v}flash_${f}`);
    assert.equal(fl.w, src.w); assert.equal(fl.h, src.h);
    for (let i = 3; i < src.data.length; i += 4) {
      assert.equal(fl.data[i], src.data[i], `máscara ${e.id}`);
      if (fl.data[i]) assert.ok(fl.data[i - 3] > 220 && fl.data[i - 2] > 220 && fl.data[i - 1] > 220, `branco ${e.id}`);
    }
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

test('10 personagens com todas as animações, retratos e silhuetas distintas', async () => {
  const { HEROES } = await import('../build.mjs');
  assert.equal(HEROES.length, 10);
  for (const id of HEROES) {
    for (const [anim, { dirs, frames }] of Object.entries(CHAR_ANIMS))
      for (const d of dirs) for (let f = 0; f < frames; f++) assert.ok(byId.has(`char_${id}_${anim}_${d}_${f}`), `${id} ${anim} ${d} ${f}`);
    for (const e of ['raiva', 'determinacao', 'vitoria']) assert.ok(byId.has(`portrait_${id}_${e}`), `retrato ${id} ${e}`);
  }
  const front = (id) => img(`char_${id}_idle_down_0`);
  for (let a = 0; a < HEROES.length; a++) for (let b = a + 1; b < HEROES.length; b++) {
    const A = front(HEROES[a]).data, B = front(HEROES[b]).data;
    let diff = 0;
    for (let i = 0; i < A.length; i += 4) if (A[i] !== B[i] || A[i + 1] !== B[i + 1] || A[i + 2] !== B[i + 2] || A[i + 3] !== B[i + 3]) diff++;
    assert.ok(diff >= 20, `${HEROES[a]} × ${HEROES[b]}: só ${diff} px de diferença`);
  }
});

test('sprites de todas as armas dos personagens (enum AllySprite)', () => {
  const cs = readFileSync(join(root, 'Assets', '_Project', 'Sim', 'Abilities', 'Kit', 'KitTypes.cs'), 'utf8');
  const body = cs.slice(cs.indexOf('enum AllySprite'), cs.indexOf('}', cs.indexOf('enum AllySprite')));
  const names = [...body.matchAll(/\b([A-Z][A-Za-z]+),/g)].map((m) => m[1].toLowerCase()).filter((n) => n !== 'byte');
  assert.ok(names.length >= 40);
  for (const n of names) assert.ok(byId.has(`ally_${n}_0`), `ally_${n}_0`);
});

test('bosses: 8 direções (normal e bravo), ataques, dano, fúria, morte e desaparecimento, tamanho constante', () => {
  const content = JSON.parse(readFileSync(join(root, 'Assets', '_Project', 'Content', 'Data', 'sp1-content.json'), 'utf8'));
  const dirs = ['down', 'up', 'left', 'right', 'downleft', 'downright', 'upleft', 'upright'];
  const anims = { idle: 2, idle_bravo: 2, attack: 3, attack_bravo: 3, special: 3, special_bravo: 3, charge: 2, hurt: 2, heavy: 2, rage: 3, death: 4, vanish: 3 };
  assert.equal(content.bosses.length, 2);
  for (const b of content.bosses) {
    const size = byId.get(`boss_${b.id}_idle_down_0`);
    assert.ok(size && size.w >= 48 && size.h >= 48, `boss ${b.id} maior que os personagens`);
    for (const walk of ['walk', 'walk_bravo']) for (const d of dirs) for (let f = 0; f < 4; f++) {
      const s = byId.get(`boss_${b.id}_${walk}_${d}_${f}`);
      assert.ok(s, `boss_${b.id}_${walk}_${d}_${f}`);
      assert.equal(s.w, size.w); assert.equal(s.h, size.h);
    }
    for (const [a, n] of Object.entries(anims)) for (let f = 0; f < n; f++) assert.ok(byId.has(`boss_${b.id}_${a}_down_${f}`), `boss_${b.id}_${a}_down_${f}`);
    // normal e bravo diferem de fato
    assert.notDeepEqual([...img(`boss_${b.id}_idle_down_0`).data], [...img(`boss_${b.id}_idle_bravo_down_0`).data], `bravo ${b.id}`);
  }
  for (const id of ['fx_boss_note', 'fx_boss_wave', 'fx_boss_orb_0', 'fx_boss_spark']) assert.ok(byId.has(id), id);
});

test('eventos e camadas: ícones, coxinhas, moto, emotes, acessórios em 4 direções e âncora da cabeça em todo quadro', () => {
  const content = JSON.parse(readFileSync(join(root, 'Assets', '_Project', 'Content', 'Data', 'sp1-content.json'), 'utf8'));
  for (const e of content.events) assert.ok(byId.has(`icon_evt_${e.id}`), `ícone do evento ${e.id}`);
  for (const id of ['fx_coxinha', 'fx_coxinha_suprema', 'fx_moto_left_0', 'fx_moto_right_1', 'emote_dez', 'emote_zero', 'emote_burro', 'emote_relogio', 'emote_coxinha', 'emote_fogo', 'emote_tontura', 'emote_camiseta', 'emote_foco', 'fx_garrafa', 'fx_engrenagem'])
    assert.ok(byId.has(id), id);
  for (const a of ['chapeu', 'camiseta', 'protoboard', 'protoboard_evo', 'cenoura', 'rubor'])
    for (const d of ['down', 'up', 'left', 'right']) {
      const s = byId.get(`acc_${a}_${d}`);
      assert.ok(s, `acc_${a}_${d}`);
      assert.equal(s.w, 32); assert.equal(s.h, 48);
    }
  const chars = manifest.sprites.filter((s) => s.id.startsWith('char_'));
  assert.ok(chars.length > 500);
  for (const s of chars) {
    assert.ok(Array.isArray(s.anchor) && s.anchor.length === 2, `âncora ${s.id}`);
    assert.ok(s.anchor[0] >= 0 && s.anchor[0] < 32 && s.anchor[1] >= 0 && s.anchor[1] < 32, `âncora válida ${s.id}`);
  }
});
