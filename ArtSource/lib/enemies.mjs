// Inimigos em pixel art: microbô 16×16, torreta 32×32, prensa 48×48. Elite: contorno magenta + aura.
import { C } from './palette.mjs';
import { Px, rect, box, line, disc, ellipse, stamp, outlineAround, recolor } from './draw.mjs';

const L = { O: C.outline, m: C.gray3, M: C.gray4, k: C.gray5, R: C.red2, r: C.red1, Y: C.red3, d: C.gray1 };

function microbo(frame) {
  const p = new Px(16, 16);
  stamp(p, [
    '................', '.......O........', '......ORO.......', '.......O........', '.....OOOOOO.....', '....OkMMMMmO....',
    '...OkMMMMMMmO...', '...OmOOOOOOmO...', '...OmORYYROmO...', '...OmOOOOOOmO...', '...OmmmmmmmmO...', '....OmmmmmmO....',
    '....OOOOOOOO....',
  ], L);
  if (frame === 0) stamp(p, ['...O.O....O.O...', '..O..O....O..O..'], L, 0, 13);
  else stamp(p, ['....OO....OO....', '...O..O..O..O...'], L, 0, 13);
  return p;
}

function torreta(frame) {
  const p = new Px(32, 32);
  for (const [x1, y1] of [[7, 29], [24, 29], [15, 30]]) { line(p, 15, 19, x1, y1, C.outline); line(p, 16, 19, x1 + 1, y1, C.gray4); }
  rect(p, 5, 29, 4, 2, C.outline); rect(p, 23, 29, 4, 2, C.outline); rect(p, 14, 30, 4, 2, C.outline);
  box(p, 8, 8, 16, 12, C.petrol2);
  rect(p, 9, 9, 14, 2, C.petrol3);
  for (let i = 0; i < 14; i++) p.set(9 + i, 16, i % 4 < 2 ? C.yellow2 : C.outline);
  box(p, 23, 11, 6, 3, C.gray4);
  disc(p, 15.5, 13, 3.4, frame === 0 ? C.red1 : C.red2);
  p.set(15, 12, frame === 0 ? C.red2 : C.red3);
  if (frame === 1) { p.set(15, 8, C.red3); p.set(15, 7, C.red2); p.set(29, 12, C.red3); }
  return p;
}

function prensa(frame) {
  const p = new Px(48, 48);
  const drop = frame === 0 ? 0 : 5;
  box(p, 10, 41, 11, 5, C.gray1); box(p, 27, 41, 11, 5, C.gray1);
  box(p, 7, 16, 34, 27, C.gray2);
  rect(p, 8, 17, 32, 3, C.gray3);
  for (let x = 8; x < 40; x++) for (let y = 35; y < 41; y++) p.set(x, y, ((x + y) >> 2) % 2 ? C.yellow2 : C.outline);
  box(p, 14, 23, 20, 5, C.red1); rect(p, 16, 24, 16, 3, C.red2); rect(p, 17, 25, 4, 1, C.red3);
  box(p, 13, 6 + drop, 5, 11 - drop, C.gray5); box(p, 30, 6 + drop, 5, 11 - drop, C.gray5);
  box(p, 9, 2 + drop, 30, 6, C.gray4); rect(p, 10, 3 + drop, 28, 1, C.gray5);
  ellipse(p, 32, 31, 3, 1, C.rust2, null);
  p.set(10, 19, C.gray4); p.set(37, 19, C.gray4);
  return p;
}

const ENEMIES = { microbo_enxame: microbo, prensa_ambulante: prensa, torreta_inspecao: torreta };

function elite(p) {
  const glow = outlineAround(recolor(p, C.outline, C.magenta), C.magenta2);
  return glow;
}

export function enemySprites() {
  const out = [];
  for (const [id, fn] of Object.entries(ENEMIES))
    for (const f of [0, 1]) {
      const p = fn(f);
      const pivot = [p.w / 2, Math.max(1, p.h - 1 - p.bottomRow() + 1)];
      out.push({ id: `enemy_${id}_${f}`, category: 'Enemies', w: p.w, h: p.h, pivot, px: p });
      out.push({ id: `enemy_${id}_elite_${f}`, category: 'Enemies', w: p.w, h: p.h, pivot, px: elite(p) });
    }
  return out;
}
