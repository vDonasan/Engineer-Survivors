# Engineering Survivors — SP1: Fundação jogável + prova online

- **Data:** 2026-10-24
- **Estado:** aprovada pelo autor em 2026-10-24 ("faça você mesmo tudo que precisa")
- **Escopo deste documento:** subprojeto 1 (SP1) de 6. Define também a arquitetura que todos os subprojetos seguintes herdam.
- **Documento de origem:** prompt mestre "Engineering Survivors — Prompt Mestre de Desenvolvimento" (requisitos completos do jogo).

---

## 1. Contexto e restrições

| Item | Decisão |
|---|---|
| Objetivo | Projeto pessoal, sem prazo. Qualidade e arquitetura antes de velocidade. |
| Orçamento de infraestrutura | Zero. Somente camadas gratuitas. |
| Arte e áudio | Originais, gerados por código (SVG → PNG; síntese de áudio). Substituíveis sem alterar código. |
| Engine | Unity 6.3 LTS (LTS atual, suporte até dez/2027), C#. |
| Plataforma | Unity WebGL, navegadores desktop. URL pública. |
| Rede | Netcode for GameObjects (NGO) 2.x, topologia cliente-servidor, host = navegador de um jogador, via Unity Relay com WebSocket seguro (`wss`). Salas e códigos via pacote Multiplayer Services (Sessions). |
| Solo | Mesma simulação do coop, com `SinglePlayerTransport` do NGO (host local, sem rede, sem sala). |
| Local do projeto | `Z:\Dev\EngineeringSurvivors` (SSD, fora do OneDrive; o disco C: tem só ~13,7 GB livres). Git, branch `main`. |

### 1.1 Fatos verificados que sustentam a arquitetura

1. Navegadores não abrem sockets de escuta, mas "se usar Unity Relay, um navegador ainda pode agir como host"; páginas HTTPS exigem WSS. (Unity Transport 2.5, WebSockets.)
2. Relay + NGO na web requer Unity Transport ≥ 2.0 com `UseWebSockets = true` e alocação com tipo de conexão `wss`. O pacote unificado Multiplayer Services substitui o SDK de Relay avulso, depreciado no Unity 6.
3. `SinglePlayerTransport` roda uma sessão local completa sem abrir rede; funciona apenas com `StartHost()`.
4. Há relato (issue NGO #4111, fechada sem correção em set/2026) de que sessões **Distributed Authority** criadas por build WebGL não aceitam ninguém. **Decisão: não usar Distributed Authority.** Cliente-servidor com host WebGL precisa ser comprovado na prática (tarefa de prova antecipada, §11).
5. A Unity marcou como "Won't Fix" erros de conexão após ~2 min com a aba WebGL em segundo plano. Além disso, o laço principal WebGL usa `requestAnimationFrame`, que para em abas ocultas: **a simulação do host para quando a aba do host fica oculta.**
6. Plano gratuito do Relay: média de 50 CCU/mês e 3 GiB por CCU, até 150 GiB/mês; Lobby: 10 GiB/mês por região.

---

## 2. Decomposição do jogo completo

| # | Subprojeto | Fases do prompt | Termina quando |
|---|---|---|---|
| **SP1** | **Fundação jogável + prova online** (este documento) | 1 e 3 | Build WebGL publicada; 2 pessoas jogam pela internet. |
| SP2 | Sistemas de build: 7 engenharias, ~15 habilidades, tags/afinidades completas, componentes, sinergias, evoluções condicionais e ramificadas, artefatos, códice básico | 2 | Builds híbridas e evoluções funcionando e testadas. |
| SP3 | Coop completo: estado de pronto, reviver, recompensas compartilhadas, sinergias entre jogadores, desconexão/reconexão, pausa por aba oculta | 4 | Partidas de 1, 2, 3 e 4 jogadores testadas. |
| SP4 | Dificuldade e balanceamento: os 12 perfis afinados com bots e partidas reais | 5 | Relatório das 12 combinações. |
| SP5 | Conteúdo: chefes com fases, eventos industriais, 1ª arena completa, inimigos especiais, laboratório/progressão persistente, códice, conquistas, música | 6 | Uma arena completa e expansível. |
| SP6 | Polimento, navegadores, desempenho, documentação final | 7 | Critérios de conclusão do prompt (§28). |

Cada subprojeto tem seu próprio ciclo spec → plano → implementação e termina com uma versão funcional.

---

## 3. Escopo do SP1

### 3.1 Dentro

- Projeto Unity 6.3 LTS com URP 2D, Input System, UI Toolkit, NGO, Multiplayer Services, Test Framework.
- Simulação central em C# puro (§4): movimento, colisão por grade espacial, dano, inimigos, surgimento de hordas, XP, subida de nível com 3 opções, especial com recarga, vitória e derrota.
- **2 especializações:** Engenharia Elétrica e Engenharia Mecânica (§6.1).
- **4 habilidades comuns** + atributos + opção de sobrevivência (§6.2).
- **Prova mínima do sistema de sinergias/evoluções:** 1 sinergia e 2 evoluções, uma visível e uma descobrível (§6.3). O sistema completo é do SP2; aqui só se valida o modelo de dados e a sincronização.
- **3 inimigos:** enxame, pesado, atirador (§6.4).
- **1 arena pequena:** Laboratório de Pesquisa (§6.5).
- **Balanceamento central** com os 12 perfis preenchidos com valores iniciais marcados "não testados" (§7).
- **Online:** criar sala, código de convite, entrar por código, até 4 jogadores, lobby mínimo (código, lista, especialização de cada um, dificuldade escolhida pelo host, iniciar). Sincronização real de movimento, inimigos, projéteis hostis, XP, subida de nível, escolhas, sinergias/evoluções, especiais e resultado (§8).
- Interface do SP1 (§9), arte e áudio do SP1 (§10).
- Testes (§12), build WebGL e publicação em hospedagem gratuita (§13).

### 3.2 Fora (com destino)

- Chefes, eventos industriais → SP5.
- Reviver, estado de pronto, reconexão, pausa automática por aba oculta → SP3.
- Outras 5 especializações, artefatos, códice → SP2.
- Laboratório, progressão persistente, conquistas, estatísticas → SP5.
- Afinação real dos 12 perfis → SP4.
- Celular e controle (gamepad) → não planejado até o SP6.

Regra: **nenhum botão sem função**. Itens de menu de subprojetos futuros não aparecem no SP1.

---

## 4. Arquitetura

### 4.1 Módulos (assemblies)

| Assembly | Conteúdo | Depende de |
|---|---|---|
| `ES.Sim` | Simulação em C# puro, **sem `UnityEngine`** (`noEngineReferences: true`): matemática, RNG, entidades, grade espacial, combate, comportamentos de habilidade, progressão, balanceamento, codecs de mensagens, bots. | — |
| `ES.Content` | ScriptableObjects que embrulham as definições de `ES.Sim` e referenciam arte/áudio; montagem do `GameDatabase`. | ES.Sim |
| `ES.Net` | Sessões (Multiplayer Services), autenticação anônima, seleção de transporte, ponte entre mensagens NGO e codecs da Sim, buffer de interpolação, previsão local. | ES.Sim, NGO, MPS |
| `ES.Game` | Bootstrap, `MatchRunner` (executa a Sim no host), visuais com pool, câmera, efeitos, áudio, painel de desempenho. | ES.Sim, ES.Content, ES.Net |
| `ES.UI` | Telas e HUD (UI Toolkit: UXML/USS + controladores). | ES.Game |
| `ES.Tests.EditMode` / `ES.Tests.PlayMode` | Testes dentro da Unity. | os acima |

### 4.2 Compilação dupla da Sim

Os arquivos de `Assets/_Project/Sim/**` são compilados **também** por `Tools/Sim/ES.Sim.csproj` (`netstandard2.1`, `LangVersion 9`), o mesmo nível de API e de linguagem da Unity. `Tools/SimTests` (NUnit) e `Tools/BalanceRunner` (console) referenciam esse projeto. Resultado: a simulação é desenvolvida e testada com `dotnet test`, sem abrir a Unity.

Restrições para o código da Sim: sem `record`/`init` (exigem polyfill na Unity), sem APIs fora do `netstandard2.1`, sem alocação por tick no caminho quente (arrays pré-alocados, sem LINQ no `Step`).

### 4.3 Estrutura de pastas

```
EngineeringSurvivors/
├─ Assets/
│  ├─ _Project/
│  │  ├─ Sim/          Core/ Data/ World/ Combat/ Progression/ Difficulty/ Codec/ Bots/
│  │  ├─ Content/      Definições (SO) e instâncias de conteúdo
│  │  ├─ Net/
│  │  ├─ Game/
│  │  ├─ UI/
│  │  ├─ Art/          (gerado por ArtSource/)
│  │  ├─ Audio/        (gerado por AudioSource/)
│  │  ├─ Scenes/  Prefabs/  Settings/
│  │  └─ Tests/        EditMode/ PlayMode/
│  └─ WebGLTemplates/ES/   Template com tela de carregamento e erros
├─ ArtSource/          SVGs modulares + script Node de rasterização e atlas
├─ AudioSource/        Parâmetros de síntese + script Node que gera WAV
├─ Tools/
│  ├─ Sim/ES.Sim.csproj
│  ├─ SimTests/
│  └─ BalanceRunner/   Partidas de bots → CSV
├─ Hosting/            _headers e script de publicação
└─ docs/               specs/, plans/, COMO-RODAR.md, TESTES.md, LIMITACOES.md
```

---

## 5. Simulação

### 5.1 Laço

- Tick fixo de **30 Hz** (`dt = 1/30`), executado **somente no host** (no solo, o host é local).
- `World.Step(inputs)`:
  1. aplica entradas de cada jogador (movimento, pedido de especial, escolha de nível);
  2. move jogadores (código de movimento compartilhado com a previsão do cliente);
  3. diretor de surgimento cria inimigos;
  4. IA dos inimigos (perseguir, separação, ataque);
  5. habilidades dos jogadores (comportamentos) e projéteis;
  6. resolução de dano, mortes, gemas de XP, coleta, subida de nível;
  7. checagem de vitória/derrota;
  8. emite eventos (regra e cosméticos) para a camada de rede/visual.
- RNG com semente por partida, só no host. Determinismo bit a bit **não** é requisito (o host é a única fonte de verdade), mas a mesma semente + mesmas entradas no mesmo binário deve reproduzir a partida (útil para bots e depuração).

### 5.2 Entidades e colisão

- Entidades em arrays de estruturas por tipo (jogadores, inimigos, projéteis, gemas), com IDs `ushort` reciclados.
- Colisão: círculos contra círculos via **grade espacial uniforme** (célula de 2 unidades), círculos contra retângulos estáticos da arena.
- Separação entre inimigos (empurrão leve via grade) para evitar empilhamento.
- **Atualização seletiva:** inimigos a mais de 30 unidades de todos os jogadores atualizam IA a cada 3 ticks.
- **Tetos:** 400 inimigos, 150 gemas (acima disso, gemas próximas se fundem), 200 projéteis hostis. Os tetos são dados, não constantes de código.

### 5.3 Jogador

- Atributos-base: vida 100, velocidade 4,5 u/s, raio de coleta 1,6 u, armadura 0, regeneração 0, invulnerabilidade de 0,5 s após dano.
- Mira automática (gênero survivor-like). Direção de "frente" = última direção de movimento.
- Jogador com vida 0 fica **caído** (sem reviver no SP1): não age, não é alvo, câmera passa a seguir um aliado.

---

## 6. Conteúdo do SP1

Todos os números são **valores iniciais para Normal/solo**, não testados, ajustados por dados.

### 6.1 Especializações

**Engenharia Elétrica** — tags de afinidade: Energia, Eletrônica.
- Arma inicial **Descarga Direcionada** (comportamento `chain`): acerta o inimigo mais próximo em 6 u e salta para mais 2 dentro de 3 u; dano 14, recarga 1,0 s, perda de 15% por salto. Níveis 1–5: +dano, +1 salto nos níveis 3 e 5, −recarga.
- Especial **Apagão** (Espaço): onda radial que se expande até 6 u em 0,4 s; 35 de dano (escala com Dano); atordoa inimigos com tag `Máquina` por 2 s; apaga projéteis hostis no raio. Recarga 22 s.
- Avançado de afinidade: +1 salto em armas `chain` com tag Energia no nível máximo.

**Engenharia Mecânica** — tags de afinidade: Mecânica, Térmico.
- Arma inicial **Braço Giratório** (comportamento `orbit`): 1 braço, raio 1,8 u, volta em 1,4 s, dano 12, intervalo de acerto por alvo 0,5 s, empurrão 2. Níveis: +1 braço nos níveis 3 e 5, +raio, +dano.
- Especial **Turbina de Emergência** (Espaço): 6 s de +45% de velocidade, +30% de dano físico, −30% de dano recebido, e contato com inimigos causa 15 de dano + empurrão. Recarga 18 s.
- Avançado de afinidade: +15% de empurrão e +5% de velocidade com armas de tag Mecânica no nível máximo.

**Afinidade (ambas):** inicial +4% de dano na habilidade com tag compatível ao adquiri-la; intermediário +4% adicionais ao chegar no nível 4; avançado conforme acima. Valores em dados.

### 6.2 Habilidades comuns, atributos e sobrevivência

| Habilidade | Tipo | Tags | Comportamento | Valores iniciais |
|---|---|---|---|---|
| Laser Industrial | Arma | Energia, Térmico | `projectile` | dano 20, recarga 1,6 s, perfura 3, velocidade 14 u/s, alcance 12 u |
| Campo Eletromagnético | Arma | Energia, Defesa | `aura` | raio 2,2 u, 6 de dano a cada 0,5 s, lentidão 15% |
| Bateria Portátil | Componente | Energia | passivo | −6% de recarga por nível em armas com tag Energia (máx. 5) |
| Turbina | Componente | Mecânica | passivo | +6% de velocidade de movimento por nível (máx. 5) |

- **Atributos** (máx. 5 cada): Dano +8%, Vida máxima +15, Recuperação +0,3 vida/s, Coleta +20% de raio, Velocidade de ataque −5% de recarga.
- **Sobrevivência:** Kit de Primeiros Socorros (cura 30%); oferecido quando vida < 50% ou quando o conjunto de opções se esgota.
- Máximo de 6 armas e 6 componentes por jogador.

### 6.3 Prova do sistema de sinergias e evoluções

| Nome | Tipo | Requisitos | Efeito |
|---|---|---|---|
| Carga Acumulada | Sinergia (ativa enquanto válida) | Descarga Direcionada ≥ 1 e Bateria Portátil ≥ 2 | A cada 4ª descarga: +100% de dano e +2 saltos. |
| Pulso Amplificado | Evolução visível | Descarga Direcionada nível 5 e Bateria Portátil ≥ 3 | Substitui a Descarga: 3 alvos iniciais, cada um com sua cadeia; amplificação a cada 3ª. |
| Rotor de Impacto | Evolução **descobrível** | Braço Giratório nível 5 e Turbina ≥ 3 | Substitui o Braço: dano dos braços cresce com a velocidade atual; durante a Turbina de Emergência os braços dobram. |

Regras validadas no SP1: evolução só é oferecida com todos os requisitos atendidos; o host valida a escolha; o resultado é idêntico para todos os clientes; evolução descobrível aparece como "???" na interface de progresso até ser descoberta.

### 6.4 Inimigos

| Inimigo | Papel | Vida | Vel. | Dano | Raio | XP | Custo | Comportamento |
|---|---|---|---|---|---|---|---|---|
| Microbô de Enxame | enxame | 10 | 3,0 | 5 por contato (0,8 s) | 0,3 | 1 | 1 | persegue o jogador mais próximo |
| Prensa Ambulante | pesado | 70 | 1,5 | 14 por contato | 0,7 | 6 | 5 | persegue; resiste 80% a empurrão |
| Torreta de Inspeção | atirador | 24 | 2,0 | 8 por disparo | 0,45 | 3 | 3 | mantém 5,5–7 u de distância; dispara a cada 2,8 s com aviso visual de 0,5 s; projétil 6 u/s |

- Todos têm tag `Máquina` (o Apagão afeta todos no SP1; tags `Blindado` e `Orgânico` entram no SP2/SP5).
- **Elite:** 3× vida, 1,3× tamanho, 1,25× dano, 4× XP, 25% de chance de soltar cura.

### 6.5 Arena e diretor de surgimento

- **Laboratório de Pesquisa:** 64 × 64 u, paredes nas bordas, bancadas e armários como obstáculos retangulares.
- Orçamento de surgimento por segundo: `B(t) = (2,0 + 1,1 · rampa · t_min) × orçamento × quantidade` (era 1,2 + 0,55; elevado após a linha de base com bots mostrar vitória em 72/72 com dano quase nulo — ver `docs/TESTES.md`), onde `rampa` e `orçamento` vêm do perfil de dificuldade e `quantidade` do perfil de equipe (§7). Custos: enxame 1, atirador 3, pesado 5.
- Composição por minuto: 0–2 só enxame; 2–4 enxame 75% / atirador 25%; 4–7 enxame 60% / atirador 20% / pesado 20%; 7+ enxame 50% / atirador 25% / pesado 25%.
- A cada 60 s, um **surto**: anel de enxame ao redor dos jogadores.
- Inimigos nascem num anel fora da visão da câmera, dentro da arena.
- **Vitória:** sobreviver 10 minutos (configurável). **Derrota:** todos os jogadores caídos.

### 6.6 Progressão

- Curva: `xpParaProximo(L) = round(5 + 5·(L−1) + 0,6·(L−1)²)`.
- **XP compartilhado:** cada gema coletada concede seu valor a todos os jogadores vivos e conectados.
- Subida de nível: 3 opções geradas pelo host por pesos (nova habilidade, melhoria, componente, atributo, evolução disponível, sobrevivência), sem repetição, respeitando níveis máximos e limites de espaço.
- **Solo:** o jogo pausa durante a escolha. **Coop:** não pausa; painel de 12 s e 2 s de invulnerabilidade; ao estourar o tempo, vale a primeira opção. Níveis acumulados viram uma fila de ofertas.

---

## 7. Balanceamento

`BalanceResolver.Resolve(dificuldade, tamanhoEquipe) → MatchBalance` é o **único** lugar que combina multiplicadores. Fórmula: `valor = base × perfilDificuldade × perfilEquipe × ajusteDaCélula` (ajuste por célula padrão = 1). Parâmetros marcados com "(+)" nas tabelas são somados em vez de multiplicados: `valor = base + perfilDificuldade + perfilEquipe + ajusteDaCélula` (ajuste padrão = 0). O `MatchBalance` resolvido entra no estado da partida e é enviado aos clientes no início.

### 7.1 Perfis de dificuldade (valores iniciais, não testados)

| Parâmetro | Fácil | Normal | Difícil |
|---|---|---|---|
| Dano inimigo | 0,65 | 1,0 | 1,35 |
| Vida inimiga | 0,9 | 1,0 | 1,1 |
| Orçamento de surgimento | 0,8 | 1,0 | 1,25 |
| Rampa do orçamento | 0,8 | 1,0 | 1,2 |
| Antecipação da composição | +1 min | 0 | −1,5 min |
| XP | 1,2 | 1,0 | 0,9 |
| Chance de cura por abate | 1,2% | 0,8% | 0,5% |
| Regeneração | 1,3 | 1,0 | 0,7 |
| Chance de elite (+) | 1% | 2,5% | 5% |
| Tempo de aviso de ataques | 1,3 | 1,0 | 0,8 |

O Difícil sobe a pressão por composição, frequência e dano; a vida inimiga sobe pouco, de propósito.

### 7.2 Perfis de equipe (valores iniciais, não testados)

| Parâmetro | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| Quantidade de inimigos | 1,0 | 1,5 | 1,9 | 2,2 |
| Vida inimiga (sublinear) | 1,0 | 1,3 | 1,55 | 1,75 |
| Chance de elite (+) | 0 | +1% | +2% | +3% |
| XP por gema | 1,0 | 0,72 | 0,6 | 0,55 |

O XP por gema cai porque há mais inimigos e o XP é compartilhado (1,5 × 0,72 ≈ 1,08 de ritmo relativo; 2,2 × 0,55 ≈ 1,21). O teto de 400 inimigos vale sempre.

### 7.3 Regras

- O tamanho da equipe é **fixado no início da partida**. Desconexão no SP1 não altera o balanceamento (regra documentada; ajustes por desconexão, se houver, são do SP3).
- Todos os jogadores usam a mesma dificuldade, escolhida pelo host.

---

## 8. Rede

### 8.1 Transportes

| Modo | Transporte | Observação |
|---|---|---|
| Solo | `SinglePlayerTransport` + `StartHost()` | Nenhuma sala, nenhuma rede. |
| Coop (host e clientes) | `UnityTransport` com `UseWebSockets = true`, Relay `wss` | Criado pelo Multiplayer Services (Sessions). |

### 8.2 Fluxo de sala

1. Autenticação anônima (Unity Authentication). Sem contas de jogador.
2. Host: cria sessão privada, máximo 4 jogadores, rede via Relay (`wss`). Exibe o código da sessão.
3. Cliente: entra pelo código.
4. Cliente envia `Hello(versãoDoBuild, hashDeConteúdo, nome, especialização)`. O host responde `Welcome` ou `Reject(motivo)`.
5. Host escolhe a dificuldade e inicia; a sessão é **trancada** (entrada tardia bloqueada).
6. Host envia `MatchStart(semente, MatchBalance, elenco, hashDeConteúdo)`.

O hash de conteúdo é calculado a partir da serialização canônica das definições do `GameDatabase`.

### 8.3 Mensagens

| Direção | Mensagem | Entrega | Frequência |
|---|---|---|---|
| C→S | `Input(seq, tick, movimento quantizado, contadorDeEspecial)` | não confiável | 30 Hz |
| C→S | `Choose(ofertaId, índice)` | confiável | ao escolher |
| S→C | `Snapshot` em blocos de até ~1 KB (tick, índice do bloco, total) | não confiável sequenciada | 15 Hz |
| S→C | Eventos de regra: inimigos mortos/removidos (em lote por tick), gemas criadas/coletadas (em lote), `LevelUpOffer`, `ChoiceApplied`, `SynergyChanged`, `EvolutionApplied`, `SpecialActivated`, `PlayerDowned`, `MatchEnd` | confiável | por evento |
| S→C | `HostBackgrounded(bool)` | confiável | ao mudar |

O pedido de especial usa um **contador** dentro do `Input`: o host ativa quando o contador aumenta e a recarga permite. Assim, um pacote perdido não perde a ativação.

### 8.4 Conteúdo do snapshot

- **Jogadores:** posição, velocidade, vida, flags, último `seq` processado, recarga do especial, nível, XP.
- **Inimigos:** ID (2 B), x e y em ponto fixo de 16 bits sobre os limites da arena (2 + 2 B), tipo + flags (elite, atordoado, piscar de dano) (1 B) = **7 B**. Elites levam +1 B de vida.
- **Projéteis hostis:** ID, posição, tipo.
- **Eventos cosméticos agregados** (números de dano, faíscas): lista limitada a 48 por snapshot; perda é aceitável.
- Projéteis dos jogadores **não** vão no snapshot: os clientes recebem eventos de disparo e simulam o visual localmente; acertos são calculados só no host. Braços orbitais e auras são derivados do estado do jogador.
- Estimativa: 300 inimigos × 7 B × 15 Hz ≈ 31 KB/s por cliente; 4 jogadores ≈ 340 MB/h. Painel de desempenho mostra bytes/s reais.

### 8.5 Cliente

- Inimigos e projéteis hostis renderizados com **interpolação** 100 ms atrás (adaptável até 200 ms se houver atraso, já que WebSocket é TCP e sofre bloqueio de fila).
- Jogador local: **previsão** com o mesmo código de movimento da Sim; histórico de entradas; ao receber o snapshot, reaplica as entradas não confirmadas; erro < 0,05 u é ignorado, acima disso é corrigido suavemente em 100 ms.
- Um inimigo passa a existir no cliente quando aparece pela primeira vez num bloco de snapshot e só é removido pelo evento confiável de morte/remoção. Cada bloco é autocontido; um bloco atrasado ou descartado deixa apenas posições momentaneamente desatualizadas, nunca inimigos "fantasmas" ou sumidos.

### 8.6 Validação no host

- Vetor de movimento limitado a comprimento 1; contador de especial monotônico; escolha precisa corresponder à oferta pendente e a um índice válido.
- Limite de 60 entradas/s por cliente; mensagens malformadas são descartadas e contadas; acima de um limite, o cliente é desconectado.
- Clientes nunca informam dano, abates, XP ou posição de inimigos.

### 8.7 Falhas e mensagens ao usuário

| Situação | Comportamento |
|---|---|
| Código inválido ou expirado | "Código inválido ou sala expirada." |
| Sala cheia | "Sala cheia (4/4)." |
| Partida já iniciada | "A partida já começou." |
| Versão diferente | "Versão diferente do anfitrião. Recarregue a página (Ctrl+F5)." |
| Conexão perdida / host saiu | Partida encerrada para o cliente: "Conexão perdida com o anfitrião." Volta ao menu com as estatísticas. |
| Cliente desconectou | Personagem fica inativo (não é alvo, não conta para derrota). Balanceamento inalterado. Reconexão: SP3. |
| Aba do host oculta | Biblioteca `.jslib` detecta `visibilitychange`; host envia `HostBackgrounded`; clientes veem o aviso "O anfitrião minimizou o jogo: a partida pode travar." Pausa automática: SP3. |

---

## 9. Interface do SP1

UI Toolkit, textos em português (Brasil) numa tabela de strings por chave, pronta para tradução.

| Tela | Conteúdo |
|---|---|
| Menu principal | Jogar Solo, Cooperativo Online, Configurações, Créditos. |
| Preparação solo | Seleção de especialização (2), dificuldade (3) com descrição, Iniciar. |
| Cooperativo | Criar sala / Entrar com código (6 caracteres). |
| Lobby mínimo | Código + botão copiar, lista de participantes com especialização, quantidade conectada, dificuldade (host escolhe; demais veem), aviso "O tamanho da equipe também ajusta o balanceamento", Iniciar (host). |
| HUD | Vida, barra de XP, nível, tempo, especial com recarga radial, habilidades e níveis, indicador de sinergia ativa, progresso de evoluções (descobríveis como "???"), vida dos aliados, retrato com emoção. |
| Subida de nível | 3 cartas: nome, ícone, raridade, descrição, atributos que mudam, tags, aviso de sinergia/evolução relacionada. Teclas 1/2/3 ou mouse. |
| Pausa (solo) / Menu (coop, sem pausar) | Continuar, Configurações, Sair da partida. |
| Resultado | Vitória/derrota, tempo, abates, nível, dano por habilidade. |
| Configurações | Volume geral/música/efeitos, intensidade de efeitos (baixa/média/alta), tremor de câmera (0–100%), escala da interface (80–130%), tecla do especial (padrão Espaço), tela cheia. Persistidas localmente. |
| Créditos | Autoria, ferramentas, licença da fonte. |
| Painel de desempenho (F3) | FPS, tempo de quadro, entidades, bytes/s enviados e recebidos, latência. |

Controles: WASD/setas para mover, Espaço (configurável) para o especial, Esc para pausa/menu, mouse na interface.

---

## 10. Arte e áudio

> **Diretriz obrigatória do autor (2026-10-24): pixel art nativo 32×32.** Substitui a direção vetorial anterior.

### 10.1 Direção

- Pixel art nativo em todo o jogo: personagens **32×32 px** (resolução nativa, sem redução de arte maior, sem vetores imitando pixel art, sem suavização).
- Paleta fixa e documentada ("noir industrial": base escura e dessaturada; acentos saturados por especialização); sombras definidas e contorno escuro de 1 px.
- Silhuetas distintas para as **7 engenharias** (arte de todas desde o SP1; jogabilidade das 5 restantes no SP2). Diversidade de gênero, corpo e tom de pele.
- Profissionais exaustos: roupas rasgadas/sujas, olheiras, curativos, suor — no limite do legível em 32×32. Expressões completas vão nos **retratos 64×64** (mesmo estilo).
- Inimigos no mesmo padrão: microbô 16×16, torreta 32×32, prensa 48×48; elite com recoloração, contorno magenta e brilho em pixels. Chefes (SP5) maiores, mesma linguagem.
- Tiles 32×32 com variações; efeitos (faíscas, arcos, laser, explosões em quadros, campo, gemas) em pixel art com tamanhos próprios.

### 10.2 Pipeline

1. `ArtSource/` contém mapas de pixels escritos à mão (ASCII indexado pela paleta) e regras de composição em camadas (corpo, roupa, cabeça, acessórios, ferramenta).
2. Um script Node grava cada quadro **pixel a pixel** em PNG (sem rasterizador vetorial, sem redimensionamento). Um comando regera tudo.
3. Animações por personagem: parado (4 direções × 2), caminhada (4 direções × 4), ataque (3), especial (3), dano (2), atordoado (2), queda (3), reviver aliado (2), vitória (2). Direita = espelho exato da esquerda.
4. Um quadro por arquivo, organizados por personagem e animação; `ANIMACOES.md` gerado documenta a estrutura e o registro do que está concluído.
5. Pastas: `Art/Characters`, `Enemies`, `Bosses`, `Weapons`, `Abilities`, `Effects`, `Tilesets`, `Environment`, `UI`, `Portraits`.
6. Testes automáticos: dimensões nativas, alfa só 0 ou 255 (sem suavização), cores dentro da paleta, base dos pés alinhada em todos os quadros, silhuetas das 7 engenharias distintas entre si, determinismo.

### 10.3 Unity

- Importação: filtro Point, sem compressão, sem mipmaps, **PPU 32** para tudo, pivôs em pixels inteiros (pés).
- Câmera: `UnityEngine.Rendering.Universal.PixelPerfectCamera`, referência **640×360**, `gridSnapping = PixelSnapping`; AA/MSAA/HDR desligados; escala inteira sempre que possível.
- Interface: fonte pixelada com licença OFL, em tamanho confortável de leitura.
- Verificação na build WebGL, não só no editor.

### 10.4 Áudio

- Sintetizador próprio em Node (osciladores, ruído, envelopes, filtros) → WAV; parâmetros versionados. 17 efeitos e 1 loop de música no SP1.
- AudioMixer: Música, Efeitos, Interface; limite de vozes simultâneas por som.

Terceiros: apenas uma fonte com licença OFL (Google Fonts), registrada em `CREDITS.md`.

---

## 11. Riscos e mitigação

| # | Risco | Mitigação |
|---|---|---|
| R1 | Host WebGL via Relay em cliente-servidor não funcionar na prática com as versões atuais | **Prova de rede antecipada**, logo após o núcleo da Sim: build WebGL mínima, 2 abas, host e cliente via Relay `wss`. Se falhar: (a) testar versões de pacote; (b) host em build desktop Windows com clientes no navegador; (c) servidor dedicado em VM gratuita (ex.: Oracle Cloud Always Free). Qualquer desvio volta para você decidir. |
| R2 | Aba oculta trava o host | Aviso no SP1; pausa controlada no SP3. Documentado em `LIMITACOES.md`. |
| R3 | Desempenho WebGL (uma thread) com hordas | Sim orientada a dados, tetos de entidades, pool de visuais, atlas de sprites, painel de desempenho, medição antecipada com 400 inimigos. |
| R4 | Cota de banda do Relay | Snapshots compactos, eventos agregados, medição de bytes/s. |
| R5 | Qualidade da arte gerada | Limitação aceita; pipeline permite troca sem código. |
| R6 | Mudanças de API nos pacotes Unity | Versões fixadas no `manifest.json`; prova de rede valida as versões escolhidas. |
| R7 | WebSocket (TCP) com perda de pacotes causa picos | Buffer de interpolação adaptativo (100–200 ms). |

---

## 12. Testes

### 12.1 Sim (`dotnet test`, NUnit, fora da Unity)

- **Unitários:** `StatBlock` (ordem e soma/multiplicação de modificadores), dano, recargas, curva de XP; gerador de ofertas (sem repetição, níveis máximos, limites de espaço, evolução só com requisitos atendidos); sinergias (ativa/desativa); afinidade (3 estágios); grade espacial (consultas corretas); codecs (ida e volta; erro de quantização ≤ 1/1024 u); validação (movimento acima de 1, opção inválida, especial em recarga, contador regredindo).
- **Balanceamento:** as 12 células existem; Difícil > Normal > Fácil em dano inimigo; vida por jogador sublinear (hp(n)/n decresce); ajuste por célula aplicado.
- **Partidas de bots** (semente fixa, velocidade acelerada): bot foge do perigo, coleta gemas, escolhe por heurística. Fumaça do SP1: 12 combinações × 2 especializações × 3 sementes, sem exceções, tetos respeitados; métricas em CSV via `Tools/BalanceRunner` (base do SP4).

### 12.2 Unity EditMode

Validação de conteúdo: IDs únicos, referências de evolução/sinergia válidas, ícones presentes, tabela de balanceamento completa, hash de conteúdo estável.

### 12.3 Unity PlayMode

Host + cliente NGO no mesmo processo: conexão e `Hello/Welcome`, rejeição por versão, movimento replicado, snapshot de inimigos chegando, oferta → escolha → `ChoiceApplied`, especial, fim de partida.

### 12.4 Testes reais

- **Claude:** build WebGL publicada aberta em 2 abas do navegador (host e cliente, via Relay real); console sem erros; painel de desempenho.
- **Autor:** 2 computadores diferentes pela URL pública, seguindo `docs/TESTES.md`.
- **Metas:** 60 FPS com 300 inimigos em desktop intermediário (mínimo aceitável 45 FPS com 400); download inicial ≤ 25 MB comprimido; memória ≤ 512 MB.
- **Navegadores-alvo:** Chrome, Edge e Firefox de desktop (2 últimas versões). Safari sem garantia. Celular fora do alvo.

---

## 13. Build, hospedagem e pré-requisitos

### 13.1 Pré-requisitos

| Item | Estado |
|---|---|
| Unity Hub (em `Z:\Unity\Unity Hub`), login e licença Personal | Feito pelo autor. |
| **Unity 6.3 LTS** com **Web Build Support** em `Z:\Unity\Editors` | Instalação pelo Claude via Hub CLI. A Unity 6.6 (6000.6.7f1, não LTS) instalada pelo Hub fica sem uso. |
| **.NET SDK** LTS em `Z:\Dev\tools\dotnet` (script oficial `dotnet-install.ps1`) | Instalação pelo Claude. |
| **Unity Cloud:** projeto `cd18869f-06b2-4e03-bc30-32ecff7d27a2` ("Engineering Survivors 2026-10-24_17-43-24"), organização `vitordonasan` | Criado pelo Hub. IDs públicos por natureza. |
| Relay e Lobby | **Verificados ativos** em 2026-10-24 pela API REST com autenticação anônima: Lobby criou/apagou sala privada; Relay alocou servidor com endpoint `wss` e gerou código de convite. |
| Conta gratuita na hospedagem + login pela CLI | Na publicação (login somente pelo autor). |

O projeto Unity criado pelo Hub em `Engineering Survivors\` (template 3D, Unity 6.6, Plastic) é substituído por um projeto 2D na raiz do repositório, ligado ao mesmo projeto do Unity Cloud; a pasta antiga vai para a Lixeira.

### 13.2 Build WebGL

- Compressão Brotli; *code stripping* alto; texturas comprimidas; template próprio (`Assets/WebGLTemplates/ES`) com tela de carregamento, aviso de navegador incompatível e mensagens de erro.
- Script de build em batchmode (`Tools/build-webgl.ps1`).

### 13.3 Hospedagem gratuita

- **1ª opção:** Cloudflare Pages (HTTPS; `_headers` com `Content-Encoding: br` e tipos corretos para `.wasm`/`.data`/`.js`; limite de 25 MiB por arquivo).
- **Plano B:** GitHub Pages com *Decompression Fallback* da Unity.
- Limites atuais conferidos no momento da publicação. Nenhuma URL é anunciada antes de publicada e verificada.

### 13.4 Segurança

- O cliente contém apenas o ID do projeto Unity Cloud. Nenhuma chave de serviço no build.
- Nenhum dado pessoal armazenado; configurações ficam no navegador (PlayerPrefs → IndexedDB).

---

## 14. Critério de conclusão do SP1

1. `dotnet test` verde; testes EditMode e PlayMode verdes.
2. Fumaça de bots nas 12 combinações sem falhas, CSV gerado.
3. Build WebGL publicada em URL pública com HTTPS.
4. Teste em 2 abas feito por Claude (registrado em `docs/TESTES.md`).
5. Teste em 2 computadores feito pelo autor e relatado.
6. Metas de desempenho medidas e registradas (atingidas ou com o desvio declarado).
7. `docs/COMO-RODAR.md` e `docs/LIMITACOES.md` escritos.
8. Nenhum botão sem função.

## 15. Limitações conhecidas iniciais

- Se o host fechar a aba, a partida termina para todos (sem migração de host).
- Aba do host oculta trava a simulação (aviso no SP1; pausa no SP3).
- Sem reviver e sem reconexão no SP1.
- Arte e áudio gerados por código; qualidade abaixo de um artista dedicado.
- Valores de balanceamento iniciais não testados até o SP4.
- Sem suporte a celular e controle.

## 16. Fontes

- Unity 6 releases: https://unity.com/releases/unity-6
- Unity Transport 2.5 — WebSockets: https://docs.unity3d.com/Packages/com.unity.transport@2.5/manual/websockets.html
- Relay + NGO: https://docs.unity.com/relay/relay-and-ngo
- NGO Transports / SinglePlayerTransport: https://mp-docs.dl.it.unity3d.com/netcode/current/advanced-topics/transports/
- Multiplayer Services — Sessions: https://docs.unity.com/mps-sdk/sessions
- Issue NGO #4111: https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/issues/4111
- Issue tracker UGSB-9 (aba em segundo plano): https://issuetracker.unity.com/issues/22707/webgl-client-encounters-connection-errors-when-player-tab-runs-in-the-background
- Preços UGS: https://unity.com/products/gaming-services/pricing
