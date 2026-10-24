// Personagens 32×32 em pixel art nativo: mapas de pixels por vista + camadas por engenharia.
// Símbolos: O contorno · S/s pele · H/h cabelo · C/c/k chapéu · J/j/L roupa · P/p calça · B bota
// G luva · E olho · N boca · K olheira · W claro · A/a acento · M/m metal · X sujeira · R vermelho
// Y brilho · _ apaga
import { C } from './palette.mjs';
import { Px } from './pixel.mjs';

export const SPECS = ['mecatronica', 'computacao', 'eletrica', 'civil', 'mecanica', 'biomedica', 'quimica'];

export const CHAR_ANIMS = {
  idle: { dirs: ['down', 'up', 'left', 'right'], frames: 2 },
  walk: { dirs: ['down', 'up', 'left', 'right'], frames: 4 },
  attack: { dirs: ['down'], frames: 3 },
  special: { dirs: ['down'], frames: 3 },
  hurt: { dirs: ['down'], frames: 2 },
  stun: { dirs: ['down'], frames: 2 },
  downed: { dirs: ['down'], frames: 3 },
  revive: { dirs: ['down'], frames: 2 },
  victory: { dirs: ['down'], frames: 2 },
};

// ---------- Corpo base (linhas 0–23) e pernas (24–31) ----------

const FRONT = {
  7: [[12, 'OOOOOOOO']], 8: [[11, 'OHHHHHHHHO']], 9: [[10, 'OHHHHHHHHHHO']], 10: [[10, 'OHSSSSSSSSHO']],
  11: [[10, 'OSSSSSSSSSSO']], 12: [[10, 'OSSESSSSESSO']], 13: [[10, 'OSSESSSSESSO']], 14: [[10, 'OsSKSSSSKSsO']],
  15: [[11, 'OSSSNNSSSO']], 16: [[12, 'OsSSSSsO']], 17: [[9, 'OJJJJsSSsJJJJO']], 18: [[8, 'OJJJJJJJJJJJJJJO']],
  19: [[8, 'OJJOJJJJJJJJOJJO']], 20: [[8, 'OJJOJJJXJJJJOJJO']], 21: [[8, 'OjJOJJJJJJJJOJjO']],
  22: [[8, 'OGGOJJJJJJJjOGGO']], 23: [[9, 'OOOjjjjjjjjOOO']],
};
const BACK = {
  7: [[12, 'OOOOOOOO']], 8: [[11, 'OHHHHHHHHO']], 9: [[10, 'OHHHHHHHHHHO']], 10: [[10, 'OHHHHHHHHHHO']],
  11: [[10, 'OHHHHHHHHHHO']], 12: [[10, 'OhHHHHHHHHhO']], 13: [[10, 'OhhHHHHHHhhO']], 14: [[10, 'OShhhhhhhhSO']],
  15: [[11, 'OSSSSSSSSO']], 16: [[12, 'OsSSSSsO']], 17: [[9, 'OJJJJJJJJJJJJO']], 18: [[8, 'OJJJJJJJJJJJJJJO']],
  19: [[8, 'OJJOJJJJJJJJOJJO']], 20: [[8, 'OJJOJJJJJXJJOJJO']], 21: [[8, 'OjJOJJJJJJJJOJjO']],
  22: [[8, 'OGGOjJJJJJJjOGGO']], 23: [[9, 'OOOjjjjjjjjOOO']],
};
const SIDE = { // virado para a esquerda
  7: [[12, 'OOOOOOO']], 8: [[11, 'OHHHHHHHO']], 9: [[10, 'OHHHHHHHHHO']], 10: [[10, 'OSSHHHHHHHO']],
  11: [[10, 'OSSSSHHHHHO']], 12: [[10, 'OSESSShHHHO']], 13: [[9, 'OSSKSSSShHHO']], 14: [[10, 'ONSSSSSShHO']],
  15: [[11, 'OSSSSSShO']], 16: [[12, 'OsSSSsO']], 17: [[11, 'OsSJJJJJJO']], 18: [[10, 'OJJJJJJJJJJO']],
  19: [[10, 'OJJjjJJJJJJO']], 20: [[10, 'OJJjjJJXJJJO']], 21: [[10, 'OJJjjJJJJJjO']], 22: [[10, 'OJGGjJJJJJjO']],
  23: [[11, 'OjjjjjjjjO']],
};

const LEGS = {
  stand: { 24: [[11, 'OPPPPPPPPO']], 25: [[11, 'OPPPOOPPPO']], 26: [[11, 'OPPPOOPPPO']], 27: [[11, 'OpPPOOPPpO']], 28: [[11, 'OpPPOOPPpO']], 29: [[10, 'OBBBBOOBBBBO']], 30: [[10, 'OBBBBOOBBBBO']], 31: [[10, 'OOOOO..OOOOO']] },
  stepL: { 24: [[11, 'OPPPPPPPPO']], 25: [[11, 'OPPPOOPPPO']], 26: [[11, 'OPPPOOPPPO']], 27: [[11, 'OpPPOOPPpO']], 28: [[10, 'OBBBBOOPPpO']], 29: [[10, 'OBBBBOOBBBBO']], 30: [[10, 'OOOOO.OBBBBO']], 31: [[17, 'OOOOO']] },
  sideStand: { 24: [[12, 'OPPPPPPO']], 25: [[12, 'OPPPPPPO']], 26: [[12, 'OPPPpPPO']], 27: [[12, 'OPPPpPPO']], 28: [[12, 'OPPPpPPO']], 29: [[10, 'OBBBBBBBBBO']], 30: [[10, 'OBBBBBBBBBO']], 31: [[10, 'OOOOOOOOOOO']] },
  sideStride: { 24: [[12, 'OPPPPPPO']], 25: [[11, 'OPPPOOPPPO']], 26: [[10, 'OPPPO..OpppO']], 27: [[10, 'OPPO....OppO']], 28: [[9, 'OPPPO....OppO']], 29: [[8, 'OBBBBO....OBBBO']], 30: [[8, 'OBBBBO....OBBBO']], 31: [[8, 'OOOOOO....OOOOO']] },
  kneel: { 28: [[10, 'OPPPPPPPPPPO']], 29: [[9, 'OBBBpPPPPpBBO']], 30: [[9, 'OBBBOOOOOOBBO']], 31: [[9, 'OOOO......OOO']] },
};

// ---------- Engenharias: paleta por papel + camadas ----------

const BASE_ROLES = { O: C.outline, E: C.black, N: C.bags, K: C.bags, M: C.gray5, m: C.gray3, X: C.dirt, R: C.red2, Y: C.yellow3, W: C.white, _: 'erase' };

const SPEC = {
  eletrica: {
    roles: { S: C.skinA2, s: C.skinA1, H: C.hairBlack, h: C.black, C: C.yellow2, c: C.yellow1, k: C.yellow3, J: C.petrol2, j: C.petrol1, L: C.petrol3, P: C.denim2, p: C.denim1, B: C.rust1, G: C.orange2, A: C.cyan2, a: C.cyan1 },
    down: { 4: [[12, 'OOOOOOOO']], 5: [[11, 'OkCCCCCCCO']], 6: [[10, 'OkCCCAACCCcO']], 7: [[10, 'OCCCCAACCCcO']], 8: [[8, 'OccccccccccccccO']], 13: [[19, 'W']], 14: [[19, 'W']], 20: [[9, 'kkk'], [12, 'kkkkkkkk'], [21, 'kkk']] },
    up: { 4: [[12, 'OOOOOOOO']], 5: [[11, 'OkCCCCCCCO']], 6: [[10, 'OkCCCCCCCCcO']], 7: [[10, 'OCCCCCCCCCcO']], 8: [[8, 'OccccccccccccccO']], 15: [[14, 'OHHO']], 16: [[14, 'OHHO']], 17: [[14, 'OHhO']], 18: [[15, 'OO']], 20: [[9, 'kkk'], [12, 'kkkkkkkk'], [21, 'kkk']] },
    left: { 4: [[12, 'OOOOOOO']], 5: [[11, 'OkCCCCCO']], 6: [[10, 'OkCACCCCcO']], 7: [[10, 'OCCACCCCcO']], 8: [[7, 'OccccccccccccO']], 10: [[20, 'HO']], 11: [[20, 'HHO']], 12: [[20, 'HHO']], 13: [[21, 'HO']], 14: [[21, 'O']], 20: [[11, 'kkkkkkkkkk']] },
  },
  civil: {
    roles: { S: C.skinB3, s: C.skinB2, H: C.hairBrown, h: C.black, C: C.orange2, c: C.orange1, k: C.orange3, J: C.lime2, j: C.lime1, L: C.denim2, P: C.denim1, p: C.black, B: C.rust2, G: C.gray4, A: C.white, a: C.gray5 },
    down: { 4: [[12, 'OOOOOOOO']], 5: [[11, 'OkCCCCCCCO']], 6: [[10, 'OkCCCCCCCCcO']], 7: [[10, 'OCCCCCCCCCcO']], 8: [[9, 'OOccccccccccOO']], 9: [[8, 'OH'], [22, 'HO']], 10: [[8, 'OH'], [22, 'HO']], 11: [[8, 'OH'], [22, 'HO']], 12: [[9, 'O'], [22, 'O']], 18: [[9, 'LL'], [21, 'LL']], 19: [[9, 'LL'], [12, 'AAAAAAAA'], [21, 'LL']], 20: [[9, 'LL'], [21, 'LL']], 21: [[9, 'LL'], [12, 'AAAAAAAA'], [21, 'LL']] },
    up: { 4: [[12, 'OOOOOOOO']], 5: [[11, 'OkCCCCCCCO']], 6: [[10, 'OkCCCCCCCCcO']], 7: [[10, 'OCCCCCCCCCcO']], 8: [[9, 'OOccccccccccOO']], 9: [[8, 'OH'], [22, 'HO']], 10: [[8, 'OH'], [22, 'HO']], 11: [[8, 'OH'], [22, 'HO']], 12: [[9, 'O'], [22, 'O']], 18: [[9, 'LL'], [21, 'LL']], 19: [[9, 'LL'], [12, 'AAAAAAAA'], [21, 'LL']], 20: [[9, 'LL'], [14, 'AAAA'], [21, 'LL']], 21: [[9, 'LL'], [12, 'AAAAAAAA'], [21, 'LL']] },
    left: { 4: [[12, 'OOOOOOO']], 5: [[11, 'OkCCCCCO']], 6: [[10, 'OkCCCCCCcO']], 7: [[10, 'OCCCCCCCcO']], 8: [[7, 'OOOcccccccccO']], 9: [[20, 'HO']], 10: [[20, 'HO']], 11: [[20, 'HHO']], 19: [[11, 'AAAAAAAAAA']], 21: [[11, 'AAAAAAAAAA']] },
  },
  mecanica: {
    roles: { S: C.skinD2, s: C.skinD1, H: C.hairAuburn, h: C.rust1, C: C.orange2, c: C.orange1, k: C.orange3, J: C.gray3, j: C.gray2, L: C.gray4, P: C.gray2, p: C.gray1, B: C.black, G: C.rust1, A: C.cyan2, a: C.rust2 },
    down: { 6: [[12, 'OOOOOOOO']], 7: [[11, 'OCCCCCCCCO']], 8: [[10, 'OkCCCCCCCCcO']], 9: [[10, 'OMAAMOOMAAMO']], 10: [[22, 'OMOMO']], 11: [[22, 'OMMMO']], 12: [[23, 'OMO']], 13: [[23, 'OMO']], 14: [[23, 'OMO']], 15: [[23, 'OMO']], 16: [[23, 'OmO']], 18: [[7, 'OJ'], [23, 'JO']], 19: [[7, 'OJ'], [12, 'aaaaaaaa'], [23, 'JO']], 20: [[7, 'OJ'], [12, 'aaXaaaaa'], [23, 'JO']], 21: [[7, 'Oj'], [12, 'aaaaaaaa'], [23, 'jO']], 22: [[7, 'OG'], [12, 'aaaaaaaa'], [23, 'GO']] },
    up: { 6: [[12, 'OOOOOOOO']], 7: [[11, 'OCCCCCCCCO']], 8: [[10, 'OkCCCCCCCCcO']], 9: [[10, 'OCCCCCCCCCcO']], 10: [[14, 'OCCO']], 11: [[13, 'OC'], [17, 'CO']], 14: [[20, 'OMO']], 15: [[19, 'OMO']], 16: [[18, 'OMO']], 17: [[17, 'OMO']], 18: [[7, 'OJ'], [16, 'OMO'], [23, 'JO']], 19: [[7, 'OJ'], [15, 'OMO'], [23, 'JO']], 20: [[7, 'OJ'], [14, 'OMMMO'], [23, 'JO']], 21: [[7, 'Oj'], [14, 'OMOMO'], [23, 'jO']], 22: [[7, 'OG'], [23, 'GO']] },
    left: { 6: [[12, 'OOOOOOO']], 7: [[11, 'OCCCCCCO']], 8: [[10, 'OkCCCCCCcO']], 9: [[9, 'OMAMOCCCCcO']], 10: [[21, 'OCO']], 11: [[21, 'OCO']], 12: [[21, 'OMO']], 13: [[21, 'OMO']], 14: [[21, 'OMO']], 15: [[20, 'OMMMO']], 19: [[12, 'aaaaa']], 20: [[12, 'aaXaa']], 21: [[12, 'aaaaa']], 22: [[12, 'aaaaa']] },
  },
  mecatronica: {
    roles: { S: C.skinC2, s: C.skinC1, H: C.hairTeal, h: C.teal1, C: C.gray2, c: C.gray1, k: C.gray3, J: C.teal2, j: C.teal1, L: C.petrol3, P: C.gray2, p: C.gray1, B: C.black, G: C.gray3, A: C.cyan2, a: C.cyan1 },
    down: { 1: [[19, 'OOO']], 2: [[19, 'ORO']], 3: [[19, 'OmO']], 4: [[3, 'O.O.O'], [19, 'OmO']], 5: [[2, 'OmmmmmO'], [19, 'OmO']], 6: [[2, 'OmAAAmO'], [19, 'OmO']], 7: [[3, 'OOOOO'], [12, 'mmmmmmmm']], 11: [[8, 'OmM'], [21, 'MmO']], 12: [[8, 'OmM'], [11, 'AAAAAAAAAA'], [21, 'MmO']], 13: [[8, 'OmM'], [21, 'MmO']], 18: [[20, 'OMMO']], 19: [[20, 'OMmO']], 20: [[20, 'OMmO']], 21: [[20, 'OMmO']], 22: [[20, 'OmmmO']], 23: [[20, 'OMOMO']], 24: [[20, 'OO.OO']] },
    up: { 1: [[19, 'OOO']], 2: [[19, 'ORO']], 3: [[19, 'OmO']], 4: [[3, 'O.O.O'], [19, 'OmO']], 5: [[2, 'OmmmmmO'], [19, 'OmO']], 6: [[2, 'OmAAAmO'], [19, 'OmO']], 7: [[3, 'OOOOO'], [12, 'mmmmmmmm']], 11: [[8, 'OmM'], [21, 'MmO']], 12: [[8, 'OmM'], [21, 'MmO']], 13: [[8, 'OmM'], [21, 'MmO']], 18: [[8, 'OMMO']], 19: [[8, 'OmMO']], 20: [[8, 'OmMO']], 21: [[8, 'OmMO']], 22: [[7, 'OmmmO']], 23: [[7, 'OMOMO']], 24: [[7, 'OO.OO']] },
    left: { 1: [[15, 'OOO']], 2: [[15, 'ORO']], 3: [[15, 'OmO']], 4: [[15, 'OmO'], [24, 'O.O.O']], 5: [[15, 'OmO'], [23, 'OmmmmmO']], 6: [[15, 'OmO'], [23, 'OmAAAmO']], 7: [[12, 'mmmmmmm'], [24, 'OOOOO']], 11: [[16, 'OMMO']], 12: [[10, 'OAAAA'], [16, 'OmMO']], 13: [[16, 'OMMO']], 19: [[12, 'MMm']], 20: [[12, 'MMm']], 21: [[12, 'MMm']], 22: [[11, 'OMOMO']], 23: [[11, 'OO.OO']] },
  },
  computacao: {
    roles: { S: C.skinC3, s: C.skinC2, H: C.hairBlack, h: C.black, C: C.purple2, c: C.purple1, k: C.purple3, J: C.purple2, j: C.purple1, L: C.purple3, P: C.denim1, p: C.black, B: C.gray4, G: C.skinC3, A: C.green2, a: C.green1 },
    down: { 5: [[12, 'OOOOOOOO']], 6: [[11, 'OJJJJJJJJO']], 7: [[10, 'OJJJJJJJJJJO']], 8: [[9, 'OJJjHHHHHHjJJO']], 9: [[9, 'OJjHHHHHHHHjJO']], 10: [[9, 'OJj'], [20, 'jJO']], 11: [[9, 'OJj'], [20, 'jJO']], 12: [[9, 'OJj'], [11, 'SMAMSSMAMS'], [20, 'jJO']], 13: [[9, 'OJj'], [20, 'jJO']], 14: [[9, 'OJj'], [20, 'jJO']], 15: [[9, 'OJJ'], [20, 'JJO']], 16: [[10, 'OJJ'], [19, 'JJO']], 17: [[14, 'W'], [17, 'W']], 20: [[12, 'OOOOOOOO']], 21: [[12, 'OAAaAAaO']], 22: [[12, 'OAaAAAAO']], 23: [[12, 'OOOOOOOO']] },
    up: { 5: [[12, 'OOOOOOOO']], 6: [[11, 'OJJJJJJJJO']], 7: [[10, 'OJJJJJJJJJJO']], 8: [[9, 'OJJJJJJJJJJJJO']], 9: [[9, 'OJJJJJJJJJJJJO']], 10: [[9, 'OJJJJJJJJJJJJO']], 11: [[9, 'OJJJJJJJJJJJJO']], 12: [[9, 'OjJJJJJJJJJJjO']], 13: [[9, 'OjjJJJJJJJJjjO']], 14: [[9, 'OJjjjjjjjjjjJO']], 15: [[9, 'OJJ'], [20, 'JJO']], 16: [[10, 'OJJ'], [19, 'JJO']] },
    left: { 5: [[12, 'OOOOOOO']], 6: [[11, 'OJJJJJJJO']], 7: [[10, 'OJJJJJJJJJO']], 8: [[9, 'OjHHHJJJJJJO']], 9: [[9, 'OjHHHJJJJJJO']], 10: [[9, 'Oj'], [16, 'JJJJJO']], 11: [[9, 'Oj'], [16, 'JJJJJO']], 12: [[10, 'MA'], [16, 'jJJJJO']], 13: [[16, 'jjJJJO']], 14: [[16, 'jjJJO']], 15: [[17, 'jJO']], 19: [[8, 'OOOO']], 20: [[8, 'OAaO']], 21: [[8, 'OAAO']], 22: [[8, 'OOOO']] },
  },
  biomedica: {
    roles: { S: C.skinA3, s: C.skinA2, H: C.hairBlack, h: C.black, C: C.hairBlack, c: C.black, k: C.gray2, J: C.offwhite, j: C.gray5, L: C.white, P: C.sky1, p: C.denim2, B: C.gray5, G: C.sky2, A: C.sky2, a: C.sky1 },
    down: { 3: [[14, 'OOOO']], 4: [[13, 'OHHHHO']], 5: [[13, 'OHhhHO']], 6: [[12, 'OOHHHHOO']], 16: [[12, 'OAAAAAAO']], 17: [[13, 'AAAA']], 18: [[11, 'm'], [20, 'm']], 19: [[9, 'RR'], [12, 'm'], [19, 'm']], 20: [[13, 'mmmmM'], [9, 'RR']], 21: [[16, 'M']], 23: [[9, 'OJJjjjjjjjjJJO']], 24: [[9, 'OJJJJJJJJJJJJO']], 25: [[9, 'OJJJJJOOJJJJJO']], 26: [[9, 'OjjjjO..OjjjjO']], 27: [[9, 'OOOOO....OOOOO']] },
    up: { 3: [[14, 'OOOO']], 4: [[13, 'OHHHHO']], 5: [[13, 'OHhhHO']], 6: [[12, 'OOHHHHOO']], 23: [[9, 'OJJjjjjjjjjJJO']], 24: [[9, 'OJJJJJJJJJJJJO']], 25: [[9, 'OJJJJJJJJJJJJO']], 26: [[9, 'OjjjjjjjjjjjjO']], 27: [[9, 'OOOOOOOOOOOOOO']] },
    left: { 3: [[15, 'OOOO']], 4: [[14, 'OHHHHO']], 5: [[14, 'OHhhHO']], 6: [[13, 'OOHHHHOO']], 15: [[11, 'OAAAA']], 19: [[13, 'R']], 23: [[10, 'OJJjjjjjjjJO']], 24: [[10, 'OJJJJJJJJJJJO']], 25: [[11, 'OJJJJJJJJJJO']], 26: [[12, 'OjjjjjjjjjO']], 27: [[13, 'OOOOOOOOO']] },
  },
  quimica: {
    roles: { S: C.skinD3, s: C.skinD2, H: C.hairAuburn, h: C.rust1, C: C.hairAuburn, c: C.rust1, k: C.rust3, J: C.white, j: C.offwhite, L: C.white, P: C.rust1, p: C.black, B: C.black, G: C.yellow2, A: C.toxic, a: C.green1 },
    down: { 8: [[9, 'O'], [22, 'O']], 9: [[8, 'OH'], [22, 'HO']], 10: [[8, 'OH'], [22, 'HO']], 11: [[8, 'OH'], [10, 'OMMMMOOMMMMO'], [22, 'HO']], 12: [[10, 'OMAAMOOMAAMO']], 13: [[10, 'OMMMMSSMMMMO']], 19: [[23, 'OMO']], 20: [[13, 'A'], [22, 'OAAAO']], 21: [[17, 'AA'], [22, 'OAAAO']], 22: [[22, 'OOOOO']], 23: [[9, 'OJJjjjjjjjjJJO']], 24: [[9, 'OJJJJJAJJJJJJO']], 25: [[9, 'OjjjjOOOOjjjjO']], 26: [[9, 'OOOOO....OOOOO']] },
    up: { 8: [[9, 'O'], [22, 'O']], 9: [[8, 'OH'], [22, 'HO']], 10: [[8, 'OH'], [22, 'HO']], 11: [[8, 'OH'], [10, 'OMMMMMMMMMMO'], [22, 'HO']], 20: [[15, 'AA']], 23: [[9, 'OJJjjjjjjjjJJO']], 24: [[9, 'OJJJJJJJJJJJJO']], 25: [[9, 'OjjjjjjjjjjjjO']], 26: [[9, 'OOOOOOOOOOOOOO']] },
    left: { 8: [[20, 'O']], 9: [[20, 'HO']], 10: [[20, 'HHO']], 11: [[9, 'OMMMMO'], [20, 'HHO']], 12: [[9, 'OAAAAM'], [20, 'HO']], 13: [[9, 'OMMMMO']], 19: [[7, 'OMO']], 20: [[6, 'OAAAO']], 21: [[6, 'OAAAO'], [14, 'A']], 22: [[6, 'OOOOO']], 23: [[10, 'OJJjjjjjjjJO']], 24: [[10, 'OJJJJJJJJJJO']], 25: [[11, 'OjjjjjjjjjO']], 26: [[12, 'OOOOOOOOO']] },
  },
};

// ---------- Composição ----------

function layer(rowsObj, roles) {
  const p = new Px(32, 32);
  for (const [y, segs] of Object.entries(rowsObj)) {
    if (!segs || !Number.isInteger(Number(y))) continue;
    for (const [x, s] of segs) {
      for (let i = 0; i < s.length; i++) {
        const ch = s[i];
        if (ch === '.') continue;
        const col = roles[ch];
        if (col === undefined) throw new Error(`símbolo sem cor "${ch}" (linha ${y})`);
        p.set(x + i, Number(y), col);
      }
    }
  }
  return p;
}

function rolesOf(spec) { return { ...BASE_ROLES, ...SPEC[spec].roles }; }

/** Parte de cima (corpo + camadas da engenharia) para uma vista. */
function upper(spec, view) {
  const roles = rolesOf(spec);
  const base = view === 'down' ? FRONT : view === 'up' ? BACK : SIDE;
  const key = view === 'right' ? 'left' : view;
  return layer(base, roles).blit(layer(SPEC[spec][key] || {}, roles));
}

function legs(spec, kind) { return layer(LEGS[kind], rolesOf(spec)); }

function compose(spec, view, legKind, bob = 0, flipLegs = false) {
  const out = new Px(32, 32);
  let l = legs(spec, legKind);
  if (flipLegs) l = l.flipX();
  out.blit(l);
  out.blit(upper(spec, view).shift(0, bob));
  return out;
}

function standing(spec, view, frame = 0) {
  const side = view === 'left';
  const kinds = side ? ['sideStand', 'sideStride', 'sideStand', 'sideStride'] : ['stand', 'stepL', 'stand', 'stepL'];
  const bob = frame % 2 === 1 ? 1 : 0;
  return compose(spec, view, kinds[frame], bob, !side && frame === 3);
}

/** Troca os pixels do braço direito (frente) por um braço erguido. */
function armUp(p, spec, both = false) {
  const roles = rolesOf(spec);
  const out = p.clone();
  // braço erguido: 4 colunas; o contorno do tronco (x=11 / x=20) continua no lugar
  const arm = layer({ 12: [[0, 'OGGO']], 13: [[0, 'OGGO']], 14: [[0, 'OJJO']], 15: [[0, 'OJJO']], 16: [[0, 'OJJO']], 17: [[0, 'OjJO']], 18: [[0, 'OjJO']] }, roles);
  const raise = (eraseFrom, armX) => {
    for (let y = 19; y <= 23; y++) for (let x = eraseFrom; x < eraseFrom + 3; x++) out.set(x, y, null);
    out.blit(arm.shift(armX, 0));
  };
  raise(21, 21);
  if (both) raise(8, 7);
  return out;
}

function withRing(p, color, parity = -1) {
  const out = p.clone();
  for (const [x, y] of p.ring()) if (parity < 0 || (x + y) % 2 === parity) out.set(x, y, color);
  return out;
}

function stamp(p, rowsObj, roles, dx = 0, dy = 0) {
  return p.clone().blit(layer(rowsObj, roles).shift(dx, dy));
}

function framesFor(spec) {
  const roles = rolesOf(spec);
  const accent = roles.A;
  const f = {};
  for (const view of ['down', 'up', 'left']) {
    f[`idle_${view}`] = [standing(spec, view, 0), compose(spec, view, view === 'left' ? 'sideStand' : 'stand', 1)];
    f[`walk_${view}`] = [0, 1, 2, 3].map((i) => standing(spec, view, i));
  }
  f.idle_right = f.idle_left.map((p) => p.flipX());
  f.walk_right = f.walk_left.map((p) => p.flipX());

  const front = f.idle_down[0];
  const raised = armUp(front, spec);
  const flash = { 8: [[24, '.Y.']], 9: [[23, 'YAY']], 10: [[24, '.Y.']] };
  f.attack_down = [raised, stamp(raised, flash, { ...roles, Y: C.yellow3 }), front];
  f.special_down = [withRing(front, accent, 0), withRing(front, accent, 1), withRing(withRing(front, accent), C.yellow3, 0)];
  f.hurt_down = [front.map((v) => (v === C.outline ? v : C.white)), front.shift(-1, 0).map((v) => (v === C.black ? C.outline : v))];
  const stars = (x) => ({ 2: [[x, '.Y.']], 3: [[x, 'YkY']], 4: [[x, '.Y.']] });
  f.stun_down = [stamp(front, stars(9), { ...roles, k: C.yellow2 }), stamp(front, stars(20), { ...roles, k: C.yellow2 })];

  const kneel = new Px(32, 32).blit(legs(spec, 'kneel')).blit(upper(spec, 'down').shift(0, 4));
  const lying = (() => {
    const r = front.rotate90cw();
    return new Px(32, 32).blit(r, 0, 31 - r.bottomRow()).shift(0, -3);
  })();
  const zz = { 6: [[24, 'YYY']], 7: [[25, 'Y']], 8: [[24, 'YYY']], 3: [[27, 'YY']], 4: [[28, 'Y']], 5: [[27, 'YY']] };
  f.downed_down = [kneel, lying, stamp(lying, zz, roles)];
  const heal = (big) => (big ? { 15: [[25, '.G.']], 16: [[25, 'GGG']], 17: [[25, '.G.']] } : { 16: [[26, 'G']] });
  f.revive_down = [stamp(kneel, heal(false), { ...roles, G: C.green2 }), stamp(kneel, heal(true), { ...roles, G: C.green2 })];
  const cheer = armUp(front, spec, true);
  f.victory_down = [cheer, cheer.shift(0, -2)];
  return f;
}

/** Lista de sprites: { id, category, w, h, pivot, px }. */
export function characterSprites() {
  const out = [];
  for (const spec of SPECS) {
    const frames = framesFor(spec);
    for (const [anim, { dirs, frames: n }] of Object.entries(CHAR_ANIMS))
      for (const d of dirs) {
        const list = frames[`${anim}_${d}`];
        if (!list || list.length !== n) throw new Error(`animação incompleta: ${spec} ${anim} ${d}`);
        list.forEach((px, i) => out.push({ id: `char_${spec}_${anim}_${d}_${i}`, category: 'Characters', sub: spec, w: 32, h: 32, pivot: [16, 1], px }));
      }
  }
  return out;
}

export function specRoles(spec) { return rolesOf(spec); }
