# Engineering Survivors

Survivor-like 2D de cima, em pixel art 32×32, sobre engenheiros exaustos segurando o turno da madrugada num laboratório
tomado por máquinas. Roda no navegador (Unity WebGL), solo ou cooperativo online de 1 a 4 jogadores.

**Jogar:** https://engineering-survivors.pages.dev (Chrome, Edge ou Firefox de computador)

**Estado:** SP1 (fundação jogável + prova online) e os dez personagens. Veja `docs/superpowers/specs/`.

## Como jogar

- **Mover:** WASD ou setas. A mira é automática.
- **Especial:** Espaço (reatribuível em Configurações).
- **Subida de nível:** clique numa carta ou aperte 1, 2 ou 3.
- **Menu/pausa:** Esc (no solo pausa; no cooperativo a partida continua). **F3:** painel de desempenho.
- **Objetivo:** sobreviver 10 minutos. A partida é perdida quando todos caem.

Cooperativo: um jogador cria a sala e passa o código de 6 caracteres; os outros entram com o código. O anfitrião escolhe
a dificuldade (Fácil, Normal, Difícil). O tamanho da equipe também ajusta o balanceamento.

## Personagens

Dez personagens jogáveis, desenhados em pixel art 32×32 a partir de fotos de referência. Cada um tem uma arma
exclusiva que muda de comportamento nos níveis 3 e 5, uma evolução (arma no nível 5 + um componente no nível 2)
que acrescenta um ataque novo sem tirar o inicial e uma melhoria avançada no nível 3 da evolução.

| Personagem | Arma inicial | Evolução | Avançada |
|---|---|---|---|
| Staut | Bolhas (prendem no nível 3) | Maletas de dinheiro que abrem em moedas (Cartão Corporativo) | moedas ricocheteiam |
| Miyuki | Cálculos ultrarrápidos, equação explosiva | Sushi e sashimi; dígitos marcam (Café Expresso) | esteira de pratos |
| Giulia | Resistores e transistores, campo elétrico | Fontes, osciloscópio e circuito (Bateria Portátil) | tempestade eletromagnética |
| Breno | Hambúrgueres e Doritos | Pneus de crossfit que quicam (Halter) | pneus orbitais |
| Artur | Bonesmashing (socos em cone, combo) | Pílulas e seringas (Marmita) | anel de seringas no soco carregado |
| Badotti | Três Cabazords autônomos | Cabazord Master (Kit Arduino) | salto combinado |
| Garcia | Celulares e papéis de cola | Computadores e TVs que caem (Cabo HDMI) | chuva de eletrônicos |
| Jean | Jabutis, montado no porco | Carro fazendo drift (Turbina) | volta acelerada |
| Fufu | Socos de braços fortes, fúria | Insulina: seringas e frascos (Pote de Whey) | fúria dispara seringas |
| Donasa | Fala extremamente alta | Fala amplificada e garrafas verdes (Megafone) | grito gigante |

Especial (Espaço): Giulia usa o Apagão, Badotti a Turbina de Emergência, os demais o Overclock.
Design completo: `docs/superpowers/specs/2026-10-24-personagens-design.md`.

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
