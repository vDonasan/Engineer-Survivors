import { test, before } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildAll, SAMPLE_RATE, BAR_SECONDS } from '../synth.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, '..', '..', 'Assets', '_Project', 'Audio');
let result;

function readWav(file) {
  const b = readFileSync(file);
  assert.equal(b.toString('ascii', 0, 4), 'RIFF');
  assert.equal(b.toString('ascii', 8, 12), 'WAVE');
  const channels = b.readUInt16LE(22), rate = b.readUInt32LE(24), bits = b.readUInt16LE(34);
  const dataLen = b.readUInt32LE(40);
  const samples = new Int16Array(b.buffer, b.byteOffset + 44, dataLen / 2);
  return { channels, rate, bits, samples };
}

before(() => { result = buildAll({ outDir: out }); });

test('cada efeito gera WAV 44,1 kHz mono 16 bits, pico ≤ −1 dBFS e a duração declarada', () => {
  assert.ok(result.sfx.length >= 15, `poucos efeitos: ${result.sfx.length}`);
  for (const s of result.sfx) {
    const w = readWav(join(out, s.file));
    assert.equal(w.channels, 1); assert.equal(w.rate, SAMPLE_RATE); assert.equal(w.bits, 16);
    assert.equal(w.samples.length, Math.round(s.duration * SAMPLE_RATE), `${s.id} duração`);
    let peak = 0; for (const v of w.samples) peak = Math.max(peak, Math.abs(v));
    assert.ok(peak <= Math.round(32767 * 0.8913), `${s.id} pico ${peak}`);
    assert.ok(peak > 3000, `${s.id} silencioso`);
  }
});

test('loop de música tem duração múltipla exata do compasso e emenda sem clique', () => {
  const w = readWav(join(out, result.music.file));
  const bar = Math.round(BAR_SECONDS * SAMPLE_RATE);
  assert.equal(w.samples.length % bar, 0);
  assert.ok(w.samples.length / bar >= 8, 'pelo menos 8 compassos');
  assert.ok(Math.abs(w.samples[0]) < 200 && Math.abs(w.samples[w.samples.length - 1]) < 200);
});

test('efeitos obrigatórios do SP1 existem', () => {
  const ids = new Set(result.sfx.map((s) => s.id));
  for (const id of ['descarga', 'arco', 'impacto_metal', 'braco', 'laser', 'campo', 'explosao', 'apagao', 'turbina',
                    'dano_jogador', 'gema', 'nivel', 'evolucao', 'alarme_surto', 'vitoria', 'derrota', 'clique_ui'])
    assert.ok(ids.has(id), id);
});

test('síntese determinística', () => {
  const again = buildAll({ outDir: out, dryRun: true });
  assert.deepEqual(again.hashes, result.hashes);
});
