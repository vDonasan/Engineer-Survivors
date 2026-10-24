import { P } from './palette.mjs';
import { svg, rect, circle, ellipse, path, line, poly, g, glowFilter } from './svg.mjs';

// Máquinas do laboratório que passaram a atacar qualquer ser vivo.
// Elite: mesmo desenho com contorno magenta brilhante.

function microbo(frame, o) {
  const W = 48, cx = 24;
  const legY = frame === 0 ? [0, 3, 0, 3] : [3, 0, 3, 0];
  let s = ellipse(cx, 41, 13, 3.5, P.shadow, { stroke: 'none', sw: 0 });
  [[12, 30], [18, 34], [30, 34], [36, 30]].forEach(([x, y], i) => {
    s += path(`M${cx + (x - cx) * 0.5} ${y - 6} L${x} ${y + legY[i]} l${x < cx ? -3 : 3} 4`, 'none', { stroke: o, sw: 3 });
  });
  s += ellipse(cx, 24, 14, 12, P.graphiteL, { stroke: o });
  s += path(`M${cx - 13} 25 q13 8 26 0`, 'none', { stroke: P.steel, sw: 2.5 });
  s += rect(cx - 2, 7, 4, 6, P.steel, { sw: 2 }) + circle(cx, 6, 2.5, P.danger, { sw: 1.5 });
  s += `<g filter="url(#glow)">` + circle(cx, 21, 5, P.danger, { stroke: o, sw: 2 }) + circle(cx + 1.5, 19.5, 1.5, P.dangerL, { stroke: 'none', sw: 0 }) + `</g>`;
  return { w: W, h: W, body: s };
}

function prensa(frame, o) {
  const W = 112, cx = 56;
  const up = frame === 0 ? 0 : 8;
  let s = ellipse(cx, 102, 34, 7, P.shadow, { stroke: 'none', sw: 0 });
  s += rect(cx - 30, 84, 20, 14, P.graphite, { r: 3, stroke: o }) + rect(cx + 10, 84, 20, 14, P.graphite, { r: 3, stroke: o });
  s += rect(cx - 34, 36, 68, 52, P.graphiteL, { r: 6, stroke: o });
  for (let i = 0; i < 5; i++) s += poly([[cx - 34 + i * 14, 80], [cx - 27 + i * 14, 80], [cx - 34 + i * 14 + 14, 88], [cx - 34 + i * 14 + 7, 88]], i % 2 ? P.outline : P.hazard, { stroke: 'none', sw: 0 });
  s += rect(cx - 34, 78, 68, 10, 'none', { r: 2, stroke: o });
  s += rect(cx - 22, 18 + up, 8, 24 - up, P.steelL, { stroke: o, sw: 2.5 }) + rect(cx + 14, 18 + up, 8, 24 - up, P.steelL, { stroke: o, sw: 2.5 });
  s += rect(cx - 30, 8 + up, 60, 14, P.steel, { r: 3, stroke: o });
  s += `<g filter="url(#glow)">` + rect(cx - 20, 50, 40, 8, P.dangerD, { r: 3, stroke: o, sw: 2 }) + rect(cx - 16, 52, 32, 4, P.danger, { r: 2, stroke: 'none', sw: 0 }) + `</g>`;
  s += circle(cx - 26, 44, 2, P.steelD, { stroke: 'none', sw: 0 }) + circle(cx + 26, 44, 2, P.steelD, { stroke: 'none', sw: 0 });
  s += ellipse(cx + 18, 70, 6, 3, P.rust, { stroke: 'none', sw: 0 });
  return { w: W, h: W, body: s };
}

function torreta(frame, o) {
  const W = 64, cx = 32;
  let s = ellipse(cx, 57, 18, 4.5, P.shadow, { stroke: 'none', sw: 0 });
  s += path(`M${cx} 34 L${cx - 18} 56 M${cx} 34 L${cx + 18} 56 M${cx} 34 L${cx} 57`, 'none', { stroke: o, sw: 4 });
  s += path(`M${cx} 34 L${cx - 18} 56 M${cx} 34 L${cx + 18} 56 M${cx} 34 L${cx} 57`, 'none', { stroke: P.steel, sw: 2 });
  s += rect(cx - 16, 14, 32, 22, P.petrol, { r: 6, stroke: o });
  s += rect(cx - 16, 28, 32, 4, P.hazard, { stroke: o, sw: 1.5 });
  const lens = frame === 0 ? P.dangerD : P.danger;
  s += `<g filter="url(#glow)">` + circle(cx, 22, 7, P.graphiteD, { stroke: o, sw: 2.5 }) + circle(cx, 22, 4.5, lens, { stroke: 'none', sw: 0 }) +
    (frame === 1 ? circle(cx, 22, 10, 'none', { stroke: P.dangerL, sw: 1.5 }) : '') + `</g>`;
  s += rect(cx + 14, 18, 10, 5, P.steelL, { r: 1, stroke: o, sw: 2 });
  return { w: W, h: W, body: s };
}

const KINDS = { microbo_enxame: microbo, prensa_ambulante: prensa, torreta_inspecao: torreta };

export function enemySprites() {
  const out = [];
  for (const [id, fn] of Object.entries(KINDS)) {
    for (let f = 0; f < 2; f++) {
      const a = fn(f, P.outline);
      out.push({ id: `enemy_${id}_${f}`, category: 'Enemies', w: a.w, h: a.h, pivot: [0.5, 0.15], svg: svg(a.w, a.h, a.body, glowFilter('glow', 2)) });
      const e = fn(f, P.elite);
      const aura = `<g filter="url(#aura)" opacity="0.7">${e.body}</g>`;
      out.push({
        id: `enemy_${id}_elite_${f}`, category: 'Enemies', w: e.w, h: e.h, pivot: [0.5, 0.15],
        svg: svg(e.w, e.h, aura + e.body, glowFilter('glow', 2) + glowFilter('aura', 3)),
      });
    }
  }
  return out;
}
