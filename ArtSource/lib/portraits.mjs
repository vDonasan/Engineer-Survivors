// Retratos 64×64 em pixel art (mesmo estilo dos sprites): 9 emoções × 7 engenharias.
import { C } from './palette.mjs';
import { Px, rect, box, line, disc, ellipse, stamp } from './draw.mjs';
import { SPECS, specRoles } from './characters.mjs';

export const EMOTIONS = ['raiva', 'medo', 'desespero', 'choro', 'exaustao', 'alivio', 'determinacao', 'vitoria', 'derrota'];

const BG = { raiva: C.red1, medo: C.denim1, desespero: C.purple1, choro: C.denim2, exaustao: C.gray2, alivio: C.teal1, determinacao: C.rust1, vitoria: C.yellow1, derrota: C.black };
const O = C.outline, E = C.black, W = C.white;

// olhos: carimbos 7×5 (olho esquerdo; o direito é espelhado)
const EYES = {
  raiva: ['OO.....', '.OOO...', '..OOO..', '.OEEO..', '.OEEO..'],
  medo: ['...OO..', '..O....', '.OWWWO.', '.OWEWO.', '.OWWWO.'],
  desespero: ['....OO.', '..OO...', '.OWWWO.', '.OWEEO.', '..OOO..'],
  choro: ['....OO.', '..OO...', '.......', '.OOOOO.', '.......'],
  exaustao: ['.......', '.OOOO..', '.......', '.OEEO..', '.KKKK..'],
  alivio: ['.......', '.......', '..OOO..', '.O...O.', '.......'],
  determinacao: ['.......', 'OOOOO..', '.......', '.OEEO..', '.OEEO..'],
  vitoria: ['.......', '.......', '.OOOOO.', 'O.....O', '.......'],
  derrota: ['.......', '.O...O.', '..O.O..', '...O...', '..O.O..'],
};
const MOUTH = {
  raiva: ['OOOOOOOO', 'OWOWOWOO', 'OOOOOOOO'],
  medo: ['..OOO...', '.OEEEO..', '..OOO...'],
  desespero: ['.OOOOOO.', 'OEEEEEEO', 'OEERREEO', '.OOOOOO.'],
  choro: ['.O....O.', '..OOOO..', '........'],
  exaustao: ['..OOOO..', '.OEEEEO.', '..OOOO..'],
  alivio: ['.O....O.', '..OOOO..', '........'],
  determinacao: ['.OOOOOO.', '........', '........'],
  vitoria: ['OOOOOOOO', 'OWWWWWWO', '.OWWWWO.', '..OOOO..'],
  derrota: ['.O.O.O..', 'O.O.O.O.', '........'],
};

function extras(p, e) {
  const drop = (x, y, col) => stamp(p, ['.O.', 'OCO', 'OCO', '.O.'], { O, C: col }, x, y);
  if (e === 'choro') for (const x of [21, 41]) { rect(p, x, 33, 2, 9, C.cyan2); }
  if (e === 'medo' || e === 'desespero' || e === 'exaustao') drop(46, 18, C.cyan3);
  if (e === 'desespero') drop(14, 22, C.cyan3);
  if (e === 'raiva') stamp(p, ['R.R', '.R.', 'R.R'], { R: C.red2 }, 47, 12);
  if (e === 'vitoria') for (const [x, y] of [[8, 8], [54, 10], [50, 50]]) stamp(p, ['.Y.', 'YYY', '.Y.'], { Y: C.yellow3 }, x, y);
  if (e === 'derrota') box(p, 16, 34, 7, 4, C.offwhite);
}

function headgear(p, spec, r) {
  switch (spec) {
    case 'eletrica': ellipse(p, 31.5, 16, 17, 10, C.yellow2); rect(p, 13, 20, 38, 4, C.outline); rect(p, 14, 21, 36, 2, C.yellow1); stamp(p, ['.OO', 'OYO', 'OO.', 'OYO', 'OO.'], { O, Y: C.cyan2 }, 30, 9); break;
    case 'civil': ellipse(p, 31.5, 16, 16, 10, C.orange2); rect(p, 15, 21, 34, 3, C.outline); rect(p, 16, 22, 32, 1, C.orange1); for (const x of [12, 49]) box(p, x, 22, 4, 10, r.H); break;
    case 'mecanica': ellipse(p, 31.5, 18, 16, 8, C.orange2); for (const x of [22, 41]) { disc(p, x, 21, 4, C.gray5); disc(p, x, 21, 2, C.cyan2, null); } break;
    case 'mecatronica': rect(p, 18, 12, 28, 3, C.gray3); for (const x of [13, 46]) box(p, x, 24, 5, 10, C.gray3); box(p, 44, 2, 3, 10, C.gray3); p.set(45, 1, C.red2); box(p, 18, 26, 28, 4, C.cyan2); break;
    case 'computacao': ellipse(p, 31.5, 26, 21, 22, C.purple2); break;
    case 'biomedica': disc(p, 31.5, 7, 5, r.H); break;
    case 'quimica': for (const x of [14, 49]) disc(p, x, 22, 5, r.H); for (const x of [24, 39]) { box(p, x - 6, 24, 13, 8, C.gray5); rect(p, x - 4, 26, 9, 4, C.toxic); } break;
  }
}

function portrait(spec, e) {
  const r = specRoles(spec);
  const p = new Px(64, 64);
  box(p, 0, 0, 64, 64, BG[e]);
  ellipse(p, 31.5, 64, 26, 14, r.J); // ombros
  if (spec === 'eletrica') rect(p, 8, 58, 48, 2, C.yellow3);
  if (spec === 'civil') { rect(p, 10, 56, 44, 2, C.white); }
  if (spec === 'mecanica') box(p, 22, 52, 20, 12, C.rust2);
  if (spec === 'biomedica' || spec === 'quimica') rect(p, 29, 50, 6, 14, r.A);
  box(p, 27, 44, 10, 8, r.s);
  if (spec === 'computacao') ellipse(p, 31.5, 28, 22, 24, C.purple2);
  ellipse(p, 31.5, 29, 15, 17, r.S); // rosto
  if (spec !== 'computacao') ellipse(p, 31.5, 16, 15, 7, r.H); // cabelo
  ellipse(p, 31.5, 29, 15, 17, null); // contorno do rosto por cima do cabelo
  for (const x of [15, 47]) ellipse(p, x, 31, 2, 4, r.s);
  headgear(p, spec, r);
  if (spec === 'computacao') for (const x of [24, 39]) { box(p, x - 5, 26, 11, 7, C.gray5); rect(p, x - 3, 28, 7, 3, C.green1); }
  const L = { O, E, W, K: C.bags, R: C.red2 };
  const eyes = EYES[e];
  stamp(p, eyes, L, 19, 28);
  stamp(p, eyes.map((row) => [...row].reverse().join('')), L, 38, 28);
  if (e !== 'alivio' && e !== 'vitoria') for (const x of [20, 39]) rect(p, x, 34, 6, 1, C.bags); // olheiras
  line(p, 31, 34, 32, 37, r.s); // nariz
  stamp(p, MOUTH[e], L, 28, 40);
  if (spec === 'eletrica') box(p, 43, 36, 6, 3, C.offwhite); // curativo
  if (spec === 'biomedica') box(p, 22, 44, 20, 5, C.sky2); // máscara no queixo
  if (spec === 'mecanica') for (let i = 0; i < 8; i++) p.set(24 + i * 2, 45 - (i % 2), r.s); // barba por fazer
  extras(p, e);
  return p;
}

export function portraitSprites() {
  const out = [];
  for (const spec of SPECS) for (const e of EMOTIONS)
    out.push({ id: `portrait_${spec}_${e}`, category: 'Portraits', w: 64, h: 64, pivot: [32, 32], px: portrait(spec, e) });
  return out;
}
