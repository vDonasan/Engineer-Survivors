// Eventos aleatórios e camadas visuais dos personagens: ícones dos eventos, coxinhas, moto, emotes sobre a
// cabeça, acessórios (chapéu de japonês, camiseta ao contrário, protoboard, cenoura) e partículas.
// Os acessórios são camadas separadas, posicionadas pelo ponto da cabeça de cada quadro (anchor do manifesto).
import { C, PALETTE_HEX } from './palette.mjs';
import { Px } from './pixel.mjs';

const K = '#14161c'; // contorno
const P = {
  paper: '#f2ede0', paperS: '#c9c2ae', red: '#e03a3a', redD: '#9a1e28', green: '#3fa34d', greenL: '#7fd36b', gold: '#ffd23f', goldD: '#c8781a',
  dough: '#e8a24a', doughL: '#f6c46e', doughD: '#a8642a', straw: '#e6c77a', strawD: '#b08a3e', strawL: '#fff0b8',
  board: '#f4f1e8', boardS: '#c8c2b0', led: '#3fe0ff', wire: '#e03a3a', carrot: '#e8792a', carrotD: '#a84a1a', leaf: '#5fae3a', leafD: '#3a6e22',
  moto: '#d63a3a', motoD: '#8a1e22', chrome: '#c9cdd2', tire: '#24272e', helmet: '#ffd23f', jacket: '#3a4a6a', blue: '#3a7ad6', blueL: '#8ec8ff',
  cheek: '#ff6a6a', pink: '#ff8ab8', white: '#ffffff', gray: '#7a808a', smoke: '#b8bec6', flame: '#ff8a1e', flameL: '#ffd23f', clock: '#e6e9ed',
  tag: '#ffffff', seam: '#8a8f99', purple: '#8a5ad6',
};
for (const v of [...Object.values(P), K]) if (!PALETTE_HEX.includes(v)) PALETTE_HEX.push(v);

const px = (w, h) => new Px(w, h);
function put(p, rows, map, ox = 0, oy = 0) {
  rows.forEach((r, y) => [...r].forEach((ch, x) => { if (ch !== '.' && map[ch]) p.set(ox + x, oy + y, map[ch]); }));
  return p;
}
function outline(p) { const o = p.clone(); for (const [x, y] of p.ring()) o.set(x, y, K); return o; }
function disc(p, cx, cy, r, col) { for (let y = -r; y <= r; y++) for (let x = -r; x <= r; x++) if (x * x + y * y <= r * r + r * 0.6) p.set(cx + x, cy + y, col); }

// ---------- ícones dos eventos (32×32, moldura por tipo) ----------

const FRAME = { positivo: P.green, negativo: P.red, especial: P.purple, neutro: P.gray };
function frame(kind) {
  const p = px(32, 32);
  for (let y = 1; y < 31; y++) for (let x = 1; x < 31; x++) p.set(x, y, (x + y) % 2 ? '#20232c' : '#262a34');
  for (let i = 1; i < 31; i++) { p.set(i, 1, FRAME[kind]); p.set(i, 30, FRAME[kind]); p.set(1, i, FRAME[kind]); p.set(30, i, FRAME[kind]); }
  for (let i = 0; i < 32; i++) { p.set(i, 0, K); p.set(i, 31, K); p.set(0, i, K); p.set(31, i, K); }
  return p;
}
function paper(p, grade) {
  for (let y = 6; y < 27; y++) for (let x = 8; x < 24; x++) p.set(x, y, y === 6 || x === 23 ? P.paperS : P.paper);
  for (let y = 10; y < 24; y += 3) for (let x = 10; x < 17; x++) p.set(x, y, P.paperS);
  if (grade === 10) {
    put(p, ['.X..XXX.', 'XX..X.X.', '.X..X.X.', '.X..X.X.', 'XXX.XXX.'], { X: P.red }, 14, 15);
    disc(p, 21, 23, 3, P.gold); p.set(21, 23, P.goldD);
  } else {
    put(p, ['.XXXX.', 'XX..XX', 'X....X', 'X....X', 'XX..XX', '.XXXX.'], { X: P.red }, 14, 14);
    for (let i = 0; i < 4; i++) p.set(9 + i * 3, 25 - (i % 2), '#1a1418');
  }
}
function coxinha(p, ox, oy, gold) {
  const a = gold ? P.gold : P.dough, l = gold ? P.strawL : P.doughL, d = gold ? P.goldD : P.doughD;
  const rows = ['....X....', '...XAX...', '..XAALX..', '..XAALX..', '.XAAAALX.', '.XAAAAAX.', 'XAAAAAAAX', 'XDAAAAADX', '.XDDDDDX.', '..XXXXX..'];
  put(p, rows, { X: K, A: a, L: l, D: d }, ox, oy);
}
function moto(p, ox, oy) {
  // de lado, virada para a direita
  const rows = [
    '..........HH..........',
    '.........HHHH.........',
    '.........JJJ..........',
    '........JJJJJ.........',
    '...MMMMMMMMMMMMC......',
    '..MMDDDDMMMMMMMCC.....',
    '.RRR.CC.....CC.RRR....',
    'RTTTR.......RTTTTR....',
    'RTCTR.......RTTCTR....',
    'RTTTR.......RTTTTR....',
    '.RRR.........RRRR.....',
  ];
  put(p, rows, { H: P.helmet, J: P.jacket, M: P.moto, D: P.motoD, C: P.chrome, R: K, T: P.tire }, ox, oy);
}

export function eventIcons() {
  const icons = {
    bolha_social: (p) => { disc(p, 16, 16, 10, '#3a5a8a'); disc(p, 16, 16, 8, '#4a7ab8'); p.set(12, 11, P.white); p.set(13, 10, P.white);
      put(p, ['X.X', 'XXX', '.X.'], { X: P.pink }, 11, 15); put(p, ['XXXX', 'X..X', 'XXXX', '.X..'], { X: P.white }, 17, 13); disc(p, 20, 21, 1, P.red); },
    dez_na_prova: (p) => paper(p, 10),
    zero_na_prova: (p) => paper(p, 0),
    chamado_de_burro: (p) => { put(p, ['XXXXXXXXXXXXXX', 'XWWWWWWWWWWWWX', 'XWW?WWW?WW?WWX', 'XWW?WW?W?W?WWX', 'XWWWW?WWWW?WWX', 'XWWWWWWWWWWWWX', 'XXXXXXXXXXXXXX', '...XX.........', '..XX..........'],
      { X: K, W: P.white, '?': P.red }, 9, 8); put(p, ['X+X', '.X.', 'X=X'], { X: P.blue, '+': P.blue, '=': P.blue }, 11, 24); },
    laboratorio_incompleto: (p) => { disc(p, 16, 16, 10, K); disc(p, 16, 16, 9, P.clock); for (let i = 0; i < 6; i++) p.set(16, 16 - i, K); for (let i = 0; i < 5; i++) p.set(16 + i, 16, P.red);
      for (const [x, y] of [[16, 8], [24, 16], [16, 24], [8, 16]]) p.set(x, y, K); put(p, ['X.X', '.X.', 'X.X'], { X: P.red }, 24, 4); },
    motoqueiro: (p) => { moto(p, 5, 11); for (let x = 2; x < 6; x++) { p.set(x, 15, P.smoke); p.set(x - 1, 18, P.smoke); } },
    coxinha: (p) => { coxinha(p, 11, 10, false); for (const [x, y] of [[8, 9], [24, 12], [9, 22]]) p.set(x, y, P.gold); },
    jalapeno: (p) => { put(p, ['......LL', '.....LL.', '...GGGG.', '..GGGGGG', '.GGGGGGG', 'GGGGGGG.', 'GGGGGG..', 'GGGG....', '.GG.....'], { L: P.leafD, G: P.green }, 10, 9);
      for (const [x, y] of [[13, 13], [14, 12]]) p.set(x, y, P.greenL); put(p, ['.F.', 'FYF', 'FYF'], { F: P.flame, Y: P.flameL }, 20, 18); },
  };
  const kinds = { bolha_social: 'especial', dez_na_prova: 'positivo', zero_na_prova: 'negativo', chamado_de_burro: 'negativo', laboratorio_incompleto: 'especial', motoqueiro: 'especial', coxinha: 'positivo', jalapeno: 'positivo' };
  return Object.entries(icons).map(([id, draw]) => { const p = frame(kinds[id]); draw(p); return { id: `icon_evt_${id}`, category: 'UI', sub: 'Eventos', w: 32, h: 32, pivot: [16, 16], px: p }; });
}

// ---------- no mundo: coxinhas, moto, emotes, partículas ----------

export function eventWorldSprites() {
  const out = [];
  const add = (id, p, cat = 'Effects') => out.push({ id, category: cat, w: p.w, h: p.h, pivot: [p.w / 2, p.h / 2], px: p });
  for (const [id, gold] of [['fx_coxinha', false], ['fx_coxinha_suprema', true]]) { const p = px(13, 12); coxinha(p, 2, 1, gold); add(id, p); }
  // moto: direita (e esquerda espelhada), 2 quadros (rodas)
  for (let f = 0; f < 2; f++) {
    const p = px(24, 14); moto(p, 1, 2);
    if (f) for (const x of [2, 15]) p.set(x + 1, 10, P.chrome);
    add(`fx_moto_right_${f}`, p); add(`fx_moto_left_${f}`, p.flipX());
  }
  // emotes sobre a cabeça (12×12)
  const emote = (id, draw) => { const p = px(12, 12); draw(p); add(`emote_${id}`, outline(p)); };
  emote('dez', (p) => { for (let y = 1; y < 11; y++) for (let x = 2; x < 10; x++) p.set(x, y, P.paper); put(p, ['X.XX', 'X.XX', 'X.XX'], { X: P.red }, 4, 4); });
  emote('zero', (p) => { for (let y = 1; y < 11; y++) for (let x = 2; x < 10; x++) p.set(x, y, P.paper); put(p, ['.XX.', 'X..X', 'X..X', '.XX.'], { X: P.red }, 4, 4); });
  emote('burro', (p) => put(p, ['.XXX.', 'X...X', '...X.', '..X..', '.....', '..X..'], { X: P.red }, 3, 2));
  emote('relogio', (p) => { disc(p, 6, 6, 4, P.clock); p.set(6, 4, K); p.set(6, 5, K); p.set(7, 6, P.red); p.set(8, 6, P.red); });
  emote('coxinha', (p) => coxinha(p, 1, 1, false));
  emote('fogo', (p) => put(p, ['....X...', '...XX...', '..XYX.X.', '.XYYXXX.', '.XYYYYX.', 'XYYWYYYX', 'XYWWWYYX', '.XYYYYX.'], { X: P.flame, Y: P.flameL, W: P.white }, 2, 2));
  emote('tontura', (p) => put(p, ['.XXXX.', 'X....X', 'X.XX.X', 'X.X..X', 'X.XXXX', '.X....'], { X: P.purple }, 3, 3));
  emote('camiseta', (p) => put(p, ['XX..XX', 'XXXXXX', '.XTTX.', '.XXXX.', '.XXXX.'], { X: P.blue, T: P.tag }, 3, 3));
  emote('notificacao', (p) => { disc(p, 6, 6, 4, P.red); put(p, ['X', 'X', 'X', '.', 'X'], { X: P.white }, 6, 4); });
  emote('coracao', (p) => put(p, ['.XX.XX.', 'XXXXXXX', 'XXXXXXX', '.XXXXX.', '..XXX..', '...X...'], { X: P.pink }, 2, 3));
  emote('foco', (p) => { put(p, ['XXXXXXXX', 'X.Y.Y..X', 'X......X', 'XXXXXXXX'], { X: P.board, Y: P.led }, 2, 4); });
  // partículas pequenas
  const dot = (id, rows, map) => { const p = px(rows[0].length, rows.length); put(p, rows, map); add(id, p); };
  dot('fx_estrela', ['.X.', 'XYX', '.X.'], { X: P.gold, Y: P.white });
  dot('fx_espiral', ['.XXX.', 'X...X', 'X.X.X', 'X..X.', '.X...'], { X: P.purple });
  dot('fx_engrenagem', ['.X.X.', 'XXXXX', '.X.X.', 'XXXXX', '.X.X.'], { X: P.chrome });
  dot('fx_chama', ['.X.', 'XYX', 'XYX', '.X.'], { X: P.flame, Y: P.flameL });
  dot('fx_mensagem', ['XXXX', 'XWWX', 'XXXX', 'X...'], { X: P.blue, W: P.white });
  dot('fx_coracao', ['X.X', 'XXX', '.X.'], { X: P.pink });
  dot('fx_simbolo', ['X.X', '.X.', 'X.X'], { X: P.blueL });
  dot('fx_garrafa', ['.X.', '.X.', 'XYX', 'XYX', 'XYX', 'XXX'], { X: P.greenL, Y: P.gold });
  dot('fx_vapor', ['.XX.', 'XXXX', '.XX.'], { X: P.smoke });
  return out;
}

// ---------- acessórios por direção (camadas) ----------
// Âncora: (16, topo da cabeça) do quadro do personagem. Cada sprite de acessório tem 32×32 com o
// pivô no ponto da âncora, então basta colocá-lo exatamente na âncora do quadro atual.

const DIRS = ['down', 'up', 'left', 'right'];

function hat(dir) {
  // chapéu cônico de palha (kasa): aba larga, ponta no topo
  const p = px(32, 48);
  const cx = 16, top = 14; // âncora em (16, 14): o chapéu cobre o alto da cabeça
  for (let y = 0; y < 6; y++) {
    const half = 2 + y * 2;
    for (let x = cx - half; x <= cx + half; x++) p.set(x, top - 6 + y + 3, (x + y) % 3 === 0 ? P.strawD : y < 2 ? P.strawL : P.straw);
  }
  for (let x = cx - 12; x <= cx + 12; x++) p.set(x, top + 3, P.strawD);
  if (dir === 'left' || dir === 'right') for (let x = cx - 3; x <= cx + 3; x++) p.set(x + (dir === 'left' ? -1 : 1), top + 4, P.redD); // cordão
  return outline(p);
}

function shirt(dir) {
  // camiseta do avesso: etiqueta para fora e costuras aparentes (sobre o tronco)
  const p = px(32, 48);
  const top = 14 + 16; // gola: ~16 px abaixo do topo da cabeça
  if (dir === 'down') {
    for (let y = 0; y < 2; y++) for (let x = 15; x < 18; x++) p.set(x, top + y, P.tag);
    p.set(16, top + 1, P.seam);
    for (let y = top; y < top + 7; y++) { p.set(11, y, P.seam); p.set(21, y, P.seam); }
    for (let x = 12; x < 21; x += 2) p.set(x, top + 7, P.seam);
  } else if (dir === 'up') {
    for (let y = top; y < top + 7; y++) { p.set(11, y, P.seam); p.set(21, y, P.seam); }
    for (let x = 12; x < 21; x += 2) p.set(x, top + 1, P.seam);
  } else {
    const x0 = dir === 'left' ? 17 : 14;
    for (let y = 0; y < 3; y++) { p.set(x0, top + y, P.tag); p.set(x0 + 1, top + y, P.tag); }
    for (let y = top + 1; y < top + 7; y++) p.set(dir === 'left' ? 13 : 19, y, P.seam);
  }
  return p;
}

function board(dir, evolved) {
  // protoboard presa à cintura, do lado da mão (some atrás do corpo de costas)
  const p = px(32, 48);
  if (dir === 'up') return p;
  const y0 = 14 + 22;
  const x0 = dir === 'left' ? 9 : dir === 'right' ? 18 : 20;
  for (let y = 0; y < 4; y++) for (let x = 0; x < 6; x++) p.set(x0 + x, y0 + y, y === 3 ? P.boardS : P.board);
  for (let x = 1; x < 5; x += 2) p.set(x0 + x, y0 + 1, P.wire);
  p.set(x0 + 2, y0 + 2, P.led);
  if (evolved) { p.set(x0 + 4, y0 + 2, P.led); p.set(x0 + 1, y0 + 2, P.gold); p.set(x0 + 5, y0 - 1, P.led); p.set(x0, y0 - 1, P.gold); }
  return outline(p);
}

function carrot(dir) {
  const p = px(32, 48);
  if (dir === 'up') return p;
  const y0 = 14 + 20, x0 = dir === 'left' ? 19 : dir === 'right' ? 10 : 10;
  put(p, ['.LL.', 'LL..', '.CC.', '.CC.', '.CD.', '..D.', '..D.'], { L: P.leaf, C: P.carrot, D: P.carrotD }, x0, y0);
  p.set(x0 + 1, y0 + 4, P.leafD); // mancha podre
  return outline(p);
}

function blush(dir) {
  // rosto avermelhado (Tequila, Jalapeño): bochechas, só de frente e de lado
  const p = px(32, 48);
  if (dir === 'up') return p;
  const y = 14 + 10;
  const xs = dir === 'down' ? [11, 12, 19, 20] : dir === 'left' ? [10, 11] : [21, 22];
  for (const x of xs) { p.set(x, y, P.cheek); p.set(x, y + 1, P.cheek); }
  return p;
}

export function accessorySprites() {
  const out = [];
  for (const dir of DIRS) {
    for (const [id, p] of [['chapeu', hat(dir)], ['camiseta', shirt(dir)], ['protoboard', board(dir, false)], ['protoboard_evo', board(dir, true)],
      ['cenoura', carrot(dir)], ['rubor', blush(dir)]])
      out.push({ id: `acc_${id}_${dir}`, category: 'Effects', sub: 'Acessorios', w: 32, h: 48, pivot: [16, 48 - 14], px: p });
  }
  return out;
}

/** Ponto da cabeça de um quadro de personagem: centro do alto da cabeça (x, y a partir do topo). */
export function headAnchor(p) {
  for (let y = 0; y < p.h; y++) {
    let sum = 0, n = 0;
    for (let x = 0; x < p.w; x++) if (p.get(x, y)) { sum += x; n++; }
    if (n >= 3) {
      // média das 4 primeiras linhas com conteúdo: centro da cabeça, não de um fio solto
      let s2 = 0, n2 = 0;
      for (let yy = y; yy < Math.min(p.h, y + 4); yy++) for (let x = 0; x < p.w; x++) if (p.get(x, yy)) { s2 += x; n2++; }
      return [Math.round(s2 / n2), y];
    }
  }
  return [16, 4];
}
