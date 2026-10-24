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

## 2026-10-24 — Prova de rede WebGL (Task 20) — **PENDENTE (bloqueio de rede da máquina)**

- Build WebGL da cena de prova: **OK** (Unity 6000.3.27f1, Brotli, 12 MB, 0 erros) em `Build/WebGL-proof`.
- Teste automatizado criado: `node Tools/netproof/proof.mjs Build/WebGL-proof 8081` — duas páginas Chromium em contextos separados (identidades anônimas diferentes): host cria a sala (`?mode=host&autostart=2`), cliente entra pelo código, cliente anda e o teste compara o estado das duas (ticks, inimigos, posição do cliente no host). Relatório em `Build/netproof/report.json`.
- Resultado: **não executado até o fim** — a autenticação anônima da Unity dá *timeout*. Diagnóstico: a rota padrão IPv4 da máquina está na interface **ZeroTier One**; o DNS resolve, mas TCP 443 para `player-auth.services.api.unity.com` (IPv4) e `api.github.com` não conecta; `www.google.com` funciona (IPv6). No início da sessão as mesmas chamadas funcionavam.
- Ação necessária do autor: desconectar o ZeroTier (ou desativar a rota padrão "Allow Default" nessa rede) e rodar o comando acima de novo.
- Observação: o painel de navegador do app suspende abas ocultas e não alcançou os serviços externos; por isso a prova usa Chromium sem interface.
