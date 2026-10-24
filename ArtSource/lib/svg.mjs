import { P } from './palette.mjs';

// Utilidades de SVG. Todas as formas usam contorno grosso escuro (legibilidade na horda).
export const OUT = 3;

export function svg(w, h, body, defs = '') {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">` +
    `<defs>${defs}</defs>${body}</svg>`;
}

const fmt = (n) => (Math.round(n * 100) / 100).toString();

export function rect(x, y, w, h, fill, { r = 0, stroke = P.outline, sw = OUT, extra = '' } = {}) {
  return `<rect x="${fmt(x)}" y="${fmt(y)}" width="${fmt(w)}" height="${fmt(h)}" rx="${fmt(r)}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}" ${extra}/>`;
}

export function circle(cx, cy, r, fill, { stroke = P.outline, sw = OUT, extra = '' } = {}) {
  return `<circle cx="${fmt(cx)}" cy="${fmt(cy)}" r="${fmt(r)}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}" ${extra}/>`;
}

export function ellipse(cx, cy, rx, ry, fill, { stroke = P.outline, sw = OUT, extra = '' } = {}) {
  return `<ellipse cx="${fmt(cx)}" cy="${fmt(cy)}" rx="${fmt(rx)}" ry="${fmt(ry)}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}" ${extra}/>`;
}

export function path(d, fill, { stroke = P.outline, sw = OUT, extra = '' } = {}) {
  return `<path d="${d}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}" stroke-linejoin="round" stroke-linecap="round" ${extra}/>`;
}

export function line(x1, y1, x2, y2, stroke, sw = 2, extra = '') {
  return `<line x1="${fmt(x1)}" y1="${fmt(y1)}" x2="${fmt(x2)}" y2="${fmt(y2)}" stroke="${stroke}" stroke-width="${sw}" stroke-linecap="round" ${extra}/>`;
}

export function poly(points, fill, opts = {}) {
  return path('M' + points.map((p) => p.map(fmt).join(' ')).join(' L') + ' Z', fill, opts);
}

export const g = (body, transform = '') => `<g ${transform ? `transform="${transform}"` : ''}>${body}</g>`;

/** Brilho (blur) para efeitos e elites. */
export function glowFilter(id, std = 3) {
  return `<filter id="${id}" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="${std}" result="b"/>` +
    `<feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>`;
}

export function radial(id, stops) {
  return `<radialGradient id="${id}">` + stops.map(([o, c, a = 1]) => `<stop offset="${o}" stop-color="${c}" stop-opacity="${a}"/>`).join('') + `</radialGradient>`;
}

export function linear(id, stops, { x1 = 0, y1 = 0, x2 = 0, y2 = 1 } = {}) {
  return `<linearGradient id="${id}" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}">` +
    stops.map(([o, c, a = 1]) => `<stop offset="${o}" stop-color="${c}" stop-opacity="${a}"/>`).join('') + `</linearGradient>`;
}

/** Zigue-zague de raio entre dois pontos (determinístico pela semente). */
export function bolt(x1, y1, x2, y2, segments, amp, seed) {
  let s = seed;
  const rnd = () => ((s = (s * 1103515245 + 12345) % 2147483648) / 2147483648) - 0.5;
  const dx = x2 - x1, dy = y2 - y1, len = Math.hypot(dx, dy), nx = -dy / len, ny = dx / len;
  const pts = [[x1, y1]];
  for (let i = 1; i < segments; i++) {
    const t = i / segments, o = rnd() * amp * 2;
    pts.push([x1 + dx * t + nx * o, y1 + dy * t + ny * o]);
  }
  pts.push([x2, y2]);
  return 'M' + pts.map((p) => p.map(fmt).join(' ')).join(' L');
}
