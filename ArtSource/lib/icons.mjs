// Ícones das habilidades 64×64: o desenho de cada item, as molduras por tipo e o guia de estilo estão em itemart.mjs.
import { itemIcon } from './itemart.mjs';

export function iconSprites(content) {
  const evolved = new Set(content.evolutions.map((e) => e.resultAbility));
  const out = [];
  const add = (id, kind, rarity, tags) => out.push({ id: `icon_${id}`, category: 'Abilities', w: 64, h: 64, pivot: [32, 32], px: itemIcon(id, kind, rarity, tags, evolved.has(id)) });
  for (const a of content.abilities) add(a.id, a.kind, a.rarity, a.tags);
  for (const s of content.specials) add(s.id, 'special', 'raro', ['Energia']);
  return out;
}
