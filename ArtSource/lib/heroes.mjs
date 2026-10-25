// Os dez personagens jogáveis, desenhados a partir das fotos de referência (boné, cabelo, óculos,
// bigode, roupa), no mesmo corpo-base 32×32 das engenharias. Jean anda montado num porco.
import { C } from './palette.mjs';
import { Px } from './pixel.mjs';
import { ellipse, rect, line } from './draw.mjs';
import { defineSpec, framesFor, CHAR_ANIMS } from './characters.mjs';

export const HEROES = ['staut', 'miyuki', 'giulia', 'breno', 'artur', 'badotti', 'garcia', 'jean', 'fufu', 'donasa'];

// Cabelo comprido (cortinas laterais e costas) reaproveitado por Miyuki e Giulia.
const curtains = (from, to) => Object.fromEntries([...Array(to - from + 1).keys()].map((i) => [from + i, [[9, 'OHH'], [20, 'HHO']]]));
const backHair = (from, to) => Object.fromEntries([...Array(to - from + 1).keys()].map((i) => [from + i, [[10, i === to - from ? 'OhhhhhhhhhhO' : 'OHHHHHHHHHHO']]]));
const sideHair = (from, to) => Object.fromEntries([...Array(to - from + 1).keys()].map((i) => [from + i, [[16, i === to - from ? 'hhhhO' : 'HHHHO']]]));
// Óculos de aro grosso retangular (Fufu) ou fino redondo (Donasa): mesmo desenho, cor do aro muda.
const glassesFront = { 11: [[12, 'MMM'], [17, 'MMM']], 12: [[12, 'MEMMMMEM']], 13: [[12, 'MEM'], [17, 'MEM']], 14: [[12, 'MMM'], [17, 'MMM']] };
const glassesSide = { 11: [[10, 'MMM']], 12: [[10, 'MEMMMMM']], 13: [[10, 'MMM']] };

const LOOKS = {
  // boné azul-marinho com logo vermelho, cabelo castanho ondulado saindo pelos lados, bochechas rosadas, jaqueta escura com zíper
  staut: {
    roles: { S: C.skinD3, s: C.skinD2, H: C.hairBrown, h: C.black, C: C.denim1, c: C.black, k: C.denim2, J: C.gray1, j: C.black, L: C.gray2, P: C.denim2, p: C.denim1, B: C.black, G: C.skinD2, A: C.red2, a: C.pink2, N: C.red1, X: C.gray1 },
    down: { 4: [[12, 'OOOOOOOO']], 5: [[11, 'OkCCCCCCCO']], 6: [[10, 'OkCCCAACCCcO']], 7: [[10, 'OCCCCAACCCcO']], 8: [[8, 'OccccccccccccccO']], 10: [[8, 'OH'], [22, 'HO']], 11: [[8, 'OH'], [22, 'HO']], 12: [[9, 'O'], [22, 'O']], 14: [[13, 'a'], [18, 'a']], 18: [[15, 'M']], 19: [[15, 'M']], 20: [[15, 'M']], 21: [[15, 'M']], 22: [[15, 'M']] },
    up: { 4: [[12, 'OOOOOOOO']], 5: [[11, 'OkCCCCCCCO']], 6: [[10, 'OkCCCCCCCCcO']], 7: [[10, 'OCCCCCCCCCcO']], 8: [[10, 'OccccWWccccO']], 10: [[8, 'OH'], [22, 'HO']], 11: [[8, 'OH'], [22, 'HO']] },
    left: { 4: [[12, 'OOOOOOO']], 5: [[11, 'OkCCCCCO']], 6: [[10, 'OkCACCCCcO']], 7: [[10, 'OCCACCCCcO']], 8: [[6, 'OcccccccccccccO']], 10: [[21, 'HO']], 11: [[21, 'HO']], 14: [[12, 'a']] },
  },
  // cabelo preto liso e comprido, jaqueta de couro preta, batom vermelho
  miyuki: {
    roles: { S: C.skinD2, s: C.skinC3, H: C.hairBlack, h: C.black, J: C.black, j: C.outline, L: C.gray2, P: C.denim1, p: C.black, B: C.black, G: C.skinC3, A: C.gray3, N: C.red2, X: C.black },
    down: { ...curtains(10, 16), 17: [[8, 'OHH'], [12, 'L'], [19, 'L'], [21, 'HHO']], 18: [[8, 'OHH'], [12, 'A'], [21, 'HHO']], 19: [[8, 'OHh'], [21, 'hHO']], 20: [[19, 'A']] },
    up: { ...backHair(15, 21) },
    left: { ...sideHair(15, 20), 18: [[12, 'L']] },
  },
  // cabelo castanho-escuro muito comprido, ombros de fora, blusa azul, colar dourado
  giulia: {
    roles: { S: C.skinC3, s: C.skinC2, H: C.hairBrown, h: C.hairBlack, J: C.cyan1, j: C.denim2, L: C.cyan2, P: C.denim2, p: C.denim1, B: C.black, G: C.skinC2, A: C.yellow2, N: C.red1, X: C.cyan1 },
    down: { ...curtains(10, 16), 17: [[8, 'OHH'], [11, 'SSS'], [18, 'SSS'], [21, 'HHO']], 18: [[8, 'OHH'], [14, 'A'], [17, 'A'], [21, 'HHO']], 19: [[8, 'OHH'], [15, 'AA'], [21, 'HHO']], 20: [[8, 'OHh'], [21, 'hHO']] },
    up: { ...backHair(15, 22), 17: [[9, 'S'], [22, 'S']] },
    left: { ...sideHair(15, 22), 17: [[13, 'SS']], 18: [[12, 'A']] },
  },
  // cabelo castanho bagunçado espetado, pele clara, camisa polo branca
  breno: {
    roles: { S: C.skinD2, s: C.skinC3, H: C.hairBrown, h: C.rust1, J: C.white, j: C.offwhite, L: C.gray5, P: C.denim2, p: C.denim1, B: C.gray4, G: C.skinC3, A: C.gray4, X: C.white },
    down: { 5: [[12, 'O.O..O.O']], 6: [[11, 'OHOHHOHHO']], 7: [[11, 'OHHHHHHHHO']], 9: [[13, 'h'], [18, 'h']], 17: [[12, 'LL'], [18, 'LL']], 18: [[15, 'jj']], 19: [[15, 'A']] },
    up: { 5: [[12, 'O.O..O.O']], 6: [[11, 'OHOHHOHHO']], 7: [[11, 'OHHHHHHHHO']] },
    left: { 5: [[12, 'O.O.O']], 6: [[11, 'OHOHOHO']], 7: [[11, 'OHHHHHHHO']], 17: [[11, 'OL']] },
  },
  // cabelo escuro ondulado até a nuca, bigode, pele morena, jaqueta branca com bordado vermelho
  artur: {
    roles: { S: C.skinC2, s: C.skinC1, H: C.hairBrown, h: C.black, J: C.white, j: C.offwhite, L: C.gray5, P: C.denim1, p: C.black, B: C.black, G: C.skinC1, A: C.red2, a: C.yellow3, N: C.rust1, X: C.white },
    down: { 7: [[11, 'OHHHHHHHHO']], 10: [[9, 'OH'], [21, 'HO']], 11: [[9, 'OH'], [21, 'HO']], 12: [[9, 'OH'], [21, 'HO']], 13: [[9, 'OH'], [21, 'HO']], 14: [[9, 'OH'], [21, 'HO']], 15: [[10, 'OH'], [13, 'hhhhhh'], [20, 'HO']], 16: [[10, 'O'], [15, 'NN'], [21, 'O']], 17: [[13, 'A'], [18, 'A']], 18: [[13, 'AaAAaA']], 19: [[13, 'AAaaAA']], 20: [[13, 'aAAAAa']], 21: [[13, 'AAaaAA']], 22: [[13, 'AaAAaA']] },
    up: { 7: [[11, 'OHHHHHHHHO']], 15: [[10, 'OHHHHHHHHHHO']], 16: [[10, 'OhhHHHHHHhhO']] },
    left: { 7: [[11, 'OHHHHHHHO']], 14: [[11, 'hh']], 15: [[16, 'HHHO']], 16: [[16, 'hhhO']], 18: [[11, 'AaA']], 19: [[11, 'aAa']] },
  },
  // cabelo castanho-escuro curto e volumoso, sorriso largo, camiseta preta com um robozinho
  badotti: {
    roles: { S: C.skinD2, s: C.skinC3, H: C.hairBrown, h: C.black, J: C.black, j: C.outline, L: C.gray1, P: C.denim2, p: C.denim1, B: C.gray5, G: C.skinC3, A: C.cyan2, N: C.white, X: C.black },
    down: { 6: [[12, 'OOOOOOOO']], 7: [[11, 'OHHHHHHHHO']], 10: [[12, 'HHH']], 15: [[13, 'OWWWWO']], 19: [[14, 'OAAO']], 20: [[14, 'AOOA']], 21: [[14, 'OAAO']] },
    up: { 6: [[12, 'OOOOOOOO']], 7: [[11, 'OHHHHHHHHO']] },
    left: { 6: [[12, 'OOOOOO']], 7: [[11, 'OHHHHHHHO']], 14: [[11, 'W']] },
  },
  // cabelo preto jogado para o lado, camisa social branca, calça escura
  garcia: {
    roles: { S: C.skinD3, s: C.skinD2, H: C.hairBlack, h: C.black, J: C.white, j: C.offwhite, L: C.gray5, P: C.black, p: C.gray1, B: C.black, G: C.skinD2, A: C.gray4, X: C.white },
    down: { 6: [[12, 'OOOOOOO']], 7: [[11, 'OHHHHHHHHO']], 10: [[12, 'HHHHH']], 11: [[12, 'HH']], 17: [[12, 'L'], [19, 'L']], 18: [[15, 'A']], 20: [[15, 'A']], 22: [[15, 'A']] },
    up: { 6: [[12, 'OOOOOOO']], 7: [[11, 'OHHHHHHHHO']] },
    left: { 6: [[11, 'OOOOO']], 7: [[10, 'OHHHHHHHO']], 10: [[11, 'HH']] },
  },
  // topete castanho, óculos de aviador escuros, sorriso, camiseta branca
  jean: {
    roles: { S: C.skinD2, s: C.skinC3, H: C.hairBrown, h: C.rust1, J: C.white, j: C.offwhite, L: C.gray5, P: C.denim2, p: C.denim1, B: C.black, G: C.skinC3, A: C.gray1, M: C.gray5, N: C.white, X: C.white },
    down: { 4: [[13, 'OOOOO']], 5: [[12, 'OHHHHHO']], 6: [[11, 'OHHHHHHHO']], 7: [[11, 'OHHHHHHHHO']], 12: [[11, 'MAAAMMAAAM']], 13: [[12, 'AAA'], [17, 'AAA']], 15: [[13, 'OWWWWO']] },
    up: { 4: [[13, 'OOOOO']], 5: [[12, 'OHHHHHO']], 6: [[11, 'OHHHHHHHO']], 7: [[11, 'OHHHHHHHHO']] },
    left: { 4: [[11, 'OOOO']], 5: [[10, 'OHHHHO']], 6: [[10, 'OHHHHHHO']], 7: [[11, 'OHHHHHHO']], 12: [[10, 'AAAMMM']], 13: [[10, 'AA']] },
  },
  // muito musculoso, pele bronzeada, óculos pretos retangulares, regata, pulseira verde
  fufu: {
    roles: { S: C.skinB3, s: C.skinB2, H: C.hairBrown, h: C.rust1, J: C.gray1, j: C.black, L: C.gray2, P: C.gray2, p: C.gray1, B: C.black, G: C.skinB2, A: C.green2, M: C.outline, X: C.gray1 },
    down: { 7: [[11, 'OHHHHHHHHO']], ...glassesFront, 17: [[6, 'OOSSS'], [21, 'SSSOO']], 18: [[5, 'OSSSSO'], [21, 'OSSSSO']], 19: [[5, 'OSSsSO'], [21, 'OSsSSO']], 20: [[5, 'OSSSsO'], [21, 'OsSSSO']], 21: [[5, 'OSSSSO'], [21, 'OAAAAO']], 22: [[5, 'OGGGGO'], [21, 'OGGGGO']], 23: [[6, 'OOOO'], [22, 'OOOO']] },
    up: { 7: [[11, 'OHHHHHHHHO']], 17: [[6, 'OOSSS'], [21, 'SSSOO']], 18: [[5, 'OSSSSO'], [21, 'OSSSSO']], 19: [[5, 'OSSsSO'], [21, 'OsSSSO']], 20: [[5, 'OSSSSO'], [21, 'OSSSSO']], 21: [[5, 'OAAAAO'], [21, 'OSSSSO']], 22: [[5, 'OGGGGO'], [21, 'OGGGGO']], 23: [[6, 'OOOO'], [22, 'OOOO']] },
    left: { 7: [[11, 'OHHHHHHO']], ...glassesSide, 17: [[11, 'OSSSO']], 18: [[10, 'OSSSSSO']], 19: [[10, 'OSSsSSO']], 20: [[10, 'OSSSsSO']], 21: [[10, 'OAAAASO']], 22: [[10, 'OGGGGGO']] },
  },
  // cabelo castanho bagunçado, óculos redondos finos, barba rala, camisa vinho
  donasa: {
    roles: { S: C.skinD2, s: C.skinC3, H: C.hairBrown, h: C.rust1, J: C.red1, j: C.rust1, L: C.red2, P: C.denim1, p: C.black, B: C.gray3, G: C.skinC3, M: C.gray4, X: C.red1 },
    down: { 6: [[12, 'O.OO.O.O']], 7: [[11, 'OHHHHHHHHO']], ...glassesFront, 15: [[12, 'h'], [19, 'h']], 16: [[13, 'h.hh.h']], 17: [[13, 'L'], [18, 'L']] },
    up: { 6: [[12, 'O.OO.O.O']], 7: [[11, 'OHHHHHHHHO']] },
    left: { 6: [[12, 'O.O.O']], 7: [[11, 'OHHHHHHO']], ...glassesSide, 15: [[12, 'hh']] },
  },
};

for (const id of HEROES) defineSpec(id, { ...LOOKS[id], roles: { A: C.amber, a: C.yellow2, ...LOOKS[id].roles } });

// ---------- Donasa: boca escancarada nos quadros de ataque/especial ----------

function shout(p) {
  const out = p.clone();
  const set = (x, y, c) => out.set(x, y, c);
  for (const x of [14, 15, 16, 17]) set(x, 15, C.outline);
  set(14, 16, C.outline); set(15, 16, C.red1); set(16, 16, C.red1); set(17, 16, C.outline);
  return out;
}

// ---------- Jean: montado no porco ----------

const PIG = { O: C.outline, P: C.pink2, p: C.pink1, q: C.pink3 };

function pigSide(f) {
  const p = new Px(32, 32);
  // pernas (alternam ao andar)
  const lift = [f % 2 === 0 ? 0 : 1, f % 2 === 0 ? 1 : 0];
  for (const [x, l] of [[11, lift[0]], [14, lift[1]], [21, lift[1]], [24, lift[0]]]) {
    rect(p, x, 27 - l, 3, 5 - (l ? 1 : 0), C.outline);
    rect(p, x + 1, 27 - l, 1, 4 - (l ? 1 : 0), C.pink1);
  }
  ellipse(p, 18, 24, 9, 4.5, C.pink2);
  rect(p, 12, 26, 13, 1, C.pink1); // barriga
  ellipse(p, 9, 22.5, 4, 4, C.pink2);
  rect(p, 3, 21, 4, 4, C.outline); rect(p, 4, 22, 2, 2, C.pink3); p.set(4, 22, C.pink1); // focinho
  p.set(8, 21, C.black);
  line(p, 10, 18, 12, 18, C.outline); p.set(11, 19, C.pink1); p.set(10, 19, C.outline); p.set(12, 19, C.outline); // orelha
  p.set(27, 21, C.outline); p.set(28, 20, C.outline); p.set(29, 21, C.outline); p.set(28, 22, C.outline); // rabinho
  return p;
}

function pigFront(f) {
  const p = new Px(32, 32);
  const lift = f % 2 === 0 ? [0, 1] : [1, 0];
  rect(p, 10, 28 - lift[0], 3, 4 - lift[0], C.outline); rect(p, 11, 28 - lift[0], 1, 3 - lift[0], C.pink1);
  rect(p, 19, 28 - lift[1], 3, 4 - lift[1], C.outline); rect(p, 20, 28 - lift[1], 1, 3 - lift[1], C.pink1);
  ellipse(p, 15.5, 24, 8, 4.5, C.pink2);
  ellipse(p, 15.5, 24.5, 5.5, 4, C.pink2);
  for (const [x0, d] of [[9, 1], [22, -1]]) { p.set(x0, 19, C.outline); p.set(x0 + d, 20, C.pink1); p.set(x0, 20, C.outline); p.set(x0 + 2 * d, 21, C.outline); }
  p.set(12, 23, C.black); p.set(19, 23, C.black);
  ellipse(p, 15.5, 26, 2.5, 1.5, C.pink3);
  p.set(14, 26, C.pink1); p.set(17, 26, C.pink1);
  return p;
}

function pigBack(f) {
  const p = pigFront(f);
  for (let y = 21; y <= 28; y++) for (let x = 9; x <= 22; x++) if (p.get(x, y) && p.get(x, y) !== C.outline) p.set(x, y, C.pink2);
  p.set(15, 25, C.outline); p.set(16, 24, C.outline); p.set(17, 25, C.outline); p.set(16, 26, C.outline); // rabinho
  return p;
}

/** Corpo do Jean sem as pernas (linhas 0–23), subido para sentar no porco. */
function rider(frame, up) {
  const out = new Px(32, 32);
  for (let y = 0; y < 24; y++) for (let x = 0; x < 32; x++) { const v = frame.get(x, y); if (v) out.set(x, y - up, v); }
  return out;
}

function mount(frame, view, f) {
  const roles = LOOKS.jean.roles;
  if (view === 'left') {
    const out = pigSide(f);
    out.blit(rider(frame, 6));
    rect(out, 14, 18, 3, 6, C.outline); rect(out, 15, 18, 1, 5, roles.P); rect(out, 14, 23, 3, 2, roles.B);
    return out;
  }
  const out = rider(frame, 5);
  if (view === 'down') for (const x of [8, 22]) { rect(out, x, 17, 3, 7, C.outline); rect(out, x + 1, 17, 1, 5, roles.P); rect(out, x, 22, 3, 2, roles.B); }
  return out.blit(view === 'up' ? pigBack(f) : pigFront(f));
}

export function heroFrames(id) {
  const frames = framesFor(id);
  if (id === 'donasa') for (const k of ['attack_down', 'special_down']) frames[k] = frames[k].map(shout);
  if (id === 'jean') {
    for (const [anim, { dirs }] of Object.entries(CHAR_ANIMS)) {
      if (anim === 'downed' || anim === 'revive') continue; // caiu do porco
      for (const d of dirs) {
        if (d === 'right') continue;
        frames[`${anim}_${d}`] = frames[`${anim}_${d}`].map((px, i) => mount(px, d, anim === 'walk' ? i : 0));
      }
    }
    frames.idle_right = frames.idle_left.map((p) => p.flipX());
    frames.walk_right = frames.walk_left.map((p) => p.flipX());
  }
  return frames;
}

export function heroSprites() {
  const out = [];
  for (const id of HEROES) {
    const frames = heroFrames(id);
    for (const [anim, { dirs, frames: n }] of Object.entries(CHAR_ANIMS))
      for (const d of dirs) {
        const list = frames[`${anim}_${d}`];
        if (!list || list.length !== n) throw new Error(`animação incompleta: ${id} ${anim} ${d}`);
        list.forEach((px, i) => out.push({ id: `char_${id}_${anim}_${d}_${i}`, category: 'Characters', sub: id, w: 32, h: 32, pivot: [16, 1], px }));
      }
  }
  return out;
}

export function heroRoles(id) { return LOOKS[id].roles; }

// ---------- Retratos 64×64 (9 emoções), a partir das mesmas referências ----------

import { box, disc, stamp } from './draw.mjs';
import { EMOTIONS, BG, EYES, MOUTH, extras } from './portraits.mjs';

function heroPortrait(id, e) {
  const r = LOOKS[id].roles;
  const p = new Px(64, 64);
  box(p, 0, 0, 64, 64, BG[e]);
  const longHair = id === 'miyuki' || id === 'giulia';
  if (longHair) { ellipse(p, 31.5, 36, 21, 27, r.H); rect(p, 11, 36, 42, 28, r.H); }
  ellipse(p, 31.5, 64, id === 'fufu' ? 31 : 26, 14, r.J);
  if (id === 'fufu') { ellipse(p, 8, 60, 9, 10, r.S); ellipse(p, 55, 60, 9, 10, r.S); }
  if (id === 'giulia') { ellipse(p, 31.5, 64, 26, 10, r.S); rect(p, 14, 58, 36, 6, r.J); }
  if (id === 'artur') { box(p, 25, 52, 14, 12, C.red2); for (let i = 0; i < 3; i++) rect(p, 27 + i * 4, 55, 2, 2, C.yellow3); }
  if (id === 'staut' || id === 'garcia' || id === 'breno' || id === 'donasa') rect(p, 31, 52, 2, 12, r.L);
  box(p, 27, 44, 10, 8, r.s);
  if (id === 'giulia') { line(p, 26, 50, 31, 55, C.yellow2); line(p, 37, 50, 32, 55, C.yellow2); disc(p, 31.5, 56, 1, C.yellow3, null); }
  ellipse(p, 31.5, 29, 15, 17, r.S);
  // cabelo
  if (id === 'jean') ellipse(p, 31.5, 11, 13, 9, r.H);
  else if (id === 'donasa') { ellipse(p, 31.5, 15, 16, 8, r.H); for (const x of [14, 49]) ellipse(p, x, 20, 4, 5, r.H); }
  else if (id === 'breno') { ellipse(p, 31.5, 15, 15, 7, r.H); for (let x = 18; x <= 44; x += 5) { line(p, x, 9, x + 2, 4, C.outline); line(p, x + 1, 9, x + 2, 5, r.H); } }
  else if (id !== 'staut') ellipse(p, 31.5, 16, 15, 7, r.H);
  if (id === 'artur') { ellipse(p, 16, 25, 4, 8, r.H); ellipse(p, 47, 25, 4, 8, r.H); }
  if (id === 'garcia') ellipse(p, 24, 19, 9, 4, r.H);
  if (id === 'badotti') ellipse(p, 31.5, 13, 15, 6, r.H);
  if (longHair) { rect(p, 16, 22, 3, 22, r.H); rect(p, 45, 22, 3, 22, r.H); }
  ellipse(p, 31.5, 29, 15, 17, null);
  for (const x of [15, 47]) ellipse(p, x, 31, 2, 4, r.s);
  if (id === 'staut') {
    ellipse(p, 31.5, 15, 16, 9, C.denim1); rect(p, 10, 19, 44, 4, C.outline); rect(p, 11, 20, 42, 2, C.black);
    stamp(p, ['RRR.', 'R..R', 'RRR.', 'R..R', 'RRR.'], { R: C.red2 }, 30, 8);
    for (const x of [13, 48]) ellipse(p, x, 26, 2, 4, r.H);
  }
  const L = { O: C.outline, E: C.black, W: C.white, K: C.bags, R: C.red2 };
  const eyes = EYES[e];
  stamp(p, eyes, L, 19, 28);
  stamp(p, eyes.map((row) => [...row].reverse().join('')), L, 38, 28);
  if (e !== 'alivio' && e !== 'vitoria') for (const x of [20, 39]) rect(p, x, 34, 6, 1, C.bags);
  line(p, 31, 34, 32, 37, r.s);
  stamp(p, MOUTH[e], { ...L, R: C.red2 }, 28, 40);
  // acessórios
  if (id === 'staut') for (const x of [18, 41]) stamp(p, ['.P.P.', 'PPPPP', '.PPP.', '..P..'], { P: C.pink2 }, x, 35);
  if (id === 'jean') { box(p, 17, 27, 13, 8, C.gray1, C.gray5); box(p, 34, 27, 13, 8, C.gray1, C.gray5); rect(p, 30, 28, 4, 1, C.gray5); rect(p, 19, 29, 3, 1, C.gray3); rect(p, 36, 29, 3, 1, C.gray3); }
  if (id === 'fufu') for (const x of [17, 34]) { rect(p, x, 26, 13, 2, C.outline); rect(p, x, 35, 13, 2, C.outline); rect(p, x, 26, 2, 11, C.outline); rect(p, x + 11, 26, 2, 11, C.outline); rect(p, 30, 28, 4, 2, C.outline); }
  if (id === 'donasa') { ellipse(p, 23, 31, 6, 5, null, C.gray4); ellipse(p, 40, 31, 6, 5, null, C.gray4); rect(p, 29, 30, 5, 1, C.gray4); for (let i = 0; i < 12; i++) p.set(20 + i * 2 + (i % 2), 44 + (i % 3), r.h); }
  if (id === 'artur') { rect(p, 25, 38, 14, 2, r.h); rect(p, 24, 39, 2, 2, r.h); rect(p, 38, 39, 2, 2, r.h); }
  if (id === 'miyuki' || id === 'giulia') rect(p, 29, 41, 6, 1, C.red2);
  extras(p, e);
  return p;
}

export function heroPortraitSprites() {
  const out = [];
  for (const id of HEROES) for (const e of EMOTIONS)
    out.push({ id: `portrait_${id}_${e}`, category: 'Portraits', w: 64, h: 64, pivot: [32, 32], px: heroPortrait(id, e) });
  return out;
}
