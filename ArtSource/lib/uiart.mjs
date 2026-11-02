// Interface em pixel art: painéis, botões, cartas e trilhos 9-slice e o fundo do menu (laboratório à noite).
import { C } from './palette.mjs';
import { Px, rect, box, line, disc, ellipse, rng } from './draw.mjs';

const O = C.outline;

// Visual moderno em pixel art: vidro escuro translúcido, cantos arredondados de pixel, colchetes de destaque
// nos cantos (não esticam no 9-slice) e botões chapados com degrau de profundidade embaixo.
const GLASS = '#121620e8', EDGE = '#2e3648', EDGE_HI = '#46506a';

/** Pixel dentro de um retângulo de cantos arredondados (raio r, em pixels). */
const inside = (x, y, w, h, r) => {
  const cx = x < r ? r : x > w - 1 - r ? w - 1 - r : x, cy = y < r ? r : y > h - 1 - r ? h - 1 - r : y;
  return Math.hypot(x - cx, y - cy) <= r + 0.35;
};

/** Painel 24×24 (bordas de 8 px): vidro escuro, contorno fino e colchetes na cor de destaque. */
function frame(accent, fill = GLASS) {
  const n = 24, p = new Px(n, n);
  for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) {
    if (!inside(x, y, n, n, 4)) continue;
    const edge = !inside(x - 1, y, n, n, 4) || !inside(x + 1, y, n, n, 4) || !inside(x, y - 1, n, n, 4) || !inside(x, y + 1, n, n, 4);
    if (edge) { p.set(x, y, O); continue; }
    const inner = !inside(x - 2, y, n, n, 4) || !inside(x + 2, y, n, n, 4) || !inside(x, y - 2, n, n, 4) || !inside(x, y + 2, n, n, 4);
    p.set(x, y, inner ? (y < 8 ? EDGE_HI : EDGE) : fill);
  }
  // colchetes nos quatro cantos
  for (const [x, y, sx, sy] of [[2, 2, 1, 1], [21, 2, -1, 1], [2, 21, 1, -1], [21, 21, -1, -1]]) {
    for (let i = 1; i < 6; i++) { p.set(x + sx * i, y, accent); p.set(x, y + sy * i, accent); }
    p.set(x + sx, y + sy, accent);
  }
  return p;
}

/** Botão 16×16 (bordas de 5 px): face chapada, brilho de 1 px no topo e degrau escuro embaixo. */
function button(face, top, depth, rim = O) {
  const n = 16, p = new Px(n, n);
  for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) {
    if (!inside(x, y, n, n, 3)) continue;
    const edge = !inside(x - 1, y, n, n, 3) || !inside(x + 1, y, n, n, 3) || !inside(x, y - 1, n, n, 3) || !inside(x, y + 1, n, n, 3);
    if (edge) p.set(x, y, rim);
    else if (y >= n - 4) p.set(x, y, depth);
    else if (y <= 2) p.set(x, y, top);
    else p.set(x, y, face);
  }
  return p;
}

/** Carta de subida de nível 24×24: vidro com faixa grossa da raridade no topo. */
function card(accent) {
  const p = frame(accent, '#151a26f0');
  for (let x = 3; x < 21; x++) { p.set(x, 2, accent); p.set(x, 3, accent); p.set(x, 4, '#00000040'); }
  return p;
}

/** Trilho de barra 12×12 (bordas de 4 px). */
function track() {
  const n = 12, p = new Px(n, n);
  for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) {
    if (!inside(x, y, n, n, 2)) continue;
    const edge = !inside(x - 1, y, n, n, 2) || !inside(x + 1, y, n, n, 2) || !inside(x, y - 1, n, n, 2) || !inside(x, y + 1, n, n, 2);
    p.set(x, y, edge ? O : y < 3 ? '#07080cf0' : '#0d1018e0');
  }
  return p;
}

/** Fundo do menu 640×360: janelas com a cidade à noite, prateleiras, monitores e o chão do laboratório. */
function menuBg() {
  const W = 640, H = 360, p = new Px(W, H), r = rng(2024);
  rect(p, 0, 0, W, H, '#16181f');
  // parede com faixas
  rect(p, 0, 0, W, 210, '#1d2029');
  for (let x = 0; x < W; x += 64) rect(p, x, 0, 2, 210, '#181a22');
  rect(p, 0, 200, W, 10, C.gray1); rect(p, 0, 200, W, 1, C.gray2);
  // janelas com o céu e a cidade
  for (let wx = 24; wx < W; wx += 152) {
    box(p, wx, 22, 120, 110, '#0d1630', C.gray3);
    const sky = ['#0d1630', '#101d3d', '#15264d'];
    for (let y = 0; y < 106; y++) rect(p, wx + 2, 24 + y, 116, 1, sky[Math.min(2, Math.floor(y / 40))]);
    for (let i = 0; i < 14; i++) p.set(wx + 3 + Math.floor(r() * 114), 26 + Math.floor(r() * 40), r() < 0.3 ? C.yellow3 : C.white);
    let x = wx + 2;
    while (x < wx + 118) {
      const bw = 8 + Math.floor(r() * 14), bh = 20 + Math.floor(r() * 50);
      rect(p, x, 130 - bh, Math.min(bw, wx + 118 - x), bh, r() < 0.5 ? '#0a0f1e' : '#0c1324');
      for (let yy = 130 - bh + 3; yy < 128; yy += 5) for (let xx = x + 2; xx < Math.min(x + bw - 1, wx + 117); xx += 3) if (r() < 0.35) p.set(xx, yy, r() < 0.7 ? C.yellow2 : C.cyan2);
      x += bw + 1;
    }
    rect(p, wx + 59, 24, 2, 106, C.gray3); rect(p, wx + 2, 76, 116, 2, C.gray3);
  }
  disc(p, 560, 50, 9, C.offwhite, null); disc(p, 556, 47, 2, C.gray5, null);
  // bancadas com monitores acesos
  for (const bx of [10, 190, 370, 520]) {
    box(p, bx, 150, 110, 52, C.gray3); rect(p, bx + 1, 151, 108, 4, C.gray4);
    for (let k = 0; k < 2; k++) {
      const mx = bx + 12 + k * 50;
      box(p, mx, 116, 38, 28, C.gray1); rect(p, mx + 2, 118, 34, 24, k ? C.petrol2 : '#13361f');
      for (let ln = 0; ln < 5; ln++) rect(p, mx + 4, 121 + ln * 4, 6 + Math.floor(r() * 22), 1, k ? C.cyan2 : C.green2);
      rect(p, mx + 17, 144, 4, 6, C.gray2);
    }
    for (let i = 0; i < 4; i++) p.set(bx + 8 + i * 4, 160, [C.red2, C.green2, C.yellow2, C.cyan2][i]);
  }
  // chão
  for (let y = 210; y < H; y++) rect(p, 0, y, W, 1, y < 230 ? '#262a33' : '#20232b');
  for (let x = 0; x < W; x += 32) line(p, x, 210, x - 40 + Math.floor(x / 8), H, '#1a1d24');
  for (let y = 240; y < H; y += 28) rect(p, 0, y, W, 1, '#1a1d24');
  for (let y = 212; y < 220; y++) for (let x = 0; x < W; x++) p.set(x, y, ((x + y) >> 3) % 2 ? C.yellow2 : O);
  // cabos no chão e uma poça de luz
  for (let x = 0; x < W; x++) { const y = 300 + Math.round(Math.sin(x / 23) * 6); p.set(x, y, C.red1); p.set(x, y + 1, O); }
  ellipse(p, 320, 330, 150, 16, '#2a2f3a', null);
  return p;
}

export function uiSprites() {
  const s = (id, px, border) => ({ id, category: 'UI', w: px.w, h: px.h, pivot: [px.w / 2, px.h / 2], px, border });
  return [
    s('ui_frame', frame(C.amber), [8, 8, 8, 8]),
    s('ui_frame_cyan', frame(C.cyan2, '#10202ae8'), [8, 8, 8, 8]),
    s('ui_frame_dark', frame(C.gray3, '#0d1016f0'), [8, 8, 8, 8]),
    s('ui_btn', button('#262d3c', '#3a4458', '#171b25', O), [5, 5, 5, 5]),
    s('ui_btn_hover', button('#323b4f', '#58d8f0', '#1b2130', C.cyan2), [5, 5, 5, 5]),
    s('ui_btn_selected', button('#1c4f5e', '#5fe6ff', '#123540', C.cyan2), [5, 5, 5, 5]),
    s('ui_btn_primary', button('#ff8a1f', '#ffc066', '#b5520a', O), [5, 5, 5, 5]),
    s('ui_card_comum', card(C.gray5), [8, 8, 8, 8]),
    s('ui_card_incomum', card(C.cyan2), [8, 8, 8, 8]),
    s('ui_card_raro', card(C.amber), [8, 8, 8, 8]),
    s('ui_card_reliquia', card(C.magenta), [8, 8, 8, 8]),
    s('ui_card_hover', card(C.white), [8, 8, 8, 8]),
    s('ui_track', track(), [4, 4, 4, 4]),
    s('ui_slot', frame(C.gray4, '#0d1018e0'), [8, 8, 8, 8]),
    s('ui_menu_bg', menuBg()),
  ];
}
