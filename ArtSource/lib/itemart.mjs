// Ícones dos itens 64×64, todos desenhados neste arquivo com um guia de estilo único:
// - objetos em vista ¾, luz de cima-esquerda, ocupando ~44–48 px do quadro;
// - formas pintadas com MATERIAIS: o renderizador aplica 4 tons por material (sombra puxada para o roxo,
//   brilho puxado para o amarelo), pontilhado nas transições e contorno colorido (mais escuro que o objeto);
// - moldura pelo tipo (arma: quadrada chanfrada; item: hexágono; atributo: redonda; especial: octógono),
//   borda na cor da raridade e fundo com o tom da categoria;
// - evoluções são o mesmo objeto turbinado (peças douradas, brilho, partes a mais).
import { Px } from './pixel.mjs';
import { C } from './palette.mjs';
import { ellipse, disc, line } from './draw.mjs';

const SIZE = 64;
const rgb = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
const hex = (c) => '#' + c.map((v) => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join('');
export const mix = (a, b, t) => { const x = rgb(a), y = rgb(b); return hex(x.map((v, i) => v + (y[i] - v) * t)); };

// ——— materiais ———
const BASE = {
  steel: '#8e9aab', iron: '#5a6476', dark: '#363a48', black: '#24262f', white: '#e9edf2', paper: '#efe6cf',
  gold: '#f0b43a', copper: '#cf7a3c', brass: '#d9b45a', red: '#e0433b', crimson: '#a8253a', orange: '#ff8a2a',
  yellow: '#ffd23f', green: '#4fbf5a', lime: '#a3d43a', leaf: '#3e8c3c', olive: '#7a8a3a', cyan: '#3fd0ff',
  blue: '#3a7bd5', navy: '#2c3b6e', purple: '#8a5ad0', pink: '#ff7eb6', cream: '#f2dfb0', bread: '#d99a4e',
  brown: '#9a5a32', wood: '#6a4228', skin: '#e8b48c', glass: '#a8ecff', straw: '#d9b45a', carrot: '#e8792c',
  papaya: '#ff9a3a', bottle: '#3fa05a', teal: '#2a8f9a', pcb: '#2f8a4c', rubber: '#3b3e4a', salmon: '#ff8a66',
  rice: '#f4f1e6', sky: '#6aa6d8', denim: '#3d5a8a', tequila: '#ecc86a', lettuce: '#6fc23a', cheese: '#ffc83a',
  meat: '#8a4a2e', bone: '#f2ead8', shell: '#4f9a3a', mint: '#7fe0b0', magenta: '#e04ad0', amber: '#ffb52e',
};
const MATTE = new Set(['paper', 'straw', 'bread', 'skin', 'rice', 'denim', 'sky', 'rubber', 'lettuce', 'meat', 'carrot', 'shell', 'olive', 'leaf', 'wood', 'bone']);
const RAMPS = {};
/** Rampa [contorno, sombra, base, claro, brilho] de um material (ou de uma cor #hex usada como material). */
export function ramp(k) {
  if (RAMPS[k]) return RAMPS[k];
  const b = BASE[k] || k;
  return (RAMPS[k] = [mix(b, '#120a22', 0.74), mix(b, '#2b1f52', 0.36), b, mix(b, '#fff2cf', 0.3), mix(b, '#ffffff', 0.66)]);
}

// ——— polígonos convexos e distância com sinal (molduras) ———
function sdPoly(pts, x, y) {
  let d = Infinity;
  for (let i = 0; i < pts.length; i++) {
    const [x0, y0] = pts[i], [x1, y1] = pts[(i + 1) % pts.length];
    const ex = x1 - x0, ey = y1 - y0, len = Math.hypot(ex, ey);
    d = Math.min(d, ((y - y0) * ex - (x - x0) * ey) / len);
  }
  return d;
}
const polyN = (n, r, rot = 0) => Array.from({ length: n }, (_, i) => [32 + Math.cos(rot + (i * 2 * Math.PI) / n) * r, 32 + Math.sin(rot + (i * 2 * Math.PI) / n) * r]);
const chamfer = (c) => [[c, 1], [63 - c, 1], [63, c], [63, 63 - c], [63 - c, 63], [c, 63], [1, 63 - c], [1, c]];
// os pontos seguem o sentido horário na tela (y para baixo), então a distância é positiva por dentro
const SHAPES = {
  weapon: chamfer(8),
  component: [[17, 1.5], [47, 1.5], [62.5, 32], [47, 62.5], [17, 62.5], [1.5, 32]],
  attribute: polyN(40, 31.2),
  survival: polyN(40, 31.2),
  special: chamfer(18),
};

const RARITY = { comum: C.gray5, incomum: C.cyan2, raro: C.amber, reliquia: C.magenta };
const CATEGORY = [
  [['Energia', 'Eletronica'], '#3fd0ff'], [['Termico'], '#ff7a2a'], [['Quimica', 'Organico'], '#a6e03a'],
  [['Robotica', 'Automacao', 'Mecanica'], '#9fb0c8'], [['Biomecanica', 'Cura', 'Suporte'], '#ff6a7a'],
  [['Software', 'Sensores'], '#b48aff'], [['Defesa', 'Mobilidade'], '#7ac0ff'], [['Projetil'], '#ffd23f'],
];
export function categoryColor(tags = []) {
  for (const t of tags) for (const [names, col] of CATEGORY) if (names.includes(t)) return col;
  return '#aab4c4';
}

/** Moldura com fundo: forma pelo tipo, borda da raridade, degradê com o tom da categoria e sombra no chão. */
function frame(kind, rarity, tint, evo) {
  const pts = SHAPES[kind] || SHAPES.weapon, accent = RARITY[rarity] || C.gray5;
  const p = new Px(SIZE, SIZE);
  const top = mix('#1b2030', tint, 0.2), bottom = mix('#0c0e15', tint, 0.05);
  for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
    const s = sdPoly(pts, x + 0.5, y + 0.5);
    if (s < 0) continue;
    if (s < 1.3) { p.set(x, y, '#0a0b12'); continue; }
    if (s < 3.4) { p.set(x, y, y < 24 ? mix(accent, '#ffffff', 0.25) : y < 44 ? accent : mix(accent, '#000000', 0.35)); continue; }
    if (s < 4.5) { p.set(x, y, '#0a0b12'); continue; }
    let col = mix(top, bottom, Math.floor((y / SIZE) * 7) / 7);
    const g = Math.hypot(x - 31.5, y - 29.5) / 30;
    const glow = evo ? 0.32 : 0.16;
    if (g < 0.9) col = mix(col, tint, glow * (Math.ceil((0.9 - g) * 5) / 5));
    if (((x - 32) / 19) ** 2 + ((y - 55) / 3.6) ** 2 <= 1) col = mix(col, '#000000', 0.45);
    p.set(x, y, col);
  }
  if (rarity === 'raro' || rarity === 'reliquia') {
    let gx = 50; while (gx > 32 && sdPoly(pts, gx + 3.5, 9) < 7) gx--;
    const g = [mix(accent, '#000000', 0.5), accent, mix(accent, '#ffffff', 0.5)];
    for (const [dx, dy, i] of [[1, 0, 0], [0, 1, 0], [1, 1, 1], [2, 1, 1], [1, 2, 1], [2, 2, 1], [3, 1, 0], [1, 3, 0], [3, 2, 0], [2, 3, 0], [1, 1, 2]]) p.set(gx + dx, 6 + dy, g[i]);
  }
  return p;
}

// ——— tela de desenho com materiais ———
class Art {
  constructor() { this.m = new Px(SIZE, SIZE); this.d = new Px(SIZE, SIZE); }
  r(x, y, w, h, k) { for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) this.m.set(x + i, y + j, k); return this; }
  e(cx, cy, rx, ry, k) {
    for (let y = Math.floor(cy - ry - 1); y <= Math.ceil(cy + ry + 1); y++) for (let x = Math.floor(cx - rx - 1); x <= Math.ceil(cx + rx + 1); x++)
      if (((x - cx) / (rx + 0.5)) ** 2 + ((y - cy) / (ry + 0.5)) ** 2 <= 1) this.m.set(x, y, k);
    return this;
  }
  c(cx, cy, r, k) { return this.e(cx, cy, r, r, k); }
  p(pts, k) {
    const ys = pts.map((q) => q[1]);
    for (let y = Math.floor(Math.min(...ys)); y <= Math.ceil(Math.max(...ys)); y++) {
      const xs = [];
      for (let i = 0; i < pts.length; i++) {
        const [x0, y0] = pts[i], [x1, y1] = pts[(i + 1) % pts.length];
        if ((y0 <= y + 0.5 && y1 > y + 0.5) || (y1 <= y + 0.5 && y0 > y + 0.5)) xs.push(x0 + ((y + 0.5 - y0) / (y1 - y0)) * (x1 - x0));
      }
      xs.sort((a, b) => a - b);
      for (let k2 = 0; k2 + 1 < xs.length; k2 += 2) for (let x = Math.round(xs[k2]); x < Math.round(xs[k2 + 1]); x++) this.m.set(x, y, k);
    }
    return this;
  }
  t(x0, y0, x1, y1, r, k) { const n = Math.max(1, Math.ceil(Math.hypot(x1 - x0, y1 - y0) * 2)); for (let i = 0; i <= n; i++) this.c(x0 + ((x1 - x0) * i) / n, y0 + ((y1 - y0) * i) / n, r, k); return this; }
  /** Linha grossa por uma lista de pontos. */
  path(pts, r, k) { for (let i = 0; i + 1 < pts.length; i++) this.t(...pts[i], ...pts[i + 1], r, k); return this; }
  ring(cx, cy, r, w, k) {
    for (let y = Math.floor(cy - r - 1); y <= Math.ceil(cy + r + 1); y++) for (let x = Math.floor(cx - r - 1); x <= Math.ceil(cx + r + 1); x++) { const d = Math.hypot(x - cx, y - cy); if (d <= r + 0.5 && d > r - w + 0.5) this.m.set(x, y, k); }
    return this;
  }
  cut(x, y, w, h) { return this.r(x, y, w, h, null); }
  // detalhes (por cima do sombreado): tom i da rampa do material (0 contorno … 4 brilho) ou cor #hex
  col(k, i) { return k[0] === '#' && i === undefined ? k : ramp(k)[i ?? 2]; }
  dp(x, y, k, i) { this.d.set(x, y, this.col(k, i)); return this; }
  dr(x, y, w, h, k, i) { const c = this.col(k, i); for (let j = 0; j < h; j++) for (let q = 0; q < w; q++) this.d.set(x + q, y + j, c); return this; }
  dl(x0, y0, x1, y1, k, i) { line(this.d, Math.round(x0), Math.round(y0), Math.round(x1), Math.round(y1), this.col(k, i)); return this; }
  dc(cx, cy, r, k, i) { disc(this.d, cx, cy, r, this.col(k, i), null); return this; }
  de(cx, cy, rx, ry, k, i) { ellipse(this.d, cx, cy, rx, ry, this.col(k, i), null); return this; }
  dring(cx, cy, r, k, i) { const c = this.col(k, i); for (let y = Math.floor(cy - r - 1); y <= Math.ceil(cy + r + 1); y++) for (let x = Math.floor(cx - r - 1); x <= Math.ceil(cx + r + 1); x++) { const d = Math.hypot(x - cx, y - cy); if (d <= r + 0.5 && d > r - 0.5) this.d.set(x, y, c); } return this; }
  /** Texto de pixels 3×5 (dígitos e símbolos). */
  txt(s, x, y, k, i) {
    const G = { 0: '111101101101111', 1: '010110010010111', 2: '111001111100111', 3: '111001111001111', 4: '101101111001001', 5: '111100111001111', 6: '111100111101111', 7: '111001010010010', 8: '111101111101111', 9: '111101111001111', '+': '000010111010000', '-': '000000111000000', '=': '000111000111000', x: '000101010101000', '%': '101001010100101' };
    [...s].forEach((ch, n) => { const g = G[ch]; if (g) for (let j = 0; j < 15; j++) if (g[j] === '1') this.dp(x + n * 4 + (j % 3), y + Math.floor(j / 3), k, i); });
    return this;
  }
  sparkle(x, y, k = 'yellow', s = 2) { this.dp(x, y, '#ffffff'); for (let q = 1; q <= s; q++) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) this.dp(x + dx * q, y + dy * q, k, q === s ? 2 : 3); return this; }
}

/** Sombreamento por material: luz de cima-esquerda, 4 tons com pontilhado e contorno colorido seletivo. */
function shade(art) {
  const m = art.m, out = new Px(SIZE, SIZE);
  const run = (x, y, dx, dy, k) => { let n = 0; while (n < 48 && m.get(x + dx * (n + 1), y + dy * (n + 1)) === k) n++; return n + 1; };
  for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
    const k = m.get(x, y);
    if (!k) continue;
    const R = ramp(k);
    const dU = run(x, y, 0, -1, k), dD = run(x, y, 0, 1, k), dL = run(x, y, -1, 0, k), dR = run(x, y, 1, 0, k);
    const t = 0.62 * (dU / (dU + dD)) + 0.38 * (dL / (dL + dR));
    const dith = (x + y) & 1;
    let i = t < 0.3 ? 3 : t < 0.68 ? 2 : 1;
    if (Math.abs(t - 0.3) < 0.03) i = dith ? 3 : 2;
    else if (Math.abs(t - 0.68) < 0.03) i = dith ? 2 : 1;
    if (dU === 1 && dD === 1) i = 3;
    else if (dU === 1) i = MATTE.has(k) ? 3 : t < 0.5 ? 4 : 3;
    else if (dL === 1 && i < 3 && t < 0.6) i = 3;
    else if (dD === 1 || dR === 1) i = Math.min(i, 1);
    out.set(x, y, R[i]);
  }
  // contorno colorido: mais suave do lado da luz
  for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) {
    if (m.get(x, y)) continue;
    const below = m.get(x, y + 1) || m.get(x + 1, y), above = m.get(x, y - 1) || m.get(x - 1, y);
    const k = above || below;
    if (!k) continue;
    const R = ramp(k);
    out.set(x, y, above ? R[0] : mix(R[0], R[1], 0.45));
  }
  out.blit(art.d);
  return out;
}

// ——— peças reutilizadas ———
const bolt = (a, ox, oy, s, k) => a.p([[ox + 8 * s, oy], [ox + 1 * s, oy + 13 * s], [ox + 6 * s, oy + 13 * s], [ox + 2 * s, oy + 24 * s], [ox + 13 * s, oy + 9 * s], [ox + 8 * s, oy + 9 * s], [ox + 12 * s, oy]], k);
const goldTrim = (a, x, y, w) => a.r(x, y, w, 2, 'gold');
const flame = (a, x, y, s = 1) => { a.e(x, y, 3.5 * s, 6 * s, 'orange'); a.e(x, y + 2 * s, 2 * s, 3.5 * s, 'yellow'); a.p([[x - 3 * s, y - 2 * s], [x, y - 10 * s], [x + 3 * s, y - 2 * s]], 'orange'); };
const speed = (a, x, y, n = 3, len = 8, k = 'white') => { for (let i = 0; i < n; i++) a.dr(x - (i % 2) * 3, y + i * 4, len - (i % 2) * 3, 1, k, k[0] === '#' ? undefined : 3); };

// ——— armas iniciais e evoluções dos personagens ———
const W = {
  descarga_direcionada(a) {
    a.r(10, 40, 14, 14, 'iron'); a.r(12, 36, 10, 5, 'steel'); a.r(14, 32, 2, 5, 'copper'); a.r(19, 32, 2, 5, 'copper');
    a.dr(12, 44, 10, 2, 'yellow', 3); a.txt('+', 13, 47, 'red', 3); a.txt('-', 17, 47, 'cyan', 3);
    bolt(a, 24, 6, 1.9, 'cyan'); a.dl(39, 10, 30, 30, '#ffffff'); a.sparkle(50, 44, 'cyan'); a.sparkle(26, 22, 'cyan', 1);
  },
  pulso_amplificado(a) {
    for (let i = 0; i < 5; i++) a.r(10, 22 + i * 6, 18, 4, i % 2 ? 'copper' : 'gold');
    a.r(8, 18, 22, 4, 'iron'); a.r(8, 52, 22, 4, 'iron'); a.r(17, 10, 4, 8, 'steel');
    bolt(a, 30, 8, 1.8, 'cyan'); a.ring(44, 30, 17, 2, 'cyan'); a.dl(45, 12, 37, 33, '#ffffff');
    a.sparkle(54, 52, 'cyan'); a.sparkle(14, 8, 'yellow', 1);
  },
  braco_giratorio(a) {
    a.e(20, 50, 12, 5, 'iron'); a.r(14, 42, 12, 8, 'dark');
    a.t(20, 44, 24, 26, 3.5, 'orange'); a.c(20, 44, 5, 'steel'); a.c(24, 26, 5, 'steel');
    a.t(24, 26, 42, 16, 3, 'orange'); a.c(42, 16, 4.5, 'steel');
    a.p([[44, 12], [54, 6], [56, 10], [48, 16]], 'iron'); a.p([[46, 19], [56, 22], [55, 26], [45, 23]], 'iron');
    a.dc(20, 44, 1.5, 'dark', 1); a.dc(24, 26, 1.5, 'dark', 1); a.dc(42, 16, 1.2, 'dark', 1);
    for (let i = 0; i < 7; i++) { const ang = 3.6 + i * 0.32; a.dp(Math.round(24 + Math.cos(ang) * 26), Math.round(30 + Math.sin(ang) * 22), '#ffffff'); }
  },
  rotor_de_impacto(a) {
    for (const ang of [-Math.PI / 2, Math.PI / 6, (5 * Math.PI) / 6]) {
      const x = 32 + Math.cos(ang) * 17, y = 32 + Math.sin(ang) * 17;
      a.t(32, 32, x, y, 2.5, 'iron'); a.r(Math.round(x - 6), Math.round(y - 5), 12, 10, 'steel'); a.r(Math.round(x - 6), Math.round(y - 5), 12, 2, 'orange');
    }
    a.c(32, 32, 7, 'orange'); a.c(32, 32, 3, 'dark');
    for (const [x, y] of [[8, 20], [6, 30], [10, 40]]) a.dr(x, y, 6, 1, '#ffffff');
    a.dring(32, 32, 24, 'steel', 3);
  },
  laser_industrial(a) {
    a.r(6, 22, 26, 20, 'iron'); a.r(6, 22, 26, 4, 'steel'); a.r(10, 42, 6, 10, 'dark'); a.r(22, 42, 6, 10, 'dark'); a.r(4, 50, 28, 4, 'dark');
    a.r(32, 26, 8, 12, 'steel'); a.c(41, 32, 4, 'red');
    a.r(44, 29, 16, 6, 'red'); a.dr(44, 31, 16, 2, 'yellow', 4);
    for (let i = 0; i < 4; i++) a.dr(9 + i * 5, 30, 3, 8, 'dark', 1);
    a.dr(34, 18, 3, 3, 'orange', 3); a.sparkle(58, 24, 'orange', 1); a.sparkle(56, 42, 'yellow', 1);
  },
  campo_eletromagnetico(a) {
    a.r(24, 10, 16, 44, 'iron'); a.r(22, 8, 20, 4, 'steel'); a.r(22, 52, 20, 4, 'steel');
    for (let i = 0; i < 9; i++) a.r(20, 14 + i * 4, 24, 3, 'copper');
    for (const r of [26, 20]) { a.d.set(0, 0, null); for (let ang = -1.1; ang <= 1.1; ang += 0.04) for (const s of [-1, 1]) a.dp(Math.round(32 + s * Math.cos(ang) * r), Math.round(32 + Math.sin(ang) * r * 0.9), 'cyan', r > 22 ? 2 : 3); }
    a.sparkle(10, 12, 'cyan', 1); a.sparkle(54, 50, 'cyan', 1);
  },
  bolhas(a) {
    a.c(28, 32, 17, 'glass'); a.c(28, 32, 13, null);
    a.ring(28, 32, 17, 3, 'glass'); a.c(49, 18, 7, 'glass'); a.c(49, 18, 4, null); a.c(48, 46, 5, 'glass'); a.c(48, 46, 3, null);
    a.de(22, 24, 4, 3, '#ffffff'); a.dc(45, 15, 1.5, '#ffffff'); a.dc(46, 44, 1, '#ffffff');
    for (let ang = 0.3; ang < 1.6; ang += 0.08) a.dp(Math.round(28 + Math.cos(ang) * 14), Math.round(32 + Math.sin(ang) * 14), 'pink', 3);
    for (let ang = 3.4; ang < 4.2; ang += 0.08) a.dp(Math.round(28 + Math.cos(ang) * 14), Math.round(32 + Math.sin(ang) * 14), 'mint', 3);
  },
  bolhas_e_maletas(a) {
    a.ring(24, 26, 17, 3, 'glass'); a.de(17, 17, 4, 3, '#ffffff');
    a.r(30, 34, 26, 18, 'brown'); a.r(38, 29, 10, 5, 'wood'); a.cut(41, 31, 4, 3);
    a.r(30, 41, 26, 3, 'wood'); goldTrim(a, 41, 40, 4); a.r(41, 40, 4, 4, 'gold');
    a.c(52, 14, 5, 'glass'); a.c(52, 14, 2.5, null); a.dc(51, 12, 1, '#ffffff');
    for (let ang = 0.3; ang < 1.5; ang += 0.08) a.dp(Math.round(24 + Math.cos(ang) * 14), Math.round(26 + Math.sin(ang) * 14), 'pink', 3);
  },
  calculos(a) {
    a.p([[14, 6], [46, 6], [50, 10], [50, 56], [46, 60], [14, 60], [10, 56], [10, 10]], 'navy');
    a.r(15, 11, 30, 13, 'pcb'); a.txt('3', 18, 15, 'lime', 4); a.dp(22, 19, 'lime', 4); a.txt('14', 24, 15, 'lime', 4);
    for (let r = 0; r < 4; r++) for (let c = 0; c < 4; c++) a.r(15 + c * 8, 28 + r * 8, 6, 5, c === 3 ? 'orange' : 'steel');
    a.dr(16, 29, 4, 1, '#ffffff'); a.txt('=', 40, 52, 'dark', 1);
  },
  matematica_sushi(a) {
    a.p([[8, 4], [34, 4], [38, 8], [38, 44], [8, 44], [4, 40], [4, 8]], 'navy');
    a.r(8, 8, 26, 10, 'pcb'); a.txt('314', 12, 11, 'lime', 4);
    for (let r = 0; r < 3; r++) for (let c = 0; c < 3; c++) a.r(9 + c * 9, 22 + r * 7, 7, 5, c === 2 ? 'orange' : 'steel');
    a.e(42, 50, 17, 6, 'rice'); a.p([[24, 46], [34, 36], [60, 38], [60, 46], [36, 50]], 'salmon');
    for (let i = 0; i < 4; i++) a.dl(32 + i * 6, 39, 36 + i * 6, 47, 'salmon', 4);
    a.r(38, 44, 6, 10, 'black'); goldTrim(a, 4, 2, 34); a.sparkle(56, 22, 'yellow');
  },
  componentes_eletricos(a) {
    a.t(4, 26, 14, 26, 1, 'steel'); a.t(42, 26, 52, 26, 1, 'steel');
    a.e(28, 26, 15, 7, 'cream'); for (const [x, k] of [[19, 'brown'], [24, 'red'], [29, 'orange'], [36, 'gold']]) a.r(x, 20, 3, 13, k);
    for (const x of [30, 36, 42]) a.t(x, 48, x, 60, 1, 'steel');
    a.p([[26, 34], [46, 34], [46, 48], [26, 48]], 'black'); a.e(36, 34, 10, 4, 'black');
    a.dr(29, 40, 10, 1, 'steel', 3); a.dr(29, 43, 6, 1, 'steel', 3);
  },
  fontes_osciloscopios(a) {
    a.r(4, 10, 56, 42, 'steel'); a.r(4, 10, 56, 4, 'white'); a.r(8, 16, 32, 30, 'black'); a.r(10, 18, 28, 26, 'pcb');
    for (let x = 0; x < 28; x++) a.dp(10 + x, Math.round(31 + Math.sin(x / 3) * 7), 'lime', 4);
    for (let x = 10; x < 38; x += 7) for (let y = 18; y < 44; y += 2) a.dp(x, y, 'pcb', 3);
    for (const [y, k] of [[20, 'red'], [32, 'yellow'], [44, 'cyan']]) { a.c(50, y, 4, 'iron'); a.dc(50, y - 2, 1, k, 3); }
    a.r(8, 52, 8, 6, 'dark'); a.r(48, 52, 8, 6, 'dark'); goldTrim(a, 4, 10, 56); a.sparkle(58, 6, 'lime', 1);
  },
  lanches(a) {
    a.e(26, 24, 18, 10, 'bread'); a.r(8, 30, 36, 4, 'lettuce'); for (let x = 8; x < 44; x += 4) a.c(x + 2, 34, 2, 'lettuce');
    a.r(9, 33, 34, 7, 'meat'); a.p([[8, 32], [44, 32], [40, 38], [30, 34], [20, 38]], 'cheese'); a.e(26, 43, 18, 5, 'bread');
    for (const [x, y] of [[20, 18], [27, 16], [32, 21], [22, 24]]) a.dr(x, y, 2, 1, 'cream', 4);
    a.p([[38, 56], [62, 40], [58, 62]], 'orange'); for (const [x, y] of [[50, 52], [56, 56], [55, 47]]) a.dc(x, y, 1, 'red', 2);
  },
  pneus_crossfit(a) {
    a.e(32, 32, 26, 24, 'rubber'); a.e(32, 32, 13, 11, null); a.ring(32, 32, 14, 2, 'iron');
    for (let i = 0; i < 16; i++) { const ang = (i * Math.PI) / 8; a.dl(32 + Math.cos(ang) * 18, 32 + Math.sin(ang) * 16.5, 32 + Math.cos(ang) * 24, 32 + Math.sin(ang) * 22.5, 'rubber', 1); }
    a.de(22, 16, 6, 2, 'rubber', 4); goldTrim(a, 20, 52, 24); a.sparkle(56, 8, 'yellow');
    a.dr(4, 30, 4, 4, '#ffffff'); a.dr(57, 44, 3, 3, '#ffffff');
  },
  bonesmashing(a) {
    a.t(14, 50, 46, 16, 4, 'bone');
    for (const [x, y] of [[10, 48], [16, 54], [42, 12], [48, 18]]) a.c(x, y, 5.5, 'bone');
    a.dl(18, 44, 40, 20, 'bone', 4);
    a.p(Array.from({ length: 16 }, (_, i) => [48 + Math.cos((i * Math.PI) / 8) * (i % 2 ? 5 : 12), 44 + Math.sin((i * Math.PI) / 8) * (i % 2 ? 5 : 12)]), 'yellow');
    a.c(48, 44, 4, 'white');
    a.dl(26, 30, 30, 36, 'bone', 0); a.dl(30, 36, 28, 40, 'bone', 0);
  },
  pilulas_seringas(a) {
    a.t(10, 52, 18, 44, 1, 'steel'); a.t(16, 46, 40, 22, 5, 'glass'); a.t(20, 42, 34, 28, 3, 'red');
    a.t(40, 22, 48, 14, 2.5, 'steel'); a.t(46, 10, 54, 18, 2, 'iron'); a.t(14, 40, 22, 48, 1.5, 'iron');
    for (let i = 0; i < 4; i++) a.dl(22 + i * 4, 36 - i * 4, 24 + i * 4, 38 - i * 4, 'glass', 0);
    a.t(36, 50, 44, 42, 5, 'red'); a.t(44, 42, 52, 34, 5, 'white'); a.dl(36, 47, 42, 41, '#ffffff');
    goldTrim(a, 44, 52, 14); a.sparkle(56, 52, 'yellow', 1);
  },
  cabazords(a) {
    const bot = (x, y, k) => { a.r(x, y + 6, 16, 14, k); a.r(x + 2, y + 9, 12, 6, 'dark'); a.dr(x + 4, y + 11, 2, 2, 'cyan', 4); a.dr(x + 10, y + 11, 2, 2, 'cyan', 4); a.r(x + 7, y, 2, 6, 'steel'); a.c(x + 8, y, 2, 'red'); a.r(x + 2, y + 20, 4, 4, 'iron'); a.r(x + 10, y + 20, 4, 4, 'iron'); };
    bot(4, 26, 'blue'); bot(44, 26, 'yellow'); bot(24, 12, 'red');
  },
  cabazord_master(a) {
    a.r(14, 42, 10, 16, 'iron'); a.r(40, 42, 10, 16, 'iron'); a.r(12, 54, 14, 6, 'dark'); a.r(38, 54, 14, 6, 'dark');
    a.r(10, 18, 44, 26, 'steel'); goldTrim(a, 10, 18, 44);
    a.r(14, 24, 11, 11, 'blue'); a.r(27, 24, 10, 11, 'red'); a.r(39, 24, 11, 11, 'yellow');
    a.r(22, 6, 20, 12, 'iron'); a.r(25, 9, 14, 5, 'dark'); a.dr(27, 10, 3, 2, 'cyan', 4); a.dr(34, 10, 3, 2, 'cyan', 4);
    a.r(2, 20, 8, 18, 'iron'); a.r(54, 20, 8, 18, 'iron'); a.r(30, 2, 4, 4, 'gold'); a.sparkle(58, 8, 'yellow');
    a.dr(16, 38, 32, 2, 'cyan', 3);
  },
  celulares_cola(a) {
    a.p([[30, 14], [56, 10], [58, 50], [34, 54]], 'paper'); for (let i = 0; i < 6; i++) a.dl(34, 20 + i * 5, 54, 17 + i * 5, 'blue', 2);
    a.dl(36, 47, 46, 45, 'red', 2);
    a.r(8, 6, 24, 44, 'black'); a.r(10, 10, 20, 34, 'cyan'); a.dr(18, 7, 6, 1, 'dark', 3); a.dc(20, 47, 1.5, 'dark', 3);
    for (let i = 0; i < 4; i++) a.dr(12, 14 + i * 7, 10 + (i % 2) * 5, 3, 'white', 4);
  },
  computadores_tvs(a) {
    a.r(4, 6, 38, 30, 'steel'); a.r(8, 10, 30, 22, 'blue'); for (let y = 10; y < 32; y += 3) a.dr(8, y, 30, 1, 'blue', 3);
    a.dr(12, 14, 8, 2, 'white', 4); a.dr(12, 19, 14, 2, 'white', 4); a.r(18, 36, 10, 6, 'iron'); a.r(12, 42, 22, 4, 'dark');
    a.r(30, 30, 30, 24, 'iron'); a.r(33, 33, 18, 16, 'pcb'); a.dr(35, 36, 10, 2, 'lime', 4); a.dc(55, 38, 2, 'dark', 3); a.dc(55, 45, 2, 'dark', 3);
    a.r(34, 54, 4, 4, 'dark'); a.r(52, 54, 4, 4, 'dark'); a.dl(42, 26, 46, 30, 'steel', 3); a.dl(50, 26, 46, 30, 'steel', 3);
    goldTrim(a, 4, 6, 38); a.sparkle(58, 12, 'yellow', 1);
  },
  jabutis(a) {
    a.c(52, 36, 7, 'olive'); a.dc(54, 34, 1.2, 'black'); a.dl(55, 39, 58, 38, 'black');
    for (const x of [16, 40]) a.r(x - 3, 42, 8, 12, 'olive');
    a.e(30, 36, 22, 16, 'shell'); a.r(8, 38, 44, 6, 'cream');
    for (const [x, y] of [[22, 28], [32, 25], [42, 30], [28, 36], [38, 37], [18, 37]]) { a.de(x, y, 4, 3, 'shell', 1); a.de(x, y - 1, 3, 1.5, 'shell', 3); }
    a.t(6, 38, 2, 42, 1.5, 'olive');
  },
  carro_drift(a) {
    for (const [x, y, r] of [[8, 46, 6], [4, 36, 4], [12, 32, 3]]) a.c(x, y, r, 'white');
    a.p([[14, 34], [22, 22], [44, 22], [52, 32], [60, 34], [60, 46], [14, 46]], 'red'); goldTrim(a, 14, 38, 46);
    a.p([[24, 25], [32, 25], [32, 32], [20, 32]], 'glass'); a.p([[35, 25], [43, 25], [48, 32], [35, 32]], 'glass');
    a.c(24, 47, 6, 'rubber'); a.c(50, 47, 6, 'rubber'); a.dc(24, 47, 2.5, 'steel', 3); a.dc(50, 47, 2.5, 'steel', 3);
    a.dr(56, 36, 4, 2, 'yellow', 4); speed(a, 4, 18, 3, 8);
  },
  socos_fortes(a) {
    a.r(28, 44, 16, 14, 'lime'); a.dr(28, 47, 16, 2, 'lime', 4);
    a.e(36, 30, 16, 15, 'skin');
    for (let i = 0; i < 4; i++) { a.dl(24 + i * 7, 18, 24 + i * 7, 28, 'skin', 1); }
    a.e(22, 34, 6, 5, 'skin'); a.dl(20, 33, 26, 31, 'skin', 1);
    for (const [x0, y0, x1, y1] of [[6, 14, 14, 20], [4, 28, 12, 28], [8, 42, 14, 38]]) a.t(x0, y0, x1, y1, 1, 'yellow');
  },
  socos_insulina(a) {
    a.e(22, 36, 14, 13, 'skin'); for (let i = 0; i < 3; i++) a.dl(14 + i * 7, 26, 14 + i * 7, 34, 'skin', 1); a.r(12, 46, 18, 12, 'lime');
    a.t(34, 50, 54, 14, 4, 'white'); a.t(46, 28, 52, 18, 4, 'blue'); a.t(54, 14, 58, 8, 1, 'steel'); a.r(36, 46, 6, 4, 'orange');
    a.dl(38, 45, 50, 23, 'white', 4); a.dr(42, 34, 4, 2, 'blue', 4); a.txt('0', 49, 23, 'black', 2);
    goldTrim(a, 12, 46, 18); a.sparkle(8, 10, 'yellow');
  },
  fala_alta(a) {
    a.e(26, 32, 20, 17, 'red'); a.e(26, 33, 15, 11, 'black'); a.r(13, 23, 26, 5, 'white'); a.e(26, 40, 9, 4, 'pink');
    a.dr(18, 23, 1, 5, 'white', 1); a.dr(26, 23, 1, 5, 'white', 1); a.dr(33, 23, 1, 5, 'white', 1);
    for (const r of [8, 13, 18]) for (let ang = -0.8; ang <= 0.8; ang += 0.05) a.dp(Math.round(44 + Math.cos(ang) * r), Math.round(32 + Math.sin(ang) * r), 'yellow', 3);
  },
  fala_amplificada(a) {
    a.e(22, 26, 16, 14, 'red'); a.e(22, 27, 12, 9, 'black'); a.r(12, 19, 20, 4, 'white'); a.e(22, 33, 7, 3, 'pink');
    for (const r of [7, 11]) for (let ang = -0.7; ang <= 0.7; ang += 0.06) a.dp(Math.round(38 + Math.cos(ang) * r), Math.round(26 + Math.sin(ang) * r), 'yellow', 3);
    a.r(46, 30, 6, 8, 'bottle'); a.p([[42, 38], [56, 38], [58, 44], [58, 60], [40, 60], [40, 44]], 'bottle');
    a.r(42, 46, 16, 8, 'cream'); a.dl(44, 49, 54, 49, 'red', 2); a.r(46, 28, 6, 3, 'gold'); a.dl(43, 42, 43, 58, 'bottle', 4);
  },
  // componentes iniciais
  bateria_portatil(a) {
    a.r(14, 12, 36, 46, 'green'); a.r(24, 6, 16, 6, 'steel'); a.r(14, 12, 36, 6, 'steel'); a.r(14, 52, 36, 6, 'steel');
    bolt(a, 24, 22, 1.2, 'yellow'); a.dl(32, 23, 27, 37, '#ffffff');
    a.dr(18, 20, 3, 30, 'green', 4);
  },
  turbina(a) {
    a.c(32, 32, 26, 'steel'); a.c(32, 32, 21, 'dark');
    for (let i = 0; i < 9; i++) { const ang = (i * 2 * Math.PI) / 9; a.p([[32 + Math.cos(ang) * 6, 32 + Math.sin(ang) * 6], [32 + Math.cos(ang + 0.2) * 20, 32 + Math.sin(ang + 0.2) * 20], [32 + Math.cos(ang + 0.55) * 20, 32 + Math.sin(ang + 0.55) * 20]], 'iron'); }
    a.c(32, 32, 6, 'orange'); a.dc(30, 30, 1.5, 'orange', 4);
    speed(a, 2, 8, 2, 6);
  },
};

// ——— itens (componentes com evolução) ———
function tequila(a, evo) {
  a.r(27, 3, 10, 7, evo ? 'gold' : 'wood'); a.r(28, 9, 8, 10, 'tequila');
  a.p([[28, 18], [36, 18], [46, 26], [46, 58], [18, 58], [18, 26]], 'tequila');
  a.r(20, 32, 24, 17, 'red'); a.r(20, 32, 24, 3, evo ? 'gold' : 'cream'); a.r(20, 46, 24, 3, evo ? 'gold' : 'cream');
  for (const [x, h] of [[27, 6], [30, 9], [32, 10], [34, 9], [37, 6]]) a.dl(32, 44, x, 44 - h, 'leaf', 3);
  a.dr(22, 22, 2, 34, '#fff6d8');
  if (evo) { for (const [x, y, k] of [[6, 10, 'magenta'], [54, 8, 'cyan'], [56, 40, 'yellow'], [6, 46, 'green'], [10, 28, 'orange'], [55, 54, 'magenta']]) { a.r(x, y, 3, 3, k); } a.sparkle(52, 22, 'yellow'); }
}
function iogurte(a, evo) {
  a.p([[15, 18], [49, 18], [45, 58], [19, 58]], 'white'); a.r(12, 11, 40, 8, 'pink'); a.p([[46, 11], [55, 5], [58, 9], [50, 13]], 'pink');
  a.p([[16, 27], [48, 27], [47, 44], [17, 44]], evo ? 'gold' : 'sky');
  a.txt('80', 25, 32, 'navy', 1); a.dr(25, 32, 1, 0, 'navy');
  for (const [x, y] of [[18, 22], [40, 50]]) a.dc(x, y, 1.5, 'pink', 3);
  a.dr(19, 20, 2, 36, '#ffffff');
  if (evo) { for (const [x, y] of [[7, 14], [57, 26], [8, 44], [56, 52]]) a.sparkle(x, y, 'yellow'); a.dr(26, 40, 14, 1, 'gold', 4); }
}
function cenoura(a, evo) {
  if (evo) for (const [x, y, r] of [[16, 18, 10], [46, 42, 11], [20, 46, 8], [48, 14, 7]]) a.c(x, y, r, 'lime');
  a.path(Array.from({ length: 9 }, (_, i) => [12 + i * 3.5, 54 - i * 3.8]), 1, 'carrot');
  for (let i = 0; i <= 30; i++) { const t = i / 30; a.c(12 + t * 29, 54 - t * 31, 1.5 + t * 7, 'carrot'); }
  for (const t of [0.35, 0.55, 0.75]) { const x = 12 + t * 29, y = 54 - t * 31, r = 1.5 + t * 7; a.dl(x - r * 0.7, y - r * 0.6, x - r * 0.1, y, 'carrot', 1); }
  for (const [x, y, r] of [[26, 40, 2.5], [35, 31, 3], [38, 23, 2], [20, 46, 1.5]]) a.dc(x, y, r, 'brown', 1);
  for (const [x1, y1] of [[44, 4], [52, 8], [58, 16], [40, 6]]) a.t(42, 22, x1, y1, 1.6, 'leaf');
  for (const [x, y] of [[8, 22], [14, 12], [54, 40], [48, 52]]) { a.c(x, y, 3, 'lime'); a.dp(x - 1, y - 1, 'lime', 4); }
  if (evo) { a.sparkle(56, 28, 'lime'); a.dr(4, 34, 4, 1, 'lime', 4); }
}
function motor(a, evo) {
  a.r(10, 24, 42, 26, 'iron');
  for (let i = 0; i < 4; i++) { a.r(13 + i * 9, 12, 8, 13, 'steel'); a.dc(17 + i * 9, 18, 1.5, 'iron', 1); }
  a.r(16, 27, 30, 9, evo ? 'gold' : 'red'); for (let i = 0; i < 5; i++) a.dr(19 + i * 5, 31, 3, 2, evo ? 'gold' : 'red', 1);
  a.c(11, 42, 7, 'dark'); a.c(11, 42, 3, 'steel');
  for (let i = 0; i < 4; i++) a.dc(24 + i * 7, 43, 2, 'steel', 3);
  a.r(50, 34, 8, 6, 'steel'); a.r(55, 28, 5, 18, 'dark');
  a.r(14, 50, 6, 7, 'dark'); a.r(42, 50, 6, 7, 'dark');
  if (evo) { flame(a, 58, 26, 0.9); a.ring(32, 34, 29, 2, 'yellow'); a.dr(22, 38, 18, 2, 'cyan', 4); }
}
function dog(a, turbo) {
  const body = turbo ? 'red' : 'copper';
  for (const [x, y] of [[14, 44], [22, 46], [36, 44], [44, 46]]) { a.r(x, y, 5, 56 - y, 'iron'); a.r(x - 1, 54, 7, 4, 'dark'); }
  a.r(9, 28, 40, 18, body); a.r(19, 32, 16, 9, 'steel'); for (let i = 0; i < 3; i++) a.dr(21 + i * 5, 34, 3, 5, 'iron', 1);
  a.r(40, 13, 18, 19, body); a.r(53, 22, 9, 8, body); a.dr(60, 23, 2, 2, 'black');
  a.r(42, 6, 6, 8, turbo ? 'crimson' : 'brown'); a.dc(50, 20, 2.5, 'yellow', 4); a.dp(50, 20, 'black');
  a.dr(53, 28, 6, 1, '#ffffff'); a.r(1, 24, 10, 6, 'steel');
  if (turbo) { goldTrim(a, 9, 28, 40); flame(a, 4, 18, 1); flame(a, 10, 13, 0.7); speed(a, 1, 36, 3, 6, 'yellow'); }
  else for (const [x, y, r] of [[5, 18, 3], [9, 12, 2.5], [4, 7, 2]]) a.c(x, y, r, 'steel');
}
function camiseta(a, evo) {
  a.p([[20, 8], [27, 8], [32, 13], [37, 8], [44, 8], [59, 19], [52, 30], [46, 26], [46, 58], [18, 58], [18, 26], [12, 30], [5, 19]], evo ? 'purple' : 'sky');
  a.r(18, 52, 28, 6, evo ? 'magenta' : 'denim');
  a.r(28, 15, 8, 7, 'white'); a.dr(29, 17, 6, 1, 'steel', 1); a.dr(29, 19, 4, 1, 'steel', 1);
  for (const x of [19, 45]) for (let y = 30; y < 52; y += 3) a.dp(x, y, '#ffffff');
  a.txt('+', 30, 30, 'white', 4); a.dl(32, 36, 32, 44, 'white', 4); a.dl(30, 42, 32, 44, 'white', 4); a.dl(34, 42, 32, 44, 'white', 4);
  if (evo) { a.ring(32, 32, 29, 2, 'cyan'); for (const [x, y] of [[8, 46], [56, 46], [10, 6]]) a.sparkle(x, y, 'cyan'); }
}
function mamao(a, evo) {
  if (evo) a.ring(32, 34, 29, 2, 'green');
  a.e(32, 36, 18, 21, 'papaya'); a.r(30, 8, 5, 9, 'leaf'); a.e(42, 11, 8, 4, 'green'); a.dl(36, 11, 48, 11, 'green', 1);
  a.dc(25, 36, 2.5, 'black'); a.dc(39, 36, 2.5, 'black'); a.dp(24, 35, '#ffffff'); a.dp(38, 35, '#ffffff');
  a.de(20, 42, 3, 2, 'pink', 3); a.de(44, 42, 3, 2, 'pink', 3); a.dl(29, 43, 31, 45, 'black'); a.dl(32, 45, 35, 43, 'black');
  if (evo) { a.p([[22, 10], [26, 2], [29, 8], [32, 0], [35, 8], [38, 2], [42, 10]], 'gold'); a.dc(32, 6, 1.5, 'red', 3); a.sparkle(54, 50, 'green'); }
}
function protoboard(a, exp) {
  a.r(4, 14, 56, 38, 'white'); a.dr(7, 18, 50, 1, 'red'); a.dr(7, 48, 50, 1, 'blue'); a.dr(5, 32, 54, 2, 'steel', 1);
  for (let y = 21; y < 46; y += 4) { if (y > 30 && y < 35) continue; for (let x = 8; x < 57; x += 4) a.dp(x, y, 'steel', 1); }
  a.r(26, 26, 18, 13, 'black'); for (let i = 0; i < 5; i++) { a.dr(28 + i * 3, 24, 1, 2, 'steel', 3); a.dr(28 + i * 3, 39, 1, 2, 'steel', 3); }
  a.r(8, 24, 12, 4, 'cream'); for (const [x, k] of [[10, 'brown'], [13, 'red'], [16, 'gold']]) a.dr(x, 24, 1, 4, k);
  a.c(51, 24, 3.5, exp ? 'cyan' : 'red'); a.dr(49, 28, 1, 4, 'steel', 3); a.dr(53, 28, 1, 4, 'steel', 3);
  a.path([[8, 44], [14, 40], [22, 42], [26, 38]], 0.8, 'green'); a.path([[56, 44], [50, 42], [44, 37]], 0.8, 'blue'); a.path([[22, 20], [30, 20], [34, 25]], 0.8, 'red');
  if (exp) { goldTrim(a, 4, 14, 56); for (const [x0, y0, x1, y1] of [[6, 6, 20, 22], [58, 6, 46, 26], [32, 2, 34, 24]]) { a.dl(x0, y0, x1, y1, 'cyan', 3); a.dl(x0 + 1, y0, x1 + 1, y1, '#ffffff'); } for (const [x, y] of [[6, 6], [58, 6], [32, 3]]) a.sparkle(x, y, 'cyan', 1); }
}
function chapeu(a, evo) {
  a.p([[32, 9], [60, 40], [4, 40]], 'straw');
  for (const k of [-0.75, -0.4, 0, 0.4, 0.75]) a.dl(32, 10, 32 + k * 27, 39, 'straw', 1);
  for (let y = 16; y < 40; y += 5) { const hw = ((y - 9) / 31) * 28; a.dl(32 - hw, y, 32 + hw, y, 'straw', 3); }
  a.r(4, 40, 56, 3, 'wood'); a.c(32, 10, 2.5, evo ? 'gold' : 'brass');
  a.t(18, 43, 26, 56, 1, 'red'); a.t(46, 43, 38, 56, 1, 'red'); a.c(32, 57, 2.5, 'crimson');
  if (evo) for (const y of [48, 56]) { a.t(2, y, 46, y, 0.8, 'cyan'); a.p([[46, y - 4], [56, y], [46, y + 4]], 'white'); }
}

const ITEMS = {
  tequila: (a) => tequila(a, false), fiesta_caotica: (a) => tequila(a, true),
  iogurte_80: (a) => iogurte(a, false), dieta_milagrosa: (a) => iogurte(a, true),
  cenoura_podre: (a) => cenoura(a, false), hortalica_desgraca: (a) => cenoura(a, true),
  motor_astra: (a) => motor(a, false), astra_alta_rotacao: (a) => motor(a, true),
  cachorro_quente: (a) => dog(a, false), cao_turbo: (a) => dog(a, true),
  camiseta_contrario: (a) => camiseta(a, false), moda_incompreensivel: (a) => camiseta(a, true),
  mamao: (a) => mamao(a, false), mamao_supremo: (a) => mamao(a, true),
  protoboard: (a) => protoboard(a, false), protoboard_experimental: (a) => protoboard(a, true),
  chapeu_japones: (a) => chapeu(a, false), mestre_projeteis: (a) => chapeu(a, true),
  cartao_corporativo(a) {
    a.p([[6, 16], [58, 12], [60, 48], [8, 52]], 'gold'); a.p([[7, 22], [58, 18], [58, 22], [7, 26]], 'brass');
    a.r(13, 30, 11, 9, 'brass'); a.dl(14, 34, 23, 34, 'brass', 1); a.dl(18, 31, 18, 38, 'brass', 1);
    for (let i = 0; i < 4; i++) a.dr(12 + i * 11, 42 - i, 8, 2, 'gold', 1);
    a.c(44, 33, 5, 'red'); a.c(51, 33, 5, 'amber'); a.dr(30, 30, 1, 6, 'gold', 4); a.sparkle(56, 8, 'yellow', 1);
  },
  cafe_expresso(a) {
    a.e(32, 54, 26, 6, 'white'); a.p([[16, 26], [48, 26], [44, 51], [20, 51]], 'white'); a.ring(50, 37, 6, 2, 'white');
    a.e(32, 27, 15, 4, 'brown'); a.de(31, 27, 10, 2, 'cream', 1); a.dc(28, 26, 1, 'cream', 3);
    a.dr(20, 30, 2, 18, '#ffffff');
    for (const x of [24, 32, 40]) for (let y = 4; y < 21; y++) a.dp(Math.round(x + Math.sin(y / 2.5 + x) * 2), y, y % 3 ? 'steel' : 'white', 3);
  },
  halter(a) {
    a.r(12, 29, 40, 6, 'steel');
    for (const x of [6, 44]) { a.r(x + 6 * (x > 30 ? 0 : 1), 14, 8, 36, 'dark'); a.r(x + (x > 30 ? 8 : -2), 20, 8, 24, 'iron'); }
    for (let x = 24; x < 41; x += 3) a.dr(x, 31, 1, 3, 'steel', 1);
  },
  marmita(a) {
    a.r(4, 18, 56, 34, 'steel'); a.r(8, 22, 48, 13, 'rice'); for (let i = 0; i < 40; i++) a.dp(10 + ((i * 7) % 45), 24 + ((i * 5) % 9), 'rice', 1);
    a.r(8, 37, 22, 12, 'meat'); for (let i = 0; i < 16; i++) a.dc(11 + ((i * 5) % 17), 40 + ((i * 3) % 7), 1, 'brown', 1);
    a.r(32, 37, 24, 12, 'lettuce'); for (const [x, y] of [[36, 41], [43, 45], [50, 40]]) a.de(x, y, 3, 1.5, 'lime', 4);
    a.c(48, 27, 4, 'red'); a.r(26, 10, 12, 9, 'iron');
  },
  pote_whey(a) {
    a.r(12, 18, 40, 40, 'black'); a.r(10, 8, 44, 11, 'steel'); for (let x = 13; x < 52; x += 4) a.dr(x, 12, 2, 6, 'steel', 1);
    a.r(16, 27, 32, 20, 'red'); a.dr(16, 27, 32, 2, 'red', 4);
    for (const [i, x] of [[0, 21], [1, 29], [2, 37]]) { void i; a.dl(x, 31, x + 2, 39, '#ffffff'); a.dl(x + 2, 39, x + 4, 33, '#ffffff'); a.dl(x + 4, 33, x + 6, 39, '#ffffff'); }
    a.dr(19, 42, 26, 2, '#ffffff'); a.dr(14, 22, 2, 32, 'black', 3);
  },
  kit_arduino(a) {
    a.r(4, 12, 56, 40, 'teal'); a.r(2, 18, 12, 10, 'steel'); a.r(2, 36, 10, 10, 'black');
    for (let i = 0; i < 12; i++) { a.dr(18 + i * 3, 14, 2, 4, 'black'); a.dr(18 + i * 3, 46, 2, 4, 'black'); }
    a.r(24, 26, 22, 12, 'black'); for (let i = 0; i < 7; i++) { a.dp(26 + i * 3, 25, 'steel', 4); a.dp(26 + i * 3, 38, 'steel', 4); }
    a.r(50, 22, 6, 6, 'steel'); a.dc(19, 33, 1.5, 'green', 4); a.dc(19, 39, 1.5, 'amber', 4); a.dc(52, 40, 1.5, 'red', 4);
    a.dr(28, 41, 14, 1, 'teal', 4);
  },
  cabo_hdmi(a) {
    const pts = []; for (let x = 30; x <= 62; x += 2) pts.push([x, 32 + Math.sin((x - 30) / 5) * 8]);
    a.path(pts, 2.5, 'black');
    a.r(4, 20, 22, 22, 'iron'); a.p([[8, 26], [22, 26], [22, 33], [19, 36], [11, 36], [8, 33]], 'black');
    for (let x = 10; x < 21; x += 2) a.dr(x, 28, 1, 2, 'gold', 4); a.r(26, 26, 7, 10, 'dark');
  },
  megafone(a) {
    a.p([[14, 24], [44, 8], [48, 8], [48, 50], [44, 50], [14, 36]], 'orange'); a.r(44, 8, 4, 42, 'white');
    a.r(6, 22, 10, 16, 'steel'); a.r(12, 36, 7, 15, 'dark');
    for (const [x0, y0, x1, y1] of [[52, 16, 58, 10], [53, 29, 61, 29], [52, 42, 58, 48]]) a.t(x0, y0, x1, y1, 1, 'yellow');
    a.dl(18, 28, 42, 15, 'orange', 4);
  },
};

// ——— atributos, sobrevivência e especiais ———
const A = {
  attr_dano(a) {
    a.t(14, 50, 44, 16, 3, 'steel'); a.p([[44, 12], [52, 10], [50, 18]], 'steel'); a.dl(17, 46, 44, 15, '#ffffff');
    a.t(10, 40, 22, 52, 2.5, 'gold'); a.t(10, 54, 4, 60, 3, 'wood'); a.c(4, 60, 3, 'gold');
    for (const [x, y] of [[40, 40], [50, 30], [30, 8]]) a.sparkle(x, y, 'red');
  },
  attr_vida(a) {
    a.c(22, 24, 11, 'red'); a.c(42, 24, 11, 'red'); a.p([[11, 27], [53, 27], [32, 54]], 'red');
    a.de(18, 20, 4, 3, 'red', 4);
    const pts = [[8, 34], [20, 34], [24, 26], [30, 42], [35, 30], [38, 34], [56, 34]];
    for (let i = 0; i + 1 < pts.length; i++) { a.dl(...pts[i], ...pts[i + 1], '#ffffff'); a.dl(pts[i][0], pts[i][1] + 1, pts[i + 1][0], pts[i + 1][1] + 1, 'white', 3); }
  },
  attr_recuperacao(a) {
    a.r(25, 10, 14, 44, 'green'); a.r(10, 25, 44, 14, 'green');
    for (let ang = 0; ang < 5.2; ang += 0.05) a.dp(Math.round(32 + Math.cos(ang) * 26), Math.round(32 + Math.sin(ang) * 26), 'mint', 3);
    a.dl(56, 22, 58, 30, 'mint', 3); a.dl(58, 30, 52, 27, 'mint', 3);
    a.dr(27, 12, 2, 12, 'green', 4);
  },
  attr_coleta(a) {
    a.path([[14, 16], [14, 36], [20, 46], [32, 50], [44, 46], [50, 36], [50, 16]], 6, 'red');
    a.r(8, 8, 13, 10, 'steel'); a.r(43, 8, 13, 10, 'steel');
    a.p([[32, 18], [40, 26], [32, 38], [24, 26]], 'cyan'); a.dl(28, 24, 32, 20, '#ffffff');
    for (const [x, y] of [[32, 6], [24, 12], [40, 12]]) a.dp(x, y, 'cyan', 4);
  },
  attr_vel_ataque(a) {
    a.c(36, 36, 20, 'white'); a.c(36, 36, 16, 'cream'); a.r(32, 8, 8, 6, 'steel'); a.r(30, 4, 12, 4, 'red');
    a.t(52, 18, 56, 14, 2, 'steel');
    a.dl(36, 36, 36, 24, 'black'); a.dl(36, 36, 45, 41, 'red', 2); a.dc(36, 36, 1.5, 'black');
    for (let i = 0; i < 12; i++) { const ang = (i * Math.PI) / 6; a.dp(Math.round(36 + Math.cos(ang) * 13), Math.round(36 + Math.sin(ang) * 13), 'dark', 2); }
    speed(a, 4, 26, 3, 10, 'amber');
  },
  kit_primeiros_socorros(a) {
    a.r(6, 18, 52, 38, 'white'); a.r(22, 10, 20, 9, 'steel'); a.cut(26, 12, 12, 7);
    a.r(28, 24, 8, 26, 'red'); a.r(19, 33, 26, 8, 'red'); a.r(6, 50, 52, 6, 'paper');
    a.dr(10, 22, 2, 26, '#ffffff');
  },
  apagao(a) {
    a.c(32, 26, 18, 'dark'); a.p([[22, 38], [42, 38], [38, 46], [26, 46]], 'dark'); a.r(24, 46, 16, 10, 'steel');
    for (let y = 48; y < 56; y += 3) a.dr(24, y, 16, 1, 'iron', 2);
    a.dl(26, 32, 30, 24, 'iron', 3); a.dl(30, 24, 34, 32, 'iron', 3); a.dl(34, 32, 38, 26, 'iron', 3);
    a.de(24, 18, 4, 5, 'dark', 3);
    bolt(a, 44, 4, 1, 'yellow'); a.sparkle(10, 12, 'cyan', 1);
  },
  turbina_emergencia(a) {
    a.c(32, 28, 24, 'steel'); a.c(32, 28, 19, 'dark');
    for (let i = 0; i < 6; i++) { const ang = (i * Math.PI) / 3; a.p([[32 + Math.cos(ang) * 5, 28 + Math.sin(ang) * 5], [32 + Math.cos(ang + 0.25) * 18, 28 + Math.sin(ang + 0.25) * 18], [32 + Math.cos(ang + 0.75) * 18, 28 + Math.sin(ang + 0.75) * 18]], 'iron'); }
    a.c(32, 28, 5, 'red');
    for (let x = 8; x < 58; x += 8) a.p([[x, 50], [x + 4, 50], [x + 8, 58], [x + 4, 58]], 'yellow');
    a.r(8, 50, 50, 8, null); for (let x = 8; x < 56; x += 8) { a.p([[x, 50], [x + 4, 50], [x + 8, 58], [x + 4, 58]], 'yellow'); a.p([[x + 4, 50], [x + 8, 50], [x + 12, 58], [x + 8, 58]], 'black'); }
    flame(a, 12, 10, 0.7); flame(a, 52, 10, 0.7);
  },
  overclock(a) {
    a.r(12, 12, 40, 40, 'dark');
    for (let i = 0; i < 9; i++) { a.r(15 + i * 4, 6, 2, 6, 'gold'); a.r(15 + i * 4, 52, 2, 6, 'gold'); a.r(6, 15 + i * 4, 6, 2, 'gold'); a.r(52, 15 + i * 4, 6, 2, 'gold'); }
    a.r(17, 17, 30, 30, 'pcb'); for (let i = 0; i < 5; i++) a.dl(18, 22 + i * 5, 24, 22 + i * 5, 'pcb', 1);
    bolt(a, 24, 19, 1.1, 'yellow'); a.sparkle(54, 8, 'yellow', 1);
  },
};

export const ITEM_ART = { ...W, ...ITEMS, ...A };

/** Ícone final 64×64 de uma habilidade. */
export function itemIcon(id, kind, rarity, tags, evolved) {
  const draw = ITEM_ART[id];
  if (!draw) throw new Error(`sem ícone para ${id}`);
  const a = new Art();
  draw(a);
  const sym = shade(a);
  const p = frame(kind, rarity, categoryColor(tags), evolved);
  // o objeto pode "vazar" da moldura (fica por cima da borda), como num ícone de carta
  for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) { const v = sym.get(x, y); if (v) p.set(x, y, v); }
  return p;
}
