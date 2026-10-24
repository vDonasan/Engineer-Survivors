// Primitivas de pixel art com bordas duras (sem suavização): tudo cai em pixels inteiros.
import { Px } from './pixel.mjs';
import { C } from './palette.mjs';

export function rect(p, x, y, w, h, col) { for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) p.set(x + i, y + j, col); return p; }

export function box(p, x, y, w, h, fill, outline = C.outline) {
  rect(p, x, y, w, h, outline);
  if (fill) rect(p, x + 1, y + 1, w - 2, h - 2, fill);
  return p;
}

export function line(p, x0, y0, x1, y1, col) {
  let dx = Math.abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
  for (;;) {
    p.set(x0, y0, col);
    if (x0 === x1 && y0 === y1) break;
    const e2 = 2 * err;
    if (e2 >= dy) { err += dy; x0 += sx; }
    if (e2 <= dx) { err += dx; y0 += sy; }
  }
  return p;
}

const inEllipse = (x, y, cx, cy, rx, ry) => ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1;

export function ellipse(p, cx, cy, rx, ry, fill, outline = C.outline) {
  for (let y = Math.floor(cy - ry - 1); y <= Math.ceil(cy + ry + 1); y++)
    for (let x = Math.floor(cx - rx - 1); x <= Math.ceil(cx + rx + 1); x++) {
      if (!inEllipse(x, y, cx, cy, rx + 0.5, ry + 0.5)) continue;
      const edge = outline && !inEllipse(x, y, cx, cy, rx - 0.5, ry - 0.5);
      if (edge) p.set(x, y, outline);
      else if (fill) p.set(x, y, fill);
    }
  return p;
}

export function disc(p, cx, cy, r, fill, outline = C.outline) { return ellipse(p, cx, cy, r, r, fill, outline); }

/** Anel de 1 px (para ondas e campos). */
export function circleLine(p, cx, cy, r, col) {
  for (let y = Math.floor(cy - r - 1); y <= Math.ceil(cy + r + 1); y++)
    for (let x = Math.floor(cx - r - 1); x <= Math.ceil(cx + r + 1); x++) {
      const d = Math.hypot(x - cx, y - cy);
      if (d <= r + 0.5 && d > r - 0.5) p.set(x, y, col);
    }
  return p;
}

/** Desenha linhas ASCII com legenda a partir de (x0, y0). */
export function stamp(p, rows, legend, x0 = 0, y0 = 0) {
  rows.forEach((s, j) => { for (let i = 0; i < s.length; i++) if (s[i] !== '.') { const col = legend[s[i]]; if (col === undefined) throw new Error(`sem cor: ${s[i]}`); p.set(x0 + i, y0 + j, col); } });
  return p;
}

export function rng(seed) {
  let s = seed >>> 0;
  return () => (s = (Math.imul(s, 1664525) + 1013904223) >>> 0) / 4294967296;
}

/** Contorno de 1 px ao redor dos pixels opacos (para elites e destaque). */
export function outlineAround(p, col) {
  const out = p.clone();
  for (const [x, y] of p.ring()) out.set(x, y, col);
  return out;
}

export function recolor(p, from, to) { return p.map((v) => (v === from ? to : v)); }

export { Px };
