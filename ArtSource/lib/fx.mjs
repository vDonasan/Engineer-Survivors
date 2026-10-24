import { P } from './palette.mjs';
import { svg, rect, circle, ellipse, path, line, poly, g, glowFilter, radial, linear, bolt } from './svg.mjs';

// Texturas de efeitos (tingidas na Unity quando faz sentido) e coletáveis.
export function fxSprites() {
  const s = (id, w, h, body, defs = '', pivot = [0.5, 0.5]) => ({ id, category: 'Fx', w, h, pivot, svg: svg(w, h, body, defs) });
  const none = { stroke: 'none', sw: 0 };
  return [
    s('fx_glow', 64, 64, circle(32, 32, 32, 'url(#rg)', none), radial('rg', [[0, '#ffffff'], [0.35, '#ffffff', 0.55], [1, '#ffffff', 0]])),
    s('fx_spark', 16, 16, poly([[8, 0], [9.5, 6.5], [16, 8], [9.5, 9.5], [8, 16], [6.5, 9.5], [0, 8], [6.5, 6.5]], '#ffffff', none)),
    s('fx_ring', 128, 128, circle(64, 64, 58, 'none', { stroke: '#ffffff', sw: 6 }) + circle(64, 64, 58, 'none', { stroke: '#ffffff', sw: 14, extra: 'opacity="0.25"' })),
    s('fx_field', 128, 128, circle(64, 64, 62, 'url(#rf)', none) + circle(64, 64, 61, 'none', { stroke: P.cyan, sw: 2.5, extra: 'opacity="0.8" stroke-dasharray="10 6"' }),
      radial('rf', [[0, P.cyan, 0], [0.7, P.cyan, 0.12], [1, P.cyan, 0.35]])),
    s('fx_bolt', 64, 16, rect(0, 2, 64, 12, 'url(#lb)', none) + rect(0, 6.5, 64, 3, '#ffffff', none),
      linear('lb', [[0, '#ffffff', 0], [0.5, '#ffffff', 0.6], [1, '#ffffff', 0]], { x1: 0, y1: 0, x2: 0, y2: 1 })),
    s('fx_laser', 32, 8, rect(0, 0, 32, 8, P.orange, { r: 4, stroke: 'none', sw: 0 }) + rect(2, 2.5, 28, 3, P.amberL, { r: 1.5, stroke: 'none', sw: 0 })),
    s('fx_hostile_bolt', 16, 16, `<g filter="url(#gl)">${circle(8, 8, 4.5, P.danger, { stroke: P.outline, sw: 1.5 })}${circle(7, 7, 1.6, P.dangerL, none)}</g>`, glowFilter('gl', 1.5)),
    s('fx_gem_xp', 16, 16, poly([[8, 1], [14, 7], [8, 15], [2, 7]], P.xp, { stroke: P.outline, sw: 1.5 }) + poly([[8, 3], [11, 7], [8, 7]], P.cyanL, none)),
    s('fx_gem_xp_big', 24, 24, poly([[12, 1], [21, 10], [12, 23], [3, 10]], P.amber, { stroke: P.outline, sw: 2 }) + poly([[12, 4], [17, 10], [12, 10]], P.amberL, none)),
    s('fx_heal', 20, 20, rect(2, 5, 16, 10, P.white, { r: 5, stroke: P.outline, sw: 1.5 }) + rect(8, 7, 4, 6, P.heal, none) + rect(7, 8.5, 6, 3, P.heal, none)),
    s('fx_smoke', 32, 32, circle(12, 18, 8, '#9aa1a9', { stroke: 'none', sw: 0, extra: 'opacity="0.6"' }) + circle(20, 14, 9, '#b5bcc4', { stroke: 'none', sw: 0, extra: 'opacity="0.6"' })),
    s('fx_shadow', 32, 16, ellipse(16, 8, 15, 6, '#000000', { stroke: 'none', sw: 0, extra: 'opacity="0.45"' })),
    s('fx_orbit_arm', 40, 40, circle(20, 20, 13, P.steel, { sw: 2.5 }) +
      [0, 1, 2, 3, 4, 5].map((i) => g(rect(-3, -17, 6, 6, P.steelL, { sw: 2 }), `translate(20 20) rotate(${i * 60})`)).join('') +
      circle(20, 20, 5, P.orange, { sw: 2 })),
    s('fx_arc', 64, 64, `<g filter="url(#ga)">${path(bolt(4, 32, 60, 32, 7, 8, 3), 'none', { stroke: '#ffffff', sw: 3 })}</g>`, glowFilter('ga', 2)),
    s('ui_panel', 64, 64, rect(2, 2, 60, 60, P.graphiteD, { r: 6, stroke: P.steelD, sw: 3 }) + poly([[2, 14], [14, 2], [20, 2], [2, 20]], P.hazard, none)),
    s('ui_button', 64, 32, rect(2, 2, 60, 28, P.graphite, { r: 5, stroke: P.steel, sw: 2.5 }) + rect(4, 4, 56, 6, P.graphiteL, { r: 3, stroke: 'none', sw: 0 })),
  ];
}
