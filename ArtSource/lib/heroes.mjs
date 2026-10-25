// Os dez personagens jogáveis, redesenhados a partir das fotos de referência com o motor de
// materiais (rig.mjs): só o preenchimento de cada parte é desenhado; contorno, luz e sombra são
// calculados. Sprites 32×32 (chibi, cabeça grande) e retratos 64×64 com 9 emoções.
import { C, PALETTE_HEX } from './palette.mjs';
import { Px } from './pixel.mjs';
import { Mat, ramp, render } from './rig.mjs';
import { CHAR_ANIMS } from './characters.mjs';
import { EMOTIONS, BG, extras } from './portraits.mjs';

export const HEROES = ['staut', 'miyuki', 'giulia', 'breno', 'artur', 'badotti', 'garcia', 'jean', 'fufu', 'donasa'];

// ---------- rampas (contorno, sombra, base, luz) ----------

const SKIN = {
  pale: ramp('#7a4636', '#dca88c', '#f3cbb0', '#fde3d1'),
  fair: ramp('#7a4a3a', '#e3b59a', '#f7d9c4', '#fdeadd'),
  light: ramp('#6e4232', '#cc9373', '#e9b796', '#f6d0b6'),
  tan: ramp('#58331f', '#a86f4b', '#c98e66', '#e0ab83'),
  bronze: ramp('#45261a', '#8a5638', '#b07448', '#cf9466'),
};
const HAIR = {
  brown: ramp('#24140c', '#4a2c1c', '#6a432c', '#8a5c3e'),
  darkbrown: ramp('#160d09', '#2c1c14', '#432c22', '#5c4134'),
  black: ramp('#0b0a0e', '#17161c', '#26242d', '#433f52'),
  darkblond: ramp('#3a2810', '#76552a', '#9c783a', '#c3a05c'),
};
const CLOTH = {
  black: ramp('#0b0c10', '#191b21', '#26292f', '#3a3e47'),
  navy: ramp('#0c1220', '#1a2438', '#25334d', '#3a4b6b'),
  white: ramp('#6b7380', '#c3c9d1', '#e6e9ed', '#ffffff'),
  jeans: ramp('#141c2b', '#26334a', '#34465e', '#4a6080'),
  darkjeans: ramp('#0d121c', '#1a2232', '#243046', '#34445e'),
  gray: ramp('#1c1f25', '#33373f', '#474c56', '#5f6672'),
  blue: ramp('#0a3a52', '#13698f', '#1f8fbf', '#4cc0e8'),
  maroon: ramp('#2a0a0e', '#5a1820', '#7c2430', '#a23a44'),
  red: ramp('#4a0a0a', '#a8282a', '#d63a3a', '#f06a5a'),
  shoe: ramp('#08090c', '#16181d', '#24272e', '#3a3e47'),
  sneaker: ramp('#3a3e47', '#a7b0bb', '#d0d6de', '#f2f4f7'),
  leather: ramp('#060608', '#121216', '#1d1d24', '#4a4a5a'),
};

// Detalhes chapados (sem sombra): cores únicas.
const FLAT = {
  eye: '#1a1418', white: '#f4f6f8', mouth: '#7a2a2a', teeth: '#f4f6f8', blush: '#f08aa0', lips: '#c22a3a',
  gold: '#ffd23f', red: '#e03a3a', yellow: '#ffe27a', green: '#4dff88', cyan: '#3fe0ff', logo: '#e03a3a',
  frame: '#14161b', frameLight: '#a7b0bb', lens: '#2a2f3a', lensShine: '#7d8794', btn: '#a7b0bb',
};

// ---------- corpo chibi 32×32 (preenchimentos; o motor desenha o contorno) ----------
// pose: { bob, legs: [dyEsq, dyDir], arms: [dyEsq, dyDir], stride (lado), raise: 'right'|'both', expr }

function frontBody(L, pose, view) {
  const m = new Mat(32, 32);
  const b = pose.bob || 0;
  const wide = L.build === 'muscle';
  const [ll, lr] = pose.legs || [0, 0];
  m.rect(12, 25 + ll, 4, 4 - ll, 'pants').rect(16, 25 + lr, 4, 4 - lr, 'pants');
  m.rect(11, 29 + Math.min(0, ll), 5, 2, 'shoe').rect(16, 29 + Math.min(0, lr), 5, 2, 'shoe');
  m.rect(11, 18 + b, 10, 7, 'shirt');
  m.rect(14, 17 + b, 4, 1, 'skin');
  const [al, ar] = pose.arms || [0, 0];
  const arm = (left, dy, raised) => {
    const w = wide ? 3 : 2;
    const xx = left ? (wide ? 8 : 9) : 21;
    const sleeveMat = L.sleeve === 'long' ? 'shirt' : 'skin';
    if (raised) { m.rect(xx, 10 + b, w, 8, sleeveMat); m.rect(xx, 8 + b, w, 2, 'skin'); return; }
    const top = 18 + b + dy;
    if (L.sleeve === 'long') m.rect(xx, top, w, 5, 'shirt');
    else if (L.sleeve === 'none') m.rect(xx, top, w, 5, 'skin');
    else { m.rect(xx, top, w, 2, 'shirt'); m.rect(xx, top + 2, w, 3, 'skin'); }
    m.rect(xx, top + 5, w, 1, 'skin');
  };
  arm(true, al, pose.raise === 'both');
  arm(false, ar, pose.raise === 'right' || pose.raise === 'both');
  m.oval(15.5, 11 + b, 6.5, 6, view === 'up' ? 'hair' : 'skin');
  if (view !== 'up') m.set(8, 11 + b, 'skin').set(8, 12 + b, 'skin').set(23, 11 + b, 'skin').set(23, 12 + b, 'skin');
  return m;
}

function sideBody(L, pose) {
  const m = new Mat(32, 32);
  const b = pose.bob || 0;
  const s = pose.stride || 0;
  m.rect(15 - s, 25, 3, 4, 'pants').rect(15 - s, 29, 4, 2, 'shoe');
  m.rect(13 + s, 25, 3, 4, 'pants').rect(12 + s, 29, 4, 2, 'shoe');
  m.rect(12, 18 + b, 8, 7, 'shirt');
  m.rect(14, 17 + b, 3, 1, 'skin');
  const xx = 14 + Math.sign(-s);
  const w = L.build === 'muscle' ? 4 : 3;
  if (L.sleeve === 'long') m.rect(xx, 18 + b, w, 5, 'shirt');
  else if (L.sleeve === 'none') m.rect(xx, 18 + b, w, 5, 'skin');
  else { m.rect(xx, 18 + b, w, 2, 'shirt'); m.rect(xx, 20 + b, w, 3, 'skin'); }
  m.rect(xx, 23 + b, w, 1, 'skin');
  m.oval(15, 11 + b, 6, 6, 'skin');
  m.set(8, 13 + b, 'skin');
  return m;
}

function face(m, L, view, b, expr) {
  const ey = 12 + b;
  if (view === 'down') {
    if (expr === 'closed') m.rect(12, ey + 1, 2, 1, 'eye').rect(18, ey + 1, 2, 1, 'eye');
    else if (L.eyes === 'almond') m.rect(12, ey, 2, 1, 'eye').rect(18, ey, 2, 1, 'eye').set(13, ey + 1, 'eye').set(18, ey + 1, 'eye');
    else m.rect(13, ey, 1, 2, 'eye').rect(18, ey, 1, 2, 'eye');
    if (L.blush) m.set(11, ey + 2, 'blush').set(20, ey + 2, 'blush');
    const my = 15 + b;
    if (expr === 'shout') m.rect(15, my - 1, 2, 2, 'mouth');
    else if (L.smile) m.rect(15, my, 2, 1, 'teeth').set(14, my, 'mouth').set(17, my, 'mouth');
    else m.rect(15, my, 2, 1, L.lips ? 'lips' : 'mouth');
  } else if (view === 'left') {
    if (L.eyes === 'almond') m.rect(10, ey, 2, 1, 'eye').set(10, ey + 1, 'eye');
    else if (expr === 'closed') m.rect(10, ey + 1, 2, 1, 'eye');
    else m.rect(10, ey, 1, 2, 'eye');
    if (L.blush) m.set(10, ey + 2, 'blush');
    m.set(10, 15 + b, expr === 'shout' ? 'mouth' : L.lips ? 'lips' : 'mouth');
    m.set(17, 12 + b, 'skin');
  }
}

// ---------- traços de cada personagem (a partir das fotos) ----------

const LOOKS = {
  // boné azul-marinho com "B" vermelho, cabelo castanho ondulado saindo pelos lados, bochechas rosadas, jaqueta escura com zíper
  staut: {
    pal: { skin: SKIN.pale, hair: HAIR.brown, shirt: CLOTH.black, pants: CLOTH.darkjeans, shoe: CLOTH.shoe, cap: CLOTH.navy, zip: '#a7b0bb' },
    sleeve: 'long', blush: true, lips: true,
    hair(m, v, b) {
      if (v === 'left') { m.oval(17, 9 + b, 5, 3, 'hair'); m.rect(18, 11 + b, 4, 5, 'hair'); m.oval(14, 6 + b, 6, 3, 'cap'); m.rect(5, 8 + b, 8, 2, 'cap'); return; }
      m.rect(8, 9 + b, 2, 6, 'hair').rect(22, 9 + b, 2, 6, 'hair');
      m.set(7, 13 + b, 'hair').set(24, 13 + b, 'hair');
      m.oval(15.5, 6 + b, 7, 3.5, 'cap');
      if (v === 'down') { m.rect(8, 9 + b, 16, 1, 'cap'); m.rect(15, 5 + b, 2, 3, 'red'); }
      else m.rect(9, 9 + b, 14, 6, 'hair');
    },
    outfit(m, v, b) { if (v === 'down') m.rect(15, 19 + b, 1, 6, 'zip'); },
  },
  // cabelo preto liso e comprido com franja reta, olhos amendoados, jaqueta de couro preta, batom vermelho
  miyuki: {
    pal: { skin: SKIN.fair, hair: HAIR.black, shirt: CLOTH.leather, pants: CLOTH.darkjeans, shoe: CLOTH.shoe, shine: '#5a5a6e' },
    sleeve: 'long', eyes: 'almond', lips: true,
    hair(m, v, b) {
      if (v === 'left') { m.oval(16, 8 + b, 6, 4, 'hair'); m.rect(16, 9 + b, 6, 14, 'hair'); m.rect(9, 6 + b, 6, 4, 'hair'); return; }
      m.oval(15.5, 7 + b, 7.5, 4, 'hair');
      m.rect(8, 8 + b, 3, 15, 'hair').rect(21, 8 + b, 3, 15, 'hair');
      if (v === 'down') m.rect(10, 8 + b, 12, 3, 'hair');
      else m.rect(9, 8 + b, 14, 14, 'hair');
    },
    outfit(m, v, b) { if (v === 'down') m.set(13, 19 + b, 'shine').set(18, 20 + b, 'shine'); },
  },
  // cabelo castanho-escuro muito comprido, ombros de fora, blusa azul, colar dourado
  giulia: {
    pal: { skin: SKIN.light, hair: HAIR.darkbrown, shirt: CLOTH.blue, pants: CLOTH.jeans, shoe: CLOTH.shoe },
    sleeve: 'none', lips: true,
    hair(m, v, b) {
      if (v === 'left') { m.oval(16, 8 + b, 6, 4, 'hair'); m.rect(15, 9 + b, 7, 16, 'hair'); m.rect(9, 6 + b, 5, 3, 'hair'); return; }
      m.oval(15.5, 7 + b, 7.5, 4, 'hair');
      m.rect(8, 8 + b, 3, 17, 'hair').rect(21, 8 + b, 3, 17, 'hair');
      if (v === 'down') { m.rect(10, 7 + b, 4, 2, 'hair'); m.set(10, 9 + b, 'hair'); } else m.rect(9, 8 + b, 14, 16, 'hair');
    },
    outfit(m, v, b) {
      if (v === 'down') { m.rect(11, 18 + b, 10, 1, 'skin'); m.set(14, 19 + b, 'gold').set(17, 19 + b, 'gold').rect(15, 20 + b, 2, 1, 'gold'); }
      else if (v === 'up') m.rect(11, 18 + b, 10, 1, 'skin');
    },
  },
  // cabelo castanho bagunçado com volume em cima, sorriso, camisa polo branca
  breno: {
    pal: { skin: SKIN.light, hair: HAIR.brown, shirt: CLOTH.white, pants: CLOTH.jeans, shoe: CLOTH.sneaker },
    sleeve: 'short', smile: true,
    hair(m, v, b) {
      if (v === 'left') { m.oval(15, 6 + b, 6, 3, 'hair'); m.rect(16, 7 + b, 5, 5, 'hair'); m.set(10, 3 + b, 'hair').set(13, 2 + b, 'hair').set(17, 3 + b, 'hair'); return; }
      m.oval(15.5, 6 + b, 7, 3.5, 'hair');
      for (const x of [10, 13, 16, 19, 21]) m.set(x, 2 + b, 'hair').set(x, 3 + b, 'hair');
      m.rect(9, 7 + b, 2, 4, 'hair').rect(21, 7 + b, 2, 4, 'hair');
      if (v === 'down') m.rect(11, 8 + b, 3, 1, 'hair'); else m.rect(9, 7 + b, 14, 8, 'hair');
    },
    outfit(m, v, b) { if (v === 'down') { m.set(13, 18 + b, 'eye').set(18, 18 + b, 'eye'); m.rect(15, 19 + b, 2, 2, 'shirtDark'); } },
  },
  // cabelo escuro ondulado até a nuca, bigode grosso, pele morena, jaqueta branca com bordado vermelho
  artur: {
    pal: { skin: SKIN.tan, hair: HAIR.darkbrown, shirt: CLOTH.white, pants: CLOTH.darkjeans, shoe: CLOTH.shoe, emb: CLOTH.red, strap: '#8a5a2a' },
    sleeve: 'long',
    hair(m, v, b) {
      if (v === 'left') { m.oval(16, 7 + b, 6, 3.5, 'hair'); m.rect(16, 8 + b, 6, 9, 'hair'); m.rect(9, 6 + b, 5, 3, 'hair'); m.rect(9, 14 + b, 3, 1, 'stache'); return; }
      m.oval(15.5, 6 + b, 7, 3, 'hair');
      m.rect(9, 7 + b, 2, 7, 'hair').rect(21, 7 + b, 2, 7, 'hair');
      m.set(8, 13 + b, 'hair').set(23, 13 + b, 'hair');
      if (v === 'down') { m.rect(11, 8 + b, 3, 1, 'hair').rect(18, 8 + b, 3, 1, 'hair'); m.rect(13, 14 + b, 6, 1, 'stache'); m.set(12, 15 + b, 'stache').set(19, 15 + b, 'stache'); }
      else m.rect(9, 7 + b, 14, 10, 'hair');
    },
    outfit(m, v, b) {
      if (v === 'down') { m.rect(13, 18 + b, 6, 7, 'emb'); for (const [x, y] of [[14, 19], [17, 19], [15, 21], [16, 21], [14, 23], [17, 23]]) m.set(x, y + b, 'yellow'); m.line(11, 18 + b, 13, 24 + b, 'strap'); }
      if (v === 'left') m.rect(12, 19 + b, 2, 5, 'emb');
    },
  },
  // cabelo castanho-escuro curto com franja, sorriso largo, camiseta preta (com um robozinho)
  badotti: {
    pal: { skin: SKIN.light, hair: HAIR.darkbrown, shirt: CLOTH.black, pants: CLOTH.jeans, shoe: CLOTH.sneaker },
    sleeve: 'short', smile: true,
    hair(m, v, b) {
      if (v === 'left') { m.oval(15, 6.5 + b, 6.5, 3.5, 'hair'); m.rect(16, 8 + b, 5, 4, 'hair'); m.rect(9, 7 + b, 4, 2, 'hair'); return; }
      m.oval(15.5, 6.5 + b, 7.5, 4, 'hair');
      m.rect(9, 8 + b, 2, 3, 'hair').rect(21, 8 + b, 2, 3, 'hair');
      if (v === 'down') { m.rect(11, 9 + b, 6, 1, 'hair'); m.set(11, 10 + b, 'hair'); } else m.rect(9, 8 + b, 14, 7, 'hair');
    },
    outfit(m, v, b) { if (v === 'down') { m.rect(14, 20 + b, 4, 3, 'cyan'); m.set(15, 21 + b, 'eye').set(16, 21 + b, 'eye'); } },
  },
  // cabelo preto jogado para o lado, rosto sério, camisa social branca, calça escura
  garcia: {
    pal: { skin: SKIN.pale, hair: HAIR.black, shirt: CLOTH.white, pants: CLOTH.black, shoe: CLOTH.shoe },
    sleeve: 'long',
    hair(m, v, b) {
      if (v === 'left') { m.oval(15, 6.5 + b, 6.5, 3.5, 'hair'); m.rect(15, 8 + b, 6, 5, 'hair'); m.rect(8, 7 + b, 6, 3, 'hair'); return; }
      m.oval(15.5, 6.5 + b, 7.5, 4, 'hair');
      m.rect(9, 8 + b, 2, 4, 'hair').rect(21, 8 + b, 2, 4, 'hair');
      if (v === 'down') { m.rect(11, 8 + b, 7, 2, 'hair'); m.rect(11, 10 + b, 3, 1, 'hair'); } else m.rect(9, 8 + b, 14, 7, 'hair');
    },
    outfit(m, v, b) { if (v === 'down') { m.set(13, 18 + b, 'eye').set(18, 18 + b, 'eye'); for (const y of [20, 22, 24]) m.set(15, y + b, 'btn'); } },
  },
  // topete castanho, óculos de aviador escuros, sorriso, camiseta branca — montado no porco
  jean: {
    pal: { skin: SKIN.light, hair: HAIR.brown, shirt: CLOTH.white, pants: CLOTH.jeans, shoe: CLOTH.shoe },
    sleeve: 'short', smile: true, glasses: 'aviator',
    hair(m, v, b) {
      if (v === 'left') { m.oval(14, 5 + b, 6, 3.5, 'hair'); m.rect(15, 7 + b, 6, 5, 'hair'); m.rect(7, 4 + b, 5, 3, 'hair'); return; }
      m.oval(15.5, 5 + b, 6.5, 3.5, 'hair');
      m.rect(9, 7 + b, 2, 4, 'hair').rect(21, 7 + b, 2, 4, 'hair');
      if (v === 'up') m.rect(9, 7 + b, 14, 8, 'hair');
    },
    outfit() {},
  },
  // muito musculoso, pele bronzeada, óculos pretos retangulares, regata preta, pulseira verde
  fufu: {
    pal: { skin: SKIN.bronze, hair: HAIR.brown, shirt: CLOTH.black, pants: CLOTH.gray, shoe: CLOTH.shoe },
    sleeve: 'none', build: 'muscle', glasses: 'rect',
    hair(m, v, b) {
      if (v === 'left') { m.oval(15, 6 + b, 6, 3.5, 'hair'); m.rect(16, 7 + b, 5, 4, 'hair'); m.rect(8, 5 + b, 5, 3, 'hair'); return; }
      m.oval(15.5, 6 + b, 7, 3.5, 'hair');
      m.rect(9, 7 + b, 2, 3, 'hair').rect(21, 7 + b, 2, 3, 'hair');
      if (v === 'down') m.rect(16, 8 + b, 5, 1, 'hair'); else m.rect(9, 7 + b, 14, 8, 'hair');
    },
    outfit(m, v, b) {
      if (v === 'down') { m.rect(11, 18 + b, 2, 7, 'skin').rect(19, 18 + b, 2, 7, 'skin'); m.rect(21, 22 + b, 3, 1, 'green'); m.set(15, 21 + b, 'shirtDark').set(16, 21 + b, 'shirtDark'); }
      if (v === 'left') m.rect(13, 22 + b, 4, 1, 'green');
    },
  },
  // cabelo loiro escuro bagunçado, óculos redondos finos, barba rala, camisa vinho
  donasa: {
    pal: { skin: SKIN.light, hair: HAIR.darkblond, shirt: CLOTH.maroon, pants: CLOTH.darkjeans, shoe: CLOTH.sneaker, stubble: '#b88a62' },
    sleeve: 'long', glasses: 'round',
    hair(m, v, b) {
      if (v === 'left') { m.oval(15, 6 + b, 6.5, 4, 'hair'); m.rect(16, 7 + b, 6, 6, 'hair'); m.set(9, 3 + b, 'hair').set(12, 2 + b, 'hair'); m.rect(9, 14 + b, 3, 2, 'stubble'); return; }
      m.oval(15.5, 6 + b, 7.5, 4, 'hair');
      for (const x of [9, 12, 15, 19, 22]) m.set(x, 2 + b, 'hair');
      m.rect(8, 8 + b, 3, 5, 'hair').rect(21, 8 + b, 3, 5, 'hair');
      if (v === 'down') { m.rect(11, 8 + b, 3, 2, 'hair').rect(17, 8 + b, 3, 1, 'hair'); for (const [x, y] of [[11, 15], [12, 16], [14, 16], [17, 16], [19, 16], [20, 15]]) m.set(x, y + b, 'stubble'); }
      else m.rect(9, 8 + b, 14, 7, 'hair');
    },
    outfit(m, v, b) { if (v === 'down') { m.set(13, 18 + b, 'shirtDark').set(18, 18 + b, 'shirtDark'); m.rect(15, 19 + b, 1, 6, 'shirtDark'); } },
  },
};

const ORDER = ['shoe', 'pants', 'shirt', 'emb', 'skin', 'hair', 'cap'];

function palette(L) {
  const p = { ...FLAT, ...L.pal };
  p.shirtDark = L.pal.shirt.s;
  p.stache = HAIR.darkbrown.b;
  return p;
}

function glasses(m, L, view, b) {
  if (!L.glasses) return;
  const ey = 12 + b;
  const f = L.glasses === 'rect' ? 'frame' : 'frameLight';
  if (view === 'down') {
    if (L.glasses === 'aviator') { m.rect(11, ey - 1, 4, 3, 'lens').rect(17, ey - 1, 4, 3, 'lens'); m.rect(15, ey - 1, 2, 1, 'frameLight'); m.set(12, ey - 1, 'lensShine').set(18, ey - 1, 'lensShine'); return; }
    for (const x of [11, 17]) m.rect(x, ey - 1, 4, 1, f).rect(x, ey + 2, 4, 1, f).set(x, ey, f).set(x, ey + 1, f).set(x + 3, ey, f).set(x + 3, ey + 1, f);
    m.rect(15, ey - 1, 2, 1, f);
  } else if (view === 'left') {
    if (L.glasses === 'aviator') { m.rect(9, ey - 1, 3, 3, 'lens').rect(12, ey - 1, 5, 1, 'frameLight'); return; }
    m.rect(9, ey - 1, 3, 1, f).rect(9, ey + 2, 3, 1, f).set(11, ey, f).set(11, ey + 1, f).rect(12, ey - 1, 5, 1, f);
  }
}

/** Personagem numa vista e pose → Px 32×32. */
function draw(id, view, pose = {}) {
  const L = LOOKS[id];
  const b = pose.bob || 0;
  const v = view === 'right' ? 'left' : view;
  const m = v === 'left' ? sideBody(L, pose) : frontBody(L, pose, v);
  L.outfit(m, v, b);
  face(m, L, v, b, pose.expr);
  L.hair(m, v, b);
  glasses(m, L, v, b);
  const px = render(m, palette(L), { order: ORDER });
  return view === 'right' ? px.flipX() : px;
}

// ---------- Jean montado no porco ----------

const PIG = { pig: ramp('#7a2a48', '#e07a9c', '#f7a1bd', '#ffc8d8'), snout: '#ffb3c9', eye: '#1a1418', nost: '#a8456a' };

function pig(view, f) {
  const m = new Mat(32, 32);
  const lift = f % 2;
  if (view === 'left') {
    m.oval(18, 24, 8.5, 4, 'pig');
    m.oval(9, 22.5, 4, 3.5, 'pig');
    m.rect(4, 22, 2, 3, 'snout'); m.set(4, 23, 'nost');
    m.set(10, 18, 'pig').set(11, 18, 'pig').set(10, 19, 'pig');
    m.set(8, 21, 'eye');
    for (const [x, l] of [[12, lift], [15, 1 - lift], [21, 1 - lift], [24, lift]]) m.rect(x, 27 - l, 2, 3, 'pig');
    m.set(27, 21, 'pig').set(28, 20, 'pig');
  } else {
    m.oval(15.5, 24, 8, 4.5, 'pig');
    m.rect(10, 27 - lift, 2, 3, 'pig').rect(20, 26 + lift, 2, 3, 'pig');
    if (view === 'down') {
      m.oval(15.5, 24.5, 5, 3.5, 'pig');
      m.rect(14, 25, 4, 2, 'snout'); m.set(14, 26, 'nost').set(17, 26, 'nost');
      m.set(12, 23, 'eye').set(19, 23, 'eye');
      m.set(10, 20, 'pig').set(21, 20, 'pig');
    } else m.set(15, 22, 'nost').set(16, 21, 'nost');
  }
  return render(m, PIG, { order: ['pig'] });
}

function mounted(view, pose, f) {
  const side = view === 'left' || view === 'right';
  const rider = draw('jean', side ? 'left' : view, { ...pose, legs: [0, 0], stride: 0 });
  const up = side ? 7 : 4;
  const body = new Px(32, 32);
  for (let y = 0; y < 26; y++) for (let x = 0; x < 32; x++) { const c = rider.get(x, y); if (c) body.set(x, y - up, c); }
  const p = pig(side ? 'left' : view, f);
  if (side) {
    const comp = new Px(32, 32).blit(p).blit(body);
    for (let y = 18; y < 25; y++) { comp.set(14, y, C.outline); comp.set(15, y, '#34465e'); comp.set(16, y, C.outline); }
    for (const x of [14, 15, 16, 17]) comp.set(x, 25, x === 14 || x === 17 ? C.outline : '#24272e');
    return view === 'right' ? comp.flipX() : comp;
  }
  if (view === 'down') for (const x of [8, 22]) for (let y = 17; y < 23; y++) { body.set(x, y, C.outline); body.set(x + 1, y, y > 20 ? '#24272e' : '#34465e'); body.set(x + 2, y, C.outline); }
  return body.blit(p);
}

// ---------- animações ----------

function frames(id) {
  const f = {};
  const walkPose = (i, side) => side
    ? { stride: [0, 2, 0, -2][i], bob: i % 2 ? -1 : 0 }
    : { legs: [[0, 0], [-1, 0], [0, 0], [0, -1]][i], arms: [[0, 0], [1, -1], [0, 0], [-1, 1]][i], bob: i % 2 ? -1 : 0 };
  const d = id === 'jean' ? (view, pose, i = 0) => mounted(view, pose, i) : (view, pose) => draw(id, view, pose);
  for (const view of ['down', 'up', 'left', 'right']) {
    const side = view === 'left' || view === 'right';
    f[`idle_${view}`] = [d(view, {}), d(view, { bob: 1 }, 1)];
    f[`walk_${view}`] = [0, 1, 2, 3].map((i) => d(view, walkPose(i, side), i));
  }
  const tint = (p, col) => p.map((v) => (v === C.outline ? v : col));
  const flash = (p) => { const o = p.clone(); for (const [x, y] of [[25, 6], [26, 5], [27, 6], [26, 7], [26, 6]]) o.set(x, y, C.yellow3); return o; };
  const ring = (p, col, parity) => { const o = p.clone(); for (const [x, y] of p.ring()) if (parity < 0 || (x + y) % 2 === parity) o.set(x, y, col); return o; };
  const stars = (p, x) => { const o = p.clone(); for (const [dx, dy] of [[1, 0], [0, 1], [1, 1], [2, 1], [1, 2]]) o.set(x + dx, 1 + dy, C.yellow3); return o; };
  const shout = id === 'donasa' ? 'shout' : undefined;
  const pose = (p) => ({ ...p, expr: p.expr ?? shout });

  f.attack_down = [d('down', pose({ raise: 'right' })), flash(d('down', pose({ raise: 'right' }))), d('down', pose({}))];
  f.special_down = [ring(d('down', {}), C.cyan2, 0), ring(d('down', {}), C.cyan2, 1), ring(d('down', pose({ raise: 'both' })), C.yellow3, -1)];
  f.hurt_down = [tint(d('down', { expr: 'closed' }), C.white), d('down', { expr: 'closed' }).shift(-1, 0)];
  f.stun_down = [stars(d('down', { expr: 'closed' }), 8), stars(d('down', { expr: 'closed' }), 20)];
  f.victory_down = [d('down', { raise: 'both' }), d('down', { raise: 'both', bob: -1 }, 1)];
  // caído e reviver (o Jean cai do porco)
  const base = draw(id, 'down', { expr: 'closed' });
  const kneel = new Px(32, 32);
  for (let y = 0; y < 26; y++) for (let x = 0; x < 32; x++) { const c = base.get(x, y); if (c) kneel.set(x, y + 4, c); }
  for (let x = 10; x < 22; x++) { kneel.set(x, 30, C.outline); if (x > 10 && x < 21) kneel.set(x, 29, LOOKS[id].pal.pants.b); }
  const lying = (() => { const r = base.rotate90cw(); return new Px(32, 32).blit(r, 0, 31 - r.bottomRow()).shift(0, -2); })();
  const zz = (p) => { const o = p.clone(); for (const [x, y] of [[24, 4], [25, 4], [26, 4], [25, 5], [24, 6], [25, 6], [26, 6]]) o.set(x, y, C.cyan3); return o; };
  f.downed_down = [kneel, lying, zz(lying)];
  const plus = (p, big) => { const o = p.clone(); for (const [x, y] of big ? [[25, 15], [24, 16], [25, 16], [26, 16], [25, 17]] : [[25, 16]]) o.set(x, y, C.green2); return o; };
  f.revive_down = [plus(kneel, false), plus(kneel, true)];
  return f;
}

// As rampas dos personagens entram na paleta oficial do jogo.
for (const group of [SKIN, HAIR, CLOTH, { f: FLAT }, { p: PIG }])
  for (const r of Object.values(group))
    for (const c of typeof r === 'string' ? [r] : Object.values(r))
      for (const hex of typeof c === 'string' ? [c] : Object.values(c))
        if (typeof hex === 'string' && hex.startsWith('#') && !PALETTE_HEX.includes(hex)) PALETTE_HEX.push(hex);
for (const L of Object.values(LOOKS)) for (const c of Object.values(L.pal)) if (typeof c === 'string' && !PALETTE_HEX.includes(c)) PALETTE_HEX.push(c);
for (const hex of ['#34465e', '#24272e']) if (!PALETTE_HEX.includes(hex)) PALETTE_HEX.push(hex);

export function heroSprites() {
  const out = [];
  for (const id of HEROES) {
    const f = frames(id);
    for (const [anim, { dirs, frames: n }] of Object.entries(CHAR_ANIMS))
      for (const dir of dirs) {
        const list = f[`${anim}_${dir}`];
        if (!list || list.length !== n) throw new Error(`animação incompleta: ${id} ${anim} ${dir}`);
        list.forEach((px, i) => out.push({ id: `char_${id}_${anim}_${dir}_${i}`, category: 'Characters', sub: id, w: 32, h: 32, pivot: [16, 1], px }));
      }
  }
  return out;
}

// ---------- retratos 64×64 com 9 emoções ----------

// Olhos 7×5 (esquerdo; o direito é espelhado). L cílio, W branco, I íris, P pupila, H brilho, S pálpebra.
const EYES = {
  open: ['.LLLLL.', 'LWIHIWL', 'LWIPIWL', '.WIIIW.', '..SSS..'],
  almond: ['LLLLLLL', 'LWIHIWL', '.WIPIW.', '..SSS..', '.......'],
  wide: ['.LLLLL.', 'LWWIWWL', 'LWIPIWL', 'LWWIWWL', '.LLLLL.'],
  narrow: ['.......', 'LLLLLLL', 'LWIPIWL', '.SSSSS.', '.......'],
  half: ['.......', 'SSSSSSS', 'LLLLLLL', 'LWIPIWL', '.SSSSS.'],
  happy: ['.......', '..LLL..', '.L...L.', 'L.....L', '.......'],
  cry: ['.......', 'L.....L', '.LLLLL.', '.......', '.......'],
  x: ['.......', '.L...L.', '..L.L..', '...L...', '..L.L..'],
};
const EMO = {
  raiva: { eye: 'narrow', brow: 'angry', mouth: 'grit' },
  medo: { eye: 'wide', brow: 'worried', mouth: 'o' },
  desespero: { eye: 'wide', brow: 'worried', mouth: 'scream' },
  choro: { eye: 'cry', brow: 'worried', mouth: 'sad' },
  exaustao: { eye: 'half', brow: 'flat', mouth: 'pant' },
  alivio: { eye: 'happy', brow: 'up', mouth: 'smile' },
  determinacao: { eye: 'open', brow: 'flat', mouth: 'line' },
  vitoria: { eye: 'happy', brow: 'up', mouth: 'grin' },
  derrota: { eye: 'x', brow: 'worried', mouth: 'wave' },
};
const MOUTHS = {
  line: ['..MMMMMM..'],
  smile: ['M........M', '.MMMMMMMM.'],
  grin: ['MMMMMMMMMM', 'MTTTTTTTTM', '.MRRRRRRM.', '..MMMMMM..'],
  grit: ['MMMMMMMMMM', 'MTMTMTMTMM', 'MMMMMMMMMM'],
  o: ['...MMMM...', '..MRRRRM..', '...MMMM...'],
  scream: ['..MMMMMM..', '.MRRRRRRM.', '.MRRRRRRM.', '..MMMMMM..'],
  sad: ['..MMMMMM..', '.M......M.'],
  pant: ['...MMMM...', '...MRRM...', '...MMMM...'],
  wave: ['.M.M.M.M..', 'M.M.M.M.M.'],
};
const BROWS = {
  flat: ['BBBBBBB'], up: ['.BBBBB.', 'B.....B'], angry: ['BB.....', '..BBB..', '.....BB'], worried: ['.....BB', '..BBB..', 'BB.....'],
};

const PLOOK = {
  staut: { hair: 'cap', blush: true, lips: true },
  miyuki: { hair: 'long', bangs: true, eyes: 'almond', lips: true },
  giulia: { hair: 'long', liner: true, lips: true, necklace: true },
  breno: { hair: 'messy', collar: true },
  artur: { hair: 'wavy', stache: true, embroidery: true },
  badotti: { hair: 'short' },
  garcia: { hair: 'swept', collar: true },
  jean: { hair: 'quiff', glasses: 'aviator' },
  fufu: { hair: 'short', glasses: 'rect', muscle: true },
  donasa: { hair: 'messyblond', glasses: 'round', stubble: true },
};

function hairPortrait(m, P) {
  switch (P.hair) {
    case 'cap':
      m.rect(13, 22, 4, 14, 'hair'); m.rect(47, 22, 4, 14, 'hair'); m.oval(13, 34, 3, 3, 'hair'); m.oval(51, 34, 3, 3, 'hair');
      m.oval(32, 15, 17, 10, 'cap'); m.rect(11, 20, 42, 4, 'cap');
      break;
    case 'long':
      m.oval(32, 15, 16, 9, 'hair');
      if (P.bangs) m.rect(17, 14, 30, 9, 'hair');
      else { m.rect(17, 14, 9, 6, 'hair'); m.rect(17, 18, 4, 12, 'hair'); m.rect(43, 16, 4, 14, 'hair'); }
      m.rect(15, 18, 4, 30, 'hair'); m.rect(45, 18, 4, 30, 'hair');
      break;
    case 'messy':
      m.oval(32, 15, 16, 9, 'hair');
      for (const x of [18, 24, 30, 36, 42]) m.oval(x, 7, 3, 4, 'hair');
      m.rect(17, 18, 6, 4, 'hair'); m.rect(30, 18, 8, 3, 'hair'); m.rect(15, 20, 3, 9, 'hair'); m.rect(46, 20, 3, 9, 'hair');
      break;
    case 'messyblond':
      m.oval(32, 15, 17, 9, 'hair');
      for (const [x, y] of [[17, 9], [23, 6], [30, 5], [37, 6], [44, 9]]) m.oval(x, y, 3, 3, 'hair');
      m.rect(17, 18, 8, 5, 'hair'); m.rect(36, 18, 7, 3, 'hair'); m.rect(15, 20, 3, 8, 'hair'); m.rect(46, 20, 3, 8, 'hair');
      break;
    case 'wavy':
      m.oval(32, 14, 16, 8, 'hair');
      m.oval(16, 24, 3, 8, 'hair'); m.oval(48, 24, 3, 8, 'hair');
      m.rect(18, 18, 11, 3, 'hair'); m.rect(35, 18, 11, 3, 'hair'); m.rect(17, 21, 4, 2, 'hair'); m.rect(43, 21, 4, 2, 'hair');
      break;
    case 'short':
      m.oval(32, 15, 16, 9, 'hair'); m.rect(17, 18, 12, 4, 'hair'); m.rect(16, 18, 3, 10, 'hair'); m.rect(45, 18, 3, 10, 'hair');
      break;
    case 'swept':
      m.oval(32, 15, 16, 9, 'hair'); m.rect(18, 17, 18, 4, 'hair'); m.rect(18, 21, 8, 3, 'hair'); m.rect(16, 18, 3, 10, 'hair'); m.rect(45, 18, 3, 8, 'hair');
      break;
    case 'quiff':
      m.oval(32, 11, 14, 10, 'hair'); m.oval(36, 6, 9, 5, 'hair'); m.rect(16, 16, 3, 10, 'hair'); m.rect(45, 16, 3, 10, 'hair');
      break;
  }
}

function glassesPortrait(p, kind) {
  if (!kind) return;
  const frame = kind === 'rect' ? '#14161b' : kind === 'round' ? '#a7b0bb' : '#c9cdd2';
  for (const x0 of [17, 35]) {
    if (kind === 'round') {
      for (let a = 0; a < 48; a++) { const t = a / 48 * Math.PI * 2; p.set(Math.round(x0 + 6 + Math.cos(t) * 6), Math.round(30 + Math.sin(t) * 5), frame); }
      continue;
    }
    const w = 12, h = kind === 'aviator' ? 9 : 8, y0 = 26;
    for (let y = y0; y < y0 + h; y++) for (let x = x0; x < x0 + w; x++) {
      const edge = y === y0 || y === y0 + h - 1 || x === x0 || x === x0 + w - 1;
      if (kind === 'aviator' && y === y0 + h - 1 && (x === x0 || x === x0 + w - 1)) continue;
      if (edge) { p.set(x, y, frame); if (kind === 'rect') p.set(x, y + (y === y0 ? 1 : y === y0 + h - 1 ? -1 : 0), frame); }
      else if (kind === 'aviator') p.set(x, y, (x - x0 + y - y0) % 7 === 0 ? '#7d8794' : '#2a2f3a');
    }
  }
  for (let x = 29; x < 35; x++) p.set(x, 28, frame);
  p.set(16, 28, frame); p.set(47, 28, frame);
}

function stampTxt(p, rows, legend, x0, y0) {
  rows.forEach((r, j) => [...r].forEach((ch, i) => { if (ch !== '.' && legend[ch]) p.set(x0 + i, y0 + j, legend[ch]); }));
}

function shadeHex(hex, k) {
  const n = parseInt(hex.slice(1), 16);
  const c = (s) => Math.max(0, Math.min(255, Math.round(((n >> s) & 255) * k))).toString(16).padStart(2, '0');
  return '#' + c(16) + c(8) + c(0);
}

function portrait(id, emo) {
  const L = LOOKS[id], P = PLOOK[id], E = EMO[emo];
  const m = new Mat(64, 64);
  if (P.hair === 'long') { m.oval(32, 34, 21, 24, 'hair'); m.rect(11, 34, 42, 30, 'hair'); }
  m.oval(32, 66, P.muscle ? 31 : 26, 15, 'shirt');
  if (P.muscle) { m.oval(5, 60, 8, 9, 'skin'); m.oval(59, 60, 8, 9, 'skin'); }
  if (id === 'giulia') { m.oval(32, 66, 25, 12, 'skin'); m.rect(16, 59, 32, 5, 'shirt'); }
  m.rect(27, 44, 10, 9, 'skin');
  if (P.collar) { m.line(24, 50, 31, 56, 'collar'); m.line(40, 50, 33, 56, 'collar'); }
  if (P.embroidery) { m.rect(25, 53, 14, 11, 'emb'); m.line(18, 52, 24, 63, 'strap'); }
  if (id === 'staut') m.rect(31, 53, 2, 11, 'zip');
  m.oval(32, 29, 15, 17, 'skin');
  m.oval(16.5, 31, 2.5, 4, 'skin'); m.oval(47.5, 31, 2.5, 4, 'skin');
  hairPortrait(m, P);
  const pal = { ...palette(L), collar: CLOTH.white, emb: CLOTH.red, strap: '#8a5a2a', zip: '#a7b0bb' };
  const px = render(m, pal, { order: ['shirt', 'collar', 'emb', 'skin', 'hair', 'cap'] });

  const out = new Px(64, 64);
  for (let y = 0; y < 64; y++) for (let x = 0; x < 64; x++) out.set(x, y, (x + y) % 8 === 0 ? shadeHex(BG[emo], 0.85) : BG[emo]);
  out.blit(px);

  const skin = L.pal.skin;
  const iris = P.eyes === 'almond' ? '#2a1a14' : '#4a3020';
  const legend = { L: '#1a1418', W: '#f4f6f8', I: iris, P: '#0e0c10', H: '#ffffff', S: skin.s };
  const eye = EYES[E.eye === 'open' && P.eyes === 'almond' ? 'almond' : E.eye];
  stampTxt(out, eye, legend, 20, 28);
  stampTxt(out, eye.map((r) => [...r].reverse().join('')), legend, 37, 28);
  if (P.liner && ['open', 'wide', 'narrow', 'half'].includes(E.eye)) for (const [x, y] of [[27, 28], [28, 27], [36, 28], [35, 27]]) out.set(x, y, '#1a1418');
  if (P.embroidery) for (let i = 0; i < 4; i++) { out.set(27 + i * 3, 56 + (i % 2) * 3, FLAT.yellow); out.set(28 + i * 3, 57 + (i % 2) * 3, FLAT.gold); }
  const brow = BROWS[E.brow];
  const by = P.bangs ? 25 : 24;
  const browCol = L.pal.hair.s;
  if (!P.bangs || E.brow !== 'flat') {
    stampTxt(out, brow, { B: browCol }, 20, by);
    stampTxt(out, brow.map((r) => [...r].reverse().join('')), { B: browCol }, 37, by);
  }
  out.set(32, 34, skin.s); out.set(32, 35, skin.s); out.set(31, 36, skin.s); out.set(33, 36, skin.o);
  if (id === 'staut') stampTxt(out, ['RRRR.', 'R...R', 'RRRR.', 'R...R', 'R...R', 'RRRR.'], { R: FLAT.logo }, 30, 8);
  if (P.blush) for (const x of [19, 41]) for (const [dx, dy] of [[0, 0], [2, 0], [0, 1], [1, 1], [2, 1], [1, 2]]) out.set(x + dx, 35 + dy, FLAT.blush);
  if (P.stache) { for (let x = 24; x < 40; x++) out.set(x, 38 + (x < 27 || x > 36 ? 1 : 0), HAIR.darkbrown.b); for (let x = 25; x < 39; x++) out.set(x, 37, HAIR.darkbrown.s); }
  if (P.stubble) for (let i = 0; i < 30; i++) out.set(20 + ((i * 7) % 25), 40 + ((i * 3) % 6), L.pal.stubble);
  stampTxt(out, MOUTHS[E.mouth], { M: P.lips ? '#9a1e2c' : '#6a2a2a', T: '#f4f6f8', R: '#b0303a' }, 27, 40);
  if (P.lips && (E.mouth === 'line' || E.mouth === 'smile')) for (let x = 29; x < 35; x++) out.set(x, 41, FLAT.lips);
  glassesPortrait(out, P.glasses);
  if (P.necklace) { for (let x = 26; x < 38; x++) out.set(x, 50 + (Math.abs(x - 31.5) < 3 ? 2 : Math.abs(x - 31.5) < 5 ? 1 : 0), FLAT.gold); out.set(31, 53, FLAT.gold); out.set(32, 53, FLAT.gold); }
  if (id === 'badotti') { for (const [x, y] of [[29, 58], [30, 58], [33, 58], [34, 58], [30, 60], [31, 60], [32, 60], [33, 60]]) out.set(x, y, FLAT.cyan); }
  extras(out, emo);
  for (let i = 0; i < 64; i++) { out.set(i, 0, C.outline); out.set(i, 63, C.outline); out.set(0, i, C.outline); out.set(63, i, C.outline); }
  return out;
}

export function heroPortraitSprites() {
  const out = [];
  for (const id of HEROES) for (const e of EMOTIONS)
    out.push({ id: `portrait_${id}_${e}`, category: 'Portraits', w: 64, h: 64, pivot: [32, 32], px: portrait(id, e) });
  return out;
}
