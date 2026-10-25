// Ícones de habilidades 32×32 (moldura na cor da raridade).
import { C } from './palette.mjs';
import { Px, rect, box, line, disc, ellipse, circleLine, stamp } from './draw.mjs';
import { iconArt } from './kitart.mjs';

const RARITY = { comum: C.gray5, incomum: C.cyan2, raro: C.amber };

function frame(rarity) {
  const p = new Px(32, 32);
  box(p, 0, 0, 32, 32, RARITY[rarity] || C.gray5);
  box(p, 1, 1, 30, 30, C.gray1);
  rect(p, 2, 2, 28, 1, C.gray2);
  return p;
}

const bolt = (p, dx, col) => stamp(p, ['....OOO', '...OYYO', '..OYYO.', '.OYYOOO', 'OYYYYYO', 'OOOYYO.', '..OYYO.', '.OYYO..', '.OYO...', '.OO....'], { O: C.outline, Y: col }, 11 + dx, 10);

const SYMBOL = {
  descarga_direcionada: (p) => bolt(p, 1, C.cyan2),
  pulso_amplificado: (p) => { bolt(p, -5, C.cyan2); bolt(p, 6, C.cyan2); bolt(p, 1, C.amber); },
  braco_giratorio: (p) => { disc(p, 15.5, 15.5, 3, C.gray4); line(p, 16, 15, 22, 8, C.outline); line(p, 17, 15, 23, 9, C.gray5); box(p, 21, 5, 6, 5, C.orange2); for (let a = 0; a < 6; a++) p.set(Math.round(15.5 + Math.cos(2 + a * 0.5) * 10), Math.round(15.5 + Math.sin(2 + a * 0.5) * 10), C.white); },
  rotor_de_impacto: (p) => { disc(p, 15.5, 15.5, 4, C.orange2); for (const [x, y] of [[15, 5], [7, 21], [24, 21]]) { line(p, 15, 15, x, y, C.outline); box(p, x - 2, y - 2, 5, 5, C.gray5); } line(p, 4, 10, 8, 10, C.white); line(p, 3, 14, 7, 14, C.white); },
  laser_industrial: (p) => { box(p, 5, 12, 9, 8, C.gray4); rect(p, 7, 14, 4, 1, C.cyan2); box(p, 13, 14, 15, 4, C.orange2); rect(p, 14, 15, 13, 2, C.yellow3); },
  campo_eletromagnetico: (p) => { circleLine(p, 15.5, 15.5, 11, C.cyan2); circleLine(p, 15.5, 15.5, 7, C.cyan3); stamp(p, ['OO..OO', 'OR..RO', 'OR..RO', 'ORRRRO', '.OOOO.'], { O: C.outline, R: C.red2 }, 13, 13); },
  bateria_portatil: (p) => { box(p, 10, 7, 12, 20, C.gray3); box(p, 13, 5, 6, 3, C.gray5); for (let i = 0; i < 3; i++) box(p, 12, 21 - i * 6, 8, 5, C.green2); },
  turbina: (p) => { disc(p, 15.5, 15.5, 11, C.gray3); for (let i = 0; i < 5; i++) { const a = i * 1.2566; line(p, 16, 16, Math.round(16 + Math.cos(a) * 9), Math.round(16 + Math.sin(a) * 9), C.gray5); } disc(p, 15.5, 15.5, 2, C.orange2); },
  attr_dano: (p) => stamp(p, ['......OO......', '.....ORRO.....', 'OO..ORRRRO..OO', 'ORRORRRRRRORRO', '.ORRRRRRRRRRO.', '..ORRRRRRRRO..', '.ORRRRRRRRRRO.', 'ORRRRRRRRRRRRO', '.OOORRRRRROOO.', '....ORROORO...', '.....OO..OO...'], { O: C.outline, R: C.red2 }, 9, 10),
  attr_vida: (p) => { stamp(p, ['.OOO..OOO.', 'ORRRO ORRRO'.replace(' ', 'O'), 'ORRRRRRRRRO', 'ORRRRRRRRRO', '.ORRRRRRRO.', '..ORRRRRO..', '...ORRRO...', '....ORO....', '.....O.....'], { O: C.outline, R: C.red2 }, 10, 10); line(p, 9, 15, 13, 15, C.white); line(p, 13, 15, 15, 12, C.white); line(p, 15, 12, 17, 18, C.white); line(p, 17, 18, 19, 15, C.white); line(p, 19, 15, 23, 15, C.white); },
  attr_recuperacao: (p) => { circleLine(p, 15.5, 15.5, 10, C.green2); rect(p, 13, 9, 6, 14, C.white); rect(p, 9, 13, 14, 6, C.white); },
  attr_coleta: (p) => stamp(p, ['OOOO..OOOO', 'OWWO..OWWO', 'ORRO..ORRO', 'ORRO..ORRO', 'ORRO..ORRO', 'ORROOOORRO', '.ORRRRRRO.', '..OOOOOO..'], { O: C.outline, W: C.gray5, R: C.red2 }, 11, 11),
  attr_vel_ataque: (p) => { disc(p, 17.5, 17.5, 8, C.white); line(p, 17, 17, 17, 12, C.outline); line(p, 17, 17, 21, 19, C.outline); box(p, 15, 7, 5, 3, C.gray4); line(p, 4, 13, 8, 13, C.amber); line(p, 3, 17, 7, 17, C.amber); line(p, 4, 21, 8, 21, C.amber); },
  kit_primeiros_socorros: (p) => { box(p, 6, 9, 20, 16, C.white); box(p, 12, 6, 8, 4, C.gray4); rect(p, 14, 12, 4, 10, C.red2); rect(p, 11, 15, 10, 4, C.red2); },
  apagao: (p) => { circleLine(p, 15.5, 15.5, 12, C.cyan2); disc(p, 15.5, 13.5, 6, C.gray2); box(p, 12, 19, 8, 5, C.gray4); stamp(p, ['.YY', 'YY.', '.YY', 'YY.'], { Y: C.amber }, 14, 10); },
  turbina_emergencia: (p) => { disc(p, 15.5, 13.5, 9, C.gray3); for (let i = 0; i < 4; i++) { const a = i * 1.5708 + 0.4; line(p, 16, 14, Math.round(16 + Math.cos(a) * 7), Math.round(14 + Math.sin(a) * 7), C.gray5); } stamp(p, ['.O.O.O.', 'OYOYOYO', 'OYYYYYO', '.OOOOO.'], { O: C.orange1, Y: C.orange2 }, 12, 24); },
  // personagens: arma inicial e evoluída
  bolhas: (p) => iconArt(p, ['bubblebig']),
  bolhas_e_maletas: (p) => iconArt(p, ['bubble', 'briefcase']),
  calculos: (p) => iconArt(p, ['digit', 'expression']),
  matematica_sushi: (p) => iconArt(p, ['digit', 'sushi']),
  componentes_eletricos: (p) => iconArt(p, ['resistor', 'transistor']),
  fontes_osciloscopios: (p) => iconArt(p, ['powersupply', 'scopewave']),
  lanches: (p) => iconArt(p, ['burger', 'dorito']),
  pneus_crossfit: (p) => iconArt(p, ['tire']),
  bonesmashing: (p) => stamp(p, ['..OOOOOO..', '.OSSSSSSO.', 'OSSOSSOSSO', 'OSSOSSOSSO', 'OSSSSSSSSO', 'OSSSSSSSSO', '.OSSSSSSO.', '..OSSSSO..', '...OJJO...', '...OJJO...'].map((r) => r.replace(/ /g, '.')), { O: C.outline, S: C.skinC2, J: C.white }, 11, 10),
  pilulas_seringas: (p) => iconArt(p, ['pill', 'syringe']),
  cabazords: (p) => iconArt(p, ['botshooter', 'botbrawler', 'botdasher']),
  cabazord_master: (p) => iconArt(p, ['botmaster']),
  celulares_cola: (p) => iconArt(p, ['phone', 'paper']),
  computadores_tvs: (p) => iconArt(p, ['computer']),
  jabutis: (p) => iconArt(p, ['jabuti']),
  carro_drift: (p) => iconArt(p, ['car']),
  socos_fortes: (p) => { stamp(p, ['..OOOOOO..', '.OSSSSSSO.', 'OSSOSSOSSO', 'OSSOSSOSSO', 'OSSSSSSSSO', 'OSSSSSSSSO', '.OSSSSSSO.', '..OSSSSO..', '...OAAO...', '...OSSO...'], { O: C.outline, S: C.skinB3, A: C.green2 }, 11, 10); line(p, 4, 12, 8, 12, C.yellow3); line(p, 4, 18, 8, 18, C.yellow3); },
  socos_insulina: (p) => iconArt(p, ['insulin', 'vial']),
  fala_alta: (p) => iconArt(p, ['soundwave']),
  fala_amplificada: (p) => iconArt(p, ['soundwave', 'bottle']),
  // componentes novos
  cartao_corporativo: (p) => { box(p, 5, 9, 22, 14, C.gold); rect(p, 6, 12, 20, 3, C.outline); box(p, 8, 16, 6, 4, C.yellow1); rect(p, 17, 19, 7, 1, C.yellow1); },
  cafe_expresso: (p) => { box(p, 8, 12, 14, 13, C.white); box(p, 21, 15, 5, 6, null); rect(p, 10, 13, 10, 3, C.rust1); for (const x of [11, 15, 19]) line(p, x, 9, x + 1, 5, C.gray5); },
  halter: (p) => { rect(p, 9, 15, 14, 2, C.gray5); for (const x of [4, 7, 22, 25]) box(p, x, 10, 3, 12, C.gray2); },
  marmita: (p) => { box(p, 5, 11, 22, 13, C.gray4); rect(p, 7, 13, 18, 4, C.white); rect(p, 7, 18, 8, 4, C.rust2); rect(p, 16, 18, 9, 4, C.lime1); box(p, 13, 8, 6, 4, C.gray5); },
  pote_whey: (p) => { box(p, 8, 9, 16, 17, C.black); box(p, 7, 6, 18, 5, C.gray3); rect(p, 10, 14, 12, 6, C.red2); stamp(p, ['W.W.W', 'W.W.W', '.W.W.'], { W: C.white }, 13, 15); },
  kit_arduino: (p) => { box(p, 5, 8, 22, 16, C.cyan1); for (let i = 0; i < 8; i++) { p.set(8 + i * 2, 9, C.gray5); p.set(8 + i * 2, 22, C.gray5); } box(p, 12, 12, 8, 6, C.black); disc(p, 9, 19, 1, C.red2, null); },
  cabo_hdmi: (p) => { box(p, 5, 12, 10, 8, C.gray3); rect(p, 7, 14, 6, 4, C.black); line(p, 15, 16, 27, 16, C.black); line(p, 15, 17, 27, 17, C.gray2); },
  megafone: (p) => { stamp(p, ['......OO', '....OOYO', 'OOOOYYYO', 'OGGOYYYO', 'OGGOYYYO', 'OOOOYYYO', '....OOYO', '......OO'], { O: C.outline, Y: C.orange2, G: C.gray4 }, 9, 12); line(p, 21, 11, 24, 8, C.white); line(p, 22, 16, 26, 16, C.white); line(p, 21, 21, 24, 24, C.white); },
  overclock: (p) => { box(p, 7, 7, 18, 18, C.gray2); rect(p, 9, 9, 14, 14, C.green1); for (let i = 0; i < 5; i++) { p.set(4, 9 + i * 3, C.gray5); p.set(27, 9 + i * 3, C.gray5); p.set(9 + i * 3, 4, C.gray5); p.set(9 + i * 3, 27, C.gray5); } stamp(p, ['..YY', '.YY.', 'YYYY', '.YY.', 'YY..'], { Y: C.yellow3 }, 14, 13); },
};

export function iconSprites(content) {
  const out = [];
  const rarity = new Map(content.abilities.map((a) => [a.id, a.rarity]));
  for (const s of content.specials) rarity.set(s.id, 'raro');
  for (const [id, r] of rarity) {
    const fn = SYMBOL[id];
    if (!fn) throw new Error(`sem ícone para ${id}`);
    const p = frame(r);
    fn(p);
    out.push({ id: `icon_${id}`, category: 'Abilities', w: 32, h: 32, pivot: [16, 16], px: p });
  }
  return out;
}
