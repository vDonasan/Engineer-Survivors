# Animações e spritesheets (gerado por ArtSource — não editar à mão)

Pixel art nativo. Personagens 32×32 px, PPU 32, pivô nos pés (x=16, y=1 a partir de baixo).
Um quadro por arquivo: `Characters/<engenharia>/char_<engenharia>_<animação>_<direção>_<quadro>.png`.
Direções: `down`, `up`, `left`, `right` (direita = espelho exato da esquerda). Animações sem direção usam `down`.

| Animação | Direções | Quadros |
|---|---|---|
| idle | down, up, left, right | 2 |
| walk | down, up, left, right | 4 |
| attack | down | 3 |
| special | down | 3 |
| hurt | down | 2 |
| stun | down | 2 |
| downed | down | 3 |
| revive | down | 2 |
| victory | down | 2 |

## Registro por personagem e engenharia

- **staut**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **miyuki**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **giulia**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **breno**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **artur**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **badotti**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **garcia**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **jean**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **fufu**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **donasa**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **mecatronica**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **computacao**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **eletrica**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **civil**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **mecanica**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **biomedica**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔
- **quimica**: idle ✔ · walk ✔ · attack ✔ · special ✔ · hurt ✔ · stun ✔ · downed ✔ · revive ✔ · victory ✔

## Retratos (64×64)

Emoções: raiva, medo, desespero, choro, exaustao, alivio, determinacao, vitoria, derrota — `Portraits/portrait_<personagem ou engenharia>_<emoção>.png`.

## Armas dos personagens

Projéteis, companheiros e zonas em `Weapons/Personagens/ally_<sprite>_<quadro>.png` (nome do enum AllySprite). Cabazord Master 48×48; carro 24×24 com 8 direções.

## Jean

Montado no porco em todas as animações, menos caído/reviver (ele cai do porco).

## Inimigos

Microbô 16×16, torreta 32×32, prensa 48×48; 2 quadros cada + variante elite (contorno magenta).
