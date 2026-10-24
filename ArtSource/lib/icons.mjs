import { P, rarityColor } from './palette.mjs';
import { svg, rect, circle, ellipse, path, line, poly, g, glowFilter, bolt } from './svg.mjs';

// Ícones 64×64: moldura com a cor da raridade e símbolo legível em tamanho pequeno.
const none = { stroke: 'none', sw: 0 };

function frame(rarity, inner) {
  return rect(3, 3, 58, 58, P.graphiteD, { r: 10, stroke: rarityColor[rarity] || P.steelL, sw: 3.5 }) +
    rect(7, 7, 50, 50, P.graphite, { r: 7, stroke: 'none', sw: 0 }) + inner;
}

const glow = (b) => `<g filter="url(#ig)">${b}</g>`;

const SYMBOLS = {
  descarga_direcionada: () => glow(poly([[36, 8], [18, 34], [30, 34], [24, 56], [46, 26], [33, 26], [40, 8]], P.cyan, { stroke: P.outline, sw: 2.5 }) +
    poly([[35, 13], [24, 31], [33, 31], [29, 45], [41, 29], [30, 29]], P.cyanL, { stroke: 'none', sw: 0 })),
  braco_giratorio: () => circle(32, 32, 7, P.steel, { sw: 2.5 }) + g(rect(-4, -22, 8, 18, P.steelL, { r: 3, sw: 2.5 }) + rect(-8, -26, 16, 7, P.orange, { r: 2, sw: 2.5 }), 'translate(32 32) rotate(35)') +
    path('M14 44 A20 20 0 0 0 44 50', 'none', { stroke: P.white, sw: 2.5, extra: 'stroke-dasharray="4 4"' }),
  laser_industrial: () => rect(10, 26, 16, 12, P.steel, { r: 2, sw: 2.5 }) + glow(rect(26, 29.5, 28, 5, P.orange, { r: 2.5, stroke: 'none', sw: 0 }) + rect(26, 31, 28, 2, P.amberL, none)),
  campo_eletromagnetico: () => glow(circle(32, 32, 20, 'none', { stroke: P.cyan, sw: 2.5, extra: 'stroke-dasharray="6 4"' }) + circle(32, 32, 12, 'none', { stroke: P.cyanL, sw: 2 })) +
    path('M26 26 v10 a6 6 0 0 0 12 0 v-10', 'none', { stroke: P.danger, sw: 4 }),
  bateria_portatil: () => rect(18, 16, 28, 38, P.graphiteL, { r: 4, sw: 3 }) + rect(26, 11, 12, 6, P.steel, { r: 2, sw: 2.5 }) +
    [0, 1, 2].map((i) => rect(22, 42 - i * 10, 20, 7, P.heal, { r: 2, sw: 1.5 })).join(''),
  turbina: () => circle(32, 32, 20, P.graphiteL, { sw: 3 }) + [0, 1, 2, 3, 4].map((i) => g(path('M0 0 q10 -6 16 -2 q-8 4 -16 2 Z', P.steelL, { sw: 1.8 }), `translate(32 32) rotate(${i * 72})`)).join('') + circle(32, 32, 4, P.orange, { sw: 2 }),
  pulso_amplificado: () => glow([[-12, P.cyan], [12, P.cyan], [0, P.amber]].map(([dx, c]) =>
    poly([[36 + dx, 8], [22 + dx, 32], [31 + dx, 32], [26 + dx, 54], [42 + dx, 28], [33 + dx, 28], [39 + dx, 8]], c, { stroke: P.outline, sw: 2 })).join('')),
  rotor_de_impacto: () => circle(32, 32, 9, P.orange, { sw: 2.5 }) + [0, 1, 2].map((i) => g(rect(-3.5, -24, 7, 15, P.steelL, { r: 2, sw: 2.5 }), `translate(32 32) rotate(${i * 120 + 15})`)).join('') +
    path('M8 22 h10 M6 32 h10 M8 42 h10', 'none', { stroke: P.white, sw: 2.5 }),
  attr_dano: () => poly([[32, 8], [37, 24], [54, 22], [41, 33], [50, 50], [32, 40], [14, 50], [23, 33], [10, 22], [27, 24]], P.danger, { sw: 2.5 }),
  attr_vida: () => path('M32 52 L13 32 a10 10 0 0 1 19 -12 a10 10 0 0 1 19 12 Z', P.danger, { sw: 2.5 }) + path('M14 34 h10 l3 -6 l5 12 l3 -6 h14', 'none', { stroke: P.white, sw: 2.5 }),
  attr_recuperacao: () => path('M32 12 a20 20 0 1 1 -18 11', 'none', { stroke: P.heal, sw: 4 }) + poly([[10, 18], [20, 18], [14, 28]], P.heal, { sw: 1.5 }) +
    path('M28 22 h8 v6 h6 v8 h-6 v6 h-8 v-6 h-6 v-8 h6 Z', P.white, { sw: 2 }),
  attr_coleta: () => path('M18 14 v18 a14 14 0 0 0 28 0 v-18 h-8 v18 a6 6 0 0 1 -12 0 v-18 Z', P.danger, { sw: 2.5 }) + rect(18, 14, 8, 7, P.steelL, { sw: 2 }) + rect(38, 14, 8, 7, P.steelL, { sw: 2 }),
  attr_vel_ataque: () => circle(32, 34, 18, P.white, { sw: 3 }) + line(32, 34, 32, 22, P.outline, 3) + line(32, 34, 41, 38, P.outline, 3) + rect(28, 10, 8, 6, P.steel, { sw: 2 }) + path('M8 24 h8 M6 32 h8', 'none', { stroke: P.amber, sw: 3 }),
  kit_primeiros_socorros: () => rect(12, 18, 40, 32, P.white, { r: 5, sw: 3 }) + rect(24, 12, 16, 8, P.steel, { r: 2, sw: 2.5 }) + rect(28, 24, 8, 20, P.danger, none) + rect(22, 30, 20, 8, P.danger, none),
  apagao: () => glow(circle(32, 32, 22, 'none', { stroke: P.cyan, sw: 3 })) + path('M24 36 a10 10 0 1 1 16 0 v6 h-16 Z', P.graphiteL, { sw: 2.5 }) + rect(25, 42, 14, 6, P.steel, { sw: 2 }) +
    path('M29 26 l4 6 h-4 l4 6', 'none', { stroke: P.amber, sw: 2.5 }),
  turbina_emergencia: () => glow(path('M20 52 q12 -14 24 0 q-6 6 -12 4 q-6 2 -12 -4 Z', P.orange, { stroke: P.orangeD, sw: 2 })) + circle(32, 28, 16, P.graphiteL, { sw: 3 }) +
    [0, 1, 2, 3].map((i) => g(path('M0 0 q8 -5 13 -1 q-7 3 -13 1 Z', P.steelL, { sw: 1.6 }), `translate(32 28) rotate(${i * 90})`)).join(''),
};

export function iconSprites(content) {
  const out = [];
  const rarityOf = new Map(content.abilities.map((a) => [a.id, a.rarity]));
  for (const s of content.specials) rarityOf.set(s.id, 'raro');
  for (const [id, rarity] of rarityOf) {
    const sym = SYMBOLS[id];
    if (!sym) throw new Error(`sem símbolo para o ícone ${id}`);
    out.push({ id: `icon_${id}`, category: 'Icons', w: 64, h: 64, pivot: [0.5, 0.5], svg: svg(64, 64, frame(rarity, sym()), glowFilter('ig', 1.6)) });
  }
  return out;
}
