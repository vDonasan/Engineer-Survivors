// Sintetizador de efeitos e música (sem dependências). Gera WAV 44,1 kHz mono 16 bits.
// Uso: npm --prefix AudioSource run build
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

export const SAMPLE_RATE = 44100;
export const BPM = 100;
export const BAR_SECONDS = (60 / BPM) * 4;
const PEAK = 0.8913; // −1 dBFS

const here = dirname(fileURLToPath(import.meta.url));

function rng(seed) {
  let s = seed >>> 0;
  return () => ((s = (Math.imul(s, 1664525) + 1013904223) >>> 0) / 4294967296) * 2 - 1;
}

/** Renderiza uma camada: oscilador com varredura de frequência, envelope, filtros e distorção. */
function layer(buf, L, seed) {
  const start = Math.round((L.start || 0) * SAMPLE_RATE);
  const len = Math.min(buf.length - start, Math.round((L.length ?? (buf.length / SAMPLE_RATE - (L.start || 0))) * SAMPLE_RATE));
  const noise = rng(seed);
  const a = L.attack ?? 0.005, d = L.decay ?? 0.1, s = L.sustain ?? 0, r = L.release ?? 0.05;
  let phase = 0, lp = 0, hpPrev = 0, hpOut = 0, held = 0;
  for (let i = 0; i < len; i++) {
    const t = i / SAMPLE_RATE, u = i / len;
    const f0 = L.freq ?? 440, f1 = L.freqEnd ?? f0;
    let f = f0 * Math.pow(f1 / f0, u);
    if (L.vibrato) f *= 1 + L.vibrato * Math.sin(2 * Math.PI * (L.vibratoRate || 8) * t);
    phase = (phase + f / SAMPLE_RATE) % 1;
    let v;
    switch (L.wave) {
      case 'square': v = phase < (L.duty ?? 0.5) ? 1 : -1; break;
      case 'saw': v = 2 * phase - 1; break;
      case 'triangle': v = 1 - 4 * Math.abs(phase - 0.5); break;
      case 'noise': if (i % Math.max(1, Math.round(SAMPLE_RATE / Math.max(f, 1) / 2)) === 0) held = noise(); v = L.crush ? held : noise(); break;
      default: v = Math.sin(2 * Math.PI * phase);
    }
    // envelope ADSR (release no fim da camada)
    const tl = len / SAMPLE_RATE;
    let env = t < a ? t / a : t < a + d ? 1 - (1 - s) * ((t - a) / d) : s;
    if (s === 0 && t >= a + d) env = 0;
    if (t > tl - r) env *= Math.max(0, (tl - t) / r);
    v *= env;
    if (L.drive) v = Math.tanh(v * L.drive) / Math.tanh(L.drive);
    if (L.lp) { const lpf = L.lp * Math.pow((L.lpEnd ?? L.lp) / L.lp, u); const k = 1 - Math.exp(-2 * Math.PI * lpf / SAMPLE_RATE); lp += k * (v - lp); v = lp; }
    if (L.hp) { const k = Math.exp(-2 * Math.PI * L.hp / SAMPLE_RATE); hpOut = k * (hpOut + v - hpPrev); hpPrev = v; v = hpOut; }
    buf[start + i] += v * (L.gain ?? 1);
  }
}

function render(duration, layers, seed) {
  const buf = new Float32Array(Math.round(duration * SAMPLE_RATE));
  layers.forEach((L, k) => layer(buf, L, seed * 31 + k));
  return buf;
}

function normalize(buf) {
  let peak = 0;
  for (const v of buf) peak = Math.max(peak, Math.abs(v));
  const g = peak > 0 ? PEAK / peak : 1;
  for (let i = 0; i < buf.length; i++) buf[i] *= g;
  return buf;
}

function fadeEdges(buf, ms = 4) {
  const n = Math.round(SAMPLE_RATE * ms / 1000);
  for (let i = 0; i < n; i++) { const k = i / n; buf[i] *= k; buf[buf.length - 1 - i] *= k; }
  return buf;
}

function wav(buf) {
  const data = Buffer.alloc(buf.length * 2);
  for (let i = 0; i < buf.length; i++) data.writeInt16LE(Math.max(-32767, Math.min(32767, Math.round(buf[i] * 32767))), i * 2);
  const h = Buffer.alloc(44);
  h.write('RIFF', 0); h.writeUInt32LE(36 + data.length, 4); h.write('WAVE', 8); h.write('fmt ', 12);
  h.writeUInt32LE(16, 16); h.writeUInt16LE(1, 20); h.writeUInt16LE(1, 22); h.writeUInt32LE(SAMPLE_RATE, 24);
  h.writeUInt32LE(SAMPLE_RATE * 2, 28); h.writeUInt16LE(2, 32); h.writeUInt16LE(16, 34); h.write('data', 36); h.writeUInt32LE(data.length, 40);
  return Buffer.concat([h, data]);
}

/** Loop industrial de tensão: bumbo, batida metálica, baixo em menor e pad dissonante. */
function music(bars = 8) {
  const beat = 60 / BPM;
  const total = bars * BAR_SECONDS;
  const layers = [];
  const bass = [55, 55, 65.41, 51.91]; // A1, A1, C2, G#1
  for (let b = 0; b < bars; b++) {
    const t0 = b * BAR_SECONDS;
    for (let q = 0; q < 4; q++) {
      const t = t0 + q * beat;
      layers.push({ wave: 'sine', freq: 120, freqEnd: 40, start: t, length: 0.25, attack: 0.001, decay: 0.22, gain: 1.1 }); // bumbo
      if (q % 2 === 1) layers.push({ wave: 'noise', freq: 9000, start: t, length: 0.18, attack: 0.001, decay: 0.15, hp: 1800, gain: 0.35 }); // batida
      layers.push({ wave: 'square', freq: 2400, freqEnd: 1800, duty: 0.2, start: t + beat / 2, length: 0.05, decay: 0.04, gain: 0.08 }); // clique metálico
      layers.push({ wave: 'saw', freq: bass[b % 4], start: t, length: beat * 0.9, attack: 0.01, decay: 0.3, sustain: 0.5, release: 0.08, lp: 420, gain: 0.45 });
    }
    layers.push({ wave: 'sine', freq: 220 * (b % 2 ? 1.06 : 1), start: t0, length: BAR_SECONDS, attack: 0.6, decay: 0.5, sustain: 0.6, release: 0.6, vibrato: 0.004, vibratoRate: 5, gain: 0.12 });
    layers.push({ wave: 'sine', freq: 311.13, start: t0, length: BAR_SECONDS, attack: 0.8, decay: 0.5, sustain: 0.5, release: 0.6, gain: 0.08 });
  }
  return fadeEdges(normalize(render(total, layers, 77)));
}

export function buildAll({ outDir = join(here, '..', 'Assets', '_Project', 'Audio'), dryRun = false } = {}) {
  const defs = JSON.parse(readFileSync(join(here, 'sfx.json'), 'utf8'));
  const result = { sfx: [], music: null, hashes: {} };
  const emit = (rel, buf, id) => {
    const bytes = wav(buf);
    result.hashes[id] = createHash('sha1').update(bytes).digest('hex');
    if (!dryRun) {
      mkdirSync(dirname(join(outDir, rel)), { recursive: true });
      writeFileSync(join(outDir, rel), bytes);
    }
  };
  defs.forEach((d, i) => {
    const buf = fadeEdges(normalize(render(d.duration, d.layers, i + 1)), 2);
    const file = `Sfx/${d.id}.wav`;
    emit(file, buf, d.id);
    result.sfx.push({ id: d.id, file, duration: d.duration });
  });
  const m = music();
  emit('Music/loop_tensao.wav', m, 'loop_tensao');
  result.music = { id: 'loop_tensao', file: 'Music/loop_tensao.wav' };
  return result;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const r = buildAll();
  console.log(`${r.sfx.length} efeitos + 1 música gerados.`);
}
