// Interface em pixel art: molduras e botões 9-slice e o fundo do menu (laboratório à noite).
import { C } from './palette.mjs';
import { Px, rect, box, line, disc, ellipse, rng } from './draw.mjs';

const O = C.outline;

/** Moldura de painel 24×24 (bordas de 8 px): aço com rebites e faixa de destaque no topo. */
function frame(accent, fill = C.gray1) {
  const p = new Px(24, 24);
  rect(p, 0, 0, 24, 24, O);
  rect(p, 1, 1, 22, 22, C.gray3);
  rect(p, 1, 1, 22, 1, C.gray4);
  rect(p, 1, 22, 22, 1, C.gray2);
  rect(p, 3, 3, 18, 18, O);
  rect(p, 4, 4, 16, 16, fill);
  rect(p, 4, 4, 16, 1, C.gray2);
  rect(p, 1, 1, 22, 2, accent); rect(p, 1, 3, 22, 1, C.gray2);
  for (const [x, y] of [[1, 5], [21, 5], [1, 20], [21, 20]]) { p.set(x + 0, y, C.gray5); p.set(x + 1, y + 1, C.gray2); }
  return p;
}

/** Botão 16×16 (bordas de 5 px). */
function button(face, light, dark, edge) {
  const p = new Px(16, 16);
  rect(p, 0, 0, 16, 16, O);
  rect(p, 1, 1, 14, 14, face);
  rect(p, 1, 1, 14, 2, light);
  rect(p, 1, 13, 14, 2, dark);
  rect(p, 1, 1, 1, 14, edge); rect(p, 14, 1, 1, 14, edge);
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
    s('ui_frame', frame(C.yellow2), [8, 8, 8, 8]),
    s('ui_frame_cyan', frame(C.cyan2, '#1b2f36'), [8, 8, 8, 8]),
    s('ui_frame_dark', frame(C.gray3, '#14161b'), [8, 8, 8, 8]),
    s('ui_btn', button(C.gray2, C.gray3, C.gray1, C.gray3), [5, 5, 5, 5]),
    s('ui_btn_hover', button(C.gray3, C.yellow2, C.gray1, C.yellow1), [5, 5, 5, 5]),
    s('ui_btn_selected', button(C.petrol2, C.cyan2, C.petrol1, C.cyan1), [5, 5, 5, 5]),
    s('ui_btn_primary', button(C.orange1, C.orange2, C.rust1, C.orange2), [5, 5, 5, 5]),
    s('ui_menu_bg', menuBg()),
  ];
}
