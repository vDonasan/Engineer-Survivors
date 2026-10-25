// Bosses Guirotto (sapo do banjo) e Mago da Elétrica, a partir das imagens de referência, no motor de
// materiais (rig.mjs). Guirotto 48×48, Mago 48×56. 8 direções de andar (as da direita são o espelho da
// esquerda: o instrumento troca de mão, mas continua legível), idle/ataques normal e bravo, carga,
// dano, impacto forte, fúria, morte e desaparecimento.
import { C, PALETTE_HEX } from './palette.mjs';
import { Px } from './pixel.mjs';
import { Mat, ramp, render } from './rig.mjs';

// ---------- paletas ----------

const FROG = {
  skin: ramp('#1e2a0e', '#4f6a22', '#6f8a2e', '#93a84a'),
  spot: ramp('#1e2a0e', '#3e5418', '#4f6a22', '#5f7a2a'),
  belly: ramp('#5a4a2a', '#c8b47a', '#e2d29a', '#f2e6bc'),
  shirt: ramp('#1c2410', '#4a5428', '#626c36', '#7c864a'),
  shorts: ramp('#1c1a10', '#4a4228', '#5e5534', '#766c46'),
  banjo: ramp('#2a160a', '#6a3a1a', '#8a522a', '#a86c3a'),
  drum: ramp('#5a4a2a', '#d8c89a', '#ece0b8', '#fff6d8'),
  eyeO: '#2a1a0a', iris: '#c8781a', irisLight: '#f0a83a', pupil: '#120c08', glint: '#ffffff', mouth: '#2a3410',
  rage: '#e03a3a', rageEye: '#ff4a2a', tongue: '#c04a5a', string: '#c9cdd2', peg: '#ffd23f',
};
const MAGE = {
  hat: ramp('#0c1430', '#1f3470', '#2c4a9a', '#4a6cc8'),
  band: ramp('#2a160a', '#5a361a', '#7a4a24', '#9a6434'),
  buckle: '#a7b0bb',
  hair: ramp('#5a6070', '#c3c9d1', '#e6e9ed', '#ffffff'),
  skin: ramp('#5a2e1e', '#b06a48', '#d08a62', '#e8a87e'),
  robe: ramp('#080c1c', '#141e3e', '#1e2c58', '#2e4078'),
  belt: ramp('#2a160a', '#5a361a', '#7a4a24', '#9a6434'),
  book: ramp('#2a140a', '#6a3a1a', '#8a522a', '#b0703a'),
  page: '#e8d8b0', gem: '#e03a3a', eye: '#1a1418', brow: '#a7b0bb', mouth: '#5a2a2a',
  bolt: '#3fe0ff', boltCore: '#ffffff', boltDark: '#1a8fb0', rageEye: '#ff4a2a',
};

const shadeOrder = ['shorts', 'banjo', 'drum', 'shirt', 'belly', 'skin', 'spot', 'robe', 'belt', 'book', 'hair', 'skinM', 'hat', 'band'];

// ---------- Guirotto ----------

/**
 * pose: { view: 'down'|'up'|'left'|'downleft'|'upleft', step: 0..3, bob, strum: 0..2, raise: bool,
 *         angry: bool, mouthOpen: bool, lean: px, squash: bool }
 */
function frog(pose) {
  const m = new Mat(48, 48);
  const v = pose.view, b = pose.bob || 0;
  const side = v === 'left', three = v === 'downleft' || v === 'upleft', back = v === 'up' || v === 'upleft';
  const sx = side ? -2 : three ? -1 : 0; // deslocamento da cabeça para o lado olhado
  // pernas e pés (alternam ao andar)
  const lift = [[0, 0], [2, 0], [0, 0], [0, 2]][pose.step || 0];
  const feet = side ? [[20, lift[0]], [26, lift[1]]] : [[15, lift[0]], [27, lift[1]]];
  for (const [x, l] of feet) {
    m.rect(x, 39 - l, 6, 5, 'skin');
    m.rect(x - 2, 44 - l, 10, 2, 'skin'); // pé de sapo largo
  }
  // bermuda cargo
  m.rect(side ? 18 : 14, 33 + b, side ? 14 : 20, 7, 'shorts');
  if (!back && !side) { m.rect(14, 35 + b, 4, 3, 'spot'); m.rect(30, 35 + b, 4, 3, 'spot'); }
  // tronco (camisa) e barriga
  m.oval(24 + sx, 28 + b, side ? 9 : 12, 8, 'shirt');
  if (!back) m.oval(24 + sx * 2, 26 + b, side ? 5 : 7, 6, 'belly');
  // braços
  if (pose.raise) { m.rect(9, 12 + b, 5, 12, 'skin'); m.rect(34, 12 + b, 5, 12, 'skin'); }
  else if (!side) { m.rect(9, 24 + b, 5, 9, 'skin'); m.rect(34, 23 + b + (pose.strum === 1 ? 1 : 0), 5, 8, 'skin'); m.rect(9, 22 + b, 5, 3, 'shirt'); m.rect(34, 22 + b, 5, 2, 'shirt'); }
  else m.rect(20, 26 + b, 5, 8, 'skin');
  // cabeça larga de sapo com olhos no alto
  m.oval(24 + sx, 15 + b, side ? 11 : 14, 9, 'skin');
  if (!back) m.oval(24 + sx * 2, 21 + b, side ? 7 : 10, 3, 'belly');
  for (const [x, y] of [[13, 13], [35, 11], [18, 19], [31, 20], [24, 8]]) if (!side || x < 30) m.set(x + sx, y + b, 'spot').set(x + sx + 1, y + b, 'spot');
  const eyes = side ? [[15]] : three ? [[15], [29]] : [[15], [33]];
  for (const [x] of eyes) m.oval(x + sx, 8 + b, 4.5, 4.5, 'skin');
  // banjo
  if (!back) banjoMat(m, pose, b);
  const px = render(m, { ...FROG, skinM: FROG.skin }, { order: shadeOrder });
  // detalhes chapados por cima (olhos, boca, cordas)
  if (!back) {
    for (const [x] of eyes) eye(px, x + sx, 8 + b, pose.angry);
    if (pose.mouthOpen) { for (let x = 17 + sx * 2; x < 32 + sx * 2; x++) px.set(x, 19 + b, FROG.mouth); for (let x = 19 + sx * 2; x < 30 + sx * 2; x++) { px.set(x, 20 + b, FROG.tongue); px.set(x, 21 + b, FROG.mouth); } }
    else for (let x = (side ? 13 : 13) + sx * 2; x < (side ? 26 : 35) + sx * 2; x++) px.set(x, 18 + b + (x % 9 === 0 ? 1 : 0), FROG.mouth);
    if (pose.angry) for (const [x] of eyes) { px.set(x + sx - 3, 3 + b, FROG.rage); px.set(x + sx - 2, 3 + b, FROG.rage); px.set(x + sx - 1, 4 + b, FROG.rage); px.set(x + sx, 4 + b, FROG.rage); }
    banjoDetail(px, pose, b);
  } else {
    // costas: o braço do banjo aparece por cima do ombro
    for (let i = 0; i < 12; i++) { px.set(34 + i, 20 - i + b, FROG.banjo.b); px.set(34 + i, 21 - i + b, FROG.banjo.s); }
    px.set(46, 8 + b, FROG.peg); px.set(45, 8 + b, FROG.peg);
  }
  return px;
}

function banjoMat(m, pose, b) {
  if (pose.raise) { m.oval(24, 6 + b, 7, 6, 'drum'); m.rect(23, 12 + b, 2, 10, 'banjo'); return; }
  const side = pose.view === 'left';
  const cx = side ? 18 : 19, cy = 29 + b;
  m.oval(cx, cy, 6.5, 6.5, 'banjo');
  m.oval(cx, cy, 5, 5, 'drum');
  // braço do banjo até a mão direita
  for (let i = 0; i < 16; i++) { m.set(cx + 5 + i, cy - 3 - Math.round(i * 0.6), 'banjo'); m.set(cx + 5 + i, cy - 2 - Math.round(i * 0.6), 'banjo'); }
}

function banjoDetail(px, pose, b) {
  if (pose.raise) return;
  const side = pose.view === 'left';
  const cx = side ? 18 : 19, cy = 29 + b;
  for (let i = 0; i < 14; i++) px.set(cx + 2 + i, cy - 2 - Math.round(i * 0.6), FROG.string);
  for (const [dx, dy] of [[20, -13], [21, -14], [19, -14]]) px.set(cx + dx, cy + dy, FROG.peg);
  // mão dedilhando
  const sy = [0, 2, -1][pose.strum || 0];
  for (const [dx, dy] of [[-1, -1], [0, -1], [1, 0], [0, 0], [-1, 0]]) px.set(cx + dx, cy + dy + sy, FROG.skin.b);
}

function eye(px, x, y, angry) {
  for (let dy = -3; dy <= 3; dy++) for (let dx = -3; dx <= 3; dx++) {
    const d = dx * dx + dy * dy;
    if (d > 10) continue;
    px.set(x + dx, y + dy, d > 6 ? FROG.eyeO : d > 3 ? (angry ? FROG.rageEye : FROG.iris) : FROG.pupil);
  }
  px.set(x - 1, y - 1, FROG.glint);
  if (!angry) px.set(x + 1, y - 2, FROG.irisLight);
}

// ---------- Mago da Elétrica ----------

/** pose: { view, step, bob, cast: 0..2 (mão com raio), book: bool (livro erguido), angry, charge: 0..2 } */
function mage(pose) {
  const m = new Mat(48, 56);
  const v = pose.view, b = pose.bob || 0;
  const side = v === 'left', three = v === 'downleft' || v === 'upleft', back = v === 'up' || v === 'upleft';
  const sx = side ? -3 : three ? -1 : 0;
  // sapatos de bico curvado
  const lift = [[0, 0], [1, 0], [0, 0], [0, 1]][pose.step || 0];
  m.rect(side ? 18 : 14, 51 - lift[0], 8, 3, 'hat'); m.set(side ? 16 : 13, 50 - lift[0], 'hat');
  m.rect(side ? 24 : 26, 51 - lift[1], 8, 3, 'hat'); m.set(side ? 33 : 34, 50 - lift[1], 'hat');
  // túnica que alarga para baixo
  for (let y = 28; y < 52; y++) { const w = 7 + Math.round((y - 28) * 0.42); m.rect(24 + sx - w, y + b, w * 2, 1, 'robe'); }
  m.rect(13 + sx, 37 + b, 22, 2, 'belt');
  if (side) m.rect(22, 30 + b, 6, 9, 'robe'); // braço de perfil, atrás da barba
  // cabelo longo atrás dos ombros
  m.rect(14 + sx, 22 + b, 20, 16, 'hair');
  // barba
  if (!back) { m.oval(24 + sx * 2, 35 + b, side ? 4 : 6, 8, 'hair'); }
  // rosto
  if (!back) m.oval(24 + sx * 2, 26 + b, side ? 4 : 5, 4, 'skinM');
  // braços / mangas
  if (!side) {
    const castUp = pose.cast > 0;
    m.rect(castUp ? 6 : 10, (castUp ? 24 : 30) + b, 6, castUp ? 8 : 10, 'robe'); // manga esquerda (mão do raio)
    m.rect(32, 30 + b, 6, 9, 'robe');
    m.rect(castUp ? 5 : 10, (castUp ? 20 : 39) + b, 5, 4, 'skinM');
    if (!back) { const by = pose.book ? 18 : 34; m.rect(31, by + b, 10, 8, 'book'); }
  }
  // chapéu: aba larga, faixa, cone com ponta caída
  m.oval(24 + sx, 21 + b, side ? 14 : 17, 3, 'hat');
  for (let y = 4; y < 20; y++) { const w = Math.max(1, Math.round((y - 3) * 0.55)); m.rect(24 + sx - w + Math.round((20 - y) * 0.15), y + b, w * 2, 1, 'hat'); }
  m.rect(26 + sx, 2 + b, 6, 3, 'hat'); m.set(32 + sx, 4 + b, 'hat').set(33 + sx, 5 + b, 'hat');
  m.rect(16 + sx, 16 + b, 17, 3, 'band');
  const px = render(m, { ...MAGE, skinM: MAGE.skin }, { order: shadeOrder });
  // detalhes chapados
  if (!back) {
    const ex = 24 + sx * 2, ey = 25 + b;
    const eyeCol = pose.angry ? MAGE.rageEye : MAGE.eye;
    if (!side) { px.set(ex - 2, ey, eyeCol); px.set(ex + 2, ey, eyeCol); } else px.set(ex - 2, ey, eyeCol);
    // sobrancelhas grossas e brancas (franzidas)
    for (const [dx, dy] of side ? [[-3, -1], [-2, -2]] : [[-3, -2], [-2, -1], [-1, -1], [3, -2], [2, -1], [1, -1]]) px.set(ex + dx, ey + dy + (pose.angry ? 1 : 0), MAGE.brow);
    for (let x = ex - 1; x <= ex + 1; x++) px.set(x, ey + 3, MAGE.mouth);
    px.set(ex + 1, ey + 1, MAGE.skin.s); px.set(ex, ey + 2, MAGE.skin.s);
    px.set(18 + sx, 16 + b, MAGE.buckle); px.set(19 + sx, 16 + b, MAGE.buckle); px.set(18 + sx, 17 + b, MAGE.buckle); px.set(18 + sx, 18 + b, MAGE.buckle);
    px.set(16 + sx, 40 + b, MAGE.gem); px.set(16 + sx, 41 + b, MAGE.gem);
    if (!side) {
      const by = (pose.book ? 18 : 34) + b;
      for (let x = 32; x < 40; x++) px.set(x, by + 1, MAGE.page);
      px.set(35, by + 4, '#3a2a1a'); px.set(36, by + 5, '#3a2a1a'); px.set(37, by + 4, '#3a2a1a');
      // raio na mão esquerda
      const hx = pose.cast > 0 ? 7 : 12, hy = (pose.cast > 0 ? 19 : 40) + b;
      orb(px, hx, hy, (pose.cast || 0) + (pose.angry ? 1 : 0), pose.charge || 0);
    }
  }
  if (pose.angry) for (const [x, y] of [[6, 30], [41, 26], [10, 46], [38, 44], [24, 1]]) { px.set(x, y + b, MAGE.bolt); px.set(x + 1, y + 1 + b, MAGE.boltCore); }
  return px;
}

function orb(px, x, y, power, charge) {
  const r = 2 + Math.min(3, power + charge);
  for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
    const d = dx * dx + dy * dy;
    if (d <= r * r) px.set(x + dx, y + dy, d < (r - 1) * (r - 1) ? MAGE.boltCore : MAGE.bolt);
  }
  // faíscas
  const arms = 3 + power;
  for (let k = 0; k < arms; k++) {
    let cx = x, cy = y;
    const a = k * 2.1 + charge;
    for (let i = 0; i < 4 + power; i++) { cx += Math.round(Math.cos(a + (i % 2 ? 0.7 : -0.7))); cy += Math.round(Math.sin(a + (i % 2 ? 0.7 : -0.7))); px.set(cx, cy, i % 2 ? MAGE.bolt : MAGE.boltDark); }
  }
}

// ---------- animações (as mesmas para os dois) ----------

const VIEWS = ['down', 'up', 'left', 'downleft', 'upleft'];
const MIRROR = { right: 'left', downright: 'downleft', upright: 'upleft' };
export const BOSS_DIRS = ['down', 'up', 'left', 'right', 'downleft', 'downright', 'upleft', 'upright'];
export const BOSS_ANIMS = {
  walk: 4, walk_bravo: 4, idle: 2, idle_bravo: 2, attack: 3, attack_bravo: 3, special: 3, special_bravo: 3,
  charge: 2, hurt: 2, heavy: 2, rage: 3, death: 4, vanish: 3,
};

function whiteTint(p) { return p.map((v) => (v === C.outline ? v : C.white)); }
function fade(p, keepEvery) { const o = p.clone(); for (let y = 0; y < o.h; y++) for (let x = 0; x < o.w; x++) if ((x + y * 3) % keepEvery !== 0) o.set(x, y, null); return o; }
function redRing(p, parity) { const o = p.clone(); for (const [x, y] of p.ring()) if ((x + y) % 2 === parity) o.set(x, y, C.red2); return o; }
function flatten(p, k) {
  const o = new Px(p.w, p.h);
  for (let y = 0; y < p.h; y++) for (let x = 0; x < p.w; x++) { const c = p.get(x, y); if (c) o.set(x, p.h - 1 - Math.round((p.h - 1 - y) * k), c); }
  return o;
}

function framesOf(draw) {
  const f = {};
  for (const view of VIEWS) {
    f[`walk_${view}`] = [0, 1, 2, 3].map((i) => draw({ view, step: i, bob: i % 2 ? -1 : 0 }));
    f[`walk_bravo_${view}`] = [0, 1, 2, 3].map((i) => draw({ view, step: i, bob: i % 2 ? -1 : 0, angry: true, mouthOpen: true }));
  }
  for (const [r, l] of Object.entries(MIRROR)) {
    f[`walk_${r}`] = f[`walk_${l}`].map((p) => p.flipX());
    f[`walk_bravo_${r}`] = f[`walk_bravo_${l}`].map((p) => p.flipX());
  }
  const d = (pose) => draw({ view: 'down', ...pose });
  f.idle_down = [d({}), d({ bob: 1, strum: 1 })];
  f.idle_bravo_down = [d({ angry: true, mouthOpen: true }), d({ angry: true, mouthOpen: true, bob: 1 })];
  f.attack_down = [d({ strum: 1, cast: 1 }), d({ strum: 2, cast: 2, mouthOpen: true }), d({ strum: 0, cast: 1 })];
  f.attack_bravo_down = [d({ strum: 1, cast: 2, angry: true, mouthOpen: true }), d({ strum: 2, cast: 2, angry: true, mouthOpen: true, bob: -1 }), d({ strum: 0, cast: 1, angry: true })];
  f.special_down = [d({ raise: true, book: true, cast: 1 }), d({ raise: true, book: true, cast: 2, bob: -1 }), d({ raise: true, book: true, cast: 2, mouthOpen: true })];
  f.special_bravo_down = [d({ raise: true, book: true, cast: 2, angry: true }), d({ raise: true, book: true, cast: 2, angry: true, bob: -2, mouthOpen: true }), d({ raise: true, book: true, cast: 2, angry: true, mouthOpen: true })];
  f.charge_down = [d({ charge: 1, book: true, cast: 1, bob: 1 }), d({ charge: 2, book: true, cast: 2 })];
  f.hurt_down = [whiteTint(d({})), d({}).shift(-1, 0)];
  f.heavy_down = [d({ mouthOpen: true }).shift(-3, 1), d({ mouthOpen: true }).shift(-1, 0)];
  f.rage_down = [redRing(d({ angry: true, mouthOpen: true }), 0), redRing(d({ angry: true, mouthOpen: true, raise: true, book: true, cast: 2, bob: -2 }), 1), d({ angry: true, mouthOpen: true, raise: true, cast: 2 })];
  const dead = d({ mouthOpen: true });
  f.death_down = [whiteTint(dead), dead.shift(0, 1), flatten(dead, 0.6), flatten(dead, 0.3)];
  f.vanish_down = [fade(flatten(dead, 0.3), 2), fade(flatten(dead, 0.3), 4), fade(flatten(dead, 0.3), 9)];
  return f;
}

export function bossSprites() {
  const out = [];
  for (const [id, draw, w, h] of [['guirotto', frog, 48, 48], ['mago', mage, 48, 56]]) {
    const f = framesOf(draw);
    for (const [anim, n] of Object.entries(BOSS_ANIMS)) {
      const dirs = anim.startsWith('walk') ? BOSS_DIRS : ['down'];
      for (const dir of dirs) {
        const list = f[`${anim}_${dir}`];
        if (!list || list.length !== n) throw new Error(`boss ${id}: animação incompleta ${anim} ${dir}`);
        list.forEach((px, i) => out.push({ id: `boss_${id}_${anim}_${dir}_${i}`, category: 'Bosses', sub: id, w, h, pivot: [w / 2, 2], px }));
      }
    }
  }
  return out;
}

for (const pal of [FROG, MAGE])
  for (const v of Object.values(pal))
    for (const hex of typeof v === 'string' ? [v] : Object.values(v)) if (!PALETTE_HEX.includes(hex)) PALETTE_HEX.push(hex);

// ---------- projéteis dos bosses (Effects) ----------

function blob(w, h, fn) { const p = new Px(w, h); for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const c = fn(x, y); if (c) p.set(x, y, c); } return p; }

export function bossFxSprites() {
  const C0 = '#2a1a0a', note = '#ffd23f', noteDark = '#c8781a';
  // nota musical (colcheia) 10×10
  const n = new Px(10, 10);
  for (let y = 1; y < 8; y++) n.set(6, y, C0);
  for (const [x, y] of [[7, 1], [8, 2], [8, 3]]) n.set(x, y, C0);
  for (let y = 6; y <= 8; y++) for (let x = 2; x <= 6; x++) if ((x - 4) ** 2 / 4 + (y - 7) ** 2 <= 1.6) n.set(x, y, note);
  n.set(3, 7, noteDark); n.set(4, 8, noteDark); n.set(3, 6, '#fff6d8');
  for (const [x, y] of n.ring()) n.set(x, y, C0);
  // onda sonora: arco 14×14
  const wave = blob(14, 14, (x, y) => { const d = Math.hypot(x - 2, y - 7); return d > 8.6 && d < 11.2 && Math.abs(y - 7) < 6.5 ? (d < 9.6 ? '#93a84a' : d < 10.4 ? '#e2d29a' : '#6f8a2e') : null; });
  // orbe elétrico 12×12 (2 quadros)
  const orb = (k) => blob(12, 12, (x, y) => { const d = Math.hypot(x - 5.5, y - 5.5); if (d < 2.6) return '#ffffff'; if (d < 4) return MAGE.bolt; if (d < 5.2 && (x * 3 + y * 5 + k * 7) % 4 === 0) return MAGE.boltDark; return null; });
  const spark = blob(7, 7, (x, y) => { const d = Math.abs(x - 3) + Math.abs(y - 3); return d === 0 ? '#ffffff' : d <= 1 ? MAGE.bolt : d <= 3 && (x === 3 || y === 3) ? MAGE.boltDark : null; });
  const fx = (id, px) => ({ id, category: 'Effects', w: px.w, h: px.h, pivot: [px.w / 2, px.h / 2], px });
  return [fx('fx_boss_note', n), fx('fx_boss_wave', wave), fx('fx_boss_orb_0', orb(0)), fx('fx_boss_orb_1', orb(1)), fx('fx_boss_spark', spark)];
}
