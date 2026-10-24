import { P } from './palette.mjs';
import { svg, rect, circle, ellipse, path, line, poly, g, glowFilter } from './svg.mjs';

// Laboratório de Pesquisa: piso de chapas, paredes com tubulação, bancadas e armários.
// Obstáculos são desenhados para "9-slice" (bordas fixas, centro esticável).

function floor(variant) {
  let s = rect(0, 0, 64, 64, P.concrete, { stroke: 'none', sw: 0 });
  s += rect(1, 1, 62, 62, 'none', { stroke: P.concreteD, sw: 2 });
  for (const [x, y] of [[5, 5], [59, 5], [5, 59], [59, 59]]) s += circle(x, y, 1.6, P.concreteL, { stroke: P.concreteD, sw: 1 });
  if (variant === 1) s += path('M10 40 l14 -6 l8 4 l12 -10', 'none', { stroke: P.concreteD, sw: 1.5 }) + path('M30 52 l10 -3', 'none', { stroke: P.concreteL, sw: 1 });
  if (variant === 2) s += ellipse(38, 30, 12, 7, '#26282c', { stroke: 'none', sw: 0, extra: 'opacity="0.7"' }) + ellipse(35, 28, 4, 2, '#3a3d44', { stroke: 'none', sw: 0 });
  if (variant === 3) {
    s += rect(8, 8, 48, 48, P.graphite, { r: 2, stroke: P.concreteD, sw: 2 });
    for (let i = 0; i < 6; i++) s += line(12 + i * 8, 10, 12 + i * 8, 54, P.graphiteD, 3);
  }
  return s;
}

function hazardBorder() {
  let s = rect(0, 0, 64, 64, P.concrete, { stroke: 'none', sw: 0 });
  for (let i = -1; i < 9; i++) s += poly([[i * 8, 0], [i * 8 + 4, 0], [i * 8 + 12, 8], [i * 8 + 8, 8]], i % 2 ? P.outline : P.hazard, { stroke: 'none', sw: 0 });
  return s + rect(0, 8, 64, 2, P.concreteD, { stroke: 'none', sw: 0 });
}

function wall() {
  let s = rect(0, 0, 64, 64, P.graphiteD, { stroke: 'none', sw: 0 });
  s += rect(0, 0, 64, 10, P.graphite, { stroke: 'none', sw: 0 });
  s += rect(0, 20, 64, 8, P.steelD, { stroke: P.outline, sw: 2 }) + rect(0, 34, 64, 5, P.rustD, { stroke: P.outline, sw: 2 });
  s += rect(14, 18, 6, 12, P.steel, { stroke: P.outline, sw: 2 }) + rect(44, 32, 6, 9, P.steel, { stroke: P.outline, sw: 2 });
  s += rect(26, 46, 12, 8, P.hazard, { stroke: P.outline, sw: 2 }) + path('M30 48 l4 4 m0 -4 l-4 4', 'none', { stroke: P.outline, sw: 1.5 });
  return s;
}

function bench() {
  // 192×64, bordas de 24 px
  let s = rect(3, 6, 186, 52, P.steelD, { r: 6 });
  s += rect(3, 6, 186, 12, P.steel, { r: 6 });
  s += rect(30, 14, 34, 22, P.graphiteD, { r: 3, sw: 2.5 }) + rect(33, 17, 28, 14, P.petrolL, { r: 2, stroke: 'none', sw: 0 });
  s += path('M36 26 l6 -4 l5 3 l7 -6', 'none', { stroke: P.cyan, sw: 1.5 });
  s += rect(80, 22, 8, 14, P.cyanL, { r: 2, sw: 2, extra: 'opacity="0.85"' }) + rect(92, 18, 9, 18, P.amberL, { r: 2, sw: 2, extra: 'opacity="0.85"' });
  s += rect(120, 16, 40, 16, P.cloth, { r: 2, sw: 2 }) + line(124, 21, 150, 21, P.clothD, 1.5) + line(124, 26, 144, 26, P.clothD, 1.5);
  s += circle(170, 24, 6, P.danger, { sw: 2 });
  s += rect(12, 44, 168, 8, P.graphiteD, { r: 2, sw: 2 });
  return s;
}

function cabinet(vertical) {
  if (vertical) {
    let s = rect(4, 3, 56, 186, P.petrolD, { r: 5 });
    for (let i = 0; i < 5; i++) s += rect(10, 14 + i * 34, 44, 28, P.petrol, { r: 3, sw: 2 }) + rect(26, 25 + i * 34, 12, 4, P.steelL, { r: 2, sw: 1.5 });
    return s;
  }
  let s = rect(3, 4, 186, 56, P.petrolD, { r: 5 });
  for (let i = 0; i < 5; i++) s += rect(14 + i * 34, 10, 28, 44, P.petrol, { r: 3, sw: 2 }) + rect(25 + i * 34, 26, 4, 12, P.steelL, { r: 2, sw: 1.5 });
  return s;
}

function decalCables() {
  return path('M0 40 q16 -20 32 0 t32 0', 'none', { stroke: P.outline, sw: 6 }) + path('M0 40 q16 -20 32 0 t32 0', 'none', { stroke: P.danger, sw: 3 }) +
    path('M0 50 q20 -8 40 4 t24 -2', 'none', { stroke: P.outline, sw: 6 }) + path('M0 50 q20 -8 40 4 t24 -2', 'none', { stroke: P.cyanD, sw: 3 });
}

export function arenaSprites() {
  const tile = (id, body, w = 64, h = 64, border = null) => ({ id, category: 'Arena', w, h, pivot: [0.5, 0.5], border, svg: svg(w, h, body) });
  return [
    tile('tile_floor_0', floor(0)),
    tile('tile_floor_1', floor(1)),
    tile('tile_floor_2', floor(2)),
    tile('tile_floor_3', floor(3)),
    tile('tile_hazard_edge', hazardBorder()),
    tile('tile_wall', wall()),
    tile('obstacle_bancada', bench(), 192, 64, [24, 24, 24, 24]),
    tile('obstacle_armario_v', cabinet(true), 64, 192, [24, 24, 24, 24]),
    tile('obstacle_armario_h', cabinet(false), 192, 64, [24, 24, 24, 24]),
    tile('decal_cables', decalCables()),
  ];
}
