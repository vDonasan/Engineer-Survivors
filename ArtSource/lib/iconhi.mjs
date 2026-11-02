// Ícones de itens desenhados direto em 64×64 (mais detalhe que a grade de 32): o sombreamento,
// o contorno externo e a moldura vêm de icons.mjs.
import { C } from './palette.mjs';
import { rect, box, line, disc, ellipse, circleLine, stamp } from './draw.mjs';

const O = C.outline;

/** Polígono preenchido (varredura por linhas). */
function poly(p, pts, col) {
  const ys = pts.map((q) => q[1]);
  for (let y = Math.floor(Math.min(...ys)); y <= Math.ceil(Math.max(...ys)); y++) {
    const xs = [];
    for (let i = 0; i < pts.length; i++) {
      const [x0, y0] = pts[i], [x1, y1] = pts[(i + 1) % pts.length];
      if ((y0 <= y + 0.5 && y1 > y + 0.5) || (y1 <= y + 0.5 && y0 > y + 0.5)) xs.push(x0 + ((y + 0.5 - y0) / (y1 - y0)) * (x1 - x0));
    }
    xs.sort((a, b) => a - b);
    for (let k = 0; k + 1 < xs.length; k += 2) for (let x = Math.round(xs[k]); x < Math.round(xs[k + 1]); x++) p.set(x, y, col);
  }
  return p;
}

/** Linha grossa (discos ao longo do segmento). */
function thick(p, x0, y0, x1, y1, r, col) {
  const n = Math.ceil(Math.hypot(x1 - x0, y1 - y0));
  for (let i = 0; i <= n; i++) disc(p, x0 + ((x1 - x0) * i) / n, y0 + ((y1 - y0) * i) / n, r, col, null);
  return p;
}

const sparkle = (p, x, y, col) => { p.set(x, y, C.white); for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) p.set(x + dx, y + dy, col); p.set(x + 2, y, col); p.set(x - 2, y, col); p.set(x, y + 2, col); p.set(x, y - 2, col); };
const ring = (p, r, col) => { circleLine(p, 31.5, 31.5, r, col); circleLine(p, 31.5, 31.5, r - 1, col); };
const flame = (p, x, y, s = 1) => { ellipse(p, x, y, 3 * s, 5 * s, C.orange2, C.red1); ellipse(p, x, y + 1.5 * s, 1.6 * s, 3 * s, C.yellow3, null); };

// ——— desenhos base ———

function tequila(p) {
  box(p, 27, 5, 10, 7, C.rust2); rect(p, 28, 6, 8, 1, C.rust3);
  box(p, 28, 11, 8, 9, '#d9b45a');
  poly(p, [[28, 19], [36, 19], [45, 26], [45, 57], [19, 57], [19, 26]], O);
  poly(p, [[29, 20], [35, 20], [44, 27], [44, 56], [20, 56], [20, 27]], '#e8c46a');
  rect(p, 20, 27, 24, 3, '#f4dc94');
  box(p, 21, 33, 22, 16, C.red2); rect(p, 22, 34, 20, 2, C.red3);
  rect(p, 22, 44, 20, 2, C.yellow2);
  for (const [x, h] of [[27, 6], [30, 8], [32, 9], [34, 8], [37, 6]]) line(p, 32, 44, x, 44 - h, C.green1);
  rect(p, 23, 22, 2, 30, '#fff3c4');
}

function iogurte(p) {
  for (let y = 18; y <= 56; y++) { const hw = 16 - ((y - 18) / 38) * 5; rect(p, Math.round(32 - hw), y, Math.round(hw * 2), 1, y === 56 ? O : C.white); p.set(Math.round(32 - hw) - 1, y, O); p.set(Math.round(32 + hw), y, O); }
  box(p, 13, 12, 38, 8, C.pink2); rect(p, 14, 13, 36, 2, C.pink3);
  poly(p, [[46, 12], [54, 6], [56, 9], [50, 14]], C.pink3);
  rect(p, 18, 28, 28, 17, C.sky2); rect(p, 18, 28, 28, 1, C.sky1); rect(p, 18, 44, 28, 1, C.sky1);
  const big = { O, '.': null };
  stamp(p, ['OOOOO..OOOOO', 'O...O..O...O', 'O...O..O...O', 'OOOOO..O...O', 'O...O..O...O', 'O...O..O...O', 'OOOOO..OOOOO'], big, 26, 32);
  ellipse(p, 23, 51, 2, 1.5, C.pink2, null); ellipse(p, 41, 51, 2, 1.5, C.pink2, null);
  rect(p, 20, 21, 2, 22, '#ffffff');
}

function cenoura(p) {
  const rot = '#7a5a2a', org = '#d4762e', dark = '#a3501f';
  for (let i = 0; i <= 40; i++) {
    const t = i / 40, x = 14 + t * 28, y = 52 - t * 30, r = 1.5 + t * 7.5;
    disc(p, x, y, r + 1, O, null);
  }
  for (let i = 0; i <= 40; i++) {
    const t = i / 40, x = 14 + t * 28, y = 52 - t * 30, r = 1.5 + t * 7.5;
    disc(p, x, y, r, org, null);
  }
  for (const t of [0.3, 0.5, 0.7, 0.85]) { const x = 14 + t * 28, y = 52 - t * 30, r = 1.5 + t * 7.5; line(p, Math.round(x - r * 0.6), Math.round(y - r * 0.8), Math.round(x - r * 0.1), Math.round(y - r * 0.1), dark); }
  for (const [x, y, r] of [[27, 38, 2.5], [36, 30, 3], [40, 22, 2], [22, 45, 1.5]]) disc(p, x, y, r, rot, null);
  for (const [x1, y1] of [[46, 6], [52, 10], [56, 16], [42, 8]]) thick(p, 43, 19, x1, y1, 1.2, '#5a7a2a');
  for (const [x1, y1] of [[46, 6], [56, 16]]) thick(p, 45, 15, x1, y1, 0.6, '#7aa31c');
  for (const [x, y] of [[10, 22], [16, 14], [54, 40], [48, 50]]) { disc(p, x, y, 3, '#9ad03a', '#5a7a2a'); p.set(x - 1, y - 1, '#d2f58a'); }
  for (const [x, y] of [[20, 26], [52, 30]]) { p.set(x, y, O); p.set(x - 1, y - 1, C.offwhite); p.set(x + 1, y - 1, C.offwhite); }
}

function motor(p) {
  box(p, 10, 24, 42, 26, C.gray3);
  for (let i = 0; i < 4; i++) { box(p, 13 + i * 9, 13, 8, 12, C.gray4); rect(p, 14 + i * 9, 14, 6, 2, C.gray5); disc(p, 17 + i * 9, 20, 1.5, C.gray2, O); }
  box(p, 16, 27, 30, 9, C.red2); rect(p, 17, 28, 28, 2, C.red3);
  for (let i = 0; i < 5; i++) rect(p, 20 + i * 5, 32, 3, 2, C.red1);
  disc(p, 11, 42, 7, C.gray2); disc(p, 11, 42, 3, C.gray4); p.set(11, 42, O);
  line(p, 11, 35, 22, 38, O); line(p, 11, 49, 22, 47, O);
  for (let i = 0; i < 4; i++) disc(p, 24 + i * 7, 43, 2, C.gray4, O);
  box(p, 50, 34, 8, 6, C.gray4); box(p, 56, 30, 4, 14, C.gray2);
  box(p, 14, 50, 6, 6, C.gray2); box(p, 42, 50, 6, 6, C.gray2);
  for (const [x, y] of [[13, 27], [48, 27], [13, 46], [48, 46]]) p.set(x, y, C.gray5);
}

function dog(p, turbo) {
  const body = turbo ? C.red2 : C.rust3, light = turbo ? C.red3 : C.orange3;
  for (const [x, y] of [[16, 44], [24, 46], [38, 44], [46, 46]]) { box(p, x, y, 5, 56 - y, C.gray2); box(p, x - 1, 53, 7, 4, C.gray1); }
  box(p, 10, 28, 40, 18, body); rect(p, 11, 29, 38, 3, light);
  box(p, 20, 32, 16, 9, C.gray4); for (let i = 0; i < 3; i++) rect(p, 22 + i * 5, 34, 3, 5, C.gray2);
  box(p, 40, 14, 18, 18, body); rect(p, 41, 15, 16, 3, light);
  box(p, 52, 22, 9, 8, body); rect(p, 59, 23, 2, 2, O);
  box(p, 42, 8, 6, 8, turbo ? C.red1 : C.rust1);
  disc(p, 50, 20, 2.5, C.yellow3); p.set(50, 20, O);
  rect(p, 53, 28, 6, 1, C.white); p.set(55, 29, C.white);
  box(p, 2, 24, 10, 6, C.gray3); rect(p, 3, 25, 8, 1, C.gray5);
  if (turbo) { flame(p, 4, 18, 1); flame(p, 9, 13, 0.8); for (const y of [36, 42]) line(p, 1, y, 7, y, C.yellow3); }
  else for (const [x, y, r] of [[5, 18, 3], [9, 12, 2.5], [4, 8, 2]]) disc(p, x, y, r, C.gray4, C.gray2);
}

function mamao(p) {
  ellipse(p, 32, 36, 18, 21, C.orange2, C.orange1);
  ellipse(p, 32, 37, 15, 18, '#ff9a3a', null);
  ellipse(p, 25, 28, 4, 7, C.orange3, null); rect(p, 23, 24, 2, 3, '#ffe0b8');
  box(p, 30, 8, 5, 9, C.green1);
  ellipse(p, 42, 11, 8, 4, C.green2, C.green1); line(p, 36, 11, 48, 11, C.green1);
  disc(p, 25, 36, 2.5, O, null); disc(p, 39, 36, 2.5, O, null); p.set(24, 35, C.white); p.set(38, 35, C.white);
  ellipse(p, 20, 42, 3, 2, C.pink2, null); ellipse(p, 44, 42, 3, 2, C.pink2, null);
  line(p, 29, 43, 31, 45, O); line(p, 32, 45, 35, 43, O);
}

function protoboard(p, exp) {
  box(p, 4, 14, 56, 38, C.offwhite); rect(p, 5, 15, 54, 2, '#e4e7ea');
  rect(p, 7, 18, 50, 1, C.red2); rect(p, 7, 48, 50, 1, C.sky1);
  rect(p, 5, 32, 54, 2, C.gray4);
  for (let y = 21; y < 46; y += 4) { if (y > 30 && y < 35) continue; for (let x = 8; x < 57; x += 4) p.set(x, y, C.gray3); }
  for (let x = 8; x < 57; x += 4) { p.set(x, 16, C.gray3); p.set(x, 50, C.gray3); }
  box(p, 26, 27, 18, 12, C.black); rect(p, 27, 28, 16, 1, C.gray2); disc(p, 29, 30, 0.8, C.gray3, null);
  for (let i = 0; i < 5; i++) { rect(p, 28 + i * 3, 25, 1, 2, C.gray5); rect(p, 28 + i * 3, 39, 1, 2, C.gray5); }
  rect(p, 8, 25, 12, 3, '#d9b45a'); line(p, 6, 26, 8, 26, C.gray5); line(p, 20, 26, 22, 26, C.gray5);
  for (const [x, c] of [[10, C.rust1], [13, C.red2], [16, C.gold]]) rect(p, x, 25, 1, 3, c);
  disc(p, 51, 24, 3.5, exp ? C.cyan2 : C.red2); p.set(50, 23, C.white); rect(p, 49, 28, 1, 4, C.gray5); rect(p, 53, 28, 1, 4, C.gray5);
  const wire = (pts, c) => { for (let i = 0; i + 1 < pts.length; i++) thick(p, ...pts[i], ...pts[i + 1], 0.6, c); };
  wire([[8, 44], [14, 40], [22, 42], [26, 37]], C.green1);
  wire([[56, 44], [50, 42], [44, 36]], C.sky1);
  wire([[22, 20], [30, 20], [34, 26]], C.red2);
  if (exp) { for (const [x0, y0, x1, y1] of [[6, 10, 20, 22], [58, 10, 46, 26], [32, 4, 34, 24]]) { line(p, x0, y0, x1, y1, C.cyan2); line(p, x0 + 1, y0, x1 + 1, y1, C.cyan3); } for (const [x, y] of [[6, 10], [58, 10], [32, 4]]) sparkle(p, x, y, C.cyan2); }
}

function chapeu(p) {
  for (let y = 12; y <= 40; y++) {
    const hw = 3 + ((y - 12) / 28) * 25;
    rect(p, Math.round(32 - hw), y, Math.round(hw * 2), 1, Math.floor((y - 12) / 4) % 2 ? '#c9a45c' : '#b08a42');
  }
  for (const k of [-0.75, -0.4, 0, 0.4, 0.75]) line(p, 32, 12, Math.round(32 + k * 28), 40, '#8a6a32');
  rect(p, 4, 40, 56, 3, '#7a5a2a'); rect(p, 4, 40, 56, 1, '#c9a45c');
  disc(p, 32, 12, 2.5, '#d9b45a');
  thick(p, 18, 43, 26, 57, 1, C.red2); thick(p, 46, 43, 38, 57, 1, C.red2); disc(p, 32, 58, 2.5, C.red1, O);
}

function camiseta(p) {
  const S = C.sky1, D = C.denim2;
  poly(p, [[20, 10], [27, 10], [32, 15], [37, 10], [44, 10], [58, 20], [52, 30], [46, 26], [46, 56], [18, 56], [18, 26], [12, 30], [6, 20]], O);
  poly(p, [[21, 12], [27, 12], [32, 17], [37, 12], [43, 12], [55, 20], [51, 27], [44, 23], [44, 54], [20, 54], [20, 23], [13, 27], [9, 20]], S);
  rect(p, 20, 50, 24, 4, C.sky2);
  line(p, 21, 12, 13, 27, D); line(p, 43, 12, 51, 27, D);
  box(p, 28, 16, 8, 7, C.white); rect(p, 29, 18, 6, 1, C.gray4); rect(p, 29, 20, 4, 1, C.gray4);
  for (const x of [20, 21, 43, 44]) for (let y = 30; y < 50; y += 3) p.set(x, y, C.white);
  stamp(p, ['..O..', '.OOO.', 'O.O.O', '..O..', '..O..'], { O: C.white }, 30, 32);
  stamp(p, ['..O..', '..O..', 'O.O.O', '.OOO.', '..O..'], { O: C.white }, 30, 40);
}

function cafe(p) {
  ellipse(p, 32, 53, 25, 6, C.offwhite); ellipse(p, 32, 52, 21, 4, C.white, null);
  for (let y = 26; y <= 50; y++) { const hw = 15 - ((y - 26) / 24) * 4; rect(p, Math.round(32 - hw) - 1, y, Math.round(hw * 2) + 2, 1, O); rect(p, Math.round(32 - hw), y, Math.round(hw * 2), 1, y > 47 ? C.offwhite : C.white); }
  circleLine(p, 49, 37, 6, O); circleLine(p, 49, 37, 5, C.white); circleLine(p, 49, 37, 4, O);
  ellipse(p, 32, 27, 14, 3.5, C.rust1, O); ellipse(p, 32, 27, 10, 2, C.rust3, null); disc(p, 30, 26.5, 1, '#e6b07a', null);
  rect(p, 20, 30, 2, 14, '#ffffff');
  for (const x of [24, 32, 40]) for (let y = 6; y < 22; y++) p.set(Math.round(x + Math.sin(y / 2.5 + x) * 2), y, y % 2 ? C.gray5 : C.offwhite);
}

function halter(p) {
  box(p, 12, 29, 40, 6, C.gray5); rect(p, 13, 30, 38, 1, C.white);
  for (const s of [-1, 1]) {
    const x = s < 0 ? 4 : 52;
    box(p, s < 0 ? 12 : 44, 16, 8, 32, C.gray2); rect(p, s < 0 ? 13 : 45, 17, 6, 2, C.gray3);
    box(p, x, 21, 8, 22, C.gray1); rect(p, x + 1, 22, 6, 1, C.gray3);
  }
  for (let x = 24; x < 41; x += 3) rect(p, x, 31, 1, 3, C.gray3);
}

function cartao(p) {
  box(p, 4, 14, 56, 36, C.gold); rect(p, 5, 15, 54, 3, C.yellow3);
  rect(p, 5, 20, 54, 2, C.yellow2);
  box(p, 10, 25, 11, 9, C.yellow2); rect(p, 11, 29, 9, 1, C.yellow1); rect(p, 15, 26, 1, 7, C.yellow1);
  for (let i = 0; i < 4; i++) rect(p, 10 + i * 11, 39, 8, 2, C.yellow1);
  rect(p, 10, 44, 20, 1, C.yellow1);
  disc(p, 44, 30, 5, C.red2); disc(p, 51, 30, 5, C.amber); ellipse(p, 47.5, 30, 1.5, 4, C.orange2, null);
  for (let i = 0; i < 3; i++) circleLine(p, 26 + i * 3, 29, 2 + i * 2, C.yellow1);
}

function arduino(p) {
  box(p, 4, 12, 56, 40, C.cyan1); rect(p, 5, 13, 54, 2, '#4fb4d0');
  box(p, 2, 18, 12, 10, C.gray4); rect(p, 3, 20, 10, 6, C.gray5);
  box(p, 2, 36, 10, 10, C.black);
  for (let i = 0; i < 12; i++) { box(p, 18 + i * 3, 14, 3, 4, C.black); box(p, 18 + i * 3, 46, 3, 4, C.black); }
  box(p, 24, 26, 22, 12, C.black); rect(p, 25, 27, 20, 1, C.gray2);
  for (let i = 0; i < 7; i++) { p.set(26 + i * 3, 25, C.gray5); p.set(26 + i * 3, 38, C.gray5); }
  box(p, 50, 22, 6, 6, C.gray5); disc(p, 53, 25, 1.2, C.gray3, null);
  disc(p, 19, 33, 1.5, C.green2, null); disc(p, 19, 39, 1.5, C.amber, null); disc(p, 52, 40, 1.5, C.red2, null);
  rect(p, 30, 41, 12, 1, '#7fd3e8');
}

function megafone(p) {
  poly(p, [[14, 24], [44, 8], [48, 8], [48, 50], [44, 50], [14, 36]], O);
  poly(p, [[15, 25], [44, 10], [46, 10], [46, 48], [44, 48], [15, 35]], C.orange2);
  poly(p, [[15, 25], [44, 10], [46, 10], [46, 18], [15, 29]], C.orange3);
  box(p, 6, 23, 10, 14, C.gray4); rect(p, 7, 24, 8, 2, C.gray5);
  box(p, 12, 36, 7, 14, C.gray2); rect(p, 13, 44, 5, 1, C.gray4);
  ellipse(p, 47, 29, 3, 19, C.gray2, O);
  for (const [x0, y0, x1, y1] of [[52, 16, 58, 10], [53, 29, 61, 29], [52, 42, 58, 48]]) thick(p, x0, y0, x1, y1, 0.7, C.white);
}

function whey(p) {
  box(p, 12, 16, 40, 42, C.black); rect(p, 13, 17, 38, 2, C.gray2);
  box(p, 10, 8, 44, 10, C.gray3); rect(p, 11, 9, 42, 2, C.gray5); for (let x = 13; x < 52; x += 4) rect(p, x, 12, 2, 5, C.gray2);
  box(p, 16, 26, 32, 20, C.red2); rect(p, 17, 27, 30, 2, C.red3);
  stamp(p, ['O...O...O', 'O...O...O', 'O..O.O..O', 'O.O...O.O', '.O.....O.'], { O: C.white }, 23, 30);
  rect(p, 18, 40, 28, 2, C.white); rect(p, 15, 22, 2, 30, C.gray2);
}

function marmita(p) {
  box(p, 4, 18, 56, 34, C.gray4); rect(p, 5, 19, 54, 2, C.gray5);
  box(p, 8, 22, 48, 13, C.white); for (let i = 0; i < 40; i++) p.set(10 + ((i * 7) % 45), 24 + ((i * 5) % 9), C.offwhite);
  box(p, 8, 36, 22, 13, C.rust1); for (let i = 0; i < 18; i++) disc(p, 11 + ((i * 5) % 17), 39 + ((i * 3) % 8), 1, C.rust2, null);
  box(p, 31, 36, 25, 13, C.lime1); for (const [x, y] of [[35, 40], [42, 44], [49, 39], [38, 46]]) ellipse(p, x, y, 3, 1.5, C.lime2, null);
  disc(p, 48, 26, 4, C.red2); p.set(47, 24, C.red3);
  box(p, 26, 10, 12, 9, C.gray3);
}

function hdmi(p) {
  box(p, 4, 20, 22, 20, C.gray3); rect(p, 5, 21, 20, 2, C.gray5);
  poly(p, [[8, 25], [22, 25], [22, 32], [19, 35], [11, 35], [8, 32]], C.black);
  for (let x = 10; x < 21; x += 2) rect(p, x, 27, 1, 2, C.gold);
  box(p, 25, 26, 7, 8, C.gray2);
  const pts = []; for (let x = 31; x <= 62; x++) pts.push([x, 30 + Math.sin((x - 31) / 5) * 7]);
  for (let i = 0; i + 1 < pts.length; i++) thick(p, ...pts[i], ...pts[i + 1], 2.2, O);
  for (let i = 0; i + 1 < pts.length; i++) thick(p, ...pts[i], ...pts[i + 1], 1.3, C.black);
}

function overclock(p) {
  box(p, 12, 12, 40, 40, C.gray2);
  for (let i = 0; i < 9; i++) { rect(p, 15 + i * 4, 7, 2, 5, C.gold); rect(p, 15 + i * 4, 52, 2, 5, C.gold); rect(p, 7, 15 + i * 4, 5, 2, C.gold); rect(p, 52, 15 + i * 4, 5, 2, C.gold); }
  box(p, 17, 17, 30, 30, C.green1); rect(p, 18, 18, 28, 2, '#2dbb60');
  for (let i = 0; i < 5; i++) line(p, 18, 22 + i * 5, 24, 22 + i * 5, '#0f6a2f');
  poly(p, [[35, 19], [25, 34], [32, 34], [28, 46], [40, 29], [33, 29], [37, 19]], C.yellow3);
  line(p, 35, 19, 25, 34, C.yellow1);
}

// ——— mapa: base e evoluções ———

export const HI = {
  tequila,
  fiesta_caotica: (p) => { tequila(p); for (const [x, y, c] of [[8, 10, C.magenta], [54, 8, C.cyan2], [56, 40, C.yellow3], [6, 46, C.green2], [10, 28, C.orange2], [54, 54, C.magenta]]) { rect(p, x, y, 3, 3, c); p.set(x + 3, y + 3, c); } sparkle(p, 50, 20, C.yellow3); },
  iogurte_80: iogurte,
  dieta_milagrosa: (p) => { iogurte(p); for (const [x, y] of [[7, 14], [56, 26], [8, 44], [55, 52]]) sparkle(p, x, y, C.yellow3); },
  cenoura_podre: cenoura,
  hortalica_desgraca: (p) => { for (const [x, y, r] of [[18, 20, 10], [44, 40, 12], [22, 46, 9], [46, 16, 8]]) disc(p, x, y, r, '#5a7a2a', null); for (const [x, y, r] of [[18, 20, 7], [44, 40, 9], [22, 46, 6]]) disc(p, x, y, r, '#7aa31c', null); cenoura(p); },
  motor_astra: motor,
  astra_alta_rotacao: (p) => { ring(p, 29, C.yellow2); motor(p); flame(p, 58, 24, 1.1); rect(p, 22, 32, 18, 2, C.cyan2); },
  cachorro_quente: (p) => dog(p, false),
  cao_turbo: (p) => dog(p, true),
  camiseta_contrario: camiseta,
  moda_incompreensivel: (p) => { ring(p, 30, C.cyan3); camiseta(p); for (const [x, y] of [[8, 46], [56, 46]]) sparkle(p, x, y, C.cyan2); },
  mamao,
  mamao_supremo: (p) => { ring(p, 30, C.green2); mamao(p); stamp(p, ['Y.Y.Y', 'YYYYY', 'YRYRY', 'YYYYY'], { Y: C.gold, R: C.red2 }, 30, 5); },
  protoboard: (p) => protoboard(p, false),
  protoboard_experimental: (p) => protoboard(p, true),
  chapeu_japones: chapeu,
  mestre_projeteis: (p) => { chapeu(p); for (const y of [50, 56]) { thick(p, 4, y, 50, y, 0.6, C.cyan2); poly(p, [[50, y - 4], [58, y], [50, y + 4]], C.white); } },
  cafe_expresso: cafe,
  halter,
  cartao_corporativo: cartao,
  kit_arduino: arduino,
  megafone,
  pote_whey: whey,
  marmita,
  cabo_hdmi: hdmi,
  overclock,
};
