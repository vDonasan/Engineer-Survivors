// Projéteis, companheiros e efeitos das armas dos personagens: ally_<sprite>_<quadro> (o nome segue
// o enum AllySprite da simulação, em minúsculas) e fx_* de estados e avisos.
import { C } from './palette.mjs';
import { Px } from './pixel.mjs';
import { rect, box, line, disc, ellipse, circleLine, stamp } from './draw.mjs';

const O = C.outline, W = C.white;
const S = (rows, legend) => stamp(new Px(rows[0].length, rows.length), rows, { O, W, ...legend });

function bubble(r, f) {
  const n = r * 2 + 4, p = new Px(n, n), c = n / 2 - 0.5;
  circleLine(p, c, c, r, C.cyan3);
  circleLine(p, c, c, r - 1, f ? C.pink3 : C.cyan2);
  p.set(Math.round(c - r / 2), Math.round(c - r / 2), W); p.set(Math.round(c - r / 2) + 1, Math.round(c - r / 2), W);
  return p;
}

const digits = ['.OO.', 'O..O', 'O..O', 'O..O', '.OO.'];
function digitSprite(f) {
  const glyphs = [['OOO', '..O', 'OOO', 'O..', 'OOO'], ['O.O', 'O.O', 'OOO', '..O', '..O'], ['OOO', 'O..', 'OOO', '..O', 'OOO'], ['.O.', 'OO.', '.O.', '.O.', 'OOO']];
  const p = new Px(7, 9);
  rect(p, 0, 0, 7, 9, O);
  rect(p, 1, 1, 5, 7, C.purple1);
  stamp(p, glyphs[f], { O: C.pink3 }, 2, 2);
  void digits;
  return p;
}

function bot(body, eye, f, size = 16) {
  const p = new Px(size, size);
  const bob = f % 2;
  box(p, 3, 4 + bob, 10, 8, body);
  rect(p, 5, 6 + bob, 6, 3, C.gray1);
  p.set(6, 7 + bob, eye); p.set(9, 7 + bob, eye);
  line(p, 8, 1 + bob, 8, 4 + bob, O); p.set(8, 0 + bob, C.red2);
  rect(p, 4, 12 + bob, 3, 2, C.gray3); rect(p, 9, 12 + bob, 3, 2, C.gray3);
  rect(p, 1, 7 + bob, 2, 3, C.gray4); rect(p, 13, 7 + bob, 2, 3, C.gray4);
  return p;
}

/** Cachorro mecânico superaquecido: escapamento soltando fumaça; o turbo tem chamas. */
function dog(f, turbo) {
  const p = new Px(18, 13);
  const leg = f ? [[3, 10], [6, 11], [11, 10], [14, 11]] : [[3, 11], [6, 10], [11, 11], [14, 10]];
  for (const [x, y] of leg) rect(p, x, y, 2, 13 - y, O);
  box(p, 2, 5, 13, 6, turbo ? C.red2 : C.rust3);
  rect(p, 3, 6, 11, 1, turbo ? C.red3 : C.orange3);
  box(p, 6, 6, 5, 3, C.gray4); p.set(8, 7, C.gray2);
  box(p, 12, 2, 6, 6, turbo ? C.red2 : C.rust3); p.set(16, 4, C.black); p.set(17, 6, O);
  rect(p, 13, 1, 2, 2, O); p.set(13, 1, turbo ? C.red1 : C.rust1);
  box(p, 0, 4, 3, 2, C.gray3);
  if (turbo) { p.set(0, 3, C.yellow3); p.set(1, 2 + f, C.orange2); p.set(0, 1 + f, C.red2); }
  else { p.set(0, 3 - f, C.gray4); p.set(1, 2 - f, C.gray5); }
  return p;
}

/** Mamão simpático com folhinha e carinha. */
function papaya(f) {
  const p = new Px(12, 15);
  ellipse(p, 5.5, 8.5 + f * 0.5, 5, 5.5, C.orange2, C.orange1);
  ellipse(p, 4, 7, 1.5, 2, C.orange3, null);
  rect(p, 5, 1, 2, 3, C.green1); p.set(7, 1, C.green2); p.set(8, 0, C.green2);
  p.set(4, 8, C.outline); p.set(7, 8, C.outline); p.set(5, 10, C.outline); p.set(6, 10, C.outline);
  p.set(3, 9, C.pink2); p.set(8, 9, C.pink2);
  return p;
}

function master(f) {
  const p = new Px(48, 48), b = f % 2;
  // pernas
  box(p, 13, 34, 8, 12, C.gray3); box(p, 27, 34, 8, 12, C.gray3);
  box(p, 11, 43, 11, 5, C.gray2); box(p, 26, 43, 11, 5, C.gray2);
  // tronco com as cores dos três Cabazords
  box(p, 9, 14 + b, 30, 22, C.gray4);
  box(p, 12, 17 + b, 8, 8, C.sky1); box(p, 20, 17 + b, 8, 8, C.red2); box(p, 28, 17 + b, 8, 8, C.yellow2);
  box(p, 17, 27 + b, 14, 6, C.gray2); rect(p, 19, 29 + b, 10, 2, C.cyan2);
  // braços
  box(p, 1, 15 + b, 9, 18, C.gray3); box(p, 38, 15 + b, 9, 18, C.gray3);
  box(p, 0, 31 + b, 11, 7, C.orange2); box(p, 37, 31 + b, 11, 7, C.orange2);
  // cabeça
  box(p, 15, 2 + b, 18, 13, C.gray4);
  box(p, 17, 6 + b, 14, 5, C.gray1); rect(p, 19, 7 + b, 4, 3, C.cyan2); rect(p, 25, 7 + b, 4, 3, C.cyan2);
  line(p, 24, 0, 24, 2 + b, O); p.set(24, 0, C.red2);
  return p;
}

function carFrame(dir) {
  // carro visto de cima, apontado para dir × 45° (0 = direita, 2 = baixo na tela)
  const base = new Px(24, 24);
  box(base, 3, 7, 18, 10, C.red2);
  rect(base, 4, 8, 16, 1, C.red3);
  box(base, 9, 8, 7, 8, C.sky2); rect(base, 10, 9, 5, 6, C.sky1); rect(base, 12, 8, 1, 8, C.red2);
  for (const [x, y] of [[4, 5], [15, 5], [4, 16], [15, 16]]) rect(base, x, y, 4, 3, O);
  rect(base, 20, 8, 1, 2, C.yellow3); rect(base, 20, 14, 1, 2, C.yellow3);
  rect(base, 3, 9, 1, 2, C.red1); rect(base, 3, 13, 1, 2, C.red1);
  const p = new Px(24, 24);
  const a = -dir * Math.PI / 4; // y da tela cresce para baixo no PNG
  for (let y = 0; y < 24; y++) for (let x = 0; x < 24; x++) {
    const dx = x - 11.5, dy = y - 11.5;
    const sx = Math.round(11.5 + dx * Math.cos(a) - dy * Math.sin(a)), sy = Math.round(11.5 + dx * Math.sin(a) + dy * Math.cos(a));
    const v = base.get(sx, sy);
    if (v) p.set(x, y, v);
  }
  return p;
}

function tire(f, size) {
  const p = new Px(size, size), c = size / 2 - 0.5, r = size / 2 - 1;
  disc(p, c, c, r, C.gray1);
  disc(p, c, c, r - 3, C.gray3);
  disc(p, c, c, r - 5, null, O);
  for (let k = 0; k < 6; k++) {
    const a = k * Math.PI / 3 + f * Math.PI / 6;
    p.set(Math.round(c + Math.cos(a) * (r - 1)), Math.round(c + Math.sin(a) * (r - 1)), C.gray4);
  }
  return p;
}

function jabuti(f) {
  return S([
    '..OOOO..O...',
    '.OgGgGO.OO..',
    'OGgGgGgOSSO.',
    'OgGgGgGOSEO.',
    'OGgGgGgOSSO.',
    '.OgGgGO.OO..',
    f ? 'O.O..O.O....' : '.O.O.O.O....',
    f ? 'S.S..S.S....' : '.S.S.S.S....',
  ], { G: C.green1, g: C.lime1, S: C.lime2, E: C.black });
}

const SPRITES = {
  laser: [S(['.OOOOOOOOOOOOOO.', 'OoAAAAAAAAAAAAoO', 'OoAAAAAAAAAAAAoO', '.OOOOOOOOOOOOOO.'], { o: C.orange2, A: C.yellow3 })],
  bubble: [bubble(4, 0), bubble(4, 1)],
  bubblebig: [bubble(8, 0), bubble(8, 1)],
  briefcase: [S(['....OOOO....', '....O..O....', 'OOOOOOOOOOOO', 'OBBBBBBBBBBO', 'OBBBBYYBBBBO', 'ObbbbYYbbbbO', 'OBBBBBBBBBBO', 'OBBBBBBBBBBO', 'OOOOOOOOOOOO'], { B: C.rust2, b: C.rust1, Y: C.gold })],
  coin: [S(['.OOOO.', 'OYYyYO', 'OYyYYO', 'OYYyYO', 'OYyYYO', '.OOOO.'], { Y: C.gold, y: C.yellow1 }), S(['..OO..', '.OYyO.', '.OyYO.', '.OYyO.', '.OyYO.', '..OO..'], { Y: C.gold, y: C.yellow1 })],
  digit: [0, 1, 2, 3].map(digitSprite),
  expression: [S(['OOOOOOOOOOOOOOOOOOOO', 'OPPPPPPPPPPPPPPPPPPO', 'OPWPWPPWWPPPPWPPWWPO', 'OPPWPPPPWPWWWWWPPWPO', 'OPWPWPPWPPPPPWPPWPPO', 'OPPPPPPWWPPPPPPPWWPO', 'OPPPPPPPPPPPPPPPPPPO', 'OOOOOOOOOOOOOOOOOOOO'], { P: C.purple2 })],
  sushi: [S(['..OOOOOO..', '.ORRRRRRO.', 'OrRRrRRrRO', 'OWWWWWWWWO', 'OWWWWWWWWO', 'OKKKKKKKKO', '.OOOOOOOO.'], { R: C.orange2, r: C.orange3, K: C.black })],
  sashimi: [S(['.OOOOOOOO.', 'ORRrRRrRRO', 'OrRRrRRrRO', '.OOOOOOOO.'], { R: C.red3, r: C.white })],
  plate: [S(['.OOOOOOOO.', 'OWWWWWWWWO', 'OWWRRRRWWO', 'OWWWWWWWWO', '.OOOOOOOO.'], { R: C.orange2 })],
  resistor: [S(['....OOOOOO....', 'MMMMOBRYBOMMMM', '....OOOOOO....'], { M: C.gray5, B: C.rust2, R: C.red2, Y: C.yellow2 })],
  transistor: [S(['.OOOOOO.', 'OKKKKKKO', 'OKKWKKKO', 'OKKKKKKO', 'OOOOOOOO', '.M.M.M..', '.M.M.M..'], { K: C.black, M: C.gray5 })],
  powersupply: [S(['OOOOOOOOOOOO', 'OGGGGGGGGGGO', 'OGKKKKKGGRGO', 'OGKAAAKGGGGO', 'OGKKKKKGYGYO', 'OGGGGGGGGGGO', 'OGRGGGGGGBGO', 'OOOOOOOOOOOO'], { G: C.gray3, K: C.black, A: C.green2, R: C.red2, Y: C.yellow2, B: C.gray1 })],
  scopewave: [0, 1].map((f) => { const p = new Px(16, 10); for (let x = 0; x < 16; x++) { const y = Math.round(4.5 + Math.sin((x + f * 4) / 2.5) * 3.5); p.set(x, y, C.green2); p.set(x, y + 1, C.green1); } return p; }),
  zoneelectric: [S(['..O..', '.OYO.', 'OYYYO', '.OYO.', '..O..'], { Y: C.cyan3 })],
  zonestorm: [S(['..O..', '.OYO.', 'OYYYO', '.OYO.', '..O..'], { Y: C.purple3 })],
  burger: [S(['..OOOOOOOO..', '.OBbBBbBBBO.', 'OBBBBBBBBBBO', 'OLLLLLLLLLLO', 'ORRRRRRRRRRO', 'OMMMMMMMMMMO', 'OBBBBBBBBBBO', '.OOOOOOOOOO.'], { B: C.orange3, b: C.white, L: C.lime1, R: C.red2, M: C.rust1 })],
  dorito: [S(['OOOOOOO', 'OYYyYYO', '.OYYyO.', '.OyYYO.', '..OYO..', '..OYO..', '...O...'], { Y: C.orange2, y: C.red2 }), S(['O......', 'OOO....', 'OYYOO..', 'OyYYYOO', 'OYYYO..', 'OOO....', 'O......'], { Y: C.orange2, y: C.red2 })],
  tire: [tire(0, 16), tire(1, 16)],
  tireorbit: [tire(0, 14), tire(1, 14)],
  pill: [S(['.OOOOO.', 'ORRWWWO', 'ORRWWWO', '.OOOOO.'], { R: C.red2 })],
  syringe: [S(['......OOOOOOO', 'MMMMMOWCCCCWOO', '......OOOOOOO'].map((r) => r.padEnd(14, '.').slice(0, 14)), { M: C.gray5, C: C.cyan2 })],
  syringespecial: [S(['......OOOOOOO.', 'MMMMMOYAAAAYOO', '......OOOOOOO.'], { M: C.gray5, A: C.magenta, Y: C.yellow3 })],
  botshooter: [bot(C.sky1, C.cyan2, 0), bot(C.sky1, C.cyan2, 1)],
  botbrawler: [bot(C.red2, C.yellow3, 0), bot(C.red2, C.yellow3, 1)],
  botdasher: [bot(C.yellow2, C.red2, 0), bot(C.yellow2, C.red2, 1)],
  botmaster: [master(0), master(1)],
  botbolt: [S(['.OO.', 'OCCO', 'OCCO', '.OO.'], { C: C.cyan2 })],
  masterbolt: [S(['..OOOO..', '.OCCCCO.', 'OCWWWWCO', 'OCWWWWCO', '.OCCCCO.', '..OOOO..'], { C: C.cyan2 })],
  phone: [S(['OOOOOOO', 'OKKKKKO', 'OKAAAKO', 'OKAAAKO', 'OKAAAKO', 'OKAAAKO', 'OKKWKKO', 'OOOOOOO'], { K: C.black, A: C.sky2 })],
  paper: [S(['OOOOOOO', 'OYYYYYO', 'OYKKKYO', 'OYYYYYO', 'OYKKYYO', 'OYYYYOO', 'OOOOOO.'], { Y: C.yellow3, K: C.yellow1 })],
  computer: [S(['OOOOOOOOOOOOOO', 'OGGGGGGGGGGGGO', 'OGKKKKKKKKKKGO', 'OGKAAAAAAAAKGO', 'OGKABAAAAAAKGO', 'OGKAAAAAAAAKGO', 'OGKKKKKKKKKKGO', 'OGGGGGGGGGGGGO', 'OOOOOOOOOOOOOO', '....OGGGGO....', '..OOOOOOOOOO..', '..OGGGGGGGGO..', '..OOOOOOOOOO..'], { G: C.gray4, K: C.black, A: C.cyan1, B: W })],
  fragment: [S(['OO.', 'OGO', '.OO'], { G: C.gray4 })],
  jabuti: [jabuti(0), jabuti(1)],
  jabutiwalk: [jabuti(0), jabuti(1)],
  car: [0, 1, 2, 3, 4, 5, 6, 7].map(carFrame),
  insulin: [S(['......OOOOOOO.', 'MMMMMOWYYYYOOO', '......OOOOOOO.'], { M: C.gray5, Y: C.orange2 })],
  vial: [S(['.OOO.', '.OWO.', 'OOOOO', 'OCCCO', 'OCcCO', 'OCCCO', 'OCCCO', 'OOOOO'], { C: C.sky2, c: W })],
  soundwave: [0, 1].map((f) => { const p = new Px(10, 16); for (let k = 0; k < 3; k++) { const r = 4 + k * 3; for (let y = 0; y < 16; y++) { const dy = y - 7.5; const x = Math.round(Math.sqrt(Math.max(0, r * r - dy * dy))) - 4 + f; if (Math.abs(dy) < r && x >= 0 && x < 10) p.set(x, y, k === 1 ? C.yellow3 : W); } } return p; }),
  carrot: [S(['......GG..', '.OOOOOOGG.', 'ORrRRrROG.', 'OrRrRRrOG.', '.OOOOOO...'], { R: '#c46a2a', r: '#7a5a2a', G: '#5a7a2a' })],
  zonestink: [0, 1].map((f) => { const p = new Px(10, 9); ellipse(p, 4.5 + f * 0.5, 4.5, 4, 3.5, '#9ad03a', '#4a6a1a'); ellipse(p, 3, 3.5, 1, 1, '#c8f06a', null); p.set(6, 6 - f, '#4a6a1a'); return p; }),
  scrappart: [S(['.OOO.', 'OMmMO', 'OmOmO', 'OMmMO', '.OOO.'], { M: C.gray5, m: C.gray3 })],
  hotdog: [0, 1].map((f) => dog(f, false)),
  hotdogturbo: [0, 1].map((f) => dog(f, true)),
  zoneheat: [0, 1].map((f) => S(f ? ['...Y..', '..YO..', '.YOOY.', '.OORO.', 'ORRRRO', '.OOOO.'] : ['..Y...', '..OY..', '.YOOY.', '.OROO.', 'ORRRRO', '.OOOO.'], { O: C.orange2, Y: C.yellow3, R: C.red2 })),
  papaya: [0, 1].map((f) => papaya(f)),
  afterimage: [(() => { const p = new Px(16, 24); ellipse(p, 7.5, 6, 5, 5, '#c8f0ff', '#7ab8d8'); rect(p, 3, 11, 10, 9, '#c8f0ff'); rect(p, 4, 20, 3, 3, '#c8f0ff'); rect(p, 9, 20, 3, 3, '#c8f0ff'); p.set(5, 6, '#7ab8d8'); p.set(10, 6, '#7ab8d8'); return p; })()],
  zoneheal: [S(['..OOO..', '..OGO..', 'OOOGOOO', 'OGGGGGO', 'OOOGOOO', '..OGO..', '..OOO..'], { G: C.green2, O: C.green1 })],
  bottle: [S(['..OO..', '..OGO.', '.OGGO.', 'OGGgGO', 'OGGgGO', 'OGGgGO', 'OGLLGO', 'OGGGGO', 'OGGGGO', '.OOOO.'], { G: C.bottle2, g: C.bottle3, L: C.yellow3 })],
};

/** Sprites que giram pelo ângulo do snapshot (os demais não giram ou têm quadros por direção). */
export const ROTATING = ['carrot', 'scrappart', 'laser', 'briefcase', 'expression', 'sashimi', 'resistor', 'transistor', 'scopewave', 'dorito', 'pill', 'syringe', 'syringespecial', 'phone', 'insulin', 'soundwave', 'bottle', 'jabuti', 'jabutiwalk', 'masterbolt', 'computer'];

export function allySprites() {
  const out = [];
  for (const [name, frames] of Object.entries(SPRITES))
    frames.forEach((px, i) => out.push({ id: `ally_${name}_${i}`, category: 'Weapons', sub: 'Personagens', w: px.w, h: px.h, pivot: [px.w / 2, px.h / 2], px }));
  const fx = (id, px) => out.push({ id, category: 'Effects', w: px.w, h: px.h, pivot: [px.w / 2, px.h / 2], px });
  const tv = new Px(18, 16);
  box(tv, 0, 0, 18, 13, C.gray2); box(tv, 2, 2, 14, 9, C.black); rect(tv, 4, 4, 7, 2, C.cyan1); rect(tv, 4, 7, 4, 1, C.cyan1);
  line(tv, 5, 13, 3, 15, O); line(tv, 12, 13, 14, 15, O);
  fx('fx_tv', tv);
  fx('fx_note', S(['..OO', '..OW', '..O.', '..O.', 'OOO.', 'OWO.', 'OOO.'], {}));
  fx('fx_shard', S(['OG.', 'OGg', '.OO'], { G: C.bottle2, g: C.bottle3 }));
  fx('fx_mark', S(['OOOOO', 'OP..O', '.OP..', 'OP..O', 'OOOOO'], { O: C.pink2, P: C.pink3 }));
  fx('fx_sticky', S(['OOOOO', 'OYYYO', 'OYKYO', 'OYYOO', 'OOOO.'], { Y: C.yellow3, K: C.yellow1 }));
  const trap = new Px(24, 24); circleLine(trap, 11.5, 11.5, 11, C.cyan3); circleLine(trap, 11.5, 11.5, 10, C.pink3); trap.set(6, 5, W); trap.set(7, 5, W); trap.set(6, 6, W);
  fx('fx_trap', trap);
  fx('fx_skid', S(['OO', 'OO'], { O: C.black }));
  const plus = S(['.O.', 'OOO', '.O.'], { O: C.yellow3 });
  fx('fx_plus', plus);
  return out;
}

/** Ícone 32×32 de uma arma: desenho do projétil/companheiro ampliado 2× sobre a moldura. */
export function iconArt(p, names) {
  const n = names.length;
  names.forEach((name, k) => {
    const src = SPRITES[name][0];
    const scale = Math.max(1, Math.min(2, Math.floor(26 / Math.max(src.w, src.h) / (n > 1 ? 1.3 : 1))));
    const w = src.w * scale, h = src.h * scale;
    const ox = Math.round(16 - w / 2 + (n > 1 ? (k - (n - 1) / 2) * 9 : 0)), oy = Math.round(16 - h / 2 + (n > 1 ? (k % 2 ? 4 : -4) : 0));
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const v = src.get(Math.floor(x / scale), Math.floor(y / scale)); if (v) p.set(ox + x, oy + y, v); }
  });
  return p;
}
