# Engineering Survivors

Survivor-like 2D de cima, em pixel art 32×32, sobre engenheiros exaustos segurando o turno da madrugada num laboratório
tomado por máquinas. Roda no navegador (Unity WebGL), solo ou cooperativo online de 1 a 4 jogadores.

**Jogar:** https://engineering-survivors.pages.dev (Chrome, Edge ou Firefox de computador)

**Estado:** SP1 (fundação jogável + prova online). Veja `docs/superpowers/specs/2026-10-24-sp1-fundacao-design.md`.

## Como jogar

- **Mover:** WASD ou setas. A mira é automática.
- **Especial:** Espaço (reatribuível em Configurações).
- **Subida de nível:** clique numa carta ou aperte 1, 2 ou 3.
- **Menu/pausa:** Esc (no solo pausa; no cooperativo a partida continua). **F3:** painel de desempenho.
- **Objetivo:** sobreviver 10 minutos. A partida é perdida quando todos caem.

Cooperativo: um jogador cria a sala e passa o código de 6 caracteres; os outros entram com o código. O anfitrião escolhe
a dificuldade (Fácil, Normal, Difícil). O tamanho da equipe também ajusta o balanceamento.

## Conteúdo do SP1

- 2 especializações jogáveis: **Elétrica** (Descarga Direcionada, especial Apagão) e **Mecânica** (Braço Giratório, especial Turbina de Emergência). Arte das 7 engenharias pronta.
- Habilidades comuns, componentes, atributos, Kit de Primeiros Socorros, uma sinergia (Carga Acumulada) e duas evoluções (uma descobrível).
- 3 inimigos (enxame, pesado, atirador) com elites, surtos a cada minuto, arena Laboratório de Pesquisa.

## Estrutura

| Pasta | O quê |
|---|---|
| `Assets/_Project/Sim` | simulação autoritativa em C# puro (sem UnityEngine), 30 Hz, também compilada fora da Unity |
| `Assets/_Project/Net` | rede: NGO + Unity Relay (WebSocket seguro), sessões, NetGame |
| `Assets/_Project/Game` | apresentação: arena, personagens, inimigos, efeitos, câmera pixel perfect, áudio, entrada |
| `Assets/_Project/UI` | interface UI Toolkit, textos pt-BR, ponto de entrada (GameBootstrap) |
| `Assets/_Project/Content` | conteúdo canônico (`sp1-content.json`) e ScriptableObjects |
| `ArtSource`, `AudioSource` | geradores de pixel art e de áudio (Node) |
| `Tools` | testes da Sim (dotnet), bots de balanceamento, builds, provas no navegador, publicação |

Como abrir, testar, gerar a build e publicar: `docs/COMO-RODAR.md`. Testes realizados: `docs/TESTES.md`.
Limitações: `docs/LIMITACOES.md`. Créditos e licenças: `CREDITS.md`.
