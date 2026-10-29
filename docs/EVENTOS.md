# Eventos aleatórios e visual dinâmico dos personagens

## Agenda

Primeiro evento aos 75 s; depois a cada 60–120 s sorteados (×0,7 no Modo Infinito, mínimo 20 s). Sorteio por peso × raridade
(comum 1, incomum 0,75, raro 0,5, relíquia 0,3), sem repetir os 2 últimos, no máximo 3 simultâneos, respeitando `minMinute`
e `noBoss`. No Modo Infinito o peso cresce com `infiniteWeightPerMinute` (Laboratório e Motoqueiro ficam mais frequentes; depois de
5 min de Infinito o Motoqueiro vem em dupla, em cruz). Tudo é decidido no host (`EventSystem`, na Sim); os clientes recebem
`EventStarted/Progress/Ended/Bonus/Moto` por eventos confiáveis e o estado visual de cada jogador num byte do snapshot (`PlayerLook`).

| Evento | Alcance | Efeito real | Meta / recompensa |
|---|---|---|---|
| Bolha Social | um jogador | +30% de experiência por 20 s; inimigos num raio de 8 m vão atrás dele | 25 abates: +40% da barra; ao coletar 60% de uma barra a bolha estoura (dano em área) |
| Dez na Prova | todos (individual) | +55% da barra (pode completar o nível); sorte nas cartas por 30 s | — |
| Zero na Prova | um jogador | perde 25% do XP ainda não convertido (nunca o nível), ataques 15% mais lentos, inimigos próximos 25% mais rápidos por 6 s | 20 abates em 18 s: devolve o XP +20% e tira a penalidade |
| Chamado de Burro | um jogador | ataques 12% mais lentos; cada abate dá XP extra | 15 abates: tira a penalidade, +20% de dano por 8 s e explosão de símbolos |
| Laboratório Incompleto | arena | aviso de 3 s; surgimento ×1,8, dois surtos, vazamentos sinalizados (vapor/descarga) a cada 3,5 s | 40 abates por jogador em 30 s: +50% da barra e 35% de chance de melhoria extra |
| Atropelou o Motoqueiro | arena | aviso de 1,6 s com a pista; a moto (grande, 44×30) derruba inimigos (dano e empurrão) e machuca quem estiver na pista | passou raspando e a moto derrubou 3+: Reflexo de Piloto (+25% de velocidade, 6 s) |
| Ganhou uma Coxinha | um jogador (perto dele) | coxinha GIGANTE vira totem por 20 s: solta XP a cada 1,2 s; quem fica no alcance (3,5 m) ganha +15% de dano, +15% de velocidade e regeneração (sai do alcance, perde) | 15%: Coxinha Suprema (dourada, 4,5 m, pulso de dano a cada 2 s) |
| Jalapeño | um jogador | +20% de dano, ataques 20% mais rápidos e queimadura ao redor por 10 s; com a Cenoura Podre, nuvem picante (dano contínuo) | no fim, explosão de calor e 1,5 s de lentidão |

## Visual dos personagens

Camadas separadas (não redesenham o personagem), presas ao **ponto da cabeça de cada quadro**, que a arte exporta no manifesto
(`anchor`); por isso acompanham as 4 direções e todas as animações (andar, ataque, dano, caído).

- Itens (pela lista de habilidades, igual para todos no coop): Chapéu de Japonês, Camiseta ao Contrário (etiqueta e costuras), Protoboard
  (brilho e componentes a mais na evolução, trilhas piscando), Cenoura Podre na cintura, engrenagens do Motor do Astra em órbita.
  Cachorro e Mamão já eram companheiros visíveis.
- Durante a escolha de melhoria a partida congela para todos (solo e coop); no coop o prazo de 12 s corre só na pausa.
- Estados (snapshot): Tequila (rubor, balanço só no desenho, estrelas/espirais, garrafa na ativação, rastro), Jalapeño (rubor, chamas,
  pegadas de calor), Iogurte (partículas nos pés), Bolha (esfera com mensagens, corações e notificações), Burro (balão e símbolos),
  Zero (prova com zero), Reflexo de Piloto (rastro).
- Emotes sobre a cabeça e expressões do retrato do HUD: Dez (comemoração), Zero (desespero), Burro (raiva), Laboratório (relógio, medo),
  Coxinha (alívio), Jalapeño (ardência), Tequila (tontura), Camiseta (ajeitar a roupa ao levar dano), Protoboard evoluída (concentração no especial).

## Onde mexer

| O quê | Onde |
|---|---|
| Pesos, raridade, duração, aviso, parâmetros, textos | `Tools/content/eventos.py` → `python Tools/content/eventos.py`, novo `sp1-content.hash`, *Conteúdo/Importar JSON* |
| Evento novo com comportamento existente | só configuração (ex.: outro "coxinha" com outros números) |
| Comportamento novo | classe `GameEventBehavior` em `Sim/Systems/EventSystem.cs` + registro em `EventSystem.Behaviors` |
| Interface (lista, anúncio, mensagens, retrato) | `MatchScreens.cs` (`UpdateEvents`, `OnEvent*`), `Hud.uxml`, `Theme.uss`, `pt-BR.json` |
| Camadas e emotes | `Game/Match/PlayerViews.cs`; moto e coxinhas em `FxSystem` |
| Arte | `ArtSource/lib/eventart.mjs` (ícones, coxinhas, moto, emotes, acessórios, `headAnchor`) |
| Sons | `AudioSource/eventos.py` |

Testes: `bash Tools/test-sim.sh --filter EventTests` (18), `npm --prefix ArtSource test`. Navegador:
`node Tools/netproof/eventos.mjs Build/WebGL 8200 x "jalapeno,motoqueiro" "chapeu_japones,tequila"`.
Depuração (com `log=1`): `evt=` dispara eventos em sequência (um a cada 12 s), `itens=` dá itens no início.
