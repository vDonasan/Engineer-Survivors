import { P } from './palette.mjs';
import { svg, rect, circle, ellipse, path, line, poly, g, glowFilter, bolt } from './svg.mjs';

// Profissionais exaustos: capacete/boné, roupas manchadas, olheiras, curativos.
export const SPECS = {
  eletrica: {
    skin: P.skinA, skinL: P.skinAL, skinD: P.skinAD, hair: P.hairA,
    jacket: P.petrol, jacketL: P.petrolL, jacketD: P.petrolD, pants: P.denim, pantsD: P.denimD,
    gloves: P.orange, accent: P.cyan, accent2: P.amber,
    torso: 28, ponytail: true, bandage: true, stubble: false, sweat: false,
    hat: 'hardhat', tool: 'coil',
  },
  mecanica: {
    skin: P.skinB, skinL: P.skinBL, skinD: P.skinBD, hair: P.hairB,
    jacket: P.steelD, jacketL: P.steel, jacketD: P.graphite, pants: P.graphiteL, pantsD: P.graphite,
    gloves: P.boot, accent: P.orange, accent2: P.steelL,
    torso: 36, ponytail: false, bandage: false, stubble: true, sweat: true,
    hat: 'cap', tool: 'wrench',
  },
};

const W = 96, H = 96, CX = 48;

function shadow() {
  return ellipse(CX, 88, 19, 5, P.shadow, { stroke: 'none', sw: 0 });
}

function legs(c, view, frame) {
  const lw = c.torso > 30 ? 11 : 9;
  const gap = c.torso > 30 ? 4 : 3;
  const lx = CX - gap / 2 - lw, rx = CX + gap / 2;
  // quadro: 0 neutro, 1 esquerda à frente, 2 neutro, 3 direita à frente
  const liftL = frame === 1 ? 3 : 0, liftR = frame === 3 ? 3 : 0;
  if (view === 'side') {
    const stride = [0, 6, 0, -6][frame];
    return g(
      rect(CX - 5 - stride * 0.5, 66, 10, 15, c.pantsD) + rect(CX - 7 - stride * 0.5, 79, 13, 7, P.boot, { r: 2 }) +
      rect(CX - 5 + stride * 0.5, 66, 10, 15, c.pants) + rect(CX - 4 + stride * 0.5, 79, 13, 7, P.boot, { r: 2 }));
  }
  return rect(lx, 66 - liftL, lw, 15 + liftL - liftL * 0.3, c.pants) + rect(lx - 1, 79 - liftL * 0.6, lw + 2, 7, P.boot, { r: 2 }) +
    rect(rx, 66 - liftR, lw, 15 + liftR - liftR * 0.3, c.pants) + rect(rx - 1, 79 - liftR * 0.6, lw + 2, 7, P.boot, { r: 2 });
}

function torso(c, view, bob) {
  const tw = view === 'side' ? 22 : c.torso;
  const x = CX - tw / 2, y = 42 + bob;
  let s = rect(x, y, tw, 27, c.jacket, { r: 7 });
  // mancha e rasgo (desgaste)
  s += ellipse(x + tw * 0.7, y + 18, 3.5, 2.2, c.jacketD, { stroke: 'none', sw: 0 });
  s += path(`M${x + 5} ${y + 8} l3 3 l-2 2`, 'none', { stroke: c.jacketD, sw: 1.5 });
  if (c.hat === 'hardhat') {
    // faixa refletiva e gola ciano
    s += rect(x, y + 15, tw, 4, P.hazard, { stroke: P.outline, sw: 1.5 });
    if (view !== 'back') s += path(`M${CX - 6} ${y} L${CX} ${y + 7} L${CX + 6} ${y}`, 'none', { stroke: c.accent, sw: 2.5 });
  } else if (view !== 'back') {
    // avental de couro com alças
    s += rect(CX - tw / 2 + 6, y + 4, tw - 12, 24, P.rust, { r: 3, sw: 2 });
    s += ellipse(CX + 3, y + 16, 3, 2, P.rustD, { stroke: 'none', sw: 0 });
    s += line(CX - tw / 2 + 8, y, CX - tw / 2 + 8, y + 5, P.rustD, 2.5) + line(CX + tw / 2 - 8, y, CX + tw / 2 - 8, y + 5, P.rustD, 2.5);
  }
  s += rect(x, y + 23, tw, 4, P.graphiteD, { sw: 1.5 });
  if (view !== 'back') s += rect(x + tw - 9, y + 22, 6, 6, P.hazardD, { r: 1, sw: 1.5 });
  return s;
}

function arm(c, x, y, len, angle, gloveColor) {
  return g(rect(-4.5, 0, 9, len, c.jacket, { r: 4 }) + circle(0, len + 1, 5, gloveColor, { sw: 2.5 }), `translate(${x} ${y}) rotate(${angle})`);
}

function head(c, view, bob, face) {
  const y = 30 + bob;
  let s = '';
  if (view === 'back' && c.ponytail) s += path(`M${CX - 2} ${y + 6} q-6 12 2 18 q6 -8 2 -18 Z`, c.hair, { sw: 2.5 });
  s += rect(CX - 4, y + 10, 8, 6, c.skinD, { sw: 2 }); // pescoço
  if (view === 'side') {
    s += ellipse(CX + 1, y, 12, 13.5, c.skin);
    if (c.ponytail) s += path(`M${CX - 10} ${y - 2} q-10 6 -6 18 q6 -6 6 -14 Z`, c.hair, { sw: 2.5 });
    s += path(`M${CX + 12} ${y + 1} l4 3 l-4 2`, c.skin, { sw: 2 }); // nariz
    s += circle(CX + 7, y - 1, 1.8, P.outline, { stroke: 'none', sw: 0 });
    s += path(`M${CX + 4} ${y + 3} q3 2 6 0`, 'none', { stroke: P.bags, sw: 1.5 });
    s += line(CX + 7, y + 8, CX + 11, y + 8, P.outline, 1.6);
    if (c.stubble) for (let i = 0; i < 5; i++) s += circle(CX + 4 + i * 2, y + 10 - (i % 2), 0.6, c.skinD, { stroke: 'none', sw: 0 });
  } else if (view === 'front') {
    s += ellipse(CX, y, 13, 14, c.skin);
    s += face(c, y);
  } else {
    // nuca: cabelo cobre a cabeça; orelhas aparecem dos lados
    s += ellipse(CX - 13, y + 1, 2.5, 4, c.skinD, { sw: 1.5 }) + ellipse(CX + 13, y + 1, 2.5, 4, c.skinD, { sw: 1.5 });
    s += ellipse(CX, y, 13, 14, c.hair);
    s += path(`M${CX - 7} ${y + 6} q7 4 14 0`, 'none', { stroke: c.skinD, sw: 1.5 });
  }
  s += hat(c, view, y);
  return s;
}

function hat(c, view, y) {
  if (c.hat === 'hardhat') {
    let s = '';
    if (view === 'side') {
      s += path(`M${CX - 12} ${y - 4} q2 -14 14 -14 q12 0 13 14 Z`, P.hazard);
      s += rect(CX - 10, y - 6, 28, 4, P.hazardD, { r: 2, sw: 2 });
    } else {
      s += path(`M${CX - 14} ${y - 4} q0 -15 14 -15 q14 0 14 15 Z`, P.hazard);
      s += rect(CX - 17, y - 6, 34, 4, P.hazardD, { r: 2, sw: 2 });
      if (view === 'front') {
        s += poly([[CX - 2, y - 17], [CX + 3, y - 12], [CX, y - 12], [CX + 2, y - 7], [CX - 4, y - 13], [CX - 1, y - 13]], c.accent, { sw: 1.2 });
        // óculos de proteção presos ao capacete
        s += rect(CX - 11, y - 9, 9, 5, P.cyanD, { r: 2, sw: 1.8 }) + rect(CX + 2, y - 9, 9, 5, P.cyanD, { r: 2, sw: 1.8 });
      }
    }
    return s;
  }
  // boné de soldador (bandana) + óculos na testa
  let s = '';
  if (view === 'side') {
    s += path(`M${CX - 12} ${y - 2} q0 -14 13 -14 q12 0 12 12 Z`, c.accent);
    s += circle(CX + 9, y - 7, 3.5, P.steelL, { sw: 2 }) + circle(CX + 9, y - 7, 1.8, P.cyan, { stroke: 'none', sw: 0 });
  } else {
    s += path(`M${CX - 13} ${y - 2} q0 -15 13 -15 q13 0 13 15 Z`, c.accent);
    if (view === 'back') s += path(`M${CX - 3} ${y - 3} l-3 9 M${CX + 3} ${y - 3} l3 9`, 'none', { stroke: P.orangeD, sw: 3 });
    else s += circle(CX - 6, y - 7, 4, P.steelL, { sw: 2 }) + circle(CX + 6, y - 7, 4, P.steelL, { sw: 2 }) +
      circle(CX - 6, y - 7, 2.2, P.cyan, { stroke: 'none', sw: 0 }) + circle(CX + 6, y - 7, 2.2, P.cyan, { stroke: 'none', sw: 0 });
  }
  return s;
}

/** Rosto padrão: cansado (olheiras, boca reta). */
function tiredFace(c, y) {
  let s = '';
  if (c.ponytail) s += path(`M${CX - 12} ${y - 3} q4 -4 10 -3 l-2 4 Z`, c.hair, { sw: 1.5 });
  s += circle(CX - 5, y + 1, 1.9, P.outline, { stroke: 'none', sw: 0 }) + circle(CX + 5, y + 1, 1.9, P.outline, { stroke: 'none', sw: 0 });
  s += path(`M${CX - 8} ${y + 4} q3 2 6 0 M${CX + 2} ${y + 4} q3 2 6 0`, 'none', { stroke: P.bags, sw: 1.5 });
  s += line(CX - 8, y - 3, CX - 3, y - 2, P.outline, 1.6) + line(CX + 3, y - 2, CX + 8, y - 3, P.outline, 1.6);
  s += path(`M${CX - 3} ${y + 8} q3 -1 6 0`, 'none', { stroke: P.outline, sw: 1.8 });
  if (c.bandage) s += g(rect(-3.5, -1.5, 7, 3, P.cloth, { sw: 1 }), `translate(${CX + 8} ${y + 6}) rotate(-25)`);
  if (c.stubble) for (let i = 0; i < 7; i++) s += circle(CX - 6 + i * 2, y + 11 - (i % 2), 0.6, c.skinD, { stroke: 'none', sw: 0 });
  if (c.sweat) s += path(`M${CX + 11} ${y - 2} q2 3 0 5 q-2 -2 0 -5 Z`, P.cyanL, { sw: 1 });
  return s;
}

function hurtFace(c, y) {
  return path(`M${CX - 8} ${y} l5 2 l-5 2 M${CX + 8} ${y} l-5 2 l5 2`, 'none', { stroke: P.outline, sw: 2 }) +
    path(`M${CX - 4} ${y + 9} q4 -4 8 0`, 'none', { stroke: P.outline, sw: 2 }) +
    path(`M${CX + 11} ${y - 2} q2 3 0 5 q-2 -2 0 -5 Z`, P.cyanL, { sw: 1 });
}

function downedFace(c, y) {
  return path(`M${CX - 8} ${y - 1} l5 5 M${CX - 3} ${y - 1} l-5 5 M${CX + 3} ${y - 1} l5 5 M${CX + 8} ${y - 1} l-5 5`, 'none', { stroke: P.outline, sw: 2 }) +
    path(`M${CX - 4} ${y + 9} q2 -2 4 0 q2 2 4 0`, 'none', { stroke: P.outline, sw: 1.8 });
}

function tool(c, view, bob, pose) {
  if (c.tool === 'wrench') {
    if (view === 'front' && pose !== 'attack') return '';
    const t = pose === 'attack' ? `translate(${CX + 22} ${30 + bob}) rotate(-35)` : `translate(${CX + 4} ${44 + bob}) rotate(${view === 'side' ? -60 : 35})`;
    return g(rect(-3, 0, 6, 26, P.steelL, { r: 2 }) + path('M-8 0 a8 8 0 1 1 16 0 l-4 0 l0 -5 l-8 0 l0 5 Z', P.steel), t);
  }
  // bobina na mão (frente/ataque) ou bateria nas costas
  if (view === 'back') return rect(CX - 9, 46 + bob, 18, 14, P.graphiteL, { r: 3 }) + rect(CX - 6, 49 + bob, 12, 3, c.accent, { sw: 1 }) + rect(CX - 6, 54 + bob, 8, 3, c.accent, { sw: 1 });
  if (view === 'side') return rect(CX + 8, 56 + bob, 14, 7, P.steel, { r: 2 }) + circle(CX + 23, 59.5 + bob, 3, c.accent, { sw: 1.5 });
  return '';
}

function figure(spec, view, frame, pose = 'walk') {
  const c = SPECS[spec];
  const bob = pose === 'walk' ? [0, -2, 0, -2][frame] : 0;
  const swing = pose === 'walk' ? [0, 6, 0, -6][frame] : 0;
  const face = pose === 'hurt' ? hurtFace : tiredFace;
  let s = shadow();

  if (view === 'back' && c.tool === 'wrench') s += tool(c, view, bob, pose);
  s += legs(c, view, frame);

  const tw = view === 'side' ? 22 : c.torso;
  if (view === 'side') {
    s += arm(c, CX - 2, 45 + bob, 19, -swing * 3, c.gloves) + torso(c, view, bob) + tool(c, view, bob, pose);
    s += arm(c, CX + 2, 45 + bob, 19, swing * 3, c.gloves);
  } else {
    const leftAngle = pose === 'special' ? 35 : 8 + swing;
    const rightAngle = pose === 'attack' ? -150 : pose === 'special' ? -35 : -8 + swing;
    s += arm(c, CX - tw / 2 - 1, 45 + bob, 20, leftAngle, c.gloves);
    s += arm(c, CX + tw / 2 + 1, 45 + bob, 20, rightAngle, c.gloves);
    s += torso(c, view, bob);
    if (view === 'back' && c.tool === 'coil') s += tool(c, view, bob, pose);
  }
  s += head(c, view, bob, face);
  if (view === 'front' && pose === 'attack') s += tool(c, view, bob, pose);
  return s;
}

function attackFx(spec) {
  const c = SPECS[spec];
  if (c.tool === 'coil')
    return `<g filter="url(#glow)">` + path(bolt(CX + 26, 18, CX + 44, 4, 5, 3, 7), 'none', { stroke: c.accent, sw: 3 }) +
      circle(CX + 26, 18, 5, P.cyanL, { stroke: c.accent, sw: 2 }) + `</g>`;
  return path(`M${CX + 6} ${8} A30 30 0 0 1 ${CX + 40} ${40}`, 'none', { stroke: P.white, sw: 3, extra: 'stroke-dasharray="6 5" opacity="0.8"' });
}

function specialFx(spec, frame) {
  const c = SPECS[spec];
  if (c.tool === 'coil') {
    let s = `<g filter="url(#glow)">` + circle(CX, 52, 36, 'none', { stroke: c.accent, sw: 2.5, extra: 'opacity="0.85"' });
    for (let i = 0; i < 4; i++) {
      const a = (i * Math.PI) / 2 + frame * 0.6;
      s += path(bolt(CX + Math.cos(a) * 12, 52 + Math.sin(a) * 12, CX + Math.cos(a) * 36, 52 + Math.sin(a) * 36, 4, 3, 11 + i + frame * 5), 'none', { stroke: i % 2 ? c.accent2 : c.accent, sw: 2.2 });
    }
    return s + `</g>`;
  }
  let s = '';
  for (let i = 0; i < 3; i++) {
    const y = 50 + i * 8 + frame * 3;
    s += path(`M${CX - 30} ${y} q-8 ${3 - frame * 2} -14 ${1 + i}`, 'none', { stroke: i === 1 ? c.accent : P.steelL, sw: 3, extra: 'opacity="0.85"' });
    s += path(`M${CX + 30} ${y} q8 ${3 - frame * 2} 14 ${1 + i}`, 'none', { stroke: i === 1 ? c.accent : P.steelL, sw: 3, extra: 'opacity="0.85"' });
  }
  return `<g filter="url(#glow)">${s}</g>` + path(`M${CX - 8} 86 q8 -12 16 0 q-8 6 -16 0 Z`, P.orangeL, { stroke: c.accent, sw: 2 });
}

/** Lista de sprites de personagem: { id, w, h, pivot, svg }. */
export function characterSprites() {
  const out = [];
  const defs = glowFilter('glow', 2.5);
  for (const spec of Object.keys(SPECS)) {
    for (const view of ['front', 'back', 'side'])
      for (let f = 0; f < 4; f++)
        out.push({ id: `char_${spec}_walk_${view}_${f}`, category: 'Characters', w: W, h: H, pivot: [0.5, 0.1], svg: svg(W, H, figure(spec, view, f), defs) });
    out.push({ id: `char_${spec}_attack`, category: 'Characters', w: W, h: H, pivot: [0.5, 0.1], svg: svg(W, H, figure(spec, 'front', 0, 'attack') + attackFx(spec), defs) });
    for (let f = 0; f < 2; f++)
      out.push({ id: `char_${spec}_special_${f}`, category: 'Characters', w: W, h: H, pivot: [0.5, 0.1], svg: svg(W, H, specialFx(spec, f) + figure(spec, 'front', 0, 'special'), defs) });
    out.push({ id: `char_${spec}_hurt`, category: 'Characters', w: W, h: H, pivot: [0.5, 0.1], svg: svg(W, H, figure(spec, 'front', 0, 'hurt'), defs) });
    const c = SPECS[spec];
    const lying = g(legs(c, 'front', 0) + torso(c, 'front', 0) + arm(c, CX - c.torso / 2 - 1, 45, 20, 20, c.gloves) +
      arm(c, CX + c.torso / 2 + 1, 45, 20, -20, c.gloves) + head(c, 'front', 0, downedFace), `rotate(-90 ${CX} 58) translate(0 -6)`);
    out.push({ id: `char_${spec}_downed`, category: 'Characters', w: W, h: H, pivot: [0.5, 0.1], svg: svg(W, H, ellipse(CX, 80, 34, 7, P.shadow, { stroke: 'none', sw: 0 }) + lying, defs) });
  }
  return out;
}
