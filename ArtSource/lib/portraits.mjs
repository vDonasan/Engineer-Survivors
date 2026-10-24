import { P } from './palette.mjs';
import { svg, rect, circle, ellipse, path, line, poly, g } from './svg.mjs';
import { SPECS } from './characters.mjs';

// Retratos 128×128 para o HUD. A emoção muda sobrancelhas, olhos, boca, lágrimas, suor e o fundo.
const none = { stroke: 'none', sw: 0 };
const CX = 64, CY = 66;

export const EMOTIONS = ['raiva', 'medo', 'desespero', 'choro', 'exaustao', 'alivio', 'determinacao', 'vitoria', 'derrota'];

const BG = {
  raiva: '#5a1d1d', medo: '#1d2f5a', desespero: '#3e1d5a', choro: '#1d3a5a', exaustao: '#34373d',
  alivio: '#1d4a32', determinacao: '#5a3d12', vitoria: '#6b5212', derrota: '#1a1b1f',
};

function eyes(e) {
  const L = CX - 13, R = CX + 13, y = CY - 2;
  const dot = (x, r = 3.2) => circle(x, y, r, P.outline, none) + circle(x + 1, y - 1, 1, P.white, none);
  switch (e) {
    case 'raiva': return dot(L) + dot(R) + path(`M${L - 9} ${y - 12} L${L + 6} ${y - 6} M${R + 9} ${y - 12} L${R - 6} ${y - 6}`, 'none', { stroke: P.outline, sw: 3.5 });
    case 'medo': return circle(L, y, 6, P.white, { sw: 2 }) + circle(R, y, 6, P.white, { sw: 2 }) + circle(L, y, 1.8, P.outline, none) + circle(R, y, 1.8, P.outline, none) +
      path(`M${L - 7} ${y - 13} q7 -5 14 -1 M${R - 7} ${y - 14} q7 -4 14 1`, 'none', { stroke: P.outline, sw: 3 });
    case 'desespero': return circle(L, y, 5, P.white, { sw: 2 }) + circle(R, y, 5, P.white, { sw: 2 }) + circle(L, y + 1, 2, P.outline, none) + circle(R, y + 1, 2, P.outline, none) +
      path(`M${L - 8} ${y - 8} L${L + 6} ${y - 15} M${R + 8} ${y - 8} L${R - 6} ${y - 15}`, 'none', { stroke: P.outline, sw: 3 });
    case 'choro': return path(`M${L - 6} ${y} q6 5 12 0 M${R - 6} ${y} q6 5 12 0`, 'none', { stroke: P.outline, sw: 3 }) +
      path(`M${L - 6} ${y - 11} L${L + 6} ${y - 14} M${R + 6} ${y - 11} L${R - 6} ${y - 14}`, 'none', { stroke: P.outline, sw: 3 });
    case 'exaustao': return path(`M${L - 6} ${y} h12 M${R - 6} ${y} h12`, 'none', { stroke: P.outline, sw: 3 }) +
      path(`M${L - 7} ${y + 5} q7 5 14 0 M${R - 7} ${y + 5} q7 5 14 0`, 'none', { stroke: P.bags, sw: 3 });
    case 'alivio': return path(`M${L - 6} ${y + 2} q6 -6 12 0 M${R - 6} ${y + 2} q6 -6 12 0`, 'none', { stroke: P.outline, sw: 3 });
    case 'determinacao': return dot(L, 3) + dot(R, 3) + path(`M${L - 9} ${y - 9} L${L + 7} ${y - 8} M${R + 9} ${y - 9} L${R - 7} ${y - 8}`, 'none', { stroke: P.outline, sw: 4 });
    case 'vitoria': return path(`M${L - 6} ${y + 1} q6 -7 12 0 M${R - 6} ${y + 1} q6 -7 12 0`, 'none', { stroke: P.outline, sw: 3.2 }) +
      poly([[R + 12, y - 16], [R + 14, y - 11], [R + 19, y - 10], [R + 14, y - 8], [R + 12, y - 3], [R + 10, y - 8], [R + 5, y - 10], [R + 10, y - 11]], P.amberL, { sw: 1 });
    case 'derrota': return path(`M${L - 5} ${y - 5} l10 10 m0 -10 l-10 10 M${R - 5} ${y - 5} l10 10 m0 -10 l-10 10`, 'none', { stroke: P.outline, sw: 3 });
  }
  return '';
}

function mouth(e) {
  const y = CY + 17;
  switch (e) {
    case 'raiva': return rect(CX - 10, y - 4, 20, 8, P.white, { r: 2, sw: 2.5 }) + line(CX - 10, y, CX + 10, y, P.outline, 1.5);
    case 'medo': return ellipse(CX, y, 5, 6, P.outline, none);
    case 'desespero': return path(`M${CX - 11} ${y + 5} q3 -12 11 -10 q8 -2 11 10 q-11 6 -22 0 Z`, P.outline, { sw: 1.5 });
    case 'choro': return path(`M${CX - 10} ${y + 3} q5 -6 10 -2 q5 -4 10 2`, 'none', { stroke: P.outline, sw: 3 });
    case 'exaustao': return ellipse(CX, y + 1, 7, 3, P.outline, none);
    case 'alivio': return path(`M${CX - 8} ${y - 1} q8 6 16 0`, 'none', { stroke: P.outline, sw: 3 });
    case 'determinacao': return line(CX - 9, y, CX + 9, y - 1, P.outline, 3.5);
    case 'vitoria': return path(`M${CX - 12} ${y - 3} q12 14 24 0 Z`, P.white, { sw: 2.5 });
    case 'derrota': return path(`M${CX - 9} ${y + 2} q3 -4 6 0 q3 4 6 0 q3 -4 6 0`, 'none', { stroke: P.outline, sw: 2.5 });
  }
  return '';
}

function extras(e) {
  const sweat = (x, y) => path(`M${x} ${y} q4 6 0 10 q-4 -4 0 -10 Z`, P.cyanL, { sw: 1.5 });
  const tear = (x) => path(`M${x} ${CY + 3} q-3 10 0 22 q3 -12 0 -22 Z`, P.xp, { sw: 1.2 });
  switch (e) {
    case 'medo': return sweat(CX + 26, CY - 22) + sweat(CX - 30, CY - 14);
    case 'desespero': return sweat(CX + 28, CY - 18) + sweat(CX + 20, CY - 26) + sweat(CX - 28, CY - 20);
    case 'choro': return tear(CX - 16) + tear(CX + 16);
    case 'exaustao': return sweat(CX + 27, CY - 20);
    case 'alivio': return sweat(CX + 27, CY - 22);
    case 'derrota': return g(rect(-8, -3, 16, 6, P.cloth, { sw: 1.5 }), `translate(${CX - 22} ${CY + 8}) rotate(20)`) + path(`M${CX + 18} ${CY - 34} q6 -6 12 0 q-6 6 -12 0`, 'none', { stroke: P.steelL, sw: 2 });
    case 'raiva': return path(`M${CX + 24} ${CY - 34} l6 4 l-2 -7 l7 2`, 'none', { stroke: P.danger, sw: 3 });
  }
  return '';
}

function bust(spec) {
  const c = SPECS[spec];
  let s = '';
  // ombros e roupa
  s += path(`M${CX - 46} 128 q4 -30 46 -32 q42 2 46 32 Z`, c.jacket, {});
  if (c.hat === 'hardhat') s += rect(CX - 40, 112, 80, 7, P.hazard, { sw: 2 }) + path(`M${CX - 12} 96 L${CX} 110 L${CX + 12} 96`, 'none', { stroke: c.accent, sw: 4 });
  else s += rect(CX - 18, 100, 36, 28, P.rust, { r: 4, sw: 2.5 });
  s += rect(CX - 9, CY + 22, 18, 14, c.skinD, { sw: 2.5 });
  if (c.ponytail) s += path(`M${CX - 30} ${CY - 6} q-14 20 -4 40 q8 -8 6 -36 Z`, c.hair, { sw: 2.5 });
  s += ellipse(CX, CY, 30, 33, c.skin);
  s += ellipse(CX - 31, CY + 2, 5, 8, c.skinD, {}) + ellipse(CX + 31, CY + 2, 5, 8, c.skinD, {});
  s += path(`M${CX - 3} ${CY + 4} q3 6 6 1`, 'none', { stroke: c.skinD, sw: 2.5 });
  if (c.stubble) for (let i = 0; i < 16; i++) s += circle(CX - 16 + (i % 8) * 4.5, CY + 22 + Math.floor(i / 8) * 4, 0.9, c.skinD, none);
  if (c.bandage) s += g(rect(-8, -3, 16, 6, P.cloth, { sw: 1.5 }), `translate(${CX + 19} ${CY + 9}) rotate(-25)`);
  // capacete / boné
  if (c.hat === 'hardhat') {
    s += path(`M${CX - 33} ${CY - 14} q0 -34 33 -34 q33 0 33 34 Z`, P.hazard);
    s += rect(CX - 38, CY - 18, 76, 8, P.hazardD, { r: 4, sw: 2.5 });
    s += rect(CX - 24, CY - 30, 20, 9, P.cyanD, { r: 3, sw: 2 }) + rect(CX + 4, CY - 30, 20, 9, P.cyanD, { r: 3, sw: 2 });
    s += poly([[CX - 3, CY - 46], [CX + 6, CY - 37], [CX + 1, CY - 37], [CX + 4, CY - 30], [CX - 7, CY - 39], [CX - 2, CY - 39]], c.accent, { sw: 1.5 });
  } else {
    s += path(`M${CX - 31} ${CY - 10} q0 -36 31 -36 q31 0 31 36 Z`, c.accent);
    s += circle(CX - 13, CY - 22, 8, P.steelL, { sw: 2.5 }) + circle(CX + 13, CY - 22, 8, P.steelL, { sw: 2.5 }) +
      circle(CX - 13, CY - 22, 4.5, P.cyan, none) + circle(CX + 13, CY - 22, 4.5, P.cyan, none);
  }
  return s;
}

export function portraitSprites() {
  const out = [];
  for (const spec of Object.keys(SPECS))
    for (const e of EMOTIONS) {
      const body = rect(0, 0, 128, 128, BG[e], { r: 12, stroke: P.outline, sw: 4 }) + bust(spec) +
        path(`M${CX - 13 - 7} ${CY + 5} q7 4 14 0 M${CX + 13 - 7} ${CY + 5} q7 4 14 0`, 'none', { stroke: P.bags, sw: 2.5, extra: 'opacity="0.8"' }) +
        eyes(e) + mouth(e) + extras(e);
      out.push({ id: `portrait_${spec}_${e}`, category: 'Portraits', w: 128, h: 128, pivot: [0.5, 0.5], svg: svg(128, 128, body) });
    }
  return out;
}
