# Registro de testes — Engineering Survivors

Este arquivo registra testes reais: o que foi executado, quando, com qual versão e o resultado.
Nada aqui é estimativa. Valores de balanceamento são **não afinados** até o SP4.

---

## 2026-10-24 — Simulação (dotnet, fora da Unity)

- Comando: `Tools/test-sim.sh` (.NET SDK 10.0.402).
- Resultado: **155/155 testes passando**, incluindo:
  - fumaça com bots nas 12 combinações × 2 especializações × 3 sementes (2 minutos cada), sem exceções e com os tetos respeitados;
  - zero alocação por tick no caminho quente (200 inimigos, 300 ticks);
  - determinismo (mesma semente + mesmas entradas = mesmo estado após 900 ticks).

## 2026-10-24 — Linha de base de balanceamento com bots (10 min, 3 sementes)

Comando: `dotnet run --project Tools/BalanceRunner -c Release -- --minutes 10 --seeds 3 --out <csv>`

O bot foge do perigo, coleta gemas, usa o especial quando cercado e escolhe cartas por heurística.
Ele desvia melhor que um humano: os números abaixo **subestimam** a dificuldade real.

### Valores iniciais da spec (orçamento 1,2 + 0,55·t)

Vitória em **72/72**. Dano sofrido médio em 10 minutos: Normal solo 10, Difícil 4 jogadores 158.
Conclusão: fácil demais — o surgimento não pressiona.

### Ajuste 1 (orçamento 2,0 + 1,1·t, surto de 30) — valores atuais

| Célula | Vitórias | Sobrev. média (s) | Nível médio | Abates | Dano sofrido | Pico de inimigos | Tick mais lento (ms) |
|---|---|---|---|---|---|---|---|
| Fácil, 1 | 6/6 | 600 | 22,7 | 3194 | 6 | 174 | 5,13 |
| Fácil, 2 | 6/6 | 600 | 24,8 | 4802 | 18 | 114 | 0,15 |
| Fácil, 3 | 6/6 | 600 | 25,7 | 6059 | 34 | 128 | 0,40 |
| Fácil, 4 | 6/6 | 600 | 26,5 | 6957 | 47 | 134 | 1,71 |
| Normal, 1 | 6/6 | 600 | 23,8 | 4492 | 55 | 224 | 0,23 |
| Normal, 2 | 6/6 | 600 | 25,5 | 6547 | 73 | 400 | 0,42 |
| Normal, 3 | 6/6 | 600 | 26,7 | 8138 | 160 | 315 | 0,26 |
| Normal, 4 | 6/6 | 600 | 28,5 | 9307 | 196 | 236 | 0,16 |
| Difícil, 1 | 4/6 | 498 | 23,7 | 5004 | 287 | 218 | 0,19 |
| Difícil, 2 | 3/6 | 537 | 24,4 | 7280 | 554 | 377 | 3,48 |
| Difícil, 3 | 3/6 | 521 | 26,2 | 8618 | 688 | 202 | 0,18 |
| Difícil, 4 | 5/6 | 577 | 29,3 | 11262 | 721 | 400 | 0,37 |

Leitura: há gradiente entre as dificuldades e o teto de 400 inimigos é atingido nas células mais intensas.
O tempo de tick (desktop, .NET) fica bem abaixo de 1 ms; a medição que vale é no navegador (pendente).

Pendências desta frente (SP4): partidas humanas, afinação das 12 células, comparação entre especializações.

## 2026-10-24 — Prova de rede WebGL (Task 20) — **APROVADA**

Comando: `node Tools/netproof/proof.mjs Build/WebGL-proof 8081` (com `PLAYWRIGHT_BROWSERS_PATH` apontando para os navegadores do Playwright).
Duas páginas Chromium sem interface, em contextos separados (identidades anônimas diferentes), na build WebGL da cena de prova
(Unity 6000.3.27f1, NGO 2.13.3, Multiplayer Services 2.4.1, Unity Transport 2.7.4, Brotli, 12 MB). O host cria a sala
(`?mode=host&autostart=2`) pelo Relay `wss`, o cliente entra pelo código, o cliente anda para a direita e o teste compara o estado das duas.
Relatório em `Build/netproof/report.json`, registros e capturas na mesma pasta.

| Execução | Sala | Resultado |
|---|---|---|
| 1ª (ZeroTier desconectado) | 9Q78NM | Sala criada, cliente entrou, partida com 2 jogadores; posições, vida e nível iguais nos dois lados. **Falhou** na contagem de inimigos: 41 no cliente × 30 no host. |
| 2ª (após a correção abaixo) | FB9P9C | **8/8 verificações aprovadas.** |

Estado no tick 548 (2ª execução), igual nos dois lados:

| | Host | Cliente |
|---|---|---|
| Tick | 548 | 548 |
| Inimigos | 35 | 35 |
| Jogador 0 (host) | (33,5; 32,0), vida 55, nível 2 | (33,5; 32,0), vida 55, nível 2 |
| Jogador 1 (cliente) | (48,2; 32,0), vida 5, nível 2 | (48,2; 32,0), vida 5, nível 2 |

Verificações: sala criada; partida iniciada com 2; cliente recebe ticks (> 200); os dois veem inimigos; contagens de inimigos
iguais (tolerância 15%); o movimento do cliente aparece no host (> 3 u); posição do cliente igual nos dois lados (< 2,5 u); nenhum erro de página.

**Bug encontrado pela 1ª execução e corrigido:** um snapshot que chega depois do `EnemyDied` (o snapshot vai por canal não confiável,
o evento por canal confiável) recriava o inimigo morto no cliente, que nunca mais era removido ("inimigos fantasmas").
Correção: o `EnemyDied` passou a levar o tick da morte e o cliente ignora registros desse inimigo com tick anterior
(e por 300 ticks, antes de o ID poder ser reutilizado). Teste: `StaleSnapshotAfterDeath_DoesNotResurrectEnemy` (dotnet, 174/174).

Observações:
- O console das duas páginas mostra `ERROR: Shader` (texto truncado pelo Unity) ao carregar. Ocorre no Chromium sem GPU (SwiftShader);
  não impediu o jogo. A verificar no navegador com GPU, na build do jogo (Task 25).
- Antes, a prova ficou bloqueada porque a rota IPv4 padrão da máquina estava no ZeroTier One (a autenticação anônima da Unity dava *timeout*);
  resolvido pelo autor desconectando o ZeroTier.
- O painel de navegador do app suspende abas ocultas e não alcança serviços externos; por isso a prova usa Chromium sem interface.
- Pendente: o teste em 2 computadores pelo autor (spec §12.4), com a build publicada.

## 2026-10-24 — Jogo na Unity (Tasks 22–24)

| Conjunto | Comando | Resultado |
|---|---|---|
| Simulação (dotnet) | `bash Tools/test-sim.sh` | 183/183 |
| Pixel art (node) | `npm --prefix ArtSource test` | 13/13 |
| Áudio (node) | `npm --prefix AudioSource test` | 4/4 |
| Unity EditMode | `Tools/unity-tests.ps1 -Platform EditMode` | 28/28 |
| Unity PlayMode | `Tools/unity-tests.ps1 -Platform PlayMode` | 10/10 |

PlayMode monta o jogo inteiro (GameBootstrap + interface) e clica nos botões reais: menu → solo → HUD; o solo não abre
transporte de rede; subida de nível pausa o solo; cartas mostram nome, raridade e tipo; a escolha atualiza o HUD; a pausa
congela o solo e "Sair da partida" volta ao menu; o fim da partida mostra o resultado; a tecla do especial reatribuída
persiste; 396 inimigos no editor em batchmode (sem renderização: ~955 FPS, só a lógica — a medição oficial é no navegador).

Verificação por mutação (os testes de Unity foram escritos junto com o código): três defeitos introduzidos de propósito
— 5ª voz do mesmo som permitida, botão do menu sem função, evolução descobrível mostrando o nome — fizeram falhar
exatamente `VoiceLimiter_FourPerSound_ThenFreesWhenOneEnds`, `AllUxmlButtons_HaveHandlers` e
`LevelUpCard_UndiscoveredEvolution_ShowsQuestionMarks`; o código foi restaurado e a suíte voltou a 28/28.

## Roteiro do teste em 2 computadores (autor) — **pendente: autor**

Pré-requisito: a build publicada (URL em `README.md` depois da publicação). Dois computadores em redes quaisquer
(podem ser casas diferentes), Chrome, Edge ou Firefox atualizados.

1. **Computador A:** abrir a URL → *Cooperativo Online* → digitar um nome → *Criar sala*. Anotar o código de 6 caracteres
   (o botão *Copiar* copia).
2. **Computador B:** abrir a URL → *Cooperativo Online* → nome → digitar o código (maiúsculas/minúsculas e espaços tanto faz) → *Entrar com código*.
3. Nos dois: a sala mostra "Jogadores 2/4" e os dois nomes. Cada um escolhe uma especialização diferente; A escolhe a dificuldade (B só vê).
4. A clica *Iniciar*. Conferir nos dois: os dois personagens andam (WASD/setas) e o outro aparece andando; inimigos iguais nas duas telas.
5. Subir de nível: as cartas aparecem com contagem de 12 s, a partida **não** pausa; escolher com 1/2/3 ou mouse.
6. Especial (Espaço) nos dois: Apagão (onda que atordoa) e Turbina (velocidade e atropelamento).
7. F3 nos dois: anotar FPS, KB/s enviados/recebidos e latência.
8. A minimiza a aba por ~10 s: B deve ver "O anfitrião minimizou o jogo: a partida pode travar."
9. Jogar até o fim (vitória aos 10 min ou derrota) e conferir a tela de resultado nos dois (tempo, abates, nível, dano por habilidade).
10. Nova sala: B entra e **A fecha a aba** no meio da partida → B deve ver "Conexão perdida com o anfitrião." e o resultado parcial.

Relatar: o que funcionou, o que falhou (com captura de tela e o texto do console F12, se houver erro), FPS e latência anotados.

## 2026-10-24 — Build do jogo no navegador (Task 25)

Build `Tools/build-webgl.ps1 -Scene Main -Out Build/WebGL`: **sucesso, 0 erros, 709 s**. Download comprimido (Brotli): **14,05 MB**
(dados 5,19 MB, wasm 9,43 MB, framework 0,08 MB, loader 0,03 MB) — meta ≤ 25 MB atingida.

**Desempenho** (`node Tools/netproof/perf.mjs Build/WebGL 8082 --edge`): Microsoft Edge com GPU real deste computador
(ANGLE, Intel UHD Graphics, Direct3D 11), janela 1280×720, solo com `?bench=N` (N inimigos mantidos, ~12 s de amostra):

| Inimigos | FPS médio | FPS mínimo (amostras de 2 s) | Meta |
|---|---|---|---|
| 300 | 127 | 122 | 60 |
| 400 | 145 | 144 | ≥ 45 |

Memória: heap do wasm 122 MB, heap JS 37 MB (meta ≤ 512 MB). O FPS acima de 60 indica monitor/navegador sem limite de 60 Hz;
é desktop com GPU integrada, ou seja, abaixo de um "desktop intermediário". Sem GPU (Chromium com SwiftShader) o mesmo teste dá 12–13 FPS
— só serve de piso, não de medida.

**Prova de rede na build do jogo** (`ES_BROWSER=msedge node Tools/netproof/proof.mjs Build/WebGL 8081 jogo`): **8/8 aprovada**
(sala CRGNB6; tick 552/553: 31 inimigos nos dois lados; jogadores em (33,5; 32,0) e (48,7; 32,0), vida 55/50, nível 2 nos dois).

**Revisão visual** (capturas em `Build/perf/`): menu, HUD (retrato, vida, XP, tempo, habilidades, evoluções 0/2), onda do Apagão,
cartas de subida de nível com ícone, raridade e tags. Defeito encontrado e corrigido: o escurecimento radial da recarga do especial
não aparecia na build (desenho vetorial do Painter2D, nem com Arc nem ponto a ponto); trocado por uma cortina escura proporcional + segundos restantes, confirmada na captura da build final (teste `Special_ShowsCooldownOnHud`). Build final: 14,02 MB, Edge 146 FPS com 300 e com 400 inimigos, EditMode 28/28, PlayMode 11/11.

Observações:
- O Chromium do Playwright não tem o codec AAC (formato que a Unity usa para áudio na Web) e acusa `decodeAudioData`; no Edge
  não há erro. Chrome, Edge e Firefox de desktop decodificam AAC. Por isso as provas usam o Edge instalado.
- `ERROR: Shader` (texto cortado pela própria Unity) aparece no console também com GPU; o jogo desenha normalmente. Registrado em `LIMITACOES.md`.

## 2026-10-25 — Publicação (Cloudflare Pages)

- Publicado em **https://engineering-survivors.pages.dev** (`npx wrangler@4.147.0 pages deploy`, 8 arquivos + `_headers`).
- Prova de rede **na URL pública** (`ES_BROWSER=msedge node Tools/netproof/proof.mjs https://engineering-survivors.pages.dev 0 publico`):
  **8/8 aprovada** — sala LLLRNG pelo Relay; 34 inimigos nos dois lados; jogadores em (33,5; 32,0) e (48,5; 32,0); nenhum erro de página.
- Pendente: teste em 2 computadores pelo autor (roteiro acima).

## 2026-10-25 — Dez personagens

- **Simulação (dotnet):** 234/234 (`bash Tools/test-sim.sh`). Inclui 50 testes dos personagens (`Tools/SimTests/Abilities/CharacterTests.cs`):
  ataque inicial com dano real nos alvos e atribuído à arma; evolução acrescenta o ataque novo e mantém o inicial (o Cabazord Master
  substitui os três robôs); melhoria avançada só no nível 3 da evolução; prisão da bolha (nível 3), campo elétrico por acertos
  agrupados, cola que gruda e estoura, marca da Miyuki, fúria do Fufu; ricochete, quique do pneu, cadeia limitada a 12 saltos sem
  repetir alvo, filhos até profundidade 2, números de dano com no máximo metade da cota; snapshot v2 com aliados e estado de inimigo.
- **Partidas completas com bots** (4 jogadores evoluídos, difícil, 4 min, todos os personagens): sem exceções, limites respeitados,
  ~3 100 abates por partida, ~0,07 ms por tick (pior tick 16 ms, no aquecimento do JIT).
- **Balanceamento com bots** (`Tools/BalanceRunner`, normal, 10 min, 2 sementes): solo e 4 jogadores vencem com todos os personagens
  (Artur e Fufu, corpo a corpo, perderam 1 de 2 no solo; o Artur recebeu +2 de dano e +0,2 de alcance depois disso, sem nova rodada).
- **Arte:** 15/15 (`npm --prefix ArtSource test`): 10 personagens com todas as animações e direções, retratos, silhuetas distintas
  (≥ 20 px de diferença entre quaisquer dois), um sprite para cada valor do enum AllySprite. **Áudio:** 4/4, 65 efeitos.
- **Unity:** EditMode 28/28, PlayMode 11/11.
- **Combate real no navegador** (`node Tools/netproof/heroes.mjs Build/WebGL 8091 --edge`): cada personagem em partida solo com a arma
  evoluída no nível avançado (`?evolve=3`) e 150 inimigos: **10/10 aprovados**, sem erros na página, 98–146 FPS (Intel UHD). Uma rodada
  mostrou 2 FPS para a Miyuki com a janela do Edge em segundo plano; isolada, duas vezes, 144 FPS. Capturas em `Build/heroes/`.
- **Prova de rede com dois navegadores** (`ES_BROWSER=msedge node Tools/netproof/proof.mjs Build/WebGL 8085 herois`): **9/9 aprovada**
  (sala N867HJ), anfitrião Giulia evoluída nível 3, cliente Badotti; nova verificação: o cliente recebe tiros, zonas e companheiros
  (Cabazord Master e tempestade visíveis na captura do cliente).
- **Ainda não testado:** os personagens novos em 2 computadores reais (roteiro acima) e partidas com 3–4 pessoas.
- **Publicação (2026-10-25):** versão dos dez personagens em https://engineering-survivors.pages.dev; prova de rede na URL pública
  com Edge (Giulia evoluída × Badotti): **9/9 aprovada**.
- **Publicação (2026-10-25, 2ª):** nova interface (ficha do personagem), cenário em alas e personagens redesenhados (motor
  de materiais, retratos com 9 emoções) em https://engineering-survivors.pages.dev; prova de rede na URL pública (sala 8DC9K9):
  **9/9 aprovada**. Testes antes da publicação: simulação 234/234, arte 15/15, Unity EditMode 28/28 e PlayMode 11/11.
