// Motor de pixel art por materiais: desenha-se só o preenchimento de cada parte (pele, cabelo,
// roupa...) e o motor aplica luz (alto à esquerda), sombra (embaixo à direita) e contorno seletivo
// (cor escura do próprio material na borda externa; linha mais escura entre materiais diferentes).
// Assim não sobram linhas de contorno sobrepostas nem pixels soltos.
import { Px } from './pixel.mjs';
import { C } from './palette.mjs';

/** Rampa de 4 tons: [contorno, sombra, base, luz]. */
export const ramp = (outline, shadow, base, light) => ({ o: outline, s: shadow, b: base, l: light });

/** Mapa de materiais: cada pixel guarda o nome do material (ou null). */
export class Mat {
  constructor(w, h) { this.w = w; this.h = h; this.m = new Array(w * h).fill(null); this.flat = new Set(); }
  get(x, y) { return x < 0 || y < 0 || x >= this.w || y >= this.h ? null : this.m[y * this.w + x]; }
  set(x, y, mat) { x = Math.round(x); y = Math.round(y); if (x >= 0 && y >= 0 && x < this.w && y < this.h) this.m[y * this.w + x] = mat; return this; }
  clone() { const o = new Mat(this.w, this.h); o.m = this.m.slice(); o.flat = new Set(this.flat); return o; }

  rect(x, y, w, h, mat) { for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) this.set(x + i, y + j, mat); return this; }
  /** Elipse cheia com centro em (cx, cy) e raios rx, ry (bordas de pixel inteiras). */
  oval(cx, cy, rx, ry, mat) {
    for (let y = Math.floor(cy - ry); y <= Math.ceil(cy + ry); y++)
      for (let x = Math.floor(cx - rx); x <= Math.ceil(cx + rx); x++) {
        const dx = (x - cx) / (rx + 0.35), dy = (y - cy) / (ry + 0.35);
        if (dx * dx + dy * dy <= 1) this.set(x, y, mat);
      }
    return this;
  }
  /** Linhas de texto: cada caractere mapeia para um material via legenda; '.' não altera. */
  rows(x0, y0, rows, legend) {
    rows.forEach((r, j) => [...r].forEach((ch, i) => { if (ch !== '.') this.set(x0 + i, y0 + j, legend[ch] === undefined ? (() => { throw new Error(`símbolo ${ch}`); })() : legend[ch]); }));
    return this;
  }
  line(x0, y0, x1, y1, mat) {
    let dx = Math.abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
    for (;;) { this.set(x0, y0, mat); if (x0 === x1 && y0 === y1) break; const e2 = 2 * err; if (e2 >= dy) { err += dy; x0 += sx; } if (e2 <= dx) { err += dx; y0 += sy; } }
    return this;
  }
  /** Apaga (transparente). */
  clear(x, y, w = 1, h = 1) { return this.rect(x, y, w, h, null); }
  shift(dx, dy) { const o = new Mat(this.w, this.h); o.flat = new Set(this.flat); for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) { const v = this.get(x, y); if (v) o.set(x + dx, y + dy, v); } return o; }
  flipX() { const o = new Mat(this.w, this.h); o.flat = new Set(this.flat); for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) o.set(this.w - 1 - x, y, this.get(x, y)); return o; }
  /** Por cima (null não altera). */
  over(src, dx = 0, dy = 0) { for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) { const v = src.get(x, y); if (v) this.set(x + dx, y + dy, v); } for (const f of src.flat) this.flat.add(f); return this; }
}

/**
 * Renderiza o mapa: cada material é uma rampa (ou uma cor única, para detalhes "chapados" como olhos).
 * - borda externa: tom de contorno do material vizinho (sel-out), mais escuro embaixo;
 * - borda entre materiais: o material "de baixo" (menor prioridade) ganha o tom de contorno;
 * - luz no topo/esquerda e sombra embaixo/direita de cada região.
 */
export function render(mat, palette, { outline = true, shade = true, order = [] } = {}) {
  const p = new Px(mat.w, mat.h);
  const pr = (m) => { const i = order.indexOf(m); return i < 0 ? 0 : i + 1; };
  const R = (m) => palette[m];
  for (let y = 0; y < mat.h; y++) for (let x = 0; x < mat.w; x++) {
    const m = mat.get(x, y);
    if (!m) continue;
    const r = R(m);
    if (r === undefined) throw new Error(`material sem cor: ${m}`);
    if (typeof r === 'string') { p.set(x, y, r); continue; }
    let col = r.b;
    if (shade && !mat.flat.has(m)) {
      const up = mat.get(x, y - 1), left = mat.get(x - 1, y), down = mat.get(x, y + 1), right = mat.get(x + 1, y);
      if (down !== m || right !== m) col = r.s;
      if ((up !== m || left !== m) && down === m && right === m) col = r.l;
    }
    // linha entre materiais: quem tem menor prioridade escurece
    for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]]) {
      const n = mat.get(nx, ny);
      if (n && n !== m && typeof R(n) !== 'string' && typeof r !== 'string' && pr(m) < pr(n) && !mat.flat.has(n)) { col = r.o; break; }
    }
    p.set(x, y, col);
  }
  if (outline) {
    const out = p.clone();
    for (let y = 0; y < mat.h; y++) for (let x = 0; x < mat.w; x++) {
      if (mat.get(x, y)) continue;
      // vizinho opaco (4-conexo) mais escuro vence; abaixo usa o contorno puro
      let best = null;
      for (const [nx, ny] of [[x, y - 1], [x - 1, y], [x + 1, y], [x, y + 1]]) {
        const n = mat.get(nx, ny);
        if (!n) continue;
        const r = R(n);
        best = typeof r === 'string' ? C.outline : r.o;
        if (ny === y - 1) best = C.outline;
      }
      if (best) out.set(x, y, best);
    }
    return out;
  }
  return p;
}
