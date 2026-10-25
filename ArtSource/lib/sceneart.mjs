// Cenário com variedade: pisos por ala (laboratório, servidores, copa, oficina, corredor), paredes
// com janelas, cartazes e portas, marcas no chão e móveis que são obstáculos de verdade.
import { C } from './palette.mjs';
import { Px, rect, box, line, disc, ellipse, rng } from './draw.mjs';

const O = C.outline;

function noise(p, seed, n, cols) {
  const r = rng(seed);
  for (let i = 0; i < n; i++) p.set(Math.floor(r() * p.w), Math.floor(r() * p.h), cols[Math.floor(r() * cols.length)]);
  return p;
}

// ---------- pisos 32×32 ----------

function server(v) { // piso elevado perfurado com LEDs de cabo
  const p = new Px(32, 32);
  rect(p, 0, 0, 32, 32, '#1f2430');
  box(p, 0, 0, 32, 32, '#232938', '#151922');
  for (let y = 4; y < 30; y += 4) for (let x = 4; x < 30; x += 4) p.set(x, y, '#11141b');
  if (v === 1) { rect(p, 0, 15, 32, 2, '#151922'); for (let x = 2; x < 32; x += 6) p.set(x, 15, C.cyan1); }
  return p;
}

function wood(v) { // copa: tábuas compridas no sentido horizontal
  const p = new Px(32, 32);
  const cols = ['#7a5636', '#71502f', '#80603e', '#6c4b2d'];
  for (let k = 0; k < 4; k++) {
    rect(p, 0, k * 8, 32, 8, cols[(k + v) % 4]);
    rect(p, 0, k * 8 + 7, 32, 1, '#4e3520');
    rect(p, 0, k * 8, 32, 1, '#8c6a46');
    if ((k + v) % 3 === 0) rect(p, (k * 13 + v * 9) % 32, k * 8 + 1, 1, 6, '#4e3520');
    for (let x = 2; x < 32; x += 7) rect(p, (x + k * 3) % 32, k * 8 + 3 + (x % 3), 3, 1, '#664629');
  }
  return p;
}

function concrete(v) { // oficina
  const p = new Px(32, 32);
  rect(p, 0, 0, 32, 32, '#4a4d52');
  noise(p, 400 + v, 90, ['#3f4247', '#55585d', '#45484d']);
  rect(p, 0, 31, 32, 1, '#383b40'); rect(p, 31, 0, 1, 32, '#383b40');
  if (v === 1) ellipse(p, 16, 16, 9, 5, '#2a2c30', null);
  if (v === 2) { rect(p, 0, 14, 32, 3, C.yellow2); for (let x = 0; x < 32; x += 2) p.set(x, 14 + (x % 3), C.yellow1); }
  return p;
}

function epoxy(v) { // corredor
  const p = new Px(32, 32);
  rect(p, 0, 0, 32, 32, '#2f4448');
  noise(p, 500 + v, 40, ['#2a3d41', '#364c50']);
  rect(p, 0, 0, 32, 1, '#26383b');
  if (v === 1) { rect(p, 0, 14, 32, 1, C.gray3); rect(p, 0, 17, 32, 1, C.gray3); }
  return p;
}

// ---------- paredes 32×32 ----------

function wallBase() {
  const p = new Px(32, 32);
  rect(p, 0, 0, 32, 32, C.gray1); rect(p, 0, 0, 32, 3, C.gray2); rect(p, 0, 29, 32, 3, '#181a20');
  rect(p, 0, 26, 32, 2, C.rust2); rect(p, 0, 26, 32, 1, C.rust3);
  return p;
}

function wallWindow() {
  const p = wallBase();
  box(p, 3, 4, 26, 20, '#0d1630', C.gray3);
  const r = rng(77);
  for (let i = 0; i < 8; i++) p.set(5 + Math.floor(r() * 22), 6 + Math.floor(r() * 6), C.white);
  for (let x = 4; x < 28; x += 4) { const h = 4 + Math.floor(r() * 8); rect(p, x, 23 - h, 3, h, '#0a0f1e'); if (r() < 0.6) p.set(x + 1, 23 - h + 2, C.yellow2); }
  rect(p, 15, 5, 2, 18, C.gray3);
  return p;
}

function wallPoster(v) {
  const p = wallBase();
  if (v === 0) { box(p, 6, 5, 20, 18, C.offwhite); rect(p, 8, 8, 16, 2, C.red2); rect(p, 8, 12, 12, 1, C.gray4); rect(p, 8, 15, 14, 1, C.gray4); rect(p, 8, 18, 9, 1, C.gray4); }
  else { box(p, 4, 6, 24, 16, C.white); line(p, 7, 18, 12, 11, C.cyan1); line(p, 12, 11, 17, 15, C.cyan1); line(p, 17, 15, 24, 9, C.cyan1); rect(p, 6, 22, 20, 2, C.gray3); }
  return p;
}

function wallDoor() {
  const p = wallBase();
  box(p, 8, 4, 16, 25, C.petrol2); rect(p, 10, 7, 12, 6, C.sky1); rect(p, 21, 17, 2, 2, C.yellow2);
  box(p, 11, 0, 10, 4, C.green1); rect(p, 13, 1, 6, 2, C.green2);
  return p;
}

function wallServer(f) {
  const p = wallBase();
  box(p, 4, 3, 24, 23, C.black);
  for (let y = 5; y < 24; y += 3) { rect(p, 6, y, 20, 2, C.gray1); p.set(23, y, (y + f) % 2 ? C.green2 : C.green1); p.set(21, y, (y + f) % 3 ? C.cyan2 : C.gray2); }
  return p;
}

// ---------- marcas no chão (transparentes) ----------

function decal(kind) {
  const p = new Px(32, 32);
  switch (kind) {
    case 'arrow': for (let i = 0; i < 8; i++) { rect(p, 12 - i, 8 + i, 1 + 2 * i, 1, C.yellow2); } rect(p, 9, 16, 6, 9, C.yellow2); break;
    case 'puddle': ellipse(p, 15, 17, 10, 5, '#2d4f63', null); ellipse(p, 12, 16, 3, 1, '#4f7d93', null); break;
    case 'papers': box(p, 6, 8, 10, 12, C.offwhite); rect(p, 8, 11, 6, 1, C.gray4); box(p, 14, 14, 11, 9, C.white); rect(p, 16, 17, 7, 1, C.gray4); break;
    case 'vent': box(p, 6, 6, 20, 20, C.gray2); for (let y = 9; y < 24; y += 3) rect(p, 8, y, 16, 1, O); break;
    case 'drain': disc(p, 15.5, 15.5, 6, C.gray2); for (let x = 12; x < 20; x += 2) rect(p, x, 11, 1, 10, O); break;
    case 'oil': ellipse(p, 16, 16, 11, 7, '#1d1e22', null); ellipse(p, 21, 20, 4, 2, '#1d1e22', null); p.set(13, 14, C.purple3); p.set(14, 14, C.cyan1); break;
    case 'tape': for (let i = 0; i < 32; i++) { p.set(i, i, C.yellow2); p.set(i, 31 - i, C.yellow2); if (i % 4 < 2) { p.set(i, i, O); p.set(i, 31 - i, O); } } break;
    case 'coffee': ellipse(p, 16, 17, 6, 4, '#4a2c18', null); ellipse(p, 22, 22, 2, 1, '#4a2c18', null); break;
    case 'rug': box(p, 1, 4, 30, 24, C.red1, C.rust1); box(p, 4, 7, 24, 18, null, C.yellow1); break;
  }
  return p;
}

function logo() { // 96×96 no centro da arena
  const p = new Px(96, 96);
  disc(p, 47.5, 47.5, 44, '#29393c', '#1d2b2e');
  disc(p, 47.5, 47.5, 38, null, C.yellow1);
  for (let a = 0; a < 12; a++) { const t = a * Math.PI / 6; rect(p, Math.round(47 + Math.cos(t) * 41), Math.round(47 + Math.sin(t) * 41), 3, 3, C.yellow2); }
  const E = ['OOOOO', 'O....', 'OOOO.', 'O....', 'OOOOO'], S = ['.OOOO', 'O....', '.OOO.', '....O', 'OOOO.'];
  const put = (g, ox) => g.forEach((row, y) => [...row].forEach((ch, x) => { if (ch === 'O') rect(p, ox + x * 4, 38 + y * 4, 4, 4, C.yellow2); }));
  put(E, 25); put(S, 51);
  return p;
}

// ---------- móveis (obstáculos; tamanho = área de colisão × 32) ----------

function serverRack(f) { // 2×3
  const p = new Px(64, 96);
  box(p, 0, 0, 64, 96, C.black, O); rect(p, 1, 1, 62, 4, C.gray2);
  for (let y = 8; y < 90; y += 6) {
    rect(p, 4, y, 56, 4, C.gray1); rect(p, 4, y, 56, 1, C.gray2);
    for (let k = 0; k < 4; k++) p.set(48 + k * 3, y + 2, ((y >> 1) + k + f) % 3 === 0 ? C.green2 : ((y + k + f) % 5 === 0 ? C.amber : C.gray2));
    for (let x = 8; x < 40; x += 3) p.set(x, y + 2, O);
  }
  return p;
}

function plant() { // 1×1
  const p = new Px(32, 32);
  box(p, 9, 19, 14, 12, C.rust2); rect(p, 10, 20, 12, 2, C.rust3);
  for (const [x, y, rx, ry] of [[16, 12, 9, 6], [10, 9, 5, 4], [22, 9, 5, 4], [16, 5, 5, 4]]) ellipse(p, x, y, rx, ry, C.green1);
  for (const [x, y] of [[12, 9], [19, 7], [16, 13], [23, 10]]) p.set(x, y, C.green2);
  return p;
}

function crates() { // 2×2
  const p = new Px(64, 64);
  const crate = (x, y, w, h) => { box(p, x, y, w, h, C.rust2); rect(p, x + 1, y + 1, w - 2, 2, C.rust3); line(p, x + 2, y + 3, x + w - 3, y + h - 2, C.rust1); line(p, x + w - 3, y + 3, x + 2, y + h - 2, C.rust1); };
  crate(2, 22, 30, 40); crate(32, 26, 30, 36); crate(16, 2, 32, 26);
  rect(p, 22, 12, 20, 3, C.yellow2); rect(p, 24, 13, 4, 1, O);
  return p;
}

function vending() { // 2×1.25 (64×40)
  const p = new Px(64, 40);
  box(p, 0, 0, 64, 40, C.red2); rect(p, 1, 1, 62, 3, C.red3);
  box(p, 4, 6, 36, 30, '#0d1630');
  for (let y = 9; y < 34; y += 6) for (let x = 7; x < 38; x += 6) rect(p, x, y, 4, 4, [C.yellow2, C.green2, C.cyan2, C.orange2][(x + y) % 4]);
  box(p, 44, 8, 16, 10, C.gray1); rect(p, 46, 10, 12, 2, C.green2); box(p, 46, 24, 12, 10, C.gray2);
  return p;
}

function sofa() { // 3×1.25 (96×40)
  const p = new Px(96, 40);
  box(p, 0, 0, 96, 22, C.purple2); rect(p, 1, 1, 94, 3, C.purple3);
  box(p, 0, 16, 12, 24, C.purple2); box(p, 84, 16, 12, 24, C.purple2);
  box(p, 12, 20, 36, 18, C.purple3); box(p, 48, 20, 36, 18, C.purple3);
  rect(p, 14, 22, 32, 2, '#8a72c8'); rect(p, 50, 22, 32, 2, '#8a72c8');
  return p;
}

function printer3d() { // 2×2
  const p = new Px(64, 64);
  box(p, 4, 4, 56, 56, C.gray2); box(p, 10, 10, 44, 40, '#11141b');
  rect(p, 10, 18, 44, 2, C.gray4); box(p, 28, 14, 8, 8, C.orange2);
  box(p, 24, 38, 16, 10, C.cyan1); rect(p, 26, 40, 12, 2, C.cyan3);
  box(p, 12, 52, 20, 6, C.black); rect(p, 14, 54, 10, 2, C.green2);
  return p;
}

function cooler() { // 1×1.25 (32×40)
  const p = new Px(32, 40);
  box(p, 6, 16, 20, 24, C.offwhite); rect(p, 7, 17, 18, 2, C.white); box(p, 13, 26, 6, 4, C.gray3);
  ellipse(p, 16, 9, 9, 8, '#5fb6e6'); ellipse(p, 13, 6, 2, 3, '#b0e0ff', null);
  return p;
}

export const PROPS = { servidor: [2, 3], planta: [1, 1], caixas: [2, 2], maquina: [2, 1.25], sofa: [3, 1.25], impressora: [2, 2], bebedouro: [1, 1.25] };

export function sceneSprites() {
  const s = (id, category, px, extra = {}) => ({ id, category, w: px.w, h: px.h, pivot: [px.w / 2, px.h / 2], px, ...extra });
  return [
    ...[0, 1].map((v) => s(`tile_server_${v}`, 'Tilesets', server(v))),
    ...[0, 1, 2].map((v) => s(`tile_wood_${v}`, 'Tilesets', wood(v))),
    ...[0, 1, 2].map((v) => s(`tile_concrete_${v}`, 'Tilesets', concrete(v))),
    ...[0, 1].map((v) => s(`tile_epoxy_${v}`, 'Tilesets', epoxy(v))),
    s('tile_wall_window', 'Tilesets', wallWindow()),
    s('tile_wall_poster_0', 'Tilesets', wallPoster(0)),
    s('tile_wall_poster_1', 'Tilesets', wallPoster(1)),
    s('tile_wall_door', 'Tilesets', wallDoor()),
    ...[0, 1].map((f) => s(`tile_wall_server_${f}`, 'Tilesets', wallServer(f))),
    ...['arrow', 'puddle', 'papers', 'vent', 'drain', 'oil', 'tape', 'coffee', 'rug'].map((k) => s(`decal_${k}`, 'Environment', decal(k))),
    s('decal_logo', 'Environment', logo()),
    s('prop_servidor_0', 'Environment', serverRack(0)), s('prop_servidor_1', 'Environment', serverRack(1)),
    s('prop_planta', 'Environment', plant()),
    s('prop_caixas', 'Environment', crates()),
    s('prop_maquina', 'Environment', vending()),
    s('prop_sofa', 'Environment', sofa()),
    s('prop_impressora', 'Environment', printer3d()),
    s('prop_bebedouro', 'Environment', cooler()),
  ];
}
