// Tiles (32×32), objetos do cenário, efeitos, armas e interface em pixel art.
import { C } from './palette.mjs';
import { Px, rect, box, line, disc, ellipse, circleLine, stamp, rng } from './draw.mjs';

function floor(variant) {
  const p = new Px(32, 32), r = rng(100 + variant);
  rect(p, 0, 0, 32, 32, C.gray2);
  for (let i = 0; i < 70; i++) p.set(Math.floor(r() * 32), Math.floor(r() * 32), r() < 0.5 ? C.gray1 : C.gray3);
  rect(p, 0, 0, 32, 1, C.gray1); rect(p, 0, 0, 1, 32, C.gray1); rect(p, 0, 31, 32, 1, C.black); rect(p, 31, 0, 1, 32, C.black);
  for (const [x, y] of [[3, 3], [28, 3], [3, 28], [28, 28]]) { p.set(x, y, C.gray4); p.set(x + 1, y + 1, C.gray1); }
  if (variant === 1) { line(p, 6, 22, 14, 18, C.gray1); line(p, 14, 18, 20, 21, C.gray1); line(p, 20, 21, 26, 15, C.gray1); }
  if (variant === 2) { ellipse(p, 18, 15, 7, 4, C.black, null); ellipse(p, 16, 14, 2, 1, C.gray2, null); }
  if (variant === 3) { box(p, 4, 4, 24, 24, C.gray1); for (let x = 7; x < 26; x += 3) rect(p, x, 6, 1, 20, C.black); }
  return p;
}

function hazardEdge() {
  const p = floor(0);
  for (let x = 0; x < 32; x++) for (let y = 0; y < 6; y++) p.set(x, y, ((x + y) >> 2) % 2 ? C.yellow2 : C.outline);
  rect(p, 0, 6, 32, 1, C.black);
  return p;
}

function wall() {
  const p = new Px(32, 32);
  rect(p, 0, 0, 32, 32, C.gray1); rect(p, 0, 0, 32, 4, C.gray2);
  box(p, -1, 10, 34, 5, C.gray3); rect(p, 0, 11, 32, 1, C.gray4);
  box(p, -1, 18, 34, 4, C.rust2); rect(p, 0, 19, 32, 1, C.rust3);
  box(p, 7, 8, 4, 9, C.gray4); box(p, 22, 16, 4, 8, C.gray4);
  box(p, 12, 24, 8, 6, C.yellow2); p.set(15, 26, C.outline); p.set(16, 27, C.outline);
  return p;
}

function bench() { // 96×32, bordas de 8 px para 9-slice
  const p = new Px(96, 32);
  box(p, 1, 3, 94, 27, C.gray3); rect(p, 2, 4, 92, 5, C.gray4); rect(p, 2, 4, 92, 1, C.gray5);
  box(p, 12, 6, 18, 12, C.gray1); rect(p, 13, 7, 16, 10, C.petrol2); line(p, 14, 14, 18, 11, C.cyan2); line(p, 18, 11, 22, 13, C.cyan2); line(p, 22, 13, 27, 9, C.cyan2);
  box(p, 38, 9, 5, 9, C.cyan3); rect(p, 39, 13, 3, 4, C.cyan2);
  box(p, 46, 7, 6, 11, C.yellow3); rect(p, 47, 12, 4, 5, C.amber);
  box(p, 60, 8, 22, 8, C.offwhite); line(p, 62, 11, 78, 11, C.gray4); line(p, 62, 13, 74, 13, C.gray4);
  disc(p, 87, 12, 3, C.red2);
  box(p, 6, 22, 84, 5, C.gray1);
  return p;
}

function cabinet(vertical) {
  const p = vertical ? new Px(32, 96) : new Px(96, 32);
  box(p, 0, 0, p.w, p.h, C.petrol1);
  for (let i = 0; i < 4; i++) {
    if (vertical) { box(p, 3, 4 + i * 23, 26, 19, C.petrol2); rect(p, 12, 12 + i * 23, 8, 2, C.gray5); }
    else { box(p, 4 + i * 23, 3, 19, 26, C.petrol2); rect(p, 12 + i * 23, 12, 2, 8, C.gray5); }
  }
  return p;
}

function cables() {
  const p = new Px(32, 32);
  for (let x = 0; x < 32; x++) {
    const y1 = 20 + Math.round(Math.sin(x / 5) * 3), y2 = 25 + Math.round(Math.cos(x / 6) * 2);
    p.set(x, y1 - 1, C.outline); p.set(x, y1, C.red2); p.set(x, y1 + 1, C.outline);
    p.set(x, y2 - 1, C.outline); p.set(x, y2, C.cyan1); p.set(x, y2 + 1, C.outline);
  }
  return p;
}

export function worldSprites() {
  const s = (id, category, px, extra = {}) => ({ id, category, w: px.w, h: px.h, pivot: [px.w / 2, px.h / 2], px, ...extra });
  const W = C.white;
  const fx = [];
  // efeitos (brancos/claros para tingir na Unity quando fizer sentido)
  const spark = stamp(new Px(8, 8), ['...W....', '...W....', '..WYW...', 'WWYYYWW.', '..WYW...', '...W....', '...W....', '........'], { W, Y: C.yellow3 });
  fx.push(s('fx_spark', 'Effects', spark));
  const dot = stamp(new Px(4, 4), ['.WW.', 'WWWW', 'WWWW', '.WW.'], { W });
  fx.push(s('fx_dot', 'Effects', dot));
  for (let f = 0; f < 3; f++) {
    const arc = new Px(16, 16);
    let y = 8;
    const r = rng(7 + f);
    for (let x = 0; x < 16; x++) { const ny = Math.max(2, Math.min(13, y + Math.round((r() - 0.5) * 4))); line(arc, x, y, x, ny, W); y = ny; }
    fx.push(s(`fx_arc_${f}`, 'Effects', arc));
  }
  fx.push(s('fx_laser', 'Effects', stamp(new Px(16, 4), ['.OOOOOOOOOOOOOO.', 'OoAAAAAAAAAAAAoO', 'OoAAAAAAAAAAAAoO', '.OOOOOOOOOOOOOO.'], { O: C.orange1, o: C.orange2, A: C.yellow3 })));
  fx.push(s('fx_hostile_bolt', 'Effects', stamp(new Px(8, 8), ['..OOOO..', '.ORRRRO.', 'ORYRRRRO', 'ORRRRRRO', 'ORRRRRRO', 'ORRRRRRO', '.ORRRRO.', '..OOOO..'], { O: C.outline, R: C.red2, Y: C.red3 })));
  fx.push(s('fx_gem_xp', 'Effects', stamp(new Px(8, 8), ['...OO...', '..OcCO..', '.OcCCCO.', 'OcCCCCaO', '.OCCCaO.', '..OCaO..', '...OO...', '........'], { O: C.outline, c: C.cyan3, C: C.cyan2, a: C.cyan1 })));
  fx.push(s('fx_gem_xp_big', 'Effects', stamp(new Px(12, 12), ['.....OO.....', '....OyYO....', '...OyYYYO...', '..OyYYYYYO..', '.OyYYYYYYaO.', 'OyYYYYYYYYaO', '.OYYYYYYYaO.', '..OYYYYYaO..', '...OYYYaO...', '....OYaO....', '.....OO.....', '............'], { O: C.outline, y: C.yellow3, Y: C.amber, a: C.orange1 })));
  fx.push(s('fx_heal', 'Effects', stamp(new Px(10, 10), ['..OOOOOO..', '.OWWWWWWO.', 'OWWWGGWWWO', 'OWWGGGGWWO', 'OWWWGGWWWO', '.OWWWWWWO.', '..OOOOOO..', '..........', '..........', '..........'], { O: C.outline, W: C.white, G: C.green2 })));
  for (let f = 0; f < 4; f++) {
    const p = new Px(32, 32), r = 3 + f * 4;
    disc(p, 15.5, 15.5, r, f < 2 ? C.yellow3 : C.orange2, f < 3 ? C.orange2 : C.gray3);
    if (f >= 1) disc(p, 15.5, 15.5, Math.max(1, r - 4), f < 3 ? C.white : C.gray2, null);
    const rr = rng(40 + f);
    for (let i = 0; i < 6 + f * 3; i++) { const a = rr() * 6.283, d = r + 2 + rr() * 4; p.set(Math.round(15.5 + Math.cos(a) * d), Math.round(15.5 + Math.sin(a) * d), f < 3 ? C.yellow2 : C.gray4); }
    fx.push(s(`fx_explosion_${f}`, 'Effects', p));
  }
  const smoke = new Px(16, 16); disc(smoke, 6, 9, 4, C.gray4, null); disc(smoke, 10, 7, 5, C.gray5, null); disc(smoke, 9, 10, 3, C.gray4, null);
  fx.push(s('fx_smoke', 'Effects', smoke));
  const shadow = new Px(16, 8); ellipse(shadow, 7.5, 3.5, 7, 3, C.black, null);
  fx.push(s('fx_shadow', 'Effects', shadow));
  const ring = new Px(64, 64); circleLine(ring, 31.5, 31.5, 30, W); circleLine(ring, 31.5, 31.5, 29, C.cyan3);
  fx.push(s('fx_ring', 'Effects', ring));

  // armas visíveis
  const arm = new Px(16, 16); disc(arm, 7.5, 7.5, 5, C.gray4); for (const [x, y] of [[7, 0], [7, 14], [0, 7], [14, 7]]) box(arm, x - 1, y - 1, 3, 3, C.gray5); disc(arm, 7.5, 7.5, 1.6, C.orange2, null);
  const emitter = new Px(16, 16); box(emitter, 2, 5, 10, 6, C.gray3); box(emitter, 11, 6, 4, 4, C.gray5); rect(emitter, 4, 7, 5, 1, C.cyan2);

  return [
    ...[0, 1, 2, 3].map((v) => s(`tile_floor_${v}`, 'Tilesets', floor(v))),
    s('tile_hazard_edge', 'Tilesets', hazardEdge()),
    s('tile_wall', 'Tilesets', wall()),
    s('obstacle_bancada', 'Environment', bench(), { border: [8, 8, 8, 8] }),
    s('obstacle_armario_v', 'Environment', cabinet(true), { border: [8, 8, 8, 8] }),
    s('obstacle_armario_h', 'Environment', cabinet(false), { border: [8, 8, 8, 8] }),
    s('decal_cables', 'Environment', cables()),
    ...fx,
    s('weapon_orbit_arm', 'Weapons', arm),
    s('weapon_laser_emitter', 'Weapons', emitter),
    s('ui_panel', 'UI', (() => { const p = new Px(24, 24); box(p, 0, 0, 24, 24, C.gray1, C.gray3); rect(p, 1, 1, 22, 1, C.gray2); for (let i = 0; i < 6; i++) p.set(1 + i, 6 - i, C.yellow2); return p; })(), { border: [8, 8, 8, 8] }),
    s('ui_button', 'UI', (() => { const p = new Px(24, 12); box(p, 0, 0, 24, 12, C.gray2, C.gray4); rect(p, 1, 1, 22, 2, C.gray3); return p; })(), { border: [4, 4, 4, 4] }),
  ];
}
