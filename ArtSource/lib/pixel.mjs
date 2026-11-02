// Tela de pixels: cada célula guarda uma cor hexadecimal da paleta ou null (transparente).
// Operações exatas (sem interpolação): composição, deslocamento, espelho, rotação de 90°.
export class Px {
  constructor(w, h) { this.w = w; this.h = h; this.c = new Array(w * h).fill(null); }

  static fromRows(rows, legend, w = 32, h = rows.length) {
    const p = new Px(w, h);
    rows.forEach((row, y) => {
      if (row == null) return;
      const [x0, s] = typeof row === 'string' ? [0, row] : row;
      if (x0 + s.length > w) throw new Error(`linha ${y} passa da largura: ${s}`);
      for (let i = 0; i < s.length; i++) {
        const ch = s[i];
        if (ch === '.') continue;
        const col = legend[ch];
        if (col === undefined) throw new Error(`símbolo sem cor: "${ch}" na linha ${y}`);
        p.set(x0 + i, y, col);
      }
    });
    return p;
  }

  get(x, y) { return x < 0 || y < 0 || x >= this.w || y >= this.h ? null : this.c[y * this.w + x]; }
  set(x, y, col) { if (x >= 0 && y >= 0 && x < this.w && y < this.h) this.c[y * this.w + x] = col; }
  clone() { const p = new Px(this.w, this.h); p.c = this.c.slice(); return p; }

  /** Desenha `src` por cima (null = não altera; "erase" apaga). */
  blit(src, dx = 0, dy = 0) {
    for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) {
      const v = src.c[y * src.w + x];
      if (v === null) continue;
      this.set(x + dx, y + dy, v === 'erase' ? null : v);
    }
    return this;
  }

  /** Copia uma faixa de linhas deslocada (para "respiração", bobbing e pulos). */
  shiftRows(y0, y1, dy) {
    const out = this.clone();
    for (let y = y0; y <= y1; y++) for (let x = 0; x < this.w; x++) out.set(x, y, null);
    for (let y = y0; y <= y1; y++) for (let x = 0; x < this.w; x++) { const v = this.get(x, y); if (v) out.set(x, y + dy, v); }
    return out;
  }

  shift(dx, dy) { const out = new Px(this.w, this.h); return out.blit(this, dx, dy); }
  flipX() { const out = new Px(this.w, this.h); for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) out.set(this.w - 1 - x, y, this.get(x, y)); return out; }
  rotate90cw() { const out = new Px(this.h, this.w); for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) out.set(this.h - 1 - y, x, this.get(x, y)); return out; }
  map(fn) { const out = this.clone(); out.c = out.c.map((v, i) => (v === null ? null : fn(v, i % this.w, Math.floor(i / this.w)))); return out; }

  /** Pixels vazios vizinhos (4-conexos) de pixels opacos. */
  ring() {
    const pts = [];
    for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) {
      if (this.get(x, y)) continue;
      if (this.get(x - 1, y) || this.get(x + 1, y) || this.get(x, y - 1) || this.get(x, y + 1)) pts.push([x, y]);
    }
    return pts;
  }

  bottomRow() { for (let y = this.h - 1; y >= 0; y--) for (let x = 0; x < this.w; x++) if (this.get(x, y)) return y; return -1; }

  toRGBA() {
    const out = new Uint8Array(this.w * this.h * 4);
    this.c.forEach((v, i) => {
      if (!v) return;
      out[i * 4] = parseInt(v.slice(1, 3), 16); out[i * 4 + 1] = parseInt(v.slice(3, 5), 16);
      out[i * 4 + 2] = parseInt(v.slice(5, 7), 16); out[i * 4 + 3] = v.length === 9 ? parseInt(v.slice(7, 9), 16) : 255;
    });
    return out;
  }
}
