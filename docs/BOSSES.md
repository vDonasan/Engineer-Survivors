# Bosses e Modo Infinito

## Regras da partida

| Momento | O que acontece |
|---|---|
| 03:55 | Aviso "GUIROTTO está chegando" (som `boss_aviso`) e contagem no HUD |
| 04:00 | **Guirotto** entra na arena (2 s invulnerável, animação de entrada, som próprio, barra de vida) |
| 08:55 | Aviso de **boss final** |
| 09:00 | **Mago da Elétrica** entra (2,4 s invulnerável, descargas, som próprio) |
| 10:00 | Nada termina se o Mago estiver vivo: o relógio continua (fica vermelho) e aparece "Tempo estendido" |
| Morte do Mago | Animação de morte, projéteis e áreas encerrados, vitória 3 s depois, tela com tempo, abates, nível e bosses derrotados |
| Tela de vitória | **CONTINUAR NO MODO INFINITO** (só o anfitrião ou o solo decide) ou **Voltar ao menu** |
| Todos caídos | Derrota normal |

Cada boss surge **uma única vez** por partida (a agenda marca `Spawned` por boss e só a Sim, no host, cria bosses).

## Fúria (NORMAL → BRAVO)

Com **30% de vida ou menos** (fração da vida máxima), o boss entra em BRAVO **uma vez só**: pausa os ataques por
`rageSeconds` (animação de fúria, som `boss_furia`, tremor de tela), não recupera vida e passa a usar:

- **Guirotto**: mais ondas (5) e mais rápidas, mais notas (11, em dois leques), ressonância com um segundo anel,
  recarga menor, combinação Acorde → Ressonância e o ataque exclusivo **Refrão Furioso** (anel de notas + ressonância + leque de ondas).
- **Mago**: Raio Direcionado triplo e com mira mais curta, Cadeia com 5 saltos, Tempestade com mais marcas e padrão em cruz,
  Sobrecarga maior e com **segunda onda em anel**, combinações Raio → Cadeia e Orbes → Tempestade.

## Ataques

**Guirotto** — Acorde do Brejo (ondas que atravessam jogadores), Ressonância do Banjo (círculo sinalizado ao redor),
Chuva de Notas (leque com curvas e ricochete), Concerto do Brejo (espiral de notas + áreas nos jogadores), Refrão Furioso (BRAVO).

**Mago da Elétrica** — Raio Direcionado (linha mirada), Cadeia de Relâmpagos (salta entre jogadores; sem outro jogador por perto,
conduz por inimigos), Tempestade Elétrica (círculos sinalizados), Orbes de Energia (espiral; alguns perseguem por 2,2 s),
Sobrecarga Arcana (carga de 2 s com o livro erguido, área grande, recuperação de 1,6 s) e Teleporte quando encurralado.

A escolha considera distância, ataque anterior (não repete) e a fúria; o alvo é o jogador menos visado recentemente, então em
grupo o boss alterna entre todos. Em equipe a vida cresce 25% por jogador extra, a recarga cai 8% por jogador e a Tempestade
ganha marcas; a dificuldade muda dano (perfil), tempo de aviso e quantidade de projéteis (fácil −1, difícil +2).

## Modo Infinito

O anfitrião continua a **mesma partida** (personagens, itens, níveis e evoluções; quem estava caído volta com metade da vida).
O relógio segue de onde parou e não há vitória por tempo. Por minuto desde a vitória: vida dos inimigos +10% (com aceleração, teto ×10),
dano +5% (teto ×3,5), orçamento de surgimento +7% (teto ×3), elites +0,8 p.p. (teto 35%), surtos maiores e tempestades de raios
a cada 40 s com cada vez mais marcas. Bosses voltam a cada 150 s (o primeiro após 60 s), com +60% de vida por retorno, mais
projéteis, recarga menor e os padrões da fúria liberados; do terceiro retorno em diante vêm os dois juntos (dueto).

## Onde mexer

| O quê | Onde |
|---|---|
| Horários, limiar da fúria, vida por equipe, parâmetros de todos os ataques | `Tools/content/bosses.py` → `python Tools/content/bosses.py` (atualiza `sp1-content.json`; o teste informa o novo `sp1-content.hash`) e, na Unity, *Engineering Survivors/Conteúdo/Importar JSON* |
| Regras do Modo Infinito e atraso da vitória | mesmo script (`RULES`) |
| Máquina de estados, ataques, áreas, agenda e Modo Infinito | `Assets/_Project/Sim/Systems/BossSystem.cs` |
| Rede | eventos `Boss*`/`MatchContinued` em `RuleEventCodec`, bloco `RunBoss` no `SnapshotCodec` (v3), estado em `MatchFeed.Bosses/Hazards` |
| Desenho, animações, áreas no chão | `Assets/_Project/Game/Match/BossViews.cs`; projéteis em `FxSystem.HostileSprite` |
| HUD, vitória, botão do Modo Infinito | `MatchScreens.cs` (`UpdateBoss`, `ResultController`), `Hud.uxml`, `Result.uxml`, `UiRouter` |
| Sprites | `ArtSource/lib/bosses.mjs` → `npm --prefix ArtSource run build` e *Atualizar catálogo* |
| Sons | `AudioSource/bosses.py` → `npm --prefix AudioSource run build` |

### Sprites

`Assets/_Project/Art/Bosses/<boss>/boss_<boss>_<animação>_<direção>_<quadro>.png` — Guirotto 48×48, Mago 48×56, pivô nos pés,
fundo transparente, ponto filtrado. Andar e andar bravo em 8 direções (as da direita espelham as da esquerda, já que o instrumento
e o livro continuam legíveis), e de frente: `idle`, `idle_bravo`, `attack`, `attack_bravo`, `special`, `special_bravo`, `charge`,
`hurt`, `heavy`, `rage`, `death`, `vanish`. Projéteis: `Effects/fx_boss_note`, `fx_boss_wave`, `fx_boss_orb_0/1`, `fx_boss_spark`.

## Testes e depuração

- Sim: `bash Tools/test-sim.sh --filter BossTests` (horários exatos, uma vez só, invulnerabilidade na entrada, fúria a 30% sem cura e
  sem repetir, ataques do BRAVO diferentes, 10:00 sem fim, vitória após a morte do Mago, derrota, Modo Infinito preservando a
  progressão, curva com tetos, retornos e dueto, coop alternando alvos, geometria das áreas, codec e snapshot).
- Arte: `npm --prefix ArtSource test` (8 direções, todas as animações, normal ≠ bravo).
- Navegador: `node Tools/netproof/bosses.mjs Build/WebGL 8170 guirotto|mago` e, em rede, `PROOF_BOSS=1 ES_BROWSER=msedge node Tools/netproof/proof.mjs Build/WebGL`.
- Parâmetros de depuração (só com `log=1`): `t=535` adianta o relógio, `god=1` deixa os jogadores invulneráveis,
  `autopick=1` escolhe a primeira carta, `bosshp=0.1` multiplica a vida dos bosses.

## Balanceamento (bots, 14 min, 2 sementes × 10 personagens por célula)

`dotnet run --project Tools/BalanceRunner -c Release -- --minutes 14 --seeds 2 --out Tools/BalanceRunner/reports/bosses.csv`
(colunas `guirottoDiedAt`, `magoDiedAt`, `bravoCount`). Vida base: Guirotto 800, Mago 1500 (× dificuldade × equipe).

| Dificuldade | Vitórias (1 / 2 / 3 / 4 jogadores) | Guirotto morto em | Mago morto em |
|---|---|---|---|
| Fácil | 23/24 · 21/24 · 21/24 · 20/24 | ~5:00–5:56 | ~11:00–11:40 |
| Normal | 22/24 · 16/24 · 13/24 · 12/24 | ~5:06–5:57 | ~10:47–11:33 |
| Difícil | 7/24 · 7/24 · 8/24 · 6/24 | ~5:08–6:02 | ~10:32–11:51 |

Os bots desviam das áreas sinalizadas, mas não perseguem o boss como um jogador; em equipe, parte das partidas chega a 14:00
com o Mago vivo (o relógio estendido continua). O pior tick medido foi 12,6 ms (média 0,02 ms).
